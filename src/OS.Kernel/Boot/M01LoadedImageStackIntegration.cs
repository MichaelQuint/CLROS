namespace OS.Boot;

/// <summary>
/// Reports the first failing boundary, or success, while composing the
/// reviewed M01-003, M01-004, and M01-005 contracts. Existing values are
/// append-only and the zero-valued default is intentionally invalid.
/// </summary>
public enum M01LoadedImageStackStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    LoadedImageRejected = 2,
    AllocationTableRejected = 3,
    AllocationEvidenceRejected = 4,
    MapLedgerEvidenceRejected = 5,
    ReservationLedgerRejected = 6,
    ReservationIdentityRejected = 7,
    StackLayoutRejected = 8,
    StackLifetimeRejected = 9,
    StaticStateRejected = 10,
    SwitchPlanRejected = 11,
    StubEmissionRejected = 12,
    StubVerificationRejected = 13,
    HandoffMaterializationRejected = 14,
    StateInitializationRejected = 15,
    ArmRejected = 16,
    TransferRejected = 17,
    ContinuationRspRejected = 18,
    UnexpectedReturn = 19,
    IllegalCoordinatorState = 20,
    StubByteModeRejected = 21,
    LoadedImageVirtualMappingRejected = 22,
    StageOneAddressMappingRejected = 23
}

/// <summary>
/// Identifies the last successfully completed coordinator checkpoint.
/// Values describe host-audited state transitions, not processor execution.
/// </summary>
public enum M01LoadedImageStackCheckpoint : byte
{
    Uninitialized = 0,
    LoadedImageValidated = 1,
    AllocationEvidenceValidated = 2,
    MapLedgerValidated = 3,
    StackLayoutReady = 4,
    SwitchPlanReady = 5,
    StubVerified = 6,
    SwitchArmed = 7,
    TransferRecorded = 8,
    ContinuationRspContained = 9
}

/// <summary>
/// Identifies the only allocation provenance accepted by stage one. This is
/// a caller attestation and is not evidence that this type called firmware.
/// </summary>
public enum M01StageOneAllocationProvenance : byte
{
    Uninitialized = 0,
    CallerAttestedSuccessfulFirmwarePageAllocation = 1
}

/// <summary>
/// Identifies the only map/ledger provenance accepted by stage one. This is
/// a caller attestation over an owned map copy and canonical ledger.
/// </summary>
public enum M01StageOneMapLedgerProvenance : byte
{
    Uninitialized = 0,
    CallerAttestedPostAllocationOwnedMap = 1
}

/// <summary>
/// Selects whether the coordinator materializes the audited bytes before
/// readback or audits a caller-materialized page. The second mode permits an
/// exact malformed-readback failure without a test-only callback.
/// </summary>
public enum M01StageOneStubByteMode : byte
{
    Uninitialized = 0,
    EmitAndVerify = 1,
    VerifyExisting = 2
}

/// <summary>
/// Identifies the explicitly attested pre-hierarchy mapping used to compare
/// loaded-image virtual storage with virtual pointers.
/// </summary>
public enum M01LoadedImageVirtualMappingProvenance : byte
{
    Uninitialized = 0,
    CallerAttestedIdentityMapping = 1
}

/// <summary>
/// Identifies the explicit pre-hierarchy relationship between the physical
/// allocation/reservation domain and the dereferenceable virtual-pointer
/// domain used by stage one.
/// </summary>
public enum M01StageOneAddressMappingProvenance : byte
{
    Uninitialized = 0,
    CallerAttestedIdentityMappings = 1
}

/// <summary>
/// Preserves address domains while binding the M01-003 physical image range
/// to an explicitly attested pre-hierarchy virtual identity mapping.
/// </summary>
public readonly struct M01LoadedImageVirtualMappingEvidence
{
    public const uint CurrentVersion = 1;
    public const uint StructureSize = 56;

    private readonly uint _size;
    private readonly uint _version;
    private readonly M01LoadedImageVirtualMappingProvenance _provenance;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly byte _reserved2;
    private readonly byte _reserved3;
    private readonly byte _reserved4;
    private readonly byte _reserved5;
    private readonly byte _reserved6;
    private readonly PhysicalAddress _physicalImageBase;
    private readonly PhysicalAddress _physicalImageEndExclusive;
    private readonly VirtualAddress _virtualImageBase;
    private readonly VirtualAddress _virtualImageEndExclusive;
    private readonly ulong _imageByteLength;

    private M01LoadedImageVirtualMappingEvidence(
        PhysicalAddress physicalImageBase,
        PhysicalAddress physicalImageEndExclusive,
        VirtualAddress virtualImageBase,
        VirtualAddress virtualImageEndExclusive,
        ulong imageByteLength)
    {
        _size = StructureSize;
        _version = CurrentVersion;
        _provenance = M01LoadedImageVirtualMappingProvenance
            .CallerAttestedIdentityMapping;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _reserved3 = 0;
        _reserved4 = 0;
        _reserved5 = 0;
        _reserved6 = 0;
        _physicalImageBase = physicalImageBase;
        _physicalImageEndExclusive = physicalImageEndExclusive;
        _virtualImageBase = virtualImageBase;
        _virtualImageEndExclusive = virtualImageEndExclusive;
        _imageByteLength = imageByteLength;
    }

    public uint Size => _size;

    public uint Version => _version;

    public M01LoadedImageVirtualMappingProvenance Provenance =>
        _provenance;

    public PhysicalAddress PhysicalImageBase => _physicalImageBase;

    public PhysicalAddress PhysicalImageEndExclusive =>
        _physicalImageEndExclusive;

    public VirtualAddress VirtualImageBase => _virtualImageBase;

    public VirtualAddress VirtualImageEndExclusive =>
        _virtualImageEndExclusive;

    public ulong ImageByteLength => _imageByteLength;

    public static M01LoadedImageVirtualMappingEvidence
        CaptureCallerAttestedIdentity(
            EfiLoadedImageRangeCapturePlan loadedImage)
    {
        if (!loadedImage.Succeeded
            || loadedImage.Range.ImageBase
                    .TryGetRawValueForUefiBoundary(
                        out ulong physicalBase)
                != AddressValueStatus.Success
            || loadedImage.Range.ImageEndExclusive
                    .TryGetRawValueForUefiBoundary(
                        out ulong physicalEnd)
                != AddressValueStatus.Success
            || VirtualAddress.TryCreateFromUefiBoundary(
                    physicalBase,
                    out VirtualAddress virtualBase)
                != AddressValueStatus.Success
            || VirtualAddress.TryCreateFromUefiBoundary(
                    physicalEnd,
                    out VirtualAddress virtualEnd)
                != AddressValueStatus.Success)
        {
            return default;
        }

        return new M01LoadedImageVirtualMappingEvidence(
            loadedImage.Range.ImageBase,
            loadedImage.Range.ImageEndExclusive,
            virtualBase,
            virtualEnd,
            loadedImage.Range.ImageSize);
    }

    public bool IsValidFor(
        EfiLoadedImageRangeCapturePlan loadedImage)
    {
        if (_size != StructureSize
            || _version != CurrentVersion
            || _provenance
                != M01LoadedImageVirtualMappingProvenance
                    .CallerAttestedIdentityMapping
            || _reserved0 != 0
            || _reserved1 != 0
            || _reserved2 != 0
            || _reserved3 != 0
            || _reserved4 != 0
            || _reserved5 != 0
            || _reserved6 != 0
            || !loadedImage.Succeeded
            || !_physicalImageBase.Equals(
                loadedImage.Range.ImageBase)
            || !_physicalImageEndExclusive.Equals(
                loadedImage.Range.ImageEndExclusive)
            || _imageByteLength != loadedImage.Range.ImageSize
            || !_virtualImageBase.IsValid
            || !_virtualImageEndExclusive.IsValid
            || _virtualImageBase.TryGetEndExclusive(
                    _imageByteLength,
                    out VirtualAddress expectedVirtualEnd)
                != AddressValueStatus.Success
            || !expectedVirtualEnd.Equals(
                _virtualImageEndExclusive)
            || _physicalImageBase.TryGetRawValueForUefiBoundary(
                    out ulong physicalBase)
                != AddressValueStatus.Success
            || _physicalImageEndExclusive
                    .TryGetRawValueForUefiBoundary(
                        out ulong physicalEnd)
                != AddressValueStatus.Success
            || _virtualImageBase.TryGetRawValueForUefiBoundary(
                    out ulong virtualBase)
                != AddressValueStatus.Success
            || _virtualImageEndExclusive
                    .TryGetRawValueForUefiBoundary(
                        out ulong virtualEnd)
                != AddressValueStatus.Success)
        {
            return false;
        }

        return physicalBase == virtualBase
            && physicalEnd == virtualEnd;
    }
}

