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
    private struct Region
    {
        public ulong Start, Bytes, Backing;
        public ulong FreshFrom;
        public uint Protection;
        public bool Fresh, NoDump, Heap;
    }
}
