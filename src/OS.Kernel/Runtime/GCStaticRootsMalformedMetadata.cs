using OS.Boot;
using System.Runtime.CompilerServices;

namespace OS.Kernel.Runtime;

public enum GCStaticMetadataStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    ModuleRejected = 2,
    SectionAbsent = 3,
    SectionEmpty = 4,
    UnsupportedEntryCount = 5,
    CallerBoundsRejected = 6,
    EntryRejected = 7,
    CellRejected = 8,
    ProtectedAlias = 9,
    UnsupportedForm = 10,
    MethodTableRejected = 11,
    RelatedTypeRejected = 12,
    DescriptorRejected = 13
}

/// <summary>
/// Caller-attested readable, stable mapped bytes. This numeric window neither
/// establishes PE permissions nor probes whether memory is actually mapped.
/// </summary>
public readonly struct GCStaticReadableWindow
{
    public GCStaticReadableWindow(ulong start, ulong bytes) { Start = start; Bytes = bytes; }
    public ulong Start { get; }
    public ulong Bytes { get; }
}

/// <summary>
/// An immediate read-only observation, never allocation, handle, root lifetime
/// or publication authority. Section payloads are not frozen by TypeManager.
/// Revalidate all original inputs rather than trusting a retained observation.
/// </summary>
public readonly struct GCStaticMetadataResult
{
    internal GCStaticMetadataResult(GCStaticMetadataStatus status,
        TypeManagerModuleStatus moduleStatus = TypeManagerModuleStatus.Success,
        ulong entry = 0, ulong cell = 0, ulong encodedCell = 0, ulong methodTable = 0,
        ulong relatedType = 0, ulong descriptor = 0, uint flags = 0, uint baseSize = 0,
        long adjustedSize = 0, ulong seriesOffset = 0, ulong seriesBytes = 0)
    {
        Status = status; ModuleStatus = moduleStatus; Entry = entry; Cell = cell;
        EncodedCell = encodedCell; MethodTable = methodTable; RelatedType = relatedType;
        Descriptor = descriptor; Flags = flags; BaseSize = baseSize;
        AdjustedSize = adjustedSize; SeriesOffset = seriesOffset; SeriesBytes = seriesBytes;
    }
    public GCStaticMetadataStatus Status { get; }
    public TypeManagerModuleStatus ModuleStatus { get; }
    public ulong Entry { get; }
    public ulong Cell { get; }
    public ulong EncodedCell { get; }
    public ulong MethodTable { get; }
    public ulong RelatedType { get; }
    public ulong Descriptor { get; }
    public uint Flags { get; }
    public uint BaseSize { get; }
    public long AdjustedSize { get; }
    public ulong SeriesOffset { get; }
    public ulong SeriesBytes { get; }
    public bool Succeeded => Status == GCStaticMetadataStatus.Success;
}

public enum GCStaticRootStatus : byte
{
    Uninitialized = 0, Success = 1, RootStorageRejected = 2, ModuleRejected = 3,
    MetadataRejected = 4, ProtectedAlias = 5, RootNotEmpty = 6, NotPublished = 7,
    RootChanged = 8, HeapRejected = 9, PostCommitRejected = 10
}

/// <summary>Scalar observation, never a CLR reference or a transferable write capability.</summary>
public readonly struct GCStaticRootResult
{
    internal GCStaticRootResult(GCStaticRootStatus status, GCStaticMetadataStatus metadataStatus = default,
        TypeManagerModuleStatus moduleStatus = default, RhpNewFastStatus heapStatus = default,
        ulong objectReference = 0, ulong cell = 0, ulong seriesStart = 0, ulong seriesBytes = 0,
        ulong allocationBytes = 0)
    {
        Status = status; MetadataStatus = metadataStatus; ModuleStatus = moduleStatus; HeapStatus = heapStatus;
        ObjectReference = objectReference; Cell = cell; SeriesStart = seriesStart;
        SeriesBytes = seriesBytes; AllocationBytes = allocationBytes;
    }
    public GCStaticRootStatus Status { get; }
    public GCStaticMetadataStatus MetadataStatus { get; }
    public TypeManagerModuleStatus ModuleStatus { get; }
    public RhpNewFastStatus HeapStatus { get; }
    public ulong ObjectReference { get; }
    public ulong Cell { get; }
    public ulong SeriesStart { get; }
    public ulong SeriesBytes { get; }
    public ulong AllocationBytes { get; }
    public bool Succeeded => Status == GCStaticRootStatus.Success;
}

