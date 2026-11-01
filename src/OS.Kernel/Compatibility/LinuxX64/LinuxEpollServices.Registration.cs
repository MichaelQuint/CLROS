#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
using Fds = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;

namespace OS.Kernel.Compatibility.LinuxX64;
public static unsafe partial class LinuxEpollServices
{
    /// <summary>Zeroed private watch metadata. Opaque user Data is copied as
    /// exactly 64 bits and is never dereferenced. Last open-description close
    /// scrubs the entire registration, including queued readiness and Data.</summary>

    public struct Registration
    {
        internal uint Port, Fd, Description, Interest, Ready, Pending;
        internal ulong Inode, Data;
    }
}
#endif
