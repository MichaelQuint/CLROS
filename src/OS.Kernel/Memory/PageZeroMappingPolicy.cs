namespace OS.Kernel.Memory;
/// <summary>
/// Validates raw mapping intents and audits page zero in a bounded,
/// caller-owned four-level x64 table image. It never reads CR3, touches live
/// page tables, mutates caller storage, or performs TLB invalidation.
/// </summary>

public static unsafe class PageZeroMappingPolicy
{
    public const ulong PageSize = 4096;
    public const ulong MaximumPageImages = 64;
    public const ulong MaximumEntryCount = MaximumPageImages * X64PageTableImagePage.EntryCountPerPage;
    public const ulong RequiredPointerAlignment = 8;
    public const ulong LowCanonicalEndExclusive = 0x0000_8000_0000_0000UL;
    public const bool PerformsLiveMutation = false;
    public const bool WritesTableImage = false;
    public const bool ReadsLivePageTables = false;
    public const bool ProvesInstalledRoot = false;
    public static PageZeroMappingPolicyResult ValidateMappingIntent(PageZeroMappingIntent intent)
    {
        if ((ulong)sizeof(PageZeroMappingIntent) != PageZeroMappingIntent.StructureSize || (ulong)sizeof(PageZeroMappingPolicyResult) != PageZeroMappingPolicyResult.StructureSize)
        {
            return PageZeroMappingPolicyResult.IntentResult(PageZeroMappingPolicyStatus.ContractLayoutMismatch, intent);
        }

        return PageZeroMappingPolicyResult.IntentResult(intent.Validate(), intent);
    }

    public static PageZeroMappingPolicyResult AuditTableImage(OS.Boot.PhysicalAddress rootTable, X64PageTableEncodingPreconditions preconditions, X64PageTableImagePage* pageImages, ulong pageImageCount, ulong pageImageCapacity, ulong* entries, ulong entryCount, ulong entryCapacity)
    {
        PageZeroMappingPolicyStatus status = ValidateAuditStorage(rootTable, preconditions, pageImages, pageImageCount, pageImageCapacity, entries, entryCount, entryCapacity, out ulong rootPageIndex);
        if (status != PageZeroMappingPolicyStatus.Success)
        {
            return AuditFailure(status, rootTable, pageImageCount, pageImageCapacity, entryCount, entryCapacity, rootPageIndex);
        }

        ulong currentPageIndex = rootPageIndex;
        ulong inspectedEntryCount = 0;
        X64PageTableLevel level = X64PageTableLevel.Pml4e;
        while (level >= X64PageTableLevel.Pte)
        {
            ulong terminalEntryIndex = pageImages[currentPageIndex].EntryStartIndex;
            ulong rawEntry = entries[terminalEntryIndex];
            inspectedEntryCount++;
            if (rawEntry == 0)
            {
                return PageZeroMappingPolicyResult.AuditResult(PageZeroMappingPolicyStatus.Success, X64PageTableEntryStatus.Success, level, rootTable, pageImageCount, pageImageCapacity, entryCount, entryCapacity, rootPageIndex, currentPageIndex, terminalEntryIndex, inspectedEntryCount, rawEntry, 0);
            }

            if ((rawEntry & 1UL) == 0)
            {
                X64PageTableEntryResult malformed = X64PageTableEntry.TryDecode(level, rawEntry, preconditions);
                return PageZeroMappingPolicyResult.AuditResult(PageZeroMappingPolicyStatus.MalformedEntry, malformed.Status, X64PageTableLevel.Uninitialized, rootTable, pageImageCount, pageImageCapacity, entryCount, entryCapacity, rootPageIndex, currentPageIndex, terminalEntryIndex, inspectedEntryCount, rawEntry, 0);
            }

            bool presentLeaf = level == X64PageTableLevel.Pte || ((level == X64PageTableLevel.Pde || level == X64PageTableLevel.Pdpte) && (rawEntry & (1UL << 7)) != 0);
            if (presentLeaf)
            {
                ulong leafPageSize = level == X64PageTableLevel.Pte ? X64PageTableEntry.PageSize4KiB : level == X64PageTableLevel.Pde ? X64PageTableEntry.PageSize2MiB : X64PageTableEntry.PageSize1GiB;
                return PageZeroMappingPolicyResult.AuditResult(PageZeroMappingPolicyStatus.PageZeroPresent, X64PageTableEntryStatus.Success, X64PageTableLevel.Uninitialized, rootTable, pageImageCount, pageImageCapacity, entryCount, entryCapacity, rootPageIndex, currentPageIndex, terminalEntryIndex, inspectedEntryCount, rawEntry, leafPageSize);
            }

            X64PageTableEntryResult decoded = X64PageTableEntry.TryDecode(level, rawEntry, preconditions);
            if (!decoded.Succeeded || decoded.Kind != X64PageTableEntryKind.TablePointer || decoded.PhysicalAddress.TryGetRawValueForHardwareRegisterBoundary(out ulong childPhysical) != OS.Boot.AddressValueStatus.Success)
            {
                return PageZeroMappingPolicyResult.AuditResult(PageZeroMappingPolicyStatus.MalformedEntry, decoded.Status, X64PageTableLevel.Uninitialized, rootTable, pageImageCount, pageImageCapacity, entryCount, entryCapacity, rootPageIndex, currentPageIndex, terminalEntryIndex, inspectedEntryCount, rawEntry, 0);
            }

            ulong childPageIndex = FindPageImage(pageImages, pageImageCount, childPhysical);
            if (childPageIndex == PageZeroMappingPolicyResult.NoIndex)
            {
                return PageZeroMappingPolicyResult.AuditResult(PageZeroMappingPolicyStatus.TablePointerTargetMissing, X64PageTableEntryStatus.Success, X64PageTableLevel.Uninitialized, rootTable, pageImageCount, pageImageCapacity, entryCount, entryCapacity, rootPageIndex, currentPageIndex, terminalEntryIndex, inspectedEntryCount, rawEntry, 0);
            }

            currentPageIndex = childPageIndex;
            level = (X64PageTableLevel)((byte)level - 1);
        }

        return AuditFailure(PageZeroMappingPolicyStatus.MalformedEntry, rootTable, pageImageCount, pageImageCapacity, entryCount, entryCapacity, rootPageIndex);
    }

