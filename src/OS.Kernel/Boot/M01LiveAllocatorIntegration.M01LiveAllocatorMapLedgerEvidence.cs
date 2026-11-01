namespace OS.Boot;
/// <summary>
/// Binds a changed opaque map key, exactly advanced CLROS generation, and
/// canonical post-arena reservation ledger to the arena allocation ordinal.
/// </summary>

public readonly struct M01LiveAllocatorMapLedgerEvidence
{
    public const uint CurrentVersion = 1;
    public const uint StructureSize = 168;
    private readonly uint _size;
    private readonly uint _version;
    private readonly M01LiveAllocatorMapLedgerProvenance _provenance;
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
    private readonly ulong _arenaAllocationOrdinal;
    private readonly ulong _mapCaptureOrdinal;
    private readonly ulong _ledgerBuildOrdinal;
    private readonly ulong _ledgerAddress;
    private readonly ulong _ledgerCount;
    private readonly ulong _ledgerCapacity;
    private M01LiveAllocatorMapLedgerEvidence(ulong previousMapKey, ulong previousMapGeneration, FirmwareMemoryMapRecord refreshedMap, ulong refreshedMapGeneration, ulong arenaAllocationOrdinal, ulong mapCaptureOrdinal, ulong ledgerBuildOrdinal, ulong ledgerAddress, ulong ledgerCount, ulong ledgerCapacity)
    {
        _size = StructureSize;
        _version = CurrentVersion;
        _provenance = M01LiveAllocatorMapLedgerProvenance.CallerAttestedPostArenaAllocationOwnedMap;
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
        _arenaAllocationOrdinal = arenaAllocationOrdinal;
        _mapCaptureOrdinal = mapCaptureOrdinal;
        _ledgerBuildOrdinal = ledgerBuildOrdinal;
        _ledgerAddress = ledgerAddress;
        _ledgerCount = ledgerCount;
        _ledgerCapacity = ledgerCapacity;
    }

    public uint Size => _size;
    public uint Version => _version;
    public M01LiveAllocatorMapLedgerProvenance Provenance => _provenance;
    public ulong PreviousMapKey => _previousMapKey;
    public ulong PreviousMapGeneration => _previousMapGeneration;
    public FirmwareMemoryMapRecord RefreshedMap => _refreshedMap;
    public ulong RefreshedMapGeneration => _refreshedMapGeneration;
    public ulong ArenaAllocationOrdinal => _arenaAllocationOrdinal;
    public ulong MapCaptureOrdinal => _mapCaptureOrdinal;
    public ulong LedgerBuildOrdinal => _ledgerBuildOrdinal;
    public ulong LedgerAddress => _ledgerAddress;
    public ulong LedgerCount => _ledgerCount;
    public ulong LedgerCapacity => _ledgerCapacity;

    public static unsafe M01LiveAllocatorMapLedgerEvidence CaptureCallerAttested(ulong previousMapKey, ulong previousMapGeneration, FirmwareMemoryMapRecord refreshedMap, ulong refreshedMapGeneration, M01LiveAllocatorAllocationEvidence allocation, ulong mapCaptureOrdinal, ulong ledgerBuildOrdinal, OS.Kernel.Memory.BootReservedRange* ledger, ulong ledgerCount, ulong ledgerCapacity)
    {
        return new M01LiveAllocatorMapLedgerEvidence(previousMapKey, previousMapGeneration, refreshedMap, refreshedMapGeneration, allocation.AllocationOrdinal, mapCaptureOrdinal, ledgerBuildOrdinal, (ulong)ledger, ledgerCount, ledgerCapacity);
    }

    public bool IsWellFormedFor(M01LiveAllocatorAllocationEvidence allocation)
    {
        return _size == StructureSize && _version == CurrentVersion && _provenance == M01LiveAllocatorMapLedgerProvenance.CallerAttestedPostArenaAllocationOwnedMap && _reserved0 == 0 && _reserved1 == 0 && _reserved2 == 0 && _reserved3 == 0 && _reserved4 == 0 && _reserved5 == 0 && _reserved6 == 0 && _previousMapGeneration != 0 && _previousMapGeneration != ~0UL && _refreshedMapGeneration == _previousMapGeneration + 1 && _previousMapKey != _refreshedMap.MapKey && IsValidOwnedMap(_refreshedMap) && allocation.IsWellFormed && _arenaAllocationOrdinal == allocation.AllocationOrdinal && _mapCaptureOrdinal > _arenaAllocationOrdinal && _ledgerBuildOrdinal > _mapCaptureOrdinal && _ledgerAddress != 0 && _ledgerCount != 0 && _ledgerCount <= OS.Kernel.Memory.BootReservationTableValidator.MaximumReservationCount && _ledgerCapacity >= _ledgerCount && _ledgerCapacity <= OS.Kernel.Memory.BootReservationTableValidator.MaximumReservationCount;
    }

    public unsafe bool IsValidFor(M01LiveAllocatorAllocationEvidence allocation, OS.Kernel.Memory.BootReservedRange* ledger, ulong ledgerCount, ulong ledgerCapacity)
    {
        return IsWellFormedFor(allocation) && ledger != null && _ledgerAddress == (ulong)ledger && _ledgerCount == ledgerCount && _ledgerCapacity == ledgerCapacity;
    }

    private static bool IsValidOwnedMap(FirmwareMemoryMapRecord map)
    {
        if (!map.HasOwnedCopy() || map.Flags != FirmwareMemoryMapRecord.FlagOwnedCopy || map.BufferAddress == 0 || map.SourceBufferAddress == 0 || map.MapSize == 0 || map.BufferSize < map.MapSize || map.SourceBufferSize < map.MapSize || map.DescriptorSize == 0 || map.DescriptorVersion == 0 || map.DescriptorCount == 0 || map.DescriptorCount > ~0UL / map.DescriptorSize || map.DescriptorCount * map.DescriptorSize != map.MapSize || map.BufferAddress > ~0UL - map.BufferSize || map.SourceBufferAddress > ~0UL - map.SourceBufferSize)
        {
            return false;
        }

        ulong bufferEnd = map.BufferAddress + map.BufferSize;
        ulong sourceEnd = map.SourceBufferAddress + map.SourceBufferSize;
        return !RangesOverlap(map.BufferAddress, bufferEnd, map.SourceBufferAddress, sourceEnd);
    }

    private static bool RangesOverlap(ulong firstStart, ulong firstEnd, ulong secondStart, ulong secondEnd)
    {
        return firstStart < secondEnd && secondStart < firstEnd;
    }
}
