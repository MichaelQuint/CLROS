namespace OS.Kernel.Memory;

public static unsafe class BootPageAllocatorStateMaterializer
{
    public const int NoNodeIndex = -1;
    public const ulong MaximumSourceNodeCount = 4096;
    public const ulong MaximumSourceRangeCount = 4096;
    public const ulong RequiredDestinationStateCapacity = 1;
    public const ulong RequiredPointerAlignment = 8;
    public const ulong RequiredPageSize = 4096;
    private const ulong MaximumUnsignedLong = ~0UL;
    internal static ulong ActualSourceNodeStructureSize => (ulong)sizeof(BootPageFreeListNode);
    internal static ulong ActualDestinationStateStructureSize => (ulong)sizeof(BootPageAllocatorState);

    public static BootPageAllocatorStateMaterializationResult Materialize(BootPageFreeListNode* sourceNodes, ulong sourceNodeCount, ulong sourceNodeCapacity, ulong sourceRangeCount, int headNodeIndex, ulong pageSize, ulong expectedFreePages, ulong expectedFreeBytes, BootPageAllocatorState* destinationState, ulong destinationStateCapacity)
    {
        if (!TryValidatePointerAndSummaryMetadata(sourceNodes, sourceNodeCount, sourceNodeCapacity, sourceRangeCount, headNodeIndex, pageSize, expectedFreePages, expectedFreeBytes, destinationState, destinationStateCapacity, out BootPageAllocatorStateMaterializationResult failure))
        {
            return failure;
        }

        ulong observedFreePages = 0;
        ulong observedFreeBytes = 0;
        ulong visitedNodeCount = 0;
        int currentNodeIndex = headNodeIndex;
        int previousNodeIndex = NoNodeIndex;
        int previousSourceRangeIndex = NoNodeIndex;
        ulong previousPhysicalStart = 0;
        ulong previousPhysicalEndExclusive = 0;
        while (currentNodeIndex != NoNodeIndex)
        {
            if (visitedNodeCount >= sourceNodeCount)
            {
                return Fail(BootPageAllocatorStateMaterializationStatus.FreeListCycleDetected, sourceNodeCount, sourceNodeCapacity, sourceRangeCount, destinationStateCapacity, headNodeIndex, currentNodeIndex, currentNodeIndex, visitedNodeCount, pageSize, expectedFreePages, expectedFreeBytes, observedFreePages, observedFreeBytes);
            }

            if (!IsValidNodeIndex(currentNodeIndex, sourceNodeCount, sourceNodeCapacity))
            {
                return Fail(visitedNodeCount == 0 ? BootPageAllocatorStateMaterializationStatus.InvalidHeadNodeIndex : BootPageAllocatorStateMaterializationStatus.InvalidNextNodeIndex, sourceNodeCount, sourceNodeCapacity, sourceRangeCount, destinationStateCapacity, headNodeIndex, currentNodeIndex, previousNodeIndex, visitedNodeCount, pageSize, expectedFreePages, expectedFreeBytes, observedFreePages, observedFreeBytes);
            }

            BootPageFreeListNode node = sourceNodes[currentNodeIndex];
            BootPageAllocatorStateMaterializationStatus nodeStatus = ValidateNode(node, sourceRangeCount, visitedNodeCount, previousSourceRangeIndex, previousPhysicalStart, previousPhysicalEndExclusive, pageSize, out ulong nodeByteLength, out ulong nodePhysicalEndExclusive);
            if (nodeStatus != BootPageAllocatorStateMaterializationStatus.Success)
            {
                return Fail(nodeStatus, sourceNodeCount, sourceNodeCapacity, sourceRangeCount, destinationStateCapacity, headNodeIndex, currentNodeIndex, previousNodeIndex, visitedNodeCount, pageSize, expectedFreePages, expectedFreeBytes, observedFreePages, observedFreeBytes);
            }

            if (!TryAdd(observedFreePages, node.PageCount, out ulong nextObservedFreePages) || !TryAdd(observedFreeBytes, nodeByteLength, out ulong nextObservedFreeBytes))
            {
                return Fail(BootPageAllocatorStateMaterializationStatus.InvalidMaterializedState, sourceNodeCount, sourceNodeCapacity, sourceRangeCount, destinationStateCapacity, headNodeIndex, currentNodeIndex, previousNodeIndex, visitedNodeCount, pageSize, expectedFreePages, expectedFreeBytes, observedFreePages, observedFreeBytes);
            }

            observedFreePages = nextObservedFreePages;
            observedFreeBytes = nextObservedFreeBytes;
            previousNodeIndex = currentNodeIndex;
            previousSourceRangeIndex = node.SourceUsableRangeIndex;
            previousPhysicalStart = node.PhysicalStart;
            previousPhysicalEndExclusive = nodePhysicalEndExclusive;
            visitedNodeCount++;
            int nextNodeIndex = node.NextNodeIndex;
            if (nextNodeIndex != NoNodeIndex && !IsValidNodeIndex(nextNodeIndex, sourceNodeCount, sourceNodeCapacity))
            {
                return Fail(BootPageAllocatorStateMaterializationStatus.InvalidNextNodeIndex, sourceNodeCount, sourceNodeCapacity, sourceRangeCount, destinationStateCapacity, headNodeIndex, nextNodeIndex, currentNodeIndex, visitedNodeCount, pageSize, expectedFreePages, expectedFreeBytes, observedFreePages, observedFreeBytes);
            }

            if (nextNodeIndex != NoNodeIndex && WasVisited(sourceNodes, headNodeIndex, visitedNodeCount, nextNodeIndex))
            {
                return Fail(BootPageAllocatorStateMaterializationStatus.FreeListCycleDetected, sourceNodeCount, sourceNodeCapacity, sourceRangeCount, destinationStateCapacity, headNodeIndex, nextNodeIndex, currentNodeIndex, visitedNodeCount, pageSize, expectedFreePages, expectedFreeBytes, observedFreePages, observedFreeBytes);
            }

            currentNodeIndex = nextNodeIndex;
        }

        if (visitedNodeCount != sourceNodeCount)
        {
            return Fail(BootPageAllocatorStateMaterializationStatus.FreeListNodeCountMismatch, sourceNodeCount, sourceNodeCapacity, sourceRangeCount, destinationStateCapacity, headNodeIndex, NoNodeIndex, previousNodeIndex, visitedNodeCount, pageSize, expectedFreePages, expectedFreeBytes, observedFreePages, observedFreeBytes);
        }

        if (observedFreePages != expectedFreePages)
        {
            return Fail(BootPageAllocatorStateMaterializationStatus.TotalPageCountMismatch, sourceNodeCount, sourceNodeCapacity, sourceRangeCount, destinationStateCapacity, headNodeIndex, NoNodeIndex, NoNodeIndex, visitedNodeCount, pageSize, expectedFreePages, expectedFreeBytes, observedFreePages, observedFreeBytes);
        }

        if (observedFreeBytes != expectedFreeBytes)
        {
            return Fail(BootPageAllocatorStateMaterializationStatus.TotalByteCountMismatch, sourceNodeCount, sourceNodeCapacity, sourceRangeCount, destinationStateCapacity, headNodeIndex, NoNodeIndex, NoNodeIndex, visitedNodeCount, pageSize, expectedFreePages, expectedFreeBytes, observedFreePages, observedFreeBytes);
        }

        BootPageAllocatorState state = new BootPageAllocatorState(isInitialized: true, pageSize, headNodeIndex, freeNodeCount: (int)sourceNodeCount, expectedFreePages, expectedFreeBytes, allocatedPageCount: 0, allocatedByteCount: 0);
        if (!IsValidMaterializedState(state, sourceNodeCount, headNodeIndex, pageSize, expectedFreePages, expectedFreeBytes))
        {
            return Fail(BootPageAllocatorStateMaterializationStatus.InvalidMaterializedState, sourceNodeCount, sourceNodeCapacity, sourceRangeCount, destinationStateCapacity, headNodeIndex, NoNodeIndex, NoNodeIndex, visitedNodeCount, pageSize, expectedFreePages, expectedFreeBytes, observedFreePages, observedFreeBytes);
        }

        destinationState[0] = state;
        return BootPageAllocatorStateMaterializationResult.Success(sourceNodeCount, sourceNodeCapacity, sourceRangeCount, destinationStateCapacity, headNodeIndex, pageSize, expectedFreePages, expectedFreeBytes, state);
    }

