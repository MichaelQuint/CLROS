using System.Text;
using System.Text.Json;
using OS.Tooling;

internal static partial class NativeAotServiceQuotaOverlapTests
{
    private sealed record Vector(byte[] Raw, QemuTranscripts.ServiceQuotaMemoryInspection Memory, QemuTranscripts.ServiceQuotaHostHttpInspection Host, QemuTranscripts.ServiceQuotaTemporalInspection Temporal, byte[] Peer);
}