/// <summary>
/// Binds the code allocation, data allocation, and owned memory-map
/// destination to caller-attested pre-hierarchy identity mappings. This
/// evidence is required before any physical reservation is compared with a
/// dereferenceable virtual address.
/// </summary>
public readonly struct M01StageOneAddressMappingEvidence
{
    public const uint CurrentVersion = 1;
    public const uint StructureSize = 72;

    private const ulong RequiredAllocationTableAlignment = 8;
    private const ulong MaximumUnsignedLong = ~0UL;

    private readonly uint _size;
    private readonly uint _version;
    private readonly M01StageOneAddressMappingProvenance _provenance;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly byte _reserved2;
    private readonly byte _reserved3;
    private readonly byte _reserved4;
    private readonly byte _reserved5;
    private readonly byte _reserved6;
    private readonly PhysicalAddress _codePhysicalStart;
    private readonly VirtualAddress _codeVirtualStart;
    private readonly PhysicalAddress _dataPhysicalStart;
    private readonly VirtualAddress _dataVirtualStart;
    private readonly PhysicalAddress _ownedMapPhysicalStart;
    private readonly VirtualAddress _ownedMapVirtualStart;
    private readonly ulong _ownedMapByteLength;

    private M01StageOneAddressMappingEvidence(
        PhysicalAddress codePhysicalStart,
        VirtualAddress codeVirtualStart,
        PhysicalAddress dataPhysicalStart,
        VirtualAddress dataVirtualStart,
        PhysicalAddress ownedMapPhysicalStart,
        VirtualAddress ownedMapVirtualStart,
        ulong ownedMapByteLength)
    {
        _size = StructureSize;
        _version = CurrentVersion;
        _provenance = M01StageOneAddressMappingProvenance
            .CallerAttestedIdentityMappings;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _reserved3 = 0;
        _reserved4 = 0;
        _reserved5 = 0;
        _reserved6 = 0;
        _codePhysicalStart = codePhysicalStart;
        _codeVirtualStart = codeVirtualStart;
        _dataPhysicalStart = dataPhysicalStart;
        _dataVirtualStart = dataVirtualStart;
        _ownedMapPhysicalStart = ownedMapPhysicalStart;
        _ownedMapVirtualStart = ownedMapVirtualStart;
        _ownedMapByteLength = ownedMapByteLength;
    }

    public uint Size => _size;

    public uint Version => _version;

    public M01StageOneAddressMappingProvenance Provenance =>
        _provenance;

    public PhysicalAddress CodePhysicalStart => _codePhysicalStart;

    public VirtualAddress CodeVirtualStart => _codeVirtualStart;

    public PhysicalAddress DataPhysicalStart => _dataPhysicalStart;

    public VirtualAddress DataVirtualStart => _dataVirtualStart;

    public PhysicalAddress OwnedMapPhysicalStart =>
        _ownedMapPhysicalStart;

    public VirtualAddress OwnedMapVirtualStart =>
        _ownedMapVirtualStart;

    public ulong OwnedMapByteLength => _ownedMapByteLength;

    public static unsafe M01StageOneAddressMappingEvidence
        CaptureCallerAttestedIdentity(
            BootMemoryRegionRecord* allocationRecords,
            ulong allocationRecordCount,
            ulong allocationRecordCapacity,
            FirmwareMemoryMapRecord ownedMap)
    {
        ulong allocationTableAddress = (ulong)allocationRecords;
        ulong allocationTableByteLength =
            M01LoadedImageStackIntegration
                .RequiredAllocationRecordCount
            * (ulong)sizeof(BootMemoryRegionRecord);
        if (allocationRecords == null
            || allocationRecordCount
                != M01LoadedImageStackIntegration
                    .RequiredAllocationRecordCount
            || allocationRecordCapacity < allocationRecordCount
            || (allocationTableAddress
                & (RequiredAllocationTableAlignment - 1)) != 0
            || allocationTableAddress
                > MaximumUnsignedLong - allocationTableByteLength)
        {
            return default;
        }

        BootMemoryRegionRecord code =
            allocationRecords[
                M01LoadedImageStackIntegration
                    .CodeAllocationRecordIndex];
        BootMemoryRegionRecord data =
            allocationRecords[
                M01LoadedImageStackIntegration
                    .DataAllocationRecordIndex];
        if (code.PhysicalStart != code.VirtualStart
            || data.PhysicalStart != data.VirtualStart
            || ownedMap.BufferAddress == 0
            || ownedMap.BufferSize == 0
            || PhysicalAddress.TryCreateFromUefiBoundary(
                    code.PhysicalStart,
                    out PhysicalAddress codePhysicalStart)
                != AddressValueStatus.Success
            || VirtualAddress.TryCreateFromUefiBoundary(
                    code.VirtualStart,
                    out VirtualAddress codeVirtualStart)
                != AddressValueStatus.Success
            || PhysicalAddress.TryCreateFromUefiBoundary(
                    data.PhysicalStart,
                    out PhysicalAddress dataPhysicalStart)
                != AddressValueStatus.Success
            || VirtualAddress.TryCreateFromUefiBoundary(
                    data.VirtualStart,
                    out VirtualAddress dataVirtualStart)
                != AddressValueStatus.Success
            || PhysicalAddress.TryCreateFromUefiBoundary(
                    ownedMap.BufferAddress,
                    out PhysicalAddress ownedMapPhysicalStart)
                != AddressValueStatus.Success
            || VirtualAddress.TryCreateFromUefiBoundary(
                    ownedMap.BufferAddress,
                    out VirtualAddress ownedMapVirtualStart)
                != AddressValueStatus.Success)
        {
            return default;
        }

        return new M01StageOneAddressMappingEvidence(
            codePhysicalStart,
            codeVirtualStart,
            dataPhysicalStart,
            dataVirtualStart,
            ownedMapPhysicalStart,
            ownedMapVirtualStart,
            ownedMap.BufferSize);
    }

    public bool IsValidFor(
        BootMemoryRegionRecord code,
        BootMemoryRegionRecord data,
        FirmwareMemoryMapRecord ownedMap)
    {
        if (_size != StructureSize
            || _version != CurrentVersion
            || _provenance
                != M01StageOneAddressMappingProvenance
                    .CallerAttestedIdentityMappings
            || _reserved0 != 0
            || _reserved1 != 0
            || _reserved2 != 0
            || _reserved3 != 0
            || _reserved4 != 0
            || _reserved5 != 0
            || _reserved6 != 0
            || code.PhysicalStart != code.VirtualStart
            || data.PhysicalStart != data.VirtualStart
            || code.ByteLength == 0
            || data.ByteLength == 0
            || ownedMap.BufferAddress == 0
            || ownedMap.BufferSize == 0
            || _ownedMapByteLength != ownedMap.BufferSize
            || _codePhysicalStart.TryGetRawValueForUefiBoundary(
                    out ulong codePhysicalStart)
                != AddressValueStatus.Success
            || _codeVirtualStart.TryGetRawValueForUefiBoundary(
                    out ulong codeVirtualStart)
                != AddressValueStatus.Success
            || _dataPhysicalStart.TryGetRawValueForUefiBoundary(
                    out ulong dataPhysicalStart)
                != AddressValueStatus.Success
            || _dataVirtualStart.TryGetRawValueForUefiBoundary(
                    out ulong dataVirtualStart)
                != AddressValueStatus.Success
            || _ownedMapPhysicalStart
                    .TryGetRawValueForUefiBoundary(
                        out ulong mapPhysicalStart)
                != AddressValueStatus.Success
            || _ownedMapVirtualStart
                    .TryGetRawValueForUefiBoundary(
                        out ulong mapVirtualStart)
                != AddressValueStatus.Success
            || codePhysicalStart != code.PhysicalStart
            || codeVirtualStart != code.VirtualStart
            || dataPhysicalStart != data.PhysicalStart
            || dataVirtualStart != data.VirtualStart
            || mapPhysicalStart != ownedMap.BufferAddress
            || mapVirtualStart != ownedMap.BufferAddress
            || codePhysicalStart != codeVirtualStart
            || dataPhysicalStart != dataVirtualStart
            || mapPhysicalStart != mapVirtualStart
            || _codePhysicalStart.TryGetEndExclusive(
                    code.ByteLength,
                    out PhysicalAddress codePhysicalEnd)
                != AddressValueStatus.Success
            || _codeVirtualStart.TryGetEndExclusive(
                    code.ByteLength,
                    out VirtualAddress codeVirtualEnd)
                != AddressValueStatus.Success
            || _dataPhysicalStart.TryGetEndExclusive(
                    data.ByteLength,
                    out PhysicalAddress dataPhysicalEnd)
                != AddressValueStatus.Success
            || _dataVirtualStart.TryGetEndExclusive(
                    data.ByteLength,
                    out VirtualAddress dataVirtualEnd)
                != AddressValueStatus.Success
            || _ownedMapPhysicalStart.TryGetEndExclusive(
                    _ownedMapByteLength,
                    out PhysicalAddress mapPhysicalEnd)
                != AddressValueStatus.Success
            || _ownedMapVirtualStart.TryGetEndExclusive(
                    _ownedMapByteLength,
                    out VirtualAddress mapVirtualEnd)
                != AddressValueStatus.Success
            || codePhysicalEnd.TryGetRawValueForUefiBoundary(
                    out ulong codePhysicalEndValue)
                != AddressValueStatus.Success
            || codeVirtualEnd.TryGetRawValueForUefiBoundary(
                    out ulong codeVirtualEndValue)
                != AddressValueStatus.Success
            || dataPhysicalEnd.TryGetRawValueForUefiBoundary(
                    out ulong dataPhysicalEndValue)
                != AddressValueStatus.Success
            || dataVirtualEnd.TryGetRawValueForUefiBoundary(
                    out ulong dataVirtualEndValue)
                != AddressValueStatus.Success
            || mapPhysicalEnd.TryGetRawValueForUefiBoundary(
                    out ulong mapPhysicalEndValue)
                != AddressValueStatus.Success
            || mapVirtualEnd.TryGetRawValueForUefiBoundary(
                    out ulong mapVirtualEndValue)
                != AddressValueStatus.Success
            || codePhysicalEndValue != code.PhysicalEndExclusive
            || codePhysicalEndValue != codeVirtualEndValue
            || dataPhysicalEndValue != data.PhysicalEndExclusive
            || dataPhysicalEndValue != dataVirtualEndValue
            || mapPhysicalEndValue != mapVirtualEndValue)
        {
            return false;
        }

        return true;
    }
}

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

    private M01StageOneAllocationEvidence(
        ulong allocationTableAddress,
        ulong allocationRecordCount,
        ulong allocationRecordCapacity,
        ulong codeAllocationOrdinal,
        ulong dataAllocationOrdinal,
        ulong codeFirmwareStatus,
        ulong dataFirmwareStatus)
    {
        _size = StructureSize;
        _version = CurrentVersion;
        _provenance = M01StageOneAllocationProvenance
            .CallerAttestedSuccessfulFirmwarePageAllocation;
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

    public static unsafe M01StageOneAllocationEvidence
        CaptureCallerAttested(
            BootMemoryRegionRecord* allocationRecords,
            ulong allocationRecordCount,
            ulong allocationRecordCapacity,
            ulong codeAllocationOrdinal,
            ulong dataAllocationOrdinal,
            ulong codeFirmwareStatus,
            ulong dataFirmwareStatus)
    {
        return new M01StageOneAllocationEvidence(
            (ulong)allocationRecords,
            allocationRecordCount,
            allocationRecordCapacity,
            codeAllocationOrdinal,
            dataAllocationOrdinal,
            codeFirmwareStatus,
            dataFirmwareStatus);
    }

    public bool IsWellFormed =>
        _size == StructureSize
        && _version == CurrentVersion
        && _provenance
            == M01StageOneAllocationProvenance
                .CallerAttestedSuccessfulFirmwarePageAllocation
        && _reserved0 == 0
        && _reserved1 == 0
        && _reserved2 == 0
        && _reserved3 == 0
        && _reserved4 == 0
        && _reserved5 == 0
        && _reserved6 == 0
        && _allocationTableAddress != 0
        && _allocationRecordCount
            == M01LoadedImageStackIntegration
                .RequiredAllocationRecordCount
        && _allocationRecordCapacity >= _allocationRecordCount
        && _codeAllocationOrdinal != 0
        && _codeAllocationOrdinal != ~0UL
        && _dataAllocationOrdinal == _codeAllocationOrdinal + 1
        && _codeFirmwareStatus == EfiSuccess
        && _dataFirmwareStatus == EfiSuccess;

    public unsafe bool IsValidFor(
        BootMemoryRegionRecord* allocationRecords,
        ulong allocationRecordCount,
        ulong allocationRecordCapacity)
    {
        return IsWellFormed
            && _allocationTableAddress == (ulong)allocationRecords
            && _allocationRecordCount == allocationRecordCount
            && _allocationRecordCapacity == allocationRecordCapacity
            && allocationRecords != null;
    }
}

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

    private M01StageOneMapLedgerEvidence(
        ulong previousMapKey,
        ulong previousMapGeneration,
        FirmwareMemoryMapRecord refreshedMap,
        ulong refreshedMapGeneration,
        ulong codeAllocationOrdinal,
        ulong dataAllocationOrdinal,
        ulong mapCaptureOrdinal,
        ulong ledgerBuildOrdinal,
        ulong ledgerAddress,
        ulong ledgerCount,
        ulong ledgerCapacity)
    {
        _size = StructureSize;
        _version = CurrentVersion;
        _provenance = M01StageOneMapLedgerProvenance
            .CallerAttestedPostAllocationOwnedMap;
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

    public static unsafe M01StageOneMapLedgerEvidence
        CaptureCallerAttested(
            ulong previousMapKey,
            ulong previousMapGeneration,
            FirmwareMemoryMapRecord refreshedMap,
            ulong refreshedMapGeneration,
            M01StageOneAllocationEvidence allocations,
            ulong mapCaptureOrdinal,
            ulong ledgerBuildOrdinal,
            OS.Kernel.Memory.BootReservedRange* ledger,
            ulong ledgerCount,
            ulong ledgerCapacity)
    {
        return new M01StageOneMapLedgerEvidence(
            previousMapKey,
            previousMapGeneration,
            refreshedMap,
            refreshedMapGeneration,
            allocations.CodeAllocationOrdinal,
            allocations.DataAllocationOrdinal,
            mapCaptureOrdinal,
            ledgerBuildOrdinal,
            (ulong)ledger,
            ledgerCount,
            ledgerCapacity);
    }

    public bool IsWellFormedFor(
        M01StageOneAllocationEvidence allocations)
    {
        return _size == StructureSize
            && _version == CurrentVersion
            && _provenance
                == M01StageOneMapLedgerProvenance
                    .CallerAttestedPostAllocationOwnedMap
            && _reserved0 == 0
            && _reserved1 == 0
            && _reserved2 == 0
            && _reserved3 == 0
            && _reserved4 == 0
            && _reserved5 == 0
            && _reserved6 == 0
            && _previousMapGeneration != 0
            && _previousMapGeneration != ~0UL
            && _refreshedMapGeneration
                == _previousMapGeneration + 1
            && _previousMapKey != _refreshedMap.MapKey
            && _refreshedMap.HasOwnedCopy()
            && _refreshedMap.Flags
                == FirmwareMemoryMapRecord.FlagOwnedCopy
            && _refreshedMap.BufferAddress != 0
            && _refreshedMap.BufferSize >= _refreshedMap.MapSize
            && _refreshedMap.SourceBufferAddress != 0
            && _refreshedMap.SourceBufferSize
                >= _refreshedMap.MapSize
            && _refreshedMap.MapSize != 0
            && _refreshedMap.DescriptorSize != 0
            && _refreshedMap.DescriptorVersion != 0
            && _refreshedMap.DescriptorCount != 0
            && _refreshedMap.DescriptorCount
                <= ~0UL / _refreshedMap.DescriptorSize
            && _refreshedMap.DescriptorCount
                    * _refreshedMap.DescriptorSize
                == _refreshedMap.MapSize
            && _refreshedMap.BufferAddress
                <= ~0UL - _refreshedMap.BufferSize
            && _refreshedMap.SourceBufferAddress
                <= ~0UL - _refreshedMap.SourceBufferSize
            && !RangesOverlap(
                _refreshedMap.BufferAddress,
                _refreshedMap.BufferAddress
                    + _refreshedMap.BufferSize,
                _refreshedMap.SourceBufferAddress,
                _refreshedMap.SourceBufferAddress
                    + _refreshedMap.SourceBufferSize)
            && allocations.IsWellFormed
            && _codeAllocationOrdinal
                == allocations.CodeAllocationOrdinal
            && _dataAllocationOrdinal
                == allocations.DataAllocationOrdinal
            && _mapCaptureOrdinal > _codeAllocationOrdinal
            && _mapCaptureOrdinal > _dataAllocationOrdinal
            && _ledgerBuildOrdinal > _mapCaptureOrdinal
            && _ledgerAddress != 0
            && _ledgerCount != 0
            && _ledgerCapacity >= _ledgerCount;
    }

    public unsafe bool IsValidFor(
        M01StageOneAllocationEvidence allocations,
        OS.Kernel.Memory.BootReservedRange* ledger,
        ulong ledgerCount,
        ulong ledgerCapacity)
    {
        if (!IsWellFormedFor(allocations)
            || _ledgerAddress != (ulong)ledger
            || _ledgerCount != ledgerCount
            || _ledgerCapacity != ledgerCapacity
            || ledger == null)
        {
            return false;
        }

        return true;
    }

    private static bool RangesOverlap(
        ulong firstStart,
        ulong firstEnd,
        ulong secondStart,
        ulong secondEnd)
    {
        return firstStart < secondEnd && secondStart < firstEnd;
    }
}

