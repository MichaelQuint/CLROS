namespace OS.Boot;
/// <summary>
/// Binds exactly one allocation record to a successful, ordered caller
/// observation. The firmware status is an attestation supplied by the future
/// live adapter.
/// </summary>

public readonly struct M01LiveAllocatorAllocationEvidence
{
    public const uint CurrentVersion = 1;
    public const uint StructureSize = 56;
    public const ulong EfiSuccess = 0;
    private readonly uint _size;
    private readonly uint _version;
    private readonly M01LiveAllocatorAllocationProvenance _provenance;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly byte _reserved2;
    private readonly byte _reserved3;
    private readonly byte _reserved4;
    private readonly byte _reserved5;
    private readonly byte _reserved6;
    private readonly ulong _allocationTableAddress;
    private readonly ulong _allocationRecordCount;
    private readonly ulong _allocationRecordCapacity;
    private readonly ulong _allocationOrdinal;
    private readonly ulong _firmwareStatus;
    private M01LiveAllocatorAllocationEvidence(ulong allocationTableAddress, ulong allocationRecordCount, ulong allocationRecordCapacity, ulong allocationOrdinal, ulong firmwareStatus)
    {
        _size = StructureSize;
        _version = CurrentVersion;
        _provenance = M01LiveAllocatorAllocationProvenance.CallerAttestedSuccessfulFirmwarePageAllocation;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _reserved3 = 0;
        _reserved4 = 0;
        _reserved5 = 0;
        _reserved6 = 0;
        _allocationTableAddress = allocationTableAddress;
        _allocationRecordCount = allocationRecordCount;
        _allocationRecordCapacity = allocationRecordCapacity;
        _allocationOrdinal = allocationOrdinal;
        _firmwareStatus = firmwareStatus;
    }

    public uint Size => _size;
    public uint Version => _version;
    public M01LiveAllocatorAllocationProvenance Provenance => _provenance;
    public ulong AllocationTableAddress => _allocationTableAddress;
    public ulong AllocationRecordCount => _allocationRecordCount;
    public ulong AllocationRecordCapacity => _allocationRecordCapacity;
    public ulong AllocationOrdinal => _allocationOrdinal;
    public ulong FirmwareStatus => _firmwareStatus;

    public static unsafe M01LiveAllocatorAllocationEvidence CaptureCallerAttested(BootMemoryRegionRecord* allocationRecords, ulong allocationRecordCount, ulong allocationRecordCapacity, ulong allocationOrdinal, ulong firmwareStatus)
    {
        return new M01LiveAllocatorAllocationEvidence((ulong)allocationRecords, allocationRecordCount, allocationRecordCapacity, allocationOrdinal, firmwareStatus);
    }

    public bool IsWellFormed => _size == StructureSize && _version == CurrentVersion && _provenance == M01LiveAllocatorAllocationProvenance.CallerAttestedSuccessfulFirmwarePageAllocation && _reserved0 == 0 && _reserved1 == 0 && _reserved2 == 0 && _reserved3 == 0 && _reserved4 == 0 && _reserved5 == 0 && _reserved6 == 0 && _allocationTableAddress != 0 && _allocationRecordCount == M01LiveAllocatorIntegration.RequiredAllocationRecordCount && _allocationRecordCapacity >= _allocationRecordCount && _allocationRecordCapacity <= M01LiveAllocatorIntegration.MaximumAllocationRecordCapacity && _allocationOrdinal != 0 && _allocationOrdinal != ~0UL && _firmwareStatus == EfiSuccess;

    public unsafe bool IsValidFor(BootMemoryRegionRecord* allocationRecords, ulong allocationRecordCount, ulong allocationRecordCapacity)
    {
        return IsWellFormed && allocationRecords != null && _allocationTableAddress == (ulong)allocationRecords && _allocationRecordCount == allocationRecordCount && _allocationRecordCapacity == allocationRecordCapacity;
    }
}