/// <summary>
/// Pinned .NET 10.0.8 x64/little-endian, one uninitialized GC-static entry and
/// one positive GCDesc series. ValidateOne performs no writes, callbacks,
/// allocations or payload object reads. The image/owner must remain mapped and exclusively
/// stable through the call, under the TypeManager unsafe caller contract.
/// The caller supplies genuine System.Object provenance separately: this code
/// checks only its admitted address and equality with the abbreviated MT field.
/// ValidateOne retains that read-only contract. The explicit materialization
/// methods additionally require registered PE permissions and a stable private
/// root owner. They retain one nonmoving allocation, not an upstream GCHandle,
/// collecting GC, hardware permission proof or genuine compiler association.
/// </summary>
public static unsafe class GCStaticRootsMalformedMetadata
{
    public const uint MinimumBaseSize = 24;
    public const uint MaximumBaseSize = 4096;
    public const ulong AbbreviatedMethodTableBytes = 16;
    public const ulong OneSeriesDescriptorBytes = 24;
    private const uint HasPointers = 0x01000000;

    public static GCStaticMetadataResult ValidateOne(EfiLoadedImageRangeCapturePlan capture,
        M01LoadedImageVirtualMappingEvidence mapping, byte* owner, ulong ownerCapacity,
        ulong moduleHandle, GCStaticReadableWindow metadata, GCStaticReadableWindow cells,
        ulong expectedSystemObjectMethodTable) => DecodeOne(capture, mapping, owner, ownerCapacity,
            moduleHandle, metadata, cells, expectedSystemObjectMethodTable, false, false, 0, 0);

