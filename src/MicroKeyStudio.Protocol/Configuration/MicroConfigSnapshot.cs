namespace MicroKeyStudio.Protocol.Configuration;

public sealed class MicroConfigSnapshot
{
    public const int DisableSleepOffset = 0x03;
    private static readonly uint[] ExpectedOffsets = { 0x00, 0x2d, 0x5a, 0x87 };

    private MicroConfigSnapshot(byte[] payload, IReadOnlyList<MicroKeyValue> keyboardValues)
    {
        _payload = payload;
        KeyboardValues = keyboardValues;
    }

    private readonly byte[] _payload;

    public byte[] Payload => CopyPayload();

    public IReadOnlyList<MicroKeyValue> KeyboardValues { get; }

    public bool IsDisableSleepValueKnown => _payload[DisableSleepOffset] is 0x00 or 0x01;

    public bool DisableSleep => _payload[DisableSleepOffset] == 0x01;

    public static MicroConfigSnapshot FromPayload(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Length != 180)
        {
            throw new ArgumentException("A Micro configuration payload must contain exactly 180 bytes.", nameof(payload));
        }

        byte[] ownedPayload = payload.ToArray();
        return new MicroConfigSnapshot(ownedPayload, DecodeKeyboardValues(ownedPayload));
    }

    public byte[] CopyPayload()
    {
        return _payload.ToArray();
    }

    public static bool IsConfigPage(byte[] packet)
    {
        return packet.Length >= 17 && packet[0] == 0x04 && packet[1] == 0x00 && packet[2] == 0x02;
    }

    public static bool TryCreate(IEnumerable<byte[]> packets, out MicroConfigSnapshot? snapshot)
    {
        snapshot = null;
        Dictionary<uint, byte[]> pages = packets
            .Where(IsConfigPage)
            .Select(TryReadPage)
            .Where(page => page is not null)
            .ToDictionary(page => page!.Value.Offset, page => page!.Value.Payload);

        if (ExpectedOffsets.Any(offset => !pages.ContainsKey(offset)))
        {
            return false;
        }

        byte[] payload = ExpectedOffsets
            .SelectMany(offset => pages[offset])
            .ToArray();

        snapshot = FromPayload(payload);
        return true;
    }

    private static (uint Offset, byte[] Payload)? TryReadPage(byte[] packet)
    {
        byte payloadLength = packet[4];
        if (payloadLength != 45 || packet.Length < 16 + payloadLength)
        {
            return null;
        }

        uint offset = BitConverter.ToUInt32(packet, 12);
        byte[] payload = packet.Skip(16).Take(payloadLength).ToArray();
        return (offset, payload);
    }

    private static IReadOnlyList<MicroKeyValue> DecodeKeyboardValues(byte[] payload)
    {
        int count = payload.Length / 4;
        var values = new List<MicroKeyValue>(count);
        for (int index = 0; index < count; index++)
        {
            byte[] bytes = payload.Skip(index * 4).Take(4).ToArray();
            uint rawValue = BitConverter.ToUInt32(bytes);
            values.Add(new MicroKeyValue(index, rawValue, MicroHidCodec.Decode(bytes)));
        }

        return values;
    }

}

public sealed record MicroKeyValue(int Slot, uint RawValue, string DisplayText);
