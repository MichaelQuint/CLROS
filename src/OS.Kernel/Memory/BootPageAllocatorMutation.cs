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
    FreeRangeNotAllocated = 18,
    ContractLayoutMismatch = 19,
    NodeTableByteCountOverflow = 20,
    TooManyNodes = 21,
    MissingNodes = 22,
    MisalignedNodes = 23,
    NodeTableAddressRangeOverflow = 24,
    ReservationTableByteCountOverflow = 25,
    TooManyReservations = 26,
    ReservationCapacityTooSmall = 27,
    MissingReservations = 28,
    MisalignedReservations = 29,
    ReservationTableAddressRangeOverflow = 30,
    NodeReservationTablesOverlap = 31,
    InvalidStateAccounting = 32,
    InvalidReservation = 33,
    UnsortedReservation = 34,
    OverlappingReservation = 35,
    ReservationOverlapsFreeRange = 36,
    AllocatedPageCountOverflow = 37,
    AllocatedByteCountOverflow = 38,
    AllocationAddressConstructionFailed = 39
}

public readonly struct BootPageAllocatorOnePageRestoreReceipt
{
    private readonly byte _isInitialized;

    private BootPageAllocatorOnePageRestoreReceipt(
        BootPageFreeListNode sourceNodeBefore,
        int previousNodeIndexBefore,
        BootPageFreeListNode previousNodeBefore)
    {
        _isInitialized = 1;
        SourceNodeBefore = sourceNodeBefore;
        PreviousNodeIndexBefore = previousNodeIndexBefore;
        PreviousNodeBefore = previousNodeBefore;
    }

    public bool IsInitialized => _isInitialized == 1;

    public BootPageFreeListNode SourceNodeBefore { get; }

    public int PreviousNodeIndexBefore { get; }

    public BootPageFreeListNode PreviousNodeBefore { get; }

    internal static BootPageAllocatorOnePageRestoreReceipt Create(
        BootPageFreeListNode sourceNodeBefore,
        int previousNodeIndexBefore,
        BootPageFreeListNode previousNodeBefore)
    {
        return new BootPageAllocatorOnePageRestoreReceipt(
            sourceNodeBefore,
            previousNodeIndexBefore,
            previousNodeBefore);
    }
}

public enum BootPageAllocatorOnePageCoalescingKind : byte
{
    Uninitialized = 0,
    None = 1,
    Previous = 2,
    Next = 3,
    PreviousAndNext = 4
}

public readonly struct BootPageAllocatorOnePageResult
{
    private readonly byte _isInitialized;

    private BootPageAllocatorOnePageResult(
        BootPageAllocatorMutationStatus status,
        BootReservedRangeStatus reservationStatus,
        OS.Boot.AddressValueStatus addressStatus,
        BootPageAllocatorState beforeState,
        BootPageAllocatorState afterState,
        OS.Boot.PhysicalAddress physicalStart,
        OS.Boot.PhysicalAddress physicalEndExclusive,
        ulong pageCount,
        ulong byteLength,
        ulong nodeCount,
        ulong nodeCapacity,
        ulong reservationCount,
        ulong reservationCapacity,
        int nodeIndex,
        int insertedNodeIndex,
        ulong visitedNodeCount,
        ulong invalidReservationIndex,
        ulong conflictingIndex,
        BootPageAllocatorOnePageRestoreReceipt restoreReceipt)
    {
        _isInitialized = 1;
        Status = status;
        ReservationStatus = reservationStatus;
        AddressStatus = addressStatus;
        BeforeState = beforeState;
        AfterState = afterState;
        PhysicalStart = physicalStart;
        PhysicalEndExclusive = physicalEndExclusive;
        PageCount = pageCount;
        ByteLength = byteLength;
        NodeCount = nodeCount;
        NodeCapacity = nodeCapacity;
        ReservationCount = reservationCount;
        ReservationCapacity = reservationCapacity;
        NodeIndex = nodeIndex;
        InsertedNodeIndex = insertedNodeIndex;
        VisitedNodeCount = visitedNodeCount;
        InvalidReservationIndex = invalidReservationIndex;
        ConflictingIndex = conflictingIndex;
        RestoreReceipt = restoreReceipt;
    }

    public bool IsInitialized => _isInitialized == 1;

    public BootPageAllocatorMutationStatus Status { get; }

    public BootReservedRangeStatus ReservationStatus { get; }

    public OS.Boot.AddressValueStatus AddressStatus { get; }

    public bool Succeeded =>
        IsInitialized
        && Status == BootPageAllocatorMutationStatus.Success
        && ReservationStatus == BootReservedRangeStatus.Success
        && AddressStatus == OS.Boot.AddressValueStatus.Success
        && InvalidReservationIndex == BootPageAllocatorMutator.NoIndex
        && ConflictingIndex == BootPageAllocatorMutator.NoIndex
        && BootPageAllocatorMutator.IsRepresentableSuccessSummary(
            BeforeState,
            AfterState,
            PhysicalStart,
            PhysicalEndExclusive,
            PageCount,
            ByteLength,
            NodeCount,
            NodeCapacity,
            ReservationCount,
            ReservationCapacity,
            NodeIndex,
            InsertedNodeIndex,
            VisitedNodeCount);

    public BootPageAllocatorState BeforeState { get; }

    public BootPageAllocatorState AfterState { get; }

    public OS.Boot.PhysicalAddress PhysicalStart { get; }

    public OS.Boot.PhysicalAddress PhysicalEndExclusive { get; }

    public ulong PageCount { get; }

    public ulong ByteLength { get; }

    public ulong NodeCount { get; }

    public ulong NodeCapacity { get; }

    public ulong ReservationCount { get; }

    public ulong ReservationCapacity { get; }

    public int NodeIndex { get; }

    public int InsertedNodeIndex { get; }

    public ulong VisitedNodeCount { get; }

    public ulong InvalidReservationIndex { get; }

    public ulong ConflictingIndex { get; }

    public BootPageAllocatorOnePageRestoreReceipt RestoreReceipt { get; }

    public bool CanRestore => Succeeded && RestoreReceipt.IsInitialized;

    public int BeforeFreeNodeCount => BeforeState.FreeNodeCount;

    public int AfterFreeNodeCount => AfterState.FreeNodeCount;

    public ulong BeforeFreePageCount => BeforeState.FreePageCount;

    public ulong AfterFreePageCount => AfterState.FreePageCount;

    public ulong BeforeFreeByteCount => BeforeState.FreeByteCount;

    public ulong AfterFreeByteCount => AfterState.FreeByteCount;

    public ulong BeforeAllocatedPageCount => BeforeState.AllocatedPageCount;

    public ulong AfterAllocatedPageCount => AfterState.AllocatedPageCount;

    public ulong BeforeAllocatedByteCount => BeforeState.AllocatedByteCount;

    public ulong AfterAllocatedByteCount => AfterState.AllocatedByteCount;

    internal static BootPageAllocatorOnePageResult Success(
        BootPageAllocatorState beforeState,
        BootPageAllocatorState afterState,
        OS.Boot.PhysicalAddress physicalStart,
        OS.Boot.PhysicalAddress physicalEndExclusive,
        ulong nodeCount,
        ulong nodeCapacity,
        ulong reservationCount,
        ulong reservationCapacity,
        int nodeIndex,
        int insertedNodeIndex,
        ulong visitedNodeCount)
    {
        return new BootPageAllocatorOnePageResult(
            BootPageAllocatorMutationStatus.Success,
            BootReservedRangeStatus.Success,
            OS.Boot.AddressValueStatus.Success,
            beforeState,
            afterState,
            physicalStart,
            physicalEndExclusive,
            BootPageAllocatorMutator.RequiredPageCount,
            BootPageAllocatorMutator.RequiredPageSize,
            nodeCount,
            nodeCapacity,
            reservationCount,
            reservationCapacity,
            nodeIndex,
            insertedNodeIndex,
            visitedNodeCount,
            BootPageAllocatorMutator.NoIndex,
            BootPageAllocatorMutator.NoIndex,
            default);
    }

    internal static BootPageAllocatorOnePageResult
        SuccessWithRestoreReceipt(
            BootPageAllocatorState beforeState,
            BootPageAllocatorState afterState,
            OS.Boot.PhysicalAddress physicalStart,
            OS.Boot.PhysicalAddress physicalEndExclusive,
            ulong nodeCount,
            ulong nodeCapacity,
            ulong reservationCount,
            ulong reservationCapacity,
            int nodeIndex,
            int insertedNodeIndex,
            ulong visitedNodeCount,
            BootPageFreeListNode sourceNodeBefore,
            int previousNodeIndexBefore,
            BootPageFreeListNode previousNodeBefore)
    {
        return new BootPageAllocatorOnePageResult(
            BootPageAllocatorMutationStatus.Success,
            BootReservedRangeStatus.Success,
            OS.Boot.AddressValueStatus.Success,
            beforeState,
            afterState,
            physicalStart,
            physicalEndExclusive,
            BootPageAllocatorMutator.RequiredPageCount,
            BootPageAllocatorMutator.RequiredPageSize,
            nodeCount,
            nodeCapacity,
            reservationCount,
            reservationCapacity,
            nodeIndex,
            insertedNodeIndex,
            visitedNodeCount,
            BootPageAllocatorMutator.NoIndex,
            BootPageAllocatorMutator.NoIndex,
            BootPageAllocatorOnePageRestoreReceipt.Create(
                sourceNodeBefore,
                previousNodeIndexBefore,
                previousNodeBefore));
    }

    internal static BootPageAllocatorOnePageResult Fail(
        BootPageAllocatorMutationStatus status,
        BootReservedRangeStatus reservationStatus,
        OS.Boot.AddressValueStatus addressStatus,
        BootPageAllocatorState state,
        ulong pageCount,
        ulong byteLength,
        ulong nodeCount,
        ulong nodeCapacity,
        ulong reservationCount,
        ulong reservationCapacity,
        int nodeIndex,
        int insertedNodeIndex,
        ulong visitedNodeCount,
        ulong invalidReservationIndex,
        ulong conflictingIndex)
    {
        return new BootPageAllocatorOnePageResult(
            status,
            reservationStatus,
            addressStatus,
            state,
            state,
            default,
            default,
            pageCount,
            byteLength,
            nodeCount,
            nodeCapacity,
            reservationCount,
            reservationCapacity,
            nodeIndex,
            insertedNodeIndex,
            visitedNodeCount,
            invalidReservationIndex,
            conflictingIndex,
            default);
    }
}