    private static GCStaticMetadataResult DecodeOne(EfiLoadedImageRangeCapturePlan capture,
        M01LoadedImageVirtualMappingEvidence mapping, byte* owner, ulong ownerCapacity,
        ulong moduleHandle, GCStaticReadableWindow metadata, GCStaticReadableWindow cells,
        ulong expectedSystemObjectMethodTable, bool permissions, bool published,
        ulong initialWord, ulong publishedReference)
    {
        TypeManagerModuleResult region = TypeManagerModuleMetadataLiveNativeAOTImage.TryGetSection(
            capture, mapping, owner, ownerCapacity, moduleHandle, 201);
        if (region.Status == TypeManagerModuleStatus.SectionAbsent)
            return new(GCStaticMetadataStatus.SectionAbsent, region.Status);
        if (!region.Succeeded) return new(GCStaticMetadataStatus.ModuleRejected, region.Status);
        if (region.Length == 0) return new(GCStaticMetadataStatus.SectionEmpty);
        if (region.Length < 0 || (region.Length & 3) != 0)
            return new(GCStaticMetadataStatus.EntryRejected);
        if (region.Length != 4) return new(GCStaticMetadataStatus.UnsupportedEntryCount);

        // The second lookup authenticates the same registered owner and supplies
        // the complete protected slot, even though the lookup row reports eight.
        TypeManagerModuleResult slot = TypeManagerModuleMetadataLiveNativeAOTImage.TryGetSection(
            capture, mapping, owner, ownerCapacity, moduleHandle, 204);
        if (!slot.Succeeded) return new(GCStaticMetadataStatus.ModuleRejected, slot.Status);
        if (mapping.VirtualImageBase.TryGetRawValueForUefiBoundary(out ulong image) != AddressValueStatus.Success ||
            !WindowInImage(metadata, image, capture.Range.ImageSize) ||
            !WindowInImage(cells, image, capture.Range.ImageSize) ||
            (expectedSystemObjectMethodTable & 7) != 0 ||
            !Contains(metadata, expectedSystemObjectMethodTable,
                ObjectArrayStringEETypeMethodTableLayoutContracts.MethodTablePrefixBytes))
            return new(GCStaticMetadataStatus.CallerBoundsRejected);

        ulong entry = region.Section;
        if ((entry & 3) != 0 || !Contains(metadata, entry, 4))
            return new(GCStaticMetadataStatus.EntryRejected);
        if (permissions && (!Range(capture, mapping, owner, ownerCapacity, moduleHandle, entry, 4, false) ||
            !Range(capture, mapping, owner, ownerCapacity, moduleHandle, expectedSystemObjectMethodTable, 24, false)))
            return new(GCStaticMetadataStatus.CallerBoundsRejected);
        if (!Relative32(entry, U32((byte*)entry), out ulong cell) ||
            (cell & 7) != 0 || !Contains(cells, cell, 8))
            return new(GCStaticMetadataStatus.CellRejected);
        if (Overlap(cell, 8, (ulong)owner, ownerCapacity) ||
            Overlap(cell, 8, slot.Section, TypeManagerModuleMetadataLiveNativeAOTImage.SlotBytes) ||
            Overlap(cell, 8, entry, 4) ||
            Overlap(cell, 8, expectedSystemObjectMethodTable,
                ObjectArrayStringEETypeMethodTableLayoutContracts.MethodTablePrefixBytes))
            return new(GCStaticMetadataStatus.ProtectedAlias);

        if (permissions && !Range(capture, mapping, owner, ownerCapacity, moduleHandle, cell, 8, true))
            return new(GCStaticMetadataStatus.CellRejected);
        ulong encoded = U64((byte*)cell);
        if (published)
        {
            if (encoded != publishedReference) return new(GCStaticMetadataStatus.CellRejected);
            encoded = initialWord; // Never interpret the absolute published reference as rel32.
        }
        // Initialized cells and preinitialized data require different lifetime /
        // decoding contracts. They are unsupported, not failed initialization.
        if ((encoded & 3) != 1) return new(GCStaticMetadataStatus.UnsupportedForm);
        if ((encoded >> 32) != 0) return new(GCStaticMetadataStatus.CellRejected);
        if (!Relative32(cell, (uint)encoded, out ulong tagged))
            return new(GCStaticMetadataStatus.MethodTableRejected);
        ulong mt = tagged & ~3UL;
        if ((mt & 7) != 0 || mt < 8 || !Contains(metadata, mt, AbbreviatedMethodTableBytes) ||
            !Contains(metadata, mt - 8, 8))
            return new(GCStaticMetadataStatus.MethodTableRejected);
        if (Overlap(cell, 8, mt - 8, 8 + AbbreviatedMethodTableBytes))
            return new(GCStaticMetadataStatus.ProtectedAlias);
        if (permissions && !Range(capture, mapping, owner, ownerCapacity, moduleHandle, mt - 8, 24, false))
            return new(GCStaticMetadataStatus.MethodTableRejected);

        long count = unchecked((long)U64((byte*)(mt - 8)));
        if (count == 0) return new(GCStaticMetadataStatus.DescriptorRejected);
        if (count != 1) return new(GCStaticMetadataStatus.UnsupportedForm);
        if (mt < OneSeriesDescriptorBytes ||
            !Contains(metadata, mt - OneSeriesDescriptorBytes, OneSeriesDescriptorBytes + AbbreviatedMethodTableBytes))
            return new(GCStaticMetadataStatus.DescriptorRejected);
        ulong descriptor = mt - OneSeriesDescriptorBytes;
        if (Overlap(cell, 8, descriptor, OneSeriesDescriptorBytes + AbbreviatedMethodTableBytes))
            return new(GCStaticMetadataStatus.ProtectedAlias);
        if (permissions && !Range(capture, mapping, owner, ownerCapacity, moduleHandle, descriptor, 40, false))
            return new(GCStaticMetadataStatus.DescriptorRejected);

        // The static EEType ends at MT+16. Never read an ordinary 24-byte prefix.
        uint flags = U32((byte*)mt), size = U32((byte*)mt + 4);
        if (flags != HasPointers) return new(GCStaticMetadataStatus.UnsupportedForm);
        if (size < MinimumBaseSize || (size & 7) != 0)
            return new(GCStaticMetadataStatus.MethodTableRejected);
        if (size > MaximumBaseSize) return new(GCStaticMetadataStatus.UnsupportedForm);
        ulong related = U64((byte*)mt + 8);
        if (related != expectedSystemObjectMethodTable)
            return new(GCStaticMetadataStatus.RelatedTypeRejected);
        long adjusted = unchecked((long)U64((byte*)descriptor));
        ulong offset = U64((byte*)descriptor + 8);
        // B includes the syncblock preceding the object reference. Exclude its
        // MT word and the byte at/after allocation end without signed overflow.
        if (adjusted < 8L - size || adjusted > -16 ||
            (offset & 7) != 0 || offset < 8 || offset > size - 16UL)
            return new(GCStaticMetadataStatus.DescriptorRejected);
        ulong length = (ulong)((long)size + adjusted);
        if ((length & 7) != 0 || length > size - 8UL - offset)
            return new(GCStaticMetadataStatus.DescriptorRejected);
        return new(GCStaticMetadataStatus.Success, TypeManagerModuleStatus.Success,
            entry, cell, encoded, mt, related, descriptor, flags, size, adjusted, offset, length);
    }

