#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
using Fds = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxEpollServices
{
    /// <summary>Zeroed port metadata shared by dup fds and retained kernel waits.
    /// Cursor provides bounded fair delivery. Never copy or mutate a live port.</summary>

    public struct Port
    {
        internal uint Active, Cursor;
    }
}
#endif
