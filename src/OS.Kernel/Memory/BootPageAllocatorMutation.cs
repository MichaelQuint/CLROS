namespace OS.Kernel.Memory;

public enum BootPageAllocatorMutationStatus
{
    Success = 0,
    StateNotInitialized = 1,
    InvalidPageSize = 2,
    InvalidPageCount = 3,
    InvalidAlignment = 4,
    NodeCapacityTooSmall = 5,
    InvalidHeadNodeIndex = 6,
    InvalidNextNodeIndex = 7,
    InvalidNodeShape = 8,
    FreeListCycleDetected = 9,
    FreeListNodeCountMismatch = 10,
    TotalPageCountMismatch = 11,
    TotalByteCountMismatch = 12,
    AllocationByteCountOverflow = 13,
    AllocationAddressOverflow = 14,
    NoSuitableRange = 15,
    MetadataCapacityTooSmall = 16,
    FreeRangeOverlaps = 17,
    FreeRangeNotAllocated = 18
}

public readonly struct BootPageAllocatorMutationResult
{
    private BootPageAllocatorMutationResult(
        bool isInitialized,
        BootPageAllocatorMutationStatus status,
        BootPageAllocatorState state,
        ulong physicalStart,
        ulong physicalEndExclusive,
        ulong pageCount,
        ulong byteLength,
        int nodeIndex,
        int insertedNodeIndex,
        int visitedNodeCount)
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

    internal static BootPageAllocatorMutationResult Success(
        BootPageAllocatorState state,
        ulong physicalStart,
        ulong physicalEndExclusive,
        ulong pageCount,
        ulong byteLength,
        int nodeIndex,
        int insertedNodeIndex,
        int visitedNodeCount)
    {
        return new BootPageAllocatorMutationResult(
            isInitialized: true,
            BootPageAllocatorMutationStatus.Success,
            state,
            physicalStart,
            physicalEndExclusive,
            pageCount,
            byteLength,
            nodeIndex,
            insertedNodeIndex,
            visitedNodeCount);
    }

    internal static BootPageAllocatorMutationResult Fail(
        BootPageAllocatorMutationStatus status,
        BootPageAllocatorState state,
        ulong physicalStart,
        ulong physicalEndExclusive,
        ulong pageCount,
        ulong byteLength,
        int nodeIndex,
        int insertedNodeIndex,
        int visitedNodeCount)
    {
        return new BootPageAllocatorMutationResult(
            isInitialized: true,
            status,
            state,
            physicalStart,
            physicalEndExclusive,
            pageCount,
            byteLength,
            nodeIndex,
            insertedNodeIndex,
            visitedNodeCount);
    }
}

public static class BootPageAllocatorMutator
{
    public const bool PerformsLiveAllocation = false;
    public const bool MutatesCallerOwnedFreeList = true;
    public const bool ConfiguresPaging = false;
    public const bool EnforcesNxOrWx = false;