    // Every owner has one serialized, stable lifetime. Self/byte bindings detect
    // accidental substitution; caller-forged records, ABA, reset/free, concurrent
    // mutation and hardware-fault rollback are outside this unsafe contract.
    private const ulong Poisoned = 1;
    private const ulong Published = 2;
    private struct Root
    {
        internal ulong Phase, Integrity, Self, Capacity, TypeOwner, TypeCapacity, Handle, Bump, BumpCapacity;
        internal EfiLoadedImageRangeCapturePlan Capture;
        internal GCStaticReadableWindow Metadata, Cells;
        internal RhpNewFastHeapAccess Heap;
        internal GCStaticMetadataResult Decoded;
        internal RhpNewFastResult Receipt;
        internal fixed byte Snapshot[68]; // entry4 + descriptor/MT40 + Object24
        internal uint Reserved;
    }

    public static ulong RequiredRootOwnerBytes => (ulong)sizeof(Root);

    /// <summary>
    /// One caller-owned zero buffer, one retained heap page outside the image,
    /// and one tagged entry. A buffer intersecting the image must lie wholly in
    /// admitted mutable data; external storage retains caller mapping/lifetime
    /// obligations. All rejection before the
    /// sole allocator commit preserves all bytes. After commit, failure leaves a
    /// poisoned non-retryable record; it cannot roll back the consumed allocation.
    /// </summary>
    public static GCStaticRootResult MaterializeOnce(EfiLoadedImageRangeCapturePlan capture,
        M01LoadedImageVirtualMappingEvidence mapping, byte* typeOwner, ulong typeCapacity,
        ulong handle, GCStaticReadableWindow metadata, GCStaticReadableWindow cells,
        ulong systemObject, RhpNewFastHeapAccess heap, RhpNewFastBumpState* bump, ulong bumpCapacity,
        byte* rootOwner, ulong rootCapacity)
    {
        GCStaticRootResult admission = AdmitRoot(capture, mapping, typeOwner, typeCapacity, handle,
            heap, bump, bumpCapacity, rootOwner, rootCapacity);
        if (!admission.Succeeded) return admission;
        Root* root = (Root*)rootOwner;
        if (root->Phase != 0)
            return TryGetRetainedRoot(capture, mapping, typeOwner, typeCapacity, handle, metadata, cells,
                systemObject, heap, bump, bumpCapacity, rootOwner, rootCapacity);
        if (!Zero(rootOwner, rootCapacity)) return new(GCStaticRootStatus.RootNotEmpty);
        GCStaticMetadataResult decoded = DecodeOne(capture, mapping, typeOwner, typeCapacity, handle,
            metadata, cells, systemObject, true, false, 0, 0);
        if (!decoded.Succeeded) return new(GCStaticRootStatus.MetadataRejected, decoded.Status, decoded.ModuleStatus);
        if (!Protected(capture, mapping, typeOwner, typeCapacity, handle, heap, bump, bumpCapacity,
                rootOwner, rootCapacity, decoded)) return new(GCStaticRootStatus.ProtectedAlias);
        // The expected Object address remains caller-provenanced, not a type
        // discovery heuristic. Check its admitted ordinary fixed header as well.
        if (U32((byte*)systemObject) != 0x50000000 || U32((byte*)systemObject + 4) != 24 ||
            U64((byte*)systemObject + 8) != 0)
            return new(GCStaticRootStatus.MetadataRejected, GCStaticMetadataStatus.RelatedTypeRejected);

        Root candidate = default;
        candidate.Phase = Poisoned; candidate.Self = (ulong)rootOwner; candidate.Capacity = rootCapacity;
        candidate.TypeOwner = (ulong)typeOwner; candidate.TypeCapacity = typeCapacity; candidate.Handle = handle;
        candidate.Capture = capture; candidate.Metadata = metadata; candidate.Cells = cells;
        candidate.Heap = heap; candidate.Bump = (ulong)bump; candidate.BumpCapacity = bumpCapacity;
        candidate.Reserved = 0;
        candidate.Decoded = decoded;
        Copy(candidate.Snapshot, (byte*)decoded.Entry, 4);
        Copy(candidate.Snapshot + 4, (byte*)decoded.Descriptor, 40);
        Copy(candidate.Snapshot + 44, (byte*)decoded.RelatedType, 24);

        RhpNewFastResult receipt = GuestTestRhpNewFast.TryAllocateStaticBase(heap, bump, bumpCapacity,
            NativeAotLayoutProfile.Ilc10_0_8WinX64LittleEndian, decoded.Flags, decoded.BaseSize, decoded.MethodTable);
        if (!receipt.AllocationSucceeded) return new(GCStaticRootStatus.HeapRejected, heapStatus: receipt.Status);
        candidate.Receipt = receipt;
        *root = candidate; // First root write: the allocation is now consumed.
        // These two pinned POD results have byte enums followed by UInt64s.
        // Clear their implicit padding explicitly before sealing actual bytes.
        for (int i = 2; i < 8; i++) ((byte*)&root->Decoded)[i] = 0;
        for (int i = 3; i < 8; i++) ((byte*)&root->Receipt)[i] = 0;
        if (!FreshAllocation(heap, bump, bumpCapacity, receipt, decoded))
            return new(GCStaticRootStatus.PostCommitRejected);
        // Complete the retained owner before the sole full-width cell publication.
        root->Phase = Published;
        root->Integrity = Integrity(root);
        *(ulong*)decoded.Cell = receipt.ObjectReference;
        if (ReadWord((byte*)decoded.Cell) != receipt.ObjectReference)
        {
            root->Phase = Poisoned;
            return new(GCStaticRootStatus.PostCommitRejected);
        }
        return Observe(decoded, receipt);
    }

