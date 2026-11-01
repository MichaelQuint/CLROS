#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
using Fds = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;

namespace OS.Kernel.Compatibility.LinuxX64;
public static unsafe partial class LinuxEpollServices
{
    /// <summary>Stack-only readiness scan for one exclusive IRQ-off poll after
    /// refresh. Start fresh and discard before thread inspection/wake, callbacks
    /// or mutations. Consecutive not-ready operations may share one successful
    /// graph audit; changed owners are audited again. Every operation identity
    /// is checked on every visit. Failure does not mutate the owned graph.</summary>

    internal ref struct ReadinessScan
    {
        private State* _audited;
        internal bool HasEvents(ulong owner, ref Operation operation, out bool ready)
        {
            ready = false;
            if (operation.Active != 1)
                return false;
            if (_audited == null || _audited != operation.Owner)
            {
                if (!LinuxEpollServices.HasEvents(owner, ref operation, out ready))
                    return false;
                _audited = operation.Owner;
                return true;
            }

            if (!CurrentOperationAfterAudit(owner, ref operation))
                return false;
            ready = HasReadyEvents(ref operation);
            return true;
        }
    }
}
#endif
