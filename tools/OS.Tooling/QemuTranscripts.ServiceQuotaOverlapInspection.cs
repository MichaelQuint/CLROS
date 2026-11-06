using System.Text;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Copied same-retention-window values only. ForcedGcMarkers are
    /// completed app collection-delta observations, not GC scheduler/root proof.
    /// No clocks, pending sockets, process/file/native or guest authority follow.</summary>

    public sealed record ServiceQuotaOverlapInspection(ServiceQuotaRetainedMapping FirstRetained, int DenialCall, ServiceQuotaProgressMarker Begin, IReadOnlyList<ServiceQuotaProgressMarker> ForcedGcMarkers, IReadOnlyList<ServiceQuotaOverlapSample> Samples);
}
