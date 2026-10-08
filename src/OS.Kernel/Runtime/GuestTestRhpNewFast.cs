using OS.Kernel.Memory;

namespace OS.Kernel.Runtime;

/// <summary>The thirteen original ValidateRetained arguments, not copied heap authority.</summary>
public unsafe struct RhpNewFastHeapAccess
{
    public BootPageAllocatorState* Allocator;
    public ulong AllocatorCapacity;
    public BootPageFreeListNode* Nodes;
    public ulong NodeCount;
    public ulong NodeCapacity;
    public BootReservedRange* Reservations;
    public ulong ReservationCount;
    public ulong ReservationCapacity;
    public ulong ParentIndex;
    public byte* Backing;
    public ulong BackingCapacity;
    public ManagedHeapPageRangeState* Heap;
    public ulong HeapCapacity;
}

/// <summary>
/// One stable, initially zeroed controller for one retained page. Collecting mode
/// uses additional caller storage; the original 80-byte prefix stays fixed.
/// Self-binding detects accidental copies, not forged PODs,
/// external resets, ABA, duplicate controllers or concurrent modification.
/// </summary>
public struct RhpNewFastBumpState
{
    internal ulong InitializedValue;
    internal ulong SelfAddressValue;
    internal ulong CapacityValue;
    internal ulong HeapAddressValue;
    internal ulong HeapCapacityValue;
    internal ulong MappedStartValue;
    internal ulong PhysicalStartValue;
    internal ulong PhysicalEndValue;
    internal ulong UsedValue;
    internal ulong CountValue;

    public bool IsInitialized => InitializedValue == 1 || InitializedValue == 2;
    public bool IsCollecting => InitializedValue == 2;
    public ulong SelfAddress => SelfAddressValue;
    public ulong HeapAddress => HeapAddressValue;
    public ulong MappedStart => MappedStartValue;
    public ulong PhysicalStart => PhysicalStartValue;
    public ulong PhysicalEndExclusive => PhysicalEndValue;
    public ulong UsedBytes => UsedValue;
    public ulong AllocationCount => CountValue;
}

public enum RhpNewFastStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidStorage = 2,
    StorageOverlap = 3,
    AlreadyInitialized = 4,
    NotInitialized = 5,
    InvalidState = 6,
    HeapAuthorityRejected = 7,
    HeapBindingMismatch = 8,
    InitialPageNotPristine = 9,
    UnsupportedProfile = 10,
    InvalidMethodTableToken = 11,
    InvalidMethodTableFlags = 12,
    UnsupportedFixedType = 13,
    InvalidBaseSize = 14,
    Exhausted = 15,
    UnsupportedArrayType = 16,
    InvalidComponentSize = 17,
    InvalidLength = 18,
    UnsupportedStringType = 19,
    UnsupportedStaticType = 20,
    InvalidReceipt = 21,
    ReceiptContentsChanged = 22,
    CollectionNotEnabled = 23,
    InvalidAllocationLedger = 24,
    AllocationIdentityExhausted = 25
}

public enum RhpNewFastOperation : byte
{
    Uninitialized = 0,
    Bind = 1,
    Allocation = 2
}

/// <summary>Scalar observations only; ObjectReference is never a host CLR object.</summary>
public readonly struct RhpNewFastResult
{
    internal RhpNewFastResult(RhpNewFastStatus status, RhpNewFastOperation operation,
        ManagedHeapPageRangeStatus heapStatus = default, ulong mappedStart = 0,
        ulong mappedEnd = 0, ulong physicalStart = 0, ulong physicalEnd = 0,
        ulong objectReference = 0, ulong methodTableToken = 0, ulong bytesConsumed = 0,
        ulong usedBefore = 0, ulong usedAfter = 0, ulong allocationCount = 0)
    {
        Status = status;
        Operation = operation;
        HeapStatus = heapStatus;
        MappedAllocationStart = mappedStart;
        MappedAllocationEndExclusive = mappedEnd;
        PhysicalAllocationStart = physicalStart;
        PhysicalAllocationEndExclusive = physicalEnd;
        ObjectReference = objectReference;
        MethodTableToken = methodTableToken;
        BytesConsumed = bytesConsumed;
        UsedBefore = usedBefore;
        UsedAfter = usedAfter;
        AllocationCount = allocationCount;
    }

    public RhpNewFastStatus Status { get; }
    public RhpNewFastOperation Operation { get; }
    public ManagedHeapPageRangeStatus HeapStatus { get; }
    public ulong MappedAllocationStart { get; }
    public ulong MappedAllocationEndExclusive { get; }
    public ulong PhysicalAllocationStart { get; }
    public ulong PhysicalAllocationEndExclusive { get; }
    public ulong ObjectReference { get; }
    public ulong MethodTableToken { get; }
    public ulong BytesConsumed { get; }
    public ulong UsedBefore { get; }
    public ulong UsedAfter { get; }
    public ulong AllocationCount { get; }
    public bool Succeeded => Status == RhpNewFastStatus.Success;
    public bool AllocationSucceeded => Succeeded && Operation == RhpNewFastOperation.Allocation && BytesConsumed != 0;
}

