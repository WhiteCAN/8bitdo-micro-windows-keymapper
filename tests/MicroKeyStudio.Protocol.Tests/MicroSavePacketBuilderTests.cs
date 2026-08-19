using MicroKeyStudio.Protocol.Configuration;
using MicroKeyStudio.Protocol.Packets;

namespace MicroKeyStudio.Protocol.Tests;

public sealed class MicroSavePacketBuilderTests
{
    [Fact]
    public void Build_overlays_slot_and_emits_literal_verified_write_pages()
    {
        MicroConfigSnapshot baseline = MicroConfigSnapshot.FromPayload(BaselinePayload());

        MicroSaveBuildResult result = MicroSavePacketBuilder.Build(
            baseline,
            new Dictionary<int, string> { [7] = "Keypad 1" });

        Assert.Equal(Hex("04 01 00 00 00 2d 00 3a d3 b4 00 00 00 00 00 00 00 ff 6d 00 00 11 09 20 20 11 09 20 20 0a 00 00 00 0d 00 00 00 0b 00 00 00 0c 00 00 00 59 00 00 00 10 00 00 00 0f 00 00 00 15 00 00 00 00"), result.Sequence.Writes[0]);

        byte[][] pages = result.Sequence.Writes.Take(4).ToArray();
        Assert.Equal(new uint[] { 0x00, 0x2d, 0x5a, 0x87 }, pages.Select(page => BitConverter.ToUInt32(page, 13)));
        Assert.All(pages, page => Assert.Equal(62, page.Length));
        Assert.All(pages, page =>
        {
            ushort crc = MicroCrc16.Compute(page.AsSpan(17, 45));
            Assert.Equal((byte)crc, page[7]);
            Assert.Equal((byte)(crc >> 8), page[8]);
        });
    }

    [Fact]
    public void Build_preserves_all_non_target_bytes_and_keeps_baseline_unchanged()
    {
        MicroConfigSnapshot baseline = MicroConfigSnapshot.FromPayload(BaselinePayload());
        byte[] originalPayload = baseline.CopyPayload();

        MicroSaveBuildResult result = MicroSavePacketBuilder.Build(
            baseline,
            new Dictionary<int, string> { [7] = "Keypad 1" });

        Assert.Equal(Hex("59 00 00 00"), result.ExpectedPayload.Skip(28).Take(4));
        for (int index = 0; index < originalPayload.Length; index++)
        {
            if (index < 28 || index > 31)
            {
                Assert.Equal(originalPayload[index], result.ExpectedPayload[index]);
            }
        }

        Assert.Equal(originalPayload, baseline.CopyPayload());
        Assert.Equal(Hex("04 06 00 5b 00 00 00 ff ff 00 00 00 00 00 00 00 00"), result.Sequence.Writes[4]);
    }

    [Fact]
    public void Build_overlays_disable_sleep_and_preserves_every_other_non_mapping_byte()
    {
        byte[] baselinePayload = BaselinePayload();
        baselinePayload[3] = 0x00;
        MicroConfigSnapshot baseline = MicroConfigSnapshot.FromPayload(baselinePayload);

        MicroSaveBuildResult result = MicroSavePacketBuilder.Build(
            baseline,
            new Dictionary<int, string>(),
            disableSleep: true);

        Assert.Equal(0x01, result.ExpectedPayload[3]);
        for (int index = 0; index < baselinePayload.Length; index++)
        {
            if (index != 3)
            {
                Assert.Equal(baselinePayload[index], result.ExpectedPayload[index]);
            }
        }

        byte[] firstPage = result.Sequence.Writes[0];
        Assert.Equal(0x01, firstPage[20]);
        ushort crc = MicroCrc16.Compute(firstPage.AsSpan(17, 45));
        Assert.Equal((byte)crc, firstPage[7]);
        Assert.Equal((byte)(crc >> 8), firstPage[8]);
        Assert.False(baseline.DisableSleep);
    }