    public static BootPageAllocatorMutationResult Allocate(
        BootPageAllocatorState state,
        Span<BootPageFreeListNode> nodes,
        ulong pageCount,
        ulong alignment)
    {
        BootPageAllocatorMutationStatus preflight = ValidateRequest(
            state,
            nodes,
            pageCount,
            alignment,
            out ulong byteLength);
        if (preflight != BootPageAllocatorMutationStatus.Success)
        {
            return BootPageAllocatorMutationResult.Fail(
                preflight,
                state,
                physicalStart: 0,
                physicalEndExclusive: 0,
                pageCount,
                byteLength,
                nodeIndex: -1,
                insertedNodeIndex: -1,
                visitedNodeCount: 0);
        }

        BootPageAllocatorMutationStatus chainStatus = ValidateFreeList(
            state,
            nodes,
            out int visitedNodeCount);
        if (chainStatus != BootPageAllocatorMutationStatus.Success)
        {
            return BootPageAllocatorMutationResult.Fail(
                chainStatus,
                state,
                physicalStart: 0,
                physicalEndExclusive: 0,
                pageCount,
                byteLength,
                nodeIndex: -1,
                insertedNodeIndex: -1,
                visitedNodeCount);
        }

        int previousNodeIndex = -1;
        int currentNodeIndex = state.HeadNodeIndex;
        for (int index = 0; index < state.FreeNodeCount && currentNodeIndex != -1; index++)
        {
            BootPageFreeListNode node = nodes[currentNodeIndex];
            if (!TryAlignUp(node.PhysicalStart, alignment, out ulong alignedStart))
            {
                return BootPageAllocatorMutationResult.Fail(
                    BootPageAllocatorMutationStatus.AllocationAddressOverflow,
                    state,
                    physicalStart: 0,
                    physicalEndExclusive: 0,
                    pageCount,
                    byteLength,
                    currentNodeIndex,
                    insertedNodeIndex: -1,
                    visitedNodeCount: index + 1);
            }

            if (alignedStart >= node.PhysicalStart &&
                TryAdd(alignedStart, byteLength, out ulong allocationEnd) &&
                allocationEnd <= node.PhysicalEndExclusive)
            {
                bool hasPrefix = alignedStart > node.PhysicalStart;
                bool hasSuffix = allocationEnd < node.PhysicalEndExclusive;
                int insertedNodeIndex = -1;

                if (hasPrefix && hasSuffix)
                {
                    insertedNodeIndex = FindUnusedNodeIndex(nodes, state, currentNodeIndex);
                    if (insertedNodeIndex < 0)
                    {
                        return BootPageAllocatorMutationResult.Fail(
                            BootPageAllocatorMutationStatus.MetadataCapacityTooSmall,
                            state,
                            alignedStart,
                            allocationEnd,
                            pageCount,
                            byteLength,
                            currentNodeIndex,
                            insertedNodeIndex,
                            index + 1);
                    }
                }

                BootPageAllocatorState nextState = ApplyAllocation(
                    state,
                    nodes,
                    previousNodeIndex,
                    currentNodeIndex,
                    insertedNodeIndex,
                    node,
                    alignedStart,
                    allocationEnd,
                    pageCount,
                    byteLength,
                    hasPrefix,
                    hasSuffix);

                return BootPageAllocatorMutationResult.Success(
                    nextState,
                    alignedStart,
                    allocationEnd,
                    pageCount,
                    byteLength,
                    currentNodeIndex,
                    insertedNodeIndex,
                    index + 1);
            }

            previousNodeIndex = currentNodeIndex;
            currentNodeIndex = node.NextNodeIndex;
        }

        return BootPageAllocatorMutationResult.Fail(
            BootPageAllocatorMutationStatus.NoSuitableRange,
            state,
            physicalStart: 0,
            physicalEndExclusive: 0,
            pageCount,
            byteLength,
            nodeIndex: -1,
            insertedNodeIndex: -1,
            visitedNodeCount);
    }