    /// <summary>
    /// Revalidates original authority, metadata, absolute cell and genuine
    /// retained receipt. Later same-bump allocations and field writes are legal;
    /// zero payload is required only at initial materialization.
    /// </summary>
    public static GCStaticRootResult TryGetRetainedRoot(EfiLoadedImageRangeCapturePlan capture,
        M01LoadedImageVirtualMappingEvidence mapping, byte* typeOwner, ulong typeCapacity,
        ulong handle, GCStaticReadableWindow metadata, GCStaticReadableWindow cells,
        ulong systemObject, RhpNewFastHeapAccess heap, RhpNewFastBumpState* bump, ulong bumpCapacity,
        byte* rootOwner, ulong rootCapacity)
    {
        GCStaticRootResult admission = AdmitRoot(capture, mapping, typeOwner, typeCapacity, handle,
            heap, bump, bumpCapacity, rootOwner, rootCapacity);
        if (!admission.Succeeded) return admission;
        Root* root = (Root*)rootOwner;
        if (root->Phase == Poisoned) return new(GCStaticRootStatus.PostCommitRejected);
        if (root->Phase != Published) return new(GCStaticRootStatus.NotPublished);
        if (root->Integrity != Integrity(root)) return new(GCStaticRootStatus.RootChanged);
        if (root->Self != (ulong)rootOwner || root->Capacity != rootCapacity ||
            root->TypeOwner != (ulong)typeOwner || root->TypeCapacity != typeCapacity || root->Handle != handle ||
            !root->Capture.Equals(capture) || root->Bump != (ulong)bump || root->BumpCapacity != bumpCapacity ||
            !SameHeap(root->Heap, heap) || root->Metadata.Start != metadata.Start || root->Metadata.Bytes != metadata.Bytes ||
            root->Cells.Start != cells.Start || root->Cells.Bytes != cells.Bytes || root->Decoded.RelatedType != systemObject ||
            root->Reserved != 0 || !Zero(rootOwner + sizeof(Root), rootCapacity - RequiredRootOwnerBytes))
            return new(GCStaticRootStatus.RootChanged);
        GCStaticMetadataResult saved = root->Decoded;
        if (!Contains(metadata, saved.Entry, 4) || !Contains(metadata, saved.Descriptor, 40) ||
            !Contains(metadata, saved.RelatedType, 24) ||
            !Range(capture, mapping, typeOwner, typeCapacity, handle, saved.Entry, 4, false) ||
            !Range(capture, mapping, typeOwner, typeCapacity, handle, saved.Descriptor, 40, false) ||
            !Range(capture, mapping, typeOwner, typeCapacity, handle, saved.RelatedType, 24, false) ||
            !Equal(root->Snapshot, (byte*)saved.Entry, 4) ||
            !Equal(root->Snapshot + 4, (byte*)saved.Descriptor, 40) ||
            !Equal(root->Snapshot + 44, (byte*)saved.RelatedType, 24) ||
            root->Receipt.MethodTableToken != saved.MethodTable || root->Receipt.BytesConsumed != saved.BaseSize)
            return new(GCStaticRootStatus.RootChanged);
        RhpNewFastStatus status = GuestTestRhpNewFast.TryValidateRetainedAllocation(heap, bump, bumpCapacity, root->Receipt);
        if (status != RhpNewFastStatus.Success) return new(GCStaticRootStatus.HeapRejected, heapStatus: status);
        GCStaticMetadataResult decoded = DecodeOne(capture, mapping, typeOwner, typeCapacity, handle,
            metadata, cells, systemObject, true, true, root->Decoded.EncodedCell, root->Receipt.ObjectReference);
        if (!decoded.Succeeded) return new(GCStaticRootStatus.MetadataRejected, decoded.Status, decoded.ModuleStatus);
        if (!SameMetadata(decoded, root->Decoded))
            return new(GCStaticRootStatus.RootChanged);
        if (!Protected(capture, mapping, typeOwner, typeCapacity, handle, heap, bump, bumpCapacity,
                rootOwner, rootCapacity, decoded)) return new(GCStaticRootStatus.ProtectedAlias);
        return Observe(decoded, root->Receipt);
    }

