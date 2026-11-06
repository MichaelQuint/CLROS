namespace OS.Boot;
/// <summary>
/// Binds the exact three allocation records to successful EFI status and
/// strictly increasing call ordinals. It is caller attestation, not evidence
/// that this host-only coordinator called AllocatePages.
/// </summary>

public readonly struct M01LivePagingAllocationEvidence
{
    public const uint CurrentVersion = 1;
    public const uint StructureSize = 88;
    public const ulong EfiSuccess = 0;
    private readonly uint _size;
    private readonly uint _version;
    private readonly M01LivePagingAllocationProvenance _provenance;
    private readonly byte _reserved0;
    private readonly ushort _reserved1;
    private readonly uint _reserved2;
    private readonly ulong _allocationTableAddress;
    private readonly ulong _allocationRecordCount;
    private readonly ulong _allocationRecordCapacity;
    private readonly ulong _cr3CodeAllocationOrdinal;
    private readonly ulong _probeAllocationOrdinal;
    private readonly ulong _tableStateAllocationOrdinal;
    private readonly ulong _cr3CodeStatus;
    private readonly ulong _probeStatus;
    private readonly ulong _tableStateStatus;
    private M01LivePagingAllocationEvidence(ulong allocationTableAddress, ulong allocationRecordCount, ulong allocationRecordCapacity, ulong cr3CodeAllocationOrdinal, ulong probeAllocationOrdinal, ulong tableStateAllocationOrdinal, ulong cr3CodeStatus, ulong probeStatus, ulong tableStateStatus)
    {
        _size = StructureSize;
        _version = CurrentVersion;
        _provenance = M01LivePagingAllocationProvenance.CallerAttestedSuccessfulFirmwarePageAllocations;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _allocationTableAddress = allocationTableAddress;
        _allocationRecordCount = allocationRecordCount;
        _allocationRecordCapacity = allocationRecordCapacity;
        _cr3CodeAllocationOrdinal = cr3CodeAllocationOrdinal;
        _probeAllocationOrdinal = probeAllocationOrdinal;
        _tableStateAllocationOrdinal = tableStateAllocationOrdinal;
        _cr3CodeStatus = cr3CodeStatus;
        _probeStatus = probeStatus;
        _tableStateStatus = tableStateStatus;
    }

    public uint Size => _size;
    public uint Version => _version;
    public M01LivePagingAllocationProvenance Provenance => _provenance;
    public ulong AllocationTableAddress => _allocationTableAddress;
    public ulong AllocationRecordCount => _allocationRecordCount;
    public ulong AllocationRecordCapacity => _allocationRecordCapacity;
    public ulong Cr3CodeAllocationOrdinal => _cr3CodeAllocationOrdinal;
    public ulong ProbeAllocationOrdinal => _probeAllocationOrdinal;
    public ulong TableStateAllocationOrdinal => _tableStateAllocationOrdinal;
    public ulong Cr3CodeStatus => _cr3CodeStatus;
    public ulong ProbeStatus => _probeStatus;
    public ulong TableStateStatus => _tableStateStatus;
    public ulong LastAllocationOrdinal => _tableStateAllocationOrdinal;

    public static unsafe M01LivePagingAllocationEvidence CaptureCallerAttested(BootMemoryRegionRecord* allocationRecords, ulong allocationRecordCount, ulong allocationRecordCapacity, ulong cr3CodeAllocationOrdinal, ulong probeAllocationOrdinal, ulong tableStateAllocationOrdinal, ulong cr3CodeStatus, ulong probeStatus, ulong tableStateStatus)
    {
        return new M01LivePagingAllocationEvidence((ulong)allocationRecords, allocationRecordCount, allocationRecordCapacity, cr3CodeAllocationOrdinal, probeAllocationOrdinal, tableStateAllocationOrdinal, cr3CodeStatus, probeStatus, tableStateStatus);
    }

    public unsafe bool IsValidFor(BootMemoryRegionRecord* allocationRecords, ulong allocationRecordCount, ulong allocationRecordCapacity)
    {
        return _size == StructureSize && _version == CurrentVersion && _provenance == M01LivePagingAllocationProvenance.CallerAttestedSuccessfulFirmwarePageAllocations && _reserved0 == 0 && _reserved1 == 0 && _reserved2 == 0 && _allocationTableAddress == (ulong)allocationRecords && _allocationRecordCount == M01LivePagingIntegration.RequiredAllocationRecordCount && _allocationRecordCount == allocationRecordCount && _allocationRecordCapacity == allocationRecordCapacity && _allocationRecordCapacity >= _allocationRecordCount && _allocationRecordCapacity <= M01LivePagingIntegration.MaximumAllocationRecordCapacity && _cr3CodeAllocationOrdinal != 0 && _cr3CodeAllocationOrdinal != ~0UL && _probeAllocationOrdinal != 0 && _probeAllocationOrdinal != ~0UL && _tableStateAllocationOrdinal != 0 && _tableStateAllocationOrdinal != ~0UL && _probeAllocationOrdinal == _cr3CodeAllocationOrdinal + 1 && _tableStateAllocationOrdinal == _probeAllocationOrdinal + 1 && _cr3CodeStatus == EfiSuccess && _probeStatus == EfiSuccess && _tableStateStatus == EfiSuccess;
    }
}