public readonly struct BootPageAllocatorOnePageFreeResult
{
    private readonly byte _isInitialized;

    private BootPageAllocatorOnePageFreeResult(
        BootPageAllocatorMutationStatus status,
        BootReservedRangeStatus reservationStatus,
        OS.Boot.AddressValueStatus addressStatus,
        BootPageAllocatorState beforeState,
        BootPageAllocatorState afterState,
        BootPageAllocatorOnePageResult allocation,
        BootPageAllocatorOnePageCoalescingKind coalescingKind,
        ulong nodeCount,
        ulong nodeCapacity,
        ulong reservationCount,
        ulong reservationCapacity,
        int restoredNodeIndex,
        int previousNodeIndex,
        int removedSuffixNodeIndex,
        ulong visitedNodeCount,
        ulong invalidReservationIndex,
        ulong conflictingIndex)
    {
        _isInitialized = 1;
        Status = status;
        ReservationStatus = reservationStatus;
        AddressStatus = addressStatus;
        BeforeState = beforeState;
        AfterState = afterState;
        Allocation = allocation;
        CoalescingKind = coalescingKind;
        NodeCount = nodeCount;
        NodeCapacity = nodeCapacity;
        ReservationCount = reservationCount;
        ReservationCapacity = reservationCapacity;
        RestoredNodeIndex = restoredNodeIndex;
        PreviousNodeIndex = previousNodeIndex;
        RemovedSuffixNodeIndex = removedSuffixNodeIndex;
        VisitedNodeCount = visitedNodeCount;
        InvalidReservationIndex = invalidReservationIndex;
        ConflictingIndex = conflictingIndex;
    }

    public bool IsInitialized => _isInitialized == 1;

    public BootPageAllocatorMutationStatus Status { get; }

    public BootReservedRangeStatus ReservationStatus { get; }

    public OS.Boot.AddressValueStatus AddressStatus { get; }

    public bool Succeeded =>
        IsInitialized
        && Status == BootPageAllocatorMutationStatus.Success
        && ReservationStatus == BootReservedRangeStatus.Success
        && AddressStatus == OS.Boot.AddressValueStatus.Success
        && InvalidReservationIndex == BootPageAllocatorMutator.NoIndex
        && ConflictingIndex == BootPageAllocatorMutator.NoIndex
        && BootPageAllocatorMutator.IsRepresentableFreeSuccessSummary(
            BeforeState,
            AfterState,
            Allocation,
            CoalescingKind,
            NodeCount,
            NodeCapacity,
            ReservationCount,
            ReservationCapacity,
            RestoredNodeIndex,
            PreviousNodeIndex,
            RemovedSuffixNodeIndex,
            VisitedNodeCount);

    public BootPageAllocatorState BeforeState { get; }

    public BootPageAllocatorState AfterState { get; }

    public BootPageAllocatorOnePageResult Allocation { get; }

    public BootPageAllocatorOnePageCoalescingKind CoalescingKind { get; }

    public OS.Boot.PhysicalAddress PhysicalStart =>
        Allocation.PhysicalStart;

    public OS.Boot.PhysicalAddress PhysicalEndExclusive =>
        Allocation.PhysicalEndExclusive;

    public ulong PageCount => Allocation.PageCount;

    public ulong ByteLength => Allocation.ByteLength;

    public ulong NodeCount { get; }

    public ulong NodeCapacity { get; }

    public ulong ReservationCount { get; }

    public ulong ReservationCapacity { get; }

    public int RestoredNodeIndex { get; }

    public int PreviousNodeIndex { get; }

    public int RemovedSuffixNodeIndex { get; }

    public ulong VisitedNodeCount { get; }

    public ulong InvalidReservationIndex { get; }

    public ulong ConflictingIndex { get; }

    public int BeforeFreeNodeCount => BeforeState.FreeNodeCount;

    public int AfterFreeNodeCount => AfterState.FreeNodeCount;

    public ulong BeforeFreePageCount => BeforeState.FreePageCount;

    public ulong AfterFreePageCount => AfterState.FreePageCount;

    public ulong BeforeFreeByteCount => BeforeState.FreeByteCount;

    public ulong AfterFreeByteCount => AfterState.FreeByteCount;

    public ulong BeforeAllocatedPageCount =>
        BeforeState.AllocatedPageCount;

    public ulong AfterAllocatedPageCount =>
        AfterState.AllocatedPageCount;

    public ulong BeforeAllocatedByteCount =>
        BeforeState.AllocatedByteCount;

    public ulong AfterAllocatedByteCount =>
        AfterState.AllocatedByteCount;

    internal static BootPageAllocatorOnePageFreeResult Success(
        BootPageAllocatorState beforeState,
        BootPageAllocatorState afterState,
        BootPageAllocatorOnePageResult allocation,
        BootPageAllocatorOnePageCoalescingKind coalescingKind,
        ulong nodeCount,
        ulong nodeCapacity,
        ulong reservationCount,
        ulong reservationCapacity,
        int restoredNodeIndex,
        int previousNodeIndex,
        int removedSuffixNodeIndex,
        ulong visitedNodeCount)
    {
        return new BootPageAllocatorOnePageFreeResult(
            BootPageAllocatorMutationStatus.Success,
            BootReservedRangeStatus.Success,
            OS.Boot.AddressValueStatus.Success,
            beforeState,
            afterState,
            allocation,
            coalescingKind,
            nodeCount,
            nodeCapacity,
            reservationCount,
            reservationCapacity,
            restoredNodeIndex,
            previousNodeIndex,
            removedSuffixNodeIndex,
            visitedNodeCount,
            BootPageAllocatorMutator.NoIndex,
            BootPageAllocatorMutator.NoIndex);
    }

    internal static BootPageAllocatorOnePageFreeResult Fail(
        BootPageAllocatorMutationStatus status,
        BootReservedRangeStatus reservationStatus,
        OS.Boot.AddressValueStatus addressStatus,
        BootPageAllocatorState state,
        BootPageAllocatorOnePageResult allocation,
        ulong nodeCount,
        ulong nodeCapacity,
        ulong reservationCount,
        ulong reservationCapacity,
        int restoredNodeIndex,
        int previousNodeIndex,
        int removedSuffixNodeIndex,
        ulong visitedNodeCount,
        ulong invalidReservationIndex,
        ulong conflictingIndex)
    {
        return new BootPageAllocatorOnePageFreeResult(
            status,
            reservationStatus,
            addressStatus,
            state,
            state,
            allocation,
            BootPageAllocatorOnePageCoalescingKind.Uninitialized,
            nodeCount,
            nodeCapacity,
            reservationCount,
            reservationCapacity,
            restoredNodeIndex,
            previousNodeIndex,
            removedSuffixNodeIndex,
            visitedNodeCount,
            invalidReservationIndex,
            conflictingIndex);
    }
}

#if !NOSTDLIB_EFI
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
#endif

public static unsafe class BootPageAllocatorMutator
{
    public const bool PerformsLiveAllocation = false;
    public const bool PerformsLiveFree = false;
    public const bool MutatesCallerOwnedFreeList = true;
    public const bool ConfiguresPaging = false;
    public const bool EnforcesNxOrWx = false;
    public const ulong RequiredPageSize = 4096;
    public const ulong RequiredPageCount = 1;
    public const ulong RequiredPointerAlignment = 8;
    public const ulong MaximumNodeCount = 4096;
    public const ulong MaximumReservationCount = 256;
    public const ulong NoIndex = ~0UL;

    private const ulong MaximumUnsignedLong = ~0UL;
    private const ulong ExpectedBootReservedRangeStructureSize = 40;

    internal static ulong ActualNodeStructureSize =>
        (ulong)sizeof(BootPageFreeListNode);

    internal static ulong ActualStateStructureSize =>
        (ulong)sizeof(BootPageAllocatorState);

    internal static ulong ActualReservationStructureSize =>
        (ulong)sizeof(BootReservedRange);