/// <summary>
/// Bounded host-testable fixed, SZ-array and String bytes on a caller-mapped retained
/// page, not a runtime export, compiled-new proof, collector, refill or OOM.
/// Caller storage must remain readable/writable, exclusive and stable; the caller
/// owns one controller and the real mapping/lifetime. Numeric spans cannot prove
/// those properties. MT prefix/token association is also a caller obligation.
/// </summary>
public static unsafe class GuestTestRhpNewFast
{
    public static ulong RequiredStateBytes => (ulong)sizeof(RhpNewFastBumpState);
    private const ulong PageBytes = 4096;
    public const uint MaximumTrackedAllocations = 170; // floor(4096 / minimum object 24)
    public static ulong RequiredCollectingStateBytes => RequiredStateBytes +
        MaximumTrackedAllocations * (ulong)sizeof(AllocationRecord);

    private struct AllocationRecord
    {
        public ulong Offset;
        public uint Bytes;
        public uint StaticBase;
        public ulong MethodTable;
        public ulong Sequence;
    }

    /// <summary>
    /// One-way opt-in before the first allocation. Records occupy the additional
    /// zeroed owner storage, not the retained heap. Caller retains exclusive access
    /// and stable lifetime for the complete capacity. No reset/adoption is supported.
    /// </summary>
    public static RhpNewFastStatus EnableCollection(RhpNewFastHeapAccess access,
        RhpNewFastBumpState* state, ulong stateCapacity)
    {
        RhpNewFastStatus status = ValidateController(access, state, stateCapacity, out _);
        if (status != RhpNewFastStatus.Success) return status;
        if (state->InitializedValue != 1 || state->UsedValue != 0 || state->CountValue != 0)
            return RhpNewFastStatus.AlreadyInitialized;
        if (stateCapacity < RequiredCollectingStateBytes) return RhpNewFastStatus.InvalidStorage;
        for (ulong i = RequiredStateBytes; i < RequiredCollectingStateBytes; i++)
            if (((byte*)state)[i] != 0) return RhpNewFastStatus.InvalidAllocationLedger;
        state->InitializedValue = 2;
        return RhpNewFastStatus.Success;
    }

    public static RhpNewFastResult InitializeOnce(RhpNewFastHeapAccess access,
        RhpNewFastBumpState* state, ulong stateCapacity)
    {
        RhpNewFastStatus status = ValidateStorage(access, state, stateCapacity);
        if (status != RhpNewFastStatus.Success) return Fail(status, RhpNewFastOperation.Bind);
        // The complete prefix is initially zero, including any compiler padding.
        for (ulong i = 0; i < RequiredStateBytes; i++)
            if (((byte*)state)[i] != 0)
                return Fail(RhpNewFastStatus.AlreadyInitialized, RhpNewFastOperation.Bind);

        ManagedHeapPageRangeResult heap = Retained(access);
        if (!heap.IsReady)
            return Fail(RhpNewFastStatus.HeapAuthorityRejected, RhpNewFastOperation.Bind, heap.Status);
        if (!CoherentPage(heap))
            return Fail(RhpNewFastStatus.HeapBindingMismatch, RhpNewFastOperation.Bind, heap.Status);
        if (!heap.ContentsAreZeroNow)
            return Fail(RhpNewFastStatus.InitialPageNotPristine, RhpNewFastOperation.Bind, heap.Status);

        *state = new RhpNewFastBumpState
        {
            InitializedValue = 1, SelfAddressValue = (ulong)state, CapacityValue = stateCapacity,
            HeapAddressValue = (ulong)access.Heap, HeapCapacityValue = access.HeapCapacity,
            MappedStartValue = heap.MappedStart, PhysicalStartValue = heap.PhysicalStart,
            PhysicalEndValue = heap.PhysicalEndExclusive, UsedValue = 0, CountValue = 0
        };
        return new RhpNewFastResult(RhpNewFastStatus.Success, RhpNewFastOperation.Bind, heap.Status);
    }

