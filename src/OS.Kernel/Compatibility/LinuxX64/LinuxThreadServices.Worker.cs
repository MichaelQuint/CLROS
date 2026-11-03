using OS.Kernel.Threading;
using OS.Pal;

namespace OS.Kernel.Compatibility.LinuxX64;

public unsafe partial struct LinuxThreadServices
{
    /// <summary>Prepared kernel entry for the same slot/generation returned by
    /// InspectNextCreateIdentity. Its argument is the corresponding Slot address.
    /// The native owner admits emitted entry bytes and all code/context/guarded
    /// stack lifetimes. After reap it must prepare the next generation before
    /// clone can use that slot again. Do not change a bound entry.</summary>

    public struct Worker
    {
        public ThreadContextSwitch.Descriptor Initial;
        public ulong Entry;
    }
}