    public static BootPageAllocatorOnePageResult AllocateOnePage(
        BootPageAllocatorState state,
        BootPageFreeListNode* nodes,
        ulong nodeCount,
        ulong nodeCapacity,
        BootReservedRange* reservations,
        ulong reservationCount,
        ulong reservationCapacity,
        ulong pageCount,
        ulong alignment)
    {
        ulong byteLength =
            pageCount <= MaximumUnsignedLong / RequiredPageSize
                ? pageCount * RequiredPageSize
                : 0;

        if (BootPageFreeListNode.StructureSize != ActualNodeStructureSize
            || BootPageAllocatorState.StructureSize != ActualStateStructureSize
            || ExpectedBootReservedRangeStructureSize
                != ActualReservationStructureSize)
        {
            return FailOnePage(
                BootPageAllocatorMutationStatus.ContractLayoutMismatch,
                state,
                pageCount,
                byteLength,
                nodeCount,
                nodeCapacity,
                reservationCount,
                reservationCapacity);
        }

        if (!state.IsInitialized)
        {
            return FailOnePage(
                BootPageAllocatorMutationStatus.StateNotInitialized,
                state,
                pageCount,
                byteLength,
                nodeCount,
                nodeCapacity,
                reservationCount,
                reservationCapacity);
        }

        if (state.PageSize != RequiredPageSize)
        {
            return FailOnePage(
                BootPageAllocatorMutationStatus.InvalidPageSize,
                state,
                pageCount,
                byteLength,
                nodeCount,
                nodeCapacity,
                reservationCount,
                reservationCapacity);
        }

        if (pageCount != RequiredPageCount)
        {
            return FailOnePage(
                BootPageAllocatorMutationStatus.InvalidPageCount,
                state,
                pageCount,
                byteLength,
                nodeCount,
                nodeCapacity,
                reservationCount,
                reservationCapacity);
        }

        if (!IsValidOnePageAlignment(alignment))
        {
            return FailOnePage(
                BootPageAllocatorMutationStatus.InvalidAlignment,
                state,
                pageCount,
                byteLength,
                nodeCount,
                nodeCapacity,
                reservationCount,
                reservationCapacity);
        }

        BootPageAllocatorMutationStatus storageStatus =
            ValidateOnePageNodeStorage(
                nodes,
                nodeCount,
                nodeCapacity,
                out ulong nodeTableStart,
                out ulong nodeTableEndExclusive);
        if (storageStatus != BootPageAllocatorMutationStatus.Success)
        {
            return FailOnePage(
                storageStatus,
                state,
                pageCount,
                byteLength,
                nodeCount,
                nodeCapacity,
                reservationCount,
                reservationCapacity);
        }

        storageStatus = ValidateOnePageReservationStorage(
            reservations,
            reservationCount,
            reservationCapacity,
            out ulong reservationTableStart,
            out ulong reservationTableEndExclusive);
        if (storageStatus != BootPageAllocatorMutationStatus.Success)
        {
            return FailOnePage(
                storageStatus,
                state,
                pageCount,
                byteLength,
                nodeCount,
                nodeCapacity,
                reservationCount,
                reservationCapacity);
        }

        if (OnePageStorageRangesOverlap(
                nodeTableStart,
                nodeTableEndExclusive,
                reservationTableStart,
                reservationTableEndExclusive))
        {
            return FailOnePage(
                BootPageAllocatorMutationStatus
                    .NodeReservationTablesOverlap,
                state,
                pageCount,
                byteLength,
                nodeCount,
                nodeCapacity,
                reservationCount,
                reservationCapacity);
        }

        BootPageAllocatorMutationStatus stateStatus =
            ValidateOnePageState(state, nodeCount);
        if (stateStatus != BootPageAllocatorMutationStatus.Success)
        {
            return FailOnePage(
                stateStatus,
                state,
                pageCount,
                byteLength,
                nodeCount,
                nodeCapacity,
                reservationCount,
                reservationCapacity);
        }

        BootPageAllocatorMutationStatus reservationStatus =
            ValidateOnePageReservations(
                reservations,
                reservationCount,
                out BootReservedRangeStatus invalidReservationStatus,
                out ulong invalidReservationIndex,
                out ulong conflictingReservationIndex);
        if (reservationStatus != BootPageAllocatorMutationStatus.Success)
        {
            return FailOnePage(
                reservationStatus,
                state,
                pageCount,
                byteLength,
                nodeCount,
                nodeCapacity,
                reservationCount,
                reservationCapacity,
                reservationStatus: invalidReservationStatus,
                invalidReservationIndex: invalidReservationIndex,
                conflictingIndex: conflictingReservationIndex);
        }

        BootPageAllocatorMutationStatus freeListStatus =
            ValidateOnePageFreeList(
                state,
                nodes,
                nodeCount,
                out ulong visitedNodeCount,
                out int invalidNodeIndex,
                out int conflictingNodeIndex);
        if (freeListStatus != BootPageAllocatorMutationStatus.Success)
        {
            return FailOnePage(
                freeListStatus,
                state,
                pageCount,
                byteLength,
                nodeCount,
                nodeCapacity,
                reservationCount,
                reservationCapacity,
                nodeIndex: invalidNodeIndex,
                visitedNodeCount: visitedNodeCount,
                conflictingIndex: conflictingNodeIndex < 0
                    ? NoIndex
                    : (ulong)conflictingNodeIndex);
        }

        BootPageAllocatorMutationStatus exclusionStatus =
            ValidateOnePageReservationExclusion(
                state,
                nodes,
                reservations,
                reservationCount,
                out invalidReservationIndex,
                out conflictingNodeIndex);
        if (exclusionStatus != BootPageAllocatorMutationStatus.Success)
        {
            return FailOnePage(
                exclusionStatus,
                state,
                pageCount,
                byteLength,
                nodeCount,
                nodeCapacity,
                reservationCount,
                reservationCapacity,
                reservationStatus: BootReservedRangeStatus.Success,
                visitedNodeCount: visitedNodeCount,
                invalidReservationIndex: invalidReservationIndex,
                conflictingIndex: conflictingNodeIndex < 0
                    ? NoIndex
                    : (ulong)conflictingNodeIndex);
        }

        if (state.AllocatedPageCount == MaximumUnsignedLong)
        {
            return FailOnePage(
                BootPageAllocatorMutationStatus
                    .AllocatedPageCountOverflow,
                state,
                pageCount,
                byteLength,
                nodeCount,
                nodeCapacity,
                reservationCount,
                reservationCapacity,
                visitedNodeCount: visitedNodeCount);
        }

        if (state.AllocatedByteCount
            > MaximumUnsignedLong - RequiredPageSize)
        {
            return FailOnePage(
                BootPageAllocatorMutationStatus
                    .AllocatedByteCountOverflow,
                state,
                pageCount,
                byteLength,
                nodeCount,
                nodeCapacity,
                reservationCount,
                reservationCapacity,
                visitedNodeCount: visitedNodeCount);
        }

        int previousNodeIndex = -1;
        int currentNodeIndex = state.HeadNodeIndex;
        while (currentNodeIndex != -1)
        {
            BootPageFreeListNode node = nodes[currentNodeIndex];
            if (!TryAlignOnePageUp(
                    node.PhysicalStart,
                    alignment,
                    out ulong allocationStart))
            {
                return FailOnePage(
                    BootPageAllocatorMutationStatus
                        .AllocationAddressOverflow,
                    state,
                    pageCount,
                    byteLength,
                    nodeCount,
                    nodeCapacity,
                    reservationCount,
                    reservationCapacity,
                    nodeIndex: currentNodeIndex,
                    visitedNodeCount: visitedNodeCount,
                    addressStatus:
                        OS.Boot.AddressValueStatus.AddressOverflow);
            }

            if (allocationStart >= node.PhysicalStart
                && allocationStart <= node.PhysicalEndExclusive
                && allocationStart
                    <= MaximumUnsignedLong - RequiredPageSize)
            {
                ulong allocationEnd =
                    allocationStart + RequiredPageSize;
                if (allocationEnd <= node.PhysicalEndExclusive)
                {
                    bool hasPrefix =
                        allocationStart > node.PhysicalStart;
                    bool hasSuffix =
                        allocationEnd < node.PhysicalEndExclusive;
                    int insertedNodeIndex = -1;

                    if (hasPrefix && hasSuffix)
                    {
                        insertedNodeIndex = FindOnePageUnusedNode(
                            nodes,
                            nodeCount,
                            currentNodeIndex);
                        if (insertedNodeIndex < 0)
                        {
                            return FailOnePage(
                                BootPageAllocatorMutationStatus
                                    .MetadataCapacityTooSmall,
                                state,
                                pageCount,
                                byteLength,
                                nodeCount,
                                nodeCapacity,
                                reservationCount,
                                reservationCapacity,
                                nodeIndex: currentNodeIndex,
                                visitedNodeCount: visitedNodeCount);
                        }
                    }

                    OS.Boot.AddressValueStatus addressStatus =
                        OS.Boot.PhysicalAddress
                            .TryCreateAlignedFromUefiBoundary(
                                allocationStart,
                                RequiredPageSize,
                                out OS.Boot.PhysicalAddress
                                    physicalStart);
                    if (addressStatus
                        != OS.Boot.AddressValueStatus.Success)
                    {
                        return FailOnePage(
                            BootPageAllocatorMutationStatus
                                .AllocationAddressConstructionFailed,
                            state,
                            pageCount,
                            byteLength,
                            nodeCount,
                            nodeCapacity,
                            reservationCount,
                            reservationCapacity,
                            nodeIndex: currentNodeIndex,
                            insertedNodeIndex: insertedNodeIndex,
                            visitedNodeCount: visitedNodeCount,
                            addressStatus: addressStatus);
                    }

                    addressStatus =
                        physicalStart.TryGetEndExclusive(
                            RequiredPageSize,
                            out OS.Boot.PhysicalAddress
                                physicalEndExclusive);
                    if (addressStatus
                        != OS.Boot.AddressValueStatus.Success
                        || physicalEndExclusive
                            .TryGetRawValueForUefiBoundary(
                                out ulong typedEndExclusive)
                            != OS.Boot.AddressValueStatus.Success
                        || typedEndExclusive != allocationEnd)
                    {
                        return FailOnePage(
                            BootPageAllocatorMutationStatus
                                .AllocationAddressConstructionFailed,
                            state,
                            pageCount,
                            byteLength,
                            nodeCount,
                            nodeCapacity,
                            reservationCount,
                            reservationCapacity,
                            nodeIndex: currentNodeIndex,
                            insertedNodeIndex: insertedNodeIndex,
                            visitedNodeCount: visitedNodeCount,
                            addressStatus: addressStatus);
                    }

                    BootPageFreeListNode previousNodeBefore =
                        previousNodeIndex >= 0
                            ? nodes[previousNodeIndex]
                            : default;
                    BootPageAllocatorState afterState =
                        ApplyOnePageAllocation(
                            state,
                            nodes,
                            previousNodeIndex,
                            currentNodeIndex,
                            insertedNodeIndex,
                            node,
                            allocationStart,
                            allocationEnd,
                            hasPrefix,
                            hasSuffix);

                    return BootPageAllocatorOnePageResult
                        .SuccessWithRestoreReceipt(
                        state,
                        afterState,
                        physicalStart,
                        physicalEndExclusive,
                        nodeCount,
                        nodeCapacity,
                        reservationCount,
                        reservationCapacity,
                        currentNodeIndex,
                        insertedNodeIndex,
                        visitedNodeCount,
                        node,
                        previousNodeIndex,
                        previousNodeBefore);
                }
            }

            previousNodeIndex = currentNodeIndex;
            currentNodeIndex = node.NextNodeIndex;
        }

        return FailOnePage(
            BootPageAllocatorMutationStatus.NoSuitableRange,
            state,
            pageCount,
            byteLength,
            nodeCount,
            nodeCapacity,
            reservationCount,
            reservationCapacity,
            visitedNodeCount: visitedNodeCount);
    }