    private static bool TryValidatePointerAndSummaryMetadata(BootPageFreeListNode* sourceNodes, ulong sourceNodeCount, ulong sourceNodeCapacity, ulong sourceRangeCount, int headNodeIndex, ulong pageSize, ulong expectedFreePages, ulong expectedFreeBytes, BootPageAllocatorState* destinationState, ulong destinationStateCapacity, out BootPageAllocatorStateMaterializationResult failure)
    {
        failure = default;
        ulong sourceNodeStructureSize = ActualSourceNodeStructureSize;
        ulong destinationStateStructureSize = ActualDestinationStateStructureSize;
        if (BootPageFreeListNode.StructureSize != sourceNodeStructureSize || BootPageAllocatorState.StructureSize != destinationStateStructureSize)
        {
            failure = FailBeforeTraversal(BootPageAllocatorStateMaterializationStatus.InvalidMaterializedState, sourceNodeCount, sourceNodeCapacity, sourceRangeCount, destinationStateCapacity, headNodeIndex, pageSize, expectedFreePages, expectedFreeBytes);
            return false;
        }

        if (sourceNodeCount == 0)
        {
            failure = FailBeforeTraversal(BootPageAllocatorStateMaterializationStatus.InvalidSourceNodeCount, sourceNodeCount, sourceNodeCapacity, sourceRangeCount, destinationStateCapacity, headNodeIndex, pageSize, expectedFreePages, expectedFreeBytes);
            return false;
        }

        if (sourceNodeCount > MaximumUnsignedLong / sourceNodeStructureSize || sourceNodeCapacity > MaximumUnsignedLong / sourceNodeStructureSize)
        {
            failure = FailBeforeTraversal(BootPageAllocatorStateMaterializationStatus.SourceNodeTableByteCountOverflow, sourceNodeCount, sourceNodeCapacity, sourceRangeCount, destinationStateCapacity, headNodeIndex, pageSize, expectedFreePages, expectedFreeBytes);
            return false;
        }

        if (sourceNodeCount > MaximumSourceNodeCount)
        {
            failure = FailBeforeTraversal(BootPageAllocatorStateMaterializationStatus.TooManySourceNodes, sourceNodeCount, sourceNodeCapacity, sourceRangeCount, destinationStateCapacity, headNodeIndex, pageSize, expectedFreePages, expectedFreeBytes);
            return false;
        }

        if (sourceNodeCapacity < sourceNodeCount)
        {
            failure = FailBeforeTraversal(BootPageAllocatorStateMaterializationStatus.SourceNodeCapacityTooSmall, sourceNodeCount, sourceNodeCapacity, sourceRangeCount, destinationStateCapacity, headNodeIndex, pageSize, expectedFreePages, expectedFreeBytes);
            return false;
        }

        ulong sourceNodeAddress = (ulong)sourceNodes;
        if (sourceNodeAddress == 0)
        {
            failure = FailBeforeTraversal(BootPageAllocatorStateMaterializationStatus.MissingSourceNodes, sourceNodeCount, sourceNodeCapacity, sourceRangeCount, destinationStateCapacity, headNodeIndex, pageSize, expectedFreePages, expectedFreeBytes);
            return false;
        }

        if ((sourceNodeAddress & (RequiredPointerAlignment - 1)) != 0)
        {
            failure = FailBeforeTraversal(BootPageAllocatorStateMaterializationStatus.MisalignedSourceNodes, sourceNodeCount, sourceNodeCapacity, sourceRangeCount, destinationStateCapacity, headNodeIndex, pageSize, expectedFreePages, expectedFreeBytes);
            return false;
        }

        ulong sourceNodeByteCount = sourceNodeCapacity * sourceNodeStructureSize;
        if (sourceNodeAddress > MaximumUnsignedLong - sourceNodeByteCount)
        {
            failure = FailBeforeTraversal(BootPageAllocatorStateMaterializationStatus.SourceNodeTableAddressRangeOverflow, sourceNodeCount, sourceNodeCapacity, sourceRangeCount, destinationStateCapacity, headNodeIndex, pageSize, expectedFreePages, expectedFreeBytes);
            return false;
        }

        if (destinationStateCapacity < RequiredDestinationStateCapacity)
        {
            failure = FailBeforeTraversal(BootPageAllocatorStateMaterializationStatus.DestinationStateCapacityTooSmall, sourceNodeCount, sourceNodeCapacity, sourceRangeCount, destinationStateCapacity, headNodeIndex, pageSize, expectedFreePages, expectedFreeBytes);
            return false;
        }

        if (destinationStateCapacity > MaximumUnsignedLong / destinationStateStructureSize)
        {
            failure = FailBeforeTraversal(BootPageAllocatorStateMaterializationStatus.DestinationStateByteCountOverflow, sourceNodeCount, sourceNodeCapacity, sourceRangeCount, destinationStateCapacity, headNodeIndex, pageSize, expectedFreePages, expectedFreeBytes);
            return false;
        }

        ulong destinationStateAddress = (ulong)destinationState;
        if (destinationStateAddress == 0)
        {
            failure = FailBeforeTraversal(BootPageAllocatorStateMaterializationStatus.MissingDestinationState, sourceNodeCount, sourceNodeCapacity, sourceRangeCount, destinationStateCapacity, headNodeIndex, pageSize, expectedFreePages, expectedFreeBytes);
            return false;
        }

        if ((destinationStateAddress & (RequiredPointerAlignment - 1)) != 0)
        {
            failure = FailBeforeTraversal(BootPageAllocatorStateMaterializationStatus.MisalignedDestinationState, sourceNodeCount, sourceNodeCapacity, sourceRangeCount, destinationStateCapacity, headNodeIndex, pageSize, expectedFreePages, expectedFreeBytes);
            return false;
        }

        ulong destinationStateByteCount = destinationStateCapacity * destinationStateStructureSize;
        if (destinationStateAddress > MaximumUnsignedLong - destinationStateByteCount)
        {
            failure = FailBeforeTraversal(BootPageAllocatorStateMaterializationStatus.DestinationStateAddressRangeOverflow, sourceNodeCount, sourceNodeCapacity, sourceRangeCount, destinationStateCapacity, headNodeIndex, pageSize, expectedFreePages, expectedFreeBytes);
            return false;
        }

        ulong sourceNodeEndExclusive = sourceNodeAddress + sourceNodeByteCount;
        ulong destinationStateEndExclusive = destinationStateAddress + destinationStateByteCount;
        if (sourceNodeAddress < destinationStateEndExclusive && destinationStateAddress < sourceNodeEndExclusive)
        {
            failure = FailBeforeTraversal(BootPageAllocatorStateMaterializationStatus.DestinationOverlapsSourceNodes, sourceNodeCount, sourceNodeCapacity, sourceRangeCount, destinationStateCapacity, headNodeIndex, pageSize, expectedFreePages, expectedFreeBytes);
            return false;
        }

        if (pageSize != RequiredPageSize)
        {
            failure = FailBeforeTraversal(BootPageAllocatorStateMaterializationStatus.InvalidPageSize, sourceNodeCount, sourceNodeCapacity, sourceRangeCount, destinationStateCapacity, headNodeIndex, pageSize, expectedFreePages, expectedFreeBytes);
            return false;
        }

        if (sourceRangeCount == 0 || sourceRangeCount > MaximumSourceRangeCount)
        {
            failure = FailBeforeTraversal(BootPageAllocatorStateMaterializationStatus.InvalidSourceRangeCount, sourceNodeCount, sourceNodeCapacity, sourceRangeCount, destinationStateCapacity, headNodeIndex, pageSize, expectedFreePages, expectedFreeBytes);
            return false;
        }

        if (!IsValidNodeIndex(headNodeIndex, sourceNodeCount, sourceNodeCapacity))
        {
            failure = FailBeforeTraversal(BootPageAllocatorStateMaterializationStatus.InvalidHeadNodeIndex, sourceNodeCount, sourceNodeCapacity, sourceRangeCount, destinationStateCapacity, headNodeIndex, pageSize, expectedFreePages, expectedFreeBytes, headNodeIndex);
            return false;
        }

        if (expectedFreePages == 0)
        {
            failure = FailBeforeTraversal(BootPageAllocatorStateMaterializationStatus.NoFreePages, sourceNodeCount, sourceNodeCapacity, sourceRangeCount, destinationStateCapacity, headNodeIndex, pageSize, expectedFreePages, expectedFreeBytes);
            return false;
        }

        if (expectedFreePages > MaximumUnsignedLong / pageSize)
        {
            failure = FailBeforeTraversal(BootPageAllocatorStateMaterializationStatus.ExpectedByteCountOverflow, sourceNodeCount, sourceNodeCapacity, sourceRangeCount, destinationStateCapacity, headNodeIndex, pageSize, expectedFreePages, expectedFreeBytes);
            return false;
        }

        if (expectedFreePages * pageSize != expectedFreeBytes)
        {
            failure = FailBeforeTraversal(BootPageAllocatorStateMaterializationStatus.ExpectedByteCountMismatch, sourceNodeCount, sourceNodeCapacity, sourceRangeCount, destinationStateCapacity, headNodeIndex, pageSize, expectedFreePages, expectedFreeBytes);
            return false;
        }

        return true;
    }