    public static RhpNewFastResult TryAllocate(RhpNewFastHeapAccess access,
        RhpNewFastBumpState* state, ulong stateCapacity, NativeAotLayoutProfile profile,
        NativeAotMethodTablePrefix prefix, ulong methodTableToken) =>
        AllocateCore(access, state, stateCapacity, profile, prefix, methodTableToken, NativeAotLayoutShape.FixedObject, 0);

    /// <summary>
    /// Explicit abbreviated-MT route. The caller authenticates the real metadata;
    /// only the numeric 16-byte token span is admitted here, never dereferenced.
    /// Shares the existing full authority check and sole allocation commit.
    /// </summary>
    public static RhpNewFastResult TryAllocateStaticBase(RhpNewFastHeapAccess access,
        RhpNewFastBumpState* state, ulong stateCapacity, NativeAotLayoutProfile profile,
        uint flags, uint baseSize, ulong methodTableToken) =>
        AllocateCore(access, state, stateCapacity, profile,
            new NativeAotMethodTablePrefix(flags, baseSize, 0, 0, 0, 0), methodTableToken,
            NativeAotLayoutShape.FixedObject, 0, true);

    /// <summary>
    /// Read-only containment/readback of a genuine retained original receipt.
    /// Collecting mode additionally requires the current live record and allocation
    /// identity; swept/replaced receipts fail even if the address and MT are reused.
    /// Not a forgery detector: caller retains exclusive stable lifetime and receipt/
    /// metadata provenance. Later allocations are legal.
    /// Payload changes are legal; the retained syncblock and MT slot must match.
    /// </summary>
    public static RhpNewFastStatus TryValidateRetainedAllocation(RhpNewFastHeapAccess access,
        RhpNewFastBumpState* state, ulong stateCapacity, RhpNewFastResult receipt)
    {
        RhpNewFastStatus status = ValidateController(access, state, stateCapacity, out ManagedHeapPageRangeResult heap);
        if (status != RhpNewFastStatus.Success) return status;
        ulong before = receipt.UsedBefore, size = receipt.BytesConsumed, count = receipt.AllocationCount;
        if (!receipt.AllocationSucceeded || receipt.HeapStatus != ManagedHeapPageRangeStatus.Success ||
            size < 24 || size > PageBytes || (size & 7) != 0 || (before & 7) != 0 ||
            before > state->UsedValue || size > state->UsedValue - before ||
            receipt.UsedAfter != before + size || count == 0 || count > state->CountValue ||
            (state->InitializedValue == 1 && ((before == 0) != (count == 1) || count - 1 > before / 24 ||
            (receipt.UsedAfter == state->UsedValue) != (count == state->CountValue) ||
            state->CountValue - count > (state->UsedValue - receipt.UsedAfter) / 24)) ||
            receipt.MappedAllocationStart != heap.MappedStart + before ||
            receipt.MappedAllocationEndExclusive != heap.MappedStart + before + size ||
            receipt.PhysicalAllocationStart != heap.PhysicalStart + before ||
            receipt.PhysicalAllocationEndExclusive != heap.PhysicalStart + before + size ||
            receipt.ObjectReference != heap.MappedStart + before + 8 ||
            !ValidToken(access, state, stateCapacity, receipt.MethodTableToken, 16))
            return RhpNewFastStatus.InvalidReceipt;
        if (state->InitializedValue == 2)
        {
            bool found = false;
            AllocationRecord* records = Records(state);
            for (uint i = 0; i < MaximumTrackedAllocations; i++)
                if (records[i].Sequence == count && records[i].Offset == before &&
                    records[i].Bytes == size && records[i].MethodTable == receipt.MethodTableToken)
                { found = true; break; }
            if (!found) return RhpNewFastStatus.InvalidReceipt;
        }
        // Only now are all reads known to be inside the current retained page.
        byte* bytes = (byte*)receipt.MappedAllocationStart;
        for (int i = 0; i < 8; i++)
            if (bytes[i] != 0 || bytes[8 + i] != (byte)(receipt.MethodTableToken >> (i * 8)))
                return RhpNewFastStatus.ReceiptContentsChanged;
        return RhpNewFastStatus.Success;
    }