    public static BootPageAllocatorOnePageFreeResult FreeOnePage(
        BootPageAllocatorState state,
        BootPageFreeListNode* nodes,
        ulong nodeCount,
        ulong nodeCapacity,
        BootReservedRange* reservations,
        ulong reservationCount,
        ulong reservationCapacity,
        BootPageAllocatorOnePageResult allocation)
    {
        if (BootPageFreeListNode.StructureSize != ActualNodeStructureSize
            || BootPageAllocatorState.StructureSize != ActualStateStructureSize
            || ExpectedBootReservedRangeStructureSize
                != ActualReservationStructureSize)
        {
            return FailFreeOnePage(
                BootPageAllocatorMutationStatus.ContractLayoutMismatch,
                state,
                allocation,
                nodeCount,
                nodeCapacity,
                reservationCount,
                reservationCapacity);
        }

        if (!state.IsInitialized)
        {
            return FailFreeOnePage(
                BootPageAllocatorMutationStatus.StateNotInitialized,
                state,
                allocation,
                nodeCount,
                nodeCapacity,
                reservationCount,
                reservationCapacity);
        }

        if (state.PageSize != RequiredPageSize)
        {
            return FailFreeOnePage(
                BootPageAllocatorMutationStatus.InvalidPageSize,
                state,
                allocation,
                nodeCount,
                nodeCapacity,
                reservationCount,
                reservationCapacity);
        }

        BootPageAllocatorMutationStatus storageStatus =
            ValidateOnePageNodeStorage(
                nodes,
                nodeCount,
                nodeCapacity,
                out ulong nodeTableStart,
                out ulong nodeTableEndExclusive);
        if (storageStatus != BootPageAllocatorMutationStatus.Success)
        {
            return FailFreeOnePage(
                storageStatus,
                state,
                allocation,
                nodeCount,
                nodeCapacity,
                reservationCount,
                reservationCapacity);
        }

        storageStatus = ValidateOnePageReservationStorage(
            reservations,
            reservationCount,
            reservationCapacity,
            out ulong reservationTableStart,
            out ulong reservationTableEndExclusive);
        if (storageStatus != BootPageAllocatorMutationStatus.Success)
        {
            return FailFreeOnePage(
                storageStatus,
                state,
                allocation,
                nodeCount,
                nodeCapacity,
                reservationCount,
                reservationCapacity);
        }

        if (OnePageStorageRangesOverlap(
                nodeTableStart,
                nodeTableEndExclusive,
                reservationTableStart,
                reservationTableEndExclusive))
        {
            return FailFreeOnePage(
                BootPageAllocatorMutationStatus
                    .NodeReservationTablesOverlap,
                state,
                allocation,
                nodeCount,
                nodeCapacity,
                reservationCount,
                reservationCapacity);
        }

        BootPageAllocatorMutationStatus stateStatus =
            ValidateOnePageState(state, nodeCount);
        if (stateStatus != BootPageAllocatorMutationStatus.Success)
        {
            return FailFreeOnePage(
                stateStatus,
                state,
                allocation,
                nodeCount,
                nodeCapacity,
                reservationCount,
                reservationCapacity);
        }

        BootPageAllocatorMutationStatus reservationStatus =
            ValidateOnePageReservations(
                reservations,
                reservationCount,
                out BootReservedRangeStatus invalidReservationStatus,
                out ulong invalidReservationIndex,
                out ulong conflictingReservationIndex);
        if (reservationStatus != BootPageAllocatorMutationStatus.Success)
        {
            return FailFreeOnePage(
                reservationStatus,
                state,
                allocation,
                nodeCount,
                nodeCapacity,
                reservationCount,
                reservationCapacity,
                reservationStatus: invalidReservationStatus,
                invalidReservationIndex: invalidReservationIndex,
                conflictingIndex: conflictingReservationIndex);
        }

        BootPageAllocatorMutationStatus freeListStatus =
            ValidateOnePageFreeList(
                state,
                nodes,
                nodeCount,
                out ulong visitedNodeCount,
                out int invalidNodeIndex,
                out int conflictingNodeIndex);
        if (freeListStatus != BootPageAllocatorMutationStatus.Success)
        {
            return FailFreeOnePage(
                freeListStatus,
                state,
                allocation,
                nodeCount,
                nodeCapacity,
                reservationCount,
                reservationCapacity,
                restoredNodeIndex: invalidNodeIndex,
                visitedNodeCount: visitedNodeCount,
                conflictingIndex: conflictingNodeIndex < 0
                    ? NoIndex
                    : (ulong)conflictingNodeIndex);
        }

        BootPageAllocatorMutationStatus exclusionStatus =
            ValidateOnePageReservationExclusion(
                state,
                nodes,
                reservations,
                reservationCount,
                out invalidReservationIndex,
                out conflictingNodeIndex);
        if (exclusionStatus != BootPageAllocatorMutationStatus.Success)
        {
            return FailFreeOnePage(
                exclusionStatus,
                state,
                allocation,
                nodeCount,
                nodeCapacity,
                reservationCount,
                reservationCapacity,
                reservationStatus: BootReservedRangeStatus.Success,
                visitedNodeCount: visitedNodeCount,
                invalidReservationIndex: invalidReservationIndex,
                conflictingIndex: conflictingNodeIndex < 0
                    ? NoIndex
                    : (ulong)conflictingNodeIndex);
        }

        if (!allocation.CanRestore
            || !IsValidOnePageRestoreReceipt(allocation)
            || allocation.NodeCount != nodeCount
            || allocation.NodeCapacity != nodeCapacity
            || allocation.ReservationCount != reservationCount
            || allocation.ReservationCapacity != reservationCapacity
            || !OnePageStatesEqual(state, allocation.AfterState)
            || state.AllocatedPageCount < RequiredPageCount
            || state.AllocatedByteCount < RequiredPageSize)
        {
            return FailFreeOnePage(
                BootPageAllocatorMutationStatus.FreeRangeNotAllocated,
                state,
                allocation,
                nodeCount,
                nodeCapacity,
                reservationCount,
                reservationCapacity,
                reservationStatus: allocation.ReservationStatus,
                addressStatus: allocation.AddressStatus,
                restoredNodeIndex: allocation.NodeIndex,
                previousNodeIndex:
                    allocation.RestoreReceipt.PreviousNodeIndexBefore,
                removedSuffixNodeIndex: allocation.InsertedNodeIndex,
                visitedNodeCount: visitedNodeCount);
        }

        exclusionStatus =
            ValidateOnePageCandidateReservationExclusion(
                allocation,
                reservations,
                reservationCount,
                out invalidReservationIndex);
        if (exclusionStatus != BootPageAllocatorMutationStatus.Success)
        {
            return FailFreeOnePage(
                exclusionStatus,
                state,
                allocation,
                nodeCount,
                nodeCapacity,
                reservationCount,
                reservationCapacity,
                reservationStatus: BootReservedRangeStatus.Success,
                restoredNodeIndex: allocation.NodeIndex,
                previousNodeIndex:
                    allocation.RestoreReceipt.PreviousNodeIndexBefore,
                removedSuffixNodeIndex: allocation.InsertedNodeIndex,
                visitedNodeCount: visitedNodeCount,
                invalidReservationIndex: invalidReservationIndex);
        }

        BootPageAllocatorMutationStatus overlapStatus =
            ValidateOnePageCandidateFreeListExclusion(
                state,
                nodes,
                allocation,
                out conflictingNodeIndex);
        if (overlapStatus != BootPageAllocatorMutationStatus.Success)
        {
            return FailFreeOnePage(
                overlapStatus,
                state,
                allocation,
                nodeCount,
                nodeCapacity,
                reservationCount,
                reservationCapacity,
                restoredNodeIndex: allocation.NodeIndex,
                previousNodeIndex:
                    allocation.RestoreReceipt.PreviousNodeIndexBefore,
                removedSuffixNodeIndex: allocation.InsertedNodeIndex,
                visitedNodeCount: visitedNodeCount,
                conflictingIndex: conflictingNodeIndex < 0
                    ? NoIndex
                    : (ulong)conflictingNodeIndex);
        }

        BootPageAllocatorMutationStatus topologyStatus =
            ValidateOnePagePostAllocationTopology(
                state,
                nodes,
                nodeCount,
                allocation,
                out BootPageAllocatorOnePageCoalescingKind
                    coalescingKind,
                out int removedSuffixNodeIndex);
        if (topologyStatus != BootPageAllocatorMutationStatus.Success)
        {
            return FailFreeOnePage(
                topologyStatus,
                state,
                allocation,
                nodeCount,
                nodeCapacity,
                reservationCount,
                reservationCapacity,
                restoredNodeIndex: allocation.NodeIndex,
                previousNodeIndex:
                    allocation.RestoreReceipt.PreviousNodeIndexBefore,
                removedSuffixNodeIndex: removedSuffixNodeIndex,
                visitedNodeCount: visitedNodeCount);
        }

        BootPageAllocatorOnePageFreeResult result =
            BootPageAllocatorOnePageFreeResult.Success(
                state,
                allocation.BeforeState,
                allocation,
                coalescingKind,
                nodeCount,
                nodeCapacity,
                reservationCount,
                reservationCapacity,
                allocation.NodeIndex,
                allocation.RestoreReceipt.PreviousNodeIndexBefore,
                removedSuffixNodeIndex,
                visitedNodeCount);
        if (!result.Succeeded)
        {
            return FailFreeOnePage(
                BootPageAllocatorMutationStatus.FreeRangeNotAllocated,
                state,
                allocation,
                nodeCount,
                nodeCapacity,
                reservationCount,
                reservationCapacity,
                restoredNodeIndex: allocation.NodeIndex,
                previousNodeIndex:
                    allocation.RestoreReceipt.PreviousNodeIndexBefore,
                removedSuffixNodeIndex: removedSuffixNodeIndex,
                visitedNodeCount: visitedNodeCount);
        }

        ApplyOnePageFree(nodes, allocation, coalescingKind);
        return result;
    }

    internal static bool IsRepresentableSuccessSummary(
        BootPageAllocatorState beforeState,
        BootPageAllocatorState afterState,
        OS.Boot.PhysicalAddress physicalStart,
        OS.Boot.PhysicalAddress physicalEndExclusive,
        ulong pageCount,
        ulong byteLength,
        ulong nodeCount,
        ulong nodeCapacity,
        ulong reservationCount,
        ulong reservationCapacity,
        int nodeIndex,
        int insertedNodeIndex,
        ulong visitedNodeCount)
    {
        if (BootPageFreeListNode.StructureSize != ActualNodeStructureSize
            || BootPageAllocatorState.StructureSize != ActualStateStructureSize
            || ExpectedBootReservedRangeStructureSize
                != ActualReservationStructureSize
            || pageCount != RequiredPageCount
            || byteLength != RequiredPageSize
            || nodeCount == 0
            || nodeCount > MaximumNodeCount
            || nodeCapacity < nodeCount
            || nodeCapacity > MaximumNodeCount
            || reservationCount > MaximumReservationCount
            || reservationCapacity < reservationCount
            || reservationCapacity > MaximumReservationCount
            || nodeIndex < 0
            || (ulong)nodeIndex >= nodeCount
            || (beforeState.FreeNodeCount == 1
                && nodeIndex != beforeState.HeadNodeIndex)
            || (insertedNodeIndex != -1
                && (insertedNodeIndex < 0
                    || (ulong)insertedNodeIndex >= nodeCount
                    || insertedNodeIndex == nodeIndex
                    || insertedNodeIndex
                        == beforeState.HeadNodeIndex))
            || beforeState.FreeNodeCount <= 0
            || visitedNodeCount != (ulong)beforeState.FreeNodeCount
            || !IsValidOnePageStateSummary(beforeState, nodeCount)
            || !IsValidOnePageStateSummary(afterState, nodeCount)
            || beforeState.FreePageCount == 0
            || beforeState.FreeByteCount < RequiredPageSize
            || beforeState.AllocatedPageCount
                > MaximumUnsignedLong - RequiredPageCount
            || beforeState.AllocatedByteCount
                > MaximumUnsignedLong - RequiredPageSize
            || afterState.FreePageCount
                != beforeState.FreePageCount - RequiredPageCount
            || afterState.FreeByteCount
                != beforeState.FreeByteCount - RequiredPageSize
            || afterState.AllocatedPageCount
                != beforeState.AllocatedPageCount + RequiredPageCount
            || afterState.AllocatedByteCount
                != beforeState.AllocatedByteCount + RequiredPageSize)
        {
            return false;
        }

        if (insertedNodeIndex >= 0)
        {
            if (beforeState.FreeNodeCount >= (int)MaximumNodeCount
                || afterState.FreeNodeCount
                    != beforeState.FreeNodeCount + 1)
            {
                return false;
            }
        }
        else if (afterState.FreeNodeCount
                != beforeState.FreeNodeCount
            && afterState.FreeNodeCount
                != beforeState.FreeNodeCount - 1)
        {
            return false;
        }

        bool removedNode = afterState.FreeNodeCount
            == beforeState.FreeNodeCount - 1;
        if (!removedNode)
        {
            if (afterState.HeadNodeIndex
                != beforeState.HeadNodeIndex)
            {
                return false;
            }
        }
        else if (nodeIndex == beforeState.HeadNodeIndex)
        {
            if (afterState.HeadNodeIndex
                == beforeState.HeadNodeIndex)
            {
                return false;
            }
        }
        else if (afterState.HeadNodeIndex
            != beforeState.HeadNodeIndex)
        {
            return false;
        }

        if (physicalStart.TryGetRawValueForUefiBoundary(
                out ulong rawStart)
                != OS.Boot.AddressValueStatus.Success
            || physicalEndExclusive.TryGetRawValueForUefiBoundary(
                out ulong rawEndExclusive)
                != OS.Boot.AddressValueStatus.Success
            || !physicalStart.IsAligned(RequiredPageSize)
            || !physicalEndExclusive.IsAligned(RequiredPageSize)
            || rawStart > MaximumUnsignedLong - RequiredPageSize
            || rawEndExclusive != rawStart + RequiredPageSize)
        {
            return false;
        }

        if (!TryAddOnePage(
                beforeState.FreePageCount,
                beforeState.AllocatedPageCount,
                out ulong beforeTotalPages)
            || !TryAddOnePage(
                afterState.FreePageCount,
                afterState.AllocatedPageCount,
                out ulong afterTotalPages)
            || !TryAddOnePage(
                beforeState.FreeByteCount,
                beforeState.AllocatedByteCount,
                out ulong beforeTotalBytes)
            || !TryAddOnePage(
                afterState.FreeByteCount,
                afterState.AllocatedByteCount,
                out ulong afterTotalBytes))
        {
            return false;
        }

        return beforeTotalPages == afterTotalPages
            && beforeTotalBytes == afterTotalBytes;
    }

