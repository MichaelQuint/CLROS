namespace OS.Kernel.Memory;
public readonly struct BootPageAllocatorOnePageFreeResult
{
    private readonly byte _isInitialized;
    private BootPageAllocatorOnePageFreeResult(BootPageAllocatorMutationStatus status, BootReservedRangeStatus reservationStatus, OS.Boot.AddressValueStatus addressStatus, BootPageAllocatorState beforeState, BootPageAllocatorState afterState, BootPageAllocatorOnePageResult allocation, BootPageAllocatorOnePageCoalescingKind coalescingKind, ulong nodeCount, ulong nodeCapacity, ulong reservationCount, ulong reservationCapacity, int restoredNodeIndex, int previousNodeIndex, int removedSuffixNodeIndex, ulong visitedNodeCount, ulong invalidReservationIndex, ulong conflictingIndex)
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
    public bool Succeeded => IsInitialized && Status == BootPageAllocatorMutationStatus.Success && ReservationStatus == BootReservedRangeStatus.Success && AddressStatus == OS.Boot.AddressValueStatus.Success && InvalidReservationIndex == BootPageAllocatorMutator.NoIndex && ConflictingIndex == BootPageAllocatorMutator.NoIndex && BootPageAllocatorMutator.IsRepresentableFreeSuccessSummary(BeforeState, AfterState, Allocation, CoalescingKind, NodeCount, NodeCapacity, ReservationCount, ReservationCapacity, RestoredNodeIndex, PreviousNodeIndex, RemovedSuffixNodeIndex, VisitedNodeCount);
    public BootPageAllocatorState BeforeState { get; }
    public BootPageAllocatorState AfterState { get; }
    public BootPageAllocatorOnePageResult Allocation { get; }
    public BootPageAllocatorOnePageCoalescingKind CoalescingKind { get; }
    public OS.Boot.PhysicalAddress PhysicalStart => Allocation.PhysicalStart;
    public OS.Boot.PhysicalAddress PhysicalEndExclusive => Allocation.PhysicalEndExclusive;
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
    public ulong BeforeAllocatedPageCount => BeforeState.AllocatedPageCount;
    public ulong AfterAllocatedPageCount => AfterState.AllocatedPageCount;
    public ulong BeforeAllocatedByteCount => BeforeState.AllocatedByteCount;
    public ulong AfterAllocatedByteCount => AfterState.AllocatedByteCount;

    internal static BootPageAllocatorOnePageFreeResult Success(BootPageAllocatorState beforeState, BootPageAllocatorState afterState, BootPageAllocatorOnePageResult allocation, BootPageAllocatorOnePageCoalescingKind coalescingKind, ulong nodeCount, ulong nodeCapacity, ulong reservationCount, ulong reservationCapacity, int restoredNodeIndex, int previousNodeIndex, int removedSuffixNodeIndex, ulong visitedNodeCount)
    {
        return new BootPageAllocatorOnePageFreeResult(BootPageAllocatorMutationStatus.Success, BootReservedRangeStatus.Success, OS.Boot.AddressValueStatus.Success, beforeState, afterState, allocation, coalescingKind, nodeCount, nodeCapacity, reservationCount, reservationCapacity, restoredNodeIndex, previousNodeIndex, removedSuffixNodeIndex, visitedNodeCount, BootPageAllocatorMutator.NoIndex, BootPageAllocatorMutator.NoIndex);
    }

    internal static BootPageAllocatorOnePageFreeResult Fail(BootPageAllocatorMutationStatus status, BootReservedRangeStatus reservationStatus, OS.Boot.AddressValueStatus addressStatus, BootPageAllocatorState state, BootPageAllocatorOnePageResult allocation, ulong nodeCount, ulong nodeCapacity, ulong reservationCount, ulong reservationCapacity, int restoredNodeIndex, int previousNodeIndex, int removedSuffixNodeIndex, ulong visitedNodeCount, ulong invalidReservationIndex, ulong conflictingIndex)
    {
        return new BootPageAllocatorOnePageFreeResult(status, reservationStatus, addressStatus, state, state, allocation, BootPageAllocatorOnePageCoalescingKind.Uninitialized, nodeCount, nodeCapacity, reservationCount, reservationCapacity, restoredNodeIndex, previousNodeIndex, removedSuffixNodeIndex, visitedNodeCount, invalidReservationIndex, conflictingIndex);
    }
}
#if !NOSTDLIB_EFI
#endif
#if !NOSTDLIB_EFI
#endif