/// <summary>
/// Caller-owned mutable storage that a future EFI adapter will place in
/// durable loaded-image storage before abandoning the firmware stack.
/// </summary>
public struct M01LoadedImageStackStaticState
{
    public const uint CurrentVersion = 1;
    public const ulong RequiredStorageAlignment = 8;
    public const ulong HandoffStorageOffset = 16;
    public const ulong SwitchStateStorageOffset = 104;
    public const ulong StackLayoutStorageOffset = 640;
    public const ulong RequiredStorageBytes = 768;

    internal uint HeaderSize;
    internal uint HeaderVersion;
    internal M01LoadedImageStackStatus CoordinatorStatus;
    internal M01LoadedImageStackCheckpoint CoordinatorCheckpoint;
    internal byte Reserved0;
    internal byte Reserved1;
    internal byte Reserved2;
    internal byte Reserved3;
    internal byte Reserved4;
    internal byte Reserved5;
    internal OwnedBootStackSwitchHandoff HandoffStorage;
    internal OwnedBootStackSwitchStateRecord SwitchStateStorage;
    internal OwnedBootStackLayout StackLayoutStorage;

    public uint Size => HeaderSize;

    public uint Version => HeaderVersion;

    public M01LoadedImageStackStatus Status => CoordinatorStatus;

    public M01LoadedImageStackCheckpoint Checkpoint =>
        CoordinatorCheckpoint;

    public OwnedBootStackSwitchHandoff Handoff => HandoffStorage;

    public OwnedBootStackSwitchStateRecord SwitchState =>
        SwitchStateStorage;

    public OwnedBootStackLayout StackLayout => StackLayoutStorage;

    public bool IsInitialized =>
        HeaderSize == RequiredStorageBytes
        && HeaderVersion == CurrentVersion
        && CoordinatorStatus == M01LoadedImageStackStatus.Success
        && CoordinatorCheckpoint
            >= M01LoadedImageStackCheckpoint.SwitchArmed
        && CoordinatorCheckpoint
            <= M01LoadedImageStackCheckpoint
                .ContinuationRspContained
        && Reserved0 == 0
        && Reserved1 == 0
        && Reserved2 == 0
        && Reserved3 == 0
        && Reserved4 == 0
        && Reserved5 == 0
        && HandoffStorage.IsValid
        && SwitchStateStorage.IsValid
        && StackLayoutStorage.IsValid
        && (StackLayoutStorage.Lifetime
                == OwnedBootStackLifetime.Ready
            || StackLayoutStorage.Lifetime
                == OwnedBootStackLifetime.Active);
}

/// <summary>
/// A checked call descriptor for the later EFI adapter. This type never
/// converts the entry address to a function pointer and never invokes it.
/// </summary>
public readonly struct M01LoadedImageStackTransferTarget
{
    private readonly VirtualAddress _entry;
    private readonly VirtualAddress _newRspTop;
    private readonly VirtualAddress _handoffAddress;
    private readonly VirtualAddress _continuationAddress;

    internal M01LoadedImageStackTransferTarget(
        VirtualAddress entry,
        VirtualAddress newRspTop,
        VirtualAddress handoffAddress,
        VirtualAddress continuationAddress)
    {
        _entry = entry;
        _newRspTop = newRspTop;
        _handoffAddress = handoffAddress;
        _continuationAddress = continuationAddress;
    }

    public VirtualAddress Entry => _entry;

    public VirtualAddress NewRspTop => _newRspTop;

    public VirtualAddress HandoffAddress => _handoffAddress;

    public VirtualAddress ContinuationAddress => _continuationAddress;

    public bool IsValidFor(OwnedBootStackSwitchPlan plan)
    {
        return plan.IsValid
            && _entry.IsValid
            && _entry.Equals(plan.StubVirtualEntry)
            && _newRspTop.IsValid
            && _newRspTop.Equals(plan.PreTransferStackPointer)
            && _handoffAddress.IsValid
            && _handoffAddress.Equals(plan.Handoff.StorageStart)
            && _continuationAddress.IsValid
            && _continuationAddress.Equals(plan.Handoff.Continuation);
    }
}

/// <summary>
/// Carries the transition evidence needed by a future static continuation.
/// It is derived only from caller-owned static storage and contains no
/// allocation-table or reservation-ledger address.
/// </summary>
public readonly struct M01LoadedImageStackStaticTransitionResult
{
    private readonly M01LoadedImageStackStatus _status;
    private readonly M01LoadedImageStackCheckpoint _checkpoint;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly byte _reserved2;
    private readonly byte _reserved3;
    private readonly byte _reserved4;
    private readonly byte _reserved5;
    private readonly OwnedBootStackSwitchStateResult _stateResult;
    private readonly OwnedBootStackLayoutResult _layoutResult;
    private readonly M01LoadedImageStackTransferTarget _target;

    private M01LoadedImageStackStaticTransitionResult(
        M01LoadedImageStackStatus status,
        M01LoadedImageStackCheckpoint checkpoint,
        OwnedBootStackSwitchStateResult stateResult,
        OwnedBootStackLayoutResult layoutResult,
        M01LoadedImageStackTransferTarget target)
    {
        _status = status;
        _checkpoint = checkpoint;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _reserved3 = 0;
        _reserved4 = 0;
        _reserved5 = 0;
        _stateResult = stateResult;
        _layoutResult = layoutResult;
        _target = target;
    }

    public M01LoadedImageStackStatus Status => _status;

    public M01LoadedImageStackCheckpoint Checkpoint => _checkpoint;

    public OwnedBootStackSwitchStateResult StateResult =>
        _stateResult;

    public OwnedBootStackLayoutResult LayoutResult =>
        _layoutResult;

    public M01LoadedImageStackTransferTarget TransferTarget =>
        _target;

    public bool TransferRecorded =>
        Validate(
            M01LoadedImageStackCheckpoint.TransferRecorded,
            OwnedBootStackSwitchState.TransferRecorded,
            OwnedBootStackLifetime.Ready);

    public bool ContinuationRspContained =>
        Validate(
            M01LoadedImageStackCheckpoint.ContinuationRspContained,
            OwnedBootStackSwitchState.ContinuationObserved,
            OwnedBootStackLifetime.Active);

    internal static M01LoadedImageStackStaticTransitionResult
        Success(
            M01LoadedImageStackCheckpoint checkpoint,
            OwnedBootStackSwitchStateResult stateResult,
            OwnedBootStackLayoutResult layoutResult,
            M01LoadedImageStackTransferTarget target)
    {
        return new M01LoadedImageStackStaticTransitionResult(
            M01LoadedImageStackStatus.Success,
            checkpoint,
            stateResult,
            layoutResult,
            target);
    }

    internal static M01LoadedImageStackStaticTransitionResult Fail(
        M01LoadedImageStackStatus status,
        M01LoadedImageStackCheckpoint checkpoint,
        OwnedBootStackSwitchStateResult stateResult = default,
        OwnedBootStackLayoutResult layoutResult = default)
    {
        return new M01LoadedImageStackStaticTransitionResult(
            status,
            checkpoint,
            stateResult,
            layoutResult,
            default);
    }

    private bool Validate(
        M01LoadedImageStackCheckpoint checkpoint,
        OwnedBootStackSwitchState state,
        OwnedBootStackLifetime lifetime)
    {
        return _status == M01LoadedImageStackStatus.Success
            && _checkpoint == checkpoint
            && _reserved0 == 0
            && _reserved1 == 0
            && _reserved2 == 0
            && _reserved3 == 0
            && _reserved4 == 0
            && _reserved5 == 0
            && _stateResult.Succeeded
            && _stateResult.CurrentState == state
            && _layoutResult.Succeeded
            && _layoutResult.CurrentLifetime == lifetime
            && _target.IsValidFor(_stateResult.Plan);
    }
}

/// <summary>
/// Value-only evidence retained across the split prepare, transfer-record,
/// and continuation-audit host calls. Raw binding addresses are pre-transfer
/// diagnostic evidence only and this aggregate must not cross the stack
/// switch.
/// </summary>
public struct M01LoadedImageStackEvidence
{
    internal EfiLoadedImageRangeCapturePlan LoadedImageCaptureValue;
    internal M01LoadedImageVirtualMappingEvidence
        LoadedImageVirtualMappingValue;
    internal M01StageOneAddressMappingEvidence
        StageOneAddressMappingValue;
    internal M01StageOneAllocationEvidence AllocationEvidenceValue;
    internal M01StageOneMapLedgerEvidence MapLedgerEvidenceValue;
    internal BootMemoryRegionRecord CodeAllocationValue;
    internal BootMemoryRegionRecord DataAllocationValue;
    internal OwnedBootStackSwitchStatus AllocationValidationStatusValue;
    internal AddressValueStatus AllocationAddressStatusValue;
    internal ulong InvalidAllocationRecordIndexValue;
    internal OS.Kernel.Memory.BootReservedRange StubReservationValue;
    internal OS.Kernel.Memory.BootReservedRange GuardReservationValue;
    internal OS.Kernel.Memory.BootReservedRange StackReservationValue;
    internal OS.Kernel.Memory.BootReservationTableValidationResult
        ReservationValidationValue;
    internal OwnedBootStackLayoutResult StackLayoutResultValue;
    internal OwnedBootStackSwitchPlanResult SwitchPlanResultValue;
    internal OwnedBootStackSwitchBytePlanResult StubEmissionValue;
    internal OwnedBootStackSwitchBytePlanResult StubVerificationValue;
    internal OwnedBootStackSwitchStateResult SwitchStateResultValue;
    internal ulong StaticStateAddressValue;
    internal ulong StaticStateCapacityValue;
    internal VirtualAddress ObservedRspValue;
    internal M01StageOneStubByteMode StubByteModeValue;

    public EfiLoadedImageRangeCapturePlan LoadedImageCapture =>
        LoadedImageCaptureValue;

    public M01LoadedImageVirtualMappingEvidence
        LoadedImageVirtualMapping =>
            LoadedImageVirtualMappingValue;

    public M01StageOneAddressMappingEvidence
        StageOneAddressMapping =>
            StageOneAddressMappingValue;

    public M01StageOneAllocationEvidence AllocationEvidence =>
        AllocationEvidenceValue;

    public M01StageOneMapLedgerEvidence MapLedgerEvidence =>
        MapLedgerEvidenceValue;

    public BootMemoryRegionRecord CodeAllocation =>
        CodeAllocationValue;

    public BootMemoryRegionRecord DataAllocation =>
        DataAllocationValue;

    public OwnedBootStackSwitchStatus AllocationValidationStatus =>
        AllocationValidationStatusValue;

    public AddressValueStatus AllocationAddressStatus =>
        AllocationAddressStatusValue;

    public ulong InvalidAllocationRecordIndex =>
        InvalidAllocationRecordIndexValue;

    public OS.Kernel.Memory.BootReservedRange StubReservation =>
        StubReservationValue;

    public OS.Kernel.Memory.BootReservedRange GuardReservation =>
        GuardReservationValue;

    public OS.Kernel.Memory.BootReservedRange StackReservation =>
        StackReservationValue;

    public OS.Kernel.Memory.BootReservationTableValidationResult
        ReservationValidation => ReservationValidationValue;

    public OwnedBootStackLayoutResult StackLayoutResult =>
        StackLayoutResultValue;

    public OwnedBootStackSwitchPlanResult SwitchPlanResult =>
        SwitchPlanResultValue;

    public OwnedBootStackSwitchBytePlanResult StubEmission =>
        StubEmissionValue;

    public OwnedBootStackSwitchBytePlanResult StubVerification =>
        StubVerificationValue;

    public OwnedBootStackSwitchStateResult SwitchStateResult =>
        SwitchStateResultValue;

    public ulong StaticStateAddress => StaticStateAddressValue;

    public ulong StaticStateCapacity => StaticStateCapacityValue;

