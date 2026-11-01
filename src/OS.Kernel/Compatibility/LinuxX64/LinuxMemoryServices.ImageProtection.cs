using OS.Kernel.Memory;

namespace OS.Kernel.Compatibility.LinuxX64;
public static unsafe partial class LinuxMemoryServices
{
#if M16_014_PROBE

#else
#endif
#if M10_004_PROBE





#else
#endif
    internal struct ImageProtection
    {
        internal ulong Start, Bytes;
        internal uint Protection;
    }
}
