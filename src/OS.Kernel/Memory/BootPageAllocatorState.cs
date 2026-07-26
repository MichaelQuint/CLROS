namespace OS.Kernel.Memory;

public enum BootPageAllocatorStateStatus
{
    Success = 0,
    FreeListPlanFailed = 1,
    InvalidPageSize = 2,
    NoFreeNodes = 3,
    NoFreePages = 4,
    SourceNodeCapacityTooSmall = 5,
    InvalidHeadNodeIndex = 6,
    InvalidNodePageCount = 7,
    UnalignedNodePhysicalStart = 8,
    NodeByteCountOverflow = 9,
    NodeByteLengthMismatch = 10,
    NodeAddressRangeOverflow = 11,
    NodePhysicalEndMismatch = 12,
    InvalidNextNodeIndex = 13,
    FreeListCycleDetected = 14,
    FreeListNodeCountMismatch = 15,
    TotalPageCountMismatch = 16,
    TotalByteCountMismatch = 17
}

public readonly struct BootPageAllocatorStateInitResult
{
    private BootPageAllocatorStateInitResult(
        BootPageAllocatorStateStatus status,
        BootPageFreeListPlanStatus freeListPlanStatus,
        int requiredNodeCapacity,
        int availableNodeCapacity,
        int visitedNodeCount,
        int invalidNodeIndex,
        ulong pageSize,
        ulong totalFreePages,
        ulong totalFreeBytes,
        BootPageAllocatorState state)
    {
        Status = status;
        FreeListPlanStatus = freeListPlanStatus;
        RequiredNodeCapacity = requiredNodeCapacity;
        AvailableNodeCapacity = availableNodeCapacity;
        VisitedNodeCount = visitedNodeCount;
        InvalidNodeIndex = invalidNodeIndex;
        PageSize = pageSize;
        TotalFreePages = totalFreePages;
        TotalFreeBytes = totalFreeBytes;
        State = state;
    }

    public BootPageAllocatorStateStatus Status { get; }

    public bool Succeeded => Status == BootPageAllocatorStateStatus.Success && State.IsInitialized;

    public BootPageFreeListPlanStatus FreeListPlanStatus { get; }

    public int RequiredNodeCapacity { get; }

    public int AvailableNodeCapacity { get; }

    public int VisitedNodeCount { get; }

    public int InvalidNodeIndex { get; }

    public ulong PageSize { get; }

    public ulong TotalFreePages { get; }

    public ulong TotalFreeBytes { get; }

    public BootPageAllocatorState State { get; }

    internal static BootPageAllocatorStateInitResult Success(
        BootPageFreeListPlanResult freeListPlan,
        int requiredNodeCapacity,
        int availableNodeCapacity,
        int visitedNodeCount,
        ulong totalFreePages,
        ulong totalFreeBytes)
    {
        return new BootPageAllocatorStateInitResult(
            BootPageAllocatorStateStatus.Success,
            freeListPlan.Status,
            requiredNodeCapacity,
            availableNodeCapacity,
            visitedNodeCount,
            invalidNodeIndex: -1,
            freeListPlan.PageSize,
            totalFreePages,
            totalFreeBytes,
            new BootPageAllocatorState(
                isInitialized: true,
                freeListPlan.PageSize,
                freeListPlan.HeadNodeIndex,
                freeListPlan.FreeSegmentCount,
                totalFreePages,
                totalFreeBytes,
                allocatedPageCount: 0,
                allocatedByteCount: 0));
    }

    internal static BootPageAllocatorStateInitResult Fail(
        BootPageAllocatorStateStatus status,
        BootPageFreeListPlanResult freeListPlan,
        int requiredNodeCapacity,
        int availableNodeCapacity,
        int visitedNodeCount,
        int invalidNodeIndex,
        ulong totalFreePages,
        ulong totalFreeBytes)
    {
        return new BootPageAllocatorStateInitResult(
            status,
            freeListPlan.Status,
            requiredNodeCapacity,
            availableNodeCapacity,
            visitedNodeCount,
            invalidNodeIndex,
            freeListPlan.PageSize,
            totalFreePages,
            totalFreeBytes,
            default);
    }
}

