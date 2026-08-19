using MicroKeyStudio.Protocol.Packets;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace MicroKeyStudio.Ble;

public sealed class WindowsBleTransport : IBleTransport
{
    private BluetoothLEDevice? _device;
    private GattCharacteristic? _writeCharacteristic;

    public event EventHandler<byte[]>? NotificationReceived;

    public async Task<IReadOnlyList<MicroBleDevice>> ScanAsync(CancellationToken cancellationToken)
    {
        var devices = new Dictionary<ulong, MicroBleDevice>();
        var watcher = new BluetoothLEAdvertisementWatcher
        {
            ScanningMode = BluetoothLEScanningMode.Active
        };

        watcher.Received += (_, args) =>
        {
            string name = args.Advertisement.LocalName;
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            if (name.Contains("8BitDo", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("80", StringComparison.OrdinalIgnoreCase))
            {
                devices[args.BluetoothAddress] = new MicroBleDevice(
                    args.BluetoothAddress.ToString("X"),
                    name,
                    args.RawSignalStrengthInDBm);
            }
        };

        watcher.Start();
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }
        finally
        {
            watcher.Stop();
        }

        return devices.Values.OrderBy(device => device.Name).ToArray();
    }

    public async Task ConnectAsync(MicroBleDevice device, CancellationToken cancellationToken)
    {
        if (!ulong.TryParse(device.Id, System.Globalization.NumberStyles.HexNumber, null, out ulong address))
        {
            throw new InvalidOperationException($"Device id '{device.Id}' is not a BLE address.");
        }

        await DisconnectAsync();
        _device = await BluetoothLEDevice.FromBluetoothAddressAsync(address).AsTask(cancellationToken);
        if (_device is null)
        {
            throw new InvalidOperationException("Windows could not open the BLE device.");
        }

        GattDeviceService? service = await FindMicroServiceAsync(_device, cancellationToken);

        if (service is null)
        {
            string availableServices = await DescribeAvailableServicesAsync(_device, cancellationToken);
            throw new InvalidOperationException(
                $"Micro service was not found. Available services: {availableServices}");
        }

        GattCharacteristicsResult characteristics = await service.GetCharacteristicsForUuidAsync(
            MicroProtocolConstants.WriteCharacteristicUuid,
            BluetoothCacheMode.Uncached).AsTask(cancellationToken);

        if (characteristics.Status != GattCommunicationStatus.Success || characteristics.Characteristics.Count == 0)
        {
            string availableCharacteristics = await DescribeAvailableCharacteristicsAsync(service, cancellationToken);
            throw new InvalidOperationException(
                $"Micro write characteristic was not found. Status: {characteristics.Status}. Available characteristics: {availableCharacteristics}");
        }

        _writeCharacteristic = characteristics.Characteristics[0];
        _writeCharacteristic.ValueChanged += OnValueChanged;

        await _writeCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
            GattClientCharacteristicConfigurationDescriptorValue.Notify).AsTask(cancellationToken);
    }

    private static async Task<GattDeviceService?> FindMicroServiceAsync(BluetoothLEDevice device, CancellationToken cancellationToken)
    {
        GattDeviceServicesResult services = await device.GetGattServicesForUuidAsync(
            MicroProtocolConstants.ServiceUuid,
            BluetoothCacheMode.Uncached).AsTask(cancellationToken);

        if (services.Status == GattCommunicationStatus.Success && services.Services.Count > 0)
        {
            return services.Services[0];
        }

        GattDeviceServicesResult allServices = await device.GetGattServicesAsync(BluetoothCacheMode.Uncached).AsTask(cancellationToken);
        if (allServices.Status != GattCommunicationStatus.Success)
        {
            return null;
        }

        Guid? selectedUuid = GattUuidSelector.SelectMicroServiceUuid(allServices.Services.Select(service => service.Uuid));
        return selectedUuid is null
            ? null
            : allServices.Services.FirstOrDefault(service => service.Uuid == selectedUuid);
    }

    private static async Task<string> DescribeAvailableServicesAsync(BluetoothLEDevice device, CancellationToken cancellationToken)
    {
        GattDeviceServicesResult allServices = await device.GetGattServicesAsync(BluetoothCacheMode.Uncached).AsTask(cancellationToken);
        if (allServices.Status != GattCommunicationStatus.Success)
        {
            return $"service query failed ({allServices.Status})";
        }

        if (allServices.Services.Count == 0)
        {
            return "(none)";
        }

        return string.Join(", ", allServices.Services.Select(service => service.Uuid));
    }

    private static async Task<string> DescribeAvailableCharacteristicsAsync(GattDeviceService service, CancellationToken cancellationToken)
    {
        GattCharacteristicsResult allCharacteristics = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached).AsTask(cancellationToken);
        if (allCharacteristics.Status != GattCommunicationStatus.Success)
        {
            return $"characteristic query failed ({allCharacteristics.Status})";
        }

        if (allCharacteristics.Characteristics.Count == 0)
        {
            return "(none)";
        }

        return string.Join(", ", allCharacteristics.Characteristics.Select(characteristic => $"{characteristic.Uuid} [{characteristic.CharacteristicProperties}]"));
    }

    public async Task WriteSequenceAsync(PacketSequence sequence, TimeSpan delayBetweenWrites, CancellationToken cancellationToken)
    {
        if (_writeCharacteristic is null)
        {
            throw new InvalidOperationException("Device is not connected.");
        }

        foreach (byte[] packet in sequence.Writes)
        {
            using var writer = new DataWriter();
            writer.WriteBytes(packet);
            GattCommunicationStatus status = await _writeCharacteristic.WriteValueAsync(
                writer.DetachBuffer(),
                GattWriteOption.WriteWithResponse).AsTask(cancellationToken);

            if (status != GattCommunicationStatus.Success)
            {
                throw new InvalidOperationException($"BLE write failed with status {status}.");
            }

            await Task.Delay(delayBetweenWrites, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<byte[]>> WriteSequenceAndCollectNotificationsAsync(
        PacketSequence sequence,
        Func<byte[], bool> shouldCollect,
        int expectedCount,
        TimeSpan timeout,
        TimeSpan delayBetweenWrites,
        CancellationToken cancellationToken)
    {
        var collected = new List<byte[]>();
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        using var completeSource = new CancellationTokenSource();
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutSource.Token, completeSource.Token);

        void Handler(object? _, byte[] packet)
        {
            if (!shouldCollect(packet))
            {
                return;
            }

            lock (collected)
            {
                if (collected.Count >= expectedCount)
                {
                    return;
                }

                collected.Add(packet.ToArray());
                if (collected.Count >= expectedCount)
                {
                    completeSource.Cancel();
                }
            }
        }

        NotificationReceived += Handler;
        try
        {
            await WriteSequenceAsync(sequence, delayBetweenWrites, cancellationToken);
            try
            {
                await Task.Delay(timeout, linkedSource.Token);
            }
            catch (OperationCanceledException) when (completeSource.IsCancellationRequested)
            {
            }
            catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
            }
        }
        finally
        {
            NotificationReceived -= Handler;
        }

        lock (collected)
        {
            return collected.ToArray();
        }
    }

    public Task DisconnectAsync()
    {
        if (_writeCharacteristic is not null)
        {
            _writeCharacteristic.ValueChanged -= OnValueChanged;
            _writeCharacteristic = null;
        }

        _device?.Dispose();
        _device = null;
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }

    private void OnValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        byte[] data = new byte[args.CharacteristicValue.Length];
        DataReader.FromBuffer(args.CharacteristicValue).ReadBytes(data);
        NotificationReceived?.Invoke(this, data);
    }
}
