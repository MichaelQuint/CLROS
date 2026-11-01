using OS.Kernel.Memory;
using System.Runtime.CompilerServices;

namespace OS.Kernel.Runtime;
/// <summary>
/// One caller-owned, initially zeroed, stable slot. Its identities detect accidental
/// substitution, not forged/cloned POD storage, ABA or concurrent modification.
/// No release/reset operation or allocator restore receipt is exposed.
/// </summary>

public struct ManagedHeapPageRangeState
{
    internal ManagedHeapPageRangeLifecycle LifecycleValue;
    internal ulong SelfAddressValue;
    internal ulong StateCapacityValue;
    internal ulong AllocatorAddressValue;
    internal ulong NodesAddressValue;
    internal ulong NodeCountValue;
    internal ulong NodeCapacityValue;
    internal ulong LedgerAddressValue;
    internal ulong ReservationCountValue;
    internal ulong ReservationCapacityValue;
    internal ulong ParentIndexValue;
    internal ulong BackingAddressValue;
    internal ulong BackingCapacityValue;
    internal BootReservedRange ParentValue;
    internal ulong PhysicalStartValue;
    internal ulong PhysicalEndValue;
    internal BootPageAllocatorState BeforeStateValue;
    public ManagedHeapPageRangeLifecycle Lifecycle => LifecycleValue;
    public ulong SelfAddress => SelfAddressValue;
    public ulong AllocatorAddress => AllocatorAddressValue;
    public ulong NodesAddress => NodesAddressValue;
    public ulong LedgerAddress => LedgerAddressValue;
    public ulong ParentIndex => ParentIndexValue;
    public ulong BackingAddress => BackingAddressValue;
    public BootReservedRange Parent => ParentValue;
    public ulong PhysicalStart => PhysicalStartValue;
    public ulong PhysicalEndExclusive => PhysicalEndValue;
    public BootPageAllocatorState InitialAllocatorState => BeforeStateValue;
}
