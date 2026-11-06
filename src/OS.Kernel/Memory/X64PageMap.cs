namespace OS.Kernel.Memory;
/// <summary>
/// Walks and mutates only a validated caller-owned flat image. It allocates no
/// table page, touches no live CR3 hierarchy, and performs no TLB operation.
/// </summary>

public static unsafe class X64PageMap
{
    public const ulong PageSize = 4096;
    public const ulong MaximumPageImages = 64;
    public const ulong MaximumEntryCount = MaximumPageImages * X64PageTableImagePage.EntryCountPerPage;
    public const ulong MaximumEssentialRangeCount = 128;
    public const ulong MaximumEssentialRangePages = 4096;
    public const ulong RequiredPointerAlignment = 8;
    public const ulong LowCanonicalEndExclusive = 0x0000_8000_0000_0000UL;
    public const bool PerformsLiveMapping = false;
    public const bool PerformsLiveInvalidation = false;
    public const bool AllocatesPageTables = false;
    public const bool SplitsLargePages = false;
    public static X64PageMapResult Apply(X64PageMapRequest request, X64PageTableImagePage* pageImages, ulong pageImageCount, ulong pageImageCapacity, ulong* entries, ulong entryCount, ulong entryCapacity, X64EssentialMappingRange* essentialRanges, ulong essentialRangeCount, ulong essentialRangeCapacity)
    {
        X64PageMapStatus requestStatus = request.Validate();
        if (requestStatus != X64PageMapStatus.Success)
        {
            return Fail(requestStatus, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity);
        }

        X64PageMapResult storageFailure = ValidateStorage(request, pageImages, pageImageCount, pageImageCapacity, entries, entryCount, entryCapacity, essentialRanges, essentialRangeCount, essentialRangeCapacity);
        if (storageFailure.IsInitialized)
        {
            return storageFailure;
        }

        ulong rootPageIndex = FindPageImage(request.RootTable, pageImages, pageImageCount);
        if (rootPageIndex == X64PageMapResult.NoIndex)
        {
            return Fail(X64PageMapStatus.RootPageNotFound, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity);
        }

        if (request.VirtualAddress.TryGetRawValueForHardwareRegisterBoundary(out ulong rawVirtual) != OS.Boot.AddressValueStatus.Success)
        {
            return Fail(X64PageMapStatus.InvalidVirtualAddress, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity, rootPageIndex: rootPageIndex);
        }

        ulong currentPageIndex = rootPageIndex;
        X64PageTableLevel level = X64PageTableLevel.Pml4e;
        for (uint depth = 0; depth < 3; depth++)
        {
            uint tableIndex = GetIndex(rawVirtual, level);
            ulong entryIndex = pageImages[currentPageIndex].EntryStartIndex + tableIndex;
            ulong rawEntry = entries[entryIndex];
            X64PageTableEntryResult decoded = X64PageTableEntry.TryDecode(level, rawEntry, request.Preconditions);
            if (!decoded.Succeeded)
            {
                return Fail(X64PageMapStatus.MalformedEntry, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity, decoded.Status, rootPageIndex: rootPageIndex, targetPageIndex: currentPageIndex, targetEntryIndex: entryIndex);
            }

            if (decoded.Kind == X64PageTableEntryKind.Unmapped)
            {
                return Fail(X64PageMapStatus.MissingTable, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity, rootPageIndex: rootPageIndex, targetPageIndex: currentPageIndex, targetEntryIndex: entryIndex);
            }

            if (decoded.Kind != X64PageTableEntryKind.TablePointer)
            {
                return Fail(X64PageMapStatus.LargePageCollision, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity, rootPageIndex: rootPageIndex, targetPageIndex: currentPageIndex, targetEntryIndex: entryIndex);
            }

            ulong childPageIndex = FindPageImage(decoded.PhysicalAddress, pageImages, pageImageCount);
            if (childPageIndex == X64PageMapResult.NoIndex)
            {
                return Fail(X64PageMapStatus.TablePointerTargetMissing, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity, rootPageIndex: rootPageIndex, targetPageIndex: currentPageIndex, targetEntryIndex: entryIndex);
            }

            currentPageIndex = childPageIndex;
            level = level == X64PageTableLevel.Pml4e ? X64PageTableLevel.Pdpte : level == X64PageTableLevel.Pdpte ? X64PageTableLevel.Pde : X64PageTableLevel.Pte;
        }

        uint pteIndex = GetIndex(rawVirtual, X64PageTableLevel.Pte);
        ulong targetEntryIndex = pageImages[currentPageIndex].EntryStartIndex + pteIndex;
        ulong previousRaw = entries[targetEntryIndex];
        X64PageTableEntryResult previous = X64PageTableEntry.TryDecode(X64PageTableLevel.Pte, previousRaw, request.Preconditions);
        if (!previous.Succeeded)
        {
            return Fail(X64PageMapStatus.MalformedEntry, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity, previous.Status, rootPageIndex: rootPageIndex, targetPageIndex: currentPageIndex, targetEntryIndex: targetEntryIndex);
        }

        X64PageTableEntry replacementEntry;
        ulong essentialIndex = FindContainingEssentialRange(rawVirtual, essentialRanges, essentialRangeCount);
        if (essentialIndex != X64PageMapResult.NoIndex)
        {
            return Fail(X64PageMapStatus.EssentialMappingMutationRejected, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity, rootPageIndex: rootPageIndex, targetPageIndex: currentPageIndex, targetEntryIndex: targetEntryIndex, invalidEssentialRangeIndex: essentialIndex);
        }

        if (request.Operation == X64PageMapOperation.Map)
        {
            if (previous.Kind != X64PageTableEntryKind.Unmapped)
            {
                return Fail(X64PageMapStatus.TargetAlreadyMapped, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity, rootPageIndex: rootPageIndex, targetPageIndex: currentPageIndex, targetEntryIndex: targetEntryIndex);
            }

            X64PageTableEntryResult encoded = X64PageTableEntry.TryEncode(new X64PageTableEntryRequest(X64PageTableLevel.Pte, X64PageTableEntryKind.Leaf, request.PhysicalAddress, request.Protection, request.CachePolicy), request.Preconditions);
            if (!encoded.Succeeded)
            {
                return Fail(X64PageMapStatus.EntryEncodingFailed, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity, encoded.Status, rootPageIndex: rootPageIndex, targetPageIndex: currentPageIndex, targetEntryIndex: targetEntryIndex);
            }

            replacementEntry = encoded.Entry;
        }
        else
        {
            if (previous.Kind != X64PageTableEntryKind.Leaf)
            {
                return Fail(X64PageMapStatus.TargetNotMapped, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity, rootPageIndex: rootPageIndex, targetPageIndex: currentPageIndex, targetEntryIndex: targetEntryIndex);
            }

            if (request.Operation == X64PageMapOperation.Unmap)
            {
                replacementEntry = default;
            }
            else
            {
                X64PageTableEntryResult replacement = X64PageTableEntry.TryReencodeLeafProtection(X64PageTableLevel.Pte, previousRaw, request.Protection, request.HardwareStatePolicy, request.Preconditions);
                if (!replacement.Succeeded)
                {
                    return Fail(X64PageMapStatus.ProtectionRewriteFailed, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity, replacement.Status, rootPageIndex: rootPageIndex, targetPageIndex: currentPageIndex, targetEntryIndex: targetEntryIndex);
                }

                replacementEntry = replacement.Entry;
                if (replacementEntry.RawValue == previousRaw)
                {
                    return Fail(X64PageMapStatus.ProtectionUnchanged, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity, X64PageTableEntryStatus.Success, rootPageIndex: rootPageIndex, targetPageIndex: currentPageIndex, targetEntryIndex: targetEntryIndex);
                }
            }
        }

        X64TlbInvalidationStatus invalidationStatus = X64TlbInvalidationPlan.TryCreate(request.VirtualAddress, PageSize, out X64TlbInvalidationRange invalidationRange);
        if (invalidationStatus != X64TlbInvalidationStatus.Success)
        {
            return Fail(X64PageMapStatus.InvalidationPlanFailed, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity, X64PageTableEntryStatus.Success, invalidationStatus, rootPageIndex, currentPageIndex, targetEntryIndex);
        }

        OS.Boot.PhysicalAddress pageImagePhysicalStart = pageImages[0].PhysicalStart;
        if (pageImages[pageImageCount - 1].PhysicalStart.TryGetEndExclusive(PageSize, out OS.Boot.PhysicalAddress pageImagePhysicalEndExclusive) != OS.Boot.AddressValueStatus.Success)
        {
            return Fail(X64PageMapStatus.ContractLayoutMismatch, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity, X64PageTableEntryStatus.Success, invalidationStatus, rootPageIndex, currentPageIndex, targetEntryIndex);
        }

        // This is the sole write. Every request, storage, table-walk,
        // essential-range, encoding, and invalidation condition has already
        // succeeded, so all failures above preserve every caller byte.
        entries[targetEntryIndex] = replacementEntry.RawValue;
        return X64PageMapResult.Success(request, pageImageCount, pageImageCapacity, pageImagePhysicalStart, pageImagePhysicalEndExclusive, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity, rootPageIndex, currentPageIndex, targetEntryIndex, previous.Entry, replacementEntry, invalidationRange);
    }

