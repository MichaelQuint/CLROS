using OS.Boot;
using OS.Kernel.Memory;

namespace OS.Kernel.Platform;

public enum PhysicalMappingUsage : byte
{
    Uninitialized = 0,
    Normal = 1,
    Device = 2,
    Framebuffer = 3
}

public enum PhysicalMappingPreparationStatus : byte
{
    Uninitialized = 0,
    Prepared = 1,
    InvalidUsage = 2,
    InvalidRange = 3,
    InvalidInheritedEntry = 4,
    RangeOutsideLargePage = 5,
    InvalidTableAddress = 6,
    TableOverlapsMappedRange = 7,
    InvalidDestination = 8,
    UnsupportedCachePolicy = 9,
    InvalidTableArena = 10,
    MalformedTable = 11,
    NonIdentityMapping = 12,
    SharedOrCyclicTable = 13,
    UnmappedTableStorage = 14,
    NoFreeTablePage = 15,
    TargetNotLargePage = 16,
    NonzeroUnusedTable = 17,
    UnsupportedCacheEnvironment = 18,
    InvalidMtrrSnapshot = 19,
    MtrrRangeNotWriteBack = 20
}

public readonly struct PhysicalMappingPreparationResult
{
    internal PhysicalMappingPreparationResult(
        PhysicalMappingPreparationStatus status,
        ulong replacementPde = 0,
        X64PageTableEntryStatus entryStatus = X64PageTableEntryStatus.Uninitialized,
        uint parentEntryIndex = uint.MaxValue,
        uint childTablePageIndex = uint.MaxValue,
        ulong originalPde = 0)
    {
        Status = status;
        ReplacementPde = replacementPde;
        EntryStatus = entryStatus;
        ParentEntryIndex = parentEntryIndex;
        ChildTablePageIndex = childTablePageIndex;
        OriginalPde = originalPde;
    }

    public PhysicalMappingPreparationStatus Status { get; }
    public bool IsPrepared => Status == PhysicalMappingPreparationStatus.Prepared;
    public ulong ReplacementPde { get; }
    public X64PageTableEntryStatus EntryStatus { get; }
    public uint ParentEntryIndex { get; }
    public uint ChildTablePageIndex { get; }
    public ulong OriginalPde { get; }
}

/// <summary>
/// Prepares detached page-table bytes; it never publishes a mapping. The caller
/// must own the destination exclusively and keep it separate from live tables.
/// Alias admission, MTRR/PAT compatibility, ownership, publication and cache/TLB
/// maintenance must be completed before these bytes can become a live mapping.
/// </summary>
public static class PostFirmwarePhysicalMappingExplicitUCWCWB
{
    public const uint EntriesPerTable = 512;
    public const ulong PageBytes = 4096;
    public const ulong LargePageBytes = EntriesPerTable * PageBytes;
    public const uint MaximumTablePages = 64;
    public const uint MaximumVariableMtrrs = 32;

