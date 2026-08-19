namespace MicroKeyStudio.Protocol.Configuration;

public static class MicroCrc16
{
    public static ushort Compute(ReadOnlySpan<byte> data)
    {
        ushort crc = 0xffff;
        foreach (byte value in data)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0
                    ? (ushort)((crc >> 1) ^ 0xa001)
                    : (ushort)(crc >> 1);
            }
        }

        return crc;
    }
}
