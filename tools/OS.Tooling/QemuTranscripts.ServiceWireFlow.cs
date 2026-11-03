using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    private sealed class ServiceWireFlow(uint port, uint client, bool quotaStreams = false)
    {
        internal readonly uint Port = port, Client = unchecked(client + 1);
        internal byte[] Request = quotaStreams ? Array.Empty<byte>() : new byte[4096], Response = quotaStreams ? Array.Empty<byte>() : new byte[16384];
        internal bool[] RequestSeen = quotaStreams ? Array.Empty<bool>() : new bool[4096], ResponseSeen = quotaStreams ? Array.Empty<bool>() : new bool[16384];
        internal uint Server;
        internal int RequestExtent, ResponseExtent, ClientSeen, ServerSeen;
        internal int SynRecord, ResponseFirstRecord, ResponseLastRecord, ClientFinRecord, ServerFinRecord, ResetRecord;
        internal bool SynAck, ClientReady, ClientFin, ServerFin, ClientFinAcked, ServerFinAcked, ServerFinFirst;
    }
}