    /// <summary>
    /// Checks an observed variable-MTRR snapshot for uniform WB coverage above
    /// 1 MiB. This is the underlying type required before selecting WB/UC/WC
    /// through PAT. The live adapter must read the registers under exclusive
    /// ownership and reject TME/SMRR support for this initial bounded profile.
    /// This helper reads no hardware and does not itself authorize publication.
    /// </summary>
    public static unsafe PhysicalMappingPreparationStatus ValidateWriteBackMtrrRange(
        PhysicalAddress physicalStart, ulong byteLength, byte physicalAddressWidth,
        ulong rawCr0, ulong rawMtrrCap, ulong rawMtrrDefaultType, bool tmeSupported,
        ulong* variableBases, ulong* variableMasks, uint variableCount)
    {
        if (physicalAddressWidth < 32 || physicalAddressWidth > 52
            || (rawCr0 & 0x60000000) != 0 || tmeSupported || (rawMtrrCap & (1UL << 11)) != 0)
            return PhysicalMappingPreparationStatus.UnsupportedCacheEnvironment;
        if (physicalStart.TryGetRawValueForHardwareRegisterBoundary(out ulong start)
                != AddressValueStatus.Success
            || start < 0x100000 || (start & (PageBytes - 1)) != 0
            || byteLength == 0 || (byteLength & (PageBytes - 1)) != 0
            || start >= (1UL << physicalAddressWidth)
            || byteLength > (1UL << physicalAddressWidth) - start)
            return PhysicalMappingPreparationStatus.InvalidRange;
        if ((rawMtrrDefaultType & ~0xCFFUL) != 0 || !IsMtrrType((byte)rawMtrrDefaultType)
            || ((byte)rawMtrrDefaultType == 1 && (rawMtrrCap & 0x400) == 0)
            || ((rawMtrrDefaultType & 0x400) != 0 && (rawMtrrCap & 0x100) == 0)
            || (rawMtrrDefaultType & 0x800) == 0
            || variableCount != (rawMtrrCap & 0xFF) || variableCount > MaximumVariableMtrrs)
            return PhysicalMappingPreparationStatus.InvalidMtrrSnapshot;
        if (variableCount != 0 && (variableBases == null || variableMasks == null
            || (((ulong)variableBases | (ulong)variableMasks) & 7) != 0
            || (ulong)variableBases > ulong.MaxValue - variableCount * sizeof(ulong)
            || (ulong)variableMasks > ulong.MaxValue - variableCount * sizeof(ulong)))
            return PhysicalMappingPreparationStatus.InvalidMtrrSnapshot;

        ulong addressMask = ((1UL << physicalAddressWidth) - 1) & ~(PageBytes - 1);
        ulong end = start + byteLength;
        for (uint i = 0; i < variableCount; i++)
        {
            ulong rawMask = variableMasks[i], rawBase = variableBases[i];
            if ((rawMask & 0x800) == 0) continue;
            ulong mask = rawMask & addressMask;
            ulong size = ((~mask) & (addressMask | (PageBytes - 1))) + 1;
            ulong rangeStart = rawBase & addressMask;
            if ((rawMask & ~(addressMask | 0x800)) != 0
                || (rawBase & ~(addressMask | 0xFF)) != 0 || !IsMtrrType((byte)rawBase)
                || ((byte)rawBase == 1 && (rawMtrrCap & 0x400) == 0)
                || size < PageBytes || (size & (size - 1)) != 0 || (rangeStart & (size - 1)) != 0)
                return PhysicalMappingPreparationStatus.InvalidMtrrSnapshot;
            if (rangeStart < end && start < rangeStart + size && (byte)rawBase != 6)
                return PhysicalMappingPreparationStatus.MtrrRangeNotWriteBack;
        }
        if ((byte)rawMtrrDefaultType == 6) return PhysicalMappingPreparationStatus.Prepared;

        // Default UC (common in firmware) is acceptable only when WB variable
        // ranges cover the whole target. Each advance reaches a range endpoint,
        // so at most variableCount advances are possible, independent of size.
        ulong cursor = start;
        for (uint pass = 0; pass < variableCount && cursor < end; pass++)
        {
            ulong coveredEnd = cursor;
            for (uint i = 0; i < variableCount; i++)
            {
                ulong rawMask = variableMasks[i], rawBase = variableBases[i];
                if ((rawMask & 0x800) == 0 || (byte)rawBase != 6) continue;
                ulong rangeStart = rawBase & addressMask;
                ulong size = ((~(rawMask & addressMask)) & (addressMask | (PageBytes - 1))) + 1;
                if (rangeStart <= cursor && rangeStart + size > coveredEnd) coveredEnd = rangeStart + size;
            }
            if (coveredEnd == cursor) return PhysicalMappingPreparationStatus.MtrrRangeNotWriteBack;
            cursor = coveredEnd;
        }
        return cursor >= end ? PhysicalMappingPreparationStatus.Prepared
            : PhysicalMappingPreparationStatus.MtrrRangeNotWriteBack;
    }

    private static bool IsMtrrType(byte value) => value == 0 || value == 1 || value == 4 || value == 5 || value == 6;