    private static X64PageMapResult ValidateStorage(X64PageMapRequest request, X64PageTableImagePage* pageImages, ulong pageImageCount, ulong pageImageCapacity, ulong* entries, ulong entryCount, ulong entryCapacity, X64EssentialMappingRange* essentialRanges, ulong essentialRangeCount, ulong essentialRangeCapacity)
    {
        if (pageImageCount == 0)
        {
            return Fail(X64PageMapStatus.InvalidPageImageCount, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity);
        }

        if (pageImageCount > MaximumPageImages)
        {
            return Fail(X64PageMapStatus.TooManyPageImages, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity);
        }

        if (pageImageCapacity < pageImageCount)
        {
            return Fail(X64PageMapStatus.PageImageCapacityTooSmall, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity);
        }

        if (pageImageCapacity > MaximumPageImages)
        {
            return Fail(X64PageMapStatus.PageImageCapacityExceedsLimit, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity);
        }

        if (pageImages == null)
        {
            return Fail(X64PageMapStatus.MissingPageImages, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity);
        }

        ulong pageImagesAddress = (ulong)pageImages;
        if ((pageImagesAddress & (RequiredPointerAlignment - 1)) != 0)
        {
            return Fail(X64PageMapStatus.MisalignedPageImages, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity);
        }

        if (!TryGetSpanEnd(pageImagesAddress, pageImageCapacity, X64PageTableImagePage.StructureSize, out ulong pageImagesEnd))
        {
            return Fail(X64PageMapStatus.PageImageSpanOverflow, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity);
        }

        if (entryCount == 0)
        {
            return Fail(X64PageMapStatus.InvalidEntryCount, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity);
        }

        if (pageImageCount > ~0UL / X64PageTableImagePage.EntryCountPerPage || entryCount != pageImageCount * X64PageTableImagePage.EntryCountPerPage)
        {
            return Fail(X64PageMapStatus.EntryCountMismatch, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity);
        }

        if (entryCapacity < entryCount)
        {
            return Fail(X64PageMapStatus.EntryCapacityTooSmall, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity);
        }

        if (entryCapacity > MaximumEntryCount)
        {
            return Fail(X64PageMapStatus.EntryCapacityExceedsLimit, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity);
        }

        if (entries == null)
        {
            return Fail(X64PageMapStatus.MissingEntries, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity);
        }

        ulong entriesAddress = (ulong)entries;
        if ((entriesAddress & (RequiredPointerAlignment - 1)) != 0)
        {
            return Fail(X64PageMapStatus.MisalignedEntries, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity);
        }

        if (!TryGetSpanEnd(entriesAddress, entryCapacity, 8, out ulong entriesEnd))
        {
            return Fail(X64PageMapStatus.EntrySpanOverflow, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity);
        }

        if (RangesOverlap(pageImagesAddress, pageImagesEnd, entriesAddress, entriesEnd))
        {
            return Fail(X64PageMapStatus.StorageOverlap, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity);
        }

        if (essentialRangeCount > MaximumEssentialRangeCount)
        {
            return Fail(X64PageMapStatus.TooManyEssentialRanges, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity);
        }

        if (essentialRangeCapacity < essentialRangeCount)
        {
            return Fail(X64PageMapStatus.EssentialRangeCapacityTooSmall, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity);
        }

        if (essentialRangeCapacity > MaximumEssentialRangeCount)
        {
            return Fail(X64PageMapStatus.EssentialRangeCapacityExceedsLimit, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity);
        }

        ulong essentialAddress = 0;
        ulong essentialEnd = 0;
        if (essentialRangeCapacity != 0)
        {
            if (essentialRanges == null)
            {
                return Fail(X64PageMapStatus.MissingEssentialRanges, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity);
            }

            essentialAddress = (ulong)essentialRanges;
            if ((essentialAddress & (RequiredPointerAlignment - 1)) != 0)
            {
                return Fail(X64PageMapStatus.MisalignedEssentialRanges, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity);
            }

            if (!TryGetSpanEnd(essentialAddress, essentialRangeCapacity, X64EssentialMappingRange.StructureSize, out essentialEnd))
            {
                return Fail(X64PageMapStatus.EssentialRangeSpanOverflow, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity);
            }

            if (RangesOverlap(essentialAddress, essentialEnd, pageImagesAddress, pageImagesEnd) || RangesOverlap(essentialAddress, essentialEnd, entriesAddress, entriesEnd))
            {
                return Fail(X64PageMapStatus.EssentialRangeStorageOverlap, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity);
            }
        }
        else if (essentialRangeCount != 0)
        {
            return Fail(X64PageMapStatus.InvalidEssentialRangeCount, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity);
        }

        ulong previousPhysicalStart = 0;
        for (ulong index = 0; index < pageImageCount; index++)
        {
            X64PageMapStatus pageStatus = pageImages[index].Validate(index, entryCount, request.Preconditions.PhysicalAddressWidth);
            if (pageStatus != X64PageMapStatus.Success)
            {
                return Fail(pageStatus, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity, invalidPageImageIndex: index);
            }

            pageImages[index].PhysicalStart.TryGetRawValueForHardwareRegisterBoundary(out ulong rawPhysicalStart);
            if (index != 0)
            {
                if (rawPhysicalStart <= previousPhysicalStart)
                {
                    return Fail(X64PageMapStatus.PageImageOrderInvalid, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity, invalidPageImageIndex: index);
                }

                if (previousPhysicalStart > ~0UL - PageSize || rawPhysicalStart < previousPhysicalStart + PageSize)
                {
                    return Fail(X64PageMapStatus.PageImagePhysicalOverlap, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity, invalidPageImageIndex: index);
                }
            }

            previousPhysicalStart = rawPhysicalStart;
        }

        ulong previousEssentialEnd = 0;
        for (ulong index = 0; index < essentialRangeCount; index++)
        {
            X64PageMapStatus rangeStatus = essentialRanges[index].Validate();
            if (rangeStatus != X64PageMapStatus.Success)
            {
                return Fail(rangeStatus, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity, invalidEssentialRangeIndex: index);
            }

            essentialRanges[index].VirtualStart.TryGetRawValueForHardwareRegisterBoundary(out ulong rawStart);
            essentialRanges[index].VirtualEndExclusive.TryGetRawValueForHardwareRegisterBoundary(out ulong rawEnd);
            if (index != 0 && rawStart < previousEssentialEnd)
            {
                return Fail(X64PageMapStatus.EssentialRangeOrderInvalid, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity, invalidEssentialRangeIndex: index);
            }

            previousEssentialEnd = rawEnd;
        }

        return default;
    }