    internal static RhpNewFastResult TryAllocateArray(RhpNewFastHeapAccess access,
        RhpNewFastBumpState* state, ulong stateCapacity, NativeAotLayoutProfile profile,
        NativeAotMethodTablePrefix prefix, ulong methodTableToken, int length) =>
        AllocateCore(access, state, stateCapacity, profile, prefix, methodTableToken, NativeAotLayoutShape.SzArray, length);

    internal static RhpNewFastResult TryAllocateString(RhpNewFastHeapAccess access,
        RhpNewFastBumpState* state, ulong stateCapacity, NativeAotLayoutProfile profile,
        NativeAotMethodTablePrefix prefix, ulong methodTableToken, long length) =>
        AllocateCore(access, state, stateCapacity, profile, prefix, methodTableToken, NativeAotLayoutShape.String, length);

    // All routes share authority validation and the sole allocation/cursor commit.
    // The core never depends on the public array/String wrappers or their files.
    private static RhpNewFastResult AllocateCore(RhpNewFastHeapAccess access,
        RhpNewFastBumpState* state, ulong stateCapacity, NativeAotLayoutProfile profile,
        NativeAotMethodTablePrefix prefix, ulong methodTableToken, NativeAotLayoutShape shape, long length, bool staticBase = false)
    {
        RhpNewFastStatus status = ValidateController(access, state, stateCapacity, out ManagedHeapPageRangeResult heap);
        if (status != RhpNewFastStatus.Success) return Fail(status, heapStatus: heap.Status);

        // Existing objects can make ContentsAreZeroNow false without losing ownership.
        if (profile != NativeAotLayoutProfile.Ilc10_0_12WinX64LittleEndian)
            return Fail(RhpNewFastStatus.UnsupportedProfile);
        if (!ValidToken(access, state, stateCapacity, methodTableToken, staticBase ? 16UL : 24UL))
            return Fail(RhpNewFastStatus.InvalidMethodTableToken);
        ulong size;
        if (staticBase)
        {
            size = prefix.BaseSize;
            status = prefix.Flags != 0x01000000 ? RhpNewFastStatus.UnsupportedStaticType
                : size < 24 || (size & 7) != 0 ? RhpNewFastStatus.InvalidBaseSize
                : size > PageBytes ? RhpNewFastStatus.UnsupportedStaticType : RhpNewFastStatus.Success;
        }
        else if (shape == NativeAotLayoutShape.SzArray)
            status = AdmitArrayType(profile, prefix, (int)length, out size);
        else if (shape == NativeAotLayoutShape.String)
            status = AdmitStringType(profile, prefix, length, out size);
        else
        {
            status = AdmitFixedType(prefix);
            size = prefix.BaseSize;
        }
        if (status != RhpNewFastStatus.Success) return Fail(status);
        bool collecting = state->InitializedValue == 2;
        if (collecting && shape != NativeAotLayoutShape.FixedObject)
            return Fail(RhpNewFastStatus.UnsupportedFixedType);
        if (state->CountValue == ~0UL) return Fail(RhpNewFastStatus.AllocationIdentityExhausted);
        ulong before = state->UsedValue;
        uint recordIndex = MaximumTrackedAllocations;
        if (collecting)
        {
            AllocationRecord* records = Records(state);
            for (uint i = 0; i < MaximumTrackedAllocations; i++)
                if (records[i].Sequence == 0) { recordIndex = i; break; }
            if (recordIndex == MaximumTrackedAllocations) return Fail(RhpNewFastStatus.Exhausted);
            // First fit across unsorted live records, bounded by their count.
            before = 0;
            for (uint pass = 0; pass <= MaximumTrackedAllocations; pass++)
            {
                bool moved = false;
                for (uint i = 0; i < MaximumTrackedAllocations; i++)
                    if (records[i].Sequence != 0 && Overlaps(before, size, records[i].Offset, records[i].Bytes))
                    { before = records[i].Offset + records[i].Bytes; moved = true; break; }
                if (!moved) break;
            }
        }
        if (before > PageBytes || size > PageBytes - before) return Fail(RhpNewFastStatus.Exhausted);

        // Full page and cursor/size bounds above prove all following sums fit.
        ulong start = heap.MappedStart + before;
        ulong physical = heap.PhysicalStart + before;
        ulong after = before + size;
        ulong count = state->CountValue + 1;
        byte* bytes = (byte*)start;
        for (ulong i = 0; i < size; i++) bytes[i] = 0;
        // Explicit x64 little-endian MT slot at reference origin B+8. Never dereference MT.
        for (int i = 0; i < 8; i++) bytes[8 + i] = (byte)(methodTableToken >> (i * 8));
        if (shape != NativeAotLayoutShape.FixedObject)
            for (int i = 0; i < 4; i++) bytes[16 + i] = (byte)((uint)length >> (i * 8));
        if (collecting)
            Records(state)[recordIndex] = new AllocationRecord
            { Offset = before, Bytes = (uint)size, StaticBase = staticBase ? 1U : 0,
              MethodTable = methodTableToken, Sequence = count };
        // High-water observation never rewinds; identity count never reuses a value.
        if (after > state->UsedValue) state->UsedValue = after;
        state->CountValue = count;
        return new RhpNewFastResult(RhpNewFastStatus.Success, RhpNewFastOperation.Allocation,
            heap.Status, start, start + size, physical, physical + size, start + 8,
            methodTableToken, size, before, after, count);
    }

