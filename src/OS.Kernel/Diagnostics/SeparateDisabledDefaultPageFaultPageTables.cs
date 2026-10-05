using OS.Boot;
using OS.Kernel.Memory;

namespace OS.Kernel.Diagnostics;

public enum SeparatePageFaultPageTableStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidRequest = 2,
    InvalidPreconditions = 3,
    InvalidRangeCount = 4,
    InvalidRange = 5,
    RangeOverlap = 6,
    TableArenaOverlap = 7,
    InsufficientTablePages = 8,
    InvalidStorage = 9,
    StorageOverlap = 10,
    StorageNotZero = 11,
    InvalidImageMetadata = 12,
    MalformedEntry = 13,
    ForeignTableTarget = 14,
    RepeatedTableTarget = 15,
    UnexpectedMapping = 16,
    MappingMismatch = 17,
    MissingMapping = 18,
    UnusedTableNotZero = 19,
    PageZeroNotAbsent = 20
}

/// <summary>One caller-declared identity interval; no allocation or lifetime authority.</summary>
public readonly struct SeparatePageFaultMappingRange
{
    private readonly byte _initialized;

    public SeparatePageFaultMappingRange(
        ulong start, ulong pageCount, PageProtection protection,
        PageCachePolicy cachePolicy = PageCachePolicy.WriteBack)
    {
        _initialized = 1;
        Start = start;
        PageCount = pageCount;
        Protection = protection;
        CachePolicy = cachePolicy;
    }

    public bool IsInitialized => _initialized == 1;
    public ulong Start { get; }
    public ulong PageCount { get; }
    public PageProtection Protection { get; }
    public PageCachePolicy CachePolicy { get; }
}

/// <summary>
/// Caller-supplied mode values are not processor observations made by this API.
/// The physical arena and its writable host backing pointer are deliberately distinct.
/// </summary>
public readonly struct SeparatePageFaultPageTableRequest
{
    private readonly byte _initialized;

    public SeparatePageFaultPageTableRequest(
        PhysicalAddress arenaStart, ulong arenaPageCount,
        X64PageTableEncodingPreconditions preconditions)
    {
        _initialized = 1;
        ArenaStart = arenaStart;
        ArenaPageCount = arenaPageCount;
        Preconditions = preconditions;
    }

    public bool IsInitialized => _initialized == 1;
    public PhysicalAddress ArenaStart { get; }
    public ulong ArenaPageCount { get; }
    public X64PageTableEncodingPreconditions Preconditions { get; }
}

