using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Successfully queued bytes in one original accepted socket's
    /// response stream. Offsets are per open description, never a reused fd;
    /// independent wire reconstruction still authenticates transmitted bytes.</summary>

    public readonly record struct DiskStaticSocketWrite(int Connection, int CallRecordIndex, ulong Before, ulong After);
}