    public VirtualAddress ObservedRsp => ObservedRspValue;

    public M01StageOneStubByteMode StubByteMode => StubByteModeValue;
}

/// <summary>
/// Immutable coordinator result. Success means the requested host-modeled
/// checkpoint is internally consistent; only the final checkpoint satisfies
/// <see cref="Succeeded"/>.
/// </summary>
public readonly struct M01LoadedImageStackIntegrationResult
{
    private readonly M01LoadedImageStackStatus _status;
    private readonly M01LoadedImageStackCheckpoint _checkpoint;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly byte _reserved2;
    private readonly byte _reserved3;
    private readonly byte _reserved4;
    private readonly byte _reserved5;
    private readonly M01LoadedImageStackEvidence _evidence;
    private readonly M01LoadedImageStackTransferTarget _transferTarget;

    private M01LoadedImageStackIntegrationResult(
        M01LoadedImageStackStatus status,
        M01LoadedImageStackCheckpoint checkpoint,
        M01LoadedImageStackEvidence evidence,
        M01LoadedImageStackTransferTarget transferTarget)
    {
        _status = status;
        _checkpoint = checkpoint;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _reserved3 = 0;
        _reserved4 = 0;
        _reserved5 = 0;
        _evidence = evidence;
        _transferTarget = transferTarget;
    }

    public M01LoadedImageStackStatus Status => _status;

    public M01LoadedImageStackCheckpoint LastCompletedCheckpoint =>
        _checkpoint;

    public M01LoadedImageStackEvidence Evidence => _evidence;

    public M01LoadedImageStackTransferTarget TransferTarget =>
        _transferTarget;

    public bool IsPrepared =>
        ValidateProgress(M01LoadedImageStackCheckpoint.SwitchArmed);

    public bool IsTransferRecorded =>
        ValidateProgress(
            M01LoadedImageStackCheckpoint.TransferRecorded);

    public bool Succeeded =>
        ValidateProgress(
            M01LoadedImageStackCheckpoint.ContinuationRspContained);

    internal static M01LoadedImageStackIntegrationResult Success(
        M01LoadedImageStackCheckpoint checkpoint,
        M01LoadedImageStackEvidence evidence,
        M01LoadedImageStackTransferTarget transferTarget = default)
    {
        return new M01LoadedImageStackIntegrationResult(
            M01LoadedImageStackStatus.Success,
            checkpoint,
            evidence,
            transferTarget);
    }

    internal static M01LoadedImageStackIntegrationResult Fail(
        M01LoadedImageStackStatus status,
        M01LoadedImageStackCheckpoint checkpoint,
        M01LoadedImageStackEvidence evidence)
    {
        return new M01LoadedImageStackIntegrationResult(
            status,
            checkpoint,
            evidence,
            default);
    }

    private bool ValidateProgress(
        M01LoadedImageStackCheckpoint requiredCheckpoint)
    {
        OwnedBootStackSwitchPlan switchPlan =
            _evidence.SwitchPlanResultValue.Plan;
        if (_status != M01LoadedImageStackStatus.Success
            || _checkpoint != requiredCheckpoint
            || _reserved0 != 0
            || _reserved1 != 0
            || _reserved2 != 0
            || _reserved3 != 0
            || _reserved4 != 0
            || _reserved5 != 0
            || !_evidence.LoadedImageCaptureValue.Succeeded
            || !_evidence.LoadedImageVirtualMappingValue
                .IsValidFor(
                    _evidence.LoadedImageCaptureValue)
            || !_evidence.StageOneAddressMappingValue.IsValidFor(
                _evidence.CodeAllocationValue,
                _evidence.DataAllocationValue,
                _evidence.MapLedgerEvidenceValue.RefreshedMap)
            || _evidence.AllocationValidationStatusValue
                != OwnedBootStackSwitchStatus.Success
            || _evidence.AllocationAddressStatusValue
                != AddressValueStatus.Success
            || _evidence.InvalidAllocationRecordIndexValue
                != OwnedBootStackSwitchPlan.NoIndex
            || !_evidence.AllocationEvidenceValue.IsWellFormed
            || !_evidence.MapLedgerEvidenceValue.IsWellFormedFor(
                _evidence.AllocationEvidenceValue)
            || !_evidence.ReservationValidationValue.Succeeded
            || _evidence.ReservationValidationValue.ReservationCount
                != _evidence.MapLedgerEvidenceValue.LedgerCount
            || _evidence.ReservationValidationValue
                    .AvailableReservationCapacity
                != _evidence.MapLedgerEvidenceValue.LedgerCapacity
            || !_evidence.SwitchPlanResultValue.Succeeded
            || !_evidence.StubVerificationValue.Succeeded
            || _evidence.StubVerificationValue.Operation
                != OwnedBootStackSwitchByteOperation.Verify
            || !_evidence.SwitchStateResultValue.Succeeded
            || _evidence.StaticStateAddressValue == 0
            || _evidence.StaticStateCapacityValue
                < M01LoadedImageStackStaticState.RequiredStorageBytes
            || !RegionEquals(
                _evidence.CodeAllocationValue,
                switchPlan.CodeAllocation)
            || !RegionEquals(
                _evidence.DataAllocationValue,
                switchPlan.DataAllocation)
            || !_evidence.StubReservationValue.Equals(
                switchPlan.StubReservation)
            || !_evidence.GuardReservationValue.Equals(
                switchPlan.StackLayout.GuardReservation)
            || !_evidence.StackReservationValue.Equals(
                switchPlan.StackLayout.StackReservation)
            || (_evidence.StubByteModeValue
                    == M01StageOneStubByteMode.EmitAndVerify
                && !_evidence.StubEmissionValue.Plan.Equals(
                    switchPlan))
            || !_evidence.StubVerificationValue.Plan.Equals(
                switchPlan)
            || !_evidence.SwitchStateResultValue.Plan.Equals(
                switchPlan)
            || !LayoutTopologyEquals(
                _evidence.StackLayoutResultValue.Layout,
                switchPlan.StackLayout)
            || switchPlan.StateStorageStart
                    .TryGetRawValueForUefiBoundary(
                        out ulong plannedStateAddress)
                != AddressValueStatus.Success
            || plannedStateAddress
                != _evidence.StaticStateAddressValue
                    + M01LoadedImageStackStaticState
                        .SwitchStateStorageOffset)
        {
            return false;
        }

        if (switchPlan.Handoff.StorageStart
                    .TryGetRawValueForUefiBoundary(
                        out ulong plannedHandoffAddress)
                != AddressValueStatus.Success
            || plannedHandoffAddress
                != _evidence.StaticStateAddressValue
                    + M01LoadedImageStackStaticState
                        .HandoffStorageOffset)
        {
            return false;
        }

        if (_evidence.StubByteModeValue
                != M01StageOneStubByteMode.EmitAndVerify
            || !_evidence.StubEmissionValue.Succeeded
            || _evidence.StubEmissionValue.Operation
                != OwnedBootStackSwitchByteOperation.Emit)
        {
            return false;
        }

        if (requiredCheckpoint
            == M01LoadedImageStackCheckpoint.SwitchArmed)
        {
            return _evidence.StackLayoutResultValue.Succeeded
                && _evidence.StackLayoutResultValue.CurrentLifetime
                    == OwnedBootStackLifetime.Ready
                && _evidence.SwitchStateResultValue.CurrentState
                    == OwnedBootStackSwitchState.Armed
                && !_transferTarget.Entry.IsValid;
        }

        if (requiredCheckpoint
            == M01LoadedImageStackCheckpoint.TransferRecorded)
        {
            return _evidence.StackLayoutResultValue.Succeeded
                && _evidence.StackLayoutResultValue.CurrentLifetime
                    == OwnedBootStackLifetime.Ready
                && _evidence.SwitchStateResultValue.CurrentState
                    == OwnedBootStackSwitchState.TransferRecorded
                && _transferTarget.IsValidFor(
                    _evidence.SwitchPlanResultValue.Plan);
        }

        return _evidence.StackLayoutResultValue.Succeeded
            && _evidence.StackLayoutResultValue.CurrentLifetime
                == OwnedBootStackLifetime.Active
            && _evidence.SwitchStateResultValue.CurrentState
                == OwnedBootStackSwitchState.ContinuationObserved
            && _evidence.ObservedRspValue.IsValid
            && _evidence.ObservedRspValue.Equals(
                _evidence.SwitchPlanResultValue.Plan
                    .ExpectedContinuationStackPointer)
            && _transferTarget.IsValidFor(
                _evidence.SwitchPlanResultValue.Plan);
    }

    private static bool RegionEquals(
        BootMemoryRegionRecord left,
        BootMemoryRegionRecord right)
    {
        return left.UefiType == right.UefiType
            && left.PhysicalStart == right.PhysicalStart
            && left.VirtualStart == right.VirtualStart
            && left.PageCount == right.PageCount
            && left.Attribute == right.Attribute
            && left.ByteLength == right.ByteLength
            && left.PhysicalEndExclusive
                == right.PhysicalEndExclusive;
    }

    private static bool LayoutTopologyEquals(
        OwnedBootStackLayout left,
        OwnedBootStackLayout right)
    {
        return left.BackingStart.Equals(right.BackingStart)
            && left.BackingEndExclusive.Equals(
                right.BackingEndExclusive)
            && left.BackingPageCount == right.BackingPageCount
            && left.BackingByteLength == right.BackingByteLength
            && left.GuardReservation.Equals(
                right.GuardReservation)
            && left.StackReservation.Equals(
                right.StackReservation)
            && left.PhysicalStackTop.Equals(
                right.PhysicalStackTop);
    }
}

/// <summary>
/// Composes stage-one host evidence without allocating, invoking firmware,
/// changing page protections, or executing the stack-switch entry.
/// </summary>
public static unsafe class M01LoadedImageStackIntegration
{
    public const ulong RequiredAllocationRecordCount = 2;
    public const ulong CodeAllocationRecordIndex = 0;
    public const ulong DataAllocationRecordIndex = 1;

    public const bool CallsFirmwarePageAllocator = false;
    public const bool InvokesFirmware = false;
    public const bool ExecutesStackSwitch = false;
    public const bool IsGuestEvidence = false;
    public const bool MakesGuardInaccessible = false;
    public const bool EnforcesWriteXorExecute = false;
    public const bool HostAuditResultMayCrossStackSwitch = false;
    public const bool StaticTransitionRequiresHostAuditResult = false;

    private const ulong RequiredTableAlignment = 8;
    private const ulong MaximumUnsignedLong = ~0UL;
    // UEFI specification values kept local so the frozen M01 EFI source
    // closure does not acquire the host-side UefiMemoryType catalog.
    private const uint EfiLoaderCodeMemoryType = 1;
    private const uint EfiLoaderDataMemoryType = 2;

    public static M01LoadedImageStackIntegrationResult Prepare(
        EfiLoadedImageRangeCapturePlan loadedImage,
        M01LoadedImageVirtualMappingEvidence loadedImageVirtualMapping,
        M01StageOneAddressMappingEvidence stageOneAddressMapping,
        BootMemoryRegionRecord* allocationRecords,
        ulong allocationRecordCount,
        ulong allocationRecordCapacity,
        M01StageOneAllocationEvidence allocationEvidence,
        M01StageOneMapLedgerEvidence mapLedgerEvidence,
        OS.Kernel.Memory.BootReservedRange* reservationLedger,
        ulong reservationCount,
        ulong reservationCapacity,
        byte* codePage,
        ulong codePageCapacity,
        ulong materializedPageByteLength,
        ulong stubEntryOffset,
        ulong declaredStubByteLength,
        VirtualAddress continuationAddress,
        M01LoadedImageStackStaticState* staticState,
        ulong staticStateCapacity)
    {
        return PrepareWithStubByteMode(
            loadedImage,
            loadedImageVirtualMapping,
            stageOneAddressMapping,
            allocationRecords,
            allocationRecordCount,
            allocationRecordCapacity,
            allocationEvidence,
            mapLedgerEvidence,
            reservationLedger,
            reservationCount,
            reservationCapacity,
            codePage,
            codePageCapacity,
            materializedPageByteLength,
            stubEntryOffset,
            declaredStubByteLength,
            continuationAddress,
            staticState,
            staticStateCapacity,
            M01StageOneStubByteMode.EmitAndVerify);
    }

