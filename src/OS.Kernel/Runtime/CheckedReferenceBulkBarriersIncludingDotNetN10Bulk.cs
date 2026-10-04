namespace OS.Kernel.Runtime;

/// <summary>A caller obligation, not proof of mapping, exclusivity or GC support.</summary>
public enum CheckedReferenceBulkProfile : byte
{
    Uninitialized = 0,
    NonCollectingExclusiveRetainedPage = 1
}

public enum CheckedReferenceBulkStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    HeapAuthorityRejected = 2,
    UnsupportedProfile = 3,
    InvalidAlignment = 4,
    InvalidLength = 5,
    InvalidRange = 6
}

public readonly struct CheckedReferenceBulkResult
{
    internal CheckedReferenceBulkResult(CheckedReferenceBulkStatus status,
        ManagedHeapPageRangeStatus heapStatus = default, ulong bytesWritten = 0)
    {
        Status = status;
        HeapStatus = heapStatus;
        BytesWritten = bytesWritten;
    }

    public CheckedReferenceBulkStatus Status { get; }
    public ManagedHeapPageRangeStatus HeapStatus { get; }
    public ulong BytesWritten { get; }
    public bool Succeeded => Status == CheckedReferenceBulkStatus.Success;
}

/// <summary>
/// Host-testable noncollecting slot stores and same-page bulk moves. Payload words
/// are opaque bits, never dereferenced or authenticated as CLR references. This
/// does not implement runtime helper exports, object/field authority, GC polling,
/// remembered sets, publication fences or concurrent access. The caller retains
/// mapping/lifetime/exclusive-access obligations for all original authority spans.
/// </summary>
public static unsafe class CheckedReferenceBulkBarriersIncludingDotNetN10Bulk
{
    private const ulong PageBytes = 4096;

    /// <summary>Validate authority, profile, alignment, then range; write one word.</summary>
    public static CheckedReferenceBulkResult TryStore(RhpNewFastHeapAccess access,
        CheckedReferenceBulkProfile profile, ulong destinationOffset, ulong referenceBits)
    {
        ManagedHeapPageRangeResult heap = Retained(access);
        if (!heap.IsReady)
            return new(CheckedReferenceBulkStatus.HeapAuthorityRejected, heap.Status);
        if (profile != CheckedReferenceBulkProfile.NonCollectingExclusiveRetainedPage)
            return new(CheckedReferenceBulkStatus.UnsupportedProfile);
        if ((destinationOffset & 7) != 0)
            return new(CheckedReferenceBulkStatus.InvalidAlignment);
        if (!WithinPage(destinationOffset, 8))
            return new(CheckedReferenceBulkStatus.InvalidRange);

        *(ulong*)(heap.MappedStart + destinationOffset) = referenceBits;
        return new(CheckedReferenceBulkStatus.Success, heap.Status, 8);
    }

    /// <summary>
    /// Validate authority, profile, offsets' alignment, length alignment, then both
    /// full spans. A validated zero-length or exact-self move writes no bytes.
    /// Retained validation reads the entire page even for these no-op requests.
    /// Other successful moves write byteCount bytes in aligned eight-byte units.
    /// </summary>
    public static CheckedReferenceBulkResult TryMove(RhpNewFastHeapAccess access,
        CheckedReferenceBulkProfile profile, ulong sourceOffset, ulong destinationOffset,
        ulong byteCount)
    {
        ManagedHeapPageRangeResult heap = Retained(access);
        if (!heap.IsReady)
            return new(CheckedReferenceBulkStatus.HeapAuthorityRejected, heap.Status);
        if (profile != CheckedReferenceBulkProfile.NonCollectingExclusiveRetainedPage)
            return new(CheckedReferenceBulkStatus.UnsupportedProfile);
        if (((sourceOffset | destinationOffset) & 7) != 0)
            return new(CheckedReferenceBulkStatus.InvalidAlignment);
        if ((byteCount & 7) != 0)
            return new(CheckedReferenceBulkStatus.InvalidLength);
        if (!WithinPage(sourceOffset, byteCount) || !WithinPage(destinationOffset, byteCount))
            return new(CheckedReferenceBulkStatus.InvalidRange);
        if (byteCount == 0 || sourceOffset == destinationOffset)
            return new(CheckedReferenceBulkStatus.Success, heap.Status);

        ulong* source = (ulong*)(heap.MappedStart + sourceOffset);
        ulong* destination = (ulong*)(heap.MappedStart + destinationOffset);
        ulong words = byteCount / 8;
        if (destinationOffset > sourceOffset && destinationOffset - sourceOffset < byteCount)
        {
            for (ulong i = words; i != 0; i--) destination[i - 1] = source[i - 1];
        }
        else
        {
            for (ulong i = 0; i < words; i++) destination[i] = source[i];
        }
        return new(CheckedReferenceBulkStatus.Success, heap.Status, byteCount);
    }

    // Subtraction after the first bound prevents full-width addition wrapping.
    private static bool WithinPage(ulong offset, ulong count) =>
        offset <= PageBytes && count <= PageBytes - offset;

    private static ManagedHeapPageRangeResult Retained(RhpNewFastHeapAccess a) =>
        ManagedHeapPageRangeLiveAllocator.ValidateRetained(a.Allocator, a.AllocatorCapacity,
            a.Nodes, a.NodeCount, a.NodeCapacity, a.Reservations, a.ReservationCount,
            a.ReservationCapacity, a.ParentIndex, a.Backing, a.BackingCapacity, a.Heap, a.HeapCapacity);
}