    private static GCStaticRootResult AdmitRoot(EfiLoadedImageRangeCapturePlan capture,
        M01LoadedImageVirtualMappingEvidence mapping, byte* typeOwner, ulong typeCapacity, ulong handle,
        RhpNewFastHeapAccess heap, RhpNewFastBumpState* bump, ulong bumpCapacity, byte* root, ulong capacity)
    {
        if (sizeof(void*) != 8 || sizeof(GCStaticMetadataResult) != 88 || sizeof(RhpNewFastResult) != 88 ||
            root == null || ((ulong)root & 7) != 0 ||
            capacity < RequiredRootOwnerBytes || (ulong)root > ~0UL - capacity)
            return new(GCStaticRootStatus.RootStorageRejected);
        TypeManagerModuleResult module = TypeManagerModuleMetadataLiveNativeAOTImage.TryGetSection(
            capture, mapping, typeOwner, typeCapacity, handle, 204);
        if (!module.Succeeded) return new(GCStaticRootStatus.ModuleRejected, moduleStatus: module.Status);
        if (!WritableOwner(capture, mapping, typeOwner, typeCapacity, handle, (ulong)root, capacity) ||
            !HeapDisjoint(heap, bump, bumpCapacity, (ulong)root, capacity))
            return new(GCStaticRootStatus.ProtectedAlias);
        return new(GCStaticRootStatus.Success);
    }

