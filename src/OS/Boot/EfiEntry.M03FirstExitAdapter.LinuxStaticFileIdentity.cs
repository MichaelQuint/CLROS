#if M10_017_PROBE || M10_018_PROBE
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Boot;

public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {
        private struct LinuxStaticFileIdentity
        {
            internal uint Fd, Generation, Kind, View;
            internal ulong Inode, Bytes, Position;
        }

    }
}
#endif
