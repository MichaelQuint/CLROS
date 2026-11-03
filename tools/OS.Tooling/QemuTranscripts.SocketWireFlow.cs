using System.Buffers.Binary;
using System.Text;
using OS.Tools.QemuRunner;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    private sealed class SocketWireFlow(uint port, int index, uint sequence, bool vectors)
    {
        internal readonly uint Port = port, Client = sequence + 1;
        internal readonly int Index = index;
        internal uint Server;
        internal bool SynAck, ClientFin, ServerFin, ClientFinAcked, ServerFinAcked, Reset;
        internal int ClientSeen, ServerSeen;
        internal readonly bool[] Request = new bool[vectors ? 64 : index == 1 ? 1 : 20], Reply = new bool[index == 0 ? vectors ? 524288 : 65536 : index == 1 || vectors && index == 2 ? 0 : vectors ? 64 : 18];
    }
}