    internal static bool IsRepresentableFreeSuccessSummary(
        BootPageAllocatorState beforeState,
        BootPageAllocatorState afterState,
        BootPageAllocatorOnePageResult allocation,
        BootPageAllocatorOnePageCoalescingKind coalescingKind,
        ulong nodeCount,
        ulong nodeCapacity,
        ulong reservationCount,
        ulong reservationCapacity,
        int restoredNodeIndex,
        int previousNodeIndex,
        int removedSuffixNodeIndex,
        ulong visitedNodeCount)
    {
        if (!allocation.CanRestore
            || !IsValidOnePageRestoreReceipt(allocation)
            || !OnePageStatesEqual(beforeState, allocation.AfterState)
            || !OnePageStatesEqual(afterState, allocation.BeforeState)
            || allocation.NodeCount != nodeCount
            || allocation.NodeCapacity != nodeCapacity
            || allocation.ReservationCount != reservationCount
            || allocation.ReservationCapacity != reservationCapacity
            || nodeCount == 0
            || nodeCount > MaximumNodeCount
            || nodeCapacity < nodeCount
            || nodeCapacity > MaximumNodeCount
            || reservationCount > MaximumReservationCount
            || reservationCapacity < reservationCount
            || reservationCapacity > MaximumReservationCount
            || restoredNodeIndex != allocation.NodeIndex
            || previousNodeIndex
                != allocation.RestoreReceipt.PreviousNodeIndexBefore
            || visitedNodeCount != (ulong)beforeState.FreeNodeCount
            || beforeState.AllocatedPageCount < RequiredPageCount
            || beforeState.AllocatedByteCount < RequiredPageSize
            || !TryGetOnePageCoalescingKind(
                allocation,
                out BootPageAllocatorOnePageCoalescingKind
                    expectedCoalescingKind)
            || coalescingKind != expectedCoalescingKind)
        {
            return false;
        }

        int expectedRemovedSuffixNodeIndex =
            coalescingKind
                == BootPageAllocatorOnePageCoalescingKind
                    .PreviousAndNext
                ? allocation.InsertedNodeIndex
                : -1;
        if (removedSuffixNodeIndex != expectedRemovedSuffixNodeIndex)
        {
            return false;
        }

        int expectedAfterFreeNodeCount =
            coalescingKind switch
            {
                BootPageAllocatorOnePageCoalescingKind.None =>
                    beforeState.FreeNodeCount + 1,
                BootPageAllocatorOnePageCoalescingKind.PreviousAndNext =>
                    beforeState.FreeNodeCount - 1,
                _ => beforeState.FreeNodeCount
            };
        if (afterState.FreeNodeCount != expectedAfterFreeNodeCount)
        {
            return false;
        }

        if (!TryAddOnePage(
                beforeState.FreePageCount,
                RequiredPageCount,
                out ulong expectedFreePages)
            || !TryAddOnePage(
                beforeState.FreeByteCount,
                RequiredPageSize,
                out ulong expectedFreeBytes)
            || afterState.FreePageCount != expectedFreePages
            || afterState.FreeByteCount != expectedFreeBytes
            || afterState.AllocatedPageCount
                != beforeState.AllocatedPageCount - RequiredPageCount
            || afterState.AllocatedByteCount
                != beforeState.AllocatedByteCount - RequiredPageSize)
        {
            return false;
        }

        if (!TryAddOnePage(
                beforeState.FreePageCount,
                beforeState.AllocatedPageCount,
                out ulong beforeTotalPages)
            || !TryAddOnePage(
                afterState.FreePageCount,
                afterState.AllocatedPageCount,
                out ulong afterTotalPages)
            || !TryAddOnePage(
                beforeState.FreeByteCount,
                beforeState.AllocatedByteCount,
                out ulong beforeTotalBytes)
            || !TryAddOnePage(
                afterState.FreeByteCount,
                afterState.AllocatedByteCount,
                out ulong afterTotalBytes))
        {
            return false;
        }

        return beforeTotalPages == afterTotalPages
            && beforeTotalBytes == afterTotalBytes;
    }

    private static BootPageAllocatorOnePageResult FailOnePage(
        BootPageAllocatorMutationStatus status,
        BootPageAllocatorState state,
        ulong pageCount,
        ulong byteLength,
        ulong nodeCount,
        ulong nodeCapacity,
        ulong reservationCount,
        ulong reservationCapacity,
        BootReservedRangeStatus reservationStatus =
            BootReservedRangeStatus.Success,
        OS.Boot.AddressValueStatus addressStatus =
            OS.Boot.AddressValueStatus.Success,
        int nodeIndex = -1,
        int insertedNodeIndex = -1,
        ulong visitedNodeCount = 0,
        ulong invalidReservationIndex = NoIndex,
        ulong conflictingIndex = NoIndex)
    {
        return BootPageAllocatorOnePageResult.Fail(
            status,
            reservationStatus,
            addressStatus,
            state,
            pageCount,
            byteLength,
            nodeCount,
            nodeCapacity,
            reservationCount,
            reservationCapacity,
            nodeIndex,
            insertedNodeIndex,
            visitedNodeCount,
            invalidReservationIndex,
            conflictingIndex);
    }

    private static BootPageAllocatorOnePageFreeResult FailFreeOnePage(
        BootPageAllocatorMutationStatus status,
        BootPageAllocatorState state,
        BootPageAllocatorOnePageResult allocation,
        ulong nodeCount,
        ulong nodeCapacity,
        ulong reservationCount,
        ulong reservationCapacity,
        BootReservedRangeStatus reservationStatus =
            BootReservedRangeStatus.Success,
        OS.Boot.AddressValueStatus addressStatus =
            OS.Boot.AddressValueStatus.Success,
        int restoredNodeIndex = -1,
        int previousNodeIndex = -1,
        int removedSuffixNodeIndex = -1,
        ulong visitedNodeCount = 0,
        ulong invalidReservationIndex = NoIndex,
        ulong conflictingIndex = NoIndex)
    {
        return BootPageAllocatorOnePageFreeResult.Fail(
            status,
            reservationStatus,
            addressStatus,
            state,
            allocation,
            nodeCount,
            nodeCapacity,
            reservationCount,
            reservationCapacity,
            restoredNodeIndex,
            previousNodeIndex,
            removedSuffixNodeIndex,
            visitedNodeCount,
            invalidReservationIndex,
            conflictingIndex);
    }

    private static bool IsValidOnePageAlignment(ulong alignment)
    {
        return alignment >= RequiredPageSize
            && (alignment & (alignment - 1)) == 0
            && (alignment & (RequiredPageSize - 1)) == 0;
    }

    private static BootPageAllocatorMutationStatus
        ValidateOnePageNodeStorage(
            BootPageFreeListNode* nodes,
            ulong nodeCount,
            ulong nodeCapacity,
            out ulong tableStart,
            out ulong tableEndExclusive)
    {
        tableStart = 0;
        tableEndExclusive = 0;
        ulong structureSize = ActualNodeStructureSize;

        if (nodeCount > MaximumUnsignedLong / structureSize
            || nodeCapacity > MaximumUnsignedLong / structureSize)
        {
            return BootPageAllocatorMutationStatus
                .NodeTableByteCountOverflow;
        }

        if (nodeCount > MaximumNodeCount
            || nodeCapacity > MaximumNodeCount)
        {
            return BootPageAllocatorMutationStatus.TooManyNodes;
        }

        if (nodeCapacity < nodeCount)
        {
            return BootPageAllocatorMutationStatus
                .NodeCapacityTooSmall;
        }

        if (nodeCapacity == 0)
        {
            return BootPageAllocatorMutationStatus.Success;
        }

        tableStart = (ulong)nodes;
        if (tableStart == 0)
        {
            return BootPageAllocatorMutationStatus.MissingNodes;
        }

        if ((tableStart & (RequiredPointerAlignment - 1)) != 0)
        {
            return BootPageAllocatorMutationStatus.MisalignedNodes;
        }

        ulong tableByteLength = nodeCapacity * structureSize;
        if (tableStart > MaximumUnsignedLong - tableByteLength)
        {
            return BootPageAllocatorMutationStatus
                .NodeTableAddressRangeOverflow;
        }

        tableEndExclusive = tableStart + tableByteLength;
        return BootPageAllocatorMutationStatus.Success;
    }

    private static BootPageAllocatorMutationStatus
        ValidateOnePageReservationStorage(
            BootReservedRange* reservations,
            ulong reservationCount,
            ulong reservationCapacity,
            out ulong tableStart,
            out ulong tableEndExclusive)
    {
        tableStart = 0;
        tableEndExclusive = 0;
        ulong structureSize = ActualReservationStructureSize;

        if (reservationCount > MaximumUnsignedLong / structureSize
            || reservationCapacity
                > MaximumUnsignedLong / structureSize)
        {
            return BootPageAllocatorMutationStatus
                .ReservationTableByteCountOverflow;
        }

        if (reservationCount > MaximumReservationCount
            || reservationCapacity > MaximumReservationCount)
        {
            return BootPageAllocatorMutationStatus
                .TooManyReservations;
        }

        if (reservationCapacity < reservationCount)
        {
            return BootPageAllocatorMutationStatus
                .ReservationCapacityTooSmall;
        }

        if (reservationCapacity == 0)
        {
            return BootPageAllocatorMutationStatus.Success;
        }

        tableStart = (ulong)reservations;
        if (tableStart == 0)
        {
            return BootPageAllocatorMutationStatus
                .MissingReservations;
        }

        if ((tableStart & (RequiredPointerAlignment - 1)) != 0)
        {
            return BootPageAllocatorMutationStatus
                .MisalignedReservations;
        }

        ulong tableByteLength = reservationCapacity * structureSize;
        if (tableStart > MaximumUnsignedLong - tableByteLength)
        {
            return BootPageAllocatorMutationStatus
                .ReservationTableAddressRangeOverflow;
        }

        tableEndExclusive = tableStart + tableByteLength;
        return BootPageAllocatorMutationStatus.Success;
    }

    private static bool OnePageStorageRangesOverlap(
        ulong leftStart,
        ulong leftEndExclusive,
        ulong rightStart,
        ulong rightEndExclusive)
    {
        return leftStart != leftEndExclusive
            && rightStart != rightEndExclusive
            && leftStart < rightEndExclusive
            && rightStart < leftEndExclusive;
    }

    private static BootPageAllocatorMutationStatus
        ValidateOnePageState(
            BootPageAllocatorState state,
            ulong nodeCount)
    {
        if (state.FreeNodeCount < 0)
        {
            return BootPageAllocatorMutationStatus
                .InvalidStateAccounting;
        }

        if ((ulong)state.FreeNodeCount > nodeCount)
        {
            return BootPageAllocatorMutationStatus
                .FreeListNodeCountMismatch;
        }

        if (state.FreePageCount < (ulong)state.FreeNodeCount)
        {
            return BootPageAllocatorMutationStatus
                .InvalidStateAccounting;
        }

        if (state.FreeNodeCount == 0)
        {
            if (state.HeadNodeIndex != -1)
            {
                return BootPageAllocatorMutationStatus
                    .InvalidHeadNodeIndex;
            }

            if (state.FreePageCount != 0
                || state.FreeByteCount != 0)
            {
                return BootPageAllocatorMutationStatus
                    .InvalidStateAccounting;
            }
        }
        else
        {
            if (state.HeadNodeIndex < 0
                || (ulong)state.HeadNodeIndex >= nodeCount)
            {
                return BootPageAllocatorMutationStatus
                    .InvalidHeadNodeIndex;
            }

            if (state.FreePageCount == 0
                || state.FreeByteCount == 0)
            {
                return BootPageAllocatorMutationStatus
                    .InvalidStateAccounting;
            }
        }

        return IsValidOnePageStateAccounting(state)
            ? BootPageAllocatorMutationStatus.Success
            : BootPageAllocatorMutationStatus.InvalidStateAccounting;
    }

