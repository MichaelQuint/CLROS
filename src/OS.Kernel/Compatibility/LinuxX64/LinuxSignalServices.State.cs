using OS.Pal;

namespace OS.Kernel.Compatibility.LinuxX64;
public static unsafe partial class LinuxSignalServices
{
    /// <summary>Noncopied caller-owned state. Initialize before attaching to an
    /// empty thread owner; End requires every actual thread generation to reap
    /// and the process borrow to detach. Callers prove the whole storage extent
    /// is mapped, exclusive, supervisor RW/NX and outside user aliases.</summary>

    public struct State
    {
        internal ulong Self, Key, Process;
        internal uint Initialized, Capacity;
        internal LinuxSyscallAccess User;
        internal delegate* unmanaged<void*, ulong, byte*, uint, byte> CopyToUser;
        internal fixed ulong Actions[64 * 4];
        internal fixed ulong Threads[(int)MaximumThreads * 14]; // ThreadState, one actual generation each.
    }
}