    public static M01LoadedImageStackIntegrationResult
        PrepareWithStubByteMode(
            EfiLoadedImageRangeCapturePlan loadedImage,
            M01LoadedImageVirtualMappingEvidence
                loadedImageVirtualMapping,
            M01StageOneAddressMappingEvidence
                stageOneAddressMapping,
            BootMemoryRegionRecord* allocationRecords,
            ulong allocationRecordCount,
            ulong allocationRecordCapacity,
            M01StageOneAllocationEvidence allocationEvidence,
            M01StageOneMapLedgerEvidence mapLedgerEvidence,
            OS.Kernel.Memory.BootReservedRange* reservationLedger,
            ulong reservationCount,
            ulong reservationCapacity,
            byte* codePage,
            ulong codePageCapacity,
            ulong materializedPageByteLength,
            ulong stubEntryOffset,
            ulong declaredStubByteLength,
            VirtualAddress continuationAddress,
            M01LoadedImageStackStaticState* staticState,
            ulong staticStateCapacity,
            M01StageOneStubByteMode stubByteMode)
    {
        M01LoadedImageStackEvidence evidence = default;
        evidence.LoadedImageCaptureValue = loadedImage;
        evidence.LoadedImageVirtualMappingValue =
            loadedImageVirtualMapping;
        evidence.StageOneAddressMappingValue =
            stageOneAddressMapping;
        evidence.StubByteModeValue = stubByteMode;
        evidence.ObservedRspValue = default;

        if (stubByteMode != M01StageOneStubByteMode.EmitAndVerify
            && stubByteMode
                != M01StageOneStubByteMode.VerifyExisting)
        {
            return M01LoadedImageStackIntegrationResult.Fail(
                M01LoadedImageStackStatus.StubByteModeRejected,
                M01LoadedImageStackCheckpoint.Uninitialized,
                evidence);
        }

        if (!loadedImage.Succeeded)
        {
            return M01LoadedImageStackIntegrationResult.Fail(
                M01LoadedImageStackStatus.LoadedImageRejected,
                M01LoadedImageStackCheckpoint.Uninitialized,
                evidence);
        }

        if (!loadedImageVirtualMapping.IsValidFor(loadedImage))
        {
            return M01LoadedImageStackIntegrationResult.Fail(
                M01LoadedImageStackStatus
                    .LoadedImageVirtualMappingRejected,
                M01LoadedImageStackCheckpoint.Uninitialized,
                evidence);
        }

        M01LoadedImageStackCheckpoint checkpoint =
            M01LoadedImageStackCheckpoint.LoadedImageValidated;

        OwnedBootStackSwitchStatus allocationStatus =
            ValidateAllocationTableMetadata(
                allocationRecords,
                allocationRecordCount,
                allocationRecordCapacity,
                out AddressValueStatus allocationAddressStatus,
                out ulong invalidAllocationIndex);
        if (allocationStatus == OwnedBootStackSwitchStatus.Success)
        {
            allocationStatus = ValidateAllocationRecords(
                allocationRecords,
                out allocationAddressStatus,
                out invalidAllocationIndex);
        }

        evidence.AllocationValidationStatusValue =
            allocationStatus;
        evidence.AllocationAddressStatusValue =
            allocationAddressStatus;
        evidence.InvalidAllocationRecordIndexValue =
            invalidAllocationIndex;
        if (allocationStatus != OwnedBootStackSwitchStatus.Success)
        {
            return M01LoadedImageStackIntegrationResult.Fail(
                M01LoadedImageStackStatus.AllocationTableRejected,
                checkpoint,
                evidence);
        }

        evidence.CodeAllocationValue =
            allocationRecords[CodeAllocationRecordIndex];
        evidence.DataAllocationValue =
            allocationRecords[DataAllocationRecordIndex];
        evidence.AllocationEvidenceValue = allocationEvidence;

        if (!allocationEvidence.IsValidFor(
                allocationRecords,
                allocationRecordCount,
                allocationRecordCapacity))
        {
            return M01LoadedImageStackIntegrationResult.Fail(
                M01LoadedImageStackStatus.AllocationEvidenceRejected,
                checkpoint,
                evidence);
        }

        checkpoint =
            M01LoadedImageStackCheckpoint.AllocationEvidenceValidated;
        evidence.MapLedgerEvidenceValue = mapLedgerEvidence;

        if (!ValidateLedgerMetadata(
                reservationLedger,
                reservationCount,
                reservationCapacity)
            || !mapLedgerEvidence.IsValidFor(
                allocationEvidence,
                reservationLedger,
                reservationCount,
                reservationCapacity))
        {
            return M01LoadedImageStackIntegrationResult.Fail(
                M01LoadedImageStackStatus.MapLedgerEvidenceRejected,
                checkpoint,
                evidence);
        }

        if (!stageOneAddressMapping.IsValidFor(
                evidence.CodeAllocationValue,
                evidence.DataAllocationValue,
                mapLedgerEvidence.RefreshedMap))
        {
            return M01LoadedImageStackIntegrationResult.Fail(
                M01LoadedImageStackStatus
                    .StageOneAddressMappingRejected,
                checkpoint,
                evidence);
        }

        OS.Kernel.Memory.BootReservationTableValidationResult
            reservationValidation =
                OS.Kernel.Memory.BootReservationTableValidator.Validate(
                    reservationLedger,
                    reservationCount,
                    reservationCapacity);
        evidence.ReservationValidationValue = reservationValidation;
        if (!reservationValidation.Succeeded)
        {
            return M01LoadedImageStackIntegrationResult.Fail(
                M01LoadedImageStackStatus.ReservationLedgerRejected,
                checkpoint,
                evidence);
        }

        if (!TryBindReservationIdentities(
                loadedImage,
                allocationRecords,
                reservationLedger,
                reservationCount,
                out OS.Kernel.Memory.BootReservedRange
                    stubReservation,
                out OS.Kernel.Memory.BootReservedRange
                    guardReservation,
                out OS.Kernel.Memory.BootReservedRange
                    stackReservation))
        {
            return M01LoadedImageStackIntegrationResult.Fail(
                M01LoadedImageStackStatus.ReservationIdentityRejected,
                checkpoint,
                evidence);
        }

        if (!ValidateOwnedMapBufferReservation(
                loadedImage.Range,
                stageOneAddressMapping,
                mapLedgerEvidence.RefreshedMap,
                allocationRecords,
                reservationLedger,
                reservationCount))
        {
            return M01LoadedImageStackIntegrationResult.Fail(
                M01LoadedImageStackStatus.ReservationIdentityRejected,
                checkpoint,
                evidence);
        }

        evidence.StubReservationValue = stubReservation;
        evidence.GuardReservationValue = guardReservation;
        evidence.StackReservationValue = stackReservation;
        checkpoint = M01LoadedImageStackCheckpoint.MapLedgerValidated;

        OS.Kernel.Memory.BootReservedRange* layoutReservations =
            reservationLedger
                + reservationValidation.BootStackGuardIndex;
        ulong dataPageCount =
            allocationRecords[DataAllocationRecordIndex].PageCount;
        PhysicalAddress.TryCreateFromUefiBoundary(
            allocationRecords[DataAllocationRecordIndex].PhysicalStart,
            out PhysicalAddress dataPhysicalStart);
        PhysicalAddress.TryCreateFromUefiBoundary(
            allocationRecords[DataAllocationRecordIndex]
                .PhysicalEndExclusive,
            out PhysicalAddress physicalStackTop);

        OwnedBootStackLayoutResult layoutResult =
            OwnedBootStackLayout.Create(
                layoutReservations,
                OwnedBootStackLayout.RequiredReservationCount,
                OwnedBootStackLayout.RequiredReservationCount,
                dataPhysicalStart,
                dataPageCount,
                physicalStackTop);
        evidence.StackLayoutResultValue = layoutResult;
        if (!layoutResult.Succeeded)
        {
            return M01LoadedImageStackIntegrationResult.Fail(
                M01LoadedImageStackStatus.StackLayoutRejected,
                checkpoint,
                evidence);
        }

        OwnedBootStackLayoutResult readyLayout =
            layoutResult.Layout.TransitionTo(
                OwnedBootStackLifetime.Ready);
        evidence.StackLayoutResultValue = readyLayout;
        if (!readyLayout.Succeeded)
        {
            return M01LoadedImageStackIntegrationResult.Fail(
                M01LoadedImageStackStatus.StackLifetimeRejected,
                checkpoint,
                evidence);
        }

        checkpoint = M01LoadedImageStackCheckpoint.StackLayoutReady;

        if (!ValidateStaticAndCodeStorage(
                loadedImageVirtualMapping,
                stageOneAddressMapping,
                allocationRecords,
                mapLedgerEvidence.RefreshedMap,
                codePage,
                codePageCapacity,
                materializedPageByteLength,
                continuationAddress,
                staticState,
                staticStateCapacity,
                reservationLedger,
                reservationCount))
        {
            return M01LoadedImageStackIntegrationResult.Fail(
                M01LoadedImageStackStatus.StaticStateRejected,
                checkpoint,
                evidence);
        }

        evidence.StaticStateAddressValue = (ulong)staticState;
        evidence.StaticStateCapacityValue = staticStateCapacity;

        VirtualAddress.TryCreateFromUefiBoundary(
            (ulong)&staticState->HandoffStorage,
            out VirtualAddress handoffAddress);
        VirtualAddress.TryCreateFromUefiBoundary(
            (ulong)&staticState->SwitchStateStorage,
            out VirtualAddress stateAddress);

        OwnedBootStackSwitchPlanResult switchPlanResult =
            OwnedBootStackSwitchPlan.Create(
                allocationRecords,
                allocationRecordCount,
                allocationRecordCapacity,
                readyLayout.Layout,
                stubEntryOffset,
                declaredStubByteLength,
                handoffAddress,
                OwnedBootStackSwitchHandoff.StructureSize,
                OwnedBootStackSwitchHandoffProvenance
                    .CallerAttestedStaticOwnedData,
                stateAddress,
                OwnedBootStackSwitchStateRecord.StructureSize,
                OwnedBootStackSwitchStateStorageProvenance
                    .CallerAttestedStaticOwnedData,
                continuationAddress,
                OwnedBootStackSwitchContinuationContract
                    .StaticNonReturningRspAndHandoff);
        evidence.SwitchPlanResultValue = switchPlanResult;
        if (!switchPlanResult.Succeeded)
        {
            return M01LoadedImageStackIntegrationResult.Fail(
                M01LoadedImageStackStatus.SwitchPlanRejected,
                checkpoint,
                evidence);
        }

        checkpoint = M01LoadedImageStackCheckpoint.SwitchPlanReady;

        if (stubByteMode
            == M01StageOneStubByteMode.EmitAndVerify)
        {
            OwnedBootStackSwitchBytePlanResult emission =
                switchPlanResult.Plan.EmitAuditedStubPage(
                    codePage,
                    codePageCapacity,
                    materializedPageByteLength);
            evidence.StubEmissionValue = emission;
            if (!emission.Succeeded)
            {
                return M01LoadedImageStackIntegrationResult.Fail(
                    M01LoadedImageStackStatus.StubEmissionRejected,
                    checkpoint,
                    evidence);
            }
        }

        OwnedBootStackSwitchBytePlanResult verification =
            switchPlanResult.Plan.VerifyAuditedStubPage(
                codePage,
                codePageCapacity,
                materializedPageByteLength,
                declaredStubByteLength);
        evidence.StubVerificationValue = verification;
        if (!verification.Succeeded)
        {
            return M01LoadedImageStackIntegrationResult.Fail(
                M01LoadedImageStackStatus.StubVerificationRejected,
                checkpoint,
                evidence);
        }

        if (stubByteMode
            == M01StageOneStubByteMode.VerifyExisting)
        {
            return M01LoadedImageStackIntegrationResult.Fail(
                M01LoadedImageStackStatus.StubByteModeRejected,
                checkpoint,
                evidence);
        }

        checkpoint = M01LoadedImageStackCheckpoint.StubVerified;

        staticState->HeaderSize =
            (uint)M01LoadedImageStackStaticState.RequiredStorageBytes;
        staticState->HeaderVersion =
            M01LoadedImageStackStaticState.CurrentVersion;
        staticState->CoordinatorStatus =
            M01LoadedImageStackStatus.Success;
        staticState->CoordinatorCheckpoint = checkpoint;
        staticState->Reserved0 = 0;
        staticState->Reserved1 = 0;
        staticState->Reserved2 = 0;
        staticState->Reserved3 = 0;
        staticState->Reserved4 = 0;
        staticState->Reserved5 = 0;
        staticState->HandoffStorage =
            switchPlanResult.Plan.Handoff;
        staticState->StackLayoutStorage = readyLayout.Layout;

        if (!HandoffsEqual(
                staticState->HandoffStorage,
                switchPlanResult.Plan.Handoff))
        {
            staticState->CoordinatorStatus =
                M01LoadedImageStackStatus
                    .HandoffMaterializationRejected;
            return M01LoadedImageStackIntegrationResult.Fail(
                M01LoadedImageStackStatus
                    .HandoffMaterializationRejected,
                checkpoint,
                evidence);
        }

        OwnedBootStackSwitchStateResult initialized =
            switchPlanResult.Plan.InitializeOneShotState(
                &staticState->SwitchStateStorage,
                OwnedBootStackSwitchStateRecord.StructureSize);
        evidence.SwitchStateResultValue = initialized;
        if (!initialized.Succeeded)
        {
            staticState->CoordinatorStatus =
                M01LoadedImageStackStatus.StateInitializationRejected;
            return M01LoadedImageStackIntegrationResult.Fail(
                M01LoadedImageStackStatus.StateInitializationRejected,
                checkpoint,
                evidence);
        }

        OwnedBootStackSwitchStateResult armed =
            switchPlanResult.Plan.Arm(
                &staticState->SwitchStateStorage,
                OwnedBootStackSwitchStateRecord.StructureSize,
                verification);
        evidence.SwitchStateResultValue = armed;
        if (!armed.Succeeded)
        {
            staticState->CoordinatorStatus =
                M01LoadedImageStackStatus.ArmRejected;
            return M01LoadedImageStackIntegrationResult.Fail(
                M01LoadedImageStackStatus.ArmRejected,
                checkpoint,
                evidence);
        }

        checkpoint = M01LoadedImageStackCheckpoint.SwitchArmed;
        staticState->CoordinatorCheckpoint = checkpoint;
        return M01LoadedImageStackIntegrationResult.Success(
            checkpoint,
            evidence);
    }

