using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Immutable component observations only. Original row positions
    /// are preserved. No guest pthread attribution, budget fit, native ABI,
    /// source/file/process/packet authority or guest acceptance is inferred.</summary>

    public sealed record ServiceQuotaComponentInspection(ServiceQuotaStackBacking Backing, IReadOnlyList<ServiceQuotaStackLease> StackLeases, IReadOnlyList<ServiceQuotaStackLeaseSnapshot> StackSnapshots, IReadOnlyList<ServiceQuotaSocketLease> SocketLeases, IReadOnlyList<ServiceQuotaSocketLeaseSnapshot> SocketSnapshots);
}
