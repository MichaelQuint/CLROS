using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>One copied supervisor turn involving a source5 admission or
    /// completion. Indices address the original normalized capture, not a
    /// filtered projection. Prepare-only repark never entered user execution.</summary>

    public sealed record NativeAotLogUploadProtocolTurn(ulong Identity, int FirstRecordIndex, int SupervisorRecordIndex, int? CompleteRecordIndex, int? ResumeRecordIndex, int? DrainRecordIndex, bool PrepareOnly, ulong Action);
}