/// <summary>
/// Snapshot of an audited supplied image, never a capability to load CR3.
/// Caller mutation/lifetime changes require a fresh Audit; no result owns memory.
/// </summary>
public readonly struct SeparatePageFaultPageTableResult
{
    private readonly byte _initialized;
    private readonly byte _profile;
    private readonly ulong _requiredTables;

    internal SeparatePageFaultPageTableResult(
        SeparatePageFaultPageTableStatus status,
        SeparatePageFaultPageTableRequest request, ulong rangeCount,
        ulong usedTablePages = 0, ulong mappedPages = 0,
        PageZeroMappingPolicyResult pageZero = default,
        bool retainedImage = false, ulong requiredTables = 0)
    {
        _initialized = 1;
        _profile = retainedImage ? (byte)1 : (byte)0;
        _requiredTables = requiredTables;
        Status = status;
        Request = request;
        RangeCount = rangeCount;
        UsedTablePages = usedTablePages;
        MappedPages = mappedPages;
        PageZero = pageZero;
    }

    public bool IsInitialized => _initialized == 1;
    public SeparatePageFaultPageTableStatus Status { get; }
    public SeparatePageFaultPageTableRequest Request { get; }
    public ulong RangeCount { get; }
    public ulong UsedTablePages { get; }
    public ulong MappedPages { get; }
    public PageZeroMappingPolicyResult PageZero { get; }
    public bool UsesRetainedImageLimits => _profile == 1;
    public ulong ConservativeTablePageBound =>
        _profile == 0 ? SeparateDisabledDefaultPageFaultPageTables.RequiredTablePageBound(RangeCount) :
        _profile == 1 ? _requiredTables : 0;
    public bool Succeeded => IsInitialized &&
        Status == SeparatePageFaultPageTableStatus.Success &&
        SeparateDisabledDefaultPageFaultPageTables.ValidateRequest(Request) ==
            SeparatePageFaultPageTableStatus.Success &&
        ConservativeTablePageBound >= 4 &&
        ConservativeTablePageBound <= SeparateDisabledDefaultPageFaultPageTables.MaximumTablePages &&
        Request.ArenaPageCount >= ConservativeTablePageBound &&
        UsedTablePages >= 4 && UsedTablePages <= ConservativeTablePageBound &&
        MappedPages >= Request.ArenaPageCount + RangeCount &&
        (_profile == 0 ?
            MappedPages <= Request.ArenaPageCount + RangeCount *
                SeparateDisabledDefaultPageFaultPageTables.MaximumRangePages :
            _profile == 1 && RangeCount >= 1 &&
            RangeCount <= SeparateDisabledDefaultPageFaultPageTables.MaximumRetainedRanges &&
            UsedTablePages == _requiredTables &&
            MappedPages <= Request.ArenaPageCount +
                SeparateDisabledDefaultPageFaultPageTables.MaximumRetainedMappedPages &&
            MappedPages <= Request.ArenaPageCount + RangeCount *
                SeparateDisabledDefaultPageFaultPageTables.MaximumRetainedRangePages) &&
        PageZero.ProvesPageZeroAbsentInSuppliedImage &&
        PageZero.PageImageCount == Request.ArenaPageCount &&
        PageZero.EntryCount == Request.ArenaPageCount *
            SeparateDisabledDefaultPageFaultPageTables.EntriesPerPage &&
        PageZero.RootPageIndex == 0 &&
        PageZero.RootTable.Equals(Request.ArenaStart);
    public bool SuppliedImageAudited => Succeeded;
    public bool IsGuestEvidence => false;
    public bool Cr3Installed => false;
    public bool ActivePermissionsObserved => false;
    public bool TlbInvalidated => false;
    public bool FaultTriggered => false;
}

/// <summary>
/// Bounded four-level, supervisor-only, WB identity image. At most eight
/// disjoint 1..16-page RX/RW+NX ranges plus the explicit RW+NX table arena.
/// Each interval is shorter than 2 MiB: caller ranges <=64 KiB, arena <=220 KiB.
/// Each touches at most two 2 MiB, two 1 GiB and two 512 GiB regions, hence
/// at most six nonroot tables. 1 + 6*(rangeCount+1) <=55 is conservative,
/// including the arena itself, without recursive allocation/self-map planning.
/// No guards, huge pages, firmware hierarchy, live mode reads or instruction bytes.
/// Separately named retained-image entry points allow larger bounded RO/RX/RW
/// ranges and count distinct hierarchy prefixes before writing. Omitted pages,
/// including declared guard gaps, remain absent; no allocation provenance is inferred.
/// Callers must keep input/output spans accessible, exclusively owned and stable
/// for the operation. This API cannot validate a raw pointer's physical lifetime.
/// </summary>
public static unsafe class SeparateDisabledDefaultPageFaultPageTables
{
    public const ulong PageSize = 4096;
    public const ulong EntriesPerPage = 512;
    public const ulong MaximumRanges = 8;
    public const ulong MaximumRangePages = 16;
    public const ulong MaximumTablePages = 55;
    public const ulong MaximumEntries = MaximumTablePages * EntriesPerPage;
    public const ulong MaximumRetainedRanges = 32;
    public const ulong MaximumRetainedRangePages = 4096;
    public const ulong MaximumRetainedMappedPages = 8192;
    private const ulong LowCanonicalEnd = 0x0000_8000_0000_0000;
    private const PageProtection Code = PageProtection.Read | PageProtection.Execute;
    private const PageProtection Data = PageProtection.Read | PageProtection.Write;

