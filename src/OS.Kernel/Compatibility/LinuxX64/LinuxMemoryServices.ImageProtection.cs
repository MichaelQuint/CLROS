using OS.Kernel.Memory;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxMemoryServices
{
    internal struct ImageProtection
    {
        internal ulong Start, Bytes;
        internal uint Protection;
    }
}
