using OS.Boot;
using OS.Kernel.Memory;

namespace OS.Kernel.Runtime;
/// <summary>
/// Building blocks for a bounded synchronous collector over the retained heap.
/// Explicit roots and one retained static base, iterative tracing, and atomic
/// sweep through the original allocator's collecting mode. No stack scanning,
/// safepoint discovery, finalizers or concurrent mutators are supported.
/// Caller mappings, metadata lifetime and exclusive access remain obligations.
/// </summary>

public static unsafe class BoundedNonMovingCollectorExplicitRootEnumeration
{
    public const uint MaximumReferenceSeries = 4;
    public const ulong MaximumObjectBytes = 4096;
    /// <summary>
    /// All inputs and heap fields remain exclusively stable for this synchronous
    /// operation. Roots are original allocation receipts (default means null).
    /// The work limit bounds distinct live objects; internal stack scratch is fixed.
    /// All live allocations, even unreachable ones, are validated before sweeping.
    /// Raw field references carry no historical generation; callers must not hide
    /// live roots or manufacture references after an object's lifetime has ended.
    /// </summary>

    public static BoundedCollectionResult Collect(NativeAotLayoutProfile profile, CollectorModuleAccess module, RhpNewFastHeapAccess heap, RhpNewFastBumpState* state, ulong capacity, RhpNewFastResult* roots, uint rootCount, uint rootCapacity, CollectorStaticRootAccess staticRoot = default, uint workLimit = GuestTestRhpNewFast.MaximumTrackedAllocations)
    {
        if (profile != NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian)
            return new(BoundedCollectionStatus.UnsupportedProfile);
        if (rootCount > rootCapacity || rootCapacity > GuestTestRhpNewFast.MaximumTrackedAllocations || (rootCapacity == 0 ? roots != null : roots == null || ((ulong)roots & 7) != 0 || (ulong)roots > ~0UL - rootCapacity * (ulong)sizeof(RhpNewFastResult)))
            return new(BoundedCollectionStatus.InvalidRoots);
        if (workLimit > GuestTestRhpNewFast.MaximumTrackedAllocations)
            return new(BoundedCollectionStatus.WorkLimitExceeded);
        RhpNewFastStatus ownerStatus = GuestTestRhpNewFast.ValidateCollectingOwner(heap, state, capacity);
        if (ownerStatus != RhpNewFastStatus.Success)
            return new(BoundedCollectionStatus.OwnerRejected, ownerStatus);
        var registered = TypeManagerModuleMetadataLiveNativeAOTImage.TryGetSection(module.Capture, module.Mapping, module.Owner, module.Capacity, module.Handle, 204);
        if (!registered.Succeeded || module.Mapping.VirtualImageBase.TryGetRawValueForUefiBoundary(out ulong image) != AddressValueStatus.Success)
            return new(BoundedCollectionStatus.ModuleRejected);
        if (Overlap((ulong)heap.Backing, heap.BackingCapacity, image, module.Capture.Range.ImageSize) || Overlap((ulong)heap.Backing, heap.BackingCapacity, (ulong)module.Owner, module.Capacity) || Overlap((ulong)state, capacity, (ulong)module.Owner, module.Capacity) || (Overlap((ulong)state, capacity, image, module.Capture.Range.ImageSize) && TypeManagerModuleMetadataLiveNativeAOTImage.TryValidateRegisteredRange(module.Capture, module.Mapping, module.Owner, module.Capacity, module.Handle, (ulong)state, capacity, TypeManagerRegisteredRangeKind.MutableData) != TypeManagerModuleStatus.Success))
            return new(BoundedCollectionStatus.StorageOverlap);
        ulong rootBytes = rootCapacity * (ulong)sizeof(RhpNewFastResult);
        if (rootBytes != 0 && (Overlap((ulong)roots, rootBytes, (ulong)state, capacity) || Overlap((ulong)roots, rootBytes, (ulong)heap.Backing, heap.BackingCapacity) || Overlap((ulong)roots, rootBytes, (ulong)module.Owner, module.Capacity)))
            return new(BoundedCollectionStatus.StorageOverlap);
        ulong* live = stackalloc ulong[3]
        {
            0,
            0,
            0
        };
        uint* queue = stackalloc uint[(int)GuestTestRhpNewFast.MaximumTrackedAllocations];
        uint queued = 0;
        ulong retainedStatic = 0;
        if (staticRoot.Owner != null)
        {
            GCStaticRootResult root = GCStaticRootsMalformedMetadata.TryGetRetainedRoot(module.Capture, module.Mapping, module.Owner, module.Capacity, module.Handle, module.Metadata, staticRoot.Cells, staticRoot.SystemObjectMethodTable, heap, state, capacity, staticRoot.Owner, staticRoot.Capacity);
            if (!root.Succeeded)
                return new(BoundedCollectionStatus.StaticRootRejected);
            retainedStatic = root.ObjectReference;
        }
        else if (staticRoot.Capacity != 0 || staticRoot.Cells.Start != 0 || staticRoot.Cells.Bytes != 0 || staticRoot.SystemObjectMethodTable != 0)
            return new(BoundedCollectionStatus.StaticRootRejected);
        // Validate every current allocation/map/reference, including dead objects.
        // The allocator already checked disjoint extents, headers and identities.
        for (uint i = 0; i < GuestTestRhpNewFast.MaximumTrackedAllocations; i++)
        {
            RhpNewFastResult allocation = GuestTestRhpNewFast.ReadTrackedAllocation(state, i, out bool abbreviated);
            if (!allocation.AllocationSucceeded)
                continue;
            CollectorTypeMapResult map = Map(module, profile, allocation, abbreviated);
            if (!map.Succeeded || map.Prefix.BaseSize != allocation.BytesConsumed)
                return new(BoundedCollectionStatus.MetadataRejected);
            if (abbreviated)
            {
                if (allocation.ObjectReference != retainedStatic)
                    return new(BoundedCollectionStatus.StaticRootRequired);
                if (!Mark(i, live, queue, ref queued, workLimit))
                    return new(BoundedCollectionStatus.WorkLimitExceeded);
            }

            for (uint s = 0; s < map.SeriesCount; s++)
            {
                map.TryGetSeries(s, out CollectorReferenceSeries series);
                for (ulong offset = 0; offset < series.Bytes; offset += 8)
                {
                    ulong child = Read64((byte*)(allocation.ObjectReference + series.OffsetFromReference + offset));
                    if (child != 0 && Find(state, child) == GuestTestRhpNewFast.MaximumTrackedAllocations)
                        return new(BoundedCollectionStatus.InvalidReference);
                }
            }
        }

        for (uint i = 0; i < rootCount; i++)
        {
            RhpNewFastResult root = roots[i];
            if (IsNullRoot(root))
                continue;
            if (GuestTestRhpNewFast.TryValidateRetainedAllocation(heap, state, capacity, root) != RhpNewFastStatus.Success)
                return new(BoundedCollectionStatus.InvalidReference);
            uint index = Find(state, root.ObjectReference);
            if (index == GuestTestRhpNewFast.MaximumTrackedAllocations)
                return new(BoundedCollectionStatus.InvalidReference);
            if (!Mark(index, live, queue, ref queued, workLimit))
                return new(BoundedCollectionStatus.WorkLimitExceeded);
        }

        for (uint next = 0; next < queued; next++)
        {
            RhpNewFastResult allocation = GuestTestRhpNewFast.ReadTrackedAllocation(state, queue[next], out bool abbreviated);
            CollectorTypeMapResult map = Map(module, profile, allocation, abbreviated);
            if (!map.Succeeded || map.Prefix.BaseSize != allocation.BytesConsumed)
                return new(BoundedCollectionStatus.MetadataRejected);
            for (uint s = 0; s < map.SeriesCount; s++)
            {
                map.TryGetSeries(s, out CollectorReferenceSeries series);
                for (ulong offset = 0; offset < series.Bytes; offset += 8)
                {
                    ulong child = Read64((byte*)(allocation.ObjectReference + series.OffsetFromReference + offset));
                    if (child == 0)
                        continue;
                    uint index = Find(state, child);
                    if (index == GuestTestRhpNewFast.MaximumTrackedAllocations)
                        return new(BoundedCollectionStatus.InvalidReference);
                    if (!Mark(index, live, queue, ref queued, workLimit))
                        return new(BoundedCollectionStatus.WorkLimitExceeded);
                }
            }
        }

        // Quiescence is a caller obligation, not a race detected by a last check.
        // No heap/owner writes happened above; commit has no fallible suboperations.
        GuestTestRhpNewFast.CommitSweep(state, live, out uint reclaimed, out ulong bytes);
        return new(BoundedCollectionStatus.Success, RhpNewFastStatus.Success, queued, reclaimed, bytes);
    }

