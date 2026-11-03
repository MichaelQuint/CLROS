using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Reconstructed HTTP exchanges and observed close/reset directions.
    /// This report does not check injected persist/reset controls, guest owner
    /// retirement, native/source identity or process lifetime.</summary>

    public readonly record struct ServiceHttpPacketInspection(int Connections, int TcpRecords, int RequestBytes, int ResponseBytes, int ServerActiveCloses, int CompleteCloses, int QuietSyns, int ClientResets = 0, int LifecycleConnections = 0);
}
