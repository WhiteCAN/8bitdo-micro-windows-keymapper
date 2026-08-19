using MicroKeyStudio.Protocol.Configuration;

namespace MicroKeyStudio.Protocol.Tests;

public sealed class MicroCrc16Tests
{
    [Theory]
    [InlineData("ff 6d 00 00 11 09 20 20 11 09 20 20 0a 00 00 00 0d 00 00 00 0b 00 00 00 0c 00 00 00 59 00 00 00 10 00 00 00 0f 00 00 00 15 00 00 00 00", 0xD33A)]
    [InlineData("00 00 00 00 00 00 00 11 00 00 00 12 00 00 00 00 00 00 00 16 00 00 00 09 00 00 00 51 00 00 00 07 00 00 00 4f 00 00 00 00 00 00 00 00 00", 0x8E09)]
    [InlineData("00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00", 0xCF30)]
    public void Official_pages_produce_observed_crc(string hex, ushort expected)
    {
        Assert.Equal(expected, MicroCrc16.Compute(Hex(hex)));
    }

    private static byte[] Hex(string value)
    {
        return value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => Convert.ToByte(part, 16))
            .ToArray();
    }
}