    private static PageZeroMappingPolicyStatus ValidateAuditStorage(OS.Boot.PhysicalAddress rootTable, X64PageTableEncodingPreconditions preconditions, X64PageTableImagePage* pageImages, ulong pageImageCount, ulong pageImageCapacity, ulong* entries, ulong entryCount, ulong entryCapacity, out ulong rootPageIndex)
    {
        rootPageIndex = PageZeroMappingPolicyResult.NoIndex;
        if ((ulong)sizeof(PageZeroMappingIntent) != PageZeroMappingIntent.StructureSize || (ulong)sizeof(PageZeroMappingPolicyResult) != PageZeroMappingPolicyResult.StructureSize || (ulong)sizeof(X64PageTableImagePage) != X64PageTableImagePage.StructureSize)
        {
            return PageZeroMappingPolicyStatus.ContractLayoutMismatch;
        }

        if (!rootTable.IsValid || !rootTable.IsAligned(PageSize))
        {
            return PageZeroMappingPolicyStatus.InvalidRootTable;
        }

        if (preconditions.Validate() != X64PageTableEntryStatus.Success)
        {
            return PageZeroMappingPolicyStatus.InvalidPreconditions;
        }

        if (pageImageCount == 0)
        {
            return PageZeroMappingPolicyStatus.InvalidPageImageCount;
        }

        if (pageImageCount > MaximumPageImages)
        {
            return PageZeroMappingPolicyStatus.TooManyPageImages;
        }

        if (pageImageCapacity < pageImageCount)
        {
            return PageZeroMappingPolicyStatus.PageImageCapacityTooSmall;
        }

        if (pageImageCapacity > MaximumPageImages)
        {
            return PageZeroMappingPolicyStatus.PageImageCapacityExceedsLimit;
        }

        if (pageImages == null)
        {
            return PageZeroMappingPolicyStatus.MissingPageImages;
        }

        ulong pageImagesAddress = (ulong)pageImages;
        if ((pageImagesAddress & (RequiredPointerAlignment - 1)) != 0)
        {
            return PageZeroMappingPolicyStatus.MisalignedPageImages;
        }

        if (!TryGetSpanEnd(pageImagesAddress, pageImageCapacity, X64PageTableImagePage.StructureSize, out ulong pageImagesEnd))
        {
            return PageZeroMappingPolicyStatus.PageImageSpanOverflow;
        }

        if (entryCount == 0)
        {
            return PageZeroMappingPolicyStatus.InvalidEntryCount;
        }

        if (pageImageCount > ~0UL / X64PageTableImagePage.EntryCountPerPage || entryCount != pageImageCount * X64PageTableImagePage.EntryCountPerPage)
        {
            return PageZeroMappingPolicyStatus.EntryCountMismatch;
        }

        if (entryCapacity < entryCount)
        {
            return PageZeroMappingPolicyStatus.EntryCapacityTooSmall;
        }

        if (entryCapacity > MaximumEntryCount)
        {
            return PageZeroMappingPolicyStatus.EntryCapacityExceedsLimit;
        }

        if (entries == null)
        {
            return PageZeroMappingPolicyStatus.MissingEntries;
        }

        ulong entriesAddress = (ulong)entries;
        if ((entriesAddress & (RequiredPointerAlignment - 1)) != 0)
        {
            return PageZeroMappingPolicyStatus.MisalignedEntries;
        }

        if (!TryGetSpanEnd(entriesAddress, entryCapacity, 8, out ulong entriesEnd))
        {
            return PageZeroMappingPolicyStatus.EntrySpanOverflow;
        }

        if (RangesOverlap(pageImagesAddress, pageImagesEnd, entriesAddress, entriesEnd))
        {
            return PageZeroMappingPolicyStatus.StorageOverlap;
        }

        ulong previousPhysicalStart = 0;
        for (ulong index = 0; index < pageImageCount; index++)
        {
            X64PageMapStatus pageStatus = pageImages[index].Validate(index, entryCount, preconditions.PhysicalAddressWidth);
            if (pageStatus != X64PageMapStatus.Success)
            {
                return PageZeroMappingPolicyStatus.InvalidPageImageRecord;
            }

            pageImages[index].PhysicalStart.TryGetRawValueForHardwareRegisterBoundary(out ulong rawPhysicalStart);
            if (index != 0)
            {
                if (rawPhysicalStart <= previousPhysicalStart)
                {
                    return PageZeroMappingPolicyStatus.PageImageOrderInvalid;
                }
            }

            previousPhysicalStart = rawPhysicalStart;
            if (pageImages[index].PhysicalStart.Equals(rootTable))
            {
                rootPageIndex = index;
            }
        }

        if (rootPageIndex == PageZeroMappingPolicyResult.NoIndex)
        {
            return PageZeroMappingPolicyStatus.RootPageNotFound;
        }

        return PageZeroMappingPolicyStatus.Success;
    }

