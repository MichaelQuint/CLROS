using OS.Kernel.Memory;
using System.Runtime.CompilerServices;

namespace OS.Kernel.Runtime;
/// <summary>
/// Reserves one data page from the original authority, before firmware exit.
/// Callers guarantee stable exclusive storage and that backing really maps the
/// selected parent. Numeric checks cannot prove mapping, physical alias freedom,
/// hardware NX, post-firmware lifetime or an unforgeable allocation registry.
/// </summary>

public static unsafe class ManagedHeapPageRangeLiveAllocator
{
    public const ulong PageSize = 4096;
    public static ulong RequiredStateBytes => (ulong)sizeof(ManagedHeapPageRangeState);

    public static ManagedHeapPageRangeResult ReserveAndInitializeOnce(BootPageAllocatorState* allocator, ulong allocatorCapacity, BootPageFreeListNode* nodes, ulong nodeCount, ulong nodeCapacity, BootReservedRange* reservations, ulong reservationCount, ulong reservationCapacity, ulong parentIndex, byte* backing, ulong backingCapacity, ManagedHeapPageRangeState* heap, ulong heapCapacityBytes)
    {
        ManagedHeapPageRangeStatus status = ValidateStorage(allocator, allocatorCapacity, nodes, nodeCount, nodeCapacity, reservations, reservationCount, reservationCapacity, parentIndex, backing, backingCapacity, heap, heapCapacityBytes);
        if (status != ManagedHeapPageRangeStatus.Success)
            return new ManagedHeapPageRangeResult(status);
        // Inspect the complete owned prefix, including padding, before any writes.
        for (ulong i = 0; i < RequiredStateBytes; i++)
            if (((byte*)heap)[i] != 0)
                return new ManagedHeapPageRangeResult(ManagedHeapPageRangeStatus.AlreadyInitialized);
        status = ValidateParent(reservations, reservationCount, reservationCapacity, parentIndex, backingCapacity, out BootReservedRange parent, out ulong parentStart, out ulong parentEnd);
        if (status != ManagedHeapPageRangeStatus.Success)
            return new ManagedHeapPageRangeResult(status);
        status = ValidateCurrentAllocator(*allocator, nodes, nodeCount, parentStart, parentEnd, 0, 0);
        if (status != ManagedHeapPageRangeStatus.Success)
            return new ManagedHeapPageRangeResult(status);
        BootPageAllocatorState before = *allocator;
        // The global ledger is not a suballocator exclusion list: its containing
        // parent would exclude every free page. Other entries are disjoint from it.
        BootPageAllocatorOnePageResult allocation = BootPageAllocatorMutator.AllocateOnePage(before, nodes, nodeCount, nodeCapacity, null, 0, 0, 1, PageSize);
        if (allocation.Status != BootPageAllocatorMutationStatus.Success)
            return new ManagedHeapPageRangeResult(ManagedHeapPageRangeStatus.AllocationRejected, allocationStatus: allocation.Status);
        // Publish reservation ownership before checking or writing the backing.
        // A later inconsistency must never silently return this page to the pool.
        *allocator = allocation.AfterState;
        allocation.PhysicalStart.TryGetRawValueForUefiBoundary(out ulong physicalStart);
        allocation.PhysicalEndExclusive.TryGetRawValueForUefiBoundary(out ulong physicalEnd);
        *heap = new ManagedHeapPageRangeState
        {
            LifecycleValue = ManagedHeapPageRangeLifecycle.ReservedNotReady,
            SelfAddressValue = (ulong)heap,
            StateCapacityValue = heapCapacityBytes,
            AllocatorAddressValue = (ulong)allocator,
            NodesAddressValue = (ulong)nodes,
            NodeCountValue = nodeCount,
            NodeCapacityValue = nodeCapacity,
            LedgerAddressValue = (ulong)reservations,
            ReservationCountValue = reservationCount,
            ReservationCapacityValue = reservationCapacity,
            ParentIndexValue = parentIndex,
            BackingAddressValue = (ulong)backing,
            BackingCapacityValue = backingCapacity,
            ParentValue = parent,
            PhysicalStartValue = physicalStart,
            PhysicalEndValue = physicalEnd,
            BeforeStateValue = before
        };
        if (!allocation.Succeeded || !IsPageWithinParent(physicalStart, physicalEnd, parentStart, parentEnd))
            return Observe(ManagedHeapPageRangeStatus.AllocationResultRejected, heap, *allocator);
        byte* page = backing + (physicalStart - parentStart);
        for (ulong i = 0; i < PageSize; i++)
            page[i] = 0;
        bool zero = ReadFixedPage(page, out ulong readBytes);
        if (!zero || readBytes != PageSize)
            return Observe(ManagedHeapPageRangeStatus.InitializationReadbackFailed, heap, *allocator, (ulong)page, readBytes, zero);
        heap->LifecycleValue = ManagedHeapPageRangeLifecycle.Ready;
        return Observe(ManagedHeapPageRangeStatus.Success, heap, *allocator, (ulong)page, readBytes, zero);
    }

