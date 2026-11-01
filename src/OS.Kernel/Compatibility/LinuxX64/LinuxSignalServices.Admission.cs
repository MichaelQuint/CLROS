using OS.Pal;

namespace OS.Kernel.Compatibility.LinuxX64;
public static unsafe partial class LinuxSignalServices
{
    /// <summary>Exact user-copy capabilities shared with the thread owner.
    /// Copies preflight the complete extent and cannot dispatch, block, change
    /// mappings or re-enter signal/thread state. Keys and lifetimes stay external.</summary>

    public struct Admission
    {
        public LinuxSyscallAccess User;
        public delegate* unmanaged<void*, ulong, byte*, uint, byte> CopyToUser;
        public uint Capacity;
        public bool FirmwareExited, SingleOwnerValidated;
    }
}