    private static BootPageAllocatorStateMaterializationStatus ValidateNode(BootPageFreeListNode node, ulong sourceRangeCount, ulong visitedNodeCount, int previousSourceRangeIndex, ulong previousPhysicalStart, ulong previousPhysicalEndExclusive, ulong pageSize, out ulong byteLength, out ulong physicalEndExclusive)
    {
        byteLength = 0;
        physicalEndExclusive = 0;
        if (node.SourceFreeSegmentIndex != (int)visitedNodeCount)
        {
            return BootPageAllocatorStateMaterializationStatus.InvalidSourceFreeSegmentIndex;
        }

        if (node.SourceUsableRangeIndex < 0 || (ulong)node.SourceUsableRangeIndex >= sourceRangeCount)
        {
            return BootPageAllocatorStateMaterializationStatus.InvalidSourceRangeIndex;
        }

        if (visitedNodeCount != 0 && node.SourceUsableRangeIndex < previousSourceRangeIndex)
        {
            return BootPageAllocatorStateMaterializationStatus.UnsortedSourceRangeIndex;
        }

        if (node.PageCount == 0)
        {
            return BootPageAllocatorStateMaterializationStatus.InvalidNodePageCount;
        }

        if (node.PhysicalStart == 0)
        {
            return BootPageAllocatorStateMaterializationStatus.InvalidNodePhysicalStart;
        }

        if ((node.PhysicalStart & (pageSize - 1)) != 0)
        {
            return BootPageAllocatorStateMaterializationStatus.UnalignedNodePhysicalStart;
        }

        if (node.PageCount > MaximumUnsignedLong / pageSize)
        {
            return BootPageAllocatorStateMaterializationStatus.NodeByteCountOverflow;
        }

        byteLength = node.PageCount * pageSize;
        if (node.ByteLength != byteLength)
        {
            return BootPageAllocatorStateMaterializationStatus.NodeByteLengthMismatch;
        }

        if (!TryAdd(node.PhysicalStart, byteLength, out physicalEndExclusive))
        {
            return BootPageAllocatorStateMaterializationStatus.NodeAddressRangeOverflow;
        }

        if (node.PhysicalEndExclusive != physicalEndExclusive)
        {
            return BootPageAllocatorStateMaterializationStatus.NodePhysicalEndMismatch;
        }

        if (visitedNodeCount != 0)
        {
            if (node.PhysicalStart < previousPhysicalStart)
            {
                return BootPageAllocatorStateMaterializationStatus.UnsortedNode;
            }

            if (node.PhysicalStart < previousPhysicalEndExclusive)
            {
                return BootPageAllocatorStateMaterializationStatus.OverlappingNode;
            }
        }

        return BootPageAllocatorStateMaterializationStatus.Success;
    }

