using System.Globalization;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private readonly record struct FutexReplay(ulong Address, uint Op, uint Expected, ulong Timeout, uint Bits, ulong Rip, ulong Deadline);
}