    private static bool Protected(EfiLoadedImageRangeCapturePlan capture,
        M01LoadedImageVirtualMappingEvidence mapping, byte* typeOwner, ulong typeCapacity, ulong handle,
        RhpNewFastHeapAccess heap, RhpNewFastBumpState* bump, ulong bumpCapacity,
        byte* root, ulong capacity, GCStaticMetadataResult m) =>
        mapping.VirtualImageBase.TryGetRawValueForUefiBoundary(out ulong image) == AddressValueStatus.Success &&
        !Overlap((ulong)heap.Backing, heap.BackingCapacity, image, capture.Range.ImageSize) &&
        !Overlap((ulong)heap.Backing, heap.BackingCapacity, (ulong)typeOwner, typeCapacity) &&
        !Overlap((ulong)root, capacity, m.Cell, 8) &&
        !Overlap((ulong)root, capacity, m.Entry, 4) &&
        !Overlap((ulong)root, capacity, m.Descriptor, 40) &&
        !Overlap((ulong)root, capacity, m.RelatedType, 24) &&
        HeapDisjoint(heap, bump, bumpCapacity, m.Cell, 8) &&
        !Overlap((ulong)bump, bumpCapacity, m.Entry, 4) &&
        !Overlap((ulong)bump, bumpCapacity, m.Descriptor, 40) &&
        !Overlap((ulong)bump, bumpCapacity, m.RelatedType, 24) &&
        WritableOwner(capture, mapping, typeOwner, typeCapacity, handle, (ulong)bump, bumpCapacity);

    // The only additional allocator writes are to its retained bump controller.
    // Both it and the root must protect registered image extents, including any
    // unused capacity. External storage receives no invented PE permission proof.
    private static bool WritableOwner(EfiLoadedImageRangeCapturePlan capture,
        M01LoadedImageVirtualMappingEvidence mapping, byte* typeOwner, ulong typeCapacity,
        ulong handle, ulong address, ulong bytes) =>
        address != 0 && bytes != 0 && address <= ~0UL - bytes &&
        mapping.VirtualImageBase.TryGetRawValueForUefiBoundary(out ulong image) == AddressValueStatus.Success &&
        !Overlap(address, bytes, (ulong)typeOwner, typeCapacity) &&
        (!Overlap(address, bytes, image, capture.Range.ImageSize) ||
            Range(capture, mapping, typeOwner, typeCapacity, handle, address, bytes, true));