    private static RhpNewFastStatus ValidateController(RhpNewFastHeapAccess access,
        RhpNewFastBumpState* state, ulong stateCapacity, out ManagedHeapPageRangeResult heap)
    {
        heap = default;
        RhpNewFastStatus status = ValidateStorage(access, state, stateCapacity);
        if (status != RhpNewFastStatus.Success) return status;
        if (state->InitializedValue == 0) return RhpNewFastStatus.NotInitialized;
        if ((state->InitializedValue != 1 && state->InitializedValue != 2) || state->SelfAddressValue != (ulong)state ||
            state->CapacityValue != stateCapacity || state->UsedValue > PageBytes ||
            (state->UsedValue & 7) != 0 || (state->UsedValue == 0) != (state->CountValue == 0) ||
            (state->InitializedValue == 1 && state->CountValue > state->UsedValue / 24) ||
            (state->InitializedValue == 2 && stateCapacity < RequiredCollectingStateBytes))
            return RhpNewFastStatus.InvalidState;

        heap = Retained(access);
        if (!heap.IsReady) return RhpNewFastStatus.HeapAuthorityRejected;
        if (!CoherentPage(heap) || state->HeapAddressValue != (ulong)access.Heap ||
            state->HeapCapacityValue != access.HeapCapacity || state->MappedStartValue != heap.MappedStart ||
            state->PhysicalStartValue != heap.PhysicalStart || state->PhysicalEndValue != heap.PhysicalEndExclusive)
            return RhpNewFastStatus.HeapBindingMismatch;

        return state->InitializedValue == 2 ? ValidateLedger(access, state, stateCapacity)
            : RhpNewFastStatus.Success;
    }

    private static AllocationRecord* Records(RhpNewFastBumpState* state) =>
        (AllocationRecord*)((byte*)state + RequiredStateBytes);

