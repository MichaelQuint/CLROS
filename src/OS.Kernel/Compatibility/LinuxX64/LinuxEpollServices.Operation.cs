#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
using Fds = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;

namespace OS.Kernel.Compatibility.LinuxX64;
public static unsafe partial class LinuxEpollServices
{
    /// <summary>Stable thread-owned wait borrow of the original epoll description.
    /// Closing/reusing its fd cannot redirect completion. User output is an
    /// address only: each copy revalidates mappings. EndOperation releases the
    /// borrow only after genuine wait completion or captured thread exit.</summary>

    public struct Operation
    {
        internal State* Owner;
        internal ulong Self, Inode, Address;
        internal uint Description, Port, Maximum, Active;
        public readonly bool IsActive => Active == 1;
    }
}
#endif
