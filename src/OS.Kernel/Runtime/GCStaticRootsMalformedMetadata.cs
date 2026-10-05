using OS.Boot;

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

/// <summary>
/// Pinned .NET 10.0.8 x64/little-endian, one uninitialized GC-static entry and
/// one positive GCDesc series. No writes, callbacks, allocations or payload
/// object reads. The original image/owner must remain mapped and exclusively
/// stable through the call, under the TypeManager unsafe caller contract.
/// The caller supplies genuine System.Object provenance separately: this code
/// checks only its admitted address and equality with the abbreviated MT field.
/// Cell RW/NX and immutable metadata permission queries, genuine compiler
/// association, materialization and retained root lifetime remain later work.
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
        ulong expectedSystemObjectMethodTable)
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
        if (!Relative32(entry, U32((byte*)entry), out ulong cell) ||
            (cell & 7) != 0 || !Contains(cells, cell, 8))
            return new(GCStaticMetadataStatus.CellRejected);
        if (Overlap(cell, 8, (ulong)owner, ownerCapacity) ||
            Overlap(cell, 8, slot.Section, TypeManagerModuleMetadataLiveNativeAOTImage.SlotBytes) ||
            Overlap(cell, 8, entry, 4) ||
            Overlap(cell, 8, expectedSystemObjectMethodTable,
                ObjectArrayStringEETypeMethodTableLayoutContracts.MethodTablePrefixBytes))
            return new(GCStaticMetadataStatus.ProtectedAlias);

        ulong encoded = U64((byte*)cell);
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

        long count = unchecked((long)U64((byte*)(mt - 8)));
        if (count == 0) return new(GCStaticMetadataStatus.DescriptorRejected);
        if (count != 1) return new(GCStaticMetadataStatus.UnsupportedForm);
        if (mt < OneSeriesDescriptorBytes ||
            !Contains(metadata, mt - OneSeriesDescriptorBytes, OneSeriesDescriptorBytes + AbbreviatedMethodTableBytes))
            return new(GCStaticMetadataStatus.DescriptorRejected);
        ulong descriptor = mt - OneSeriesDescriptorBytes;
        if (Overlap(cell, 8, descriptor, OneSeriesDescriptorBytes + AbbreviatedMethodTableBytes))
            return new(GCStaticMetadataStatus.ProtectedAlias);

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