    public static ManagedHeapPageRangeResult ValidateRetained(BootPageAllocatorState* allocator, ulong allocatorCapacity, BootPageFreeListNode* nodes, ulong nodeCount, ulong nodeCapacity, BootReservedRange* reservations, ulong reservationCount, ulong reservationCapacity, ulong parentIndex, byte* backing, ulong backingCapacity, ManagedHeapPageRangeState* heap, ulong heapCapacityBytes)
    {
        ManagedHeapPageRangeStatus status = ValidateStorage(allocator, allocatorCapacity, nodes, nodeCount, nodeCapacity, reservations, reservationCount, reservationCapacity, parentIndex, backing, backingCapacity, heap, heapCapacityBytes);
        if (status != ManagedHeapPageRangeStatus.Success)
            return new ManagedHeapPageRangeResult(status);
        if (heap->LifecycleValue != ManagedHeapPageRangeLifecycle.Ready && heap->LifecycleValue != ManagedHeapPageRangeLifecycle.ReservedNotReady)
            return new ManagedHeapPageRangeResult(ManagedHeapPageRangeStatus.NotReady);
        if (heap->SelfAddressValue != (ulong)heap || heap->StateCapacityValue != heapCapacityBytes || heap->AllocatorAddressValue != (ulong)allocator || heap->NodesAddressValue != (ulong)nodes || heap->NodeCountValue != nodeCount || heap->NodeCapacityValue != nodeCapacity || heap->LedgerAddressValue != (ulong)reservations || heap->ReservationCountValue != reservationCount || heap->ReservationCapacityValue != reservationCapacity || heap->ParentIndexValue != parentIndex || heap->BackingAddressValue != (ulong)backing || heap->BackingCapacityValue != backingCapacity)
            return new ManagedHeapPageRangeResult(ManagedHeapPageRangeStatus.IdentityMismatch);
        status = ValidateParent(reservations, reservationCount, reservationCapacity, parentIndex, backingCapacity, out BootReservedRange parent, out ulong parentStart, out ulong parentEnd);
        if (status != ManagedHeapPageRangeStatus.Success)
            return new ManagedHeapPageRangeResult(status);
        if (!parent.Equals(heap->ParentValue) || !IsPageWithinParent(heap->PhysicalStartValue, heap->PhysicalEndValue, parentStart, parentEnd))
            return new ManagedHeapPageRangeResult(ManagedHeapPageRangeStatus.IdentityMismatch);
        status = ValidateCurrentAllocator(*allocator, nodes, nodeCount, parentStart, parentEnd, heap->PhysicalStartValue, heap->PhysicalEndValue);
        if (status != ManagedHeapPageRangeStatus.Success)
            return new ManagedHeapPageRangeResult(status);
        if (heap->LifecycleValue != ManagedHeapPageRangeLifecycle.Ready)
            return Observe(ManagedHeapPageRangeStatus.NotReady, heap, *allocator);
        byte* page = backing + (heap->PhysicalStartValue - parentStart);
        bool zero = ReadFixedPage(page, out ulong readBytes);
        return Observe(ManagedHeapPageRangeStatus.Success, heap, *allocator, (ulong)page, readBytes, zero);
    }

    private static ManagedHeapPageRangeStatus ValidateStorage(BootPageAllocatorState* allocator, ulong allocatorCapacity, BootPageFreeListNode* nodes, ulong nodeCount, ulong nodeCapacity, BootReservedRange* reservations, ulong reservationCount, ulong reservationCapacity, ulong parentIndex, byte* backing, ulong backingCapacity, ManagedHeapPageRangeState* heap, ulong heapCapacityBytes)
    {
        if (sizeof(void*) != 8 || (ulong)sizeof(BootPageAllocatorState) != 56 || (ulong)sizeof(BootPageFreeListNode) != 48 || (ulong)sizeof(BootReservedRange) != 40 || allocatorCapacity != 1 || nodeCount == 0 || nodeCount > BootPageAllocatorMutator.MaximumNodeCount || nodeCapacity < nodeCount || nodeCapacity > ~0UL / 48 || reservationCount == 0 || reservationCount > BootReservationTableValidator.MaximumReservationCount || reservationCapacity < reservationCount || reservationCapacity > ~0UL / 40 || parentIndex >= reservationCount || backingCapacity < PageSize || heapCapacityBytes < RequiredStateBytes)
            return ManagedHeapPageRangeStatus.InvalidStorage;
        // Check entire supplied capacities, not just the bytes we will touch.
        ulong* starts = stackalloc ulong[5]
        {
            (ulong)allocator,
            (ulong)nodes,
            (ulong)reservations,
            (ulong)heap,
            (ulong)backing
        };
        ulong* lengths = stackalloc ulong[5]
        {
            56,
            nodeCapacity * 48,
            reservationCapacity * 40,
            heapCapacityBytes,
            backingCapacity
        };
        for (int i = 0; i < 5; i++)
        {
            ulong alignment = i == 4 ? PageSize : 8;
            if (starts[i] == 0 || (starts[i] & (alignment - 1)) != 0 || starts[i] > ~0UL - lengths[i])
                return ManagedHeapPageRangeStatus.InvalidStorage;
        }

        for (int i = 0; i < 5; i++)
            for (int j = i + 1; j < 5; j++)
                if (starts[i] < starts[j] + lengths[j] && starts[j] < starts[i] + lengths[i])
                    return ManagedHeapPageRangeStatus.StorageOverlap;
        return ManagedHeapPageRangeStatus.Success;
    }

