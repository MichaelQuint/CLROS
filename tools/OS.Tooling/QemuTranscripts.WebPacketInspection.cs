using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using OS.Tools.QemuRunner;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Observed anchored HTTP streams and TCP close directions.
    /// A FIN or FIN ACK supplies no guest descriptor release, worker reap,
    /// device stop, TIME_WAIT drain or complete runner/source qualification.</summary>

    public readonly record struct WebPacketInspection(int Connections, int TcpRecords, int Received, int Sent, int ClientFins, int ServerFins, int ClientFinAcks, int ServerFinAcks, string HealthSha256, string ConfigurationSha256, IReadOnlyList<uint> TimeWaitPorts, IReadOnlyList<WebResponseObservation> Responses, bool UtcChecked, bool ControlsChecked);
}