    public static ulong RequiredTablePageBound(ulong rangeCount) =>
        rangeCount >= 1 && rangeCount <= MaximumRanges ? 1 + 6 * (rangeCount + 1) : 0;

    public static SeparatePageFaultPageTableResult Build(
        SeparatePageFaultPageTableRequest request,
        SeparatePageFaultMappingRange* ranges, ulong rangeCount, ulong rangeCapacity,
        X64PageTableImagePage* pages, ulong pageCapacity,
        ulong* entries, ulong entryCapacity) => BuildCore(request, ranges, rangeCount,
            rangeCapacity, pages, pageCapacity, entries, entryCapacity, false);

    /// <summary>
    /// Build a pristine retained identity image using at most 55 table pages.
    /// Accept 1..32 disjoint ranges, each 1..4096 pages, at most 8192 caller pages
    /// total, plus the table arena. Ranges may be RO+NX, RX or RW+NX, WB only.
    /// Exact table capacity is checked before any output mutation. This does not
    /// establish firmware accessibility, ownership, live guards or CR3 authority.
    /// </summary>
    public static SeparatePageFaultPageTableResult BuildRetainedImage(
        SeparatePageFaultPageTableRequest request,
        SeparatePageFaultMappingRange* ranges, ulong rangeCount, ulong rangeCapacity,
        X64PageTableImagePage* pages, ulong pageCapacity,
        ulong* entries, ulong entryCapacity) => BuildCore(request, ranges, rangeCount,
            rangeCapacity, pages, pageCapacity, entries, entryCapacity, true);

    private static SeparatePageFaultPageTableResult BuildCore(
        SeparatePageFaultPageTableRequest request,
        SeparatePageFaultMappingRange* ranges, ulong rangeCount, ulong rangeCapacity,
        X64PageTableImagePage* pages, ulong pageCapacity,
        ulong* entries, ulong entryCapacity, bool retainedImage)
    {
        SeparatePageFaultPageTableStatus status = ValidateInputs(request,
            ranges, rangeCount, rangeCapacity, pages, pageCapacity, entries,
            entryCapacity, retainedImage, out ulong root, out _, out _);
        if (status != SeparatePageFaultPageTableStatus.Success)
            return Failure(status, request, rangeCount);

        ulong entryCount = request.ArenaPageCount * EntriesPerPage;
        for (ulong index = 0; index < entryCount; index++)
            if (entries[index] != 0)
                return Failure(SeparatePageFaultPageTableStatus.StorageNotZero, request, rangeCount);

        // All encoding, bounds and capacity preconditions have passed. Metadata
        // and entries are modified only below this point; input spans cannot alias.
        for (ulong page = 0; page < request.ArenaPageCount; page++)
        {
            X64PageTableImagePage.TryCreate(Address(root + page * PageSize),
                page * EntriesPerPage, out pages[page]);
        }
        ulong usedPages = 1;
        for (ulong range = 0; range <= rangeCount; range++)
        {
            ulong start = range == rangeCount ? root : ranges[range].Start;
            ulong count = range == rangeCount ? request.ArenaPageCount : ranges[range].PageCount;
            PageProtection protection = range == rangeCount ? Data : ranges[range].Protection;
            for (ulong page = 0; page < count; page++)
            {
                ulong address = start + page * PageSize;
                ulong table = 0;
                for (int level = 4; level > 1; level--)
                {
                    ulong entry = table * EntriesPerPage + ((address >> (12 + 9 * (level - 1))) & 511);
                    if (entries[entry] == 0)
                    {
                        ulong child = usedPages++;
                        entries[entry] = Encode(root + child * PageSize,
                            (X64PageTableLevel)level, PageProtection.None, request.Preconditions);
                    }
                    // Only entries produced above are followed; no supplied hierarchy is borrowed.
                    table = ((entries[entry] & 0x000f_ffff_ffff_f000UL) - root) / PageSize;
                }
                entries[table * EntriesPerPage + ((address >> 12) & 511)] =
                    Encode(address, X64PageTableLevel.Pte, protection, request.Preconditions);
            }
        }
        return AuditCore(request, ranges, rangeCount, rangeCapacity, pages, pageCapacity,
            entries, entryCapacity, retainedImage);
    }

