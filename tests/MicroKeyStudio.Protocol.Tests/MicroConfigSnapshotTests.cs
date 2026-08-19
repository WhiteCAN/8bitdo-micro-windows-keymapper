using MicroKeyStudio.Protocol.Configuration;

namespace MicroKeyStudio.Protocol.Tests;

public sealed class MicroConfigSnapshotTests
{
    [Fact]
    public void TryCreate_collects_sorted_config_pages_and_decodes_keyboard_values()
    {
        byte[][] responses =
        {
            Hex("04 00 02 00 2d 00 9d 95 b4 00 00 00 00 00 00 00 8c b8 00 00 11 09 20 20 11 09 20 20 28 00 00 00 2a 00 00 00 e0 1a 00 00 e0 e1 17 00 e0 00 00 00 e2 00 00 00 e0 06 00 00 e0 19 00 00 00"),
            Hex("04 00 02 00 2d 00 69 07 b4 00 00 00 2d 00 00 00 00 00 00 00 00 00 00 4e 00 00 00 4b 00 00 00 e2 3d 00 00 29 00 00 00 52 00 00 00 51 00 00 00 50 00 00 00 4f 00 00 00 00 00 00 00 00 00"),
            Hex("04 00 02 00 2d 00 30 cf b4 00 00 00 5a 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00"),
            Hex("04 00 02 00 2d 00 30 cf b4 00 00 00 87 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00")
        };

        Assert.True(MicroConfigSnapshot.TryCreate(responses, out MicroConfigSnapshot? snapshot));
        Assert.NotNull(snapshot);
        Assert.Equal(180, snapshot.Payload.Length);

        IReadOnlyList<MicroKeyValue> values = snapshot.KeyboardValues;
        Assert.Equal("Enter", values[3].DisplayText);
        Assert.Equal("Backspace", values[4].DisplayText);
        Assert.Equal("Ctrl+W", values[5].DisplayText);
        Assert.Equal("Ctrl+Shift+T", values[6].DisplayText);
        Assert.Equal("Alt+F4", values[15].DisplayText);
        Assert.Equal("Esc", values[16].DisplayText);
        Assert.Equal("Up", values[17].DisplayText);
        Assert.Equal("Right", values[20].DisplayText);
    }

    [Fact]
    public void TryCreate_decodes_keypad_number_values()
    {
        byte[][] responses =
        {
            Hex("04 00 02 00 2d 00 b4 f3 b4 00 00 00 00 00 00 00 ff 6d 00 00 11 09 20 20 11 09 20 20 0a 00 00 00 0d 00 00 00 0b 00 00 00 0c 00 00 00 59 00 00 00 10 00 00 00 0f 00 00 00 15 00 00 00 00"),
            Hex("04 00 02 00 2d 00 09 8e b4 00 00 00 2d 00 00 00 00 00 00 00 00 00 00 11 00 00 00 12 00 00 00 00 00 00 00 16 00 00 00 09 00 00 00 51 00 00 00 07 00 00 00 4f 00 00 00 00 00 00 00 00 00"),
            Hex("04 00 02 00 2d 00 30 cf b4 00 00 00 5a 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00"),
            Hex("04 00 02 00 2d 00 30 cf b4 00 00 00 87 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00")
        };

        Assert.True(MicroConfigSnapshot.TryCreate(responses, out MicroConfigSnapshot? snapshot));
        Assert.NotNull(snapshot);

        Assert.Equal("Keypad 1", snapshot.KeyboardValues[7].DisplayText);
    }

    [Fact]
    public void TryCreate_rejects_incomplete_page_sets()
    {
        byte[][] responses =
        {
            Hex("04 00 02 00 2d 00 9d 95 b4 00 00 00 00 00 00 00 8c b8")
        };

        Assert.False(MicroConfigSnapshot.TryCreate(responses, out _));
    }

    [Theory]
    [InlineData(44)]
    [InlineData(46)]
    public void TryCreate_rejects_pages_that_are_not_45_bytes(byte malformedLength)
    {
        byte[][] responses =
        {
            BuildPage(0x00, malformedLength),
            BuildPage(0x2d, 45),
            BuildPage(0x5a, 45),
            BuildPage(0x87, 45)
        };

        Assert.False(MicroConfigSnapshot.TryCreate(responses, out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(179)]
    [InlineData(181)]
    public void FromPayload_rejects_lengths_other_than_four_pages(int length)
    {
        Assert.Throws<ArgumentException>(() => MicroConfigSnapshot.FromPayload(new byte[length]));
    }

    [Fact]
    public void FromPayload_and_CopyPayload_are_immutable_boundaries()
    {
        byte[] payload = Enumerable.Repeat((byte)0x5a, 180).ToArray();
        MicroConfigSnapshot snapshot = MicroConfigSnapshot.FromPayload(payload);
        payload[0] = 0xff;

        byte[] copy = snapshot.CopyPayload();
        Assert.Equal(0x5a, copy[0]);
        copy[0] = 0xff;
        Assert.Equal(0x5a, snapshot.CopyPayload()[0]);
    }

    [Theory]
    [InlineData(0x00, false)]
    [InlineData(0x01, true)]
    public void FromPayload_decodes_disable_sleep_flag(byte rawValue, bool expected)
    {
        byte[] payload = new byte[180];
        payload[3] = rawValue;

        MicroConfigSnapshot snapshot = MicroConfigSnapshot.FromPayload(payload);

        Assert.Equal(expected, snapshot.DisableSleep);
    }

    private static byte[] Hex(string value)
    {
        return value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => Convert.ToByte(part, 16))
            .ToArray();
    }

    private static byte[] BuildPage(uint offset, byte payloadLength)
    {
        byte[] packet = new byte[16 + payloadLength];
        packet[0] = 0x04;
        packet[1] = 0x00;
        packet[2] = 0x02;
        packet[4] = payloadLength;
        BitConverter.GetBytes(offset).CopyTo(packet, 12);
        return packet;
    }
}
