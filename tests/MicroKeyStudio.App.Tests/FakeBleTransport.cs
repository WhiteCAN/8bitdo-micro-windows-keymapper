using MicroKeyStudio.Ble;
using MicroKeyStudio.Protocol.Packets;

namespace MicroKeyStudio.App.Tests;

internal enum FakeBleInvocationKind
{
    Connect,
    Collect,
    Write,
    Disconnect
}

internal sealed record FakeBleInvocation(
    FakeBleInvocationKind Kind,
    PacketSequence? Sequence,
    TimeSpan DelayBetweenWrites,
    CancellationToken CancellationToken,
    bool IsConnected);

internal sealed class FakeBleTransport : IBleTransport, IDisposable
{
    private readonly Queue<NotificationBatch> _notificationBatches = new();
    private readonly List<PacketSequence> _collectedSequences = new();
    private readonly List<PacketSequence> _writtenSequences = new();
    private readonly List<FakeBleInvocation> _invocations = new();
    private IReadOnlyList<MicroBleDevice> _scanResults = Array.Empty<MicroBleDevice>();
    private Exception? _connectException;
    private Exception? _writeException;
    private NotificationBatch? _blockedNotificationBatch;
    private bool _isConnected;

    public event EventHandler<byte[]>? NotificationReceived;

    public PacketSequence? LastCollectedSequence { get; private set; }

    public PacketSequence? LastWrittenSequence { get; private set; }

    public IReadOnlyList<PacketSequence> CollectedSequences => _collectedSequences;

    public IReadOnlyList<PacketSequence> WrittenSequences => _writtenSequences;

    public IReadOnlyList<FakeBleInvocation> Invocations => _invocations;

    public IReadOnlyList<FakeBleInvocation> WriteInvocations => _invocations
        .Where(invocation => invocation.Kind == FakeBleInvocationKind.Write)
        .ToArray();

    public MicroBleDevice? LastConnectedDevice { get; private set; }

    public Task<IReadOnlyList<MicroBleDevice>> ScanAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(_scanResults);
    }

    public Task ConnectAsync(MicroBleDevice device, CancellationToken cancellationToken)
    {
        LastConnectedDevice = device;
        if (_connectException is not null)
        {
            throw _connectException;
        }

        _isConnected = true;
        _invocations.Add(new FakeBleInvocation(
            FakeBleInvocationKind.Connect,
            null,
            TimeSpan.Zero,
            cancellationToken,
            _isConnected));
        return Task.CompletedTask;
    }

    public Task WriteSequenceAsync(PacketSequence sequence, TimeSpan delayBetweenWrites, CancellationToken cancellationToken)
    {
        LastWrittenSequence = sequence;
        _writtenSequences.Add(sequence);
        _invocations.Add(new FakeBleInvocation(
            FakeBleInvocationKind.Write,
            sequence,
            delayBetweenWrites,
            cancellationToken,
            _isConnected));
        if (_writeException is not null)
        {
            throw _writeException;
        }

        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<byte[]>> WriteSequenceAndCollectNotificationsAsync(
        PacketSequence sequence,
        Func<byte[], bool> shouldCollect,
        int expectedCount,
        TimeSpan timeout,
        TimeSpan delayBetweenWrites,
        CancellationToken cancellationToken)
    {
        LastCollectedSequence = sequence;
        _collectedSequences.Add(sequence);
        _invocations.Add(new FakeBleInvocation(
            FakeBleInvocationKind.Collect,
            sequence,
            delayBetweenWrites,
            cancellationToken,
            _isConnected));
        NotificationBatch batch = _notificationBatches.Count > 0
            ? _notificationBatches.Dequeue()
            : new NotificationBatch(Array.Empty<byte[]>());
        batch.Started?.TrySetResult();
        if (batch.Release is not null)
        {
            await batch.Release.Task;
        }

        IReadOnlyList<byte[]> collected = batch.Packets
            .Where(shouldCollect)
            .Take(expectedCount)
            .ToArray();
        return collected;
    }

    public Task DisconnectAsync()
    {
        _invocations.Add(new FakeBleInvocation(
            FakeBleInvocationKind.Disconnect,
            null,
            TimeSpan.Zero,
            CancellationToken.None,
            _isConnected));
        _isConnected = false;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
    }

    public void RaiseNotification(byte[] data)
    {
        NotificationReceived?.Invoke(this, data);
    }

    public void QueueCollectedNotifications(params byte[][] notifications)
    {
        QueueCollectedNotificationBatch(notifications);
    }

    public void QueueCollectedNotificationBatch(params byte[][] packets)
    {
        _notificationBatches.Enqueue(new NotificationBatch(ClonePackets(packets)));
    }

    public void QueueBlockedCollectedNotificationBatch(params byte[][] packets)
    {
        if (_blockedNotificationBatch is not null)
        {
            throw new InvalidOperationException("Only one blocked notification batch can be queued at a time.");
        }

        _blockedNotificationBatch = new NotificationBatch(
            ClonePackets(packets),
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        _notificationBatches.Enqueue(_blockedNotificationBatch);
    }

    public Task WaitForBlockedCollectionAsync()
    {
        return _blockedNotificationBatch?.Started?.Task
            ?? throw new InvalidOperationException("No blocked notification batch is queued.");
    }

    public void ReleaseBlockedCollection()
    {
        _blockedNotificationBatch?.Release?.TrySetResult();
        _blockedNotificationBatch = null;
    }

    public void QueueScanResults(params MicroBleDevice[] devices)
    {
        _scanResults = devices;
    }

    public void ThrowOnConnect(Exception exception)
    {
        _connectException = exception;
    }

    public void ThrowOnWrite(Exception exception)
    {
        _writeException = exception;
    }

    private static IReadOnlyList<byte[]> ClonePackets(IEnumerable<byte[]> packets)
    {
        return packets.Select(packet => packet.ToArray()).ToArray();
    }

    private sealed record NotificationBatch(
        IReadOnlyList<byte[]> Packets,
        TaskCompletionSource? Started = null,
        TaskCompletionSource? Release = null);
}
