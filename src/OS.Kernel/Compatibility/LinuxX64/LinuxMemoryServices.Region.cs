using OS.Kernel.Memory;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxMemoryServices
{
    private struct Region
    {
        public ulong Start, Bytes, Backing;
        public ulong FreshFrom;
        public uint Protection;
        public bool Fresh, NoDump, Heap;
    }
}
