namespace OS.Boot;
/// <summary>
/// Describes construction of a caller-owned flat page-table image. No
/// processor table page or physical allocation is dereferenced.
/// </summary>

public readonly struct M01LivePagingTableBuildResult
{
    public const ulong StructureSize = 80;
    public const ulong NoIndex = ~0UL;
    private readonly M01LivePagingTableBuildStatus _status;
    private readonly OS.Kernel.Memory.X64PageTableEntryStatus _entryStatus;
    private readonly ushort _reserved0;
    private readonly uint _reserved1;
    private readonly PhysicalAddress _rootTable;
    private readonly ulong _rangeCount;
    private readonly ulong _validatedRangeCount;
    private readonly ulong _tablePageCount;
    private readonly ulong _entryCount;
    private readonly ulong _mappedPageCount;
    private readonly ulong _invalidRangeIndex;
    private readonly ulong _invalidPageOrdinal;
    private readonly ulong _probeLeafEntryIndex;
    internal M01LivePagingTableBuildResult(M01LivePagingTableBuildStatus status, OS.Kernel.Memory.X64PageTableEntryStatus entryStatus, PhysicalAddress rootTable, ulong rangeCount, ulong validatedRangeCount, ulong tablePageCount, ulong entryCount, ulong mappedPageCount, ulong invalidRangeIndex, ulong invalidPageOrdinal, ulong probeLeafEntryIndex)
    {
        _status = status;
        _entryStatus = entryStatus;
        _reserved0 = 0;
        _reserved1 = 0;
        _rootTable = rootTable;
        _rangeCount = rangeCount;
        _validatedRangeCount = validatedRangeCount;
        _tablePageCount = tablePageCount;
        _entryCount = entryCount;
        _mappedPageCount = mappedPageCount;
        _invalidRangeIndex = invalidRangeIndex;
        _invalidPageOrdinal = invalidPageOrdinal;
        _probeLeafEntryIndex = probeLeafEntryIndex;
    }

    public M01LivePagingTableBuildStatus Status => _status;
    public OS.Kernel.Memory.X64PageTableEntryStatus EntryStatus => _entryStatus;
    public PhysicalAddress RootTable => _rootTable;
    public ulong RangeCount => _rangeCount;
    public ulong ValidatedRangeCount => _validatedRangeCount;
    public ulong TablePageCount => _tablePageCount;
    public ulong EntryCount => _entryCount;
    public ulong MappedPageCount => _mappedPageCount;
    public ulong InvalidRangeIndex => _invalidRangeIndex;
    public ulong InvalidPageOrdinal => _invalidPageOrdinal;
    public ulong ProbeLeafEntryIndex => _probeLeafEntryIndex;
    public bool Succeeded => _status == M01LivePagingTableBuildStatus.Success && _entryStatus == OS.Kernel.Memory.X64PageTableEntryStatus.Success && _reserved0 == 0 && _reserved1 == 0 && _rootTable.IsValid && _rangeCount != 0 && _validatedRangeCount == _rangeCount && _tablePageCount != 0 && _entryCount == _tablePageCount * OS.Kernel.Memory.X64PageTableImagePage.EntryCountPerPage && _mappedPageCount != 0 && _invalidRangeIndex == NoIndex && _invalidPageOrdinal == NoIndex && _probeLeafEntryIndex < _entryCount;
}
