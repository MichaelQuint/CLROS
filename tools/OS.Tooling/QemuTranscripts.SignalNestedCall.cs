using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    private readonly record struct SignalNestedCall(ulong Nr, uint Depth = 0, ulong A0 = 0, ulong A1 = 0, ulong A2 = 0, ulong A3 = 0, ulong Value = 0, bool Restorer = false);
}
