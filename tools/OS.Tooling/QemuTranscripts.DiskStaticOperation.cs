using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Actual captured call interval through its consumed completion.
    /// Metadata copies must fit an original pending interval; overlap grants
    /// no unique request attribution. Values do not reconstruct the FS cache.</summary>

    public readonly record struct DiskStaticOperation(ulong Identity, ulong Ordinal, ulong Number, ulong Request, int BeginRecordIndex, int WakeRecordIndex, int CompleteRecordIndex, uint OutputPrefix, long Value);
}
