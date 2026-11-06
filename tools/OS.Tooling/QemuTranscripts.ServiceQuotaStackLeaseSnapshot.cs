using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Copied stack borrower counts and byte high-water. End means
    /// checked empty borrowers, while their backing reservation stays retained.</summary>

    public readonly record struct ServiceQuotaStackLeaseSnapshot(int RecordIndex, ulong Acquired, ulong Returned, ulong Live, ulong StackBytes, ulong ContextBytes, ulong PeakLive, ulong PeakStackBytes, ulong PeakContextBytes, bool End);
}
