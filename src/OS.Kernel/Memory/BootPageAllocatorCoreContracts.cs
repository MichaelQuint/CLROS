namespace OS.Kernel.Memory;

public readonly struct BootPageFreeListNode
{
    public const ulong StructureSize = 48;

    // This typed POD is not wire-compatible with the legacy EFI six-ulong
    // node-slot table. A future live adapter must transcode both provenance
    // indexes and the signed terminal link instead of reinterpreting bytes.
    public BootPageFreeListNode(
        int sourceFreeSegmentIndex,
        int sourceUsableRangeIndex,
        ulong physicalStart,
        ulong physicalEndExclusive,
        ulong pageCount,
        ulong byteLength,
        int nextNodeIndex)
    {
        SourceFreeSegmentIndex = sourceFreeSegmentIndex;
        SourceUsableRangeIndex = sourceUsableRangeIndex;
        PhysicalStart = physicalStart;
        PhysicalEndExclusive = physicalEndExclusive;
        PageCount = pageCount;
        ByteLength = byteLength;
        NextNodeIndex = nextNodeIndex;
    }

    public int SourceFreeSegmentIndex { get; }

    public int SourceUsableRangeIndex { get; }

    public ulong PhysicalStart { get; }

    public ulong PhysicalEndExclusive { get; }

    public ulong PageCount { get; }

    public ulong ByteLength { get; }

    public int NextNodeIndex { get; }
}

public readonly struct BootPageAllocatorState
{
    public const ulong StructureSize = 56;

    public BootPageAllocatorState(
        bool isInitialized,
        ulong pageSize,
        int headNodeIndex,
        int freeNodeCount,
        ulong freePageCount,
        ulong freeByteCount,
        ulong allocatedPageCount,
        ulong allocatedByteCount)
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
