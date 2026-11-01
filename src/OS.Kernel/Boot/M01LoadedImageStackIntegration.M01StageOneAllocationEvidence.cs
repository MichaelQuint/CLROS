namespace OS.Boot;
/// <summary>
/// Binds the two allocation records to successful, ordered caller
/// observations. Map keys are deliberately absent because EFI map keys are
/// opaque and do not order allocation operations.
/// </summary>

public readonly struct M01StageOneAllocationEvidence
{
    public const uint CurrentVersion = 1;
    public const uint StructureSize = 72;
    public const ulong EfiSuccess = 0;
    private readonly uint _size;
    private readonly uint _version;
    private readonly M01StageOneAllocationProvenance _provenance;
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
    private readonly ulong _codeAllocationOrdinal;
    private readonly ulong _dataAllocationOrdinal;
    private readonly ulong _codeFirmwareStatus;
    private readonly ulong _dataFirmwareStatus;
    private M01StageOneAllocationEvidence(ulong allocationTableAddress, ulong allocationRecordCount, ulong allocationRecordCapacity, ulong codeAllocationOrdinal, ulong dataAllocationOrdinal, ulong codeFirmwareStatus, ulong dataFirmwareStatus)
    {
        _size = StructureSize;
        _version = CurrentVersion;
        _provenance = M01StageOneAllocationProvenance.CallerAttestedSuccessfulFirmwarePageAllocation;
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
        _codeAllocationOrdinal = codeAllocationOrdinal;
        _dataAllocationOrdinal = dataAllocationOrdinal;
        _codeFirmwareStatus = codeFirmwareStatus;
        _dataFirmwareStatus = dataFirmwareStatus;
    }

    public uint Size => _size;
    public uint Version => _version;
    public M01StageOneAllocationProvenance Provenance => _provenance;
    public ulong AllocationTableAddress => _allocationTableAddress;
    public ulong AllocationRecordCount => _allocationRecordCount;
    public ulong AllocationRecordCapacity => _allocationRecordCapacity;
    public ulong CodeAllocationOrdinal => _codeAllocationOrdinal;
    public ulong DataAllocationOrdinal => _dataAllocationOrdinal;
    public ulong CodeFirmwareStatus => _codeFirmwareStatus;
    public ulong DataFirmwareStatus => _dataFirmwareStatus;

    public static unsafe M01StageOneAllocationEvidence CaptureCallerAttested(BootMemoryRegionRecord* allocationRecords, ulong allocationRecordCount, ulong allocationRecordCapacity, ulong codeAllocationOrdinal, ulong dataAllocationOrdinal, ulong codeFirmwareStatus, ulong dataFirmwareStatus)
    {
        return new M01StageOneAllocationEvidence((ulong)allocationRecords, allocationRecordCount, allocationRecordCapacity, codeAllocationOrdinal, dataAllocationOrdinal, codeFirmwareStatus, dataFirmwareStatus);
    }

    public bool IsWellFormed => _size == StructureSize && _version == CurrentVersion && _provenance == M01StageOneAllocationProvenance.CallerAttestedSuccessfulFirmwarePageAllocation && _reserved0 == 0 && _reserved1 == 0 && _reserved2 == 0 && _reserved3 == 0 && _reserved4 == 0 && _reserved5 == 0 && _reserved6 == 0 && _allocationTableAddress != 0 && _allocationRecordCount == M01LoadedImageStackIntegration.RequiredAllocationRecordCount && _allocationRecordCapacity >= _allocationRecordCount && _codeAllocationOrdinal != 0 && _codeAllocationOrdinal != ~0UL && _dataAllocationOrdinal == _codeAllocationOrdinal + 1 && _codeFirmwareStatus == EfiSuccess && _dataFirmwareStatus == EfiSuccess;

    public unsafe bool IsValidFor(BootMemoryRegionRecord* allocationRecords, ulong allocationRecordCount, ulong allocationRecordCapacity)
    {
        return IsWellFormed && _allocationTableAddress == (ulong)allocationRecords && _allocationRecordCount == allocationRecordCount && _allocationRecordCapacity == allocationRecordCapacity && allocationRecords != null;
    }
}
#if !NOSTDLIB_EFI
#endif
#if !NOSTDLIB_EFI
#endif