    private static RhpNewFastStatus ValidateLedger(RhpNewFastHeapAccess access,
        RhpNewFastBumpState* state, ulong capacity)
    {
        AllocationRecord* records = Records(state);
        for (uint i = 0; i < MaximumTrackedAllocations; i++)
        {
            AllocationRecord r = records[i];
            if (r.Sequence == 0)
            {
                if (r.Offset != 0 || r.Bytes != 0 || r.StaticBase != 0 || r.MethodTable != 0)
                    return RhpNewFastStatus.InvalidAllocationLedger;
                continue;
            }
            if (r.Sequence > state->CountValue || r.Bytes < 24 || (r.Bytes & 7) != 0 ||
                (r.Offset & 7) != 0 || r.Offset > state->UsedValue || r.Bytes > state->UsedValue - r.Offset ||
                r.StaticBase > 1 || !ValidToken(access, state, capacity, r.MethodTable, r.StaticBase == 1 ? 16UL : 24UL))
                return RhpNewFastStatus.InvalidAllocationLedger;
            for (uint j = 0; j < i; j++)
                if (records[j].Sequence != 0 && (records[j].Sequence == r.Sequence ||
                    Overlaps(r.Offset, r.Bytes, records[j].Offset, records[j].Bytes)))
                    return RhpNewFastStatus.InvalidAllocationLedger;
            byte* bytes = (byte*)(state->MappedStartValue + r.Offset);
            for (int j = 0; j < 8; j++)
                if (bytes[j] != 0 || bytes[8 + j] != (byte)(r.MethodTable >> (j * 8)))
                    return RhpNewFastStatus.ReceiptContentsChanged;
        }
        return RhpNewFastStatus.Success;
    }

    internal static RhpNewFastStatus ValidateCollectingOwner(RhpNewFastHeapAccess access,
        RhpNewFastBumpState* state, ulong capacity)
    {
        RhpNewFastStatus status = ValidateController(access, state, capacity, out _);
        return status != RhpNewFastStatus.Success ? status : state->InitializedValue == 2
            ? RhpNewFastStatus.Success : RhpNewFastStatus.CollectionNotEnabled;
    }

    // Only called inside a validated, exclusive collection operation. The record
    // remains private to the original allocator; consumers receive scalar receipts.
    internal static RhpNewFastResult ReadTrackedAllocation(RhpNewFastBumpState* state,
        uint index, out bool staticBase)
    {
        AllocationRecord r = Records(state)[index];
        staticBase = r.StaticBase == 1;
        if (r.Sequence == 0) return default;
        ulong start = state->MappedStartValue + r.Offset, physical = state->PhysicalStartValue + r.Offset;
        return new(RhpNewFastStatus.Success, RhpNewFastOperation.Allocation,
            ManagedHeapPageRangeStatus.Success, start, start + r.Bytes, physical, physical + r.Bytes,
            start + 8, r.MethodTable, r.Bytes, r.Offset, r.Offset + r.Bytes, r.Sequence);
    }

    // Infallible commit after the collector validates the complete graph. No
    // callbacks, metadata reads or status-producing operations after the first write.
    internal static void CommitSweep(RhpNewFastBumpState* state, ulong* live,
        out uint reclaimedObjects, out ulong reclaimedBytes)
    {
        reclaimedObjects = 0; reclaimedBytes = 0;
        AllocationRecord* records = Records(state);
        for (uint i = 0; i < MaximumTrackedAllocations; i++)
            if (records[i].Sequence != 0 && (live[i / 64] & (1UL << (int)(i % 64))) == 0)
            {
                byte* start = (byte*)(state->MappedStartValue + records[i].Offset);
                for (uint j = 0; j < records[i].Bytes; j++) start[j] = 0;
                reclaimedObjects++; reclaimedBytes += records[i].Bytes;
                records[i] = default;
            }
    }

