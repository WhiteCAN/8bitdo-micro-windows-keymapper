using MicroKeyStudio.Protocol.Packets;

namespace MicroKeyStudio.Ble;

public interface IBleTransport : IAsyncDisposable
{
    event EventHandler<byte[]>? NotificationReceived;

    Task<IReadOnlyList<MicroBleDevice>> ScanAsync(CancellationToken cancellationToken);

    Task ConnectAsync(MicroBleDevice device, CancellationToken cancellationToken);

    Task WriteSequenceAsync(PacketSequence sequence, TimeSpan delayBetweenWrites, CancellationToken cancellationToken);

    Task<IReadOnlyList<byte[]>> WriteSequenceAndCollectNotificationsAsync(
        PacketSequence sequence,
        Func<byte[], bool> shouldCollect,
        int expectedCount,
        TimeSpan timeout,
        TimeSpan delayBetweenWrites,
        CancellationToken cancellationToken);

    Task DisconnectAsync();
}