    private static ManagedHeapPageRangeStatus ValidateParent(BootReservedRange* reservations, ulong count, ulong capacity, ulong parentIndex, ulong backingCapacity, out BootReservedRange parent, out ulong start, out ulong end)
    {
        parent = default;
        start = 0;
        end = 0;
        if (!BootReservationTableValidator.Validate(reservations, count, capacity).Succeeded)
            return ManagedHeapPageRangeStatus.InvalidReservationTable;
        parent = reservations[parentIndex];
        if (parent.Kind != BootReservationKind.AllocatorArena || parent.ByteLength > backingCapacity)
            return ManagedHeapPageRangeStatus.InvalidParent;
        parent.PhysicalStart.TryGetRawValueForUefiBoundary(out start);
        parent.PhysicalEndExclusive.TryGetRawValueForUefiBoundary(out end);
        return ManagedHeapPageRangeStatus.Success;
    }

    private static ManagedHeapPageRangeStatus ValidateCurrentAllocator(BootPageAllocatorState state, BootPageFreeListNode* nodes, ulong nodeCount, ulong parentStart, ulong parentEnd, ulong retainedStart, ulong retainedEnd)
    {
        ulong parentPages = (parentEnd - parentStart) / PageSize;
        if (!state.IsInitialized || state.PageSize != PageSize || state.FreeNodeCount < 0 || (ulong)state.FreeNodeCount > nodeCount || state.FreePageCount > parentPages || state.AllocatedPageCount != parentPages - state.FreePageCount || state.FreeByteCount != state.FreePageCount * PageSize || state.AllocatedByteCount != state.AllocatedPageCount * PageSize || state.FreePageCount < (ulong)state.FreeNodeCount)
            return ManagedHeapPageRangeStatus.InvalidAllocator;
        ulong visited = 0;
        ulong pages = 0;
        ulong previousEnd = parentStart;
        int index = state.HeadNodeIndex;
        while (index != -1)
        {
            if (index < 0 || (ulong)index >= nodeCount || visited >= nodeCount)
                return ManagedHeapPageRangeStatus.InvalidAllocator;
            BootPageFreeListNode node = nodes[index];
            if (node.SourceFreeSegmentIndex < 0 || node.SourceUsableRangeIndex < 0 || node.PhysicalStart < previousEnd || (node.PhysicalStart & (PageSize - 1)) != 0 || node.PhysicalEndExclusive <= node.PhysicalStart || node.PhysicalEndExclusive > parentEnd || node.PageCount == 0 || node.PageCount > parentPages - pages || node.ByteLength != node.PageCount * PageSize || node.PhysicalEndExclusive - node.PhysicalStart != node.ByteLength)
                return ManagedHeapPageRangeStatus.InvalidAllocator;
            if (retainedStart != 0 && node.PhysicalStart < retainedEnd && retainedStart < node.PhysicalEndExclusive)
                return ManagedHeapPageRangeStatus.RetainedRangeIsFree;
            previousEnd = node.PhysicalEndExclusive;
            pages += node.PageCount;
            visited++;
            index = node.NextNodeIndex;
        }

        if (visited != (ulong)state.FreeNodeCount || pages != state.FreePageCount)
            return ManagedHeapPageRangeStatus.InvalidAllocator;
        return ManagedHeapPageRangeStatus.Success;
    }

    private static bool IsPageWithinParent(ulong start, ulong end, ulong parentStart, ulong parentEnd) => start >= parentStart && (start & (PageSize - 1)) == 0 && end > start && end <= parentEnd && end - start == PageSize;
    // Both initialization and later observation call this actual reader. It has
    // no knowledge of the preceding stores and never stops on the first nonzero byte.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ReadFixedPage(byte* page, out ulong readBytes)
    {
        byte combined = 0;
        readBytes = 0;
        for (ulong i = 0; i < PageSize; i++)
        {
            combined |= page[i];
            readBytes++;
        }

        return combined == 0;
    }

    private static ManagedHeapPageRangeResult Observe(ManagedHeapPageRangeStatus status, ManagedHeapPageRangeState* heap, BootPageAllocatorState current, ulong mappedStart = 0, ulong readBytes = 0, bool zero = false) => new ManagedHeapPageRangeResult(status, heap->LifecycleValue, heap->PhysicalStartValue, heap->PhysicalEndValue, mappedStart, heap->BeforeStateValue, current, readBytes, zero);
}
