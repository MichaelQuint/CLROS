namespace OS.Kernel.Memory;
/// <summary>
/// Pointer-free evidence returned by the page-zero policy. A successful
/// mapping-intent result validates only the proposed interval. Only a
/// successful table-image audit can prove absence in the supplied image.
/// </summary>

public readonly struct PageZeroMappingPolicyResult
{
    public const ulong StructureSize = 128;
    public const ushort StructureVersion = 1;
    public const ulong NoIndex = ~0UL;
    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly PageZeroMappingPolicyStatus _status;
    private readonly PageZeroMappingPolicyOperation _operation;
    private readonly X64PageTableEntryStatus _entryStatus;
    private readonly X64PageTableLevel _absentAtLevel;
    private readonly byte _reserved0;
    private readonly uint _reserved1;
    private readonly PageZeroMappingIntent _intent;
    private readonly OS.Boot.PhysicalAddress _rootTable;
    private readonly ulong _pageImageCount;
    private readonly ulong _pageImageCapacity;
    private readonly ulong _entryCount;
    private readonly ulong _entryCapacity;
    private readonly ulong _rootPageIndex;
    private readonly ulong _terminalPageIndex;
    private readonly ulong _terminalEntryIndex;
    private readonly ulong _inspectedEntryCount;
    private readonly ulong _terminalRawEntry;
    private readonly ulong _presentLeafPageSize;
    private PageZeroMappingPolicyResult(PageZeroMappingPolicyStatus status, PageZeroMappingPolicyOperation operation, X64PageTableEntryStatus entryStatus, X64PageTableLevel absentAtLevel, PageZeroMappingIntent intent, OS.Boot.PhysicalAddress rootTable, ulong pageImageCount, ulong pageImageCapacity, ulong entryCount, ulong entryCapacity, ulong rootPageIndex, ulong terminalPageIndex, ulong terminalEntryIndex, ulong inspectedEntryCount, ulong terminalRawEntry, ulong presentLeafPageSize)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _status = status;
        _operation = operation;
        _entryStatus = entryStatus;
        _absentAtLevel = absentAtLevel;
        _reserved0 = 0;
        _reserved1 = 0;
        _intent = intent;
        _rootTable = rootTable;
        _pageImageCount = pageImageCount;
        _pageImageCapacity = pageImageCapacity;
        _entryCount = entryCount;
        _entryCapacity = entryCapacity;
        _rootPageIndex = rootPageIndex;
        _terminalPageIndex = terminalPageIndex;
        _terminalEntryIndex = terminalEntryIndex;
        _inspectedEntryCount = inspectedEntryCount;
        _terminalRawEntry = terminalRawEntry;
        _presentLeafPageSize = presentLeafPageSize;
    }

    public bool IsInitialized => _structureSize == StructureSize && _structureVersion == StructureVersion && _isInitialized == 1 && _reserved0 == 0 && _reserved1 == 0;
    public PageZeroMappingPolicyStatus Status => _status;
    public PageZeroMappingPolicyOperation Operation => _operation;
    public X64PageTableEntryStatus EntryStatus => _entryStatus;
    public X64PageTableLevel AbsentAtLevel => _absentAtLevel;
    public PageZeroMappingIntent Intent => _intent;
    public OS.Boot.PhysicalAddress RootTable => _rootTable;
    public ulong PageImageCount => _pageImageCount;
    public ulong PageImageCapacity => _pageImageCapacity;
    public ulong EntryCount => _entryCount;
    public ulong EntryCapacity => _entryCapacity;
    public ulong RootPageIndex => _rootPageIndex;
    public ulong TerminalPageIndex => _terminalPageIndex;
    public ulong TerminalEntryIndex => _terminalEntryIndex;
    public ulong InspectedEntryCount => _inspectedEntryCount;
    public ulong TerminalRawEntry => _terminalRawEntry;
    public ulong PresentLeafPageSize => _presentLeafPageSize;
    public bool Succeeded => Validate() == PageZeroMappingPolicyStatus.Success;
    public bool ProvesPageZeroAbsentInSuppliedImage => ValidateAbsenceEvidence() == PageZeroMappingPolicyStatus.Success;

    public PageZeroMappingPolicyStatus Validate()
    {
        if (!IsInitialized)
        {
            return PageZeroMappingPolicyStatus.ContractLayoutMismatch;
        }

        if (_status != PageZeroMappingPolicyStatus.Success)
        {
            return _status;
        }

        if (_operation == PageZeroMappingPolicyOperation.MappingIntent)
        {
            PageZeroMappingPolicyStatus intentStatus = _intent.Validate();
            if (intentStatus != PageZeroMappingPolicyStatus.Success)
            {
                return intentStatus;
            }

            return _entryStatus == X64PageTableEntryStatus.Uninitialized && _absentAtLevel == X64PageTableLevel.Uninitialized && _reserved0 == 0 && _reserved1 == 0 && _rootTable.Equals(default) && _pageImageCount == 0 && _pageImageCapacity == 0 && _entryCount == 0 && _entryCapacity == 0 && _rootPageIndex == NoIndex && _terminalPageIndex == NoIndex && _terminalEntryIndex == NoIndex && _inspectedEntryCount == 0 && _terminalRawEntry == 0 && _presentLeafPageSize == 0 ? PageZeroMappingPolicyStatus.Success : PageZeroMappingPolicyStatus.ContractLayoutMismatch;
        }

        if (_operation == PageZeroMappingPolicyOperation.TableImageAudit)
        {
            return ValidateAbsenceEvidence();
        }

        return PageZeroMappingPolicyStatus.ContractLayoutMismatch;
    }

    public PageZeroMappingPolicyStatus ValidateAbsenceEvidence()
    {
        if (!IsInitialized || _status != PageZeroMappingPolicyStatus.Success || _operation != PageZeroMappingPolicyOperation.TableImageAudit || _entryStatus != X64PageTableEntryStatus.Success || _absentAtLevel < X64PageTableLevel.Pte || _absentAtLevel > X64PageTableLevel.Pml4e || !_intent.IsCanonicalDefault || !_rootTable.IsValid || !_rootTable.IsAligned(PageZeroMappingPolicy.PageSize) || _pageImageCount == 0 || _pageImageCount > PageZeroMappingPolicy.MaximumPageImages || _pageImageCapacity < _pageImageCount || _pageImageCapacity > PageZeroMappingPolicy.MaximumPageImages || _entryCount != _pageImageCount * X64PageTableImagePage.EntryCountPerPage || _entryCapacity < _entryCount || _entryCapacity > PageZeroMappingPolicy.MaximumEntryCount || _rootPageIndex >= _pageImageCount || _terminalPageIndex >= _pageImageCount || _terminalPageIndex > ~0UL / X64PageTableImagePage.EntryCountPerPage || _terminalEntryIndex != _terminalPageIndex * X64PageTableImagePage.EntryCountPerPage || _terminalEntryIndex >= _entryCount || _inspectedEntryCount != 5UL - (byte)_absentAtLevel || (_absentAtLevel == X64PageTableLevel.Pml4e && _terminalPageIndex != _rootPageIndex) || _terminalRawEntry != 0 || _presentLeafPageSize != 0)
        {
            return PageZeroMappingPolicyStatus.ContractLayoutMismatch;
        }

        return PageZeroMappingPolicyStatus.Success;
    }

    internal static PageZeroMappingPolicyResult IntentResult(PageZeroMappingPolicyStatus status, PageZeroMappingIntent intent)
    {
        return new PageZeroMappingPolicyResult(status, PageZeroMappingPolicyOperation.MappingIntent, X64PageTableEntryStatus.Uninitialized, X64PageTableLevel.Uninitialized, intent, default, 0, 0, 0, 0, NoIndex, NoIndex, NoIndex, 0, 0, 0);
    }

    internal static PageZeroMappingPolicyResult AuditResult(PageZeroMappingPolicyStatus status, X64PageTableEntryStatus entryStatus, X64PageTableLevel absentAtLevel, OS.Boot.PhysicalAddress rootTable, ulong pageImageCount, ulong pageImageCapacity, ulong entryCount, ulong entryCapacity, ulong rootPageIndex, ulong terminalPageIndex, ulong terminalEntryIndex, ulong inspectedEntryCount, ulong terminalRawEntry, ulong presentLeafPageSize)
    {
        return new PageZeroMappingPolicyResult(status, PageZeroMappingPolicyOperation.TableImageAudit, entryStatus, absentAtLevel, default, rootTable, pageImageCount, pageImageCapacity, entryCount, entryCapacity, rootPageIndex, terminalPageIndex, terminalEntryIndex, inspectedEntryCount, terminalRawEntry, presentLeafPageSize);
    }
}