    public static BootPageAllocatorMutationResult Free(
        BootPageAllocatorState state,
        Span<BootPageFreeListNode> nodes,
        ulong physicalStart,
        ulong pageCount)
    {
        BootPageAllocatorMutationStatus preflight = ValidateRequest(
            state,
            nodes,
            pageCount,
            state.PageSize,
            out ulong byteLength);
        if (preflight != BootPageAllocatorMutationStatus.Success)
        {
            return BootPageAllocatorMutationResult.Fail(
                preflight,
                state,
                physicalStart,
                physicalEndExclusive: 0,
                pageCount,
                byteLength,
                nodeIndex: -1,
                insertedNodeIndex: -1,
                visitedNodeCount: 0);
        }

        if (!IsAligned(physicalStart, state.PageSize) ||
            !TryAdd(physicalStart, byteLength, out ulong physicalEndExclusive))
        {
            return BootPageAllocatorMutationResult.Fail(
                BootPageAllocatorMutationStatus.AllocationAddressOverflow,
                state,
                physicalStart,
                physicalEndExclusive: 0,
                pageCount,
                byteLength,
                nodeIndex: -1,
                insertedNodeIndex: -1,
                visitedNodeCount: 0);
        }

        if (state.AllocatedPageCount < pageCount || state.AllocatedByteCount < byteLength)
        {
            return BootPageAllocatorMutationResult.Fail(
                BootPageAllocatorMutationStatus.FreeRangeNotAllocated,
                state,
                physicalStart,
                physicalEndExclusive,
                pageCount,
                byteLength,
                nodeIndex: -1,
                insertedNodeIndex: -1,
                visitedNodeCount: 0);
        }

        BootPageAllocatorMutationStatus chainStatus = ValidateFreeList(
            state,
            nodes,
            out int visitedNodeCount);
        if (chainStatus != BootPageAllocatorMutationStatus.Success)
        {
            return BootPageAllocatorMutationResult.Fail(
                chainStatus,
                state,
                physicalStart,
                physicalEndExclusive,
                pageCount,
                byteLength,
                nodeIndex: -1,
                insertedNodeIndex: -1,
                visitedNodeCount);
        }

        int previousNodeIndex = -1;
        int currentNodeIndex = state.HeadNodeIndex;
        int visited = 0;
        while (currentNodeIndex != -1)
        {
            BootPageFreeListNode current = nodes[currentNodeIndex];
            if (physicalEndExclusive <= current.PhysicalStart)
            {
                break;
            }

            if (physicalStart < current.PhysicalEndExclusive)
            {
                return BootPageAllocatorMutationResult.Fail(
                    BootPageAllocatorMutationStatus.FreeRangeOverlaps,
                    state,
                    physicalStart,
                    physicalEndExclusive,
                    pageCount,
                    byteLength,
                    currentNodeIndex,
                    insertedNodeIndex: -1,
                    visitedNodeCount: visited + 1);
            }

            previousNodeIndex = currentNodeIndex;
            currentNodeIndex = current.NextNodeIndex;
            visited++;
        }

        bool mergesPrevious = previousNodeIndex >= 0 &&
            nodes[previousNodeIndex].PhysicalEndExclusive == physicalStart;
        bool mergesNext = currentNodeIndex >= 0 &&
            physicalEndExclusive == nodes[currentNodeIndex].PhysicalStart;
        int insertedNodeIndex = -1;

        if (!mergesPrevious && !mergesNext)
        {
            insertedNodeIndex = FindUnusedNodeIndex(nodes, state, excludedNodeIndex: -1);
            if (insertedNodeIndex < 0)
            {
                return BootPageAllocatorMutationResult.Fail(
                    BootPageAllocatorMutationStatus.MetadataCapacityTooSmall,
                    state,
                    physicalStart,
                    physicalEndExclusive,
                    pageCount,
                    byteLength,
                    currentNodeIndex,
                    insertedNodeIndex,
                    visited);
            }
        }

        BootPageAllocatorState nextState = ApplyFree(
            state,
            nodes,
            previousNodeIndex,
            currentNodeIndex,
            insertedNodeIndex,
            physicalStart,
            physicalEndExclusive,
            pageCount,
            byteLength,
            mergesPrevious,
            mergesNext);

        return BootPageAllocatorMutationResult.Success(
            nextState,
            physicalStart,
            physicalEndExclusive,
            pageCount,
            byteLength,
            previousNodeIndex >= 0 ? previousNodeIndex : currentNodeIndex,
            insertedNodeIndex,
            visited);
    }