    /// <summary>
    /// Audits a quiescent four-level identity-map image and prepares its selected
    /// 2 MiB leaf split in an unused table page. The root is arena page zero.
    /// All reachable tables must belong to this contiguous caller-owned arena;
    /// every arena page must have an identity WB/RW/NX mapping. Aliases, shared
    /// tables, cycles and nonzero unreachable pages are rejected. The live parent
    /// is never written. A caller must bind the image to its actual owned CR3 and
    /// perform the cache transition before publishing ReplacementPde.
    /// </summary>
    public static unsafe PhysicalMappingPreparationResult PrepareIdentitySplit(
        PhysicalAddress tableArenaPhysicalAddress,
        ulong* tableEntries,
        uint tablePageCount,
        PhysicalAddress physicalStart,
        ulong byteLength,
        PhysicalMappingUsage usage,
        X64PageTableEncodingPreconditions preconditions)
    {
        if (GetCachePolicy(usage) == PageCachePolicy.Uninitialized)
            return new(PhysicalMappingPreparationStatus.InvalidUsage);
        if (physicalStart.TryGetRawValueForHardwareRegisterBoundary(out ulong start)
                != AddressValueStatus.Success
            || (start & (PageBytes - 1)) != 0 || byteLength == 0
            || (byteLength & (PageBytes - 1)) != 0 || byteLength > ulong.MaxValue - start)
            return new(PhysicalMappingPreparationStatus.InvalidRange);

        X64PageTableEntryStatus conditionStatus = preconditions.Validate();
        if (conditionStatus != X64PageTableEntryStatus.Success)
            return new(PhysicalMappingPreparationStatus.InvalidTableArena, entryStatus: conditionStatus);
        ulong arenaBytes = tablePageCount * PageBytes;
        if (tablePageCount == 0 || tablePageCount > MaximumTablePages
            || tableEntries == null || ((ulong)tableEntries & 7) != 0
            || (ulong)tableEntries > ulong.MaxValue - arenaBytes
            || tableArenaPhysicalAddress.TryGetRawValueForHardwareRegisterBoundary(out ulong arenaStart)
                != AddressValueStatus.Success
            || (arenaStart & (PageBytes - 1)) != 0
            || arenaStart >= (1UL << preconditions.PhysicalAddressWidth)
            || arenaBytes > (1UL << preconditions.PhysicalAddressWidth) - arenaStart)
            return new(PhysicalMappingPreparationStatus.InvalidTableArena);

        ulong visited = 0, mappedStorage = 0;
        uint parentIndex = uint.MaxValue;
        PhysicalMappingPreparationStatus status = AuditIdentityTable(tableEntries, tablePageCount,
            arenaStart, 0, X64PageTableLevel.Pml4e, 0, start, preconditions,
            ref visited, ref mappedStorage, ref parentIndex);
        if (status != PhysicalMappingPreparationStatus.Prepared) return new(status);
        ulong allPages = tablePageCount == 64 ? ulong.MaxValue : (1UL << (int)tablePageCount) - 1;
        if (mappedStorage != allPages) return new(PhysicalMappingPreparationStatus.UnmappedTableStorage);
        if (parentIndex == uint.MaxValue) return new(PhysicalMappingPreparationStatus.TargetNotLargePage);
        // Publication temporarily removes the entire large leaf. Neither its
        // selected cache range nor its unchanged neighbors may contain tables
        // needed to access the parent during that transition.
        ulong largeStart = start & ~(LargePageBytes - 1);
        if (largeStart < arenaStart + arenaBytes && arenaStart < largeStart + LargePageBytes)
            return new(PhysicalMappingPreparationStatus.TableOverlapsMappedRange);

        uint freePage = uint.MaxValue;
        for (uint page = 0; page < tablePageCount; page++)
        {
            if ((visited & (1UL << (int)page)) != 0) continue;
            for (uint slot = 0; slot < EntriesPerTable; slot++)
                if (tableEntries[page * EntriesPerTable + slot] != 0)
                    return new(PhysicalMappingPreparationStatus.NonzeroUnusedTable);
            if (freePage == uint.MaxValue) freePage = page;
        }
        if (freePage == uint.MaxValue) return new(PhysicalMappingPreparationStatus.NoFreeTablePage);

        ulong original = tableEntries[parentIndex];
        PhysicalAddress.TryCreateFromHardwareRegisterBoundary(arenaStart + freePage * PageBytes,
            out PhysicalAddress childAddress);
        PhysicalMappingPreparationResult split = PrepareTwoMiBSplit(original, physicalStart, byteLength,
            usage, childAddress, tableEntries + freePage * EntriesPerTable, EntriesPerTable, preconditions);
        return !split.IsPrepared ? split : new(PhysicalMappingPreparationStatus.Prepared,
            split.ReplacementPde, split.EntryStatus, parentIndex, freePage, original);
    }

