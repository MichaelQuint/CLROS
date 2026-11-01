using OS.Boot;

namespace OS.Kernel.Runtime;
public static unsafe partial class CompilerInterfaceDispatch
{
    private struct State
    {
        internal ulong Phase, Self, Capacity, ModuleOwner, ModuleCapacity, ModuleHandle, ModuleSlot;
        internal EfiLoadedImageRangeCapturePlan Capture;
        internal M01LoadedImageVirtualMappingEvidence Mapping;
        internal RhpNewFastHeapAccess Heap;
        internal RhpNewFastBumpState* Allocator;
        internal ulong AllocatorCapacity, InitialStub, CellsStart, CellsBytes;
        internal uint CellCapacity, CacheCapacity, CellCount, CacheCount, WordCount, SealedTableCount;
        internal ulong Resolutions, Hits;
    }
}
