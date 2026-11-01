namespace OS.Kernel.Memory;
#if !NOSTDLIB_EFI
public readonly struct BootPageAllocatorMutationResult
{
    private BootPageAllocatorMutationResult(bool isInitialized, BootPageAllocatorMutationStatus status, BootPageAllocatorState state, ulong physicalStart, ulong physicalEndExclusive, ulong pageCount, ulong byteLength, int nodeIndex, int insertedNodeIndex, int visitedNodeCount)
    {
        IsInitialized = isInitialized;
        Status = status;
        State = state;
        PhysicalStart = physicalStart;
        PhysicalEndExclusive = physicalEndExclusive;
        PageCount = pageCount;
        ByteLength = byteLength;
        NodeIndex = nodeIndex;
        InsertedNodeIndex = insertedNodeIndex;
        VisitedNodeCount = visitedNodeCount;
    }

    public bool IsInitialized { get; }
    public BootPageAllocatorMutationStatus Status { get; }
    public bool Succeeded => IsInitialized && Status == BootPageAllocatorMutationStatus.Success;
    public BootPageAllocatorState State { get; }
    public ulong PhysicalStart { get; }
    public ulong PhysicalEndExclusive { get; }
    public ulong PageCount { get; }
    public ulong ByteLength { get; }
    public int NodeIndex { get; }
    public int InsertedNodeIndex { get; }
    public int VisitedNodeCount { get; }

    internal static BootPageAllocatorMutationResult Success(BootPageAllocatorState state, ulong physicalStart, ulong physicalEndExclusive, ulong pageCount, ulong byteLength, int nodeIndex, int insertedNodeIndex, int visitedNodeCount)
    {
        return new BootPageAllocatorMutationResult(isInitialized: true, BootPageAllocatorMutationStatus.Success, state, physicalStart, physicalEndExclusive, pageCount, byteLength, nodeIndex, insertedNodeIndex, visitedNodeCount);
    }

    internal static BootPageAllocatorMutationResult Fail(BootPageAllocatorMutationStatus status, BootPageAllocatorState state, ulong physicalStart, ulong physicalEndExclusive, ulong pageCount, ulong byteLength, int nodeIndex, int insertedNodeIndex, int visitedNodeCount)
    {
        return new BootPageAllocatorMutationResult(isInitialized: true, status, state, physicalStart, physicalEndExclusive, pageCount, byteLength, nodeIndex, insertedNodeIndex, visitedNodeCount);
    }
}
#endif
#if !NOSTDLIB_EFI
#endif