    private static bool WasVisited(BootPageFreeListNode* sourceNodes, int headNodeIndex, ulong visitedNodeCount, int candidateNodeIndex)
    {
        int visitedNodeIndex = headNodeIndex;
        for (ulong visitIndex = 0; visitIndex < visitedNodeCount; visitIndex++)
        {
            if (visitedNodeIndex == candidateNodeIndex)
            {
                return true;
            }

            visitedNodeIndex = sourceNodes[visitedNodeIndex].NextNodeIndex;
        }

        return false;
    }

    private static bool IsValidNodeIndex(int nodeIndex, ulong sourceNodeCount, ulong sourceNodeCapacity)
    {
        return nodeIndex >= 0 && (ulong)nodeIndex < sourceNodeCount && (ulong)nodeIndex < sourceNodeCapacity;
    }

    private static bool IsValidMaterializedState(BootPageAllocatorState state, ulong sourceNodeCount, int headNodeIndex, ulong pageSize, ulong expectedFreePages, ulong expectedFreeBytes)
    {
        return state.IsInitialized && state.PageSize == pageSize && state.HeadNodeIndex == headNodeIndex && state.FreeNodeCount == (int)sourceNodeCount && state.FreePageCount == expectedFreePages && state.FreeByteCount == expectedFreeBytes && state.AllocatedPageCount == 0 && state.AllocatedByteCount == 0;
    }