    [Fact]
    public void Build_without_an_explicit_sleep_edit_preserves_an_unknown_device_value()
    {
        byte[] baselinePayload = BaselinePayload();
        baselinePayload[MicroConfigSnapshot.DisableSleepOffset] = 0x7f;
        MicroConfigSnapshot baseline = MicroConfigSnapshot.FromPayload(baselinePayload);

        MicroSaveBuildResult result = MicroSavePacketBuilder.Build(
            baseline,
            new Dictionary<int, string>());

        Assert.Equal(0x7f, result.ExpectedPayload[MicroConfigSnapshot.DisableSleepOffset]);
    }

    [Fact]
    public void Build_rejects_every_invalid_slot_without_returning_a_packet_sequence()
    {
        MicroConfigSnapshot baseline = MicroConfigSnapshot.FromPayload(BaselinePayload());
        MicroSaveBuildResult? result = null;

        MicroMappingEncodingException exception = Assert.Throws<MicroMappingEncodingException>(() =>
            result = MicroSavePacketBuilder.Build(
                baseline,
                new Dictionary<int, string>
                {
                    [2] = "Mouse Left",
                    [17] = "Unknown Key",
                    [45] = "A"
                }));

        Assert.Equal(new[] { 2, 17, 45 }, exception.InvalidSlots);
        Assert.Contains("2", exception.Message);
        Assert.Contains("17", exception.Message);
        Assert.Contains("45", exception.Message);
        Assert.Null(result);
    }

    [Fact]
    public void BuildFromPayload_restores_snapshot_bytes_in_literal_write_page()
    {
        MicroConfigSnapshot snapshot = MicroConfigSnapshot.FromPayload(BaselinePayload());

        PacketSequence sequence = MicroSavePacketBuilder.BuildFromPayload(snapshot);

        Assert.Equal(Hex("04 01 00 00 00 2d 00 b4 f3 b4 00 00 00 00 00 00 00 ff 6d 00 00 11 09 20 20 11 09 20 20 0a 00 00 00 0d 00 00 00 0b 00 00 00 0c 00 00 00 0e 00 00 00 10 00 00 00 0f 00 00 00 15 00 00 00 00"), sequence.Writes[0]);
        Assert.Equal(Hex("04 06 00 5b 00 00 00 ff ff 00 00 00 00 00 00 00 00"), sequence.Writes[4]);
    }

    [Fact]
    public void MatchesSlots_accepts_expected_payload_and_reports_only_changed_slot()
    {
        IReadOnlyDictionary<int, string> actions = new Dictionary<int, string> { [7] = "Keypad 1" };
        byte[] expectedPayload = BaselinePayload();
        expectedPayload[28] = 0x59;
        MicroConfigSnapshot expectedSnapshot = MicroConfigSnapshot.FromPayload(expectedPayload);

        Assert.True(MicroSavePacketBuilder.MatchesSlots(expectedSnapshot, actions, out IReadOnlyList<int> matchingSlots));
        Assert.Empty(matchingSlots);

        MicroConfigSnapshot readbackWithOriginalSlot = MicroConfigSnapshot.FromPayload(BaselinePayload());
        Assert.False(MicroSavePacketBuilder.MatchesSlots(readbackWithOriginalSlot, actions, out IReadOnlyList<int> mismatchedSlots));
        Assert.Equal(new[] { 7 }, mismatchedSlots);
    }

    private static byte[] BaselinePayload()
    {
        return Hex("ff 6d 00 00 11 09 20 20 11 09 20 20 0a 00 00 00 0d 00 00 00 0b 00 00 00 0c 00 00 00 0e 00 00 00 10 00 00 00 0f 00 00 00 15 00 00 00 00 " +
            "00 00 00 00 00 00 00 00 11 00 00 00 12 00 00 00 00 00 00 00 16 00 00 00 09 00 00 00 51 00 00 00 07 00 00 00 4f 00 00 00 00 00 00 00 00 " +
            "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 " +
            "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00");
    }

    private static byte[] Hex(string value)
    {
        return value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => Convert.ToByte(part, 16))
            .ToArray();
    }
}