    public static M01LoadedImageStackStaticTransitionResult
        RecordTransferFromStaticState(
            M01LoadedImageStackStaticState* staticState,
            ulong staticStateCapacity)
    {
        if (!ValidateStaticTransitionState(
                staticState,
                staticStateCapacity,
                M01LoadedImageStackCheckpoint.SwitchArmed))
        {
            return M01LoadedImageStackStaticTransitionResult.Fail(
                M01LoadedImageStackStatus.IllegalCoordinatorState,
                M01LoadedImageStackCheckpoint.Uninitialized);
        }

        OwnedBootStackSwitchPlan plan =
            staticState->SwitchStateStorage.Plan;
        OwnedBootStackSwitchStateResult transfer =
            plan.RecordTransfer(
                &staticState->SwitchStateStorage,
                OwnedBootStackSwitchStateRecord.StructureSize);
        if (!transfer.Succeeded)
        {
            staticState->CoordinatorStatus =
                M01LoadedImageStackStatus.TransferRejected;
            return M01LoadedImageStackStaticTransitionResult.Fail(
                M01LoadedImageStackStatus.TransferRejected,
                M01LoadedImageStackCheckpoint.SwitchArmed,
                transfer);
        }

        M01LoadedImageStackTransferTarget target =
            CreateTransferTarget(plan);
        if (!target.IsValidFor(plan))
        {
            staticState->CoordinatorStatus =
                M01LoadedImageStackStatus.TransferRejected;
            return M01LoadedImageStackStaticTransitionResult.Fail(
                M01LoadedImageStackStatus.TransferRejected,
                M01LoadedImageStackCheckpoint.SwitchArmed,
                transfer);
        }

        staticState->CoordinatorCheckpoint =
            M01LoadedImageStackCheckpoint.TransferRecorded;
        OwnedBootStackLayoutResult readyLayout =
            CreateReadyLayoutEvidence(
            staticState->StackLayoutStorage);
        return M01LoadedImageStackStaticTransitionResult.Success(
            M01LoadedImageStackCheckpoint.TransferRecorded,
            transfer,
            readyLayout,
            target);
    }

    public static M01LoadedImageStackStaticTransitionResult
        ObserveContinuationRspFromStaticState(
            M01LoadedImageStackStaticState* staticState,
            ulong staticStateCapacity,
            VirtualAddress observedRsp)
    {
        if (!ValidateStaticTransitionState(
                staticState,
                staticStateCapacity,
                M01LoadedImageStackCheckpoint.TransferRecorded))
        {
            return M01LoadedImageStackStaticTransitionResult.Fail(
                M01LoadedImageStackStatus.IllegalCoordinatorState,
                M01LoadedImageStackCheckpoint.Uninitialized);
        }

        OwnedBootStackSwitchPlan plan =
            staticState->SwitchStateStorage.Plan;
        OwnedBootStackSwitchStateResult observation =
            plan.AuditContinuationRsp(
                &staticState->SwitchStateStorage,
                OwnedBootStackSwitchStateRecord.StructureSize,
                observedRsp);
        if (!observation.Succeeded)
        {
            staticState->CoordinatorStatus =
                M01LoadedImageStackStatus.ContinuationRspRejected;
            return M01LoadedImageStackStaticTransitionResult.Fail(
                M01LoadedImageStackStatus.ContinuationRspRejected,
                M01LoadedImageStackCheckpoint.TransferRecorded,
                observation);
        }

        OwnedBootStackLayoutResult activeLayout =
            staticState->StackLayoutStorage.TransitionTo(
                OwnedBootStackLifetime.Active);
        if (!activeLayout.Succeeded)
        {
            staticState->CoordinatorStatus =
                M01LoadedImageStackStatus.StackLifetimeRejected;
            return M01LoadedImageStackStaticTransitionResult.Fail(
                M01LoadedImageStackStatus.StackLifetimeRejected,
                M01LoadedImageStackCheckpoint.TransferRecorded,
                observation,
                activeLayout);
        }

        staticState->StackLayoutStorage = activeLayout.Layout;
        staticState->CoordinatorCheckpoint =
            M01LoadedImageStackCheckpoint.ContinuationRspContained;
        M01LoadedImageStackTransferTarget target =
            CreateTransferTarget(plan);
        return M01LoadedImageStackStaticTransitionResult.Success(
            M01LoadedImageStackCheckpoint.ContinuationRspContained,
            observation,
            activeLayout,
            target);
    }

    public static M01LoadedImageStackStaticTransitionResult
        ReportUnexpectedReturnFromStaticState(
            M01LoadedImageStackStaticState* staticState,
            ulong staticStateCapacity)
    {
        if (!ValidateStaticTransitionState(
                staticState,
                staticStateCapacity,
                M01LoadedImageStackCheckpoint.TransferRecorded))
        {
            return M01LoadedImageStackStaticTransitionResult.Fail(
                M01LoadedImageStackStatus.IllegalCoordinatorState,
                M01LoadedImageStackCheckpoint.Uninitialized);
        }

        OwnedBootStackSwitchStateResult returned =
            staticState->SwitchStateStorage.Plan
                .ReportUnexpectedReturn(
                    &staticState->SwitchStateStorage,
                    OwnedBootStackSwitchStateRecord.StructureSize);
        staticState->CoordinatorStatus =
            M01LoadedImageStackStatus.UnexpectedReturn;
        return M01LoadedImageStackStaticTransitionResult.Fail(
            M01LoadedImageStackStatus.UnexpectedReturn,
            M01LoadedImageStackCheckpoint.TransferRecorded,
            returned);
    }

#if !NOSTDLIB_EFI
    // Host convenience only. A live continuation must use the static-state
    // APIs above and must not carry this audit aggregate across the switch.
    public static M01LoadedImageStackIntegrationResult RecordTransfer(
        M01LoadedImageStackIntegrationResult prepared,
        M01LoadedImageStackStaticState* staticState,
        ulong staticStateCapacity)
    {
        M01LoadedImageStackEvidence evidence = prepared.Evidence;
        if (!prepared.IsPrepared
            || !StaticStateMatches(
                prepared,
                staticState,
                staticStateCapacity,
                M01LoadedImageStackCheckpoint.SwitchArmed))
        {
            return M01LoadedImageStackIntegrationResult.Fail(
                M01LoadedImageStackStatus.IllegalCoordinatorState,
                prepared.LastCompletedCheckpoint,
                evidence);
        }

        M01LoadedImageStackStaticTransitionResult transition =
            RecordTransferFromStaticState(
                staticState,
                staticStateCapacity);
        evidence.SwitchStateResultValue =
            transition.StateResult;
        evidence.StackLayoutResultValue =
            transition.LayoutResult;
        if (!transition.TransferRecorded)
        {
            return M01LoadedImageStackIntegrationResult.Fail(
                transition.Status,
                M01LoadedImageStackCheckpoint.SwitchArmed,
                evidence);
        }

        return M01LoadedImageStackIntegrationResult.Success(
            M01LoadedImageStackCheckpoint.TransferRecorded,
            evidence,
            transition.TransferTarget);
    }

    public static M01LoadedImageStackIntegrationResult
        ObserveContinuationRsp(
            M01LoadedImageStackIntegrationResult transferred,
            M01LoadedImageStackStaticState* staticState,
            ulong staticStateCapacity,
            VirtualAddress observedRsp)
    {
        M01LoadedImageStackEvidence evidence = transferred.Evidence;
        if (!transferred.IsTransferRecorded
            || !StaticStateMatches(
                transferred,
                staticState,
                staticStateCapacity,
                M01LoadedImageStackCheckpoint.TransferRecorded))
        {
            return M01LoadedImageStackIntegrationResult.Fail(
                M01LoadedImageStackStatus.IllegalCoordinatorState,
                transferred.LastCompletedCheckpoint,
                evidence);
        }

        M01LoadedImageStackStaticTransitionResult transition =
            ObserveContinuationRspFromStaticState(
                staticState,
                staticStateCapacity,
                observedRsp);
        evidence.SwitchStateResultValue =
            transition.StateResult;
        evidence.StackLayoutResultValue =
            transition.LayoutResult;
        evidence.ObservedRspValue = observedRsp;
        if (!transition.ContinuationRspContained)
        {
            return M01LoadedImageStackIntegrationResult.Fail(
                transition.Status,
                M01LoadedImageStackCheckpoint.TransferRecorded,
                evidence);
        }
        return M01LoadedImageStackIntegrationResult.Success(
            M01LoadedImageStackCheckpoint.ContinuationRspContained,
            evidence,
            transition.TransferTarget);
    }

    public static M01LoadedImageStackIntegrationResult
        ReportUnexpectedReturn(
            M01LoadedImageStackIntegrationResult transferred,
            M01LoadedImageStackStaticState* staticState,
            ulong staticStateCapacity)
    {
        M01LoadedImageStackEvidence evidence = transferred.Evidence;
        if (!transferred.IsTransferRecorded
            || !StaticStateMatches(
                transferred,
                staticState,
                staticStateCapacity,
                M01LoadedImageStackCheckpoint.TransferRecorded))
        {
            return M01LoadedImageStackIntegrationResult.Fail(
                M01LoadedImageStackStatus.IllegalCoordinatorState,
                transferred.LastCompletedCheckpoint,
                evidence);
        }

        M01LoadedImageStackStaticTransitionResult transition =
            ReportUnexpectedReturnFromStaticState(
                staticState,
                staticStateCapacity);
        evidence.SwitchStateResultValue =
            transition.StateResult;
        return M01LoadedImageStackIntegrationResult.Fail(
            transition.Status,
            M01LoadedImageStackCheckpoint.TransferRecorded,
            evidence);
    }
#endif

    private static M01LoadedImageStackTransferTarget
        CreateTransferTarget(OwnedBootStackSwitchPlan plan)
    {
        return new M01LoadedImageStackTransferTarget(
            plan.StubVirtualEntry,
            plan.PreTransferStackPointer,
            plan.Handoff.StorageStart,
            plan.Handoff.Continuation);
    }

    private static OwnedBootStackLayoutResult
        CreateReadyLayoutEvidence(OwnedBootStackLayout layout)
    {
        if (!layout.IsValid
            || layout.Lifetime != OwnedBootStackLifetime.Ready)
        {
            return default;
        }

        return OwnedBootStackLayoutResult.Create(
            OwnedBootStackLayoutStatus.Success,
            AddressValueStatus.Success,
            OS.Kernel.Memory.BootReservationTableStatus.Uninitialized,
            OS.Kernel.Memory.BootReservedRangeStatus.Uninitialized,
            OwnedBootStackLifetime.Reserved,
            OwnedBootStackLifetime.Ready,
            OwnedBootStackLifetime.Ready,
            0,
            0,
            0,
            OS.Kernel.Memory.BootReservationTableValidationResult
                .NoIndex,
            OS.Kernel.Memory.BootReservationTableValidationResult
                .NoIndex,
            layout);
    }

