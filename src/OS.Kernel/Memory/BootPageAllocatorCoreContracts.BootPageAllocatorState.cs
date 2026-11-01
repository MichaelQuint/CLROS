namespace OS.Kernel.Memory;
public readonly struct BootPageAllocatorState
{
    public const ulong StructureSize = 56;
    public BootPageAllocatorState(bool isInitialized, ulong pageSize, int headNodeIndex, int freeNodeCount, ulong freePageCount, ulong freeByteCount, ulong allocatedPageCount, ulong allocatedByteCount)
    {
        IsInitialized = isInitialized;
        PageSize = pageSize;
        HeadNodeIndex = headNodeIndex;
        FreeNodeCount = freeNodeCount;
        FreePageCount = freePageCount;
        FreeByteCount = freeByteCount;
        AllocatedPageCount = allocatedPageCount;
        AllocatedByteCount = allocatedByteCount;
    }

    public bool IsInitialized { get; }
    public ulong PageSize { get; }
    public int HeadNodeIndex { get; }
    public int FreeNodeCount { get; }
    public ulong FreePageCount { get; }
    public ulong FreeByteCount { get; }
    public ulong AllocatedPageCount { get; }
    public ulong AllocatedByteCount { get; }
}