    private static BootPageAllocatorState ApplyAllocation(
        BootPageAllocatorState state,
        Span<BootPageFreeListNode> nodes,
        int previousNodeIndex,
        int currentNodeIndex,
        int insertedNodeIndex,
        BootPageFreeListNode node,
        ulong allocationStart,
        ulong allocationEnd,
        ulong pageCount,
        ulong byteLength,
        bool hasPrefix,
        bool hasSuffix)
    {
        int headNodeIndex = state.HeadNodeIndex;
        int freeNodeCount = state.FreeNodeCount;

        if (!hasPrefix && !hasSuffix)
        {
            int nextNodeIndex = node.NextNodeIndex;
            if (previousNodeIndex >= 0)
            {
                BootPageFreeListNode previous = nodes[previousNodeIndex];
                nodes[previousNodeIndex] = ReplaceNext(previous, nextNodeIndex);
            }
            else
            {
                headNodeIndex = nextNodeIndex;
            }

            nodes[currentNodeIndex] = default;
            freeNodeCount--;
        }
        else if (!hasPrefix)
        {
            nodes[currentNodeIndex] = CreateNode(
                node,
                allocationEnd,
                node.PhysicalEndExclusive,
                node.NextNodeIndex,
                state.PageSize);
        }
        else if (!hasSuffix)
        {
            nodes[currentNodeIndex] = CreateNode(
                node,
                node.PhysicalStart,
                allocationStart,
                node.NextNodeIndex,
                state.PageSize);
        }
        else
        {
            nodes[insertedNodeIndex] = CreateNode(
                node,
                allocationEnd,
                node.PhysicalEndExclusive,
                node.NextNodeIndex,
                state.PageSize);
            nodes[currentNodeIndex] = CreateNode(
                node,
                node.PhysicalStart,
                allocationStart,
                insertedNodeIndex,
                state.PageSize);
            freeNodeCount++;
        }

        return new BootPageAllocatorState(
            isInitialized: true,
            state.PageSize,
            headNodeIndex,
            freeNodeCount,
            state.FreePageCount - pageCount,
            state.FreeByteCount - byteLength,
            state.AllocatedPageCount + pageCount,
            state.AllocatedByteCount + byteLength);
    }

    private static BootPageAllocatorState ApplyFree(
        BootPageAllocatorState state,
        Span<BootPageFreeListNode> nodes,
        int previousNodeIndex,
        int currentNodeIndex,
        int insertedNodeIndex,
        ulong physicalStart,
        ulong physicalEndExclusive,
        ulong pageCount,
        ulong byteLength,
        bool mergesPrevious,
        bool mergesNext)
    {
        int headNodeIndex = state.HeadNodeIndex;
        int freeNodeCount = state.FreeNodeCount;

        if (mergesPrevious && mergesNext)
        {
            BootPageFreeListNode previous = nodes[previousNodeIndex];
            BootPageFreeListNode next = nodes[currentNodeIndex];
            nodes[previousNodeIndex] = CreateNode(
                previous,
                previous.PhysicalStart,
                next.PhysicalEndExclusive,
                next.NextNodeIndex,
                state.PageSize);
            nodes[currentNodeIndex] = default;
            freeNodeCount--;
        }
        else if (mergesPrevious)
        {
            BootPageFreeListNode previous = nodes[previousNodeIndex];
            nodes[previousNodeIndex] = CreateNode(
                previous,
                previous.PhysicalStart,
                physicalEndExclusive,
                previous.NextNodeIndex,
                state.PageSize);
        }
        else if (mergesNext)
        {
            BootPageFreeListNode next = nodes[currentNodeIndex];
            nodes[currentNodeIndex] = CreateNode(
                next,
                physicalStart,
                next.PhysicalEndExclusive,
                next.NextNodeIndex,
                state.PageSize);
        }
        else
        {
            nodes[insertedNodeIndex] = new BootPageFreeListNode(
                sourceFreeSegmentIndex: -1,
                sourceUsableRangeIndex: -1,
                physicalStart,
                physicalEndExclusive,
                pageCount,
                byteLength,
                currentNodeIndex);

            if (previousNodeIndex >= 0)
            {
                BootPageFreeListNode previous = nodes[previousNodeIndex];
                nodes[previousNodeIndex] = ReplaceNext(previous, insertedNodeIndex);
            }
            else
            {
                headNodeIndex = insertedNodeIndex;
            }

            freeNodeCount++;
        }

        return new BootPageAllocatorState(
            isInitialized: true,
            state.PageSize,
            headNodeIndex,
            freeNodeCount,
            state.FreePageCount + pageCount,
            state.FreeByteCount + byteLength,
            state.AllocatedPageCount - pageCount,
            state.AllocatedByteCount - byteLength);
    }

