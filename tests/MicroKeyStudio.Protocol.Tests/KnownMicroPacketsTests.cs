using MicroKeyStudio.Protocol.Packets;

namespace MicroKeyStudio.Protocol.Tests;

public sealed class KnownMicroPacketsTests
{
    [Fact]
    public void Constants_match_observed_micro_ble_uuids()
    {
        Assert.Equal(Guid.Parse("0000FF10-0000-1000-8000-00805F9B34FB"), MicroProtocolConstants.ServiceUuid);
        Assert.Equal(Guid.Parse("0000FF13-0000-1000-8000-00805F9B34FB"), MicroProtocolConstants.WriteCharacteristicUuid);
    }

    [Fact]
    public void Captured_save_sequence_contains_expected_public_shape()
    {
        PacketSequence sequence = KnownMicroPackets.CreateCapturedSaveSequence();

        Assert.Equal("Captured Micro save sequence", sequence.Name);
        Assert.Equal(17, sequence.Writes.Count);
        Assert.All(sequence.Writes, packet => Assert.Equal((byte)0x04, packet[0]));
        Assert.Contains(sequence.Writes, packet => packet.Length == 62);
        Assert.Contains(sequence.Writes, packet => packet.SequenceEqual(new byte[]
        {
            0x04, 0x06, 0x00, 0x5b, 0x00, 0x00, 0x00, 0xff, 0xff, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        }));
    }

    [Fact]
    public void Captured_load_sequence_reads_four_micro_config_pages_without_save_packets()
    {
        PacketSequence sequence = KnownMicroPackets.CreateCapturedLoadSequence();

        Assert.Equal("Captured Micro load sequence", sequence.Name);
        Assert.Equal(8, sequence.Writes.Count);
        Assert.Equal(4, sequence.Writes.Count(packet => packet.Length > 2 && packet[0] == 0x04 && packet[1] == 0x02));
        Assert.Contains(sequence.Writes, packet => packet.SequenceEqual(Hex("04 02 00 00 00 2d 00 30 cf b4 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00")));
        Assert.Contains(sequence.Writes, packet => packet.SequenceEqual(Hex("04 02 00 00 00 2d 00 30 cf b4 00 00 00 87 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00")));
        Assert.DoesNotContain(sequence.Writes, packet => packet.Length > 2 && packet[0] == 0x04 && packet[1] == 0x01);
        Assert.DoesNotContain(sequence.Writes, packet => packet.SequenceEqual(new byte[]
        {
            0x04, 0x06, 0x00, 0x5b, 0x00, 0x00, 0x00, 0xff, 0xff, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        }));
    }

    private static byte[] Hex(string value)
    {
        return value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => Convert.ToByte(part, 16))
            .ToArray();
    }
}
