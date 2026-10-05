using OS.Boot;

namespace OS.Kernel.Runtime;

public enum CollectorTypeMapStatus : byte
{
    Uninitialized, Success, UnsupportedProfile, InvalidWindow, InvalidMethodTable,
    ModuleRejected, UnsupportedType, InvalidDescriptor, UnsupportedSeriesCount,
    OverlappingSeries
}

public readonly struct CollectorReferenceSeries
{
    internal CollectorReferenceSeries(ulong offset, ulong bytes)
    { OffsetFromReference = offset; Bytes = bytes; }

    public ulong OffsetFromReference { get; }
    public ulong Bytes { get; }
}

/// <summary>
/// Immediate metadata observation, not a retained allocation or root capability.
/// Collection must revalidate original metadata and allocation ownership before
/// following any object reference. This result alone authorizes no heap writes.
/// </summary>
public readonly struct CollectorTypeMapResult
{
    internal CollectorTypeMapResult(CollectorTypeMapStatus status,
        TypeManagerModuleStatus moduleStatus = default, ulong methodTable = 0,
        NativeAotMethodTablePrefix prefix = default, uint count = 0,
        CollectorReferenceSeries first = default, CollectorReferenceSeries second = default,
        CollectorReferenceSeries third = default, CollectorReferenceSeries fourth = default)
    {
        Status = status; ModuleStatus = moduleStatus; MethodTable = methodTable;
        Prefix = prefix; SeriesCount = count;
        this.first = first; this.second = second; this.third = third; this.fourth = fourth;
    }

    private readonly CollectorReferenceSeries first, second, third, fourth;
    public CollectorTypeMapStatus Status { get; }
    public TypeManagerModuleStatus ModuleStatus { get; }
    public ulong MethodTable { get; }
    public NativeAotMethodTablePrefix Prefix { get; }
    public uint SeriesCount { get; }
    public bool Succeeded => Status == CollectorTypeMapStatus.Success;

    public bool TryGetSeries(uint index, out CollectorReferenceSeries series)
    {
        series = default;
        if (!Succeeded || index >= SeriesCount) return false;
        series = index switch { 0 => first, 1 => second, 2 => third, _ => fourth };
        return true;
    }
}

/// <summary>
/// Building blocks for a bounded synchronous collector over the retained heap.
/// The first implemented part decodes fixed-object reference maps from the
/// original registered image. It does not yet trace, sweep, or reclaim objects.
/// Caller mappings, metadata lifetime and exclusive access remain obligations.
/// </summary>
public static unsafe class BoundedNonMovingCollectorExplicitRootEnumeration
{
    public const uint MaximumReferenceSeries = 4;
    public const ulong MaximumObjectBytes = 4096;