    /// <summary>
    /// Read-only independent hierarchy walk; accepts only pristine prepared
    /// images, including zero accessed/dirty bits. Not an active-table auditor.
    /// Checks metadata for every arena page and all entries, including unused pages.
    /// </summary>
    public static SeparatePageFaultPageTableResult Audit(
        SeparatePageFaultPageTableRequest request,
        SeparatePageFaultMappingRange* ranges, ulong rangeCount, ulong rangeCapacity,
        X64PageTableImagePage* pages, ulong pageCapacity,
        ulong* entries, ulong entryCapacity) => AuditCore(request, ranges, rangeCount,
            rangeCapacity, pages, pageCapacity, entries, entryCapacity, false);

    /// <summary>Read-only pristine-image audit with the same limits as BuildRetainedImage.</summary>
    public static SeparatePageFaultPageTableResult AuditRetainedImage(
        SeparatePageFaultPageTableRequest request,
        SeparatePageFaultMappingRange* ranges, ulong rangeCount, ulong rangeCapacity,
        X64PageTableImagePage* pages, ulong pageCapacity,
        ulong* entries, ulong entryCapacity) => AuditCore(request, ranges, rangeCount,
            rangeCapacity, pages, pageCapacity, entries, entryCapacity, true);

    private static SeparatePageFaultPageTableResult AuditCore(
        SeparatePageFaultPageTableRequest request,
        SeparatePageFaultMappingRange* ranges, ulong rangeCount, ulong rangeCapacity,
        X64PageTableImagePage* pages, ulong pageCapacity,
        ulong* entries, ulong entryCapacity, bool retainedImage)
    {
        SeparatePageFaultPageTableStatus status = ValidateInputs(request,
            ranges, rangeCount, rangeCapacity, pages, pageCapacity, entries,
            entryCapacity, retainedImage, out ulong root, out ulong expectedLeaves,
            out ulong requiredTables);
        if (status != SeparatePageFaultPageTableStatus.Success)
            return Failure(status, request, rangeCount);
        ulong count = request.ArenaPageCount * EntriesPerPage;
        for (ulong page = 0; page < request.ArenaPageCount; page++)
        {
            if (pages[page].Validate(page, count, request.Preconditions.PhysicalAddressWidth) !=
                    X64PageMapStatus.Success ||
                !pages[page].PhysicalStart.Equals(Address(root + page * PageSize)))
                return Failure(SeparatePageFaultPageTableStatus.InvalidImageMetadata, request, rangeCount);
        }

        ulong visited = 0;
        ulong used = 0;
        ulong leaves = 0;
        status = Walk(request, ranges, rangeCount, entries, root, 0,
            X64PageTableLevel.Pml4e, 0, ref visited, ref used, ref leaves);
        if (status != SeparatePageFaultPageTableStatus.Success)
            return Failure(status, request, rangeCount);
        if (leaves != expectedLeaves)
            return Failure(SeparatePageFaultPageTableStatus.MissingMapping, request, rangeCount);

        for (ulong page = 0; page < request.ArenaPageCount; page++)
            if ((visited & (1UL << (int)page)) == 0)
                for (ulong entry = 0; entry < EntriesPerPage; entry++)
                    if (entries[page * EntriesPerPage + entry] != 0)
                        return Failure(SeparatePageFaultPageTableStatus.UnusedTableNotZero, request, rangeCount);

        PageZeroMappingPolicyResult absence = PageZeroMappingPolicy.AuditTableImage(
            request.ArenaStart, request.Preconditions, pages, request.ArenaPageCount,
            pageCapacity, entries, count, entryCapacity);
        if (!absence.ProvesPageZeroAbsentInSuppliedImage)
            return Failure(SeparatePageFaultPageTableStatus.PageZeroNotAbsent, request, rangeCount);
        return new SeparatePageFaultPageTableResult(SeparatePageFaultPageTableStatus.Success,
            request, rangeCount, used, leaves, absence, retainedImage, requiredTables);
    }