    private static X64PageMapResult Fail(X64PageMapStatus status, X64PageMapRequest request, ulong pageImageCount, ulong pageImageCapacity, ulong entryCount, ulong entryCapacity, ulong essentialRangeCount, ulong essentialRangeCapacity, X64PageTableEntryStatus entryStatus = X64PageTableEntryStatus.Uninitialized, X64TlbInvalidationStatus invalidationStatus = X64TlbInvalidationStatus.Uninitialized, ulong rootPageIndex = X64PageMapResult.NoIndex, ulong targetPageIndex = X64PageMapResult.NoIndex, ulong targetEntryIndex = X64PageMapResult.NoIndex, ulong invalidPageImageIndex = X64PageMapResult.NoIndex, ulong invalidEssentialRangeIndex = X64PageMapResult.NoIndex)
    {
        return X64PageMapResult.Fail(status, request, pageImageCount, pageImageCapacity, entryCount, entryCapacity, essentialRangeCount, essentialRangeCapacity, entryStatus, invalidationStatus, rootPageIndex, targetPageIndex, targetEntryIndex, invalidPageImageIndex, invalidEssentialRangeIndex);
    }

    private static ulong FindPageImage(OS.Boot.PhysicalAddress physicalAddress, X64PageTableImagePage* pageImages, ulong pageImageCount)
    {
        for (ulong index = 0; index < pageImageCount; index++)
        {
            if (pageImages[index].PhysicalStart.Equals(physicalAddress))
            {
                return index;
            }
        }

        return X64PageMapResult.NoIndex;
    }