    private static PageZeroMappingPolicyResult AuditFailure(PageZeroMappingPolicyStatus status, OS.Boot.PhysicalAddress rootTable, ulong pageImageCount, ulong pageImageCapacity, ulong entryCount, ulong entryCapacity, ulong rootPageIndex)
    {
        return PageZeroMappingPolicyResult.AuditResult(status, X64PageTableEntryStatus.Uninitialized, X64PageTableLevel.Uninitialized, rootTable, pageImageCount, pageImageCapacity, entryCount, entryCapacity, rootPageIndex, PageZeroMappingPolicyResult.NoIndex, PageZeroMappingPolicyResult.NoIndex, 0, 0, 0);
    }

    private static ulong FindPageImage(X64PageTableImagePage* pageImages, ulong pageImageCount, ulong rawPhysicalStart)
    {
        for (ulong index = 0; index < pageImageCount; index++)
        {
            pageImages[index].PhysicalStart.TryGetRawValueForHardwareRegisterBoundary(out ulong candidate);
            if (candidate == rawPhysicalStart)
            {
                return index;
            }
        }

        return PageZeroMappingPolicyResult.NoIndex;
    }

    private static bool TryGetSpanEnd(ulong start, ulong count, ulong elementSize, out ulong end)
    {
        end = 0;
        if (count > ~0UL / elementSize)
        {
            return false;
        }

        ulong byteLength = count * elementSize;
        if (start > ~0UL - byteLength)
        {
            return false;
        }

        end = start + byteLength;
        return true;
    }

    private static bool RangesOverlap(ulong firstStart, ulong firstEnd, ulong secondStart, ulong secondEnd)
    {
        return firstStart < secondEnd && secondStart < firstEnd;
    }
}
