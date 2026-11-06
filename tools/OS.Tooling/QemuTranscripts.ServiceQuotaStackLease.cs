using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Original kernel generation, its guarded usable stack/context,
    /// and original normalized CREATE/clone CALL and genuine REAP positions.
    /// Tid comes from that CALL; this value grants no scheduler authority.</summary>

    public readonly record struct ServiceQuotaStackLease(int RecordIndex, ulong Identity, int Index, ulong Stack, ulong StackBytes, ulong Context, ulong ContextBytes, int Tid, int CreateCallRecordIndex, int ReapRecordIndex);
}