    private static unsafe PhysicalMappingPreparationStatus AuditIdentityTable(
        ulong* entries, uint pageCount, ulong arenaStart, uint pageIndex,
        X64PageTableLevel level, ulong virtualBase, ulong target,
        X64PageTableEncodingPreconditions preconditions,
        ref ulong visited, ref ulong mappedStorage, ref uint targetParentIndex)
    {
        ulong pageBit = 1UL << (int)pageIndex;
        if ((visited & pageBit) != 0) return PhysicalMappingPreparationStatus.SharedOrCyclicTable;
        visited |= pageBit;
        ulong arenaEnd = arenaStart + pageCount * PageBytes;
        int shift = 12 + 9 * ((int)level - 1);
        for (uint slot = 0; slot < EntriesPerTable; slot++)
        {
            uint index = pageIndex * EntriesPerTable + slot;
            ulong raw = entries[index];
            if (raw == 0) continue;
            // This owned-map profile admits low canonical identity addresses.
            if (level == X64PageTableLevel.Pml4e && slot >= 256)
                return PhysicalMappingPreparationStatus.NonIdentityMapping;
            X64PageTableEntryResult decoded = X64PageTableEntry.TryDecode(level, raw, preconditions);
            if (!decoded.Succeeded) return PhysicalMappingPreparationStatus.MalformedTable;
            decoded.PhysicalAddress.TryGetRawValueForHardwareRegisterBoundary(out ulong address);
            ulong virtualStart = virtualBase | ((ulong)slot << shift);
            if (decoded.Kind == X64PageTableEntryKind.TablePointer)
            {
                if (address < arenaStart || address >= arenaEnd)
                    return PhysicalMappingPreparationStatus.InvalidTableArena;
                PhysicalMappingPreparationStatus child = AuditIdentityTable(entries, pageCount, arenaStart,
                    (uint)((address - arenaStart) / PageBytes), (X64PageTableLevel)((byte)level - 1),
                    virtualStart, target, preconditions, ref visited, ref mappedStorage, ref targetParentIndex);
                if (child != PhysicalMappingPreparationStatus.Prepared) return child;
                continue;
            }
            if (address != virtualStart) return PhysicalMappingPreparationStatus.NonIdentityMapping;
            ulong end = address + decoded.PageSizeBytes;
            if (level == X64PageTableLevel.Pde && target >= address && target < end)
                targetParentIndex = index;
            if (address < arenaEnd && arenaStart < end)
            {
                if (decoded.CachePolicy != PageCachePolicy.WriteBack
                    || decoded.Protection != (PageProtection.Read | PageProtection.Write))
                    return PhysicalMappingPreparationStatus.UnmappedTableStorage;
                ulong overlapStart = address > arenaStart ? address : arenaStart;
                ulong overlapEnd = end < arenaEnd ? end : arenaEnd;
                for (ulong current = overlapStart; current < overlapEnd; current += PageBytes)
                    mappedStorage |= 1UL << (int)((current - arenaStart) / PageBytes);
            }
        }
        return PhysicalMappingPreparationStatus.Prepared;
    }

    public static PageCachePolicy GetCachePolicy(PhysicalMappingUsage usage)
    {
        return usage switch
        {
            PhysicalMappingUsage.Normal => PageCachePolicy.WriteBack,
            PhysicalMappingUsage.Device => PageCachePolicy.Uncached,
            PhysicalMappingUsage.Framebuffer => PageCachePolicy.WriteCombining,
            _ => PageCachePolicy.Uninitialized
        };
    }

