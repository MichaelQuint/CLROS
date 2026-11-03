namespace OS.Kernel.Memory;

public readonly struct BootPageAllocatorOnePageResult
{
    private readonly byte _isInitialized;
    private BootPageAllocatorOnePageResult(BootPageAllocatorMutationStatus status, BootReservedRangeStatus reservationStatus, OS.Boot.AddressValueStatus addressStatus, BootPageAllocatorState beforeState, BootPageAllocatorState afterState, OS.Boot.PhysicalAddress physicalStart, OS.Boot.PhysicalAddress physicalEndExclusive, ulong pageCount, ulong byteLength, ulong nodeCount, ulong nodeCapacity, ulong reservationCount, ulong reservationCapacity, int nodeIndex, int insertedNodeIndex, ulong visitedNodeCount, ulong invalidReservationIndex, ulong conflictingIndex, BootPageAllocatorOnePageRestoreReceipt restoreReceipt)
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
    public BootPageAllocatorMutationStatus Status
    {
        get;
    }
    public BootReservedRangeStatus ReservationStatus
    {
        get;
    }
    public OS.Boot.AddressValueStatus AddressStatus
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == BootPageAllocatorMutationStatus.Success && ReservationStatus == BootReservedRangeStatus.Success && AddressStatus == OS.Boot.AddressValueStatus.Success && InvalidReservationIndex == BootPageAllocatorMutator.NoIndex && ConflictingIndex == BootPageAllocatorMutator.NoIndex && BootPageAllocatorMutator.IsRepresentableSuccessSummary(BeforeState, AfterState, PhysicalStart, PhysicalEndExclusive, PageCount, ByteLength, NodeCount, NodeCapacity, ReservationCount, ReservationCapacity, NodeIndex, InsertedNodeIndex, VisitedNodeCount);
    public BootPageAllocatorState BeforeState
    {
        get;
    }
    public BootPageAllocatorState AfterState
    {
        get;
    }
    public OS.Boot.PhysicalAddress PhysicalStart
    {
        get;
    }
    public OS.Boot.PhysicalAddress PhysicalEndExclusive
    {
        get;
    }
    public ulong PageCount
    {
        get;
    }
    public ulong ByteLength
    {
        get;
    }
    public ulong NodeCount
    {
        get;
    }
    public ulong NodeCapacity
    {
        get;
    }
    public ulong ReservationCount
    {
        get;
    }
    public ulong ReservationCapacity
    {
        get;
    }
    public int NodeIndex
    {
        get;
    }
    public int InsertedNodeIndex
    {
        get;
    }
    public ulong VisitedNodeCount
    {
        get;
    }
    public ulong InvalidReservationIndex
    {
        get;
    }
    public ulong ConflictingIndex
    {
        get;
    }
    public BootPageAllocatorOnePageRestoreReceipt RestoreReceipt
    {
        get;
    }
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

    internal static BootPageAllocatorOnePageResult Success(BootPageAllocatorState beforeState, BootPageAllocatorState afterState, OS.Boot.PhysicalAddress physicalStart, OS.Boot.PhysicalAddress physicalEndExclusive, ulong nodeCount, ulong nodeCapacity, ulong reservationCount, ulong reservationCapacity, int nodeIndex, int insertedNodeIndex, ulong visitedNodeCount)
    {
        return new BootPageAllocatorOnePageResult(BootPageAllocatorMutationStatus.Success, BootReservedRangeStatus.Success, OS.Boot.AddressValueStatus.Success, beforeState, afterState, physicalStart, physicalEndExclusive, BootPageAllocatorMutator.RequiredPageCount, BootPageAllocatorMutator.RequiredPageSize, nodeCount, nodeCapacity, reservationCount, reservationCapacity, nodeIndex, insertedNodeIndex, visitedNodeCount, BootPageAllocatorMutator.NoIndex, BootPageAllocatorMutator.NoIndex, default);
    }

    internal static BootPageAllocatorOnePageResult SuccessWithRestoreReceipt(BootPageAllocatorState beforeState, BootPageAllocatorState afterState, OS.Boot.PhysicalAddress physicalStart, OS.Boot.PhysicalAddress physicalEndExclusive, ulong nodeCount, ulong nodeCapacity, ulong reservationCount, ulong reservationCapacity, int nodeIndex, int insertedNodeIndex, ulong visitedNodeCount, BootPageFreeListNode sourceNodeBefore, int previousNodeIndexBefore, BootPageFreeListNode previousNodeBefore)
    {
        return new BootPageAllocatorOnePageResult(BootPageAllocatorMutationStatus.Success, BootReservedRangeStatus.Success, OS.Boot.AddressValueStatus.Success, beforeState, afterState, physicalStart, physicalEndExclusive, BootPageAllocatorMutator.RequiredPageCount, BootPageAllocatorMutator.RequiredPageSize, nodeCount, nodeCapacity, reservationCount, reservationCapacity, nodeIndex, insertedNodeIndex, visitedNodeCount, BootPageAllocatorMutator.NoIndex, BootPageAllocatorMutator.NoIndex, BootPageAllocatorOnePageRestoreReceipt.Create(sourceNodeBefore, previousNodeIndexBefore, previousNodeBefore));
    }

    internal static BootPageAllocatorOnePageResult Fail(BootPageAllocatorMutationStatus status, BootReservedRangeStatus reservationStatus, OS.Boot.AddressValueStatus addressStatus, BootPageAllocatorState state, ulong pageCount, ulong byteLength, ulong nodeCount, ulong nodeCapacity, ulong reservationCount, ulong reservationCapacity, int nodeIndex, int insertedNodeIndex, ulong visitedNodeCount, ulong invalidReservationIndex, ulong conflictingIndex)
    {
        return new BootPageAllocatorOnePageResult(status, reservationStatus, addressStatus, state, state, default, default, pageCount, byteLength, nodeCount, nodeCapacity, reservationCount, reservationCapacity, nodeIndex, insertedNodeIndex, visitedNodeCount, invalidReservationIndex, conflictingIndex, default);
    }
}
#if !NOSTDLIB_EFI
#endif
#if !NOSTDLIB_EFI
#endif