    private static CollectorTypeMapResult Map(CollectorModuleAccess m, NativeAotLayoutProfile profile, RhpNewFastResult allocation, bool abbreviated) => InspectType(profile, m.Capture, m.Mapping, m.Owner, m.Capacity, m.Handle, m.Metadata, allocation.MethodTableToken, abbreviated);
    private static uint Find(RhpNewFastBumpState* state, ulong reference)
    {
        for (uint i = 0; i < GuestTestRhpNewFast.MaximumTrackedAllocations; i++)
        {
            RhpNewFastResult record = GuestTestRhpNewFast.ReadTrackedAllocation(state, i, out _);
            if (record.AllocationSucceeded && record.ObjectReference == reference)
                return i;
        }

        return GuestTestRhpNewFast.MaximumTrackedAllocations;
    }

    private static bool Mark(uint index, ulong* live, uint* queue, ref uint queued, uint limit)
    {
        ulong bit = 1UL << (int)(index % 64);
        if ((live[index / 64] & bit) != 0)
            return true;
        if (queued >= limit)
            return false;
        live[index / 64] |= bit;
        queue[queued++] = index;
        return true;
    }

    private static bool IsNullRoot(RhpNewFastResult r) => r.Status == 0 && r.Operation == 0 && r.HeapStatus == 0 && r.MappedAllocationStart == 0 && r.MappedAllocationEndExclusive == 0 && r.PhysicalAllocationStart == 0 && r.PhysicalAllocationEndExclusive == 0 && r.ObjectReference == 0 && r.MethodTableToken == 0 && r.BytesConsumed == 0 && r.UsedBefore == 0 && r.UsedAfter == 0 && r.AllocationCount == 0;
    private static bool Overlap(ulong a, ulong aBytes, ulong b, ulong bBytes) => a < b + bBytes && b < a + aBytes;
    /// <summary>
    /// Decode ordinary fixed-class or abbreviated GC-static metadata for the
    /// pinned .NET 11.0.0 RC2 x64 profile. Variable-sized objects, negative/repeating
    /// GCDesc forms, finalizers and dynamic type metadata are not admitted.
    /// Every read is inside both the caller window and registered immutable PE
    /// metadata. There are no allocations, callbacks or writes to caller memory.
    /// </summary>