    private static bool ValidateStaticTransitionState(
        M01LoadedImageStackStaticState* staticState,
        ulong staticStateCapacity,
        M01LoadedImageStackCheckpoint requiredCheckpoint)
    {
        if (staticState == null)
        {
            return false;
        }

        ulong stateAddress = (ulong)staticState;
        if ((stateAddress
                & (M01LoadedImageStackStaticState
                    .RequiredStorageAlignment - 1)) != 0
            || staticStateCapacity
                < M01LoadedImageStackStaticState.RequiredStorageBytes
            || stateAddress
                > MaximumUnsignedLong
                    - M01LoadedImageStackStaticState
                        .RequiredStorageBytes
            || !staticState->IsInitialized
            || staticState->CoordinatorStatus
                != M01LoadedImageStackStatus.Success
            || staticState->CoordinatorCheckpoint
                != requiredCheckpoint
            || (ulong)&staticState->HandoffStorage - stateAddress
                != M01LoadedImageStackStaticState
                    .HandoffStorageOffset
            || (ulong)&staticState->SwitchStateStorage - stateAddress
                != M01LoadedImageStackStaticState
                    .SwitchStateStorageOffset
            || (ulong)&staticState->StackLayoutStorage - stateAddress
                != M01LoadedImageStackStaticState
                    .StackLayoutStorageOffset)
        {
            return false;
        }

        OwnedBootStackSwitchPlan plan =
            staticState->SwitchStateStorage.Plan;
        if (!plan.IsValid
            || !HandoffsEqual(
                staticState->HandoffStorage,
                plan.Handoff)
            || !StaticLayoutTopologyEquals(
                staticState->StackLayoutStorage,
                plan.StackLayout)
            || staticState->StackLayoutStorage.Lifetime
                != OwnedBootStackLifetime.Ready)
        {
            return false;
        }

        if (requiredCheckpoint
            == M01LoadedImageStackCheckpoint.SwitchArmed)
        {
            return staticState->SwitchStateStorage.State
                == OwnedBootStackSwitchState.Armed;
        }

        return requiredCheckpoint
                == M01LoadedImageStackCheckpoint.TransferRecorded
            && staticState->SwitchStateStorage.State
                == OwnedBootStackSwitchState.TransferRecorded;
    }

    private static OwnedBootStackSwitchStatus
        ValidateAllocationTableMetadata(
        BootMemoryRegionRecord* records,
        ulong count,
        ulong capacity,
        out AddressValueStatus addressStatus,
        out ulong invalidRecordIndex)
    {
        addressStatus = AddressValueStatus.Uninitialized;
        invalidRecordIndex = OwnedBootStackSwitchPlan.NoIndex;
        if (count < RequiredAllocationRecordCount)
        {
            return OwnedBootStackSwitchStatus
                .InvalidAllocationRecordCount;
        }

        if (count > RequiredAllocationRecordCount)
        {
            return OwnedBootStackSwitchStatus
                .TooManyAllocationRecords;
        }

        if (capacity < count)
        {
            return OwnedBootStackSwitchStatus
                .AllocationRecordCapacityTooSmall;
        }

        if (records == null)
        {
            return OwnedBootStackSwitchStatus
                .MissingAllocationRecords;
        }

        ulong address = (ulong)records;
        ulong byteLength =
            RequiredAllocationRecordCount
            * (ulong)sizeof(BootMemoryRegionRecord);
        if ((address & (RequiredTableAlignment - 1)) != 0)
        {
            addressStatus = AddressValueStatus.MisalignedAddress;
            return OwnedBootStackSwitchStatus
                .MisalignedAllocationRecords;
        }

        if (address > MaximumUnsignedLong - byteLength)
        {
            addressStatus = AddressValueStatus.AddressOverflow;
            return OwnedBootStackSwitchStatus
                .AllocationRecordAddressRangeOverflow;
        }

        addressStatus = AddressValueStatus.Success;
        return OwnedBootStackSwitchStatus.Success;
    }

    private static OwnedBootStackSwitchStatus
        ValidateAllocationRecords(
            BootMemoryRegionRecord* records,
            out AddressValueStatus addressStatus,
            out ulong invalidRecordIndex)
    {
        BootMemoryRegionRecord code =
            records[CodeAllocationRecordIndex];
        BootMemoryRegionRecord data =
            records[DataAllocationRecordIndex];

        OwnedBootStackSwitchStatus status =
            ValidateAllocationRecord(
                code,
                isCode: true,
                out addressStatus);
        if (status != OwnedBootStackSwitchStatus.Success)
        {
            invalidRecordIndex = CodeAllocationRecordIndex;
            return status;
        }

        status = ValidateAllocationRecord(
                data,
                isCode: false,
                out addressStatus);
        if (status != OwnedBootStackSwitchStatus.Success)
        {
            invalidRecordIndex = DataAllocationRecordIndex;
            return status;
        }

        if (data.PageCount < 2)
        {
            addressStatus = AddressValueStatus.ZeroLength;
            invalidRecordIndex = DataAllocationRecordIndex;
            return OwnedBootStackSwitchStatus.InvalidDataPageCount;
        }

        ulong codeVirtualEnd =
            code.VirtualStart + code.ByteLength;
        ulong dataVirtualEnd =
            data.VirtualStart + data.ByteLength;
        if (RangesOverlap(
                code.PhysicalStart,
                code.PhysicalEndExclusive,
                data.PhysicalStart,
                data.PhysicalEndExclusive)
        )
        {
            addressStatus = AddressValueStatus.InvalidAddress;
            invalidRecordIndex = DataAllocationRecordIndex;
            return OwnedBootStackSwitchStatus.PhysicalAllocationOverlap;
        }

        if (RangesOverlap(
                code.VirtualStart,
                codeVirtualEnd,
                data.VirtualStart,
                dataVirtualEnd))
        {
            addressStatus = AddressValueStatus.InvalidAddress;
            invalidRecordIndex = DataAllocationRecordIndex;
            return OwnedBootStackSwitchStatus.VirtualAllocationOverlap;
        }

        addressStatus = AddressValueStatus.Success;
        invalidRecordIndex = OwnedBootStackSwitchPlan.NoIndex;
        return OwnedBootStackSwitchStatus.Success;
    }

    private static OwnedBootStackSwitchStatus
        ValidateAllocationRecord(
        BootMemoryRegionRecord record,
        bool isCode,
        out AddressValueStatus addressStatus)
    {
        uint expectedType = isCode
            ? EfiLoaderCodeMemoryType
            : EfiLoaderDataMemoryType;
        if (record.UefiType != expectedType)
        {
            addressStatus = AddressValueStatus.Uninitialized;
            return isCode
                ? OwnedBootStackSwitchStatus.InvalidCodeMemoryType
                : OwnedBootStackSwitchStatus.InvalidDataMemoryType;
        }

        if (record.PhysicalStart == 0)
        {
            addressStatus = AddressValueStatus.InvalidAddress;
            return isCode
                ? OwnedBootStackSwitchStatus.InvalidCodePhysicalStart
                : OwnedBootStackSwitchStatus.InvalidDataPhysicalStart;
        }

        if ((record.PhysicalStart
            & (BootMemoryRegionRecord.PageSize - 1)) != 0)
        {
            addressStatus = AddressValueStatus.MisalignedAddress;
            return isCode
                ? OwnedBootStackSwitchStatus
                    .MisalignedCodePhysicalStart
                : OwnedBootStackSwitchStatus
                    .MisalignedDataPhysicalStart;
        }

        if (record.VirtualStart == 0)
        {
            addressStatus = AddressValueStatus.InvalidAddress;
            return isCode
                ? OwnedBootStackSwitchStatus.InvalidCodeVirtualStart
                : OwnedBootStackSwitchStatus.InvalidDataVirtualStart;
        }

        if ((record.VirtualStart
            & (BootMemoryRegionRecord.PageSize - 1)) != 0)
        {
            addressStatus = AddressValueStatus.MisalignedAddress;
            return isCode
                ? OwnedBootStackSwitchStatus
                    .MisalignedCodeVirtualStart
                : OwnedBootStackSwitchStatus
                    .MisalignedDataVirtualStart;
        }

        if (record.PageCount == 0
            || (isCode && record.PageCount != 1))
        {
            addressStatus = AddressValueStatus.ZeroLength;
            return isCode
                ? OwnedBootStackSwitchStatus.InvalidCodePageCount
                : OwnedBootStackSwitchStatus.InvalidDataPageCount;
        }

        if (record.PageCount
            > MaximumUnsignedLong
                / BootMemoryRegionRecord.PageSize)
        {
            addressStatus = AddressValueStatus.AddressOverflow;
            return isCode
                ? OwnedBootStackSwitchStatus.CodeByteCountOverflow
                : OwnedBootStackSwitchStatus.DataByteCountOverflow;
        }

        ulong expectedBytes =
            record.PageCount * BootMemoryRegionRecord.PageSize;
        if (record.ByteLength != expectedBytes)
        {
            addressStatus = AddressValueStatus.InvalidAddress;
            return isCode
                ? OwnedBootStackSwitchStatus.CodeByteLengthMismatch
                : OwnedBootStackSwitchStatus.DataByteLengthMismatch;
        }

        if (record.PhysicalStart
            > MaximumUnsignedLong - expectedBytes)
        {
            addressStatus = AddressValueStatus.AddressOverflow;
            return isCode
                ? OwnedBootStackSwitchStatus.CodePhysicalEndOverflow
                : OwnedBootStackSwitchStatus.DataPhysicalEndOverflow;
        }

        if (record.PhysicalEndExclusive
            != record.PhysicalStart + expectedBytes)
        {
            addressStatus = AddressValueStatus.InvalidAddress;
            return isCode
                ? OwnedBootStackSwitchStatus.CodePhysicalEndMismatch
                : OwnedBootStackSwitchStatus.DataPhysicalEndMismatch;
        }

        if (record.VirtualStart
            > MaximumUnsignedLong - expectedBytes)
        {
            addressStatus = AddressValueStatus.AddressOverflow;
            return isCode
                ? OwnedBootStackSwitchStatus.CodeVirtualEndOverflow
                : OwnedBootStackSwitchStatus.DataVirtualEndOverflow;
        }

        addressStatus = AddressValueStatus.Success;
        return OwnedBootStackSwitchStatus.Success;
    }

    private static bool ValidateLedgerMetadata(
        OS.Kernel.Memory.BootReservedRange* ledger,
        ulong count,
        ulong capacity)
    {
        if (count == 0
            || count
                > OS.Kernel.Memory.BootReservationTableValidator
                    .MaximumReservationCount
            || capacity < count
            || ledger == null)
        {
            return false;
        }

        ulong address = (ulong)ledger;
        ulong recordSize =
            (ulong)sizeof(OS.Kernel.Memory.BootReservedRange);
        return (address & (RequiredTableAlignment - 1)) == 0
            && count <= MaximumUnsignedLong / recordSize
            && address
                <= MaximumUnsignedLong - (count * recordSize);
    }

    private static bool TryBindReservationIdentities(
        EfiLoadedImageRangeCapturePlan loadedImage,
        BootMemoryRegionRecord* allocations,
        OS.Kernel.Memory.BootReservedRange* ledger,
        ulong count,
        out OS.Kernel.Memory.BootReservedRange stub,
        out OS.Kernel.Memory.BootReservedRange guard,
        out OS.Kernel.Memory.BootReservedRange stack)
    {
        stub = default;
        guard = default;
        stack = default;

        PhysicalAddress.TryCreateFromUefiBoundary(
            allocations[CodeAllocationRecordIndex].PhysicalStart,
            out PhysicalAddress codePhysicalStart);
        if (OS.Kernel.Memory.BootReservedRange.TryCreate(
                OS.Kernel.Memory.BootReservationKind
                    .BootStackSwitchStub,
                codePhysicalStart,
                allocations[CodeAllocationRecordIndex].PageCount,
                out OS.Kernel.Memory.BootReservedRange expectedStub)
            != OS.Kernel.Memory.BootReservedRangeStatus.Success)
        {
            return false;
        }

        ulong loadedCount = 0;
        ulong stubCount = 0;
        ulong guardCount = 0;
        ulong stackCount = 0;

        for (ulong index = 0; index < count; index++)
        {
            OS.Kernel.Memory.BootReservedRange current =
                ledger[index];
            if (current.Kind
                == OS.Kernel.Memory.BootReservationKind.LoadedImage)
            {
                loadedCount++;
                if (!current.Equals(
                        loadedImage.Range.Reservation))
                {
                    return false;
                }
            }
            else if (current.Kind
                == OS.Kernel.Memory.BootReservationKind
                    .BootStackSwitchStub)
            {
                stubCount++;
                stub = current;
                if (!current.Equals(expectedStub))
                {
                    return false;
                }
            }
            else if (current.Kind
                == OS.Kernel.Memory.BootReservationKind.BootStackGuard)
            {
                guardCount++;
                guard = current;
            }
            else if (current.Kind
                == OS.Kernel.Memory.BootReservationKind.BootStack)
            {
                stackCount++;
                stack = current;
            }
        }

        if (loadedCount != 1
            || stubCount != 1
            || guardCount != 1
            || stackCount != 1)
        {
            return false;
        }

        BootMemoryRegionRecord data =
            allocations[DataAllocationRecordIndex];
        return guard.PhysicalStart.TryGetRawValueForUefiBoundary(
                    out ulong guardStart)
                == AddressValueStatus.Success
            && stack.PhysicalEndExclusive
                    .TryGetRawValueForUefiBoundary(
                        out ulong stackEnd)
                == AddressValueStatus.Success
            && guardStart == data.PhysicalStart
            && stackEnd == data.PhysicalEndExclusive
            && guard.PageCount == 1
            && guard.ByteLength
                == OS.Kernel.Memory.BootReservedRange.PageSize
            && guard.PageCount
                <= MaximumUnsignedLong - stack.PageCount
            && guard.PageCount + stack.PageCount == data.PageCount
            && guard.ByteLength
                <= MaximumUnsignedLong - stack.ByteLength
            && guard.ByteLength + stack.ByteLength
                == data.ByteLength;
    }