    private static BootPageAllocatorMutationStatus ValidateRequest(
        BootPageAllocatorState state,
        Span<BootPageFreeListNode> nodes,
        ulong pageCount,
        ulong alignment,
        out ulong byteLength)
    {
        byteLength = 0;

        if (!state.IsInitialized)
        {
            return BootPageAllocatorMutationStatus.StateNotInitialized;
        }

        if (!IsValidPageSize(state.PageSize))
        {
            return BootPageAllocatorMutationStatus.InvalidPageSize;
        }

        if (pageCount == 0)
        {
            return BootPageAllocatorMutationStatus.InvalidPageCount;
        }

        if (!IsValidAlignment(alignment, state.PageSize))
        {
            return BootPageAllocatorMutationStatus.InvalidAlignment;
        }

        if (nodes.Length < state.FreeNodeCount)
        {
            return BootPageAllocatorMutationStatus.NodeCapacityTooSmall;
        }

        if (pageCount > ulong.MaxValue / state.PageSize)
        {
            return BootPageAllocatorMutationStatus.AllocationByteCountOverflow;
        }

        byteLength = pageCount * state.PageSize;
        return BootPageAllocatorMutationStatus.Success;
    }

    private static BootPageAllocatorMutationStatus ValidateFreeList(
        BootPageAllocatorState state,
        ReadOnlySpan<BootPageFreeListNode> nodes,
        out int visitedNodeCount)
    {
        visitedNodeCount = 0;

        if (state.FreeNodeCount == 0)
        {
            if (state.HeadNodeIndex != -1 ||
                state.FreePageCount != 0 ||
                state.FreeByteCount != 0)
            {
                return BootPageAllocatorMutationStatus.FreeListNodeCountMismatch;
            }

            return BootPageAllocatorMutationStatus.Success;
        }

        if (!IsValidNodeIndex(state.HeadNodeIndex, nodes.Length))
        {
            return BootPageAllocatorMutationStatus.InvalidHeadNodeIndex;
        }

        int currentNodeIndex = state.HeadNodeIndex;
        ulong totalPages = 0;
        ulong totalBytes = 0;
        ulong previousStart = 0;
        ulong previousEndExclusive = 0;

        for (; visitedNodeCount < state.FreeNodeCount; visitedNodeCount++)
        {
            if (!IsValidNodeIndex(currentNodeIndex, nodes.Length))
            {
                return BootPageAllocatorMutationStatus.InvalidNextNodeIndex;
            }

            BootPageFreeListNode node = nodes[currentNodeIndex];
            if (!ValidateNodeShape(state.PageSize, node, out ulong byteLength, out ulong endExclusive))
            {
                return BootPageAllocatorMutationStatus.InvalidNodeShape;
            }

            if (visitedNodeCount != 0 &&
                (node.PhysicalStart < previousStart || node.PhysicalStart < previousEndExclusive))
            {
                return BootPageAllocatorMutationStatus.InvalidNodeShape;
            }

            if (!TryAdd(totalPages, node.PageCount, out totalPages))
            {
                return BootPageAllocatorMutationStatus.TotalPageCountMismatch;
            }

            if (!TryAdd(totalBytes, byteLength, out totalBytes))
            {
                return BootPageAllocatorMutationStatus.TotalByteCountMismatch;
            }

            previousStart = node.PhysicalStart;
            previousEndExclusive = endExclusive;

            int nextNodeIndex = node.NextNodeIndex;
            if (nextNodeIndex == currentNodeIndex)
            {
                visitedNodeCount++;
                return BootPageAllocatorMutationStatus.FreeListCycleDetected;
            }

            if (nextNodeIndex != -1 && !IsValidNodeIndex(nextNodeIndex, nodes.Length))
            {
                visitedNodeCount++;
                return BootPageAllocatorMutationStatus.InvalidNextNodeIndex;
            }

            currentNodeIndex = nextNodeIndex;
            if (currentNodeIndex == -1)
            {
                visitedNodeCount++;
                break;
            }
        }

        if (currentNodeIndex != -1)
        {
            return BootPageAllocatorMutationStatus.FreeListCycleDetected;
        }

        if (visitedNodeCount != state.FreeNodeCount)
        {
            return BootPageAllocatorMutationStatus.FreeListNodeCountMismatch;
        }

        if (totalPages != state.FreePageCount)
        {
            return BootPageAllocatorMutationStatus.TotalPageCountMismatch;
        }

        return totalBytes == state.FreeByteCount
            ? BootPageAllocatorMutationStatus.Success
            : BootPageAllocatorMutationStatus.TotalByteCountMismatch;
    }