    private static bool IsValidOnePageStateSummary(
        BootPageAllocatorState state,
        ulong nodeCount)
    {
        return state.IsInitialized
            && state.PageSize == RequiredPageSize
            && ValidateOnePageState(state, nodeCount)
                == BootPageAllocatorMutationStatus.Success;
    }

    private static bool IsValidOnePageStateAccounting(
        BootPageAllocatorState state)
    {
        if (state.FreePageCount
                > MaximumUnsignedLong / RequiredPageSize
            || state.AllocatedPageCount
                > MaximumUnsignedLong / RequiredPageSize
            || state.FreeByteCount
                != state.FreePageCount * RequiredPageSize
            || state.AllocatedByteCount
                != state.AllocatedPageCount * RequiredPageSize
            || !TryAddOnePage(
                state.FreePageCount,
                state.AllocatedPageCount,
                out ulong totalPages)
            || !TryAddOnePage(
                state.FreeByteCount,
                state.AllocatedByteCount,
                out ulong totalBytes)
            || totalPages > MaximumUnsignedLong / RequiredPageSize)
        {
            return false;
        }

        return totalBytes == totalPages * RequiredPageSize;
    }

    private static BootPageAllocatorMutationStatus
        ValidateOnePageReservations(
            BootReservedRange* reservations,
            ulong reservationCount,
            out BootReservedRangeStatus reservationStatus,
            out ulong invalidReservationIndex,
            out ulong conflictingReservationIndex)
    {
        reservationStatus = BootReservedRangeStatus.Success;
        invalidReservationIndex = NoIndex;
        conflictingReservationIndex = NoIndex;
        ulong previousStart = 0;
        ulong previousEndExclusive = 0;

        for (ulong index = 0; index < reservationCount; index++)
        {
            BootReservedRange reservation = reservations[index];
            reservationStatus = reservation.Validate();
            if (reservationStatus != BootReservedRangeStatus.Success)
            {
                invalidReservationIndex = index;
                return BootPageAllocatorMutationStatus
                    .InvalidReservation;
            }

            if (!TryGetOnePageReservationBounds(
                    reservation,
                    out ulong currentStart,
                    out ulong currentEndExclusive))
            {
                reservationStatus =
                    BootReservedRangeStatus.InvalidPhysicalStart;
                invalidReservationIndex = index;
                return BootPageAllocatorMutationStatus
                    .InvalidReservation;
            }

            if (index != 0)
            {
                if (currentStart < previousStart)
                {
                    invalidReservationIndex = index;
                    conflictingReservationIndex = index - 1;
                    return BootPageAllocatorMutationStatus
                        .UnsortedReservation;
                }

                if (currentStart < previousEndExclusive)
                {
                    invalidReservationIndex = index;
                    conflictingReservationIndex = index - 1;
                    return BootPageAllocatorMutationStatus
                        .OverlappingReservation;
                }
            }

            previousStart = currentStart;
            previousEndExclusive = currentEndExclusive;
        }

        return BootPageAllocatorMutationStatus.Success;
    }

    private static bool TryGetOnePageReservationBounds(
        BootReservedRange reservation,
        out ulong physicalStart,
        out ulong physicalEndExclusive)
    {
        physicalStart = 0;
        physicalEndExclusive = 0;

        return reservation.PhysicalStart
                .TryGetRawValueForUefiBoundary(out physicalStart)
                == OS.Boot.AddressValueStatus.Success
            && reservation.PhysicalEndExclusive
                .TryGetRawValueForUefiBoundary(
                    out physicalEndExclusive)
                == OS.Boot.AddressValueStatus.Success;
    }

    private static BootPageAllocatorMutationStatus
        ValidateOnePageFreeList(
            BootPageAllocatorState state,
            BootPageFreeListNode* nodes,
            ulong nodeCount,
            out ulong visitedNodeCount,
            out int invalidNodeIndex,
            out int conflictingNodeIndex)
    {
        visitedNodeCount = 0;
        invalidNodeIndex = -1;
        conflictingNodeIndex = -1;

        if (state.FreeNodeCount == 0)
        {
            return BootPageAllocatorMutationStatus.Success;
        }

        ulong totalPages = 0;
        ulong totalBytes = 0;
        ulong previousStart = 0;
        ulong previousEndExclusive = 0;
        int currentNodeIndex = state.HeadNodeIndex;

        while (visitedNodeCount < (ulong)state.FreeNodeCount)
        {
            if (currentNodeIndex < 0
                || (ulong)currentNodeIndex >= nodeCount)
            {
                invalidNodeIndex = currentNodeIndex;
                if (currentNodeIndex == -1
                    && visitedNodeCount != 0)
                {
                    return BootPageAllocatorMutationStatus
                        .FreeListNodeCountMismatch;
                }

                return visitedNodeCount == 0
                    ? BootPageAllocatorMutationStatus
                        .InvalidHeadNodeIndex
                    : BootPageAllocatorMutationStatus
                        .InvalidNextNodeIndex;
            }

            if (WasOnePageNodeVisited(
                    nodes,
                    state.HeadNodeIndex,
                    visitedNodeCount,
                    currentNodeIndex))
            {
                invalidNodeIndex = currentNodeIndex;
                conflictingNodeIndex = currentNodeIndex;
                return BootPageAllocatorMutationStatus
                    .FreeListCycleDetected;
            }

            BootPageFreeListNode node = nodes[currentNodeIndex];
            if (!ValidateOnePageNodeShape(node))
            {
                invalidNodeIndex = currentNodeIndex;
                return BootPageAllocatorMutationStatus
                    .InvalidNodeShape;
            }

            if (visitedNodeCount != 0)
            {
                if (node.PhysicalStart < previousStart)
                {
                    invalidNodeIndex = currentNodeIndex;
                    conflictingNodeIndex =
                        FindOnePagePreviousNodeIndex(
                            nodes,
                            state.HeadNodeIndex,
                            visitedNodeCount);
                    return BootPageAllocatorMutationStatus
                        .InvalidNodeShape;
                }

                if (node.PhysicalStart < previousEndExclusive)
                {
                    invalidNodeIndex = currentNodeIndex;
                    conflictingNodeIndex =
                        FindOnePagePreviousNodeIndex(
                            nodes,
                            state.HeadNodeIndex,
                            visitedNodeCount);
                    return BootPageAllocatorMutationStatus
                        .InvalidNodeShape;
                }
            }

            if (!TryAddOnePage(
                    totalPages,
                    node.PageCount,
                    out totalPages))
            {
                invalidNodeIndex = currentNodeIndex;
                return BootPageAllocatorMutationStatus
                    .TotalPageCountMismatch;
            }

            if (!TryAddOnePage(
                    totalBytes,
                    node.ByteLength,
                    out totalBytes))
            {
                invalidNodeIndex = currentNodeIndex;
                return BootPageAllocatorMutationStatus
                    .TotalByteCountMismatch;
            }

            previousStart = node.PhysicalStart;
            previousEndExclusive = node.PhysicalEndExclusive;
            visitedNodeCount++;

            int nextNodeIndex = node.NextNodeIndex;
            if (nextNodeIndex != -1
                && (nextNodeIndex < 0
                    || (ulong)nextNodeIndex >= nodeCount))
            {
                invalidNodeIndex = nextNodeIndex;
                conflictingNodeIndex = currentNodeIndex;
                return BootPageAllocatorMutationStatus
                    .InvalidNextNodeIndex;
            }

            currentNodeIndex = nextNodeIndex;
        }

        if (currentNodeIndex != -1)
        {
            invalidNodeIndex = currentNodeIndex;
            return WasOnePageNodeVisited(
                    nodes,
                    state.HeadNodeIndex,
                    visitedNodeCount,
                    currentNodeIndex)
                ? BootPageAllocatorMutationStatus
                    .FreeListCycleDetected
                : BootPageAllocatorMutationStatus
                    .FreeListNodeCountMismatch;
        }

        if (totalPages != state.FreePageCount)
        {
            return BootPageAllocatorMutationStatus
                .TotalPageCountMismatch;
        }

        return totalBytes == state.FreeByteCount
            ? BootPageAllocatorMutationStatus.Success
            : BootPageAllocatorMutationStatus
                .TotalByteCountMismatch;
    }

    private static bool ValidateOnePageNodeShape(
        BootPageFreeListNode node)
    {
        if (node.SourceFreeSegmentIndex < 0
            || node.SourceUsableRangeIndex < 0
            || node.PhysicalStart == 0
            || (node.PhysicalStart & (RequiredPageSize - 1)) != 0
            || node.PageCount == 0
            || node.PageCount
                > MaximumUnsignedLong / RequiredPageSize)
        {
            return false;
        }

        ulong expectedBytes = node.PageCount * RequiredPageSize;
        return node.ByteLength == expectedBytes
            && node.PhysicalStart
                <= MaximumUnsignedLong - expectedBytes
            && node.PhysicalEndExclusive
                == node.PhysicalStart + expectedBytes
            && node.PhysicalEndExclusive > node.PhysicalStart
            && (node.PhysicalEndExclusive
                & (RequiredPageSize - 1)) == 0;
    }

    private static bool WasOnePageNodeVisited(
        BootPageFreeListNode* nodes,
        int headNodeIndex,
        ulong visitedNodeCount,
        int candidateNodeIndex)
    {
        int currentNodeIndex = headNodeIndex;
        for (ulong index = 0;
            index < visitedNodeCount && currentNodeIndex != -1;
            index++)
        {
            if (currentNodeIndex == candidateNodeIndex)
            {
                return true;
            }

            currentNodeIndex = nodes[currentNodeIndex].NextNodeIndex;
        }

        return false;
    }

    private static int FindOnePagePreviousNodeIndex(
        BootPageFreeListNode* nodes,
        int headNodeIndex,
        ulong visitedNodeCount)
    {
        int currentNodeIndex = headNodeIndex;
        int previousNodeIndex = -1;
        for (ulong index = 0;
            index < visitedNodeCount && currentNodeIndex != -1;
            index++)
        {
            previousNodeIndex = currentNodeIndex;
            currentNodeIndex = nodes[currentNodeIndex].NextNodeIndex;
        }

        return previousNodeIndex;
    }

