using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Copied static backing and actual borrower counters. Backing is
    /// outside the app dynamic pool and remains retained after End.</summary>

    public readonly record struct ServiceQuotaSocketLeaseSnapshot(int RecordIndex, ulong BackingBytes, ulong Acquired, ulong Returned, ulong Live, ulong ReceiveBytes, ulong SendBytes, ulong PeakLive, ulong PeakReceiveBytes, ulong PeakSendBytes, bool End);
}