    internal static SeparatePageFaultPageTableStatus ValidateRequest(SeparatePageFaultPageTableRequest request)
    {
        if (!request.IsInitialized || request.ArenaPageCount == 0 ||
            request.ArenaPageCount > MaximumTablePages)
            return SeparatePageFaultPageTableStatus.InvalidRequest;
        if (request.Preconditions.Validate() != X64PageTableEntryStatus.Success)
            return SeparatePageFaultPageTableStatus.InvalidPreconditions;
        if (request.ArenaStart.TryGetRawValueForHardwareRegisterBoundary(out ulong root) !=
                AddressValueStatus.Success || !ValidInterval(root, request.ArenaPageCount, request.Preconditions))
            return SeparatePageFaultPageTableStatus.InvalidRequest;
        // The real encoder validates PAT index zero for WB paging structures,
        // as well as the requested data-leaf policy. No raw PAT bit assumptions.
        if (!EncodingValid(root, X64PageTableLevel.Pml4e, PageProtection.None, request.Preconditions) ||
            !EncodingValid(root, X64PageTableLevel.Pte, Data, request.Preconditions))
            return SeparatePageFaultPageTableStatus.InvalidPreconditions;
        return SeparatePageFaultPageTableStatus.Success;
    }

    private static SeparatePageFaultPageTableStatus ValidateInputs(
        SeparatePageFaultPageTableRequest request,
        SeparatePageFaultMappingRange* ranges, ulong rangeCount, ulong rangeCapacity,
        X64PageTableImagePage* pages, ulong pageCapacity, ulong* entries, ulong entryCapacity,
        bool retainedImage, out ulong root, out ulong expectedLeaves, out ulong requiredTables)
    {
        root = 0;
        expectedLeaves = 0;
        requiredTables = 0;
        SeparatePageFaultPageTableStatus status = ValidateRequest(request);
        if (status != SeparatePageFaultPageTableStatus.Success) return status;
        request.ArenaStart.TryGetRawValueForHardwareRegisterBoundary(out root);
        ulong bound = RequiredTablePageBound(rangeCount);
        ulong maximumRanges = retainedImage ? MaximumRetainedRanges : MaximumRanges;
        if (rangeCount == 0 || rangeCount > maximumRanges ||
            rangeCapacity < rangeCount || rangeCapacity > maximumRanges)
            return SeparatePageFaultPageTableStatus.InvalidRangeCount;
        if (!retainedImage && request.ArenaPageCount < bound)
            return SeparatePageFaultPageTableStatus.InsufficientTablePages;
        if (pageCapacity < request.ArenaPageCount || pageCapacity > MaximumTablePages ||
            entryCapacity < request.ArenaPageCount * EntriesPerPage || entryCapacity > MaximumEntries)
            return SeparatePageFaultPageTableStatus.InvalidStorage;
        ulong rangeBytes = rangeCapacity * (ulong)sizeof(SeparatePageFaultMappingRange);
        ulong pageBytes = pageCapacity * (ulong)sizeof(X64PageTableImagePage);
        ulong entryBytes = entryCapacity * sizeof(ulong);
        if (!ValidStorage(ranges, rangeBytes) || !ValidStorage(pages, pageBytes) || !ValidStorage(entries, entryBytes))
            return SeparatePageFaultPageTableStatus.InvalidStorage;
        if (Overlap((ulong)ranges, rangeBytes, (ulong)pages, pageBytes) ||
            Overlap((ulong)ranges, rangeBytes, (ulong)entries, entryBytes) ||
            Overlap((ulong)pages, pageBytes, (ulong)entries, entryBytes))
            return SeparatePageFaultPageTableStatus.StorageOverlap;

        expectedLeaves = request.ArenaPageCount;
        ulong maximumRangePages = retainedImage ? MaximumRetainedRangePages : MaximumRangePages;
        for (ulong index = 0; index < rangeCount; index++)
        {
            SeparatePageFaultMappingRange range = ranges[index];
            if (!range.IsInitialized || range.PageCount == 0 || range.PageCount > maximumRangePages ||
                (range.Protection != Code && range.Protection != Data &&
                    (!retainedImage || range.Protection != PageProtection.Read)) ||
                range.CachePolicy != PageCachePolicy.WriteBack ||
                !ValidInterval(range.Start, range.PageCount, request.Preconditions, maximumRangePages) ||
                !EncodingValid(range.Start, X64PageTableLevel.Pte, range.Protection, request.Preconditions))
                return SeparatePageFaultPageTableStatus.InvalidRange;
            ulong length = range.PageCount * PageSize;
            if (Overlap(range.Start, length, root, request.ArenaPageCount * PageSize))
                return SeparatePageFaultPageTableStatus.TableArenaOverlap;
            for (ulong prior = 0; prior < index; prior++)
                if (Overlap(range.Start, length, ranges[prior].Start, ranges[prior].PageCount * PageSize))
                    return SeparatePageFaultPageTableStatus.RangeOverlap;
            expectedLeaves += range.PageCount;
            if (retainedImage && expectedLeaves - request.ArenaPageCount > MaximumRetainedMappedPages)
                return SeparatePageFaultPageTableStatus.InvalidRange;
        }
        requiredTables = retainedImage ? CountRequiredTables(request, ranges, rangeCount, root) : bound;
        if (request.ArenaPageCount < requiredTables)
            return SeparatePageFaultPageTableStatus.InsufficientTablePages;
        return SeparatePageFaultPageTableStatus.Success;
    }