    private static BootPageAllocatorMutationStatus
        ValidateOnePageReservationExclusion(
            BootPageAllocatorState state,
            BootPageFreeListNode* nodes,
            BootReservedRange* reservations,
            ulong reservationCount,
            out ulong invalidReservationIndex,
            out int conflictingNodeIndex)
    {
        invalidReservationIndex = NoIndex;
        conflictingNodeIndex = -1;

        for (ulong reservationIndex = 0;
            reservationIndex < reservationCount;
            reservationIndex++)
        {
            BootReservedRange reservation =
                reservations[reservationIndex];
            TryGetOnePageReservationBounds(
                reservation,
                out ulong reservationStart,
                out ulong reservationEndExclusive);

            int nodeIndex = state.HeadNodeIndex;
            for (int visited = 0;
                visited < state.FreeNodeCount;
                visited++)
            {
                BootPageFreeListNode node = nodes[nodeIndex];
                if (reservationStart < node.PhysicalEndExclusive
                    && node.PhysicalStart < reservationEndExclusive)
                {
                    invalidReservationIndex = reservationIndex;
                    conflictingNodeIndex = nodeIndex;
                    return BootPageAllocatorMutationStatus
                        .ReservationOverlapsFreeRange;
                }

                nodeIndex = node.NextNodeIndex;
            }
        }

        return BootPageAllocatorMutationStatus.Success;
    }

    private static bool TryAlignOnePageUp(
        ulong value,
        ulong alignment,
        out ulong aligned)
    {
        aligned = 0;
        ulong mask = alignment - 1;
        ulong remainder = value & mask;
        if (remainder == 0)
        {
            aligned = value;
            return true;
        }

        ulong increment = alignment - remainder;
        if (value > MaximumUnsignedLong - increment)
        {
            return false;
        }

        aligned = value + increment;
        return true;
    }

    private static int FindOnePageUnusedNode(
        BootPageFreeListNode* nodes,
        ulong nodeCount,
        int currentNodeIndex)
    {
        // A merely unlinked slot may contain stale or caller-owned metadata.
        // Splitting may claim only an exact default slot so failure cannot
        // silently overwrite data outside the validated active chain.
        for (ulong index = 0; index < nodeCount; index++)
        {
            if ((int)index != currentNodeIndex
                && IsZeroedOnePageNodeSlot(nodes, index))
            {
                return (int)index;
            }
        }

        return -1;
    }

