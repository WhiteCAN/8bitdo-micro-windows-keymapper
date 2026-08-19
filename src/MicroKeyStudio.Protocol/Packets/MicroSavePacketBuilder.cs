using System.Buffers.Binary;
using MicroKeyStudio.Protocol.Configuration;

namespace MicroKeyStudio.Protocol.Packets;

public sealed record MicroSaveBuildResult(
    PacketSequence Sequence,
    byte[] ExpectedPayload,
    IReadOnlyList<MicroMappingChange> Changes);

public sealed record MicroMappingChange(int Slot, string Action);

public sealed class MicroMappingEncodingException : Exception
{
    public MicroMappingEncodingException(IReadOnlyList<int> invalidSlots)
        : base($"Could not encode mappings for slots: {string.Join(", ", invalidSlots)}.")
    {
        InvalidSlots = invalidSlots;
    }

    public IReadOnlyList<int> InvalidSlots { get; }
}

public static class MicroSavePacketBuilder
{
    private static readonly uint[] PageOffsets = { 0x00, 0x2d, 0x5a, 0x87 };
    private const int SlotCount = 45;
    private const int UsagesPerSlot = 4;
    private const int PagePayloadLength = 45;

    public static MicroSaveBuildResult Build(
        MicroConfigSnapshot baseline,
        IReadOnlyDictionary<int, string> actionsBySlot)
    {
        return BuildCore(baseline, actionsBySlot, disableSleep: null);
    }

    public static MicroSaveBuildResult Build(
        MicroConfigSnapshot baseline,
        IReadOnlyDictionary<int, string> actionsBySlot,
        bool disableSleep)
    {
        return BuildCore(baseline, actionsBySlot, disableSleep);
    }

    private static MicroSaveBuildResult BuildCore(
        MicroConfigSnapshot baseline,
        IReadOnlyDictionary<int, string> actionsBySlot,
        bool? disableSleep)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(actionsBySlot);

        IReadOnlyList<(int Slot, string Action, byte[] Usages)> encodedMappings = EncodeMappings(actionsBySlot);
        byte[] expectedPayload = baseline.CopyPayload();
        var changes = new List<MicroMappingChange>(encodedMappings.Count);
        foreach ((int slot, string action, byte[] usages) in encodedMappings)
        {
            usages.CopyTo(expectedPayload, slot * UsagesPerSlot);
            changes.Add(new MicroMappingChange(slot, action));
        }

        if (disableSleep.HasValue)
        {
            expectedPayload[MicroConfigSnapshot.DisableSleepOffset] = disableSleep.Value ? (byte)0x01 : (byte)0x00;
        }

        return new MicroSaveBuildResult(BuildPages(expectedPayload), expectedPayload, changes.ToArray());
    }

    public static PacketSequence BuildFromPayload(MicroConfigSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return BuildPages(snapshot.CopyPayload());
    }

    public static bool MatchesSlots(
        MicroConfigSnapshot snapshot,
        IReadOnlyDictionary<int, string> actionsBySlot,
        out IReadOnlyList<int> mismatchedSlots)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(actionsBySlot);

        IReadOnlyList<(int Slot, string Action, byte[] Usages)> encodedMappings = EncodeMappings(actionsBySlot);
        byte[] payload = snapshot.CopyPayload();
        mismatchedSlots = encodedMappings
            .Where(mapping => !payload.AsSpan(mapping.Slot * UsagesPerSlot, UsagesPerSlot).SequenceEqual(mapping.Usages))
            .Select(mapping => mapping.Slot)
            .ToArray();
        return mismatchedSlots.Count == 0;
    }

    private static IReadOnlyList<(int Slot, string Action, byte[] Usages)> EncodeMappings(
        IReadOnlyDictionary<int, string> actionsBySlot)
    {
        var encodedMappings = new List<(int Slot, string Action, byte[] Usages)>();
        var invalidSlots = new List<int>();
        foreach ((int slot, string action) in actionsBySlot.OrderBy(pair => pair.Key))
        {
            if (slot < 0 || slot >= SlotCount ||
                !MicroHidCodec.TryEncode(action, out byte[] usages, out _) ||
                usages.Length != UsagesPerSlot)
            {
                invalidSlots.Add(slot);
                continue;
            }

            encodedMappings.Add((slot, action, usages));
        }

        if (invalidSlots.Count > 0)
        {
            throw new MicroMappingEncodingException(invalidSlots.ToArray());
        }

        return encodedMappings;
    }

    private static PacketSequence BuildPages(byte[] payload)
    {
        var pages = PageOffsets
            .Select(offset => BuildPage(offset, payload.AsSpan((int)offset, PagePayloadLength)))
            .ToList();
        pages.Add(KnownMicroPackets.CreateObservedSaveCommitPacket());
        return new PacketSequence("Micro save sequence", pages);
    }

    private static byte[] BuildPage(uint offset, ReadOnlySpan<byte> payload)
    {
        byte[] page = new byte[17 + PagePayloadLength];
        page[0] = 0x04;
        page[1] = 0x01;
        page[5] = PagePayloadLength;
        ushort crc = MicroCrc16.Compute(payload);
        page[7] = (byte)crc;
        page[8] = (byte)(crc >> 8);
        page[9] = 0xb4;
        BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(13), offset);
        payload.CopyTo(page.AsSpan(17));
        return page;
    }
}