    private static bool ValidateOwnedMapBufferReservation(
        EfiLoadedImageRange loadedImage,
        M01StageOneAddressMappingEvidence addressMapping,
        FirmwareMemoryMapRecord map,
        BootMemoryRegionRecord* allocations,
        OS.Kernel.Memory.BootReservedRange* ledger,
        ulong ledgerCount)
    {
        if (!addressMapping.IsValidFor(
                allocations[CodeAllocationRecordIndex],
                allocations[DataAllocationRecordIndex],
                map)
            || addressMapping.OwnedMapPhysicalStart
                    .TryGetRawValueForUefiBoundary(
                        out ulong mapStart)
                != AddressValueStatus.Success
            || addressMapping.OwnedMapPhysicalStart
                    .TryGetEndExclusive(
                        addressMapping.OwnedMapByteLength,
                        out PhysicalAddress mapEndAddress)
                != AddressValueStatus.Success
            || mapEndAddress.TryGetRawValueForUefiBoundary(
                    out ulong mapEnd)
                != AddressValueStatus.Success)
        {
            return false;
        }

        if (RangesOverlap(
                mapStart,
                mapEnd,
                allocations[CodeAllocationRecordIndex].PhysicalStart,
                allocations[CodeAllocationRecordIndex]
                    .PhysicalEndExclusive)
            || RangesOverlap(
                mapStart,
                mapEnd,
                allocations[DataAllocationRecordIndex].PhysicalStart,
                allocations[DataAllocationRecordIndex]
                    .PhysicalEndExclusive)
            || loadedImage.ImageBase
                    .TryGetRawValueForUefiBoundary(
                        out ulong imageStart)
                != AddressValueStatus.Success
            || loadedImage.ImageEndExclusive
                    .TryGetRawValueForUefiBoundary(
                        out ulong imageEnd)
                != AddressValueStatus.Success)
        {
            return false;
        }

        if (mapStart >= imageStart && mapEnd <= imageEnd)
        {
            return true;
        }

        ulong ownedMapReservationCount = 0;
        bool covered = false;
        for (ulong index = 0; index < ledgerCount; index++)
        {
            OS.Kernel.Memory.BootReservedRange current =
                ledger[index];
            if (current.Kind
                != OS.Kernel.Memory.BootReservationKind
                    .OwnedMemoryMap)
            {
                continue;
            }

            ownedMapReservationCount++;
            if (current.PhysicalStart
                    .TryGetRawValueForUefiBoundary(
                        out ulong reservationStart)
                    == AddressValueStatus.Success
                && current.PhysicalEndExclusive
                    .TryGetRawValueForUefiBoundary(
                        out ulong reservationEnd)
                    == AddressValueStatus.Success
                && mapStart >= reservationStart
                && mapEnd <= reservationEnd)
            {
                covered = true;
            }
        }

        return ownedMapReservationCount == 1 && covered;
    }

    private static bool ValidateStaticAndCodeStorage(
        M01LoadedImageVirtualMappingEvidence loadedImageMapping,
        M01StageOneAddressMappingEvidence addressMapping,
        BootMemoryRegionRecord* allocations,
        FirmwareMemoryMapRecord ownedMap,
        byte* codePage,
        ulong codePageCapacity,
        ulong materializedPageByteLength,
        VirtualAddress continuationAddress,
        M01LoadedImageStackStaticState* staticState,
        ulong staticStateCapacity,
        OS.Kernel.Memory.BootReservedRange* ledger,
        ulong ledgerCount)
    {
        BootMemoryRegionRecord code =
            allocations[CodeAllocationRecordIndex];
        BootMemoryRegionRecord data =
            allocations[DataAllocationRecordIndex];
        ulong codeAddress = (ulong)codePage;
        ulong codeLength = code.ByteLength;
        ulong dataAddress = data.VirtualStart;
        ulong dataLength = data.ByteLength;
        ulong stateAddress = (ulong)staticState;
        ulong stateLength =
            M01LoadedImageStackStaticState.RequiredStorageBytes;
        ulong allocationTableAddress = (ulong)allocations;
        ulong allocationTableLength =
            RequiredAllocationRecordCount
            * (ulong)sizeof(BootMemoryRegionRecord);
        ulong ledgerAddress = (ulong)ledger;
        ulong ledgerLength =
            ledgerCount
            * (ulong)sizeof(OS.Kernel.Memory.BootReservedRange);

        if (!addressMapping.IsValidFor(code, data, ownedMap)
            || addressMapping.OwnedMapVirtualStart
                    .TryGetRawValueForUefiBoundary(
                        out ulong mapStart)
                != AddressValueStatus.Success
            || loadedImageMapping.VirtualImageBase
                    .TryGetRawValueForUefiBoundary(
                        out ulong imageBase)
                != AddressValueStatus.Success
            || loadedImageMapping.VirtualImageEndExclusive
                    .TryGetRawValueForUefiBoundary(
                        out ulong imageEnd)
                != AddressValueStatus.Success
            || continuationAddress.TryGetRawValueForUefiBoundary(
                    out ulong continuation)
                != AddressValueStatus.Success
            || codePage == null
            || codeAddress != code.VirtualStart
            || codePageCapacity < codeLength
            || materializedPageByteLength != codeLength
            || codeAddress > MaximumUnsignedLong - codeLength
            || dataAddress > MaximumUnsignedLong - dataLength
            || mapStart
                > MaximumUnsignedLong
                    - addressMapping.OwnedMapByteLength
            || allocationTableAddress
                > MaximumUnsignedLong - allocationTableLength
            || ledgerAddress > MaximumUnsignedLong - ledgerLength
            || staticState == null
            || (stateAddress
                & (M01LoadedImageStackStaticState
                    .RequiredStorageAlignment - 1)) != 0
            || staticStateCapacity < stateLength
            || stateAddress > MaximumUnsignedLong - stateLength
            || continuation == MaximumUnsignedLong)
        {
            return false;
        }

        ulong codeEnd = codeAddress + codeLength;
        ulong dataEnd = dataAddress + dataLength;
        ulong mapEnd =
            mapStart + addressMapping.OwnedMapByteLength;
        ulong stateEnd = stateAddress + stateLength;
        ulong allocationTableEnd =
            allocationTableAddress + allocationTableLength;
        ulong ledgerEnd = ledgerAddress + ledgerLength;

        if (RangesOverlap(codeAddress, codeEnd, dataAddress, dataEnd)
            || RangesOverlap(
                codeAddress,
                codeEnd,
                imageBase,
                imageEnd)
            || RangesOverlap(
                codeAddress,
                codeEnd,
                mapStart,
                mapEnd)
            || RangesOverlap(
                codeAddress,
                codeEnd,
                stateAddress,
                stateEnd)
            || RangesOverlap(
                codeAddress,
                codeEnd,
                allocationTableAddress,
                allocationTableEnd)
            || RangesOverlap(
                codeAddress,
                codeEnd,
                ledgerAddress,
                ledgerEnd)
            || RangesOverlap(
                dataAddress,
                dataEnd,
                imageBase,
                imageEnd)
            || RangesOverlap(
                dataAddress,
                dataEnd,
                mapStart,
                mapEnd)
            || RangesOverlap(
                dataAddress,
                dataEnd,
                stateAddress,
                stateEnd)
            || RangesOverlap(
                dataAddress,
                dataEnd,
                allocationTableAddress,
                allocationTableEnd)
            || RangesOverlap(
                dataAddress,
                dataEnd,
                ledgerAddress,
                ledgerEnd)
            || RangesOverlap(
                mapStart,
                mapEnd,
                stateAddress,
                stateEnd)
            || RangesOverlap(
                mapStart,
                mapEnd,
                allocationTableAddress,
                allocationTableEnd)
            || RangesOverlap(
                mapStart,
                mapEnd,
                ledgerAddress,
                ledgerEnd)
            || RangesOverlap(
                allocationTableAddress,
                allocationTableEnd,
                ledgerAddress,
                ledgerEnd)
            || RangesOverlap(
                stateAddress,
                stateEnd,
                allocationTableAddress,
                allocationTableEnd)
            || RangesOverlap(
                stateAddress,
                stateEnd,
                ledgerAddress,
                ledgerEnd)
            || (continuation >= codeAddress
                && continuation < codeEnd)
            || (continuation >= dataAddress
                && continuation < dataEnd)
            || (continuation >= mapStart
                && continuation < mapEnd)
            || (continuation >= stateAddress
                && continuation < stateEnd)
            || (continuation >= allocationTableAddress
                && continuation < allocationTableEnd)
            || (continuation >= ledgerAddress
                && continuation < ledgerEnd)
            || !IsZeroed((byte*)staticState, stateLength))
        {
            return false;
        }

        ulong handoffStart = (ulong)&staticState->HandoffStorage;
        ulong handoffEnd =
            handoffStart
            + OwnedBootStackSwitchHandoff.StructureSize;
        ulong switchStateStart =
            (ulong)&staticState->SwitchStateStorage;
        ulong switchStateEnd =
            switchStateStart
            + OwnedBootStackSwitchStateRecord.StructureSize;
        return handoffStart >= imageBase
            && stateAddress >= imageBase
            && stateEnd <= imageEnd
            && handoffEnd <= imageEnd
            && switchStateStart >= imageBase
            && switchStateEnd <= imageEnd
            && continuation >= imageBase
            && continuation < imageEnd;
    }

#if !NOSTDLIB_EFI
    private static bool StaticStateMatches(
        M01LoadedImageStackIntegrationResult result,
        M01LoadedImageStackStaticState* staticState,
        ulong staticStateCapacity,
        M01LoadedImageStackCheckpoint checkpoint)
    {
        if (staticState == null
            || (ulong)staticState
                != result.Evidence.StaticStateAddress
            || staticStateCapacity
                != result.Evidence.StaticStateCapacity
            || staticStateCapacity
                < M01LoadedImageStackStaticState.RequiredStorageBytes
            || !staticState->IsInitialized
            || staticState->CoordinatorCheckpoint != checkpoint
            || !HandoffsEqual(
                staticState->HandoffStorage,
                result.Evidence.SwitchPlanResult.Plan.Handoff)
            || !staticState->SwitchStateStorage.Plan.Equals(
                result.Evidence.SwitchPlanResult.Plan))
        {
            return false;
        }

        return true;
    }
#endif

    private static bool HandoffsEqual(
        OwnedBootStackSwitchHandoff left,
        OwnedBootStackSwitchHandoff right)
    {
        return left.Size == right.Size
            && left.Version == right.Version
            && left.StorageStart.Equals(right.StorageStart)
            && left.StorageEndExclusive.Equals(
                right.StorageEndExclusive)
            && left.StorageByteLength == right.StorageByteLength
            && left.StubEntry.Equals(right.StubEntry)
            && left.UsableStackStart.Equals(
                right.UsableStackStart)
            && left.UsableStackEndExclusive.Equals(
                right.UsableStackEndExclusive)
            && left.PreTransferStackPointer.Equals(
                right.PreTransferStackPointer)
            && left.ExpectedContinuationStackPointer.Equals(
                right.ExpectedContinuationStackPointer)
            && left.Continuation.Equals(right.Continuation)
            && left.Provenance == right.Provenance
            && left.ContinuationContract
                == right.ContinuationContract;
    }

    private static bool StaticLayoutTopologyEquals(
        OwnedBootStackLayout left,
        OwnedBootStackLayout right)
    {
        return left.BackingStart.Equals(right.BackingStart)
            && left.BackingEndExclusive.Equals(
                right.BackingEndExclusive)
            && left.BackingPageCount == right.BackingPageCount
            && left.BackingByteLength == right.BackingByteLength
            && left.GuardReservation.Equals(
                right.GuardReservation)
            && left.StackReservation.Equals(
                right.StackReservation)
            && left.PhysicalStackTop.Equals(
                right.PhysicalStackTop);
    }

    private static bool IsZeroed(byte* storage, ulong length)
    {
        for (ulong index = 0; index < length; index++)
        {
            if (storage[index] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool RangesOverlap(
        ulong firstStart,
        ulong firstEnd,
        ulong secondStart,
        ulong secondEnd)
    {
        return firstStart < secondEnd && secondStart < firstEnd;
    }
}
