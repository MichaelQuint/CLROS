using OS.Kernel.Threading;

namespace OS.Kernel.Compatibility.LinuxX64;

public unsafe partial struct LinuxThreadServices
{
    /// <summary>Mapped, disjoint caller-owned storage and genuine user-memory /
    /// monotonic-clock capabilities, used with User.Context. Resolve borrows the
    /// exact readable four-byte alias with a nonzero, nonreused mapping lifetime.
    /// It changes no mappings/ownership. Distinct user words need distinct
    /// aliases. Only this process's anonymous/private image backing is supported;
    /// user-VA aliases and file/shared-memory mappings are unsupported. Shared
    /// anonymous operations require writable backing; private waits need read.
    /// The memory owner must consult MayChangeUserRange before every overlapping
    /// unmap, fixed replacement, protection, discard or heap shrink. Keep aliases
    /// mapped/exclusive until completion drops every reference. The kernel table
    /// must use monotonic nanoseconds, never APIC count or diagnostic IRQ epochs.
    /// ReadMonotonic supplies the actual owned clock; these declarations and host
    /// simulations are not hardware/lifetime proof.</summary>

    public struct FutexAdmission
    {
        public FutexCell* Cells;
        public WaitAddress.Slot* Addresses;
        public WaitAddress.Registration* Registrations;
        public uint Capacity, CellBytes, AddressBytes, RegistrationBytes;
        public ulong OwnerKey;
        public delegate* unmanaged<void*, ulong, uint**, ulong*, byte> Resolve;
        public delegate* unmanaged<void*, ulong*, byte> ReadMonotonic;
    }
}