public static class BootPageAllocatorStateInitializer
{
    public static BootPageAllocatorStateInitResult Initialize(
        BootPageFreeListPlanResult freeListPlan,
        ReadOnlySpan<BootPageFreeListNode> nodes)
    {
        int requiredNodeCapacity = GetRequiredNodeCapacity(freeListPlan);

        if (!freeListPlan.Succeeded)
        {
            return Fail(
                BootPageAllocatorStateStatus.FreeListPlanFailed,
                freeListPlan,
                requiredNodeCapacity,
                nodes,
                visitedNodeCount: 0,
                invalidNodeIndex: -1,
                totalFreePages: freeListPlan.TotalFreePages,
                totalFreeBytes: freeListPlan.TotalFreeBytes);
        }

        if (!IsValidPageSize(freeListPlan.PageSize))
        {
            return Fail(
                BootPageAllocatorStateStatus.InvalidPageSize,
                freeListPlan,
                requiredNodeCapacity,
                nodes,
                visitedNodeCount: 0,
                invalidNodeIndex: -1,
                totalFreePages: 0,
                totalFreeBytes: 0);
        }

        if (freeListPlan.FreeSegmentCount <= 0)
        {
            return Fail(
                BootPageAllocatorStateStatus.NoFreeNodes,
                freeListPlan,
                requiredNodeCapacity,
                nodes,
                visitedNodeCount: 0,
                invalidNodeIndex: -1,
                totalFreePages: 0,
                totalFreeBytes: 0);
        }

        if (freeListPlan.TotalFreePages == 0)
        {
            return Fail(
                BootPageAllocatorStateStatus.NoFreePages,
                freeListPlan,
                requiredNodeCapacity,
                nodes,
                visitedNodeCount: 0,
                invalidNodeIndex: -1,
                totalFreePages: 0,
                totalFreeBytes: 0);
        }

        if (nodes.Length < requiredNodeCapacity)
        {
            return Fail(
                BootPageAllocatorStateStatus.SourceNodeCapacityTooSmall,
                freeListPlan,
                requiredNodeCapacity,
                nodes,
                visitedNodeCount: 0,
                invalidNodeIndex: -1,
                totalFreePages: 0,
                totalFreeBytes: 0);
        }

        if (!IsValidNodeIndex(freeListPlan.HeadNodeIndex, requiredNodeCapacity, nodes.Length))
        {
            return Fail(
                BootPageAllocatorStateStatus.InvalidHeadNodeIndex,
                freeListPlan,
                requiredNodeCapacity,
                nodes,
                visitedNodeCount: 0,
                freeListPlan.HeadNodeIndex,
                totalFreePages: 0,
                totalFreeBytes: 0);
        }

        ulong totalPages = 0;
        ulong totalBytes = 0;
        int currentNodeIndex = freeListPlan.HeadNodeIndex;
        int visitedNodeCount = 0;

        for (; visitedNodeCount < freeListPlan.FreeSegmentCount; visitedNodeCount++)
        {
            if (!IsValidNodeIndex(currentNodeIndex, requiredNodeCapacity, nodes.Length))
            {
                return Fail(
                    BootPageAllocatorStateStatus.InvalidNextNodeIndex,
                    freeListPlan,
                    requiredNodeCapacity,
                    nodes,
                    visitedNodeCount,
                    currentNodeIndex,
                    totalPages,
                    totalBytes);
            }

            BootPageFreeListNode node = nodes[currentNodeIndex];
            BootPageAllocatorStateStatus nodeStatus = ValidateNode(
                freeListPlan.PageSize,
                node,
                out ulong byteLength,
                out ulong physicalEndExclusive);
            if (nodeStatus != BootPageAllocatorStateStatus.Success)
            {
                return Fail(
                    nodeStatus,
                    freeListPlan,
                    requiredNodeCapacity,
                    nodes,
                    visitedNodeCount,
                    currentNodeIndex,
                    totalPages,
                    totalBytes);
            }

            if (!TryAdd(totalPages, node.PageCount, out ulong nextTotalPages))
            {
                return Fail(
                    BootPageAllocatorStateStatus.TotalPageCountMismatch,
                    freeListPlan,
                    requiredNodeCapacity,
                    nodes,
                    visitedNodeCount,
                    currentNodeIndex,
                    totalPages,
                    totalBytes);
            }

            if (!TryAdd(totalBytes, byteLength, out ulong nextTotalBytes))
            {
                return Fail(
                    BootPageAllocatorStateStatus.TotalByteCountMismatch,
                    freeListPlan,
                    requiredNodeCapacity,
                    nodes,
                    visitedNodeCount,
                    currentNodeIndex,
                    totalPages,
                    totalBytes);
            }

            totalPages = nextTotalPages;
            totalBytes = nextTotalBytes;

            int nextNodeIndex = node.NextNodeIndex;
            if (nextNodeIndex == currentNodeIndex)
            {
                return Fail(
                    BootPageAllocatorStateStatus.FreeListCycleDetected,
                    freeListPlan,
                    requiredNodeCapacity,
                    nodes,
                    visitedNodeCount + 1,
                    nextNodeIndex,
                    totalPages,
                    totalBytes);
            }

            if (nextNodeIndex != -1 && !IsValidNodeIndex(nextNodeIndex, requiredNodeCapacity, nodes.Length))
            {
                return Fail(
                    BootPageAllocatorStateStatus.InvalidNextNodeIndex,
                    freeListPlan,
                    requiredNodeCapacity,
                    nodes,
                    visitedNodeCount + 1,
                    nextNodeIndex,
                    totalPages,
                    totalBytes);
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
            return Fail(
                BootPageAllocatorStateStatus.FreeListCycleDetected,
                freeListPlan,
                requiredNodeCapacity,
                nodes,
                visitedNodeCount,
                currentNodeIndex,
                totalPages,
                totalBytes);
        }

        if (visitedNodeCount != freeListPlan.FreeSegmentCount)
        {
            return Fail(
                BootPageAllocatorStateStatus.FreeListNodeCountMismatch,
                freeListPlan,
                requiredNodeCapacity,
                nodes,
                visitedNodeCount,
                invalidNodeIndex: -1,
                totalPages,
                totalBytes);
        }

        if (totalPages != freeListPlan.TotalFreePages)
        {
            return Fail(
                BootPageAllocatorStateStatus.TotalPageCountMismatch,
                freeListPlan,
                requiredNodeCapacity,
                nodes,
                visitedNodeCount,
                invalidNodeIndex: -1,
                totalPages,
                totalBytes);
        }

        if (totalBytes != freeListPlan.TotalFreeBytes)
        {
            return Fail(
                BootPageAllocatorStateStatus.TotalByteCountMismatch,
                freeListPlan,
                requiredNodeCapacity,
                nodes,
                visitedNodeCount,
                invalidNodeIndex: -1,
                totalPages,
                totalBytes);
        }

        return BootPageAllocatorStateInitResult.Success(
            freeListPlan,
            requiredNodeCapacity,
            nodes.Length,
            visitedNodeCount,
            totalPages,
            totalBytes);
    }

    private static BootPageAllocatorStateInitResult Fail(
        BootPageAllocatorStateStatus status,
        BootPageFreeListPlanResult freeListPlan,
        int requiredNodeCapacity,
        ReadOnlySpan<BootPageFreeListNode> nodes,
        int visitedNodeCount,
        int invalidNodeIndex,
        ulong totalFreePages,
        ulong totalFreeBytes)
    {
        return BootPageAllocatorStateInitResult.Fail(
            status,
            freeListPlan,
            requiredNodeCapacity,
            nodes.Length,
            visitedNodeCount,
            invalidNodeIndex,
            totalFreePages,
            totalFreeBytes);
    }

    private static BootPageAllocatorStateStatus ValidateNode(
        ulong pageSize,
        BootPageFreeListNode node,
        out ulong byteLength,
        out ulong physicalEndExclusive)
    {
        byteLength = 0;
        physicalEndExclusive = 0;

        if (node.PageCount == 0)
        {
            return BootPageAllocatorStateStatus.InvalidNodePageCount;
        }

        if (!IsAligned(node.PhysicalStart, pageSize))
        {
            return BootPageAllocatorStateStatus.UnalignedNodePhysicalStart;
        }

        if (node.PageCount > ulong.MaxValue / pageSize)
        {
            return BootPageAllocatorStateStatus.NodeByteCountOverflow;
        }

        byteLength = node.PageCount * pageSize;
        if (node.ByteLength != byteLength)
        {
            return BootPageAllocatorStateStatus.NodeByteLengthMismatch;
        }

        if (!TryAdd(node.PhysicalStart, byteLength, out physicalEndExclusive))
        {
            return BootPageAllocatorStateStatus.NodeAddressRangeOverflow;
        }

        return node.PhysicalEndExclusive == physicalEndExclusive
            ? BootPageAllocatorStateStatus.Success
            : BootPageAllocatorStateStatus.NodePhysicalEndMismatch;
    }

    private static int GetRequiredNodeCapacity(BootPageFreeListPlanResult freeListPlan)
    {
        return freeListPlan.RequiredNodeCapacity > freeListPlan.FreeSegmentCount
            ? freeListPlan.RequiredNodeCapacity
            : freeListPlan.FreeSegmentCount;
    }

    private static bool IsValidNodeIndex(int nodeIndex, int requiredNodeCapacity, int availableNodeCapacity)
    {
        return nodeIndex >= 0 && nodeIndex < requiredNodeCapacity && nodeIndex < availableNodeCapacity;
    }

    private static bool IsValidPageSize(ulong pageSize)
    {
        return pageSize != 0 && (pageSize & (pageSize - 1)) == 0;
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
