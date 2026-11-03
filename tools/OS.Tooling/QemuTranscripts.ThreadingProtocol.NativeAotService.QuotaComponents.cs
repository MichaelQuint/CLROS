using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private sealed partial class ThreadingProtocol
    {
        internal void RequireQuotaComponents(ServiceQuotaComponentInspection components)
        {
            Need(serviceQuotaFailure && quotaAborted && history.All(t => t.Reaped) && components.StackLeases.Count == history.Count && components.StackLeases.Select(l => (l.Identity, l.Tid)).SequenceEqual(history.Select(t => (t.Identity, t.Tid))) && components.StackSnapshots[^1].Returned == (ulong)reaped && components.StackSnapshots[^1].PeakLive == (ulong)peak, "quota_components_scheduler");
        }
    }
}