    public static CollectorTypeMapResult InspectType(NativeAotLayoutProfile profile, EfiLoadedImageRangeCapturePlan capture, M01LoadedImageVirtualMappingEvidence mapping, byte* typeOwner, ulong typeCapacity, ulong moduleHandle, GCStaticReadableWindow metadata, ulong methodTable, bool abbreviatedStaticBase = false)
    {
        if (profile != NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian)
            return new(CollectorTypeMapStatus.UnsupportedProfile);
        if (metadata.Start == 0 || metadata.Bytes == 0 || metadata.Start > ~0UL - metadata.Bytes)
            return new(CollectorTypeMapStatus.InvalidWindow);
        ulong prefixBytes = abbreviatedStaticBase ? 16UL : 24UL;
        if (methodTable == 0 || (methodTable & 7) != 0 || !Contains(metadata, methodTable, prefixBytes))
            return new(CollectorTypeMapStatus.InvalidMethodTable);
        TypeManagerModuleStatus authority = TypeManagerModuleMetadataLiveNativeAOTImage.TryValidateRegisteredRange(capture, mapping, typeOwner, typeCapacity, moduleHandle, methodTable, prefixBytes, TypeManagerRegisteredRangeKind.ImmutableMetadata);
        if (authority != TypeManagerModuleStatus.Success)
            return new(CollectorTypeMapStatus.ModuleRejected, authority);
        byte* mt = (byte*)methodTable;
        uint flags = Read32(mt), size = Read32(mt + 4);
        NativeAotMethodTablePrefix prefix = new(flags, size, Read64(mt + 8), abbreviatedStaticBase ? (ushort)0 : Read16(mt + 16), abbreviatedStaticBase ? (ushort)0 : Read16(mt + 18), abbreviatedStaticBase ? 0 : Read32(mt + 20));
        if (abbreviatedStaticBase)
        {
            if (flags != 0x01000000 || size < 24 || size > MaximumObjectBytes || (size & 7) != 0)
                return new(CollectorTypeMapStatus.UnsupportedType);
        }
        else
        {
            NativeAotLayoutResult layout = ObjectArrayStringEETypeMethodTableLayoutContracts.Evaluate(profile, prefix, 0, MaximumObjectBytes);
            if (!layout.Succeeded || layout.Shape != NativeAotLayoutShape.FixedObject)
                return new(CollectorTypeMapStatus.UnsupportedType);
        }

        if ((flags & 0x01000000) == 0)
            return new(CollectorTypeMapStatus.Success, authority, methodTable, prefix);
        if (methodTable < 8 || !Contains(metadata, methodTable - 8, 8))
            return new(CollectorTypeMapStatus.InvalidDescriptor);
        authority = TypeManagerModuleMetadataLiveNativeAOTImage.TryValidateRegisteredRange(capture, mapping, typeOwner, typeCapacity, moduleHandle, methodTable - 8, 8, TypeManagerRegisteredRangeKind.ImmutableMetadata);
        if (authority != TypeManagerModuleStatus.Success)
            return new(CollectorTypeMapStatus.ModuleRejected, authority);
        long signedCount = unchecked((long)Read64(mt - 8));
        if (signedCount < 1 || signedCount > MaximumReferenceSeries)
            return new(CollectorTypeMapStatus.UnsupportedSeriesCount);
        uint count = (uint)signedCount;
        ulong descriptorBytes = 8 + count * 16UL;
        if (methodTable < descriptorBytes || !Contains(metadata, methodTable - descriptorBytes, descriptorBytes))
            return new(CollectorTypeMapStatus.InvalidDescriptor);
        authority = TypeManagerModuleMetadataLiveNativeAOTImage.TryValidateRegisteredRange(capture, mapping, typeOwner, typeCapacity, moduleHandle, methodTable - descriptorBytes, descriptorBytes + prefixBytes, TypeManagerRegisteredRangeKind.ImmutableMetadata);
        if (authority != TypeManagerModuleStatus.Success)
            return new(CollectorTypeMapStatus.ModuleRejected, authority);
        CollectorReferenceSeries* series = stackalloc CollectorReferenceSeries[4];
        for (uint i = 0; i < MaximumReferenceSeries; i++)
            series[i] = default;
        // GCDesc grows backwards: count at MT-8, highest series at MT-24.
        // The stored signed length is adjusted by subtracting object BaseSize.
        // Source: pinned dotnet/dotnet 820230c386db5b0106c5430b3e5aee92a7826bf5,
        // src/runtime/src/coreclr/gc/gcdesc.h, CGCDesc/CGCDescSeries.
        for (uint i = 0; i < count; i++)
        {
            byte* descriptor = mt - 24 - i * 16;
            long adjusted = unchecked((long)Read64(descriptor));
            ulong offset = Read64(descriptor + 8);
            if (adjusted < 8L - size || adjusted > -16 || (offset & 7) != 0 || offset < 8 || offset > size - 16UL)
                return new(CollectorTypeMapStatus.InvalidDescriptor);
            ulong bytes = (ulong)(size + adjusted);
            if ((bytes & 7) != 0 || bytes > size - 8UL - offset)
                return new(CollectorTypeMapStatus.InvalidDescriptor);
            for (uint j = 0; j < i; j++)
                if (offset < series[j].OffsetFromReference + series[j].Bytes && series[j].OffsetFromReference < offset + bytes)
                    return new(CollectorTypeMapStatus.OverlappingSeries);
            series[i] = new(offset, bytes);
        }

        return new(CollectorTypeMapStatus.Success, authority, methodTable, prefix, count, series[0], series[1], series[2], series[3]);
    }

    private static bool Contains(GCStaticReadableWindow window, ulong address, ulong bytes) => address >= window.Start && address - window.Start <= window.Bytes && bytes <= window.Bytes - (address - window.Start);
    private static ushort Read16(byte* p) => (ushort)(p[0] | p[1] << 8);
    private static uint Read32(byte* p) => p[0] | (uint)p[1] << 8 | (uint)p[2] << 16 | (uint)p[3] << 24;
    private static ulong Read64(byte* p) => Read32(p) | (ulong)Read32(p + 4) << 32;
}
