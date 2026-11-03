using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    private sealed record ServiceWireCapture(List<ServiceWireFlow> Flows, int TcpRecords, int QuietSyns);
}