    // A four-level 4-KiB identity image has one root and one child table per
    // distinct 512-GiB, 1-GiB and 2-MiB address prefix. Include the arena's own
    // mapping. Validated ranges bound this count without allocating scratch
    // memory or depending on their ordering; overlapping prefixes count once.
    private static ulong CountRequiredTables(SeparatePageFaultPageTableRequest request,
        SeparatePageFaultMappingRange* ranges, ulong rangeCount, ulong root)
    {
        ulong tables = 1;
        for (int shift = 21; shift <= 39; shift += 9)
        {
            for (ulong index = 0; index <= rangeCount; index++)
            {
                ulong start = index == rangeCount ? root : ranges[index].Start;
                ulong pages = index == rangeCount ? request.ArenaPageCount : ranges[index].PageCount;
                ulong last = (start + pages * PageSize - 1) >> shift;
                for (ulong prefix = start >> shift; prefix <= last; prefix++)
                {
                    bool seen = false;
                    for (ulong prior = 0; prior < index; prior++)
                        if (prefix >= (ranges[prior].Start >> shift) &&
                            prefix <= ((ranges[prior].Start + ranges[prior].PageCount * PageSize - 1) >> shift))
                        {
                            seen = true;
                            break;
                        }
                    if (!seen) tables++;
                }
            }
        }
        return tables;
    }

