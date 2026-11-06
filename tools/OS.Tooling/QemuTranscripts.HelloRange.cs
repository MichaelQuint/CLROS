using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private readonly record struct HelloRange(ulong Start, ulong End, uint Protection, bool Dynamic);
}
