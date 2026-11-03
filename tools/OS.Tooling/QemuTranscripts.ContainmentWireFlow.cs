using System.Buffers.Binary;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    private sealed class ContainmentWireFlow(int role)
    {
        internal readonly int Role = role;
        internal readonly uint Client = (uint)(10001 + role * 10000);
        internal readonly bool[] Sent = new bool[role == 0 ? 32768 : 15];
        internal uint Server;
        internal bool SynAck, HandshakeAck, Fin, Reset;
        internal int Received, SentThrough, SynRequests;
        internal ulong FirstReply, LastReply, FirstSyn, LastSyn;
    }
}
