namespace OS.Boot;
/// <summary>
/// Binds the three allocations to the first owned map and canonical
/// reservation ledger captured after those allocations.
/// </summary>

public readonly struct M01LivePagingMapLedgerEvidence
{
    public const uint CurrentVersion = 1;
    public const uint StructureSize = 168;
    private readonly uint _size;
    private readonly uint _version;
    private readonly M01LivePagingMapLedgerProvenance _provenance;
    private readonly byte _reserved0;
    private readonly ushort _reserved1;
    private readonly uint _reserved2;
    private readonly ulong _previousMapKey;
    private readonly ulong _previousMapGeneration;
    private readonly FirmwareMemoryMapRecord _refreshedMap;
    private readonly ulong _refreshedMapGeneration;
    private readonly ulong _lastAllocationOrdinal;
    private readonly ulong _mapCaptureOrdinal;
    private readonly ulong _ledgerBuildOrdinal;
    private readonly ulong _ledgerAddress;
    private readonly ulong _ledgerCount;
    private readonly ulong _ledgerCapacity;
    private M01LivePagingMapLedgerEvidence(ulong previousMapKey, ulong previousMapGeneration, FirmwareMemoryMapRecord refreshedMap, ulong refreshedMapGeneration, ulong lastAllocationOrdinal, ulong mapCaptureOrdinal, ulong ledgerBuildOrdinal, ulong ledgerAddress, ulong ledgerCount, ulong ledgerCapacity)
    {
        _size = StructureSize;
        _version = CurrentVersion;
        _provenance = M01LivePagingMapLedgerProvenance.CallerAttestedPostAllocationOwnedMap;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _previousMapKey = previousMapKey;
        _previousMapGeneration = previousMapGeneration;
        _refreshedMap = refreshedMap;
        _refreshedMapGeneration = refreshedMapGeneration;
        _lastAllocationOrdinal = lastAllocationOrdinal;
        _mapCaptureOrdinal = mapCaptureOrdinal;
        _ledgerBuildOrdinal = ledgerBuildOrdinal;
        _ledgerAddress = ledgerAddress;
        _ledgerCount = ledgerCount;
        _ledgerCapacity = ledgerCapacity;
    }

    public uint Size => _size;
    public uint Version => _version;
    public M01LivePagingMapLedgerProvenance Provenance => _provenance;
    public ulong PreviousMapKey => _previousMapKey;
    public ulong PreviousMapGeneration => _previousMapGeneration;
    public FirmwareMemoryMapRecord RefreshedMap => _refreshedMap;
    public ulong RefreshedMapGeneration => _refreshedMapGeneration;
    public ulong LastAllocationOrdinal => _lastAllocationOrdinal;
    public ulong MapCaptureOrdinal => _mapCaptureOrdinal;
    public ulong LedgerBuildOrdinal => _ledgerBuildOrdinal;
    public ulong LedgerAddress => _ledgerAddress;
    public ulong LedgerCount => _ledgerCount;
    public ulong LedgerCapacity => _ledgerCapacity;

    public static unsafe M01LivePagingMapLedgerEvidence CaptureCallerAttested(ulong previousMapKey, ulong previousMapGeneration, FirmwareMemoryMapRecord refreshedMap, ulong refreshedMapGeneration, M01LivePagingAllocationEvidence allocations, ulong mapCaptureOrdinal, ulong ledgerBuildOrdinal, OS.Kernel.Memory.BootReservedRange* ledger, ulong ledgerCount, ulong ledgerCapacity)
    {
        return new M01LivePagingMapLedgerEvidence(previousMapKey, previousMapGeneration, refreshedMap, refreshedMapGeneration, allocations.LastAllocationOrdinal, mapCaptureOrdinal, ledgerBuildOrdinal, (ulong)ledger, ledgerCount, ledgerCapacity);
    }

    public unsafe bool IsValidFor(M01LiveAllocatorStaticState stageTwo, M01LivePagingAllocationEvidence allocations, OS.Kernel.Memory.BootReservedRange* ledger, ulong ledgerCount, ulong ledgerCapacity)
    {
        return _size == StructureSize && _version == CurrentVersion && _provenance == M01LivePagingMapLedgerProvenance.CallerAttestedPostAllocationOwnedMap && _reserved0 == 0 && _reserved1 == 0 && _reserved2 == 0 && stageTwo.IsCompleted && _previousMapKey == stageTwo.RefreshedMapKey && _previousMapGeneration == stageTwo.RefreshedMapGeneration && _previousMapGeneration != 0 && _previousMapGeneration != ~0UL && _refreshedMapGeneration == _previousMapGeneration + 1 && _previousMapKey != _refreshedMap.MapKey && IsValidOwnedMap(_refreshedMap) && _lastAllocationOrdinal == allocations.LastAllocationOrdinal && _mapCaptureOrdinal > _lastAllocationOrdinal && _ledgerBuildOrdinal > _mapCaptureOrdinal && _ledgerAddress == (ulong)ledger && _ledgerCount == ledgerCount && _ledgerCapacity == ledgerCapacity && _ledgerCount != 0 && _ledgerCount <= _ledgerCapacity && _ledgerCapacity <= OS.Kernel.Memory.BootReservationTableValidator.MaximumReservationCount;
    }

    private static bool IsValidOwnedMap(FirmwareMemoryMapRecord map)
    {
        if (!map.HasOwnedCopy() || map.Flags != FirmwareMemoryMapRecord.FlagOwnedCopy || map.BufferAddress == 0 || map.BufferSize == 0 || map.SourceBufferAddress == 0 || map.SourceBufferSize == 0 || map.MapSize == 0 || map.MapSize > map.BufferSize || map.MapSize > map.SourceBufferSize || map.MapKey == 0 || map.DescriptorSize < 40 || (map.DescriptorSize & 7) != 0 || map.DescriptorVersion == 0 || map.DescriptorCount == 0 || map.DescriptorCount > 4096 || map.DescriptorCount > ~0UL / map.DescriptorSize || map.MapSize != map.DescriptorCount * map.DescriptorSize || map.BufferAddress > ~0UL - map.BufferSize || map.SourceBufferAddress > ~0UL - map.SourceBufferSize)
        {
            return false;
        }

        ulong bufferEnd = map.BufferAddress + map.BufferSize;
        ulong sourceEnd = map.SourceBufferAddress + map.SourceBufferSize;
        return bufferEnd <= map.SourceBufferAddress || sourceEnd <= map.BufferAddress;
    }
}
