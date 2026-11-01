namespace OS.Kernel.Memory;
public readonly struct BootPageAllocatorStateMaterializationResult
{
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly byte _reserved2;
    private readonly byte _reserved3;
    private readonly byte _reserved4;
    private readonly byte _reserved5;
    private readonly byte _reserved6;
    private BootPageAllocatorStateMaterializationResult(BootPageAllocatorStateMaterializationStatus status, ulong sourceNodeCount, ulong sourceNodeCapacity, ulong sourceRangeCount, ulong requiredDestinationStateCapacity, ulong availableDestinationStateCapacity, ulong materializedStateCount, int headNodeIndex, int invalidNodeIndex, int conflictingNodeIndex, ulong visitedNodeCount, ulong pageSize, ulong expectedFreePages, ulong expectedFreeBytes, ulong observedFreePages, ulong observedFreeBytes, BootPageAllocatorState state)
    {
        Status = status;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _reserved3 = 0;
        _reserved4 = 0;
        _reserved5 = 0;
        _reserved6 = 0;
        SourceNodeCount = sourceNodeCount;
        SourceNodeCapacity = sourceNodeCapacity;
        SourceRangeCount = sourceRangeCount;
        RequiredDestinationStateCapacity = requiredDestinationStateCapacity;
        AvailableDestinationStateCapacity = availableDestinationStateCapacity;
        MaterializedStateCount = materializedStateCount;
        HeadNodeIndex = headNodeIndex;
        InvalidNodeIndex = invalidNodeIndex;
        ConflictingNodeIndex = conflictingNodeIndex;
        VisitedNodeCount = visitedNodeCount;
        PageSize = pageSize;
        ExpectedFreePages = expectedFreePages;
        ExpectedFreeBytes = expectedFreeBytes;
        ObservedFreePages = observedFreePages;
        ObservedFreeBytes = observedFreeBytes;
        State = state;
    }

    public BootPageAllocatorStateMaterializationStatus Status { get; }
    public bool Succeeded => Status == BootPageAllocatorStateMaterializationStatus.Success && ReservedFieldsAreZero() && SourceNodeCount != 0 && SourceNodeCount <= SourceNodeCapacity && SourceNodeCount <= BootPageAllocatorStateMaterializer.MaximumSourceNodeCount && SourceRangeCount != 0 && SourceRangeCount <= BootPageAllocatorStateMaterializer.MaximumSourceRangeCount && RequiredDestinationStateCapacity == BootPageAllocatorStateMaterializer.RequiredDestinationStateCapacity && AvailableDestinationStateCapacity >= RequiredDestinationStateCapacity && MaterializedStateCount == 1 && HeadNodeIndex >= 0 && (ulong)HeadNodeIndex < SourceNodeCount && InvalidNodeIndex == BootPageAllocatorStateMaterializer.NoNodeIndex && ConflictingNodeIndex == BootPageAllocatorStateMaterializer.NoNodeIndex && VisitedNodeCount == SourceNodeCount && PageSize == BootPageAllocatorStateMaterializer.RequiredPageSize && ExpectedFreePages != 0 && ExpectedFreeBytes != 0 && ObservedFreePages == ExpectedFreePages && ObservedFreeBytes == ExpectedFreeBytes && BootPageAllocatorStateMaterializer.IsRepresentableSuccessSummary(SourceNodeCount, SourceNodeCapacity, AvailableDestinationStateCapacity, PageSize, ExpectedFreePages, ExpectedFreeBytes) && State.IsInitialized && State.PageSize == PageSize && State.HeadNodeIndex == HeadNodeIndex && State.FreeNodeCount == (int)SourceNodeCount && State.FreePageCount == ExpectedFreePages && State.FreeByteCount == ExpectedFreeBytes && State.AllocatedPageCount == 0 && State.AllocatedByteCount == 0;
    public ulong SourceNodeCount { get; }
    public ulong SourceNodeCapacity { get; }
    public ulong SourceRangeCount { get; }
    public ulong RequiredDestinationStateCapacity { get; }
    public ulong AvailableDestinationStateCapacity { get; }
    public ulong MaterializedStateCount { get; }
    public int HeadNodeIndex { get; }
    public int InvalidNodeIndex { get; }
    public int ConflictingNodeIndex { get; }
    public ulong VisitedNodeCount { get; }
    public ulong PageSize { get; }
    public ulong ExpectedFreePages { get; }
    public ulong ExpectedFreeBytes { get; }
    public ulong ObservedFreePages { get; }
    public ulong ObservedFreeBytes { get; }
    public BootPageAllocatorState State { get; }

    internal static BootPageAllocatorStateMaterializationResult Success(ulong sourceNodeCount, ulong sourceNodeCapacity, ulong sourceRangeCount, ulong availableDestinationStateCapacity, int headNodeIndex, ulong pageSize, ulong freePages, ulong freeBytes, BootPageAllocatorState state)
    {
        return new BootPageAllocatorStateMaterializationResult(BootPageAllocatorStateMaterializationStatus.Success, sourceNodeCount, sourceNodeCapacity, sourceRangeCount, BootPageAllocatorStateMaterializer.RequiredDestinationStateCapacity, availableDestinationStateCapacity, materializedStateCount: 1, headNodeIndex, BootPageAllocatorStateMaterializer.NoNodeIndex, BootPageAllocatorStateMaterializer.NoNodeIndex, sourceNodeCount, pageSize, freePages, freeBytes, freePages, freeBytes, state);
    }

    internal static BootPageAllocatorStateMaterializationResult Fail(BootPageAllocatorStateMaterializationStatus status, ulong sourceNodeCount, ulong sourceNodeCapacity, ulong sourceRangeCount, ulong availableDestinationStateCapacity, int headNodeIndex, int invalidNodeIndex, int conflictingNodeIndex, ulong visitedNodeCount, ulong pageSize, ulong expectedFreePages, ulong expectedFreeBytes, ulong observedFreePages, ulong observedFreeBytes)
    {
        return new BootPageAllocatorStateMaterializationResult(status, sourceNodeCount, sourceNodeCapacity, sourceRangeCount, BootPageAllocatorStateMaterializer.RequiredDestinationStateCapacity, availableDestinationStateCapacity, materializedStateCount: 0, headNodeIndex, invalidNodeIndex, conflictingNodeIndex, visitedNodeCount, pageSize, expectedFreePages, expectedFreeBytes, observedFreePages, observedFreeBytes, default);
    }

    private bool ReservedFieldsAreZero()
    {
        return _reserved0 == 0 && _reserved1 == 0 && _reserved2 == 0 && _reserved3 == 0 && _reserved4 == 0 && _reserved5 == 0 && _reserved6 == 0;
    }
}