    private static RhpNewFastStatus ValidateStorage(RhpNewFastHeapAccess access,
        RhpNewFastBumpState* state, ulong capacity)
    {
        if (sizeof(void*) != 8 || sizeof(BootPageAllocatorState) != 56 ||
            sizeof(BootPageFreeListNode) != 48 || sizeof(BootReservedRange) != 40 ||
            capacity < RequiredStateBytes || access.AllocatorCapacity != 1 ||
            access.NodeCount == 0 || access.NodeCount > BootPageAllocatorMutator.MaximumNodeCount ||
            access.NodeCapacity < access.NodeCount || access.NodeCapacity > ~0UL / 48 ||
            access.ReservationCount == 0 || access.ReservationCount > BootReservationTableValidator.MaximumReservationCount ||
            access.ReservationCapacity < access.ReservationCount || access.ReservationCapacity > ~0UL / 40 ||
            access.ParentIndex >= access.ReservationCount || access.BackingCapacity < PageBytes ||
            access.HeapCapacity < ManagedHeapPageRangeLiveAllocator.RequiredStateBytes)
            return RhpNewFastStatus.InvalidStorage;

        ulong* starts = stackalloc ulong[6] { (ulong)access.Allocator, (ulong)access.Nodes,
            (ulong)access.Reservations, (ulong)access.Heap, (ulong)access.Backing, (ulong)state };
        ulong* lengths = stackalloc ulong[6] { 56, access.NodeCapacity * 48,
            access.ReservationCapacity * 40, access.HeapCapacity, access.BackingCapacity, capacity };
        for (int i = 0; i < 6; i++)
            if (starts[i] == 0 || (starts[i] & (i == 4 ? 4095UL : 7UL)) != 0 ||
                starts[i] > ~0UL - lengths[i])
                return RhpNewFastStatus.InvalidStorage;
        for (int i = 0; i < 6; i++)
            for (int j = i + 1; j < 6; j++)
                if (Overlaps(starts[i], lengths[i], starts[j], lengths[j]))
                    return RhpNewFastStatus.StorageOverlap;
        return RhpNewFastStatus.Success;
    }

    private static ManagedHeapPageRangeResult Retained(RhpNewFastHeapAccess a) =>
        ManagedHeapPageRangeLiveAllocator.ValidateRetained(a.Allocator, a.AllocatorCapacity,
            a.Nodes, a.NodeCount, a.NodeCapacity, a.Reservations, a.ReservationCount,
            a.ReservationCapacity, a.ParentIndex, a.Backing, a.BackingCapacity, a.Heap, a.HeapCapacity);

    private static bool CoherentPage(ManagedHeapPageRangeResult heap) =>
        heap.ReadBytes == PageBytes && heap.PhysicalStart != 0 &&
        (heap.PhysicalStart & 4095) == 0 && heap.PhysicalStart <= ~0UL - PageBytes &&
        heap.PhysicalEndExclusive == heap.PhysicalStart + PageBytes && heap.MappedStart != 0 &&
        (heap.MappedStart & 4095) == 0 && heap.MappedStart <= ~0UL - PageBytes;

    private static bool ValidToken(RhpNewFastHeapAccess a, RhpNewFastBumpState* state,
        ulong stateCapacity, ulong token, ulong tokenBytes)
    {
        // Numeric disjointness only: no mapped/readable/authentic metadata claim.
        if (token == 0 || (token & 7) != 0 || token > ~0UL - tokenBytes) return false;
        return !Overlaps(token, tokenBytes, (ulong)a.Allocator, 56) &&
            !Overlaps(token, tokenBytes, (ulong)a.Nodes, a.NodeCapacity * 48) &&
            !Overlaps(token, tokenBytes, (ulong)a.Reservations, a.ReservationCapacity * 40) &&
            !Overlaps(token, tokenBytes, (ulong)a.Heap, a.HeapCapacity) &&
            !Overlaps(token, tokenBytes, (ulong)a.Backing, a.BackingCapacity) &&
            !Overlaps(token, tokenBytes, (ulong)state, stateCapacity);
    }

    // Pinned dotnet/dotnet 95017c711e6afc1085133d440e42b4bd78155701:
    // MethodTable.Constants.cs / EETypeBuilderHelpers.cs define these scalar flags;
    // RuntimeExports.RhBox/RhNewObject also route fixed value types to RhpNewFast.
    // Admission does not prove actual compiler metadata, generic identity or field layout.
    private static RhpNewFastStatus AdmitFixedType(NativeAotMethodTablePrefix prefix)
    {
        uint flags = prefix.Flags;
        uint low = flags & 0xFFFF;
        if ((flags & 0x00200000) != 0 || (low & ~0x17FFU) != 0)
            return RhpNewFastStatus.InvalidMethodTableFlags;
        uint element = (flags >> 26) & 31;
        bool value = (element >= 2 && element <= 16) || element == 18;
        if ((flags & 0x80030000) != 0 || (!value && element != 20))
            return RhpNewFastStatus.UnsupportedFixedType;
        if ((!value && (low & 0x00E0) != 0) || (element != 18 && (low & 0x0700) != 0))
            return RhpNewFastStatus.InvalidMethodTableFlags;
        // Finalization, dynamic authority, byref-like and special alignment stay unsupported.
        // HasPointers, IsGeneric and inert dispatch/variance/interface bits remain admitted.
        if ((flags & 0x00180000) != 0 || (low & 0x1017) != 0)
            return RhpNewFastStatus.UnsupportedFixedType;
        if (prefix.BaseSize < 24 || (prefix.BaseSize & 7) != 0)
            return RhpNewFastStatus.InvalidBaseSize;
        return RhpNewFastStatus.Success;
    }

