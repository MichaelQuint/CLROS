using OS.Kernel.Memory;

namespace OS.Kernel.Runtime;
/// <summary>The thirteen original ValidateRetained arguments, not copied heap authority.</summary>

public unsafe struct RhpNewFastHeapAccess
{
    public BootPageAllocatorState* Allocator;
    public ulong AllocatorCapacity;
    public BootPageFreeListNode* Nodes;
    public ulong NodeCount;
    public ulong NodeCapacity;
    public BootReservedRange* Reservations;
    public ulong ReservationCount;
    public ulong ReservationCapacity;
    public ulong ParentIndex;
    public byte* Backing;
    public ulong BackingCapacity;
    public ManagedHeapPageRangeState* Heap;
    public ulong HeapCapacity;
}
