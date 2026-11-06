using System.Buffers.Binary;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private readonly ref struct SocketTcpPacket(uint remote, uint sequence, uint acknowledgement, byte flags, ReadOnlySpan<byte> payload)
    {
        internal readonly uint Remote = remote, Sequence = sequence, Acknowledgement = acknowledgement;
        internal readonly byte Flags = flags;
        internal readonly ReadOnlySpan<byte> Payload = payload;
    }
}
