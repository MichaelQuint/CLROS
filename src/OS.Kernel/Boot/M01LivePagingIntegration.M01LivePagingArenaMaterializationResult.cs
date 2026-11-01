namespace OS.Boot;
/// <summary>
/// Value-only evidence from exact table-page materialization and readback.
/// Hashes are diagnostic; exact per-entry comparison remains authoritative.
/// </summary>

public readonly struct M01LivePagingArenaMaterializationResult
{
    public const ushort CurrentVersion = 1;
    public const ulong StructureSize = 160;
    public const ulong NoIndex = ~0UL;
    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly M01LivePagingArenaMaterializationStatus _status;
    private readonly byte _isInitialized;
    private readonly OS.Kernel.Memory.X64PageTableEntryStatus _pageTableEntryStatus;
    private readonly OS.Kernel.Memory.X64PageMapStatus _pageImageStatus;
    private readonly ushort _reserved0;
    private readonly uint _reserved1;
    private readonly PhysicalAddress _rootTable;
    private readonly ulong _tablePageCount;
    private readonly ulong _verifiedPageCount;
    private readonly ulong _entryCount;
    private readonly ulong _verifiedEntryCount;
    private readonly ulong _invalidPageOrdinal;
    private readonly ulong _invalidEntryIndex;
    private readonly ulong _expectedEntry;
    private readonly ulong _observedEntry;
    private readonly ulong _sourceFingerprint;
    private readonly ulong _observedFingerprint;
    private readonly ulong _stateFingerprintBefore;
    private readonly ulong _stateFingerprintAfter;
    private readonly ulong _bytesWritten;
    private readonly ulong _reachablePageCount;
    private readonly ulong _mappedLeafCount;
    private readonly ulong _verifiedStateByteCount;
    private readonly ulong _invalidStateByteIndex;
    internal M01LivePagingArenaMaterializationResult(M01LivePagingArenaMaterializationStatus status, OS.Kernel.Memory.X64PageTableEntryStatus pageTableEntryStatus, OS.Kernel.Memory.X64PageMapStatus pageImageStatus, PhysicalAddress rootTable, ulong tablePageCount, ulong verifiedPageCount, ulong entryCount, ulong verifiedEntryCount, ulong invalidPageOrdinal, ulong invalidEntryIndex, ulong expectedEntry, ulong observedEntry, ulong sourceFingerprint, ulong observedFingerprint, ulong stateFingerprintBefore, ulong stateFingerprintAfter, ulong bytesWritten, ulong reachablePageCount, ulong mappedLeafCount, ulong verifiedStateByteCount, ulong invalidStateByteIndex)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = CurrentVersion;
        _status = status;
        _isInitialized = 1;
        _pageTableEntryStatus = pageTableEntryStatus;
        _pageImageStatus = pageImageStatus;
        _reserved0 = 0;
        _reserved1 = 0;
        _rootTable = rootTable;
        _tablePageCount = tablePageCount;
        _verifiedPageCount = verifiedPageCount;
        _entryCount = entryCount;
        _verifiedEntryCount = verifiedEntryCount;
        _invalidPageOrdinal = invalidPageOrdinal;
        _invalidEntryIndex = invalidEntryIndex;
        _expectedEntry = expectedEntry;
        _observedEntry = observedEntry;
        _sourceFingerprint = sourceFingerprint;
        _observedFingerprint = observedFingerprint;
        _stateFingerprintBefore = stateFingerprintBefore;
        _stateFingerprintAfter = stateFingerprintAfter;
        _bytesWritten = bytesWritten;
        _reachablePageCount = reachablePageCount;
        _mappedLeafCount = mappedLeafCount;
        _verifiedStateByteCount = verifiedStateByteCount;
        _invalidStateByteIndex = invalidStateByteIndex;
    }

    public uint Size => _structureSize;
    public ushort Version => _structureVersion;
    public bool IsInitialized => _structureSize == StructureSize && _structureVersion == CurrentVersion && _isInitialized == 1 && _reserved0 == 0 && _reserved1 == 0;
    public M01LivePagingArenaMaterializationStatus Status => _status;
    public OS.Kernel.Memory.X64PageTableEntryStatus PageTableEntryStatus => _pageTableEntryStatus;
    public OS.Kernel.Memory.X64PageMapStatus PageImageStatus => _pageImageStatus;
    public PhysicalAddress RootTable => _rootTable;
    public ulong TablePageCount => _tablePageCount;
    public ulong VerifiedPageCount => _verifiedPageCount;
    public ulong EntryCount => _entryCount;
    public ulong VerifiedEntryCount => _verifiedEntryCount;
    public ulong InvalidPageOrdinal => _invalidPageOrdinal;
    public ulong InvalidEntryIndex => _invalidEntryIndex;
    public ulong ExpectedEntry => _expectedEntry;
    public ulong ObservedEntry => _observedEntry;
    public ulong SourceFingerprint => _sourceFingerprint;
    public ulong ObservedFingerprint => _observedFingerprint;
    public ulong StateFingerprintBefore => _stateFingerprintBefore;
    public ulong StateFingerprintAfter => _stateFingerprintAfter;
    public ulong BytesWritten => _bytesWritten;
    public ulong ReachablePageCount => _reachablePageCount;
    public ulong MappedLeafCount => _mappedLeafCount;
    public ulong VerifiedStateByteCount => _verifiedStateByteCount;
    public ulong InvalidStateByteIndex => _invalidStateByteIndex;
    public bool Succeeded => IsInitialized && _status == M01LivePagingArenaMaterializationStatus.Success && _pageTableEntryStatus == OS.Kernel.Memory.X64PageTableEntryStatus.Success && _pageImageStatus == OS.Kernel.Memory.X64PageMapStatus.Success && _rootTable.IsValid && _tablePageCount != 0 && _verifiedPageCount == _tablePageCount && _verifiedEntryCount == _entryCount && _invalidPageOrdinal == NoIndex && _invalidEntryIndex == NoIndex && _sourceFingerprint == _observedFingerprint && _stateFingerprintBefore == _stateFingerprintAfter && _reachablePageCount == _tablePageCount && _verifiedStateByteCount == M01LivePagingIntegration.RequiredArenaStateSnapshotByteCount && _invalidStateByteIndex == NoIndex;
}
