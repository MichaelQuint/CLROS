#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
using Fds = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;

namespace OS.Kernel.Compatibility.LinuxX64;
public static unsafe partial class LinuxEpollServices
{
    /// <summary>Stack-only admission for one serialized IRQ-off refresh. Begin
    /// audits the complete descriptor/epoll graph and exact thread owner. Carry
    /// only across unchanged field reads; discard before inspection, callbacks
    /// or mutation. Refresh consumes the admission before readiness changes;
    /// default, consumed or mismatched admissions perform the full audit again.
    /// Retained-operation checks still audit the resulting readiness graph.</summary>

    internal ref struct ThreadRefresh
    {
        private Fds.State* _descriptors;
        private ulong _owner;
        internal static bool Begin(Fds.State* descriptors, ulong owner, out ThreadRefresh refresh)
        {
            refresh = default;
            if (!Fds.Attached(descriptors, owner))
                return false;
            refresh._descriptors = descriptors;
            refresh._owner = owner;
            return true;
        }

        internal bool Refresh(Fds.State* descriptors, ulong owner)
        {
            bool admitted = _descriptors != null && _descriptors == descriptors && _owner == owner;
            this = default;
            if (!admitted && !Fds.Attached(descriptors, owner))
                return false;
            return descriptors->Epoll == null || RefreshCore(descriptors->Epoll);
        }
    }
}
#endif