    /// <summary>
    /// Splits one admitted 2 MiB leaf into 512 detached 4 KiB entries, changing
    /// cache policy only inside the requested physical range. Permissions and
    /// physical translations are preserved. The large leaf's A/D observations
    /// conservatively apply to every child. Failure leaves destination untouched.
    /// Only the strict, existing x64 entry decoder's admitted subset is supported.
    /// </summary>
    public static unsafe PhysicalMappingPreparationResult PrepareTwoMiBSplit(
        ulong inheritedPde,
        PhysicalAddress physicalStart,
        ulong byteLength,
        PhysicalMappingUsage usage,
        PhysicalAddress tablePhysicalAddress,
        ulong* destination,
        uint destinationEntryCapacity,
        X64PageTableEncodingPreconditions preconditions)
    {
        PageCachePolicy policy = GetCachePolicy(usage);
        if (policy == PageCachePolicy.Uninitialized)
        {
            return new(PhysicalMappingPreparationStatus.InvalidUsage);
        }

        if (physicalStart.TryGetRawValueForHardwareRegisterBoundary(out ulong start)
                != AddressValueStatus.Success
            || (start & (PageBytes - 1)) != 0
            || byteLength == 0
            || (byteLength & (PageBytes - 1)) != 0
            || byteLength > ulong.MaxValue - start)
        {
            return new(PhysicalMappingPreparationStatus.InvalidRange);
        }

        X64PageTableEntryResult inherited = X64PageTableEntry.TryDecode(
            X64PageTableLevel.Pde, inheritedPde, preconditions);
        if (!inherited.Succeeded || !inherited.IsLargePage)
        {
            return new(PhysicalMappingPreparationStatus.InvalidInheritedEntry,
                entryStatus: inherited.Status);
        }

        inherited.PhysicalAddress.TryGetRawValueForHardwareRegisterBoundary(out ulong baseAddress);
        // The decoder admits at most 52-bit, 2 MiB aligned physical addresses.
        ulong end = start + byteLength;
        ulong largeEnd = baseAddress + LargePageBytes;
        if (start < baseAddress || end > largeEnd)
        {
            return new(PhysicalMappingPreparationStatus.RangeOutsideLargePage);
        }

        X64PageTableEntryResult table = X64PageTableEntry.TryEncode(
            new(X64PageTableLevel.Pde, X64PageTableEntryKind.TablePointer,
                tablePhysicalAddress, PageProtection.None, PageCachePolicy.WriteBack),
            preconditions);
        if (!table.Succeeded)
        {
            return new(PhysicalMappingPreparationStatus.InvalidTableAddress,
                entryStatus: table.Status);
        }

        tablePhysicalAddress.TryGetRawValueForHardwareRegisterBoundary(out ulong tableAddress);
        if (tableAddress >= baseAddress && tableAddress < largeEnd)
        {
            return new(PhysicalMappingPreparationStatus.TableOverlapsMappedRange);
        }

        ulong destinationAddress = (ulong)destination;
        if (destination == null || (destinationAddress & (sizeof(ulong) - 1)) != 0
            || destinationEntryCapacity < EntriesPerTable
            || destinationAddress > ulong.MaxValue - PageBytes)
        {
            return new(PhysicalMappingPreparationStatus.InvalidDestination);
        }

        X64PageTableEntryResult original = X64PageTableEntry.TryEncode(
            new(X64PageTableLevel.Pte, X64PageTableEntryKind.Leaf,
                inherited.PhysicalAddress, inherited.Protection, inherited.CachePolicy),
            preconditions);
        X64PageTableEntryResult selected = X64PageTableEntry.TryEncode(
            new(X64PageTableLevel.Pte, X64PageTableEntryKind.Leaf,
                inherited.PhysicalAddress, inherited.Protection, policy),
            preconditions);
        if (!original.Succeeded || !selected.Succeeded)
        {
            return new(PhysicalMappingPreparationStatus.UnsupportedCachePolicy,
                entryStatus: !original.Succeeded ? original.Status : selected.Status);
        }

        // Encoding the aligned base proves every child: offsets use only the
        // previously zero address bits 12..20, below the physical-width limit.
        // Re-encoding is required because PAT moves from bit 12 to bit 7.
        ulong hardwareState = inheritedPde & ((1UL << 5) | (1UL << 6));
        uint firstPage = (uint)((start - baseAddress) / PageBytes);
        uint endPage = (uint)((end - baseAddress) / PageBytes);
        for (uint index = 0; index < EntriesPerTable; index++)
        {
            ulong raw = index >= firstPage && index < endPage
                ? selected.Entry.RawValue : original.Entry.RawValue;
            destination[index] = raw | (index * PageBytes) | hardwareState;
        }

        return new(PhysicalMappingPreparationStatus.Prepared,
            table.Entry.RawValue | (inheritedPde & (1UL << 5)),
            X64PageTableEntryStatus.Success);
    }
}
