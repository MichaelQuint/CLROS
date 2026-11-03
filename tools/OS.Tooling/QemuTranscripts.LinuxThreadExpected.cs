using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    private readonly record struct LinuxThreadExpected(ulong Nr, ulong A0 = 0, ulong A1 = 0, ulong A2 = 0, ulong A3 = 0, ulong A4 = 0);
}