    internal static bool IsRepresentableSuccessSummary(ulong sourceNodeCount, ulong sourceNodeCapacity, ulong destinationStateCapacity, ulong pageSize, ulong expectedFreePages, ulong expectedFreeBytes)
    {
        ulong sourceNodeStructureSize = ActualSourceNodeStructureSize;
        ulong destinationStateStructureSize = ActualDestinationStateStructureSize;
        return BootPageFreeListNode.StructureSize == sourceNodeStructureSize && BootPageAllocatorState.StructureSize == destinationStateStructureSize && sourceNodeCount <= MaximumUnsignedLong / sourceNodeStructureSize && sourceNodeCapacity <= MaximumUnsignedLong / sourceNodeStructureSize && destinationStateCapacity <= MaximumUnsignedLong / destinationStateStructureSize && pageSize == RequiredPageSize && expectedFreePages != 0 && expectedFreePages <= MaximumUnsignedLong / pageSize && expectedFreePages * pageSize == expectedFreeBytes;
    }

    private static bool TryAdd(ulong left, ulong right, out ulong value)
    {
        if (left > MaximumUnsignedLong - right)
        {
            value = 0;
            return false;
        }

        value = left + right;
        return true;
    }

    private static BootPageAllocatorStateMaterializationResult FailBeforeTraversal(BootPageAllocatorStateMaterializationStatus status, ulong sourceNodeCount, ulong sourceNodeCapacity, ulong sourceRangeCount, ulong destinationStateCapacity, int headNodeIndex, ulong pageSize, ulong expectedFreePages, ulong expectedFreeBytes, int invalidNodeIndex = NoNodeIndex)
    {
        return Fail(status, sourceNodeCount, sourceNodeCapacity, sourceRangeCount, destinationStateCapacity, headNodeIndex, invalidNodeIndex, conflictingNodeIndex: NoNodeIndex, visitedNodeCount: 0, pageSize, expectedFreePages, expectedFreeBytes, observedFreePages: 0, observedFreeBytes: 0);
    }

    private static BootPageAllocatorStateMaterializationResult Fail(BootPageAllocatorStateMaterializationStatus status, ulong sourceNodeCount, ulong sourceNodeCapacity, ulong sourceRangeCount, ulong destinationStateCapacity, int headNodeIndex, int invalidNodeIndex, int conflictingNodeIndex, ulong visitedNodeCount, ulong pageSize, ulong expectedFreePages, ulong expectedFreeBytes, ulong observedFreePages, ulong observedFreeBytes)
    {
        return BootPageAllocatorStateMaterializationResult.Fail(status, sourceNodeCount, sourceNodeCapacity, sourceRangeCount, destinationStateCapacity, headNodeIndex, invalidNodeIndex, conflictingNodeIndex, visitedNodeCount, pageSize, expectedFreePages, expectedFreeBytes, observedFreePages, observedFreeBytes);
    }
}
