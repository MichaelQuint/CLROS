using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using OS.Tools.QemuRunner;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private sealed class WebWireFlow(uint port, uint client, byte[] request, int responseCapacity)
    {
        internal readonly uint Port = port, Client = unchecked(client + 1);
        internal readonly byte[] Request = request, Response = new byte[responseCapacity];
        internal readonly bool[] RequestSeen = new bool[request.Length], ResponseSeen = new bool[responseCapacity];
        internal uint Server;
        internal int ClientSeen, ServerSeen, ResponseExtent;
        internal bool SynAck, ClientReady, ClientFin, ServerFin, ClientFinAcked, ServerFinAcked, ServerFinFirst;
    }
}
