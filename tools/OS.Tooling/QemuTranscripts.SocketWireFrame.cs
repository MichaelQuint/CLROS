using System.Buffers.Binary;
using System.Text;
using OS.Tools.QemuRunner;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    private readonly ref struct SocketWireFrame(uint remote, uint sequence, uint ack, byte flags, ReadOnlySpan<byte> payload)
    {
        internal readonly uint Remote = remote, Sequence = sequence, Ack = ack;
        internal readonly byte Flags = flags;
        internal readonly ReadOnlySpan<byte> Payload = payload;
    }
}
