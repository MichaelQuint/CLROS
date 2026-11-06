using System.Buffers.Binary;
using System.Text;
using OS.Tools.QemuRunner;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Value-only original socket wire totals; no execution authority.</summary>

    public readonly record struct SocketPacketInspection(int Connections, int Received, int Sent, int Resets, int ClientFins, int ServerFins, ulong PeerReceived, ulong PumpReceived, ulong PumpArp, ulong PumpArpCopied, ulong PumpTcpCopied, ulong PumpIgnored);
}