    private static RhpNewFastStatus AdmitArrayType(NativeAotLayoutProfile profile,
        NativeAotMethodTablePrefix prefix, int length, out ulong size)
    {
        size = 0;
        NativeAotLayoutResult layout = ObjectArrayStringEETypeMethodTableLayoutContracts.Evaluate(
            profile, prefix, length, ~0UL);
        // Preserve evaluator precedence even for malformed non-array descriptions.
        if (!layout.Succeeded)
        {
            switch (layout.Status)
            {
                case NativeAotLayoutStatus.InvalidFlags: return RhpNewFastStatus.InvalidMethodTableFlags;
                case NativeAotLayoutStatus.InvalidBaseSize: return RhpNewFastStatus.InvalidBaseSize;
                case NativeAotLayoutStatus.InvalidComponentSize: return RhpNewFastStatus.InvalidComponentSize;
                case NativeAotLayoutStatus.InvalidLength: return RhpNewFastStatus.InvalidLength;
                case NativeAotLayoutStatus.CapacityTooSmall: return RhpNewFastStatus.Exhausted;
                default: return RhpNewFastStatus.UnsupportedArrayType;
            }
        }
        if (layout.Shape != NativeAotLayoutShape.SzArray)
            return RhpNewFastStatus.UnsupportedArrayType;
        ushort component = layout.ComponentSize;
        if ((component != 1 && component != 2 && component != 4 && component != 8) ||
            (layout.ContainsGcPointers && component != 8))
            return RhpNewFastStatus.InvalidComponentSize;
        size = layout.AlignedSizeBytes;
        return RhpNewFastStatus.Success;
    }

    private static RhpNewFastStatus AdmitStringType(NativeAotLayoutProfile profile,
        NativeAotMethodTablePrefix prefix, long length, out ulong size)
    {
        size = 0;
        // Validate the full signed x64 count before narrowing. An invalid count
        // becomes the evaluator's invalid sentinel, preserving its flags/base/
        // component-before-length precedence without truncating the caller value.
        int count = length >= 0 && length <= ObjectArrayStringEETypeMethodTableLayoutContracts.MaximumStringLength
            ? (int)length : -1;
        NativeAotLayoutResult layout = ObjectArrayStringEETypeMethodTableLayoutContracts.Evaluate(
            profile, prefix, count, ~0UL);
        if (!layout.Succeeded)
        {
            switch (layout.Status)
            {
                case NativeAotLayoutStatus.InvalidFlags: return RhpNewFastStatus.InvalidMethodTableFlags;
                case NativeAotLayoutStatus.InvalidBaseSize: return RhpNewFastStatus.InvalidBaseSize;
                case NativeAotLayoutStatus.InvalidComponentSize: return RhpNewFastStatus.InvalidComponentSize;
                case NativeAotLayoutStatus.InvalidLength: return RhpNewFastStatus.InvalidLength;
                case NativeAotLayoutStatus.CapacityTooSmall: return RhpNewFastStatus.Exhausted;
                default: return RhpNewFastStatus.UnsupportedStringType;
            }
        }
        if (layout.Shape != NativeAotLayoutShape.String)
            return RhpNewFastStatus.UnsupportedStringType;
        // The existing String evaluator requires base22/component2/no pointers:
        // align8(22+2*n) includes the zero UTF-16 terminator at B+20+2*n.
        size = layout.AlignedSizeBytes;
        return RhpNewFastStatus.Success;
    }

    private static bool Overlaps(ulong start, ulong length, ulong other, ulong otherLength) =>
        start < other + otherLength && other < start + length;

    private static RhpNewFastResult Fail(RhpNewFastStatus status,
        RhpNewFastOperation operation = RhpNewFastOperation.Allocation,
        ManagedHeapPageRangeStatus heapStatus = default) => new(status, operation, heapStatus);
}