    private static SeparatePageFaultPageTableStatus Walk(
        SeparatePageFaultPageTableRequest request, SeparatePageFaultMappingRange* ranges,
        ulong rangeCount, ulong* entries, ulong root, ulong table, X64PageTableLevel level,
        ulong prefix, ref ulong visited, ref ulong used, ref ulong leaves)
    {
        ulong bit = 1UL << (int)table;
        if ((visited & bit) != 0) return SeparatePageFaultPageTableStatus.RepeatedTableTarget;
        visited |= bit;
        used++;
        ulong initialLeaves = leaves;
        for (ulong index = 0; index < EntriesPerPage; index++)
        {
            ulong raw = entries[table * EntriesPerPage + index];
            if (raw == 0) continue;
            X64PageTableEntryResult decoded = X64PageTableEntry.TryDecode(level, raw, request.Preconditions);
            if (!decoded.Succeeded || !decoded.IsPresent)
                return SeparatePageFaultPageTableStatus.MalformedEntry;
            decoded.PhysicalAddress.TryGetRawValueForHardwareRegisterBoundary(out ulong physical);
            ulong address = prefix | (index << (12 + 9 * ((int)level - 1)));
            if (level != X64PageTableLevel.Pte)
            {
                if (decoded.Kind != X64PageTableEntryKind.TablePointer ||
                    raw != Encode(physical, level, PageProtection.None, request.Preconditions))
                    return SeparatePageFaultPageTableStatus.MalformedEntry;
                if (physical < root || physical - root >= request.ArenaPageCount * PageSize)
                    return SeparatePageFaultPageTableStatus.ForeignTableTarget;
                SeparatePageFaultPageTableStatus status = Walk(request, ranges, rangeCount,
                    entries, root, (physical - root) / PageSize, (X64PageTableLevel)((int)level - 1),
                    address, ref visited, ref used, ref leaves);
                if (status != SeparatePageFaultPageTableStatus.Success) return status;
            }
            else
            {
                PageProtection expected = ExpectedProtection(request, ranges, rangeCount, root, address);
                if (expected == PageProtection.None)
                    return SeparatePageFaultPageTableStatus.UnexpectedMapping;
                if (physical != address || raw != Encode(address, level, expected, request.Preconditions))
                    return SeparatePageFaultPageTableStatus.MappingMismatch;
                leaves++;
            }
        }
        return leaves != initialLeaves ? SeparatePageFaultPageTableStatus.Success :
            level == X64PageTableLevel.Pml4e ? SeparatePageFaultPageTableStatus.MissingMapping :
            SeparatePageFaultPageTableStatus.UnexpectedMapping;
    }

    private static PageProtection ExpectedProtection(SeparatePageFaultPageTableRequest request,
        SeparatePageFaultMappingRange* ranges, ulong count, ulong root, ulong address)
    {
        if (address >= root && address - root < request.ArenaPageCount * PageSize) return Data;
        for (ulong index = 0; index < count; index++)
            if (address >= ranges[index].Start && address - ranges[index].Start < ranges[index].PageCount * PageSize)
                return ranges[index].Protection;
        return PageProtection.None;
    }

    private static bool ValidInterval(ulong start, ulong count, X64PageTableEncodingPreconditions mode,
        ulong maximumPages = MaximumTablePages)
    {
        if (start < PageSize || (start & (PageSize - 1)) != 0 || count == 0 || count > maximumPages)
            return false;
        ulong length = count * PageSize;
        ulong physicalEnd = 1UL << mode.PhysicalAddressWidth;
        return start < LowCanonicalEnd && length <= LowCanonicalEnd - start &&
            start < physicalEnd && length <= physicalEnd - start;
    }

    private static bool ValidStorage(void* pointer, ulong bytes) =>
        pointer != null && ((ulong)pointer & 7) == 0 && bytes <= ~0UL - (ulong)pointer;

    private static bool Overlap(ulong left, ulong leftBytes, ulong right, ulong rightBytes) =>
        left < right + rightBytes && right < left + leftBytes;

    private static PhysicalAddress Address(ulong raw)
    {
        PhysicalAddress.TryCreateFromHardwareRegisterBoundary(raw, out PhysicalAddress address);
        return address;
    }

    private static X64PageTableEntryResult Encoded(ulong address, X64PageTableLevel level,
        PageProtection protection, X64PageTableEncodingPreconditions mode) =>
        X64PageTableEntry.TryEncode(new X64PageTableEntryRequest(level,
            level == X64PageTableLevel.Pte ? X64PageTableEntryKind.Leaf : X64PageTableEntryKind.TablePointer,
            Address(address), protection, PageCachePolicy.WriteBack), mode);

    private static bool EncodingValid(ulong address, X64PageTableLevel level,
        PageProtection protection, X64PageTableEncodingPreconditions mode) => Encoded(address, level, protection, mode).Succeeded;

    private static ulong Encode(ulong address, X64PageTableLevel level,
        PageProtection protection, X64PageTableEncodingPreconditions mode) => Encoded(address, level, protection, mode).Entry.RawValue;

    private static SeparatePageFaultPageTableResult Failure(SeparatePageFaultPageTableStatus status,
        SeparatePageFaultPageTableRequest request, ulong rangeCount) => new(status, request, rangeCount);
}