    private static bool HeapDisjoint(RhpNewFastHeapAccess a, RhpNewFastBumpState* bump,
        ulong bumpCapacity, ulong address, ulong bytes)
    {
        if (a.NodeCapacity > ~0UL / 48 || a.ReservationCapacity > ~0UL / 40) return false;
        ulong* starts = stackalloc ulong[6] { (ulong)a.Allocator, (ulong)a.Nodes, (ulong)a.Reservations,
            (ulong)a.Heap, (ulong)a.Backing, (ulong)bump };
        ulong* lengths = stackalloc ulong[6] { 56, a.NodeCapacity * 48, a.ReservationCapacity * 40,
            a.HeapCapacity, a.BackingCapacity, bumpCapacity };
        for (int i = 0; i < 6; i++)
            if (starts[i] == 0 || lengths[i] == 0 || starts[i] > ~0UL - lengths[i] ||
                Overlap(address, bytes, starts[i], lengths[i])) return false;
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool FreshAllocation(RhpNewFastHeapAccess heap, RhpNewFastBumpState* bump,
        ulong capacity, RhpNewFastResult receipt, GCStaticMetadataResult metadata)
    {
        if (receipt.MethodTableToken != metadata.MethodTable || receipt.BytesConsumed != metadata.BaseSize ||
            GuestTestRhpNewFast.TryValidateRetainedAllocation(heap, bump, capacity, receipt) != RhpNewFastStatus.Success)
            return false;
        byte* allocation = (byte*)receipt.MappedAllocationStart;
        for (ulong i = 0; i < receipt.BytesConsumed; i++)
            if ((i < 8 || i >= 16) && allocation[i] != 0) return false;
        return true;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong ReadWord(byte* address) => U64(address);

    // Local unkeyed byte-integrity check, not authentication against a forger.
    // It binds the exact stored receipt/padding before any retained field is used.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong Integrity(Root* root)
    {
        ulong value = 14695981039346656037UL;
        byte* bytes = (byte*)root;
        for (ulong i = 0; i < RequiredRootOwnerBytes; i++)
            if (i < 8 || i >= 16) value = unchecked((value ^ bytes[i]) * 1099511628211UL);
        return value;
    }

    private static GCStaticRootResult Observe(GCStaticMetadataResult metadata, RhpNewFastResult receipt) =>
        new(GCStaticRootStatus.Success, GCStaticMetadataStatus.Success, TypeManagerModuleStatus.Success,
            RhpNewFastStatus.Success, receipt.ObjectReference, metadata.Cell,
            receipt.ObjectReference + metadata.SeriesOffset, metadata.SeriesBytes, receipt.BytesConsumed);
    private static bool Range(EfiLoadedImageRangeCapturePlan capture, M01LoadedImageVirtualMappingEvidence mapping,
        byte* owner, ulong capacity, ulong handle, ulong address, ulong bytes, bool mutable) =>
        TypeManagerModuleMetadataLiveNativeAOTImage.TryValidateRegisteredRange(capture, mapping, owner, capacity,
            handle, address, bytes, mutable ? TypeManagerRegisteredRangeKind.MutableData :
            TypeManagerRegisteredRangeKind.ImmutableMetadata) == TypeManagerModuleStatus.Success;
    private static bool SameHeap(RhpNewFastHeapAccess a, RhpNewFastHeapAccess b) =>
        a.Allocator == b.Allocator && a.AllocatorCapacity == b.AllocatorCapacity && a.Nodes == b.Nodes &&
        a.NodeCount == b.NodeCount && a.NodeCapacity == b.NodeCapacity && a.Reservations == b.Reservations &&
        a.ReservationCount == b.ReservationCount && a.ReservationCapacity == b.ReservationCapacity &&
        a.ParentIndex == b.ParentIndex && a.Backing == b.Backing && a.BackingCapacity == b.BackingCapacity &&
        a.Heap == b.Heap && a.HeapCapacity == b.HeapCapacity;
    private static bool SameMetadata(GCStaticMetadataResult a, GCStaticMetadataResult b) =>
        a.Status == b.Status && a.ModuleStatus == b.ModuleStatus && a.Entry == b.Entry && a.Cell == b.Cell &&
        a.EncodedCell == b.EncodedCell && a.MethodTable == b.MethodTable && a.RelatedType == b.RelatedType &&
        a.Descriptor == b.Descriptor && a.Flags == b.Flags && a.BaseSize == b.BaseSize &&
        a.AdjustedSize == b.AdjustedSize && a.SeriesOffset == b.SeriesOffset && a.SeriesBytes == b.SeriesBytes;
    private static bool Zero(byte* p, ulong bytes)
    { for (ulong i = 0; i < bytes; i++) if (p[i] != 0) return false; return true; }
    private static bool Equal(byte* a, byte* b, ulong bytes)
    { for (ulong i = 0; i < bytes; i++) if (a[i] != b[i]) return false; return true; }
    private static void Copy(byte* target, byte* source, ulong bytes)
    { for (ulong i = 0; i < bytes; i++) target[i] = source[i]; }

    private static bool Relative32(ulong origin, uint encoded, out ulong target)
    {
        target = 0;
        long delta = unchecked((int)encoded);
        if (delta >= 0)
        {
            if ((ulong)delta > ~0UL - origin) return false;
            target = origin + (ulong)delta;
        }
        else
        {
            ulong magnitude = (ulong)-delta;
            if (origin < magnitude) return false;
            target = origin - magnitude;
        }
        return true;
    }
    private static bool WindowInImage(GCStaticReadableWindow window, ulong image, ulong bytes) =>
        window.Start != 0 && window.Bytes != 0 && window.Start <= ~0UL - window.Bytes &&
        window.Start >= image && window.Start - image <= bytes && window.Bytes <= bytes - (window.Start - image);
    private static bool Contains(GCStaticReadableWindow window, ulong address, ulong bytes) =>
        address >= window.Start && address - window.Start <= window.Bytes && bytes <= window.Bytes - (address - window.Start);
    private static bool Overlap(ulong a, ulong an, ulong b, ulong bn) => an != 0 && bn != 0 &&
        (a <= b ? b - a < an : a - b < bn);
    private static uint U32(byte* p) => (uint)p[0] | (uint)p[1] << 8 | (uint)p[2] << 16 | (uint)p[3] << 24;
    private static ulong U64(byte* p) => U32(p) | (ulong)U32(p + 4) << 32;
}
