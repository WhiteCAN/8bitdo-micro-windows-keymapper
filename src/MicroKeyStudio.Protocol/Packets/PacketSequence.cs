namespace MicroKeyStudio.Protocol.Packets;

public sealed record PacketSequence(string Name, IReadOnlyList<byte[]> Writes);
