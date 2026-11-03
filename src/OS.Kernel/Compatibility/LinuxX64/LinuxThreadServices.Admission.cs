using OS.Kernel.Threading;
using OS.Pal;

namespace OS.Kernel.Compatibility.LinuxX64;

public unsafe partial struct LinuxThreadServices
{
    /// <summary>Actual native owner capabilities. All ranges are mapped,
    /// exclusive, supervisor-only and disjoint from code and other kernel
    /// resources; declared sizes grant no mapping authority. User copies must
    /// complete their preflighted extent, without changing scheduler/mappings.
    /// A failed TID copy after publication poisons execution for owned shutdown.
    /// </summary>

    public struct Admission
    {
        public KernelThreadTable* Threads;
        public ulong ThreadKey;
        public Slot* Slots;
        public Worker* Workers;
        public uint Capacity, SlotBytes, WorkerBytes, ProcessId, ObservedMxcsrMask;
        public LinuxSyscallAccess User;
#if !NOSTDLIB_EFI || M16_027_PROBE
        /// <summary>Actual whole separate direct user-copy callback carrier,
        /// retained through Release. Required by finite paired storage; zero
        /// preserves unpaired callers. No transitive pointee/mapping claim.</summary>

        public ulong UserContextBytes;
#endif
        public delegate* unmanaged<void*, ulong, byte*, uint, byte> CopyToUser;
        public bool FirmwareExited, SingleOwnerValidated;
    }
}
