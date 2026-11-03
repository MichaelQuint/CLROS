using OS.Boot;
using OS.Kernel.Memory;

namespace OS.Kernel.Diagnostics;
/// <summary>
/// Snapshot of an audited supplied image, never a capability to load CR3.
/// Caller mutation/lifetime changes require a fresh Audit; no result owns memory.
/// </summary>

public readonly struct SeparatePageFaultPageTableResult
{
    private readonly byte _initialized;
    private readonly byte _profile;
    private readonly ulong _requiredTables;
    internal SeparatePageFaultPageTableResult(SeparatePageFaultPageTableStatus status, SeparatePageFaultPageTableRequest request, ulong rangeCount, ulong usedTablePages = 0, ulong mappedPages = 0, PageZeroMappingPolicyResult pageZero = default, bool retainedImage = false, ulong requiredTables = 0)
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
    public SeparatePageFaultPageTableStatus Status
    {
        get;
    }
    public SeparatePageFaultPageTableRequest Request
    {
        get;
    }
    public ulong RangeCount
    {
        get;
    }
    public ulong UsedTablePages
    {
        get;
    }
    public ulong MappedPages
    {
        get;
    }
    public PageZeroMappingPolicyResult PageZero
    {
        get;
    }
    public bool UsesRetainedImageLimits => _profile == 1;
    public ulong ConservativeTablePageBound => _profile == 0 ? SeparateDisabledDefaultPageFaultPageTables.RequiredTablePageBound(RangeCount) : _profile == 1 ? _requiredTables : 0;
    public bool Succeeded => IsInitialized && Status == SeparatePageFaultPageTableStatus.Success && SeparateDisabledDefaultPageFaultPageTables.ValidateRequest(Request) == SeparatePageFaultPageTableStatus.Success && ConservativeTablePageBound >= 4 && ConservativeTablePageBound <= SeparateDisabledDefaultPageFaultPageTables.MaximumTablePages && Request.ArenaPageCount >= ConservativeTablePageBound && UsedTablePages >= 4 && UsedTablePages <= ConservativeTablePageBound && MappedPages >= Request.ArenaPageCount + RangeCount && (_profile == 0 ? MappedPages <= Request.ArenaPageCount + RangeCount * SeparateDisabledDefaultPageFaultPageTables.MaximumRangePages : _profile == 1 && RangeCount >= 1 && RangeCount <= SeparateDisabledDefaultPageFaultPageTables.MaximumRetainedRanges && UsedTablePages == _requiredTables && MappedPages <= Request.ArenaPageCount + SeparateDisabledDefaultPageFaultPageTables.MaximumRetainedMappedPages && MappedPages <= Request.ArenaPageCount + RangeCount * SeparateDisabledDefaultPageFaultPageTables.MaximumRetainedRangePages) && PageZero.ProvesPageZeroAbsentInSuppliedImage && PageZero.PageImageCount == Request.ArenaPageCount && PageZero.EntryCount == Request.ArenaPageCount * SeparateDisabledDefaultPageFaultPageTables.EntriesPerPage && PageZero.RootPageIndex == 0 && PageZero.RootTable.Equals(Request.ArenaStart);
    public bool SuppliedImageAudited => Succeeded;
    public bool IsGuestEvidence => false;
    public bool Cr3Installed => false;
    public bool ActivePermissionsObserved => false;
    public bool TlbInvalidated => false;
    public bool FaultTriggered => false;
}
#if M16_025_PROBE

#else
#endif
#if M10_004_PROBE

#else
#endif