    private static bool IsZeroedOnePageNodeSlot(
        BootPageFreeListNode* nodes,
        ulong index)
    {
        byte* bytes = (byte*)&nodes[index];
        for (int byteIndex = 0;
            byteIndex < (int)BootPageFreeListNode.StructureSize;
            byteIndex++)
        {
            if (bytes[byteIndex] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsDefaultOnePageNode(
        BootPageFreeListNode node)
    {
        return node.SourceFreeSegmentIndex == 0
            && node.SourceUsableRangeIndex == 0
            && node.PhysicalStart == 0
            && node.PhysicalEndExclusive == 0
            && node.PageCount == 0
            && node.ByteLength == 0
            && node.NextNodeIndex == 0;
    }

    private static bool IsValidOnePageRestoreReceipt(
        BootPageAllocatorOnePageResult allocation)
    {
        if (!allocation.CanRestore
            || allocation.NodeIndex < 0
            || (ulong)allocation.NodeIndex >= allocation.NodeCount
            || !ValidateOnePageNodeShape(
                allocation.RestoreReceipt.SourceNodeBefore)
            || !TryGetOnePageCoalescingKind(
                allocation,
                out BootPageAllocatorOnePageCoalescingKind
                    coalescingKind))
        {
            return false;
        }

        BootPageAllocatorOnePageRestoreReceipt receipt =
            allocation.RestoreReceipt;
        BootPageFreeListNode sourceNode = receipt.SourceNodeBefore;
        int previousNodeIndex = receipt.PreviousNodeIndexBefore;

        if (sourceNode.NextNodeIndex != -1
            && (sourceNode.NextNodeIndex < 0
                || (ulong)sourceNode.NextNodeIndex
                    >= allocation.NodeCount
                || sourceNode.NextNodeIndex == allocation.NodeIndex))
        {
            return false;
        }

        if (previousNodeIndex < -1
            || (previousNodeIndex >= 0
                && ((ulong)previousNodeIndex >= allocation.NodeCount
                    || previousNodeIndex == allocation.NodeIndex
                    || previousNodeIndex
                        == allocation.InsertedNodeIndex)))
        {
            return false;
        }

        if (previousNodeIndex == -1)
        {
            if (!IsDefaultOnePageNode(receipt.PreviousNodeBefore)
                || allocation.BeforeState.HeadNodeIndex
                    != allocation.NodeIndex)
            {
                return false;
            }
        }
        else
        {
            BootPageFreeListNode previousNode =
                receipt.PreviousNodeBefore;
            if (!ValidateOnePageNodeShape(previousNode)
                || previousNode.NextNodeIndex
                    != allocation.NodeIndex
                || previousNode.PhysicalEndExclusive
                    > sourceNode.PhysicalStart
                || allocation.BeforeState.HeadNodeIndex
                    == allocation.NodeIndex)
            {
                return false;
            }
        }

        bool hasInsertedSuffix =
            coalescingKind
                == BootPageAllocatorOnePageCoalescingKind
                    .PreviousAndNext;
        if (hasInsertedSuffix)
        {
            if (allocation.InsertedNodeIndex < 0
                || (ulong)allocation.InsertedNodeIndex
                    >= allocation.NodeCount
                || allocation.InsertedNodeIndex
                    == allocation.NodeIndex
                || allocation.InsertedNodeIndex
                    == sourceNode.NextNodeIndex)
            {
                return false;
            }
        }
        else if (allocation.InsertedNodeIndex != -1)
        {
            return false;
        }

        int expectedAfterFreeNodeCount =
            coalescingKind switch
            {
                BootPageAllocatorOnePageCoalescingKind.None =>
                    allocation.BeforeState.FreeNodeCount - 1,
                BootPageAllocatorOnePageCoalescingKind
                    .PreviousAndNext =>
                    allocation.BeforeState.FreeNodeCount + 1,
                _ => allocation.BeforeState.FreeNodeCount
            };
        if (allocation.AfterState.FreeNodeCount
                != expectedAfterFreeNodeCount
            || (coalescingKind
                    == BootPageAllocatorOnePageCoalescingKind.None
                && previousNodeIndex == -1
                && allocation.AfterState.HeadNodeIndex
                    != sourceNode.NextNodeIndex)
            || (coalescingKind
                    == BootPageAllocatorOnePageCoalescingKind.None
                && previousNodeIndex >= 0
                && allocation.AfterState.HeadNodeIndex
                    != allocation.BeforeState.HeadNodeIndex)
            || (coalescingKind
                    != BootPageAllocatorOnePageCoalescingKind.None
                && allocation.AfterState.HeadNodeIndex
                    != allocation.BeforeState.HeadNodeIndex))
        {
            return false;
        }

        return true;
    }

    private static bool TryGetOnePageCoalescingKind(
        BootPageAllocatorOnePageResult allocation,
        out BootPageAllocatorOnePageCoalescingKind coalescingKind)
    {
        coalescingKind =
            BootPageAllocatorOnePageCoalescingKind.Uninitialized;
        BootPageFreeListNode sourceNode =
            allocation.RestoreReceipt.SourceNodeBefore;
        if (allocation.PhysicalStart.TryGetRawValueForUefiBoundary(
                out ulong physicalStart)
                != OS.Boot.AddressValueStatus.Success
            || allocation.PhysicalEndExclusive
                .TryGetRawValueForUefiBoundary(
                    out ulong physicalEndExclusive)
                != OS.Boot.AddressValueStatus.Success
            || physicalStart < sourceNode.PhysicalStart
            || physicalEndExclusive
                > sourceNode.PhysicalEndExclusive
            || physicalStart >= physicalEndExclusive)
        {
            return false;
        }

        bool hasPrefix = physicalStart > sourceNode.PhysicalStart;
        bool hasSuffix =
            physicalEndExclusive < sourceNode.PhysicalEndExclusive;
        coalescingKind = hasPrefix
            ? hasSuffix
                ? BootPageAllocatorOnePageCoalescingKind
                    .PreviousAndNext
                : BootPageAllocatorOnePageCoalescingKind.Previous
            : hasSuffix
                ? BootPageAllocatorOnePageCoalescingKind.Next
                : BootPageAllocatorOnePageCoalescingKind.None;
        return true;
    }

    private static BootPageAllocatorMutationStatus
        ValidateOnePageCandidateReservationExclusion(
            BootPageAllocatorOnePageResult allocation,
            BootReservedRange* reservations,
            ulong reservationCount,
            out ulong invalidReservationIndex)
    {
        invalidReservationIndex = NoIndex;
        if (!TryGetOnePageAllocationBounds(
                allocation,
                out ulong physicalStart,
                out ulong physicalEndExclusive))
        {
            return BootPageAllocatorMutationStatus
                .FreeRangeNotAllocated;
        }

        for (ulong index = 0; index < reservationCount; index++)
        {
            TryGetOnePageReservationBounds(
                reservations[index],
                out ulong reservationStart,
                out ulong reservationEndExclusive);
            if (reservationStart < physicalEndExclusive
                && physicalStart < reservationEndExclusive)
            {
                invalidReservationIndex = index;
                return BootPageAllocatorMutationStatus
                    .ReservationOverlapsFreeRange;
            }
        }

        return BootPageAllocatorMutationStatus.Success;
    }

    private static BootPageAllocatorMutationStatus
        ValidateOnePageCandidateFreeListExclusion(
            BootPageAllocatorState state,
            BootPageFreeListNode* nodes,
            BootPageAllocatorOnePageResult allocation,
            out int conflictingNodeIndex)
    {
        conflictingNodeIndex = -1;
        if (!TryGetOnePageAllocationBounds(
                allocation,
                out ulong physicalStart,
                out ulong physicalEndExclusive))
        {
            return BootPageAllocatorMutationStatus
                .FreeRangeNotAllocated;
        }

        int nodeIndex = state.HeadNodeIndex;
        for (int visited = 0;
            visited < state.FreeNodeCount;
            visited++)
        {
            BootPageFreeListNode node = nodes[nodeIndex];
            if (physicalStart < node.PhysicalEndExclusive
                && node.PhysicalStart < physicalEndExclusive)
            {
                conflictingNodeIndex = nodeIndex;
                return BootPageAllocatorMutationStatus
                    .FreeRangeOverlaps;
            }

            nodeIndex = node.NextNodeIndex;
        }

        return BootPageAllocatorMutationStatus.Success;
    }

    private static BootPageAllocatorMutationStatus
        ValidateOnePagePostAllocationTopology(
            BootPageAllocatorState state,
            BootPageFreeListNode* nodes,
            ulong nodeCount,
            BootPageAllocatorOnePageResult allocation,
            out BootPageAllocatorOnePageCoalescingKind
                coalescingKind,
            out int removedSuffixNodeIndex)
    {
        coalescingKind =
            BootPageAllocatorOnePageCoalescingKind.Uninitialized;
        removedSuffixNodeIndex = -1;
        if (!IsValidOnePageRestoreReceipt(allocation)
            || allocation.NodeIndex < 0
            || (ulong)allocation.NodeIndex >= nodeCount
            || !TryGetOnePageCoalescingKind(
                allocation,
                out coalescingKind)
            || !TryGetOnePageAllocationBounds(
                allocation,
                out ulong physicalStart,
                out ulong physicalEndExclusive))
        {
            return BootPageAllocatorMutationStatus
                .FreeRangeNotAllocated;
        }

        BootPageAllocatorOnePageRestoreReceipt receipt =
            allocation.RestoreReceipt;
        BootPageFreeListNode sourceNode = receipt.SourceNodeBefore;
        int previousNodeIndex = receipt.PreviousNodeIndexBefore;
        bool sourceNodeIsActive = IsOnePageNodeActive(
            state,
            nodes,
            allocation.NodeIndex);
        bool previousNodeIsActive =
            previousNodeIndex < 0
            || IsOnePageNodeActive(
                state,
                nodes,
                previousNodeIndex);
        int successorNodeIndex = sourceNode.NextNodeIndex;
        if (successorNodeIndex != -1
            && (!IsOnePageNodeActive(
                    state,
                    nodes,
                    successorNodeIndex)
                || sourceNode.PhysicalEndExclusive
                    > nodes[successorNodeIndex].PhysicalStart))
        {
            return BootPageAllocatorMutationStatus
                .FreeRangeNotAllocated;
        }

        if (coalescingKind
            == BootPageAllocatorOnePageCoalescingKind.None)
        {
            if (sourceNodeIsActive
                || !previousNodeIsActive
                || !IsZeroedOnePageNodeSlot(
                    nodes,
                    (ulong)allocation.NodeIndex))
            {
                return BootPageAllocatorMutationStatus
                    .FreeRangeNotAllocated;
            }

            if (previousNodeIndex < 0)
            {
                return state.HeadNodeIndex
                        == sourceNode.NextNodeIndex
                    ? BootPageAllocatorMutationStatus.Success
                    : BootPageAllocatorMutationStatus
                        .FreeRangeNotAllocated;
            }

            BootPageFreeListNode expectedPrevious =
                ReplaceOnePageNext(
                    receipt.PreviousNodeBefore,
                    sourceNode.NextNodeIndex);
            return OnePageNodesEqual(
                    nodes[previousNodeIndex],
                    expectedPrevious)
                ? BootPageAllocatorMutationStatus.Success
                : BootPageAllocatorMutationStatus
                    .FreeRangeNotAllocated;
        }

        if (!sourceNodeIsActive || !previousNodeIsActive)
        {
            return BootPageAllocatorMutationStatus
                .FreeRangeNotAllocated;
        }

        if (previousNodeIndex < 0)
        {
            if (state.HeadNodeIndex != allocation.NodeIndex)
            {
                return BootPageAllocatorMutationStatus
                    .FreeRangeNotAllocated;
            }
        }
        else if (!OnePageNodesEqual(
                nodes[previousNodeIndex],
                receipt.PreviousNodeBefore))
        {
            return BootPageAllocatorMutationStatus
                .FreeRangeNotAllocated;
        }

        if (coalescingKind
            == BootPageAllocatorOnePageCoalescingKind.Next)
        {
            BootPageFreeListNode expectedSuffix =
                CreateOnePageNode(
                    sourceNode,
                    physicalEndExclusive,
                    sourceNode.PhysicalEndExclusive,
                    sourceNode.NextNodeIndex);
            return OnePageNodesEqual(
                    nodes[allocation.NodeIndex],
                    expectedSuffix)
                ? BootPageAllocatorMutationStatus.Success
                : BootPageAllocatorMutationStatus
                    .FreeRangeNotAllocated;
        }

        if (coalescingKind
            == BootPageAllocatorOnePageCoalescingKind.Previous)
        {
            BootPageFreeListNode expectedPrefix =
                CreateOnePageNode(
                    sourceNode,
                    sourceNode.PhysicalStart,
                    physicalStart,
                    sourceNode.NextNodeIndex);
            return OnePageNodesEqual(
                    nodes[allocation.NodeIndex],
                    expectedPrefix)
                ? BootPageAllocatorMutationStatus.Success
                : BootPageAllocatorMutationStatus
                    .FreeRangeNotAllocated;
        }

        int insertedNodeIndex = allocation.InsertedNodeIndex;
        if (insertedNodeIndex < 0
            || (ulong)insertedNodeIndex >= nodeCount
            || !IsOnePageNodeActive(
                state,
                nodes,
                insertedNodeIndex))
        {
            return BootPageAllocatorMutationStatus
                .FreeRangeNotAllocated;
        }

        BootPageFreeListNode expectedPrefixNode =
            CreateOnePageNode(
                sourceNode,
                sourceNode.PhysicalStart,
                physicalStart,
                insertedNodeIndex);
        BootPageFreeListNode expectedSuffixNode =
            CreateOnePageNode(
                sourceNode,
                physicalEndExclusive,
                sourceNode.PhysicalEndExclusive,
                sourceNode.NextNodeIndex);
        if (!OnePageNodesEqual(
                nodes[allocation.NodeIndex],
                expectedPrefixNode)
            || !OnePageNodesEqual(
                nodes[insertedNodeIndex],
                expectedSuffixNode))
        {
            return BootPageAllocatorMutationStatus
                .FreeRangeNotAllocated;
        }

        removedSuffixNodeIndex = insertedNodeIndex;
        return BootPageAllocatorMutationStatus.Success;
    }

    private static bool IsOnePageNodeActive(
        BootPageAllocatorState state,
        BootPageFreeListNode* nodes,
        int candidateNodeIndex)
    {
        int nodeIndex = state.HeadNodeIndex;
        for (int visited = 0;
            visited < state.FreeNodeCount && nodeIndex != -1;
            visited++)
        {
            if (nodeIndex == candidateNodeIndex)
            {
                return true;
            }

            nodeIndex = nodes[nodeIndex].NextNodeIndex;
        }

        return false;
    }

    private static void ApplyOnePageFree(
        BootPageFreeListNode* nodes,
        BootPageAllocatorOnePageResult allocation,
        BootPageAllocatorOnePageCoalescingKind coalescingKind)
    {
        BootPageAllocatorOnePageRestoreReceipt receipt =
            allocation.RestoreReceipt;
        nodes[allocation.NodeIndex] = receipt.SourceNodeBefore;

        if (coalescingKind
                == BootPageAllocatorOnePageCoalescingKind.None
            && receipt.PreviousNodeIndexBefore >= 0)
        {
            nodes[receipt.PreviousNodeIndexBefore] =
                receipt.PreviousNodeBefore;
        }
        else if (coalescingKind
            == BootPageAllocatorOnePageCoalescingKind
                .PreviousAndNext)
        {
            nodes[allocation.InsertedNodeIndex] = default;
        }
    }

    private static bool TryGetOnePageAllocationBounds(
        BootPageAllocatorOnePageResult allocation,
        out ulong physicalStart,
        out ulong physicalEndExclusive)
    {
        physicalStart = 0;
        physicalEndExclusive = 0;
        return allocation.PhysicalStart
                .TryGetRawValueForUefiBoundary(out physicalStart)
                == OS.Boot.AddressValueStatus.Success
            && allocation.PhysicalEndExclusive
                .TryGetRawValueForUefiBoundary(
                    out physicalEndExclusive)
                == OS.Boot.AddressValueStatus.Success
            && physicalStart <= MaximumUnsignedLong - RequiredPageSize
            && physicalEndExclusive
                == physicalStart + RequiredPageSize;
    }

    private static bool OnePageStatesEqual(
        BootPageAllocatorState left,
        BootPageAllocatorState right)
    {
        return left.IsInitialized == right.IsInitialized
            && left.PageSize == right.PageSize
            && left.HeadNodeIndex == right.HeadNodeIndex
            && left.FreeNodeCount == right.FreeNodeCount
            && left.FreePageCount == right.FreePageCount
            && left.FreeByteCount == right.FreeByteCount
            && left.AllocatedPageCount == right.AllocatedPageCount
            && left.AllocatedByteCount == right.AllocatedByteCount;
    }

    private static bool OnePageNodesEqual(
        BootPageFreeListNode left,
        BootPageFreeListNode right)
    {
        return left.SourceFreeSegmentIndex
                == right.SourceFreeSegmentIndex
            && left.SourceUsableRangeIndex
                == right.SourceUsableRangeIndex
            && left.PhysicalStart == right.PhysicalStart
            && left.PhysicalEndExclusive
                == right.PhysicalEndExclusive
            && left.PageCount == right.PageCount
            && left.ByteLength == right.ByteLength
            && left.NextNodeIndex == right.NextNodeIndex;
    }

    private static BootPageAllocatorState ApplyOnePageAllocation(
        BootPageAllocatorState state,
        BootPageFreeListNode* nodes,
        int previousNodeIndex,
        int currentNodeIndex,
        int insertedNodeIndex,
        BootPageFreeListNode node,
        ulong allocationStart,
        ulong allocationEnd,
        bool hasPrefix,
        bool hasSuffix)
    {
        int nextHeadNodeIndex = state.HeadNodeIndex;
        int nextFreeNodeCount = state.FreeNodeCount;

        if (!hasPrefix && !hasSuffix)
        {
            if (previousNodeIndex < 0)
            {
                nextHeadNodeIndex = node.NextNodeIndex;
            }
            else
            {
                BootPageFreeListNode previous =
                    nodes[previousNodeIndex];
                nodes[previousNodeIndex] =
                    ReplaceOnePageNext(
                        previous,
                        node.NextNodeIndex);
            }

            nodes[currentNodeIndex] = default;
            nextFreeNodeCount--;
        }
        else if (!hasPrefix)
        {
            nodes[currentNodeIndex] = CreateOnePageNode(
                node,
                allocationEnd,
                node.PhysicalEndExclusive,
                node.NextNodeIndex);
        }
        else if (!hasSuffix)
        {
            nodes[currentNodeIndex] = CreateOnePageNode(
                node,
                node.PhysicalStart,
                allocationStart,
                node.NextNodeIndex);
        }
        else
        {
            nodes[insertedNodeIndex] = CreateOnePageNode(
                node,
                allocationEnd,
                node.PhysicalEndExclusive,
                node.NextNodeIndex);
            nodes[currentNodeIndex] = CreateOnePageNode(
                node,
                node.PhysicalStart,
                allocationStart,
                insertedNodeIndex);
            nextFreeNodeCount++;
        }

        return new BootPageAllocatorState(
            isInitialized: true,
            RequiredPageSize,
            nextHeadNodeIndex,
            nextFreeNodeCount,
            state.FreePageCount - RequiredPageCount,
            state.FreeByteCount - RequiredPageSize,
            state.AllocatedPageCount + RequiredPageCount,
            state.AllocatedByteCount + RequiredPageSize);
    }

    private static BootPageFreeListNode ReplaceOnePageNext(
        BootPageFreeListNode node,
        int nextNodeIndex)
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

    private static BootPageFreeListNode CreateOnePageNode(
        BootPageFreeListNode source,
        ulong physicalStart,
        ulong physicalEndExclusive,
        int nextNodeIndex)
    {
        ulong byteLength = physicalEndExclusive - physicalStart;
        return new BootPageFreeListNode(
            source.SourceFreeSegmentIndex,
            source.SourceUsableRangeIndex,
            physicalStart,
            physicalEndExclusive,
            byteLength / RequiredPageSize,
            byteLength,
            nextNodeIndex);
    }

    private static bool TryAddOnePage(
        ulong left,
        ulong right,
        out ulong value)
    {
        value = 0;
        if (left > MaximumUnsignedLong - right)
        {
            return false;
        }

        value = left + right;
        return true;
    }

#if !NOSTDLIB_EFI
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
#endif
}