    private static ulong FindContainingEssentialRange(ulong rawVirtual, X64EssentialMappingRange* ranges, ulong rangeCount)
    {
        for (ulong index = 0; index < rangeCount; index++)
        {
            ranges[index].VirtualStart.TryGetRawValueForHardwareRegisterBoundary(out ulong rawStart);
            ranges[index].VirtualEndExclusive.TryGetRawValueForHardwareRegisterBoundary(out ulong rawEnd);
            if (rawVirtual >= rawStart && rawVirtual < rawEnd)
            {
                return index;
            }
        }

        return X64PageMapResult.NoIndex;
    }

    private static uint GetIndex(ulong virtualAddress, X64PageTableLevel level)
    {
        if (level == X64PageTableLevel.Pml4e)
        {
            return (uint)((virtualAddress >> 39) & 0x1ffUL);
        }

        if (level == X64PageTableLevel.Pdpte)
        {
            return (uint)((virtualAddress >> 30) & 0x1ffUL);
        }

        if (level == X64PageTableLevel.Pde)
        {
            return (uint)((virtualAddress >> 21) & 0x1ffUL);
        }

        return (uint)((virtualAddress >> 12) & 0x1ffUL);
    }

    private static bool RangesOverlap(ulong firstStart, ulong firstEnd, ulong secondStart, ulong secondEnd)
    {
        return firstStart < secondEnd && secondStart < firstEnd;
    }

    private static bool TryGetSpanEnd(ulong start, ulong capacity, ulong elementSize, out ulong end)
    {
        end = 0;
        if (capacity != 0 && elementSize > ~0UL / capacity)
        {
            return false;
        }

        ulong byteLength = capacity * elementSize;
        if (start > ~0UL - byteLength)
        {
            return false;
        }

        end = start + byteLength;
        return true;
    }
}
