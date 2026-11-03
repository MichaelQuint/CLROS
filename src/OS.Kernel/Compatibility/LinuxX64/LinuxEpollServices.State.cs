#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
using Fds = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxEpollServices
{
    /// <summary>Noncopyable, one-use owner of caller-retained disjoint tables.
    /// Descriptors, ports and registrations remain mapped supervisor-only until
    /// Finish, after every fd and retained wait have drained. Counts are bounded
    /// capacities, not allocation requests or mapping authority.</summary>

    public struct State
    {
        internal ulong Self;
        internal Fds.State* Descriptors;
        internal Port* Ports;
        internal Registration* Registrations;
        internal uint PortCount, RegistrationCount, Initialized;
    }
}
#endif