    private static int FindUnusedNodeIndex(
        ReadOnlySpan<BootPageFreeListNode> nodes,
        BootPageAllocatorState state,
        int excludedNodeIndex)
    {
        for (int candidate = 0; candidate < nodes.Length; candidate++)
        {
            if (candidate == excludedNodeIndex)
            {
                continue;
            }

            if (!IsIndexInChain(candidate, nodes, state))
            {
                return candidate;
            }
        }

        return -1;
    }

    private static bool IsIndexInChain(
        int candidate,
        ReadOnlySpan<BootPageFreeListNode> nodes,
        BootPageAllocatorState state)
    {
        int currentNodeIndex = state.HeadNodeIndex;
        for (int visited = 0; visited < state.FreeNodeCount && currentNodeIndex != -1; visited++)
        {
            if (currentNodeIndex == candidate)
            {
                return true;
            }

            if (!IsValidNodeIndex(currentNodeIndex, nodes.Length))
            {
                return false;
            }

            currentNodeIndex = nodes[currentNodeIndex].NextNodeIndex;
        }

        return false;
    }

    private static BootPageFreeListNode ReplaceNext(BootPageFreeListNode node, int nextNodeIndex)
    {
        return new BootPageFreeListNode(
            node.SourceFreeSegmentIndex,
            node.SourceUsableRangeIndex,
            node.PhysicalStart,
            node.PhysicalEndExclusive,
            node.PageCount,
            node.ByteLength,
            nextNodeIndex);
    }

    private static BootPageFreeListNode CreateNode(
        BootPageFreeListNode source,
        ulong physicalStart,
        ulong physicalEndExclusive,
        int nextNodeIndex,
        ulong pageSize)
    {
        ulong byteLength = physicalEndExclusive - physicalStart;
        return new BootPageFreeListNode(
            source.SourceFreeSegmentIndex,
            source.SourceUsableRangeIndex,
            physicalStart,
            physicalEndExclusive,
            byteLength / pageSize,
            byteLength,
            nextNodeIndex);
    }

    private static bool ValidateNodeShape(
        ulong pageSize,
        BootPageFreeListNode node,
        out ulong byteLength,
        out ulong physicalEndExclusive)
    {
        byteLength = 0;
        physicalEndExclusive = 0;

        if (node.PageCount == 0 ||
            !IsAligned(node.PhysicalStart, pageSize) ||
            node.PageCount > ulong.MaxValue / pageSize)
        {
            return false;
        }

        byteLength = node.PageCount * pageSize;
        if (node.ByteLength != byteLength ||
            !TryAdd(node.PhysicalStart, byteLength, out physicalEndExclusive))
        {
            return false;
        }

        return node.PhysicalEndExclusive == physicalEndExclusive;
    }

    private static bool TryAlignUp(ulong value, ulong alignment, out ulong aligned)
    {
        ulong mask = alignment - 1;
        if ((value & mask) == 0)
        {
            aligned = value;
            return true;
        }

        ulong addend = alignment - (value & mask);
        return TryAdd(value, addend, out aligned);
    }

    private static bool IsValidNodeIndex(int nodeIndex, int capacity)
    {
        return nodeIndex >= 0 && nodeIndex < capacity;
    }

    private static bool IsValidPageSize(ulong pageSize)
    {
        return pageSize != 0 && (pageSize & (pageSize - 1)) == 0;
    }

    private static bool IsValidAlignment(ulong alignment, ulong pageSize)
    {
        return alignment >= pageSize &&
            (alignment & (alignment - 1)) == 0 &&
            (alignment % pageSize) == 0;
    }

    private static bool IsAligned(ulong value, ulong alignment)
    {
        return (value & (alignment - 1)) == 0;
    }

    private static bool TryAdd(ulong left, ulong right, out ulong value)
    {
        if (left > ulong.MaxValue - right)
        {
            value = 0;
            return false;
        }

        value = left + right;
        return true;
    }
}