    /// <summary>
    /// Decode ordinary fixed-class or abbreviated GC-static metadata for the
    /// pinned .NET 10.0.8 x64 profile. Variable-sized objects, negative/repeating
    /// GCDesc forms, finalizers and dynamic type metadata are not admitted.
    /// Every read is inside both the caller window and registered immutable PE
    /// metadata. There are no allocations, callbacks or writes to caller memory.
    /// </summary>
    public static CollectorTypeMapResult InspectType(NativeAotLayoutProfile profile,
        EfiLoadedImageRangeCapturePlan capture, M01LoadedImageVirtualMappingEvidence mapping,
        byte* typeOwner, ulong typeCapacity, ulong moduleHandle,
        GCStaticReadableWindow metadata, ulong methodTable, bool abbreviatedStaticBase = false)
    {
        if (profile != NativeAotLayoutProfile.Ilc10_0_8WinX64LittleEndian)
            return new(CollectorTypeMapStatus.UnsupportedProfile);
        if (metadata.Start == 0 || metadata.Bytes == 0 || metadata.Start > ~0UL - metadata.Bytes)
            return new(CollectorTypeMapStatus.InvalidWindow);
        ulong prefixBytes = abbreviatedStaticBase ? 16UL : 24UL;
        if (methodTable == 0 || (methodTable & 7) != 0 || !Contains(metadata, methodTable, prefixBytes))
            return new(CollectorTypeMapStatus.InvalidMethodTable);
        TypeManagerModuleStatus authority = TypeManagerModuleMetadataLiveNativeAOTImage.TryValidateRegisteredRange(
            capture, mapping, typeOwner, typeCapacity, moduleHandle, methodTable, prefixBytes,
            TypeManagerRegisteredRangeKind.ImmutableMetadata);
        if (authority != TypeManagerModuleStatus.Success)
            return new(CollectorTypeMapStatus.ModuleRejected, authority);

        byte* mt = (byte*)methodTable;
        uint flags = Read32(mt), size = Read32(mt + 4);
        NativeAotMethodTablePrefix prefix = new(flags, size, Read64(mt + 8),
            abbreviatedStaticBase ? (ushort)0 : Read16(mt + 16),
            abbreviatedStaticBase ? (ushort)0 : Read16(mt + 18),
            abbreviatedStaticBase ? 0 : Read32(mt + 20));
        if (abbreviatedStaticBase)
        {
            if (flags != 0x01000000 || size < 24 || size > MaximumObjectBytes || (size & 7) != 0)
                return new(CollectorTypeMapStatus.UnsupportedType);
        }
        else
        {
            NativeAotLayoutResult layout = ObjectArrayStringEETypeMethodTableLayoutContracts.Evaluate(
                profile, prefix, 0, MaximumObjectBytes);
            if (!layout.Succeeded || layout.Shape != NativeAotLayoutShape.FixedObject)
                return new(CollectorTypeMapStatus.UnsupportedType);
        }
        if ((flags & 0x01000000) == 0)
            return new(CollectorTypeMapStatus.Success, authority, methodTable, prefix);

        if (methodTable < 8 || !Contains(metadata, methodTable - 8, 8))
            return new(CollectorTypeMapStatus.InvalidDescriptor);
        authority = TypeManagerModuleMetadataLiveNativeAOTImage.TryValidateRegisteredRange(
            capture, mapping, typeOwner, typeCapacity, moduleHandle, methodTable - 8, 8,
            TypeManagerRegisteredRangeKind.ImmutableMetadata);
        if (authority != TypeManagerModuleStatus.Success)
            return new(CollectorTypeMapStatus.ModuleRejected, authority);
        long signedCount = unchecked((long)Read64(mt - 8));
        if (signedCount < 1 || signedCount > MaximumReferenceSeries)
            return new(CollectorTypeMapStatus.UnsupportedSeriesCount);
        uint count = (uint)signedCount;
        ulong descriptorBytes = 8 + count * 16UL;
        if (methodTable < descriptorBytes || !Contains(metadata, methodTable - descriptorBytes, descriptorBytes))
            return new(CollectorTypeMapStatus.InvalidDescriptor);
        authority = TypeManagerModuleMetadataLiveNativeAOTImage.TryValidateRegisteredRange(
            capture, mapping, typeOwner, typeCapacity, moduleHandle, methodTable - descriptorBytes,
            descriptorBytes + prefixBytes, TypeManagerRegisteredRangeKind.ImmutableMetadata);
        if (authority != TypeManagerModuleStatus.Success)
            return new(CollectorTypeMapStatus.ModuleRejected, authority);

        CollectorReferenceSeries* series = stackalloc CollectorReferenceSeries[4];
        for (uint i = 0; i < MaximumReferenceSeries; i++) series[i] = default;
        // GCDesc grows backwards: count at MT-8, highest series at MT-24.
        // The stored signed length is adjusted by subtracting object BaseSize.
        // Source: pinned dotnet/dotnet 94ea82652cdd4e0f8046b5bd5becbd11461482ca,
        // src/runtime/src/coreclr/gc/gcdesc.h, CGCDesc/CGCDescSeries.
        for (uint i = 0; i < count; i++)
        {
            byte* descriptor = mt - 24 - i * 16;
            long adjusted = unchecked((long)Read64(descriptor));
            ulong offset = Read64(descriptor + 8);
            if (adjusted < 8L - size || adjusted > -16 || (offset & 7) != 0 ||
                offset < 8 || offset > size - 16UL)
                return new(CollectorTypeMapStatus.InvalidDescriptor);
            ulong bytes = (ulong)(size + adjusted);
            if ((bytes & 7) != 0 || bytes > size - 8UL - offset)
                return new(CollectorTypeMapStatus.InvalidDescriptor);
            for (uint j = 0; j < i; j++)
                if (offset < series[j].OffsetFromReference + series[j].Bytes &&
                    series[j].OffsetFromReference < offset + bytes)
                    return new(CollectorTypeMapStatus.OverlappingSeries);
            series[i] = new(offset, bytes);
        }
        return new(CollectorTypeMapStatus.Success, authority, methodTable, prefix, count,
            series[0], series[1], series[2], series[3]);
    }

    private static bool Contains(GCStaticReadableWindow window, ulong address, ulong bytes) =>
        address >= window.Start && address - window.Start <= window.Bytes &&
        bytes <= window.Bytes - (address - window.Start);
    private static ushort Read16(byte* p) => (ushort)(p[0] | p[1] << 8);
    private static uint Read32(byte* p) => p[0] | (uint)p[1] << 8 | (uint)p[2] << 16 | (uint)p[3] << 24;
    private static ulong Read64(byte* p) => Read32(p) | (ulong)Read32(p + 4) << 32;
}
