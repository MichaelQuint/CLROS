namespace OS.Boot;
/// <summary>
/// Binds one changed opaque EFI map key and an exactly advanced CLROS-owned
/// generation to the canonical post-allocation reservation ledger.
/// </summary>

public readonly struct M01StageOneMapLedgerEvidence
{
    public const uint CurrentVersion = 1;
    public const uint StructureSize = 176;
    private readonly uint _size;
    private readonly uint _version;
    private readonly M01StageOneMapLedgerProvenance _provenance;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly byte _reserved2;
    private readonly byte _reserved3;
    private readonly byte _reserved4;
    private readonly byte _reserved5;
    private readonly byte _reserved6;
    private readonly ulong _previousMapKey;
    private readonly ulong _previousMapGeneration;
    private readonly FirmwareMemoryMapRecord _refreshedMap;
    private readonly ulong _refreshedMapGeneration;
    private readonly ulong _codeAllocationOrdinal;
    private readonly ulong _dataAllocationOrdinal;
    private readonly ulong _mapCaptureOrdinal;
    private readonly ulong _ledgerBuildOrdinal;
    private readonly ulong _ledgerAddress;
    private readonly ulong _ledgerCount;
    private readonly ulong _ledgerCapacity;
    private M01StageOneMapLedgerEvidence(ulong previousMapKey, ulong previousMapGeneration, FirmwareMemoryMapRecord refreshedMap, ulong refreshedMapGeneration, ulong codeAllocationOrdinal, ulong dataAllocationOrdinal, ulong mapCaptureOrdinal, ulong ledgerBuildOrdinal, ulong ledgerAddress, ulong ledgerCount, ulong ledgerCapacity)
    {
        _size = StructureSize;
        _version = CurrentVersion;
        _provenance = M01StageOneMapLedgerProvenance.CallerAttestedPostAllocationOwnedMap;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _reserved3 = 0;
        _reserved4 = 0;
        _reserved5 = 0;
        _reserved6 = 0;
        _previousMapKey = previousMapKey;
        _previousMapGeneration = previousMapGeneration;
        _refreshedMap = refreshedMap;
        _refreshedMapGeneration = refreshedMapGeneration;
        _codeAllocationOrdinal = codeAllocationOrdinal;
        _dataAllocationOrdinal = dataAllocationOrdinal;
        _mapCaptureOrdinal = mapCaptureOrdinal;
        _ledgerBuildOrdinal = ledgerBuildOrdinal;
        _ledgerAddress = ledgerAddress;
        _ledgerCount = ledgerCount;
        _ledgerCapacity = ledgerCapacity;
    }

    public uint Size => _size;
    public uint Version => _version;
    public M01StageOneMapLedgerProvenance Provenance => _provenance;
    public ulong PreviousMapKey => _previousMapKey;
    public ulong PreviousMapGeneration => _previousMapGeneration;
    public FirmwareMemoryMapRecord RefreshedMap => _refreshedMap;
    public ulong RefreshedMapGeneration => _refreshedMapGeneration;
    public ulong CodeAllocationOrdinal => _codeAllocationOrdinal;
    public ulong DataAllocationOrdinal => _dataAllocationOrdinal;
    public ulong MapCaptureOrdinal => _mapCaptureOrdinal;
    public ulong LedgerBuildOrdinal => _ledgerBuildOrdinal;
    public ulong LedgerAddress => _ledgerAddress;
    public ulong LedgerCount => _ledgerCount;
    public ulong LedgerCapacity => _ledgerCapacity;

    public static unsafe M01StageOneMapLedgerEvidence CaptureCallerAttested(ulong previousMapKey, ulong previousMapGeneration, FirmwareMemoryMapRecord refreshedMap, ulong refreshedMapGeneration, M01StageOneAllocationEvidence allocations, ulong mapCaptureOrdinal, ulong ledgerBuildOrdinal, OS.Kernel.Memory.BootReservedRange* ledger, ulong ledgerCount, ulong ledgerCapacity)
    {
        return new M01StageOneMapLedgerEvidence(previousMapKey, previousMapGeneration, refreshedMap, refreshedMapGeneration, allocations.CodeAllocationOrdinal, allocations.DataAllocationOrdinal, mapCaptureOrdinal, ledgerBuildOrdinal, (ulong)ledger, ledgerCount, ledgerCapacity);
    }

    public bool IsWellFormedFor(M01StageOneAllocationEvidence allocations)
    {
        return _size == StructureSize && _version == CurrentVersion && _provenance == M01StageOneMapLedgerProvenance.CallerAttestedPostAllocationOwnedMap && _reserved0 == 0 && _reserved1 == 0 && _reserved2 == 0 && _reserved3 == 0 && _reserved4 == 0 && _reserved5 == 0 && _reserved6 == 0 && _previousMapGeneration != 0 && _previousMapGeneration != ~0UL && _refreshedMapGeneration == _previousMapGeneration + 1 && _previousMapKey != _refreshedMap.MapKey && _refreshedMap.HasOwnedCopy() && _refreshedMap.Flags == FirmwareMemoryMapRecord.FlagOwnedCopy && _refreshedMap.BufferAddress != 0 && _refreshedMap.BufferSize >= _refreshedMap.MapSize && _refreshedMap.SourceBufferAddress != 0 && _refreshedMap.SourceBufferSize >= _refreshedMap.MapSize && _refreshedMap.MapSize != 0 && _refreshedMap.DescriptorSize != 0 && _refreshedMap.DescriptorVersion != 0 && _refreshedMap.DescriptorCount != 0 && _refreshedMap.DescriptorCount <= ~0UL / _refreshedMap.DescriptorSize && _refreshedMap.DescriptorCount * _refreshedMap.DescriptorSize == _refreshedMap.MapSize && _refreshedMap.BufferAddress <= ~0UL - _refreshedMap.BufferSize && _refreshedMap.SourceBufferAddress <= ~0UL - _refreshedMap.SourceBufferSize && !RangesOverlap(_refreshedMap.BufferAddress, _refreshedMap.BufferAddress + _refreshedMap.BufferSize, _refreshedMap.SourceBufferAddress, _refreshedMap.SourceBufferAddress + _refreshedMap.SourceBufferSize) && allocations.IsWellFormed && _codeAllocationOrdinal == allocations.CodeAllocationOrdinal && _dataAllocationOrdinal == allocations.DataAllocationOrdinal && _mapCaptureOrdinal > _codeAllocationOrdinal && _mapCaptureOrdinal > _dataAllocationOrdinal && _ledgerBuildOrdinal > _mapCaptureOrdinal && _ledgerAddress != 0 && _ledgerCount != 0 && _ledgerCapacity >= _ledgerCount;
    }

    public unsafe bool IsValidFor(M01StageOneAllocationEvidence allocations, OS.Kernel.Memory.BootReservedRange* ledger, ulong ledgerCount, ulong ledgerCapacity)
    {
        if (!IsWellFormedFor(allocations) || _ledgerAddress != (ulong)ledger || _ledgerCount != ledgerCount || _ledgerCapacity != ledgerCapacity || ledger == null)
        {
            return false;
        }

        return true;
    }

    private static bool RangesOverlap(ulong firstStart, ulong firstEnd, ulong secondStart, ulong secondEnd)
    {
        return firstStart < secondEnd && secondStart < firstEnd;
    }
}
