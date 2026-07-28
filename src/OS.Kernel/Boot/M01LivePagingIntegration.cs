namespace OS.Boot;

// Reference boundary:
// - references/SharpOS-main/OS/src/Kernel/Paging/X64PageTable.cs,
//   local snapshot SHA-256
//   35503B543628E5A462073D498704050EEEF7D79DEB2B84F01CACA29714200BAC,
//   influenced only the four-level hierarchy walk and table ownership model.
// - references/SharpOS-main/OS/src/Kernel/Paging/Cr3Accessor.cs,
//   local snapshot SHA-256
//   088D73C1BF807B652CD2F7ED795C1507C1F28884DD4184A2A28A574CF01E9E47,
//   was used only to cross-check preservation of the complete observed CR3
//   value, including architecturally permitted low bits.
// No reference implementation is copied. This file composes first-party
// CLROS plans over caller-owned POD and never executes privileged bytes.

/// <summary>
/// Reports the first rejected boundary in the third M01 host coordinator.
/// Existing values are append-only and the zero-valued default is invalid.
/// </summary>
public enum M01LivePagingStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    StageTwoRejected = 2,
    AllocationTableRejected = 3,
    AllocationEvidenceRejected = 4,
    MapLedgerEvidenceRejected = 5,
    ReservationLedgerRejected = 6,
    ReservationIdentityRejected = 7,
    CoordinatorStateRejected = 8,
    DuplicatePhase = 9,
    IdentityMappingRejected = 10,
    TableStorageRejected = 11,
    TableConstructionRejected = 12,
    PreflightStubRejected = 13,
    PreflightRejected = 14,
    TransitionStubRejected = 15,
    TransitionEvidenceRejected = 16,
    Cr3PlanRejected = 17,
    Cr3TranscriptRejected = 18,
    InvlpgStubRejected = 19,
    ProtectionProbeStubRejected = 20,
    ProtectionMutationRejected = 21,
    ProtectionProbeRejected = 22,
    PageZeroRejected = 23,
    IllegalCoordinatorState = 24,
    ByteModeRejected = 25,
    CleanupPlanRejected = 26
}

/// <summary>
/// Identifies the last completed host-audit checkpoint. These values do not
/// claim that firmware allocation or any privileged instruction executed.
/// </summary>
public enum M01LivePagingCheckpoint : byte
{
    Uninitialized = 0,
    StageTwoValidated = 1,
    AllocationsValidated = 2,
    MapLedgerValidated = 3,
    ReservationsValidated = 4,
    IdentityMappingPlanned = 5,
    StubBytesVerified = 6,
    TableImageBuilt = 7,
    PagingModeValidated = 8,
    Cr3PlanValidated = 9,
    Cr3TranscriptValidated = 10,
    ProtectionPlanValidated = 11,
    PageZeroValidated = 12,
    Completed = 13,
    PreparedForCompositeTransition = 14
}

/// <summary>
/// Separates the M01-029 preparation contract from the retained M01-019
/// synthetic execution model. Neither mode is processor execution evidence.
/// </summary>
public enum M01LivePagingExecutionMode : byte
{
    Uninitialized = 0,
    PreparationOnly = 1,
    ModelOnlyLegacy = 2
}

/// <summary>
/// Controls whether the coordinator writes the audited bytes or only checks
/// bytes already supplied by its caller. VerifyExisting is needed for exact
/// malformed-readback tests without test-only callbacks.
/// </summary>
public enum M01LivePagingByteMode : byte
{
    Uninitialized = 0,
    EmitAndVerify = 1,
    VerifyExisting = 2
}

public enum M01LivePagingAllocationProvenance : byte
{
    Uninitialized = 0,
    CallerAttestedSuccessfulFirmwarePageAllocations = 1
}

public enum M01LivePagingMapLedgerProvenance : byte
{
    Uninitialized = 0,
    CallerAttestedPostAllocationOwnedMap = 1
}

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

    private M01LivePagingAllocationEvidence(
        ulong allocationTableAddress,
        ulong allocationRecordCount,
        ulong allocationRecordCapacity,
        ulong cr3CodeAllocationOrdinal,
        ulong probeAllocationOrdinal,
        ulong tableStateAllocationOrdinal,
        ulong cr3CodeStatus,
        ulong probeStatus,
        ulong tableStateStatus)
    {
        _size = StructureSize;
        _version = CurrentVersion;
        _provenance = M01LivePagingAllocationProvenance
            .CallerAttestedSuccessfulFirmwarePageAllocations;
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

    public M01LivePagingAllocationProvenance Provenance =>
        _provenance;

    public ulong AllocationTableAddress => _allocationTableAddress;

    public ulong AllocationRecordCount => _allocationRecordCount;

    public ulong AllocationRecordCapacity =>
        _allocationRecordCapacity;

    public ulong Cr3CodeAllocationOrdinal =>
        _cr3CodeAllocationOrdinal;

    public ulong ProbeAllocationOrdinal => _probeAllocationOrdinal;

    public ulong TableStateAllocationOrdinal =>
        _tableStateAllocationOrdinal;

    public ulong Cr3CodeStatus => _cr3CodeStatus;

    public ulong ProbeStatus => _probeStatus;

    public ulong TableStateStatus => _tableStateStatus;

    public ulong LastAllocationOrdinal =>
        _tableStateAllocationOrdinal;

    public static unsafe M01LivePagingAllocationEvidence
        CaptureCallerAttested(
            BootMemoryRegionRecord* allocationRecords,
            ulong allocationRecordCount,
            ulong allocationRecordCapacity,
            ulong cr3CodeAllocationOrdinal,
            ulong probeAllocationOrdinal,
            ulong tableStateAllocationOrdinal,
            ulong cr3CodeStatus,
            ulong probeStatus,
            ulong tableStateStatus)
    {
        return new M01LivePagingAllocationEvidence(
            (ulong)allocationRecords,
            allocationRecordCount,
            allocationRecordCapacity,
            cr3CodeAllocationOrdinal,
            probeAllocationOrdinal,
            tableStateAllocationOrdinal,
            cr3CodeStatus,
            probeStatus,
            tableStateStatus);
    }

    public unsafe bool IsValidFor(
        BootMemoryRegionRecord* allocationRecords,
        ulong allocationRecordCount,
        ulong allocationRecordCapacity)
    {
        return _size == StructureSize
            && _version == CurrentVersion
            && _provenance
                == M01LivePagingAllocationProvenance
                    .CallerAttestedSuccessfulFirmwarePageAllocations
            && _reserved0 == 0
            && _reserved1 == 0
            && _reserved2 == 0
            && _allocationTableAddress == (ulong)allocationRecords
            && _allocationRecordCount
                == M01LivePagingIntegration
                    .RequiredAllocationRecordCount
            && _allocationRecordCount == allocationRecordCount
            && _allocationRecordCapacity == allocationRecordCapacity
            && _allocationRecordCapacity >= _allocationRecordCount
            && _allocationRecordCapacity
                <= M01LivePagingIntegration
                    .MaximumAllocationRecordCapacity
            && _cr3CodeAllocationOrdinal != 0
            && _cr3CodeAllocationOrdinal != ~0UL
            && _probeAllocationOrdinal != 0
            && _probeAllocationOrdinal != ~0UL
            && _tableStateAllocationOrdinal != 0
            && _tableStateAllocationOrdinal != ~0UL
            && _probeAllocationOrdinal
                == _cr3CodeAllocationOrdinal + 1
            && _tableStateAllocationOrdinal
                == _probeAllocationOrdinal + 1
            && _cr3CodeStatus == EfiSuccess
            && _probeStatus == EfiSuccess
            && _tableStateStatus == EfiSuccess;
    }
}

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

    private M01LivePagingMapLedgerEvidence(
        ulong previousMapKey,
        ulong previousMapGeneration,
        FirmwareMemoryMapRecord refreshedMap,
        ulong refreshedMapGeneration,
        ulong lastAllocationOrdinal,
        ulong mapCaptureOrdinal,
        ulong ledgerBuildOrdinal,
        ulong ledgerAddress,
        ulong ledgerCount,
        ulong ledgerCapacity)
    {
        _size = StructureSize;
        _version = CurrentVersion;
        _provenance = M01LivePagingMapLedgerProvenance
            .CallerAttestedPostAllocationOwnedMap;
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

    public M01LivePagingMapLedgerProvenance Provenance =>
        _provenance;

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

    public static unsafe M01LivePagingMapLedgerEvidence
        CaptureCallerAttested(
            ulong previousMapKey,
            ulong previousMapGeneration,
            FirmwareMemoryMapRecord refreshedMap,
            ulong refreshedMapGeneration,
            M01LivePagingAllocationEvidence allocations,
            ulong mapCaptureOrdinal,
            ulong ledgerBuildOrdinal,
            OS.Kernel.Memory.BootReservedRange* ledger,
            ulong ledgerCount,
            ulong ledgerCapacity)
    {
        return new M01LivePagingMapLedgerEvidence(
            previousMapKey,
            previousMapGeneration,
            refreshedMap,
            refreshedMapGeneration,
            allocations.LastAllocationOrdinal,
            mapCaptureOrdinal,
            ledgerBuildOrdinal,
            (ulong)ledger,
            ledgerCount,
            ledgerCapacity);
    }

    public unsafe bool IsValidFor(
        M01LiveAllocatorStaticState stageTwo,
        M01LivePagingAllocationEvidence allocations,
        OS.Kernel.Memory.BootReservedRange* ledger,
        ulong ledgerCount,
        ulong ledgerCapacity)
    {
        return _size == StructureSize
            && _version == CurrentVersion
            && _provenance
                == M01LivePagingMapLedgerProvenance
                    .CallerAttestedPostAllocationOwnedMap
            && _reserved0 == 0
            && _reserved1 == 0
            && _reserved2 == 0
            && stageTwo.IsCompleted
            && _previousMapKey == stageTwo.RefreshedMapKey
            && _previousMapGeneration
                == stageTwo.RefreshedMapGeneration
            && _previousMapGeneration != 0
            && _previousMapGeneration != ~0UL
            && _refreshedMapGeneration
                == _previousMapGeneration + 1
            && _previousMapKey != _refreshedMap.MapKey
            && IsValidOwnedMap(_refreshedMap)
            && _lastAllocationOrdinal
                == allocations.LastAllocationOrdinal
            && _mapCaptureOrdinal > _lastAllocationOrdinal
            && _ledgerBuildOrdinal > _mapCaptureOrdinal
            && _ledgerAddress == (ulong)ledger
            && _ledgerCount == ledgerCount
            && _ledgerCapacity == ledgerCapacity
            && _ledgerCount != 0
            && _ledgerCount <= _ledgerCapacity
            && _ledgerCapacity
                <= OS.Kernel.Memory.BootReservationTableValidator
                    .MaximumReservationCount;
    }

    private static bool IsValidOwnedMap(
        FirmwareMemoryMapRecord map)
    {
        if (!map.HasOwnedCopy()
            || map.Flags != FirmwareMemoryMapRecord.FlagOwnedCopy
            || map.BufferAddress == 0
            || map.BufferSize == 0
            || map.SourceBufferAddress == 0
            || map.SourceBufferSize == 0
            || map.MapSize == 0
            || map.MapSize > map.BufferSize
            || map.MapSize > map.SourceBufferSize
            || map.MapKey == 0
            || map.DescriptorSize < 40
            || (map.DescriptorSize & 7) != 0
            || map.DescriptorVersion == 0
            || map.DescriptorCount == 0
            || map.DescriptorCount > 4096
            || map.DescriptorCount
                > ~0UL / map.DescriptorSize
            || map.MapSize
                != map.DescriptorCount * map.DescriptorSize
            || map.BufferAddress > ~0UL - map.BufferSize
            || map.SourceBufferAddress
                > ~0UL - map.SourceBufferSize)
        {
            return false;
        }

        ulong bufferEnd = map.BufferAddress + map.BufferSize;
        ulong sourceEnd =
            map.SourceBufferAddress + map.SourceBufferSize;
        return bufferEnd <= map.SourceBufferAddress
            || sourceEnd <= map.BufferAddress;
    }
}

public enum M01LivePagingCleanupStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidCompletedCount = 2,
    InvalidAllocationStorage = 3,
    InvalidAllocationRecord = 4,
    FirmwareServicesUnavailable = 5,
    OwnedRootNotRestored = 6
}

/// <summary>
/// Deterministic reverse-order release plan for pre-entry or safely restored
/// failures. This type does not call FreePages.
/// </summary>
public readonly struct M01LivePagingCleanupPlan
{
    public const ulong StructureSize = 48;
    public const ulong NoIndex = ~0UL;

    private readonly M01LivePagingCleanupStatus _status;
    private readonly byte _reserved0;
    private readonly ushort _reserved1;
    private readonly uint _reserved2;
    private readonly ulong _completedAllocationCount;
    private readonly ulong _releaseCount;
    private readonly ulong _firstReleaseIndex;
    private readonly ulong _secondReleaseIndex;
    private readonly ulong _thirdReleaseIndex;

    internal M01LivePagingCleanupPlan(
        M01LivePagingCleanupStatus status,
        ulong completedAllocationCount,
        ulong releaseCount,
        ulong firstReleaseIndex,
        ulong secondReleaseIndex,
        ulong thirdReleaseIndex)
    {
        _status = status;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _completedAllocationCount = completedAllocationCount;
        _releaseCount = releaseCount;
        _firstReleaseIndex = firstReleaseIndex;
        _secondReleaseIndex = secondReleaseIndex;
        _thirdReleaseIndex = thirdReleaseIndex;
    }

    public M01LivePagingCleanupStatus Status => _status;

    public ulong CompletedAllocationCount =>
        _completedAllocationCount;

    public ulong ReleaseCount => _releaseCount;

    public ulong FirstReleaseIndex => _firstReleaseIndex;

    public ulong SecondReleaseIndex => _secondReleaseIndex;

    public ulong ThirdReleaseIndex => _thirdReleaseIndex;

    public bool Succeeded =>
        _status == M01LivePagingCleanupStatus.Success
        && _reserved0 == 0
        && _reserved1 == 0
        && _reserved2 == 0
        && _completedAllocationCount == _releaseCount
        && _releaseCount
            <= M01LivePagingIntegration
                .RequiredAllocationRecordCount
        && (_releaseCount == 0
            ? _firstReleaseIndex == NoIndex
                && _secondReleaseIndex == NoIndex
                && _thirdReleaseIndex == NoIndex
            : _releaseCount == 1
                ? _firstReleaseIndex == 0
                    && _secondReleaseIndex == NoIndex
                    && _thirdReleaseIndex == NoIndex
                : _releaseCount == 2
                    ? _firstReleaseIndex == 1
                        && _secondReleaseIndex == 0
                        && _thirdReleaseIndex == NoIndex
                    : _firstReleaseIndex == 2
                        && _secondReleaseIndex == 1
                        && _thirdReleaseIndex == 0);
}

public enum M01LivePagingTableBuildStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidMappingPlan = 2,
    InvalidMappingRangeStorage = 3,
    InvalidTableStorage = 4,
    TableStorageOverlap = 5,
    InvalidRange = 6,
    PageZeroRejected = 7,
    WritableExecutableRejected = 8,
    IdentityAddressMismatch = 9,
    PageImageCreationFailed = 10,
    TablePageCapacityExhausted = 11,
    MalformedExistingEntry = 12,
    MissingChildPage = 13,
    DuplicateLeaf = 14,
    EntryEncodingFailed = 15,
    TablePageCountMismatch = 16,
    MappingPageCountMismatch = 17,
    MissingCr3CodeMapping = 18,
    MissingProtectionProbeMapping = 19,
    MissingTableStateMapping = 20,
    MissingOwnedStackMapping = 21,
    GuardMapped = 22,
    MissingStageOneStubMapping = 23
}

/// <summary>
/// Describes construction of a caller-owned flat page-table image. No
/// processor table page or physical allocation is dereferenced.
/// </summary>
public readonly struct M01LivePagingTableBuildResult
{
    public const ulong StructureSize = 80;
    public const ulong NoIndex = ~0UL;

    private readonly M01LivePagingTableBuildStatus _status;
    private readonly OS.Kernel.Memory.X64PageTableEntryStatus
        _entryStatus;
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

    internal M01LivePagingTableBuildResult(
        M01LivePagingTableBuildStatus status,
        OS.Kernel.Memory.X64PageTableEntryStatus entryStatus,
        PhysicalAddress rootTable,
        ulong rangeCount,
        ulong validatedRangeCount,
        ulong tablePageCount,
        ulong entryCount,
        ulong mappedPageCount,
        ulong invalidRangeIndex,
        ulong invalidPageOrdinal,
        ulong probeLeafEntryIndex)
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

    public OS.Kernel.Memory.X64PageTableEntryStatus EntryStatus =>
        _entryStatus;

    public PhysicalAddress RootTable => _rootTable;

    public ulong RangeCount => _rangeCount;

    public ulong ValidatedRangeCount => _validatedRangeCount;

    public ulong TablePageCount => _tablePageCount;

    public ulong EntryCount => _entryCount;

    public ulong MappedPageCount => _mappedPageCount;

    public ulong InvalidRangeIndex => _invalidRangeIndex;

    public ulong InvalidPageOrdinal => _invalidPageOrdinal;

    public ulong ProbeLeafEntryIndex => _probeLeafEntryIndex;

    public bool Succeeded =>
        _status == M01LivePagingTableBuildStatus.Success
        && _entryStatus
            == OS.Kernel.Memory.X64PageTableEntryStatus.Success
        && _reserved0 == 0
        && _reserved1 == 0
        && _rootTable.IsValid
        && _rangeCount != 0
        && _validatedRangeCount == _rangeCount
        && _tablePageCount != 0
        && _entryCount
            == _tablePageCount
                * OS.Kernel.Memory.X64PageTableImagePage
                    .EntryCountPerPage
        && _mappedPageCount != 0
        && _invalidRangeIndex == NoIndex
        && _invalidPageOrdinal == NoIndex
        && _probeLeafEntryIndex < _entryCount;
}

/// <summary>
/// Reports the first rejected boundary while sizing one caller-owned physical
/// arena that contains every fresh table page followed by one state/canary
/// page. Values are append-only and the zero-valued default is invalid.
/// </summary>
public enum M01LivePagingArenaPlanStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    ContractLayoutMismatch = 2,
    InvalidFixedRequirementCount = 3,
    InvalidFixedRequirementCapacity = 4,
    MissingFixedRequirements = 5,
    MisalignedFixedRequirements = 6,
    FixedRequirementSpanOverflow = 7,
    InvalidFixedRequirementSet = 8,
    InvalidDescriptor = 9,
    InvalidCandidate = 10,
    InvalidPreconditions = 11,
    InvalidIterationLimit = 12,
    InvalidReservationStorage = 13,
    ReservationValidationFailed = 14,
    InvalidRequirementWorkspace = 15,
    InvalidRangeWorkspace = 16,
    WorkspaceOverlap = 17,
    RequirementConstructionFailed = 18,
    IdentityPlanRejected = 19,
    RangeWorkspaceTooSmall = 20,
    TableCapacityExceeded = 21,
    NonMonotonicIteration = 22,
    IterationLimitReached = 23,
    TablePageCountOverflow = 24,
    ArenaGeometryOverflow = 25,
    PhysicalWidthExceeded = 26,
    DescriptorCapacityTooSmall = 27,
    ReservationOverlap = 28,
    CapacityExceedsLimit = 29
}

/// <summary>
/// Value-only result of one bounded table/state-arena fixed-point plan.
/// The exact LoaderData allocation record can be passed unchanged to the
/// materializer after the caller has performed the allocation.
/// </summary>
public readonly struct M01LivePagingArenaPlanResult
{
    public const ushort CurrentVersion = 1;
    public const ulong StructureSize = 568;
    public const ulong NoIndex = ~0UL;

    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly M01LivePagingArenaPlanStatus _status;
    private readonly byte _isInitialized;
    private readonly AddressValueStatus _addressStatus;
    private readonly OS.Kernel.Memory.BootReservationTableStatus
        _reservationStatus;
    private readonly OS.Kernel.Boot.X64BootIdentityMappingStatus
        _mappingStatus;
    private readonly OS.Kernel.Memory.X64PageTableEntryStatus
        _pageTableEntryStatus;
    private readonly uint _reserved;
    private readonly OS.Kernel.Memory.X64PageTableEncodingPreconditions
        _pageTablePreconditions;
    private readonly BootMemoryRegionRecord _allocationRecord;
    private readonly OS.Kernel.Boot.X64BootIdentityMappingPlanResult
        _mappingPlan;
    private readonly PhysicalAddress _candidatePhysicalBase;
    private readonly PhysicalAddress _rootTable;
    private readonly PhysicalAddress _statePage;
    private readonly PhysicalAddress _arenaEndExclusive;
    private readonly ulong _iterationCount;
    private readonly ulong _iterationLimit;
    private readonly ulong _tablePageCount;
    private readonly ulong _statePageCount;
    private readonly ulong _totalPageCount;
    private readonly ulong _tableByteLength;
    private readonly ulong _totalByteLength;
    private readonly ulong _availablePageCountFromCandidate;
    private readonly ulong _requiredFixedRequirementCount;
    private readonly ulong _requiredRequirementWorkspaceCount;
    private readonly ulong _requiredRangeWorkspaceCount;
    private readonly ulong _requiredPageImageCount;
    private readonly ulong _requiredPageTableEntryCount;
    private readonly OS.Kernel.Boot.X64BootIdentityMappingRequirement
        _bootStackGuardRequirement;
    private readonly OS.Kernel.Boot.X64BootIdentityMappingRequirement
        _codeAndCallGateRequirement;
    private readonly OS.Kernel.Boot.X64BootIdentityMappingRequirement
        _ownedStackRequirement;
    private readonly OS.Kernel.Boot.X64BootIdentityMappingRequirement
        _protectionProbeRequirement;

    internal M01LivePagingArenaPlanResult(
        M01LivePagingArenaPlanStatus status,
        AddressValueStatus addressStatus,
        OS.Kernel.Memory.BootReservationTableStatus reservationStatus,
        OS.Kernel.Boot.X64BootIdentityMappingStatus mappingStatus,
        OS.Kernel.Memory.X64PageTableEntryStatus pageTableEntryStatus,
        OS.Kernel.Memory.X64PageTableEncodingPreconditions
            pageTablePreconditions,
        BootMemoryRegionRecord allocationRecord,
        OS.Kernel.Boot.X64BootIdentityMappingPlanResult mappingPlan,
        PhysicalAddress candidatePhysicalBase,
        PhysicalAddress rootTable,
        PhysicalAddress statePage,
        PhysicalAddress arenaEndExclusive,
        ulong iterationCount,
        ulong iterationLimit,
        ulong tablePageCount,
        ulong statePageCount,
        ulong totalPageCount,
        ulong tableByteLength,
        ulong totalByteLength,
        ulong availablePageCountFromCandidate,
        ulong requiredFixedRequirementCount,
        ulong requiredRequirementWorkspaceCount,
        ulong requiredRangeWorkspaceCount,
        ulong requiredPageImageCount,
        ulong requiredPageTableEntryCount,
        OS.Kernel.Boot.X64BootIdentityMappingRequirement
            bootStackGuardRequirement,
        OS.Kernel.Boot.X64BootIdentityMappingRequirement
            codeAndCallGateRequirement,
        OS.Kernel.Boot.X64BootIdentityMappingRequirement
            ownedStackRequirement,
        OS.Kernel.Boot.X64BootIdentityMappingRequirement
            protectionProbeRequirement)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = CurrentVersion;
        _status = status;
        _isInitialized = 1;
        _addressStatus = addressStatus;
        _reservationStatus = reservationStatus;
        _mappingStatus = mappingStatus;
        _pageTableEntryStatus = pageTableEntryStatus;
        _reserved = 0;
        _pageTablePreconditions = pageTablePreconditions;
        _allocationRecord = allocationRecord;
        _mappingPlan = mappingPlan;
        _candidatePhysicalBase = candidatePhysicalBase;
        _rootTable = rootTable;
        _statePage = statePage;
        _arenaEndExclusive = arenaEndExclusive;
        _iterationCount = iterationCount;
        _iterationLimit = iterationLimit;
        _tablePageCount = tablePageCount;
        _statePageCount = statePageCount;
        _totalPageCount = totalPageCount;
        _tableByteLength = tableByteLength;
        _totalByteLength = totalByteLength;
        _availablePageCountFromCandidate =
            availablePageCountFromCandidate;
        _requiredFixedRequirementCount =
            requiredFixedRequirementCount;
        _requiredRequirementWorkspaceCount =
            requiredRequirementWorkspaceCount;
        _requiredRangeWorkspaceCount = requiredRangeWorkspaceCount;
        _requiredPageImageCount = requiredPageImageCount;
        _requiredPageTableEntryCount =
            requiredPageTableEntryCount;
        _bootStackGuardRequirement =
            bootStackGuardRequirement;
        _codeAndCallGateRequirement =
            codeAndCallGateRequirement;
        _ownedStackRequirement = ownedStackRequirement;
        _protectionProbeRequirement =
            protectionProbeRequirement;
    }

    public uint Size => _structureSize;

    public ushort Version => _structureVersion;

    public bool IsInitialized =>
        _structureSize == StructureSize
        && _structureVersion == CurrentVersion
        && _isInitialized == 1
        && _reserved == 0;

    public M01LivePagingArenaPlanStatus Status => _status;

    public AddressValueStatus AddressStatus => _addressStatus;

    public OS.Kernel.Memory.BootReservationTableStatus
        ReservationStatus => _reservationStatus;

    public OS.Kernel.Boot.X64BootIdentityMappingStatus MappingStatus =>
        _mappingStatus;

    public OS.Kernel.Memory.X64PageTableEntryStatus
        PageTableEntryStatus => _pageTableEntryStatus;

    public OS.Kernel.Memory.X64PageTableEncodingPreconditions
        PageTablePreconditions => _pageTablePreconditions;

    public BootMemoryRegionRecord AllocationRecord =>
        _allocationRecord;

    public OS.Kernel.Boot.X64BootIdentityMappingPlanResult MappingPlan =>
        _mappingPlan;

    public PhysicalAddress CandidatePhysicalBase =>
        _candidatePhysicalBase;

    public PhysicalAddress RootTable => _rootTable;

    public PhysicalAddress StatePage => _statePage;

    public PhysicalAddress ArenaEndExclusive =>
        _arenaEndExclusive;

    public ulong IterationCount => _iterationCount;

    public ulong IterationLimit => _iterationLimit;

    public ulong TablePageCount => _tablePageCount;

    public ulong StatePageCount => _statePageCount;

    public ulong TotalPageCount => _totalPageCount;

    public ulong TableByteLength => _tableByteLength;

    public ulong TotalByteLength => _totalByteLength;

    public ulong AvailablePageCountFromCandidate =>
        _availablePageCountFromCandidate;

    public ulong RequiredFixedRequirementCount =>
        _requiredFixedRequirementCount;

    public ulong RequiredRequirementWorkspaceCount =>
        _requiredRequirementWorkspaceCount;

    public ulong RequiredRangeWorkspaceCount =>
        _requiredRangeWorkspaceCount;

    public ulong RequiredPageImageCount =>
        _requiredPageImageCount;

    public ulong RequiredPageTableEntryCount =>
        _requiredPageTableEntryCount;

    public OS.Kernel.Boot.X64BootIdentityMappingRequirement
        BootStackGuardRequirement =>
            _bootStackGuardRequirement;

    public OS.Kernel.Boot.X64BootIdentityMappingRequirement
        CodeAndCallGateRequirement =>
            _codeAndCallGateRequirement;

    public OS.Kernel.Boot.X64BootIdentityMappingRequirement
        OwnedStackRequirement => _ownedStackRequirement;

    public OS.Kernel.Boot.X64BootIdentityMappingRequirement
        ProtectionProbeRequirement =>
            _protectionProbeRequirement;

    public bool Succeeded =>
        IsInitialized
        && _status == M01LivePagingArenaPlanStatus.Success
        && _addressStatus == AddressValueStatus.Success
        && _reservationStatus
            == OS.Kernel.Memory.BootReservationTableStatus.Success
        && _mappingStatus
            == OS.Kernel.Boot.X64BootIdentityMappingStatus.Success
        && _pageTableEntryStatus
            == OS.Kernel.Memory.X64PageTableEntryStatus.Success
        && _mappingPlan.Succeeded
        && _candidatePhysicalBase.IsValid
        && _rootTable.IsValid
        && _statePage.IsValid
        && _arenaEndExclusive.IsValid
        && _tablePageCount != 0
        && _statePageCount
            == M01LivePagingIntegration.RequiredArenaStatePageCount
        && _totalPageCount
            == _tablePageCount + _statePageCount
        && _requiredPageImageCount == _tablePageCount
        && _requiredPageTableEntryCount
            == _tablePageCount
                * OS.Kernel.Memory.X64PageTableImagePage
                    .EntryCountPerPage
        && _bootStackGuardRequirement.IsInitialized
        && _bootStackGuardRequirement.Purpose
            == OS.Kernel.Boot.X64BootIdentityMappingPurpose
                .BootStackGuard
        && _codeAndCallGateRequirement.IsInitialized
        && _codeAndCallGateRequirement.Purpose
            == OS.Kernel.Boot.X64BootIdentityMappingPurpose
                .CompositeCodeAndCallGate
        && _ownedStackRequirement.IsInitialized
        && _ownedStackRequirement.Purpose
            == OS.Kernel.Boot.X64BootIdentityMappingPurpose
                .CompositeOwnedStack
        && _protectionProbeRequirement.IsInitialized
        && _protectionProbeRequirement.Purpose
            == OS.Kernel.Boot.X64BootIdentityMappingPurpose
                .ProtectionProbePage;
}

/// <summary>
/// Controls whether the arena materializer writes the audited table bytes or
/// independently verifies table bytes already supplied by the caller.
/// </summary>
public enum M01LivePagingArenaByteMode : byte
{
    Uninitialized = 0,
    EmitAndVerify = 1,
    VerifyExisting = 2
}

/// <summary>
/// Reports the first rejected arena materialization or independent readback
/// boundary. Values are append-only and the zero-valued default is invalid.
/// </summary>
public enum M01LivePagingArenaMaterializationStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    ContractLayoutMismatch = 2,
    InvalidByteMode = 3,
    InvalidPlan = 4,
    AllocationRecordMismatch = 5,
    InvalidPageImageCount = 6,
    PageImageCapacityTooSmall = 7,
    PageImageCapacityExceedsLimit = 8,
    MissingPageImages = 9,
    MisalignedPageImages = 10,
    PageImageSpanOverflow = 11,
    InvalidEntryCount = 12,
    EntryCountMismatch = 13,
    EntryCapacityTooSmall = 14,
    EntryCapacityExceedsLimit = 15,
    MissingEntries = 16,
    MisalignedEntries = 17,
    EntrySpanOverflow = 18,
    MissingArenaBacking = 19,
    InvalidArenaByteCount = 20,
    ArenaCapacityTooSmall = 21,
    ArenaCapacityExceedsLimit = 22,
    MisalignedArenaBacking = 23,
    ArenaBackingSpanOverflow = 24,
    StorageOverlap = 25,
    PageImageMalformed = 26,
    PageImageOrderInvalid = 27,
    PageImageOutsideTableArena = 28,
    PageImageTargetsStatePage = 29,
    HierarchyMalformed = 30,
    MissingChildPage = 31,
    DuplicateChildOrCycle = 32,
    UnreachablePageImage = 33,
    LeafCountMismatch = 34,
    EntryMismatch = 35,
    StatePageChanged = 36,
    SemanticMappingMismatch = 37,
    MissingStateSnapshot = 38,
    InvalidStateSnapshotByteCount = 39,
    StateSnapshotCapacityTooSmall = 40,
    StateSnapshotCapacityExceedsLimit = 41,
    MisalignedStateSnapshot = 42,
    StateSnapshotSpanOverflow = 43,
    StateSnapshotStorageOverlap = 44
}

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
    private readonly OS.Kernel.Memory.X64PageTableEntryStatus
        _pageTableEntryStatus;
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

    internal M01LivePagingArenaMaterializationResult(
        M01LivePagingArenaMaterializationStatus status,
        OS.Kernel.Memory.X64PageTableEntryStatus pageTableEntryStatus,
        OS.Kernel.Memory.X64PageMapStatus pageImageStatus,
        PhysicalAddress rootTable,
        ulong tablePageCount,
        ulong verifiedPageCount,
        ulong entryCount,
        ulong verifiedEntryCount,
        ulong invalidPageOrdinal,
        ulong invalidEntryIndex,
        ulong expectedEntry,
        ulong observedEntry,
        ulong sourceFingerprint,
        ulong observedFingerprint,
        ulong stateFingerprintBefore,
        ulong stateFingerprintAfter,
        ulong bytesWritten,
        ulong reachablePageCount,
        ulong mappedLeafCount,
        ulong verifiedStateByteCount,
        ulong invalidStateByteIndex)
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

    public bool IsInitialized =>
        _structureSize == StructureSize
        && _structureVersion == CurrentVersion
        && _isInitialized == 1
        && _reserved0 == 0
        && _reserved1 == 0;

    public M01LivePagingArenaMaterializationStatus Status =>
        _status;

    public OS.Kernel.Memory.X64PageTableEntryStatus
        PageTableEntryStatus => _pageTableEntryStatus;

    public OS.Kernel.Memory.X64PageMapStatus PageImageStatus =>
        _pageImageStatus;

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

    public ulong StateFingerprintBefore =>
        _stateFingerprintBefore;

    public ulong StateFingerprintAfter => _stateFingerprintAfter;

    public ulong BytesWritten => _bytesWritten;

    public ulong ReachablePageCount => _reachablePageCount;

    public ulong MappedLeafCount => _mappedLeafCount;

    public ulong VerifiedStateByteCount =>
        _verifiedStateByteCount;

    public ulong InvalidStateByteIndex =>
        _invalidStateByteIndex;

    public bool Succeeded =>
        IsInitialized
        && _status
            == M01LivePagingArenaMaterializationStatus.Success
        && _pageTableEntryStatus
            == OS.Kernel.Memory.X64PageTableEntryStatus.Success
        && _pageImageStatus
            == OS.Kernel.Memory.X64PageMapStatus.Success
        && _rootTable.IsValid
        && _tablePageCount != 0
        && _verifiedPageCount == _tablePageCount
        && _verifiedEntryCount == _entryCount
        && _invalidPageOrdinal == NoIndex
        && _invalidEntryIndex == NoIndex
        && _sourceFingerprint == _observedFingerprint
        && _stateFingerprintBefore == _stateFingerprintAfter
        && _reachablePageCount == _tablePageCount
        && _verifiedStateByteCount
            == M01LivePagingIntegration
                .RequiredArenaStateSnapshotByteCount
        && _invalidStateByteIndex == NoIndex;
}

/// <summary>
/// Durable caller-owned state written only after every stage-three host audit
/// succeeds. Its contents remain value-only and no pointer is retained.
/// </summary>
public unsafe struct M01LivePagingStaticState
{
    public const uint CurrentVersion = 1;
    public const ulong RequiredStorageAlignment = 8;

    internal uint HeaderSize;
    internal uint HeaderVersion;
    internal M01LivePagingStatus CoordinatorStatus;
    internal M01LivePagingCheckpoint CoordinatorCheckpoint;
    internal byte Reserved0;
    internal byte Reserved1;
    internal byte Reserved2;
    internal byte Reserved3;
    internal byte Reserved4;
    internal byte Reserved5;
    internal ulong StageTwoStateAddressValue;
    internal ulong StageTwoStateCapacityValue;
    internal ulong RefreshedMapKeyValue;
    internal ulong RefreshedMapGenerationValue;
    internal OS.Kernel.Memory.BootReservedRange
        Cr3CodeReservationValue;
    internal OS.Kernel.Memory.BootReservedRange
        ProtectionProbeReservationValue;
    internal OS.Kernel.Memory.BootReservedRange
        TableStateReservationValue;
    internal PhysicalAddress OwnedRootValue;
    internal ulong RawFirmwareCr3Value;
    internal ulong RawCr4Value;
    internal ulong OriginalRflagsValue;
    internal ulong FinalCanaryValue;
    internal ulong Cr3ElapsedTicksValue;
    internal ulong Cr3EventCountValue;
    internal ulong ProtectionEventCountValue;
    internal ulong TablePageCountValue;
    internal ulong MappedPageCountValue;
    internal byte PageZeroAbsentValue;
    internal byte Reserved6;
    internal ushort Reserved7;
    internal uint Reserved8;

    public static ulong RequiredStorageBytes =>
        (ulong)sizeof(M01LivePagingStaticState);

    public uint Size => HeaderSize;

    public uint Version => HeaderVersion;

    public M01LivePagingStatus Status => CoordinatorStatus;

    public M01LivePagingCheckpoint Checkpoint =>
        CoordinatorCheckpoint;

    public ulong StageTwoStateAddress => StageTwoStateAddressValue;

    public ulong StageTwoStateCapacity => StageTwoStateCapacityValue;

    public ulong RefreshedMapKey => RefreshedMapKeyValue;

    public ulong RefreshedMapGeneration =>
        RefreshedMapGenerationValue;

    public OS.Kernel.Memory.BootReservedRange Cr3CodeReservation =>
        Cr3CodeReservationValue;

    public OS.Kernel.Memory.BootReservedRange
        ProtectionProbeReservation =>
            ProtectionProbeReservationValue;

    public OS.Kernel.Memory.BootReservedRange TableStateReservation =>
        TableStateReservationValue;

    public PhysicalAddress OwnedRoot => OwnedRootValue;

    public ulong RawFirmwareCr3 => RawFirmwareCr3Value;

    public ulong RawCr4 => RawCr4Value;

    public ulong OriginalRflags => OriginalRflagsValue;

    public ulong FinalCanary => FinalCanaryValue;

    public ulong Cr3ElapsedTicks => Cr3ElapsedTicksValue;

    public ulong Cr3EventCount => Cr3EventCountValue;

    public ulong ProtectionEventCount => ProtectionEventCountValue;

    public ulong TablePageCount => TablePageCountValue;

    public ulong MappedPageCount => MappedPageCountValue;

    public bool PageZeroAbsent => PageZeroAbsentValue == 1;

    public bool IsCompleted =>
        M01LivePagingIntegration.IsCompletedState(this);
}

/// <summary>
/// Value-only leaf results retained for diagnosis. Pointer inputs are never
/// persisted in this record.
/// </summary>
public struct M01LivePagingEvidence
{
    internal M01LivePagingAllocationEvidence AllocationEvidenceValue;
    internal M01LivePagingMapLedgerEvidence MapLedgerEvidenceValue;
    internal OS.Kernel.Memory.BootReservationTableValidationResult
        ReservationValidationValue;
    internal ulong Cr3CodeReservationIndexValue;
    internal ulong ProtectionProbeReservationIndexValue;
    internal ulong TableStateReservationIndexValue;
    internal OS.Kernel.Boot.X64BootIdentityMappingPlanResult
        MappingPlanValue;
    internal M01LivePagingTableBuildResult TableBuildValue;
    internal OS.Kernel.Memory.X64PagingModePreflightStubResult
        PreflightMaterializationValue;
    internal OS.Kernel.Memory.X64PagingModePreflightStubResult
        PreflightReadbackValue;
    internal OS.Kernel.Memory.X64PagingModePreflightResult
        PreflightValue;
    internal OS.Kernel.Memory
        .X64ReversibleCr3TransitionStubPlanResult
        TransitionMaterializationValue;
    internal OS.Kernel.Memory
        .X64ReversibleCr3TransitionStubPlanResult
        TransitionReadbackValue;
    internal OS.Kernel.Memory
        .X64ReversibleCr3TransitionEvidenceResult
        TransitionEvidenceValue;
    internal OS.Kernel.Memory.X64Cr3LoadStubPlanResult Cr3PlanValue;
    internal OS.Kernel.Memory.X64Cr3ProbeAuditResult Cr3AuditValue;
    internal OS.Kernel.Memory.X64InvlpgStubPlanResult
        InvlpgMaterializationValue;
    internal OS.Kernel.Memory.X64InvlpgStubPlanResult
        InvlpgReadbackValue;
    internal OS.Kernel.Memory.X64ProtectionProbeStubPlanResult
        ProbeMaterializationValue;
    internal OS.Kernel.Memory.X64ProtectionProbeStubPlanResult
        ProbeReadbackValue;
    internal OS.Kernel.Memory.X64PageMapResult
        ProtectionMutationValue;
    internal OS.Kernel.Memory.X64ProtectionProbePlanResult
        ProtectionPlanValue;
    internal OS.Kernel.Memory.PageZeroMappingPolicyResult
        PageZeroAuditValue;

    public M01LivePagingAllocationEvidence AllocationEvidence =>
        AllocationEvidenceValue;

    public M01LivePagingMapLedgerEvidence MapLedgerEvidence =>
        MapLedgerEvidenceValue;

    public OS.Kernel.Memory.BootReservationTableValidationResult
        ReservationValidation => ReservationValidationValue;

    public ulong Cr3CodeReservationIndex =>
        Cr3CodeReservationIndexValue;

    public ulong ProtectionProbeReservationIndex =>
        ProtectionProbeReservationIndexValue;

    public ulong TableStateReservationIndex =>
        TableStateReservationIndexValue;

    public OS.Kernel.Boot.X64BootIdentityMappingPlanResult
        MappingPlan => MappingPlanValue;

    public M01LivePagingTableBuildResult TableBuild =>
        TableBuildValue;

    public OS.Kernel.Memory.X64PagingModePreflightStubResult
        PreflightMaterialization => PreflightMaterializationValue;

    public OS.Kernel.Memory.X64PagingModePreflightStubResult
        PreflightReadback => PreflightReadbackValue;

    public OS.Kernel.Memory.X64PagingModePreflightResult Preflight =>
        PreflightValue;

    public OS.Kernel.Memory
        .X64ReversibleCr3TransitionStubPlanResult
        TransitionMaterialization => TransitionMaterializationValue;

    public OS.Kernel.Memory
        .X64ReversibleCr3TransitionStubPlanResult
        TransitionReadback => TransitionReadbackValue;

    public OS.Kernel.Memory
        .X64ReversibleCr3TransitionEvidenceResult
        TransitionEvidence => TransitionEvidenceValue;

    public OS.Kernel.Memory.X64Cr3LoadStubPlanResult Cr3Plan =>
        Cr3PlanValue;

    public OS.Kernel.Memory.X64Cr3ProbeAuditResult Cr3Audit =>
        Cr3AuditValue;

    public OS.Kernel.Memory.X64InvlpgStubPlanResult
        InvlpgMaterialization => InvlpgMaterializationValue;

    public OS.Kernel.Memory.X64InvlpgStubPlanResult InvlpgReadback =>
        InvlpgReadbackValue;

    public OS.Kernel.Memory.X64ProtectionProbeStubPlanResult
        ProbeMaterialization => ProbeMaterializationValue;

    public OS.Kernel.Memory.X64ProtectionProbeStubPlanResult
        ProbeReadback => ProbeReadbackValue;

    public OS.Kernel.Memory.X64PageMapResult ProtectionMutation =>
        ProtectionMutationValue;

    public OS.Kernel.Memory.X64ProtectionProbePlanResult
        ProtectionPlan => ProtectionPlanValue;

    public OS.Kernel.Memory.PageZeroMappingPolicyResult
        PageZeroAudit => PageZeroAuditValue;
}

/// <summary>
/// Preparation-only description of the modeled probe-leaf rewrite and the
/// exact one-target invalidation invocation that a later composite transition
/// may consume. The caller-owned leaf is restored before this value can
/// succeed. No instruction is invoked.
/// </summary>
public readonly struct M01LivePagingProtectionPreparation
{
    private readonly M01LivePagingStatus _status;
    private readonly byte _initialLeafRestored;
    private readonly ushort _reserved0;
    private readonly uint _reserved1;
    private readonly OS.Kernel.Memory.X64PageMapResult _mutation;
    private readonly OS.Kernel.Memory.X64InvlpgInvocationPlanResult
        _invlpgInvocation;
    private readonly ulong _protectedLeafReadback;

    internal M01LivePagingProtectionPreparation(
        M01LivePagingStatus status,
        OS.Kernel.Memory.X64PageMapResult mutation,
        OS.Kernel.Memory.X64InvlpgInvocationPlanResult
            invlpgInvocation,
        ulong protectedLeafReadback,
        bool initialLeafRestored)
    {
        _status = status;
        _initialLeafRestored = initialLeafRestored ? (byte)1 : (byte)0;
        _reserved0 = 0;
        _reserved1 = 0;
        _mutation = mutation;
        _invlpgInvocation = invlpgInvocation;
        _protectedLeafReadback = protectedLeafReadback;
    }

    public M01LivePagingStatus Status => _status;

    public OS.Kernel.Memory.X64PageMapResult Mutation => _mutation;

    public OS.Kernel.Memory.X64InvlpgInvocationPlanResult
        InvlpgInvocation => _invlpgInvocation;

    public ulong ProtectedLeafReadback => _protectedLeafReadback;

    public bool InitialLeafRestored => _initialLeafRestored == 1;

    public bool Succeeded =>
        _status == M01LivePagingStatus.Success
        && _initialLeafRestored == 1
        && _reserved0 == 0
        && _reserved1 == 0
        && _mutation.Succeeded
        && _invlpgInvocation.Succeeded
        && _protectedLeafReadback
            == _mutation.ReplacementEntry.RawValue;
}

/// <summary>
/// Value-only M01-029 evidence. It deliberately omits transition runtime
/// state, CR3 events, protection observations/events, and CR3 policy. The
/// included leaf results describe preparation and caller-owned model
/// mutation only.
/// </summary>
public struct M01LivePagingPreparationEvidence
{
    internal M01LivePagingAllocationEvidence AllocationEvidenceValue;
    internal M01LivePagingMapLedgerEvidence MapLedgerEvidenceValue;
    internal OS.Kernel.Memory.BootReservationTableValidationResult
        ReservationValidationValue;
    internal ulong Cr3CodeReservationIndexValue;
    internal ulong ProtectionProbeReservationIndexValue;
    internal ulong TableStateReservationIndexValue;
    internal OS.Kernel.Boot.X64BootIdentityMappingPlanResult
        MappingPlanValue;
    internal M01LivePagingTableBuildResult TableBuildValue;
    internal OS.Kernel.Memory.X64PagingModePreflightStubResult
        PreflightMaterializationValue;
    internal OS.Kernel.Memory.X64PagingModePreflightStubResult
        PreflightReadbackValue;
    internal OS.Kernel.Memory.X64PagingModePreflightResult
        PreflightValue;
    internal OS.Kernel.Memory
        .X64ReversibleCr3TransitionStubPlanResult
        TransitionMaterializationValue;
    internal OS.Kernel.Memory
        .X64ReversibleCr3TransitionStubPlanResult
        TransitionReadbackValue;
    internal OS.Kernel.Memory.X64InvlpgStubPlanResult
        InvlpgMaterializationValue;
    internal OS.Kernel.Memory.X64InvlpgStubPlanResult
        InvlpgReadbackValue;
    internal OS.Kernel.Memory.X64ProtectionProbeStubPlanResult
        ProbeMaterializationValue;
    internal OS.Kernel.Memory.X64ProtectionProbeStubPlanResult
        ProbeReadbackValue;
    internal M01LivePagingProtectionPreparation
        ProtectionPreparationValue;
    internal OS.Kernel.Memory.PageZeroMappingPolicyResult
        PageZeroAuditValue;
    internal M01LivePagingCleanupPlan CleanupPlanValue;

    public M01LivePagingAllocationEvidence AllocationEvidence =>
        AllocationEvidenceValue;

    public M01LivePagingMapLedgerEvidence MapLedgerEvidence =>
        MapLedgerEvidenceValue;

    public OS.Kernel.Memory.BootReservationTableValidationResult
        ReservationValidation => ReservationValidationValue;

    public ulong Cr3CodeReservationIndex =>
        Cr3CodeReservationIndexValue;

    public ulong ProtectionProbeReservationIndex =>
        ProtectionProbeReservationIndexValue;

    public ulong TableStateReservationIndex =>
        TableStateReservationIndexValue;

    public OS.Kernel.Boot.X64BootIdentityMappingPlanResult
        MappingPlan => MappingPlanValue;

    public M01LivePagingTableBuildResult TableBuild =>
        TableBuildValue;

    public OS.Kernel.Memory.X64PagingModePreflightStubResult
        PreflightMaterialization => PreflightMaterializationValue;

    public OS.Kernel.Memory.X64PagingModePreflightStubResult
        PreflightReadback => PreflightReadbackValue;

    public OS.Kernel.Memory.X64PagingModePreflightResult Preflight =>
        PreflightValue;

    public OS.Kernel.Memory
        .X64ReversibleCr3TransitionStubPlanResult
        TransitionMaterialization => TransitionMaterializationValue;

    public OS.Kernel.Memory
        .X64ReversibleCr3TransitionStubPlanResult
        TransitionReadback => TransitionReadbackValue;

    public OS.Kernel.Memory.X64InvlpgStubPlanResult
        InvlpgMaterialization => InvlpgMaterializationValue;

    public OS.Kernel.Memory.X64InvlpgStubPlanResult InvlpgReadback =>
        InvlpgReadbackValue;

    public OS.Kernel.Memory.X64ProtectionProbeStubPlanResult
        ProbeMaterialization => ProbeMaterializationValue;

    public OS.Kernel.Memory.X64ProtectionProbeStubPlanResult
        ProbeReadback => ProbeReadbackValue;

    public M01LivePagingProtectionPreparation ProtectionPreparation =>
        ProtectionPreparationValue;

    public OS.Kernel.Memory.X64PageMapResult ProtectionMutation =>
        ProtectionPreparationValue.Mutation;

    public OS.Kernel.Memory.X64InvlpgInvocationPlanResult
        InvlpgInvocation =>
            ProtectionPreparationValue.InvlpgInvocation;

    public OS.Kernel.Memory.PageZeroMappingPolicyResult
        PageZeroAudit => PageZeroAuditValue;

    public M01LivePagingCleanupPlan CleanupPlan => CleanupPlanValue;

    public bool Succeeded =>
        ReservationValidationValue.Succeeded
        && MappingPlanValue.Succeeded
        && TableBuildValue.Succeeded
        && PreflightMaterializationValue.Succeeded
        && PreflightReadbackValue.Succeeded
        && PreflightReadbackValue.ExactBytes
        && PreflightValue.Succeeded
        && TransitionMaterializationValue.Succeeded
        && TransitionReadbackValue.Succeeded
        && InvlpgMaterializationValue.Succeeded
        && InvlpgReadbackValue.ReadBackVerified
        && ProbeMaterializationValue.Succeeded
        && ProbeReadbackValue.ReadBackVerified
        && ProtectionPreparationValue.Succeeded
        && PageZeroAuditValue.ProvesPageZeroAbsentInSuppliedImage
        && CleanupPlanValue.Succeeded;
}

/// <summary>
/// Durable caller-owned M01-029 preparation state. Pointer values are retained
/// only as explicit address/count/capacity facts for the later serialized
/// integration issues; this record owns none of the referenced storage.
/// </summary>
public unsafe struct M01LivePagingPreparationState
{
    public const uint CurrentVersion = 1;
    public const ulong RequiredStorageAlignment = 8;

    internal uint HeaderSize;
    internal uint HeaderVersion;
    internal M01LivePagingStatus PreparationStatus;
    internal M01LivePagingCheckpoint PreparationCheckpoint;
    internal M01LivePagingExecutionMode ExecutionModeValue;
    internal byte Reserved0;
    internal uint Reserved1;
    internal ulong StageTwoStateAddressValue;
    internal ulong StageTwoStateCountValue;
    internal ulong StageTwoStateCapacityValue;
    internal ulong AllocationTableAddressValue;
    internal ulong AllocationRecordCountValue;
    internal ulong AllocationRecordCapacityValue;
    internal ulong ReservationLedgerAddressValue;
    internal ulong ReservationCountValue;
    internal ulong ReservationCapacityValue;
    internal ulong MappingRequirementAddressValue;
    internal ulong MappingRequirementCountValue;
    internal ulong MappingRequirementCapacityValue;
    internal ulong MappingRangeAddressValue;
    internal ulong MappingRangeCountValue;
    internal ulong MappingRangeCapacityValue;
    internal ulong PageImageAddressValue;
    internal ulong PageImageCountValue;
    internal ulong PageImageCapacityValue;
    internal ulong PageEntryAddressValue;
    internal ulong PageEntryCountValue;
    internal ulong PageEntryCapacityValue;
    internal ulong Cr3CodePageAddressValue;
    internal ulong Cr3CodePageCapacityValue;
    internal ulong ProtectionProbePageAddressValue;
    internal ulong ProtectionProbePageCapacityValue;
    internal ulong PreflightObservationAddressValue;
    internal ulong PreflightObservationCountValue;
    internal ulong PreflightObservationCapacityValue;
    internal ulong RefreshedMapKeyValue;
    internal ulong RefreshedMapGenerationValue;
    internal OS.Kernel.Memory.BootReservedRange
        Cr3CodeReservationValue;
    internal OS.Kernel.Memory.BootReservedRange
        ProtectionProbeReservationValue;
    internal OS.Kernel.Memory.BootReservedRange
        TableStateReservationValue;
    internal PhysicalAddress OwnedRootValue;
    internal ulong RawFirmwareCr3Value;
    internal ulong RawCr4Value;
    internal ulong OriginalRflagsValue;
    internal ulong TablePageCountValue;
    internal ulong MappedPageCountValue;
    internal ulong ProbeLeafEntryIndexValue;
    internal byte PageZeroAbsentValue;
    internal byte StubBytesVerifiedValue;
    internal byte InitialProbeLeafRestoredValue;
    internal byte CleanupPlannedValue;
    internal uint Reserved2;

    public static ulong RequiredStorageBytes =>
        (ulong)sizeof(M01LivePagingPreparationState);

    public uint Size => HeaderSize;

    public uint Version => HeaderVersion;

    public M01LivePagingStatus Status => PreparationStatus;

    public M01LivePagingCheckpoint Checkpoint =>
        PreparationCheckpoint;

    public M01LivePagingExecutionMode ExecutionMode =>
        ExecutionModeValue;

    public ulong StageTwoStateAddress => StageTwoStateAddressValue;

    public ulong StageTwoStateCount => StageTwoStateCountValue;

    public ulong StageTwoStateCapacity => StageTwoStateCapacityValue;

    public ulong AllocationTableAddress => AllocationTableAddressValue;

    public ulong AllocationRecordCount => AllocationRecordCountValue;

    public ulong AllocationRecordCapacity =>
        AllocationRecordCapacityValue;

    public ulong ReservationLedgerAddress =>
        ReservationLedgerAddressValue;

    public ulong ReservationCount => ReservationCountValue;

    public ulong ReservationCapacity => ReservationCapacityValue;

    public ulong MappingRequirementAddress =>
        MappingRequirementAddressValue;

    public ulong MappingRequirementCount => MappingRequirementCountValue;

    public ulong MappingRequirementCapacity =>
        MappingRequirementCapacityValue;

    public ulong MappingRangeAddress => MappingRangeAddressValue;

    public ulong MappingRangeCount => MappingRangeCountValue;

    public ulong MappingRangeCapacity => MappingRangeCapacityValue;

    public ulong PageImageAddress => PageImageAddressValue;

    public ulong PageImageCount => PageImageCountValue;

    public ulong PageImageCapacity => PageImageCapacityValue;

    public ulong PageEntryAddress => PageEntryAddressValue;

    public ulong PageEntryCount => PageEntryCountValue;

    public ulong PageEntryCapacity => PageEntryCapacityValue;

    public ulong Cr3CodePageAddress => Cr3CodePageAddressValue;

    public ulong Cr3CodePageCapacity => Cr3CodePageCapacityValue;

    public ulong ProtectionProbePageAddress =>
        ProtectionProbePageAddressValue;

    public ulong ProtectionProbePageCapacity =>
        ProtectionProbePageCapacityValue;

    public ulong PreflightObservationAddress =>
        PreflightObservationAddressValue;

    public ulong PreflightObservationCount =>
        PreflightObservationCountValue;

    public ulong PreflightObservationCapacity =>
        PreflightObservationCapacityValue;

    public ulong RefreshedMapKey => RefreshedMapKeyValue;

    public ulong RefreshedMapGeneration =>
        RefreshedMapGenerationValue;

    public OS.Kernel.Memory.BootReservedRange Cr3CodeReservation =>
        Cr3CodeReservationValue;

    public OS.Kernel.Memory.BootReservedRange
        ProtectionProbeReservation =>
            ProtectionProbeReservationValue;

    public OS.Kernel.Memory.BootReservedRange TableStateReservation =>
        TableStateReservationValue;

    public PhysicalAddress OwnedRoot => OwnedRootValue;

    public ulong RawFirmwareCr3 => RawFirmwareCr3Value;

    public ulong RawCr4 => RawCr4Value;

    public ulong OriginalRflags => OriginalRflagsValue;

    public ulong TablePageCount => TablePageCountValue;

    public ulong MappedPageCount => MappedPageCountValue;

    public ulong ProbeLeafEntryIndex => ProbeLeafEntryIndexValue;

    public bool PageZeroAbsent => PageZeroAbsentValue == 1;

    public bool StubBytesVerified => StubBytesVerifiedValue == 1;

    public bool InitialProbeLeafRestored =>
        InitialProbeLeafRestoredValue == 1;

    public bool CleanupPlanned => CleanupPlannedValue == 1;

    public bool IsPrepared =>
        M01LivePagingIntegration.IsPreparedState(this);
}

/// <summary>
/// Result of M01-029 preparation. Success is preparation evidence only and
/// cannot be interpreted as generated-code, privileged, EFI, or guest
/// execution.
/// </summary>
public readonly struct M01LivePagingPreparationResult
{
    private readonly M01LivePagingStatus _status;
    private readonly M01LivePagingCheckpoint _checkpoint;
    private readonly M01LivePagingExecutionMode _executionMode;
    private readonly byte _reserved0;
    private readonly uint _reserved1;
    private readonly M01LivePagingPreparationEvidence _evidence;
    private readonly M01LivePagingPreparationState _state;

    private M01LivePagingPreparationResult(
        M01LivePagingStatus status,
        M01LivePagingCheckpoint checkpoint,
        M01LivePagingExecutionMode executionMode,
        M01LivePagingPreparationEvidence evidence,
        M01LivePagingPreparationState state)
    {
        _status = status;
        _checkpoint = checkpoint;
        _executionMode = executionMode;
        _reserved0 = 0;
        _reserved1 = 0;
        _evidence = evidence;
        _state = state;
    }

    public M01LivePagingStatus Status => _status;

    public M01LivePagingCheckpoint LastCompletedCheckpoint =>
        _checkpoint;

    public M01LivePagingExecutionMode ExecutionMode =>
        _executionMode;

    public M01LivePagingPreparationEvidence Evidence => _evidence;

    public M01LivePagingPreparationState State => _state;

    public bool IsPreparationOnly =>
        _executionMode == M01LivePagingExecutionMode.PreparationOnly;

    public bool IsModelOnly => false;

    public bool IsExecutionEvidence => false;

    public bool Succeeded =>
        _status == M01LivePagingStatus.Success
        && _checkpoint
            == M01LivePagingCheckpoint.PreparedForCompositeTransition
        && _executionMode
            == M01LivePagingExecutionMode.PreparationOnly
        && _reserved0 == 0
        && _reserved1 == 0
        && _evidence.Succeeded
        && _state.IsPrepared;

    public bool PreparedForCompositeTransition => Succeeded;

    public bool UsesExactLiveIntervalBindings =>
        Succeeded
        && _state.MappingRequirementCount
            == M01LivePagingIntegration
                .RequiredLiveIntervalMappingRequirementCount
        && _evidence.MappingPlan.RequiredPurposeMask
            == OS.Kernel.Boot.X64BootIdentityMappingPlan
                .RequiredLiveIntervalPurposeMask;

    internal static M01LivePagingPreparationResult Success(
        M01LivePagingPreparationEvidence evidence,
        M01LivePagingPreparationState state)
    {
        return new M01LivePagingPreparationResult(
            M01LivePagingStatus.Success,
            M01LivePagingCheckpoint.PreparedForCompositeTransition,
            M01LivePagingExecutionMode.PreparationOnly,
            evidence,
            state);
    }

    internal static M01LivePagingPreparationResult Fail(
        M01LivePagingStatus status,
        M01LivePagingCheckpoint checkpoint,
        M01LivePagingPreparationEvidence evidence)
    {
        return new M01LivePagingPreparationResult(
            status,
            checkpoint,
            M01LivePagingExecutionMode.PreparationOnly,
            evidence,
            default);
    }
}

public readonly struct M01LivePagingIntegrationResult
{
    private readonly M01LivePagingStatus _status;
    private readonly M01LivePagingCheckpoint _checkpoint;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly byte _reserved2;
    private readonly byte _reserved3;
    private readonly byte _reserved4;
    private readonly byte _reserved5;
    private readonly M01LivePagingEvidence _evidence;
    private readonly M01LivePagingStaticState _staticState;

    private M01LivePagingIntegrationResult(
        M01LivePagingStatus status,
        M01LivePagingCheckpoint checkpoint,
        M01LivePagingEvidence evidence,
        M01LivePagingStaticState staticState)
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
        _staticState = staticState;
    }

    public M01LivePagingStatus Status => _status;

    public M01LivePagingCheckpoint LastCompletedCheckpoint =>
        _checkpoint;

    public M01LivePagingEvidence Evidence => _evidence;

    public M01LivePagingStaticState StaticState => _staticState;

    public M01LivePagingExecutionMode ExecutionMode =>
        M01LivePagingExecutionMode.ModelOnlyLegacy;

    public bool IsModelOnly => true;

    public bool IsExecutionEvidence => false;

    public bool PreparedForCompositeTransition => false;

    public bool Succeeded =>
        _status == M01LivePagingStatus.Success
        && _checkpoint == M01LivePagingCheckpoint.Completed
        && _reserved0 == 0
        && _reserved1 == 0
        && _reserved2 == 0
        && _reserved3 == 0
        && _reserved4 == 0
        && _reserved5 == 0
        && _evidence.ReservationValidationValue.Succeeded
        && _evidence.MappingPlanValue.Succeeded
        && _evidence.TableBuildValue.Succeeded
        && _evidence.PreflightMaterializationValue.Succeeded
        && _evidence.PreflightReadbackValue.Succeeded
        && _evidence.PreflightValue.Succeeded
        && _evidence.TransitionMaterializationValue.Succeeded
        && _evidence.TransitionReadbackValue.Succeeded
        && _evidence.TransitionEvidenceValue.Succeeded
        && _evidence.Cr3PlanValue.Succeeded
        && _evidence.Cr3AuditValue.Succeeded
        && _evidence.InvlpgMaterializationValue.Succeeded
        && _evidence.InvlpgReadbackValue.ReadBackVerified
        && _evidence.ProbeMaterializationValue.Succeeded
        && _evidence.ProbeReadbackValue.ReadBackVerified
        && _evidence.ProtectionMutationValue.Succeeded
        && _evidence.ProtectionPlanValue.Succeeded
        && _evidence.PageZeroAuditValue
            .ProvesPageZeroAbsentInSuppliedImage
        && _staticState.IsCompleted;

    internal static M01LivePagingIntegrationResult Success(
        M01LivePagingEvidence evidence,
        M01LivePagingStaticState staticState)
    {
        return new M01LivePagingIntegrationResult(
            M01LivePagingStatus.Success,
            M01LivePagingCheckpoint.Completed,
            evidence,
            staticState);
    }

    internal static M01LivePagingIntegrationResult Fail(
        M01LivePagingStatus status,
        M01LivePagingCheckpoint checkpoint,
        M01LivePagingEvidence evidence)
    {
        return new M01LivePagingIntegrationResult(
            status,
            checkpoint,
            evidence,
            default);
    }
}

/// <summary>
/// Third M01 host composition boundary. It writes only caller-owned byte and
/// flat table-image buffers and a final POD state record. It never calls
/// firmware, dereferences a reported physical address, invokes generated
/// code, writes CR0/CR3/CR4/RFLAGS, changes a processor PTE, executes INVLPG,
/// calls the protection probe, or dereferences virtual address zero.
/// </summary>
public static unsafe class M01LivePagingIntegration
{
    private struct CoordinatorStorageSpan
    {
        internal ulong Start;
        internal ulong End;
    }

    private struct ArenaPlanState
    {
        internal AddressValueStatus AddressStatus;
        internal OS.Kernel.Memory.BootReservationTableStatus
            ReservationStatus;
        internal OS.Kernel.Boot.X64BootIdentityMappingStatus
            MappingStatus;
        internal OS.Kernel.Memory.X64PageTableEntryStatus
            PageTableEntryStatus;
        internal OS.Kernel.Memory.X64PageTableEncodingPreconditions
            PageTablePreconditions;
        internal BootMemoryRegionRecord AllocationRecord;
        internal OS.Kernel.Boot.X64BootIdentityMappingPlanResult
            MappingPlan;
        internal PhysicalAddress CandidatePhysicalBase;
        internal PhysicalAddress RootTable;
        internal PhysicalAddress StatePage;
        internal PhysicalAddress ArenaEndExclusive;
        internal ulong IterationCount;
        internal ulong IterationLimit;
        internal ulong TablePageCount;
        internal ulong TotalPageCount;
        internal ulong TableByteLength;
        internal ulong TotalByteLength;
        internal ulong AvailablePageCountFromCandidate;
        internal ulong RequiredRangeWorkspaceCount;
        internal OS.Kernel.Boot.X64BootIdentityMappingRequirement
            BootStackGuardRequirement;
        internal OS.Kernel.Boot.X64BootIdentityMappingRequirement
            CodeAndCallGateRequirement;
        internal OS.Kernel.Boot.X64BootIdentityMappingRequirement
            OwnedStackRequirement;
        internal OS.Kernel.Boot.X64BootIdentityMappingRequirement
            ProtectionProbeRequirement;
    }

    private struct ArenaMaterializationState
    {
        internal OS.Kernel.Memory.X64PageTableEntryStatus
            PageTableEntryStatus;
        internal OS.Kernel.Memory.X64PageMapStatus PageImageStatus;
        internal PhysicalAddress RootTable;
        internal ulong TablePageCount;
        internal ulong VerifiedPageCount;
        internal ulong EntryCount;
        internal ulong VerifiedEntryCount;
        internal ulong InvalidPageOrdinal;
        internal ulong InvalidEntryIndex;
        internal ulong ExpectedEntry;
        internal ulong ObservedEntry;
        internal ulong SourceFingerprint;
        internal ulong ObservedFingerprint;
        internal ulong StateFingerprintBefore;
        internal ulong StateFingerprintAfter;
        internal ulong BytesWritten;
        internal ulong ReachablePageCount;
        internal ulong MappedLeafCount;
        internal ulong VerifiedStateByteCount;
        internal ulong InvalidStateByteIndex;
    }

    public const bool CallsFirmwarePageAllocator = false;
    public const bool PerformsFirmwareAllocation = false;
    public const bool PerformsFirmwareCleanup = false;
    public const bool InvokesFirmware = false;
    public const bool InvokesFirmwareCallbacks = false;
    public const bool ExecutesPrivilegedInstructions = false;
    public const bool ExecutesGeneratedCode = false;
    public const bool PerformsLiveCr3Load = false;
    public const bool PerformsLivePageProtection = false;
    public const bool PerformsLiveTlbInvalidation = false;
    public const bool ReadsInstalledPageTables = false;
    public const bool ProvesInstalledRoot = false;
    public const bool DereferencesPageZero = false;
    public const bool IsGuestEvidence = false;
    public const bool ProvesCompositeOwnedCr3MachineCodeInterval = false;
    public const bool ModelsFutureCompositeOwnedCr3Sequence = false;
    public const bool ComposesSeparateFutureCompositeLeaves = true;
    public const bool RestoresCallerOwnedInitialTableImage = false;
    public const bool RestoresModeledProbeLeafToInitialProtection = true;
    public const bool ProvesOwnedSerialDiagnostics = false;
    public const bool ProvesAcpiOrFramebufferSourceProvenance = false;
    public const bool EnforcesPerSectionLoadedImageProtections = false;
    public const bool CrossBindsStageOneSwitchStub = true;
    public const bool PreparationCrossBindsStageOneSwitchStub = false;
    public const bool CrossBindsOwnedSerialStub = false;
    public const bool SerialPurposeIsCompatibilityAliasOnly = true;
    public const bool CrossBindsOwnedMapIdentity = false;
    public const bool CrossBindsDiagnosticState = false;
    public const bool SupportsPcide = false;
    public const bool SupportsFiveLevelPaging = false;
    public const bool EliminatesNmiOrSmiRisk = false;
    public const bool LegacyExecuteIsModelOnly = true;
    public const bool ExecuteModelOnlyIsModelOnly = true;
    public const bool LegacyResultsCanPrepareCompositeTransition = false;
    public const bool PreparationExecutesGeneratedCode = false;
    public const bool PreparationExecutesPrivilegedInstructions = false;
    public const bool PreparationInvokesFirmware = false;
    public const bool PreparationIsGuestEvidence = false;
    public const bool PreparationUsesExactLiveIntervalBindings = true;
    public const bool PreparationRequiresHistoricalIdentityAliases =
        false;
    public const bool ArenaPlannerInvokesFirmware = false;
    public const bool ArenaMaterializerInvokesFirmware = false;
    public const bool ArenaMaterializerAllocatesFirmwarePages = false;
    public const bool ArenaMaterializerLoadsCr3 = false;
    public const bool ArenaMaterializerChangesPageProtection = false;
    public const bool ArenaMaterializerExecutesGeneratedCode = false;
    public const bool ArenaMaterializerIsGuestEvidence = false;
    public const bool ArenaMaterializerRequiresStateSnapshot = true;
    public const bool ArenaMaterializerUsesExactStateComparison =
        true;

    public const ulong RequiredAllocationRecordCount = 3;
    public const ulong MaximumAllocationRecordCapacity = 16;
    public const ulong Cr3CodeAllocationRecordIndex = 0;
    public const ulong ProtectionProbeAllocationRecordIndex = 1;
    public const ulong TableStateAllocationRecordIndex = 2;
    public const ulong RequiredCodePageCount = 1;
    public const ulong RequiredProbePageCount = 1;
    public const ulong RequiredNonImageMappingRequirementCount = 10;
    public const ulong RequiredLiveIntervalMappingRequirementCount = 7;
    public const ulong RequiredPointerAlignment = 8;
    public const ulong PageSize = 4096;
    public const ulong PreflightEntryOffset = 0;
    public const ulong TransitionEntryOffset = 256;
    public const ulong InvlpgEntryOffset = 2240;
    public const ulong ProtectionProbeEntryOffset = 0;
    public const ulong NoIndex = ~0UL;
    public const ulong RequiredArenaFixedRequirementCount = 4;
    public const ulong RequiredArenaRequirementWorkspaceCount = 7;
    public const ulong RequiredArenaStatePageCount = 1;
    public const ulong MinimumArenaTotalPageCount = 5;
    public const ulong MaximumArenaIterationCount = 65;
    public const ulong MaximumArenaTotalPageCount = 65;
    public const ulong MaximumArenaTotalByteLength =
        MaximumArenaTotalPageCount * PageSize;
    public const ulong RequiredArenaStateSnapshotByteCount =
        PageSize;

    private const ulong MaximumUnsignedLong = ~0UL;
    private const ulong RflagsDirectionFlagBit = 1UL << 10;

    public static M01LivePagingCleanupPlan PlanCleanup(
        BootMemoryRegionRecord* allocationRecords,
        ulong allocationRecordCount,
        ulong allocationRecordCapacity,
        ulong completedAllocationCount,
        bool firmwareServicesAvailable,
        bool ownedRootWasEntered,
        bool ownedRootRestored)
    {
        if (completedAllocationCount > RequiredAllocationRecordCount
            || allocationRecordCount
                < completedAllocationCount
            || allocationRecordCount > allocationRecordCapacity
            || allocationRecordCapacity
                > MaximumAllocationRecordCapacity)
        {
            return CleanupFailure(
                M01LivePagingCleanupStatus.InvalidCompletedCount,
                completedAllocationCount);
        }

        if (completedAllocationCount == 0)
        {
            return new M01LivePagingCleanupPlan(
                M01LivePagingCleanupStatus.Success,
                0,
                0,
                M01LivePagingCleanupPlan.NoIndex,
                M01LivePagingCleanupPlan.NoIndex,
                M01LivePagingCleanupPlan.NoIndex);
        }

        if (allocationRecords == null
            || (((ulong)allocationRecords)
                & (RequiredPointerAlignment - 1)) != 0
            || !TryGetSpanEnd(
                (ulong)allocationRecords,
                allocationRecordCapacity,
                (ulong)sizeof(BootMemoryRegionRecord),
                out ulong ignoredEnd))
        {
            return CleanupFailure(
                M01LivePagingCleanupStatus
                    .InvalidAllocationStorage,
                completedAllocationCount);
        }

        for (ulong index = 0;
            index < completedAllocationCount;
            index++)
        {
            if (!ValidateAllocationForIndex(
                    allocationRecords[index],
                    index,
                    requireExactTablePages: false,
                    expectedTablePages: 0))
            {
                return CleanupFailure(
                    M01LivePagingCleanupStatus
                        .InvalidAllocationRecord,
                    completedAllocationCount);
            }

            for (ulong priorIndex = 0;
                priorIndex < index;
                priorIndex++)
            {
                if (AllocationsOverlap(
                        allocationRecords[priorIndex],
                        allocationRecords[index]))
                {
                    return CleanupFailure(
                        M01LivePagingCleanupStatus
                            .InvalidAllocationRecord,
                        completedAllocationCount);
                }
            }
        }

        if (!firmwareServicesAvailable)
        {
            return CleanupFailure(
                M01LivePagingCleanupStatus
                    .FirmwareServicesUnavailable,
                completedAllocationCount);
        }

        if (ownedRootWasEntered && !ownedRootRestored)
        {
            return CleanupFailure(
                M01LivePagingCleanupStatus.OwnedRootNotRestored,
                completedAllocationCount);
        }

        return new M01LivePagingCleanupPlan(
            M01LivePagingCleanupStatus.Success,
            completedAllocationCount,
            completedAllocationCount,
            completedAllocationCount - 1,
            completedAllocationCount >= 2
                ? completedAllocationCount - 2
                : M01LivePagingCleanupPlan.NoIndex,
            completedAllocationCount == 3
                ? 0
                : M01LivePagingCleanupPlan.NoIndex);
    }

    /// <summary>
    /// Computes the exact table-page plus state-page arena needed by the
    /// supplied four live requirements. This bounded planner writes only its
    /// caller-owned scratch workspaces and never allocates firmware pages.
    /// </summary>
    public static M01LivePagingArenaPlanResult PlanOwnedTableArena(
        OS.Kernel.Boot.X64BootIdentityMappingRequirement*
            fixedRequirements,
        ulong fixedRequirementCount,
        ulong fixedRequirementCapacity,
        BootMemoryRegionRecord conventionalDescriptor,
        OS.Kernel.Memory.BootReservedRange* reservations,
        ulong reservationCount,
        ulong reservationCapacity,
        PhysicalAddress candidatePhysicalBase,
        OS.Kernel.Memory.X64PageTableEncodingPreconditions
            pageTablePreconditions,
        ulong iterationLimit,
        OS.Kernel.Boot.X64BootIdentityMappingRequirement*
            requirementWorkspace,
        ulong requirementWorkspaceCount,
        ulong requirementWorkspaceCapacity,
        OS.Kernel.Boot.X64BootIdentityMappingRange* rangeWorkspace,
        ulong rangeWorkspaceCount,
        ulong rangeWorkspaceCapacity)
    {
        ArenaPlanState state = default;
        state.PageTablePreconditions = pageTablePreconditions;
        state.CandidatePhysicalBase = candidatePhysicalBase;
        state.IterationLimit = iterationLimit;
        state.AddressStatus = AddressValueStatus.Uninitialized;
        state.ReservationStatus =
            OS.Kernel.Memory.BootReservationTableStatus
                .Uninitialized;
        state.MappingStatus =
            OS.Kernel.Boot.X64BootIdentityMappingStatus
                .Uninitialized;
        state.PageTableEntryStatus =
            OS.Kernel.Memory.X64PageTableEntryStatus
                .Uninitialized;

        if ((ulong)sizeof(M01LivePagingArenaPlanResult)
                != M01LivePagingArenaPlanResult.StructureSize
            || (ulong)sizeof(
                    M01LivePagingArenaMaterializationResult)
                != M01LivePagingArenaMaterializationResult
                    .StructureSize
            || (ulong)sizeof(BootMemoryRegionRecord) != 56
            || (ulong)sizeof(PhysicalAddress) != 8
            || (ulong)sizeof(
                    OS.Kernel.Memory.BootReservedRange)
                != 40
            || (ulong)sizeof(
                    OS.Kernel.Boot
                        .X64BootIdentityMappingRequirement)
                != OS.Kernel.Boot
                    .X64BootIdentityMappingRequirement
                    .StructureSize
            || (ulong)sizeof(
                    OS.Kernel.Boot
                        .X64BootIdentityMappingRange)
                != OS.Kernel.Boot
                    .X64BootIdentityMappingRange
                    .StructureSize
            || (ulong)sizeof(
                    OS.Kernel.Boot
                        .X64BootIdentityMappingPlanResult)
                != OS.Kernel.Boot
                    .X64BootIdentityMappingPlanResult
                    .StructureSize
            || (ulong)sizeof(
                    OS.Kernel.Memory
                        .X64PageTableEncodingPreconditions)
                != OS.Kernel.Memory
                    .X64PageTableEncodingPreconditions
                    .StructureSize)
        {
            return ArenaPlanFailure(
                M01LivePagingArenaPlanStatus
                    .ContractLayoutMismatch,
                state);
        }

        if (fixedRequirementCount
            != RequiredArenaFixedRequirementCount)
        {
            return ArenaPlanFailure(
                M01LivePagingArenaPlanStatus
                    .InvalidFixedRequirementCount,
                state);
        }

        if (fixedRequirementCapacity < fixedRequirementCount)
        {
            return ArenaPlanFailure(
                M01LivePagingArenaPlanStatus
                    .InvalidFixedRequirementCapacity,
                state);
        }

        if (fixedRequirementCapacity
            > OS.Kernel.Boot.X64BootIdentityMappingPlan
                .MaximumSourceCount)
        {
            return ArenaPlanFailure(
                M01LivePagingArenaPlanStatus.CapacityExceedsLimit,
                state);
        }

        if (fixedRequirements == null)
        {
            return ArenaPlanFailure(
                M01LivePagingArenaPlanStatus
                    .MissingFixedRequirements,
                state);
        }

        ulong fixedStart = (ulong)fixedRequirements;
        if ((fixedStart & (RequiredPointerAlignment - 1)) != 0)
        {
            state.AddressStatus =
                AddressValueStatus.MisalignedAddress;
            return ArenaPlanFailure(
                M01LivePagingArenaPlanStatus
                    .MisalignedFixedRequirements,
                state);
        }

        if (!TryGetSpanEnd(
                fixedStart,
                fixedRequirementCapacity,
                (ulong)sizeof(
                    OS.Kernel.Boot
                        .X64BootIdentityMappingRequirement),
                out ulong fixedEnd))
        {
            state.AddressStatus =
                AddressValueStatus.AddressOverflow;
            return ArenaPlanFailure(
                M01LivePagingArenaPlanStatus
                    .FixedRequirementSpanOverflow,
                state);
        }

        if (!TryValidateConventionalDescriptor(
                conventionalDescriptor,
                out ulong descriptorStart,
                out ulong descriptorEnd))
        {
            state.AddressStatus =
                AddressValueStatus.InvalidAddress;
            return ArenaPlanFailure(
                M01LivePagingArenaPlanStatus.InvalidDescriptor,
                state);
        }

        state.AddressStatus =
            candidatePhysicalBase
                .TryGetRawValueForUefiBoundary(
                    out ulong candidateStart);
        if (state.AddressStatus != AddressValueStatus.Success
            || !candidatePhysicalBase.IsAligned(PageSize)
            || candidateStart < descriptorStart
            || candidateStart >= descriptorEnd)
        {
            if (state.AddressStatus == AddressValueStatus.Success)
            {
                state.AddressStatus =
                    (candidateStart & (PageSize - 1)) != 0
                        ? AddressValueStatus.MisalignedAddress
                        : AddressValueStatus.InvalidAddress;
            }
            return ArenaPlanFailure(
                M01LivePagingArenaPlanStatus.InvalidCandidate,
                state);
        }

        state.AvailablePageCountFromCandidate =
            (descriptorEnd - candidateStart) / PageSize;

        state.PageTableEntryStatus =
            pageTablePreconditions.Validate();
        if (state.PageTableEntryStatus
            != OS.Kernel.Memory.X64PageTableEntryStatus.Success)
        {
            return ArenaPlanFailure(
                M01LivePagingArenaPlanStatus
                    .InvalidPreconditions,
                state);
        }

        if (iterationLimit == 0
            || iterationLimit > MaximumArenaIterationCount)
        {
            return ArenaPlanFailure(
                M01LivePagingArenaPlanStatus
                    .InvalidIterationLimit,
                state);
        }

        if (reservationCount == 0
            || reservationCount > reservationCapacity)
        {
            return ArenaPlanFailure(
                M01LivePagingArenaPlanStatus
                    .InvalidReservationStorage,
                state);
        }

        if (reservationCapacity
            > OS.Kernel.Memory.BootReservationTableValidator
                .MaximumReservationCount)
        {
            return ArenaPlanFailure(
                M01LivePagingArenaPlanStatus.CapacityExceedsLimit,
                state);
        }

        if (reservations == null
            || (((ulong)reservations)
                & (RequiredPointerAlignment - 1)) != 0
            || !TryGetSpanEnd(
                (ulong)reservations,
                reservationCapacity,
                (ulong)sizeof(
                    OS.Kernel.Memory.BootReservedRange),
                out ulong reservationEnd))
        {
            state.AddressStatus = reservations != null
                    && (((ulong)reservations)
                        & (RequiredPointerAlignment - 1)) != 0
                ? AddressValueStatus.MisalignedAddress
                : AddressValueStatus.AddressOverflow;
            return ArenaPlanFailure(
                M01LivePagingArenaPlanStatus
                    .InvalidReservationStorage,
                state);
        }
        ulong reservationStart = (ulong)reservations;

        OS.Kernel.Memory.BootReservationTableValidationResult
            reservationValidation =
                OS.Kernel.Memory.BootReservationTableValidator
                    .Validate(
                        reservations,
                        reservationCount,
                        reservationCapacity);
        state.ReservationStatus = reservationValidation.Status;
        if (!reservationValidation.Succeeded)
        {
            return ArenaPlanFailure(
                M01LivePagingArenaPlanStatus
                    .ReservationValidationFailed,
                state);
        }

        if (requirementWorkspaceCount != 0
            || requirementWorkspaceCapacity
                < RequiredArenaRequirementWorkspaceCount)
        {
            return ArenaPlanFailure(
                M01LivePagingArenaPlanStatus
                    .InvalidRequirementWorkspace,
                state);
        }

        if (requirementWorkspaceCapacity
            > OS.Kernel.Boot.X64BootIdentityMappingPlan
                .MaximumSourceCount)
        {
            return ArenaPlanFailure(
                M01LivePagingArenaPlanStatus.CapacityExceedsLimit,
                state);
        }

        if (requirementWorkspace == null
            || (((ulong)requirementWorkspace)
                & (RequiredPointerAlignment - 1)) != 0
            || !TryGetSpanEnd(
                (ulong)requirementWorkspace,
                requirementWorkspaceCapacity,
                (ulong)sizeof(
                    OS.Kernel.Boot
                        .X64BootIdentityMappingRequirement),
                out ulong requirementWorkspaceEnd))
        {
            state.AddressStatus =
                requirementWorkspace != null
                    && (((ulong)requirementWorkspace)
                        & (RequiredPointerAlignment - 1)) != 0
                ? AddressValueStatus.MisalignedAddress
                : AddressValueStatus.AddressOverflow;
            return ArenaPlanFailure(
                M01LivePagingArenaPlanStatus
                    .InvalidRequirementWorkspace,
                state);
        }
        ulong requirementWorkspaceStart =
            (ulong)requirementWorkspace;

        if (rangeWorkspaceCount != 0
            || rangeWorkspaceCapacity
                < RequiredArenaRequirementWorkspaceCount)
        {
            return ArenaPlanFailure(
                M01LivePagingArenaPlanStatus.InvalidRangeWorkspace,
                state);
        }

        if (rangeWorkspaceCapacity
            > OS.Kernel.Boot.X64BootIdentityMappingPlan
                .MaximumDestinationCount)
        {
            return ArenaPlanFailure(
                M01LivePagingArenaPlanStatus.CapacityExceedsLimit,
                state);
        }

        if (rangeWorkspace == null
            || (((ulong)rangeWorkspace)
                & (RequiredPointerAlignment - 1)) != 0
            || !TryGetSpanEnd(
                (ulong)rangeWorkspace,
                rangeWorkspaceCapacity,
                (ulong)sizeof(
                    OS.Kernel.Boot.X64BootIdentityMappingRange),
                out ulong rangeWorkspaceEnd))
        {
            state.AddressStatus = rangeWorkspace != null
                    && (((ulong)rangeWorkspace)
                        & (RequiredPointerAlignment - 1)) != 0
                ? AddressValueStatus.MisalignedAddress
                : AddressValueStatus.AddressOverflow;
            return ArenaPlanFailure(
                M01LivePagingArenaPlanStatus.InvalidRangeWorkspace,
                state);
        }
        ulong rangeWorkspaceStart = (ulong)rangeWorkspace;

        if (!AreDisjoint(
                fixedStart,
                fixedEnd,
                reservationStart,
                reservationEnd)
            || !AreDisjoint(
                fixedStart,
                fixedEnd,
                requirementWorkspaceStart,
                requirementWorkspaceEnd)
            || !AreDisjoint(
                fixedStart,
                fixedEnd,
                rangeWorkspaceStart,
                rangeWorkspaceEnd)
            || !AreDisjoint(
                reservationStart,
                reservationEnd,
                requirementWorkspaceStart,
                requirementWorkspaceEnd)
            || !AreDisjoint(
                reservationStart,
                reservationEnd,
                rangeWorkspaceStart,
                rangeWorkspaceEnd)
            || !AreDisjoint(
                requirementWorkspaceStart,
                requirementWorkspaceEnd,
                rangeWorkspaceStart,
                rangeWorkspaceEnd))
        {
            return ArenaPlanFailure(
                M01LivePagingArenaPlanStatus.WorkspaceOverlap,
                state);
        }

        OS.Kernel.Boot.X64BootIdentityMappingRequirement
            bootStackGuard = default;
        OS.Kernel.Boot.X64BootIdentityMappingRequirement
            codeAndCallGate = default;
        OS.Kernel.Boot.X64BootIdentityMappingRequirement
            ownedStack = default;
        OS.Kernel.Boot.X64BootIdentityMappingRequirement
            protectionProbe = default;
        byte observedFixedPurposeMask = 0;
        for (ulong index = 0;
            index < fixedRequirementCount;
            index++)
        {
            OS.Kernel.Boot.X64BootIdentityMappingRequirement
                requirement = fixedRequirements[index];
            byte purposeBit;
            switch (requirement.Purpose)
            {
                case OS.Kernel.Boot.X64BootIdentityMappingPurpose
                    .BootStackGuard:
                    purposeBit = 1;
                    bootStackGuard = requirement;
                    break;
                case OS.Kernel.Boot.X64BootIdentityMappingPurpose
                    .CompositeCodeAndCallGate:
                    purposeBit = 2;
                    codeAndCallGate = requirement;
                    break;
                case OS.Kernel.Boot.X64BootIdentityMappingPurpose
                    .CompositeOwnedStack:
                    purposeBit = 4;
                    ownedStack = requirement;
                    break;
                case OS.Kernel.Boot.X64BootIdentityMappingPurpose
                    .ProtectionProbePage:
                    purposeBit = 8;
                    protectionProbe = requirement;
                    break;
                default:
                    return ArenaPlanFailure(
                        M01LivePagingArenaPlanStatus
                            .InvalidFixedRequirementSet,
                        state);
            }

            if (!requirement.IsInitialized
                || (observedFixedPurposeMask & purposeBit) != 0)
            {
                return ArenaPlanFailure(
                    M01LivePagingArenaPlanStatus
                        .InvalidFixedRequirementSet,
                    state);
            }
            observedFixedPurposeMask |= purposeBit;
        }

        if (observedFixedPurposeMask != 0x0f)
        {
            return ArenaPlanFailure(
                M01LivePagingArenaPlanStatus
                    .InvalidFixedRequirementSet,
                state);
        }

        requirementWorkspace[0] = bootStackGuard;
        requirementWorkspace[1] = codeAndCallGate;
        requirementWorkspace[2] = ownedStack;
        requirementWorkspace[3] = protectionProbe;
        state.BootStackGuardRequirement = bootStackGuard;
        state.CodeAndCallGateRequirement = codeAndCallGate;
        state.OwnedStackRequirement = ownedStack;
        state.ProtectionProbeRequirement = protectionProbe;

        ulong currentTotalPageCount =
            MinimumArenaTotalPageCount;
        bool converged = false;
        for (ulong iteration = 1;
            iteration <= iterationLimit;
            iteration++)
        {
            state.IterationCount = iteration;
            if (!TryWriteArenaRequirements(
                    requirementWorkspace,
                    candidatePhysicalBase,
                    currentTotalPageCount,
                    out state.RootTable,
                    out state.StatePage,
                    out state.ArenaEndExclusive,
                    out state.AddressStatus))
            {
                return ArenaPlanFailure(
                    M01LivePagingArenaPlanStatus
                        .RequirementConstructionFailed,
                    state);
            }

            state.MappingPlan =
                OS.Kernel.Boot.X64BootIdentityMappingPlan
                    .CreateLiveInterval(
                        requirementWorkspace,
                        RequiredArenaRequirementWorkspaceCount,
                        RequiredArenaRequirementWorkspaceCount,
                        rangeWorkspace,
                        RequiredArenaRequirementWorkspaceCount,
                        OS.Kernel.Memory.X64PageMap
                            .MaximumPageImages,
                        pageTablePreconditions);
            state.MappingStatus = state.MappingPlan.Status;
            state.AddressStatus =
                state.MappingPlan.AddressStatus;
            state.PageTableEntryStatus =
                state.MappingPlan.PageTableEntryStatus;
            state.RequiredRangeWorkspaceCount =
                state.MappingPlan.RequiredDestinationCapacity;

            if (!state.MappingPlan.Succeeded)
            {
                M01LivePagingArenaPlanStatus failureStatus =
                    state.MappingPlan.Status
                        == OS.Kernel.Boot
                            .X64BootIdentityMappingStatus
                            .DestinationCapacityTooSmall
                    ? M01LivePagingArenaPlanStatus
                        .RangeWorkspaceTooSmall
                    : state.MappingPlan.Status
                        == OS.Kernel.Boot
                            .X64BootIdentityMappingStatus
                            .TableCapacityTooSmall
                        ? M01LivePagingArenaPlanStatus
                            .TableCapacityExceeded
                        : state.MappingPlan.Status
                            == OS.Kernel.Boot
                                .X64BootIdentityMappingStatus
                                .PhysicalAddressOutsideWidth
                            ? M01LivePagingArenaPlanStatus
                                .PhysicalWidthExceeded
                        : M01LivePagingArenaPlanStatus
                            .IdentityPlanRejected;
                return ArenaPlanFailure(
                    failureStatus,
                    state);
            }

            ulong tablePageCount =
                state.MappingPlan.TotalTablePageCount;
            if (tablePageCount == 0
                || tablePageCount
                    > OS.Kernel.Memory.X64PageMap
                        .MaximumPageImages
                || tablePageCount
                    > MaximumUnsignedLong
                        - RequiredArenaStatePageCount)
            {
                return ArenaPlanFailure(
                    M01LivePagingArenaPlanStatus
                        .TablePageCountOverflow,
                    state);
            }

            ulong nextTotalPageCount =
                tablePageCount + RequiredArenaStatePageCount;
            if (nextTotalPageCount
                > MaximumArenaTotalPageCount)
            {
                return ArenaPlanFailure(
                    M01LivePagingArenaPlanStatus
                        .TableCapacityExceeded,
                    state);
            }

            if (nextTotalPageCount < currentTotalPageCount)
            {
                return ArenaPlanFailure(
                    M01LivePagingArenaPlanStatus
                        .NonMonotonicIteration,
                    state);
            }

            if (nextTotalPageCount == currentTotalPageCount)
            {
                state.TablePageCount = tablePageCount;
                state.TotalPageCount = nextTotalPageCount;
                converged = true;
                break;
            }
            currentTotalPageCount = nextTotalPageCount;
        }

        if (!converged)
        {
            return ArenaPlanFailure(
                M01LivePagingArenaPlanStatus
                    .IterationLimitReached,
                state);
        }

        if (state.TablePageCount
                > MaximumUnsignedLong / PageSize
            || state.TotalPageCount
                > MaximumUnsignedLong / PageSize)
        {
            return ArenaPlanFailure(
                M01LivePagingArenaPlanStatus
                    .ArenaGeometryOverflow,
                state);
        }

        state.TableByteLength =
            state.TablePageCount * PageSize;
        state.TotalByteLength =
            state.TotalPageCount * PageSize;

        if (state.TotalByteLength
                > MaximumArenaTotalByteLength
            || candidateStart
                > MaximumUnsignedLong - state.TotalByteLength
            || candidateStart + state.TotalByteLength
                != Raw(state.ArenaEndExclusive))
        {
            state.AddressStatus =
                AddressValueStatus.AddressOverflow;
            return ArenaPlanFailure(
                M01LivePagingArenaPlanStatus
                    .ArenaGeometryOverflow,
                state);
        }

        ulong arenaEnd =
            candidateStart + state.TotalByteLength;
        if (arenaEnd > descriptorEnd
            || state.TotalPageCount
                > state.AvailablePageCountFromCandidate)
        {
            return ArenaPlanFailure(
                M01LivePagingArenaPlanStatus
                    .DescriptorCapacityTooSmall,
                state);
        }

        ulong physicalLimit =
            1UL << pageTablePreconditions
                .PhysicalAddressWidth;
        if (arenaEnd > physicalLimit)
        {
            state.PageTableEntryStatus =
                OS.Kernel.Memory.X64PageTableEntryStatus
                    .PhysicalAddressOutsideWidth;
            return ArenaPlanFailure(
                M01LivePagingArenaPlanStatus
                    .PhysicalWidthExceeded,
                state);
        }

        for (ulong index = 0;
            index < reservationCount;
            index++)
        {
            ulong reservationRangeStart =
                Raw(reservations[index].PhysicalStart);
            ulong reservationRangeEnd =
                Raw(reservations[index].PhysicalEndExclusive);
            if (!AreDisjoint(
                    candidateStart,
                    arenaEnd,
                    reservationRangeStart,
                    reservationRangeEnd))
            {
                return ArenaPlanFailure(
                    M01LivePagingArenaPlanStatus
                        .ReservationOverlap,
                    state);
            }
        }

        state.AllocationRecord =
            new BootMemoryRegionRecord(
                (uint)UefiMemoryType.LoaderData,
                candidateStart,
                candidateStart,
                state.TotalPageCount,
                conventionalDescriptor.Attribute,
                state.TotalByteLength,
                arenaEnd);
        state.AddressStatus = AddressValueStatus.Success;
        state.ReservationStatus =
            OS.Kernel.Memory.BootReservationTableStatus.Success;
        state.MappingStatus =
            OS.Kernel.Boot.X64BootIdentityMappingStatus.Success;
        state.PageTableEntryStatus =
            OS.Kernel.Memory.X64PageTableEntryStatus.Success;
        return ArenaPlanSuccess(state);
    }

    /// <summary>
    /// Materializes or independently verifies the exact page-table image in
    /// caller-owned CPU-addressable backing storage. The backing pointer is
    /// deliberately independent of the planned physical address.
    /// </summary>
    public static M01LivePagingArenaMaterializationResult
        MaterializeOwnedTableArena(
            M01LivePagingArenaPlanResult plan,
            BootMemoryRegionRecord allocationRecord,
            OS.Kernel.Memory.X64PageTableImagePage* pageImages,
            ulong pageImageCount,
            ulong pageImageCapacity,
            ulong* pageTableEntries,
            ulong pageTableEntryCount,
            ulong pageTableEntryCapacity,
            byte* arenaBacking,
            ulong arenaBackingByteCount,
            ulong arenaBackingByteCapacity,
            M01LivePagingArenaByteMode byteMode,
            byte* stateSnapshot,
            ulong stateSnapshotByteCount,
            ulong stateSnapshotByteCapacity)
    {
        ArenaMaterializationState state = default;
        state.RootTable = plan.RootTable;
        state.TablePageCount = plan.TablePageCount;
        state.EntryCount = pageTableEntryCount;
        state.InvalidPageOrdinal =
            M01LivePagingArenaMaterializationResult.NoIndex;
        state.InvalidEntryIndex =
            M01LivePagingArenaMaterializationResult.NoIndex;
        state.InvalidStateByteIndex =
            M01LivePagingArenaMaterializationResult.NoIndex;
        state.PageTableEntryStatus =
            OS.Kernel.Memory.X64PageTableEntryStatus
                .Uninitialized;
        state.PageImageStatus =
            OS.Kernel.Memory.X64PageMapStatus.Uninitialized;

        if ((ulong)sizeof(
                    M01LivePagingArenaMaterializationResult)
                != M01LivePagingArenaMaterializationResult
                    .StructureSize
            || (ulong)sizeof(M01LivePagingArenaPlanResult)
                != M01LivePagingArenaPlanResult.StructureSize
            || (ulong)sizeof(
                    OS.Kernel.Memory.X64PageTableImagePage)
                != OS.Kernel.Memory.X64PageTableImagePage
                    .StructureSize)
        {
            return ArenaMaterializationFailure(
                M01LivePagingArenaMaterializationStatus
                    .ContractLayoutMismatch,
                state);
        }

        if (byteMode
                != M01LivePagingArenaByteMode.EmitAndVerify
            && byteMode
                != M01LivePagingArenaByteMode.VerifyExisting)
        {
            return ArenaMaterializationFailure(
                M01LivePagingArenaMaterializationStatus
                    .InvalidByteMode,
                state);
        }

        if (!IsCanonicalArenaPlan(plan))
        {
            return ArenaMaterializationFailure(
                M01LivePagingArenaMaterializationStatus
                    .InvalidPlan,
                state);
        }
        state.PageTableEntryStatus =
            OS.Kernel.Memory.X64PageTableEntryStatus.Success;

        OS.Kernel.Boot.X64BootIdentityMappingRequirement*
            rederivedRequirements =
                stackalloc OS.Kernel.Boot
                    .X64BootIdentityMappingRequirement[
                        (int)RequiredArenaRequirementWorkspaceCount];
        OS.Kernel.Boot.X64BootIdentityMappingRange*
            rederivedRanges =
                stackalloc OS.Kernel.Boot
                    .X64BootIdentityMappingRange[
                        (int)RequiredArenaRequirementWorkspaceCount];
        if (!TryRederiveArenaMappingPlan(
                plan,
                rederivedRequirements,
                rederivedRanges,
                out OS.Kernel.Boot
                    .X64BootIdentityMappingPlanResult
                    rederivedMappingPlan)
            || !MappingPlansEqual(
                plan.MappingPlan,
                rederivedMappingPlan))
        {
            return ArenaMaterializationFailure(
                M01LivePagingArenaMaterializationStatus
                    .SemanticMappingMismatch,
                state);
        }

        if (!AllocationRecordsEqual(
                allocationRecord,
                plan.AllocationRecord))
        {
            return ArenaMaterializationFailure(
                M01LivePagingArenaMaterializationStatus
                    .AllocationRecordMismatch,
                state);
        }

        if (pageImageCount == 0
            || pageImageCount != plan.RequiredPageImageCount)
        {
            return ArenaMaterializationFailure(
                M01LivePagingArenaMaterializationStatus
                    .InvalidPageImageCount,
                state);
        }

        if (pageImageCapacity < pageImageCount)
        {
            return ArenaMaterializationFailure(
                M01LivePagingArenaMaterializationStatus
                    .PageImageCapacityTooSmall,
                state);
        }

        if (pageImageCapacity
            > OS.Kernel.Memory.X64PageMap.MaximumPageImages)
        {
            return ArenaMaterializationFailure(
                M01LivePagingArenaMaterializationStatus
                    .PageImageCapacityExceedsLimit,
                state);
        }

        if (pageImages == null)
        {
            return ArenaMaterializationFailure(
                M01LivePagingArenaMaterializationStatus
                    .MissingPageImages,
                state);
        }

        ulong pageImageStart = (ulong)pageImages;
        if ((pageImageStart
                & (RequiredPointerAlignment - 1)) != 0)
        {
            return ArenaMaterializationFailure(
                M01LivePagingArenaMaterializationStatus
                    .MisalignedPageImages,
                state);
        }

        if (!TryGetSpanEnd(
                pageImageStart,
                pageImageCapacity,
                (ulong)sizeof(
                    OS.Kernel.Memory.X64PageTableImagePage),
                out ulong pageImageEnd))
        {
            return ArenaMaterializationFailure(
                M01LivePagingArenaMaterializationStatus
                    .PageImageSpanOverflow,
                state);
        }

        if (pageTableEntryCount == 0)
        {
            return ArenaMaterializationFailure(
                M01LivePagingArenaMaterializationStatus
                    .InvalidEntryCount,
                state);
        }

        if (pageTableEntryCount
            != plan.RequiredPageTableEntryCount)
        {
            return ArenaMaterializationFailure(
                M01LivePagingArenaMaterializationStatus
                    .EntryCountMismatch,
                state);
        }

        if (pageTableEntryCapacity < pageTableEntryCount)
        {
            return ArenaMaterializationFailure(
                M01LivePagingArenaMaterializationStatus
                    .EntryCapacityTooSmall,
                state);
        }

        if (pageTableEntryCapacity
            > OS.Kernel.Memory.X64PageMap.MaximumEntryCount)
        {
            return ArenaMaterializationFailure(
                M01LivePagingArenaMaterializationStatus
                    .EntryCapacityExceedsLimit,
                state);
        }

        if (pageTableEntries == null)
        {
            return ArenaMaterializationFailure(
                M01LivePagingArenaMaterializationStatus
                    .MissingEntries,
                state);
        }

        ulong pageTableEntryStart = (ulong)pageTableEntries;
        if ((pageTableEntryStart
                & (RequiredPointerAlignment - 1)) != 0)
        {
            return ArenaMaterializationFailure(
                M01LivePagingArenaMaterializationStatus
                    .MisalignedEntries,
                state);
        }

        if (!TryGetSpanEnd(
                pageTableEntryStart,
                pageTableEntryCapacity,
                (ulong)sizeof(ulong),
                out ulong pageTableEntryEnd))
        {
            return ArenaMaterializationFailure(
                M01LivePagingArenaMaterializationStatus
                    .EntrySpanOverflow,
                state);
        }

        if (arenaBacking == null)
        {
            return ArenaMaterializationFailure(
                M01LivePagingArenaMaterializationStatus
                    .MissingArenaBacking,
                state);
        }

        if (arenaBackingByteCount != plan.TotalByteLength)
        {
            return ArenaMaterializationFailure(
                M01LivePagingArenaMaterializationStatus
                    .InvalidArenaByteCount,
                state);
        }

        if (arenaBackingByteCapacity < arenaBackingByteCount)
        {
            return ArenaMaterializationFailure(
                M01LivePagingArenaMaterializationStatus
                    .ArenaCapacityTooSmall,
                state);
        }

        if (arenaBackingByteCapacity
            > MaximumArenaTotalByteLength)
        {
            return ArenaMaterializationFailure(
                M01LivePagingArenaMaterializationStatus
                    .ArenaCapacityExceedsLimit,
                state);
        }

        ulong arenaBackingStart = (ulong)arenaBacking;
        if ((arenaBackingStart & (PageSize - 1)) != 0)
        {
            return ArenaMaterializationFailure(
                M01LivePagingArenaMaterializationStatus
                    .MisalignedArenaBacking,
                state);
        }

        if (!TryGetSpanEnd(
                arenaBackingStart,
                arenaBackingByteCapacity,
                1,
                out ulong arenaBackingEnd))
        {
            return ArenaMaterializationFailure(
                M01LivePagingArenaMaterializationStatus
                    .ArenaBackingSpanOverflow,
                state);
        }

        if (stateSnapshotByteCount
            != RequiredArenaStateSnapshotByteCount)
        {
            return ArenaMaterializationFailure(
                M01LivePagingArenaMaterializationStatus
                    .InvalidStateSnapshotByteCount,
                state);
        }

        if (stateSnapshotByteCapacity < stateSnapshotByteCount)
        {
            return ArenaMaterializationFailure(
                M01LivePagingArenaMaterializationStatus
                    .StateSnapshotCapacityTooSmall,
                state);
        }

        if (stateSnapshotByteCapacity
            > RequiredArenaStateSnapshotByteCount)
        {
            return ArenaMaterializationFailure(
                M01LivePagingArenaMaterializationStatus
                    .StateSnapshotCapacityExceedsLimit,
                state);
        }

        if (stateSnapshot == null)
        {
            return ArenaMaterializationFailure(
                M01LivePagingArenaMaterializationStatus
                    .MissingStateSnapshot,
                state);
        }

        ulong stateSnapshotStart = (ulong)stateSnapshot;
        if ((stateSnapshotStart
                & (RequiredPointerAlignment - 1)) != 0)
        {
            return ArenaMaterializationFailure(
                M01LivePagingArenaMaterializationStatus
                    .MisalignedStateSnapshot,
                state);
        }

        if (!TryGetSpanEnd(
                stateSnapshotStart,
                stateSnapshotByteCapacity,
                1,
                out ulong stateSnapshotEnd))
        {
            return ArenaMaterializationFailure(
                M01LivePagingArenaMaterializationStatus
                    .StateSnapshotSpanOverflow,
                state);
        }

        if (!AreDisjoint(
                pageImageStart,
                pageImageEnd,
                pageTableEntryStart,
                pageTableEntryEnd)
            || !AreDisjoint(
                pageImageStart,
                pageImageEnd,
                arenaBackingStart,
                arenaBackingEnd)
            || !AreDisjoint(
                pageTableEntryStart,
                pageTableEntryEnd,
                arenaBackingStart,
                arenaBackingEnd)
            || !AreDisjoint(
                stateSnapshotStart,
                stateSnapshotEnd,
                pageImageStart,
                pageImageEnd)
            || !AreDisjoint(
                stateSnapshotStart,
                stateSnapshotEnd,
                pageTableEntryStart,
                pageTableEntryEnd)
            || !AreDisjoint(
                stateSnapshotStart,
                stateSnapshotEnd,
                arenaBackingStart,
                arenaBackingEnd))
        {
            bool snapshotOverlap =
                !AreDisjoint(
                    stateSnapshotStart,
                    stateSnapshotEnd,
                    pageImageStart,
                    pageImageEnd)
                || !AreDisjoint(
                    stateSnapshotStart,
                    stateSnapshotEnd,
                    pageTableEntryStart,
                    pageTableEntryEnd)
                || !AreDisjoint(
                    stateSnapshotStart,
                    stateSnapshotEnd,
                    arenaBackingStart,
                    arenaBackingEnd);
            return ArenaMaterializationFailure(
                snapshotOverlap
                    ? M01LivePagingArenaMaterializationStatus
                        .StateSnapshotStorageOverlap
                    : M01LivePagingArenaMaterializationStatus
                        .StorageOverlap,
                state);
        }

        byte* statePageBacking =
            arenaBacking + plan.TableByteLength;

        ulong candidateStart = Raw(plan.CandidatePhysicalBase);
        ulong statePageStart = Raw(plan.StatePage);
        for (ulong ordinal = 0;
            ordinal < pageImageCount;
            ordinal++)
        {
            state.PageImageStatus =
                pageImages[ordinal].Validate(
                    ordinal,
                    pageTableEntryCount,
                    plan.PageTablePreconditions
                        .PhysicalAddressWidth);
            if (state.PageImageStatus
                != OS.Kernel.Memory.X64PageMapStatus.Success)
            {
                state.InvalidPageOrdinal = ordinal;
                return ArenaMaterializationFailure(
                    M01LivePagingArenaMaterializationStatus
                        .PageImageMalformed,
                    state);
            }

            ulong actualPhysical =
                Raw(pageImages[ordinal].PhysicalStart);
            if (actualPhysical == statePageStart)
            {
                state.InvalidPageOrdinal = ordinal;
                return ArenaMaterializationFailure(
                    M01LivePagingArenaMaterializationStatus
                        .PageImageTargetsStatePage,
                    state);
            }

            if (ordinal > MaximumUnsignedLong / PageSize
                || candidateStart
                    > MaximumUnsignedLong
                        - ordinal * PageSize)
            {
                state.InvalidPageOrdinal = ordinal;
                return ArenaMaterializationFailure(
                    M01LivePagingArenaMaterializationStatus
                        .PageImageOutsideTableArena,
                    state);
            }

            ulong expectedPhysical =
                candidateStart + ordinal * PageSize;
            if (actualPhysical < candidateStart
                || actualPhysical >= statePageStart)
            {
                state.InvalidPageOrdinal = ordinal;
                return ArenaMaterializationFailure(
                    M01LivePagingArenaMaterializationStatus
                        .PageImageOutsideTableArena,
                    state);
            }

            if (actualPhysical != expectedPhysical
                || (ordinal == 0
                    && !pageImages[ordinal]
                        .PhysicalStart.Equals(plan.RootTable)))
            {
                state.InvalidPageOrdinal = ordinal;
                return ArenaMaterializationFailure(
                    M01LivePagingArenaMaterializationStatus
                        .PageImageOrderInvalid,
                    state);
            }
        }
        state.PageImageStatus =
            OS.Kernel.Memory.X64PageMapStatus.Success;

        state.SourceFingerprint = Fnv1aArenaImage(
            pageImages,
            pageImageCount,
            pageTableEntries,
            useBackingEntries: false,
            arenaBacking);

        ulong visitedMask = 1;
        state.ReachablePageCount = 1;
        ulong* rangeLeafCounts =
            stackalloc ulong[
                (int)RequiredArenaRequirementWorkspaceCount];
        for (ulong index = 0;
            index < RequiredArenaRequirementWorkspaceCount;
            index++)
        {
            rangeLeafCounts[index] = 0;
        }
        M01LivePagingArenaMaterializationStatus auditStatus =
            AuditArenaHierarchyPage(
                pageImages,
                pageImageCount,
                pageTableEntries,
                pageTableEntryCount,
                plan.PageTablePreconditions,
                rederivedRanges,
                rederivedMappingPlan.OutputRangeCount,
                rangeLeafCounts,
                0,
                OS.Kernel.Memory.X64PageTableLevel.Pml4e,
                0,
                ref visitedMask,
                ref state);
        if (auditStatus
            != M01LivePagingArenaMaterializationStatus.Success)
        {
            return ArenaMaterializationFailure(
                auditStatus,
                state);
        }

        ulong expectedVisitedMask =
            pageImageCount == 64
                ? MaximumUnsignedLong
                : (1UL << (int)pageImageCount) - 1;
        if (visitedMask != expectedVisitedMask
            || state.ReachablePageCount != pageImageCount)
        {
            state.PageImageStatus =
                OS.Kernel.Memory.X64PageMapStatus
                    .PageImageOrderInvalid;
            return ArenaMaterializationFailure(
                M01LivePagingArenaMaterializationStatus
                    .UnreachablePageImage,
                state);
        }

        if (state.MappedLeafCount
            != plan.MappingPlan.UnionPageCount)
        {
            return ArenaMaterializationFailure(
                M01LivePagingArenaMaterializationStatus
                    .LeafCountMismatch,
                state);
        }

        ulong semanticRangePageCount = 0;
        for (ulong rangeIndex = 0;
            rangeIndex < rederivedMappingPlan.OutputRangeCount;
            rangeIndex++)
        {
            if (!rederivedRanges[rangeIndex].IsInitialized
                || rangeLeafCounts[rangeIndex]
                    != rederivedRanges[rangeIndex].PageCount
                || semanticRangePageCount
                    > MaximumUnsignedLong
                        - rangeLeafCounts[rangeIndex])
            {
                return ArenaMaterializationFailure(
                    M01LivePagingArenaMaterializationStatus
                        .SemanticMappingMismatch,
                    state);
            }
            semanticRangePageCount +=
                rangeLeafCounts[rangeIndex];
        }
        if (semanticRangePageCount
            != rederivedMappingPlan.UnionPageCount)
        {
            return ArenaMaterializationFailure(
                M01LivePagingArenaMaterializationStatus
                    .SemanticMappingMismatch,
                state);
        }

        for (ulong stateByteIndex = 0;
            stateByteIndex < RequiredArenaStateSnapshotByteCount;
            stateByteIndex++)
        {
            stateSnapshot[stateByteIndex] =
                statePageBacking[stateByteIndex];
        }
        state.StateFingerprintBefore =
            Fnv1aBytes(
                stateSnapshot,
                RequiredArenaStateSnapshotByteCount);
        state.StateFingerprintAfter =
            state.StateFingerprintBefore;

        if (byteMode
            == M01LivePagingArenaByteMode.EmitAndVerify)
        {
            for (ulong ordinal = 0;
                ordinal < pageImageCount;
                ordinal++)
            {
                ulong* destination =
                    (ulong*)(arenaBacking
                        + ordinal * PageSize);
                ulong sourceStart =
                    pageImages[ordinal].EntryStartIndex;
                for (ulong entryOrdinal = 0;
                    entryOrdinal
                        < OS.Kernel.Memory
                            .X64PageTableImagePage
                            .EntryCountPerPage;
                    entryOrdinal++)
                {
                    destination[entryOrdinal] =
                        pageTableEntries[
                            sourceStart + entryOrdinal];
                }
            }
            state.BytesWritten = plan.TableByteLength;
        }

        bool foundMismatch = false;
        for (ulong ordinal = 0;
            ordinal < pageImageCount;
            ordinal++)
        {
            ulong* observedPage =
                (ulong*)(arenaBacking + ordinal * PageSize);
            ulong sourceStart =
                pageImages[ordinal].EntryStartIndex;
            for (ulong entryOrdinal = 0;
                entryOrdinal
                    < OS.Kernel.Memory
                        .X64PageTableImagePage
                        .EntryCountPerPage;
                entryOrdinal++)
            {
                ulong sourceIndex =
                    sourceStart + entryOrdinal;
                ulong expected =
                    pageTableEntries[sourceIndex];
                ulong observed = observedPage[entryOrdinal];
                if (!foundMismatch && expected != observed)
                {
                    foundMismatch = true;
                    state.InvalidPageOrdinal = ordinal;
                    state.InvalidEntryIndex = sourceIndex;
                    state.ExpectedEntry = expected;
                    state.ObservedEntry = observed;
                }
                state.VerifiedEntryCount++;
            }
            state.VerifiedPageCount++;
        }

        state.ObservedFingerprint = Fnv1aArenaImage(
            pageImages,
            pageImageCount,
            pageTableEntries,
            useBackingEntries: true,
            arenaBacking);
        bool foundStateMismatch = false;
        for (ulong stateByteIndex = 0;
            stateByteIndex < RequiredArenaStateSnapshotByteCount;
            stateByteIndex++)
        {
            if (!foundStateMismatch
                && stateSnapshot[stateByteIndex]
                    != statePageBacking[stateByteIndex])
            {
                foundStateMismatch = true;
                state.InvalidStateByteIndex = stateByteIndex;
            }
            state.VerifiedStateByteCount++;
        }
        state.StateFingerprintAfter =
            Fnv1aBytes(statePageBacking, PageSize);
        if (foundStateMismatch
            || state.StateFingerprintBefore
                != state.StateFingerprintAfter)
        {
            return ArenaMaterializationFailure(
                M01LivePagingArenaMaterializationStatus
                    .StatePageChanged,
                state);
        }

        if (foundMismatch
            || state.SourceFingerprint
                != state.ObservedFingerprint)
        {
            return ArenaMaterializationFailure(
                M01LivePagingArenaMaterializationStatus
                    .EntryMismatch,
                state);
        }

        return ArenaMaterializationSuccess(state);
    }

    /// <summary>
    /// Validates and constructs caller-owned paging preparation without
    /// accepting or manufacturing an execution transcript. Architectural
    /// preflight is evaluated against the allocation-derived root and arena
    /// before any caller output buffer is written.
    /// </summary>
    public static M01LivePagingPreparationResult Prepare(
        M01LiveAllocatorStaticState* stageTwoState,
        ulong stageTwoStateCount,
        ulong stageTwoStateCapacity,
        OwnedBootStackSwitchPlanResult stackSwitchPlan,
        BootMemoryRegionRecord* allocationRecords,
        ulong allocationRecordCount,
        ulong allocationRecordCapacity,
        M01LivePagingAllocationEvidence allocationEvidence,
        M01LivePagingMapLedgerEvidence mapLedgerEvidence,
        OS.Kernel.Memory.BootReservedRange* reservationLedger,
        ulong reservationCount,
        ulong reservationCapacity,
        OS.Kernel.Boot.X64BootIdentityMappingRequirement*
            mappingRequirements,
        ulong mappingRequirementCount,
        ulong mappingRequirementCapacity,
        OS.Kernel.Boot.X64BootIdentityMappingRange* mappingRanges,
        ulong mappingRangeCount,
        ulong mappingRangeCapacity,
        OS.Kernel.Memory.X64PageTableImagePage* pageImages,
        ulong pageImageCount,
        ulong pageImageCapacity,
        ulong* pageTableEntries,
        ulong pageTableEntryCount,
        ulong pageTableEntryCapacity,
        OS.Kernel.Memory.X64PageTableEncodingPreconditions
            pageTablePreconditions,
        byte* cr3CodePage,
        ulong cr3CodePageCapacity,
        byte* protectionProbePage,
        ulong protectionProbePageCapacity,
        OS.Kernel.Memory.X64PagingModePreflightObservation*
            preflightObservation,
        ulong preflightObservationCount,
        ulong preflightObservationCapacity,
        M01LivePagingByteMode byteMode,
        M01LivePagingPreparationState* preparationState,
        ulong preparationStateCount,
        ulong preparationStateCapacity)
    {
        M01LivePagingPreparationEvidence evidence = default;
        evidence.AllocationEvidenceValue = allocationEvidence;
        evidence.MapLedgerEvidenceValue = mapLedgerEvidence;
        evidence.Cr3CodeReservationIndexValue = NoIndex;
        evidence.ProtectionProbeReservationIndexValue = NoIndex;
        evidence.TableStateReservationIndexValue = NoIndex;
        M01LivePagingCheckpoint checkpoint =
            M01LivePagingCheckpoint.Uninitialized;

        if (stageTwoStateCount != 1
            || stageTwoStateCapacity != 1
            || !ValidateStageTwoState(
                stageTwoState,
                M01LiveAllocatorStaticState.RequiredStorageBytes,
                out ulong stageTwoStart,
                out ulong stageTwoEnd)
            || !stackSwitchPlan.Succeeded)
        {
            return FailPreparation(
                M01LivePagingStatus.StageTwoRejected,
                checkpoint,
                evidence);
        }
        checkpoint = M01LivePagingCheckpoint.StageTwoValidated;

        M01LoadedImageStackStaticState* stageOne =
            (M01LoadedImageStackStaticState*)
                stageTwoState->StageOneStateAddress;
        if (!ValidateStageOneState(
                stageOne,
                stageTwoState->StageOneStateCapacity,
                stackSwitchPlan,
                out ulong stageOneStart,
                out ulong stageOneEnd))
        {
            return FailPreparation(
                M01LivePagingStatus.StageTwoRejected,
                checkpoint,
                evidence);
        }

        if (!ValidateAllocationStorage(
                allocationRecords,
                allocationRecordCount,
                allocationRecordCapacity,
                out ulong allocationTableStart,
                out ulong allocationTableEnd))
        {
            return FailPreparation(
                M01LivePagingStatus.AllocationTableRejected,
                checkpoint,
                evidence);
        }

        if (!allocationEvidence.IsValidFor(
                allocationRecords,
                allocationRecordCount,
                allocationRecordCapacity))
        {
            return FailPreparation(
                M01LivePagingStatus.AllocationEvidenceRejected,
                checkpoint,
                evidence);
        }

        BootMemoryRegionRecord code =
            allocationRecords[Cr3CodeAllocationRecordIndex];
        BootMemoryRegionRecord probe =
            allocationRecords[ProtectionProbeAllocationRecordIndex];
        BootMemoryRegionRecord tableState =
            allocationRecords[TableStateAllocationRecordIndex];
        if (!ValidateAllocationForIndex(
                code,
                Cr3CodeAllocationRecordIndex,
                requireExactTablePages: false,
                expectedTablePages: 0)
            || !ValidateAllocationForIndex(
                probe,
                ProtectionProbeAllocationRecordIndex,
                requireExactTablePages: false,
                expectedTablePages: 0)
            || !ValidateAllocationForIndex(
                tableState,
                TableStateAllocationRecordIndex,
                requireExactTablePages: false,
                expectedTablePages: 0)
            || tableState.ByteLength <= PageSize
            || AllocationsOverlap(code, probe)
            || AllocationsOverlap(code, tableState)
            || AllocationsOverlap(probe, tableState))
        {
            return FailPreparation(
                M01LivePagingStatus.AllocationTableRejected,
                checkpoint,
                evidence);
        }
        checkpoint = M01LivePagingCheckpoint.AllocationsValidated;

        bool mapLedgerValid = mapLedgerEvidence.IsValidFor(
            *stageTwoState,
            allocationEvidence,
            reservationLedger,
            reservationCount,
            reservationCapacity);
        if (!ValidateReservationStorage(
                reservationLedger,
                reservationCount,
                reservationCapacity,
                out ulong reservationTableStart,
                out ulong reservationTableEnd)
            || !mapLedgerValid)
        {
            return FailPreparation(
                mapLedgerValid
                    ? M01LivePagingStatus.ReservationLedgerRejected
                    : M01LivePagingStatus.MapLedgerEvidenceRejected,
                checkpoint,
                evidence);
        }
        checkpoint = M01LivePagingCheckpoint.MapLedgerValidated;

        if (!ValidatePreparationStateStorage(
                preparationState,
                preparationStateCount,
                preparationStateCapacity,
                out ulong preparationStateStart,
                out ulong preparationStateEnd))
        {
            return FailPreparation(
                M01LivePagingStatus.CoordinatorStateRejected,
                checkpoint,
                evidence);
        }

        if (!ValidatePreparationBufferTopology(
                stageOneStart,
                stageOneEnd,
                stageTwoStart,
                stageTwoEnd,
                allocationTableStart,
                allocationTableEnd,
                reservationTableStart,
                reservationTableEnd,
                preparationStateStart,
                preparationStateEnd,
                mapLedgerEvidence.RefreshedMap,
                mappingRequirements,
                mappingRequirementCount,
                mappingRequirementCapacity,
                mappingRanges,
                mappingRangeCapacity,
                pageImages,
                pageImageCapacity,
                pageTableEntries,
                pageTableEntryCapacity,
                cr3CodePage,
                cr3CodePageCapacity,
                code,
                protectionProbePage,
                protectionProbePageCapacity,
                probe,
                tableState,
                preflightObservation,
                preflightObservationCount,
                preflightObservationCapacity))
        {
            return FailPreparation(
                M01LivePagingStatus.CoordinatorStateRejected,
                checkpoint,
                evidence);
        }

        if (preparationState->IsPrepared)
        {
            return FailPreparation(
                M01LivePagingStatus.DuplicatePhase,
                checkpoint,
                evidence);
        }

        if (!IsDefaultPreparationState(*preparationState)
            || mappingRangeCount != 0
            || pageImageCount != 0
            || pageTableEntryCount != 0
            || byteMode != M01LivePagingByteMode.EmitAndVerify
                && byteMode != M01LivePagingByteMode.VerifyExisting)
        {
            return FailPreparation(
                byteMode != M01LivePagingByteMode.EmitAndVerify
                    && byteMode
                        != M01LivePagingByteMode.VerifyExisting
                    ? M01LivePagingStatus.ByteModeRejected
                    : M01LivePagingStatus.CoordinatorStateRejected,
                checkpoint,
                evidence);
        }

        evidence.ReservationValidationValue =
            OS.Kernel.Memory.BootReservationTableValidator.Validate(
                reservationLedger,
                reservationCount,
                reservationCapacity);
        if (!evidence.ReservationValidationValue.Succeeded)
        {
            return FailPreparation(
                M01LivePagingStatus.ReservationLedgerRejected,
                checkpoint,
                evidence);
        }

        if (!FindExactReservation(
                reservationLedger,
                reservationCount,
                code,
                OS.Kernel.Memory.BootReservationKind
                    .Cr3TransitionStub,
                out evidence.Cr3CodeReservationIndexValue,
                out OS.Kernel.Memory.BootReservedRange
                    codeReservation)
            || !FindExactReservation(
                reservationLedger,
                reservationCount,
                probe,
                OS.Kernel.Memory.BootReservationKind
                    .PageProtectionProbe,
                out evidence.ProtectionProbeReservationIndexValue,
                out OS.Kernel.Memory.BootReservedRange
                    probeReservation)
            || !FindExactReservation(
                reservationLedger,
                reservationCount,
                tableState,
                OS.Kernel.Memory.BootReservationKind.PageTableArena,
                out evidence.TableStateReservationIndexValue,
                out OS.Kernel.Memory.BootReservedRange
                    tableStateReservation)
            || !ContainsExactReservation(
                reservationLedger,
                reservationCount,
                stackSwitchPlan.Plan.StackLayout.GuardReservation)
            || !ContainsExactReservation(
                reservationLedger,
                reservationCount,
                stackSwitchPlan.Plan.StackLayout.StackReservation))
        {
            return FailPreparation(
                M01LivePagingStatus.ReservationIdentityRejected,
                checkpoint,
                evidence);
        }
        checkpoint = M01LivePagingCheckpoint.ReservationsValidated;

        if (!ValidateLiveIntervalIdentityMappingBindings(
                mappingRequirements,
                mappingRequirementCount,
                reservationLedger,
                reservationCount,
                stackSwitchPlan.Plan,
                codeReservation,
                probeReservation,
                tableStateReservation))
        {
            return FailPreparation(
                M01LivePagingStatus.IdentityMappingRejected,
                checkpoint,
                evidence);
        }

        PhysicalAddress provisionalRoot =
            ToPhysical(tableState.PhysicalStart);
        ulong provisionalTableByteLength =
            tableState.ByteLength - PageSize;
        OS.Kernel.Memory.X64PagingModePreflightRequest
            earlyPreflightRequest =
                OS.Kernel.Memory.X64PagingModePreflightRequest
                    .CreateEnabled(
                        provisionalRoot,
                        ToPhysical(tableState.PhysicalStart),
                        provisionalTableByteLength);
        OS.Kernel.Memory.X64PagingModePreflightResult
            earlyPreflightState = default;
        evidence.PreflightValue =
            OS.Kernel.Memory.X64PagingModePreflightStubPlan.Evaluate(
                &earlyPreflightRequest,
                1,
                1,
                preflightObservation,
                preflightObservationCount,
                preflightObservationCapacity,
                &earlyPreflightState,
                1,
                1);
        if (!evidence.PreflightValue.Succeeded
            || (evidence.PreflightValue.Rflags
                & RflagsDirectionFlagBit) != 0)
        {
            return FailPreparation(
                M01LivePagingStatus.PreflightRejected,
                checkpoint,
                evidence);
        }
        checkpoint = M01LivePagingCheckpoint.PagingModeValidated;

        evidence.MappingPlanValue =
            OS.Kernel.Boot.X64BootIdentityMappingPlan
                .CreateLiveInterval(
                mappingRequirements,
                mappingRequirementCount,
                mappingRequirementCapacity,
                mappingRanges,
                mappingRangeCapacity,
                pageImageCapacity,
                pageTablePreconditions);
        if (!evidence.MappingPlanValue.Succeeded
            || evidence.MappingPlanValue.TotalTablePageCount
                == MaximumUnsignedLong
            || evidence.MappingPlanValue.TotalTableByteLength
                != provisionalTableByteLength
            || !ValidateAllocationForIndex(
                tableState,
                TableStateAllocationRecordIndex,
                requireExactTablePages: true,
                expectedTablePages:
                    evidence.MappingPlanValue.TotalTablePageCount + 1))
        {
            return FailPreparation(
                M01LivePagingStatus.IdentityMappingRejected,
                checkpoint,
                evidence);
        }
        checkpoint =
            M01LivePagingCheckpoint.IdentityMappingPlanned;

        if (byteMode == M01LivePagingByteMode.EmitAndVerify)
        {
            ZeroBytes(cr3CodePage, cr3CodePageCapacity);
            ZeroBytes(
                protectionProbePage,
                protectionProbePageCapacity);
        }

        OS.Kernel.Memory.X64PagingModePreflightResult
            materializedPreflightState = default;
        if (byteMode == M01LivePagingByteMode.VerifyExisting)
        {
            evidence.PreflightReadbackValue =
                OS.Kernel.Memory.X64PagingModePreflightStubPlan
                    .TryValidateMaterialized(
                        cr3CodePage,
                        cr3CodePageCapacity,
                        PreflightEntryOffset,
                        OS.Kernel.Memory
                            .X64PagingModePreflightStubPlan
                            .RequiredByteLength,
                        preflightObservation,
                        OS.Kernel.Memory
                            .X64PagingModePreflightObservation
                            .StructureSize,
                        &materializedPreflightState,
                        OS.Kernel.Memory.X64PagingModePreflightResult
                            .StructureSize);
            if (!evidence.PreflightReadbackValue.Succeeded)
            {
                return FailPreparation(
                    M01LivePagingStatus.PreflightStubRejected,
                    checkpoint,
                    evidence);
            }

            evidence.PreflightMaterializationValue =
                new OS.Kernel.Memory.X64PagingModePreflightStubResult(
                    OS.Kernel.Memory.X64PagingModePreflightStatus
                        .Success,
                    OS.Kernel.Memory.X64PagingModePreflightOperation
                        .Materialize,
                    OS.Kernel.Memory.X64PagingModePreflightStubPlan
                        .RequiredByteLength,
                    cr3CodePageCapacity,
                    PreflightEntryOffset,
                    code.VirtualStart + PreflightEntryOffset,
                    OS.Kernel.Memory.X64PagingModePreflightStubPlan
                        .RequiredByteLength,
                    OS.Kernel.Memory.X64PagingModePreflightStubResult
                        .NoMismatchOffset,
                    exactBytes: true);
        }
        else
        {
            evidence.PreflightMaterializationValue =
                OS.Kernel.Memory.X64PagingModePreflightStubPlan
                    .TryMaterialize(
                        cr3CodePage,
                        cr3CodePageCapacity,
                        PreflightEntryOffset,
                        OS.Kernel.Memory
                            .X64PagingModePreflightStubPlan
                            .RequiredByteLength,
                        preflightObservation,
                        OS.Kernel.Memory
                            .X64PagingModePreflightObservation
                            .StructureSize,
                        &materializedPreflightState,
                        OS.Kernel.Memory.X64PagingModePreflightResult
                            .StructureSize);
        }
        evidence.PreflightReadbackValue =
            OS.Kernel.Memory.X64PagingModePreflightStubPlan
                .TryValidateMaterialized(
                    cr3CodePage,
                    cr3CodePageCapacity,
                    PreflightEntryOffset,
                    OS.Kernel.Memory.X64PagingModePreflightStubPlan
                        .RequiredByteLength,
                    preflightObservation,
                    OS.Kernel.Memory.X64PagingModePreflightObservation
                        .StructureSize,
                    &materializedPreflightState,
                    OS.Kernel.Memory.X64PagingModePreflightResult
                        .StructureSize);
        if (!evidence.PreflightMaterializationValue.Succeeded
            || !evidence.PreflightReadbackValue.Succeeded
            || !evidence.PreflightReadbackValue.ExactBytes)
        {
            return FailPreparation(
                M01LivePagingStatus.PreflightStubRejected,
                checkpoint,
                evidence);
        }

        byte* transitionEntry =
            cr3CodePage + TransitionEntryOffset;
        ulong transitionEntryAddress =
            code.VirtualStart + TransitionEntryOffset;
        ulong transitionCapacity =
            cr3CodePageCapacity - TransitionEntryOffset;
        if (byteMode == M01LivePagingByteMode.VerifyExisting)
        {
            evidence.TransitionReadbackValue =
                OS.Kernel.Memory
                    .X64ReversibleCr3TransitionStubPlan.Validate(
                        transitionEntry,
                        OS.Kernel.Memory
                            .X64ReversibleCr3TransitionStubPlan
                            .RequiredByteLength,
                        transitionCapacity,
                        transitionEntryAddress);
            if (!evidence.TransitionReadbackValue.Succeeded)
            {
                return FailPreparation(
                    M01LivePagingStatus.TransitionStubRejected,
                    checkpoint,
                    evidence);
            }
            evidence.TransitionMaterializationValue =
                evidence.TransitionReadbackValue;
        }
        else
        {
            evidence.TransitionMaterializationValue =
                OS.Kernel.Memory
                    .X64ReversibleCr3TransitionStubPlan.Emit(
                        transitionEntry,
                        transitionCapacity,
                        transitionEntryAddress);
        }
        evidence.TransitionReadbackValue =
            OS.Kernel.Memory.X64ReversibleCr3TransitionStubPlan
                .Validate(
                    transitionEntry,
                    OS.Kernel.Memory
                        .X64ReversibleCr3TransitionStubPlan
                        .RequiredByteLength,
                    transitionCapacity,
                    transitionEntryAddress);
        if (!evidence.TransitionMaterializationValue.Succeeded
            || !evidence.TransitionReadbackValue.Succeeded)
        {
            return FailPreparation(
                M01LivePagingStatus.TransitionStubRejected,
                checkpoint,
                evidence);
        }

        OS.Kernel.Memory.X64TlbInvalidationStatus rangeStatus =
            OS.Kernel.Memory.X64TlbInvalidationPlan.TryCreate(
                ToVirtual(probe.VirtualStart),
                PageSize,
                out OS.Kernel.Memory.X64TlbInvalidationRange
                    invalidationRange);
        OS.Kernel.Memory.X64TlbInvalidationRecord*
            invalidationRecords =
                stackalloc OS.Kernel.Memory
                    .X64TlbInvalidationRecord[1];
        OS.Kernel.Memory.X64TlbInvalidationPlanResult
            invalidationPlan =
                rangeStatus
                    == OS.Kernel.Memory.X64TlbInvalidationStatus
                        .Success
                ? OS.Kernel.Memory.X64TlbInvalidationPlan
                    .Materialize(
                        invalidationRange,
                        invalidationRecords,
                        1)
                : default;
        if (!invalidationPlan.Succeeded)
        {
            return FailPreparation(
                M01LivePagingStatus.InvlpgStubRejected,
                checkpoint,
                evidence);
        }

        if (byteMode == M01LivePagingByteMode.VerifyExisting)
        {
            evidence.InvlpgReadbackValue =
                OS.Kernel.Memory.X64InvlpgStubPlan.ValidateReadback(
                    cr3CodePage,
                    code.VirtualStart,
                    cr3CodePageCapacity,
                    CreateExpectedInvlpgMaterialization(
                        code,
                        invalidationRecords[0]));
            if (!evidence.InvlpgReadbackValue.ReadBackVerified)
            {
                return FailPreparation(
                    M01LivePagingStatus.InvlpgStubRejected,
                    checkpoint,
                    evidence);
            }
            evidence.InvlpgMaterializationValue =
                CreateExpectedInvlpgMaterialization(
                    code,
                    invalidationRecords[0]);
        }
        else
        {
            evidence.InvlpgMaterializationValue =
                OS.Kernel.Memory.X64InvlpgStubPlan.Emit(
                    cr3CodePage,
                    code.VirtualStart,
                    cr3CodePageCapacity,
                    ToVirtual(code.VirtualStart),
                    InvlpgEntryOffset,
                    invalidationRecords[0]);
        }
        evidence.InvlpgReadbackValue =
            OS.Kernel.Memory.X64InvlpgStubPlan.ValidateReadback(
                cr3CodePage,
                code.VirtualStart,
                cr3CodePageCapacity,
                evidence.InvlpgMaterializationValue);
        if (!evidence.InvlpgMaterializationValue.Succeeded
            || !evidence.InvlpgReadbackValue.ReadBackVerified
            || StubRangesOverlap())
        {
            return FailPreparation(
                M01LivePagingStatus.InvlpgStubRejected,
                checkpoint,
                evidence);
        }

        if (byteMode == M01LivePagingByteMode.VerifyExisting)
        {
            evidence.ProbeReadbackValue =
                OS.Kernel.Memory.X64ProtectionProbeStubPlan
                    .ValidateReadback(
                        protectionProbePage,
                        probe.VirtualStart,
                        protectionProbePageCapacity,
                        CreateExpectedProbeMaterialization(
                            probe,
                            probeReservation));
            if (!evidence.ProbeReadbackValue.ReadBackVerified)
            {
                return FailPreparation(
                    M01LivePagingStatus
                        .ProtectionProbeStubRejected,
                    checkpoint,
                    evidence);
            }
            evidence.ProbeMaterializationValue =
                CreateExpectedProbeMaterialization(
                    probe,
                    probeReservation);
        }
        else
        {
            evidence.ProbeMaterializationValue =
                OS.Kernel.Memory.X64ProtectionProbeStubPlan.Emit(
                    protectionProbePage,
                    probe.VirtualStart,
                    protectionProbePageCapacity,
                    probe,
                    probeReservation,
                    ProtectionProbeEntryOffset);
        }
        evidence.ProbeReadbackValue =
            OS.Kernel.Memory.X64ProtectionProbeStubPlan
                .ValidateReadback(
                    protectionProbePage,
                    probe.VirtualStart,
                    protectionProbePageCapacity,
                    evidence.ProbeMaterializationValue);
        if (!evidence.ProbeMaterializationValue.Succeeded
            || !evidence.ProbeReadbackValue.ReadBackVerified
            || !AuditStubPageLayouts(
                cr3CodePage,
                protectionProbePage))
        {
            return FailPreparation(
                M01LivePagingStatus.ProtectionProbeStubRejected,
                checkpoint,
                evidence);
        }
        checkpoint = M01LivePagingCheckpoint.StubBytesVerified;

        evidence.TableBuildValue = BuildTableImage(
            evidence.MappingPlanValue,
            mappingRanges,
            evidence.MappingPlanValue.OutputRangeCount,
            mappingRangeCapacity,
            tableState,
            code,
            probe,
            stageTwoState->StageOneStateAddress,
            pageImages,
            pageImageCapacity,
            pageTableEntries,
            pageTableEntryCapacity,
            pageTablePreconditions,
            requireStageOneStubMapping: false);
        if (!evidence.TableBuildValue.Succeeded
            || !evidence.TableBuildValue.RootTable.Equals(
                provisionalRoot))
        {
            return FailPreparation(
                evidence.TableBuildValue.Status
                    == M01LivePagingTableBuildStatus
                        .InvalidTableStorage
                    ? M01LivePagingStatus.TableStorageRejected
                    : M01LivePagingStatus.TableConstructionRejected,
                checkpoint,
                evidence);
        }
        checkpoint = M01LivePagingCheckpoint.TableImageBuilt;

        PhysicalAddress root =
            evidence.TableBuildValue.RootTable;
        OS.Kernel.Memory.PageProtection readExecute =
            (OS.Kernel.Memory.PageProtection)(
                (byte)OS.Kernel.Memory.PageProtection.Read
                | (byte)OS.Kernel.Memory.PageProtection.Execute);
        OS.Kernel.Memory.X64PageMapResult mutation =
            OS.Kernel.Memory.X64PageMap.Apply(
                OS.Kernel.Memory.X64PageMapRequest.CreateProtect(
                    root,
                    ToVirtual(probe.VirtualStart),
                    readExecute,
                    OS.Kernel.Memory.X64HardwareStatePolicy.Preserve,
                    pageTablePreconditions),
                pageImages,
                evidence.TableBuildValue.TablePageCount,
                pageImageCapacity,
                pageTableEntries,
                evidence.TableBuildValue.EntryCount,
                pageTableEntryCapacity,
                null,
                0,
                0);
        if (!mutation.Succeeded
            || mutation.TargetEntryIndex
                != evidence.TableBuildValue.ProbeLeafEntryIndex)
        {
            if (mutation.Succeeded)
            {
                _ = RestoreModeledProbeLeaf(
                    pageTableEntries,
                    evidence.TableBuildValue.EntryCount,
                    pageTableEntryCapacity,
                    mutation);
            }
            return FailPreparation(
                M01LivePagingStatus.ProtectionMutationRejected,
                checkpoint,
                evidence);
        }

        ulong protectedLeafReadback =
            pageTableEntries[mutation.TargetEntryIndex];
        OS.Kernel.Memory.X64InvlpgInvocationRequest
            invlpgRequest =
                OS.Kernel.Memory.X64InvlpgInvocationRequest.Create(
                    evidence.InvlpgReadbackValue.EntryAddress,
                    invalidationRecords[0],
                    readExecute,
                    1);
        OS.Kernel.Memory.X64InvlpgInvocationPlanResult
            invlpgInvocation =
                OS.Kernel.Memory.X64InvlpgStubPlan
                    .CreateInvocationPlan(
                        evidence.InvlpgReadbackValue,
                        invlpgRequest);
        bool restoredInitialProbeLeaf =
            RestoreModeledProbeLeaf(
                pageTableEntries,
                evidence.TableBuildValue.EntryCount,
                pageTableEntryCapacity,
                mutation);
        evidence.ProtectionPreparationValue =
            new M01LivePagingProtectionPreparation(
                invlpgInvocation.Succeeded
                    && restoredInitialProbeLeaf
                    ? M01LivePagingStatus.Success
                    : M01LivePagingStatus
                        .ProtectionMutationRejected,
                mutation,
                invlpgInvocation,
                protectedLeafReadback,
                restoredInitialProbeLeaf);
        if (!evidence.ProtectionPreparationValue.Succeeded)
        {
            return FailPreparation(
                M01LivePagingStatus.ProtectionMutationRejected,
                checkpoint,
                evidence);
        }
        checkpoint =
            M01LivePagingCheckpoint.ProtectionPlanValidated;

        evidence.PageZeroAuditValue =
            OS.Kernel.Memory.PageZeroMappingPolicy.AuditTableImage(
                root,
                pageTablePreconditions,
                pageImages,
                evidence.TableBuildValue.TablePageCount,
                pageImageCapacity,
                pageTableEntries,
                evidence.TableBuildValue.EntryCount,
                pageTableEntryCapacity);
        if (!evidence.PageZeroAuditValue
                .ProvesPageZeroAbsentInSuppliedImage)
        {
            return FailPreparation(
                M01LivePagingStatus.PageZeroRejected,
                checkpoint,
                evidence);
        }
        checkpoint = M01LivePagingCheckpoint.PageZeroValidated;

        evidence.CleanupPlanValue = PlanCleanup(
            allocationRecords,
            allocationRecordCount,
            allocationRecordCapacity,
            RequiredAllocationRecordCount,
            firmwareServicesAvailable: true,
            ownedRootWasEntered: false,
            ownedRootRestored: false);
        if (!evidence.CleanupPlanValue.Succeeded)
        {
            return FailPreparation(
                M01LivePagingStatus.CleanupPlanRejected,
                checkpoint,
                evidence);
        }

        M01LivePagingPreparationState candidate = default;
        candidate.HeaderSize =
            (uint)M01LivePagingPreparationState.RequiredStorageBytes;
        candidate.HeaderVersion =
            M01LivePagingPreparationState.CurrentVersion;
        candidate.PreparationStatus = M01LivePagingStatus.Success;
        candidate.PreparationCheckpoint =
            M01LivePagingCheckpoint.PreparedForCompositeTransition;
        candidate.ExecutionModeValue =
            M01LivePagingExecutionMode.PreparationOnly;
        candidate.StageTwoStateAddressValue = (ulong)stageTwoState;
        candidate.StageTwoStateCountValue = stageTwoStateCount;
        candidate.StageTwoStateCapacityValue = stageTwoStateCapacity;
        candidate.AllocationTableAddressValue =
            (ulong)allocationRecords;
        candidate.AllocationRecordCountValue =
            allocationRecordCount;
        candidate.AllocationRecordCapacityValue =
            allocationRecordCapacity;
        candidate.ReservationLedgerAddressValue =
            (ulong)reservationLedger;
        candidate.ReservationCountValue = reservationCount;
        candidate.ReservationCapacityValue = reservationCapacity;
        candidate.MappingRequirementAddressValue =
            (ulong)mappingRequirements;
        candidate.MappingRequirementCountValue =
            mappingRequirementCount;
        candidate.MappingRequirementCapacityValue =
            mappingRequirementCapacity;
        candidate.MappingRangeAddressValue = (ulong)mappingRanges;
        candidate.MappingRangeCountValue =
            evidence.MappingPlanValue.OutputRangeCount;
        candidate.MappingRangeCapacityValue = mappingRangeCapacity;
        candidate.PageImageAddressValue = (ulong)pageImages;
        candidate.PageImageCountValue =
            evidence.TableBuildValue.TablePageCount;
        candidate.PageImageCapacityValue = pageImageCapacity;
        candidate.PageEntryAddressValue = (ulong)pageTableEntries;
        candidate.PageEntryCountValue =
            evidence.TableBuildValue.EntryCount;
        candidate.PageEntryCapacityValue = pageTableEntryCapacity;
        candidate.Cr3CodePageAddressValue = (ulong)cr3CodePage;
        candidate.Cr3CodePageCapacityValue = cr3CodePageCapacity;
        candidate.ProtectionProbePageAddressValue =
            (ulong)protectionProbePage;
        candidate.ProtectionProbePageCapacityValue =
            protectionProbePageCapacity;
        candidate.PreflightObservationAddressValue =
            (ulong)preflightObservation;
        candidate.PreflightObservationCountValue =
            preflightObservationCount;
        candidate.PreflightObservationCapacityValue =
            preflightObservationCapacity;
        candidate.RefreshedMapKeyValue =
            mapLedgerEvidence.RefreshedMap.MapKey;
        candidate.RefreshedMapGenerationValue =
            mapLedgerEvidence.RefreshedMapGeneration;
        candidate.Cr3CodeReservationValue = codeReservation;
        candidate.ProtectionProbeReservationValue =
            probeReservation;
        candidate.TableStateReservationValue =
            tableStateReservation;
        candidate.OwnedRootValue = root;
        candidate.RawFirmwareCr3Value =
            evidence.PreflightValue.RawFirmwareCr3;
        candidate.RawCr4Value = evidence.PreflightValue.RawCr4;
        candidate.OriginalRflagsValue =
            evidence.PreflightValue.Rflags;
        candidate.TablePageCountValue =
            evidence.TableBuildValue.TablePageCount;
        candidate.MappedPageCountValue =
            evidence.TableBuildValue.MappedPageCount;
        candidate.ProbeLeafEntryIndexValue =
            evidence.TableBuildValue.ProbeLeafEntryIndex;
        candidate.PageZeroAbsentValue = 1;
        candidate.StubBytesVerifiedValue = 1;
        candidate.InitialProbeLeafRestoredValue = 1;
        candidate.CleanupPlannedValue = 1;

        if (!IsPreparedState(candidate))
        {
            return FailPreparation(
                M01LivePagingStatus.IllegalCoordinatorState,
                checkpoint,
                evidence);
        }

        M01LivePagingPreparationResult result =
            M01LivePagingPreparationResult.Success(
                evidence,
                candidate);
        if (!result.Succeeded)
        {
            return FailPreparation(
                M01LivePagingStatus.IllegalCoordinatorState,
                checkpoint,
                evidence);
        }

        *preparationState = candidate;
        return result;
    }

    /// <summary>
    /// Explicit name for the retained M01-019 synthetic transcript
    /// coordinator. A successful result remains model-only and can never
    /// satisfy PreparedForCompositeTransition.
    /// </summary>
    public static M01LivePagingIntegrationResult ExecuteModelOnly(
        M01LiveAllocatorStaticState* stageTwoState,
        ulong stageTwoStateCapacity,
        OwnedBootStackSwitchPlanResult stackSwitchPlan,
        BootMemoryRegionRecord* allocationRecords,
        ulong allocationRecordCount,
        ulong allocationRecordCapacity,
        M01LivePagingAllocationEvidence allocationEvidence,
        M01LivePagingMapLedgerEvidence mapLedgerEvidence,
        OS.Kernel.Memory.BootReservedRange* reservationLedger,
        ulong reservationCount,
        ulong reservationCapacity,
        OS.Kernel.Boot.X64BootIdentityMappingRequirement*
            mappingRequirements,
        ulong mappingRequirementCount,
        ulong mappingRequirementCapacity,
        OS.Kernel.Boot.X64BootIdentityMappingRange* mappingRanges,
        ulong mappingRangeCapacity,
        OS.Kernel.Memory.X64PageTableImagePage* pageImages,
        ulong pageImageCapacity,
        ulong* pageTableEntries,
        ulong pageTableEntryCapacity,
        OS.Kernel.Memory.X64PageTableEncodingPreconditions
            pageTablePreconditions,
        byte* cr3CodePage,
        ulong cr3CodePageCapacity,
        byte* protectionProbePage,
        ulong protectionProbePageCapacity,
        OS.Kernel.Memory.X64PagingModePreflightObservation*
            preflightObservation,
        ulong preflightObservationCapacity,
        OS.Kernel.Memory.X64ReversibleCr3TransitionRuntimeState*
            transitionRuntimeState,
        OS.Kernel.Memory.X64Cr3ProbeEvent* cr3Events,
        ulong cr3EventCount,
        ulong cr3EventCapacity,
        OS.Kernel.Memory.X64ProtectionProbeObservation
            protectionObservation,
        OS.Kernel.Memory.X64ProtectionProbeEvent* protectionEvents,
        ulong protectionEventCount,
        ulong protectionEventCapacity,
        OS.Kernel.Memory.X64Cr3ProbePolicy cr3Policy,
        M01LivePagingByteMode byteMode,
        M01LivePagingStaticState* staticState,
        ulong staticStateCapacity)
    {
        return Execute(
            stageTwoState,
            stageTwoStateCapacity,
            stackSwitchPlan,
            allocationRecords,
            allocationRecordCount,
            allocationRecordCapacity,
            allocationEvidence,
            mapLedgerEvidence,
            reservationLedger,
            reservationCount,
            reservationCapacity,
            mappingRequirements,
            mappingRequirementCount,
            mappingRequirementCapacity,
            mappingRanges,
            mappingRangeCapacity,
            pageImages,
            pageImageCapacity,
            pageTableEntries,
            pageTableEntryCapacity,
            pageTablePreconditions,
            cr3CodePage,
            cr3CodePageCapacity,
            protectionProbePage,
            protectionProbePageCapacity,
            preflightObservation,
            preflightObservationCapacity,
            transitionRuntimeState,
            cr3Events,
            cr3EventCount,
            cr3EventCapacity,
            protectionObservation,
            protectionEvents,
            protectionEventCount,
            protectionEventCapacity,
            cr3Policy,
            byteMode,
            staticState,
            staticStateCapacity);
    }

    public static M01LivePagingIntegrationResult Execute(
        M01LiveAllocatorStaticState* stageTwoState,
        ulong stageTwoStateCapacity,
        OwnedBootStackSwitchPlanResult stackSwitchPlan,
        BootMemoryRegionRecord* allocationRecords,
        ulong allocationRecordCount,
        ulong allocationRecordCapacity,
        M01LivePagingAllocationEvidence allocationEvidence,
        M01LivePagingMapLedgerEvidence mapLedgerEvidence,
        OS.Kernel.Memory.BootReservedRange* reservationLedger,
        ulong reservationCount,
        ulong reservationCapacity,
        OS.Kernel.Boot.X64BootIdentityMappingRequirement*
            mappingRequirements,
        ulong mappingRequirementCount,
        ulong mappingRequirementCapacity,
        OS.Kernel.Boot.X64BootIdentityMappingRange* mappingRanges,
        ulong mappingRangeCapacity,
        OS.Kernel.Memory.X64PageTableImagePage* pageImages,
        ulong pageImageCapacity,
        ulong* pageTableEntries,
        ulong pageTableEntryCapacity,
        OS.Kernel.Memory.X64PageTableEncodingPreconditions
            pageTablePreconditions,
        byte* cr3CodePage,
        ulong cr3CodePageCapacity,
        byte* protectionProbePage,
        ulong protectionProbePageCapacity,
        OS.Kernel.Memory.X64PagingModePreflightObservation*
            preflightObservation,
        ulong preflightObservationCapacity,
        OS.Kernel.Memory.X64ReversibleCr3TransitionRuntimeState*
            transitionRuntimeState,
        OS.Kernel.Memory.X64Cr3ProbeEvent* cr3Events,
        ulong cr3EventCount,
        ulong cr3EventCapacity,
        OS.Kernel.Memory.X64ProtectionProbeObservation
            protectionObservation,
        OS.Kernel.Memory.X64ProtectionProbeEvent* protectionEvents,
        ulong protectionEventCount,
        ulong protectionEventCapacity,
        OS.Kernel.Memory.X64Cr3ProbePolicy cr3Policy,
        M01LivePagingByteMode byteMode,
        M01LivePagingStaticState* staticState,
        ulong staticStateCapacity)
    {
        M01LivePagingEvidence evidence = default;
        evidence.AllocationEvidenceValue = allocationEvidence;
        evidence.MapLedgerEvidenceValue = mapLedgerEvidence;
        evidence.Cr3CodeReservationIndexValue = NoIndex;
        evidence.ProtectionProbeReservationIndexValue = NoIndex;
        evidence.TableStateReservationIndexValue = NoIndex;
        M01LivePagingCheckpoint checkpoint =
            M01LivePagingCheckpoint.Uninitialized;

        if (!ValidateStageTwoState(
                stageTwoState,
                stageTwoStateCapacity,
                out ulong stageTwoStart,
                out ulong stageTwoEnd)
            || !stackSwitchPlan.Succeeded)
        {
            return Fail(
                M01LivePagingStatus.StageTwoRejected,
                checkpoint,
                evidence);
        }
        checkpoint = M01LivePagingCheckpoint.StageTwoValidated;
        M01LoadedImageStackStaticState* stageOne =
            (M01LoadedImageStackStaticState*)
                stageTwoState->StageOneStateAddress;
        if (!ValidateStageOneState(
                stageOne,
                stageTwoState->StageOneStateCapacity,
                stackSwitchPlan,
                out ulong stageOneStart,
                out ulong stageOneEnd))
        {
            return Fail(
                M01LivePagingStatus.StageTwoRejected,
                checkpoint,
                evidence);
        }

        if (!ValidateAllocationStorage(
                allocationRecords,
                allocationRecordCount,
                allocationRecordCapacity,
                out ulong allocationTableStart,
                out ulong allocationTableEnd))
        {
            return Fail(
                M01LivePagingStatus.AllocationTableRejected,
                checkpoint,
                evidence);
        }

        if (!allocationEvidence.IsValidFor(
                allocationRecords,
                allocationRecordCount,
                allocationRecordCapacity))
        {
            return Fail(
                M01LivePagingStatus.AllocationEvidenceRejected,
                checkpoint,
                evidence);
        }

        BootMemoryRegionRecord code =
            allocationRecords[Cr3CodeAllocationRecordIndex];
        BootMemoryRegionRecord probe =
            allocationRecords[ProtectionProbeAllocationRecordIndex];
        BootMemoryRegionRecord tableState =
            allocationRecords[TableStateAllocationRecordIndex];
        if (!ValidateAllocationForIndex(
                code,
                Cr3CodeAllocationRecordIndex,
                requireExactTablePages: false,
                expectedTablePages: 0)
            || !ValidateAllocationForIndex(
                probe,
                ProtectionProbeAllocationRecordIndex,
                requireExactTablePages: false,
                expectedTablePages: 0)
            || !ValidateAllocationForIndex(
                tableState,
                TableStateAllocationRecordIndex,
                requireExactTablePages: false,
                expectedTablePages: 0)
            || AllocationsOverlap(code, probe)
            || AllocationsOverlap(code, tableState)
            || AllocationsOverlap(probe, tableState))
        {
            return Fail(
                M01LivePagingStatus.AllocationTableRejected,
                checkpoint,
                evidence);
        }
        checkpoint = M01LivePagingCheckpoint.AllocationsValidated;

        if (!ValidateReservationStorage(
                reservationLedger,
                reservationCount,
                reservationCapacity,
                out ulong reservationTableStart,
                out ulong reservationTableEnd)
            || !mapLedgerEvidence.IsValidFor(
                *stageTwoState,
                allocationEvidence,
                reservationLedger,
                reservationCount,
                reservationCapacity))
        {
            return Fail(
                mapLedgerEvidence.IsValidFor(
                    *stageTwoState,
                    allocationEvidence,
                    reservationLedger,
                    reservationCount,
                    reservationCapacity)
                    ? M01LivePagingStatus.ReservationLedgerRejected
                    : M01LivePagingStatus.MapLedgerEvidenceRejected,
                checkpoint,
                evidence);
        }
        checkpoint = M01LivePagingCheckpoint.MapLedgerValidated;

        if (!ValidateStaticStateStorage(
                staticState,
                staticStateCapacity,
                out ulong staticStateStart,
                out ulong staticStateEnd)
            || !AreDisjoint(
                stageTwoStart,
                stageTwoEnd,
                allocationTableStart,
                allocationTableEnd)
            || !AreDisjoint(
                stageOneStart,
                stageOneEnd,
                stageTwoStart,
                stageTwoEnd)
            || !AreDisjoint(
                stageOneStart,
                stageOneEnd,
                allocationTableStart,
                allocationTableEnd)
            || !AreDisjoint(
                stageOneStart,
                stageOneEnd,
                reservationTableStart,
                reservationTableEnd)
            || !AreDisjoint(
                stageOneStart,
                stageOneEnd,
                staticStateStart,
                staticStateEnd)
            || !AreDisjoint(
                stageTwoStart,
                stageTwoEnd,
                reservationTableStart,
                reservationTableEnd)
            || !AreDisjoint(
                stageTwoStart,
                stageTwoEnd,
                staticStateStart,
                staticStateEnd)
            || !AreDisjoint(
                allocationTableStart,
                allocationTableEnd,
                reservationTableStart,
                reservationTableEnd)
            || !AreDisjoint(
                allocationTableStart,
                allocationTableEnd,
                staticStateStart,
                staticStateEnd)
            || !AreDisjoint(
                reservationTableStart,
                reservationTableEnd,
                staticStateStart,
                staticStateEnd)
            || MetadataOverlapsMap(
                stageOneStart,
                stageOneEnd,
                mapLedgerEvidence.RefreshedMap)
            || MetadataOverlapsMap(
                stageTwoStart,
                stageTwoEnd,
                mapLedgerEvidence.RefreshedMap)
            || MetadataOverlapsMap(
                allocationTableStart,
                allocationTableEnd,
                mapLedgerEvidence.RefreshedMap)
            || MetadataOverlapsMap(
                reservationTableStart,
                reservationTableEnd,
                mapLedgerEvidence.RefreshedMap)
            || MetadataOverlapsMap(
                staticStateStart,
                staticStateEnd,
                mapLedgerEvidence.RefreshedMap))
        {
            return Fail(
                M01LivePagingStatus.CoordinatorStateRejected,
                checkpoint,
                evidence);
        }

        if (staticState->IsCompleted)
        {
            return Fail(
                M01LivePagingStatus.DuplicatePhase,
                checkpoint,
                evidence);
        }

        if (!IsDefaultState(*staticState))
        {
            return Fail(
                M01LivePagingStatus.CoordinatorStateRejected,
                checkpoint,
                evidence);
        }

        if (!ValidateCoordinatorBufferTopology(
                stageOneStart,
                stageOneEnd,
                stageTwoStart,
                stageTwoEnd,
                allocationTableStart,
                allocationTableEnd,
                reservationTableStart,
                reservationTableEnd,
                staticStateStart,
                staticStateEnd,
                mapLedgerEvidence.RefreshedMap,
                mappingRequirements,
                mappingRequirementCount,
                mappingRequirementCapacity,
                mappingRanges,
                mappingRangeCapacity,
                pageImages,
                pageImageCapacity,
                pageTableEntries,
                pageTableEntryCapacity,
                cr3CodePage,
                cr3CodePageCapacity,
                code,
                protectionProbePage,
                protectionProbePageCapacity,
                probe,
                tableState,
                preflightObservation,
                preflightObservationCapacity,
                transitionRuntimeState,
                cr3Events,
                cr3EventCount,
                cr3EventCapacity,
                protectionEvents,
                protectionEventCount,
                protectionEventCapacity))
        {
            return Fail(
                M01LivePagingStatus.CoordinatorStateRejected,
                checkpoint,
                evidence);
        }

        evidence.ReservationValidationValue =
            OS.Kernel.Memory.BootReservationTableValidator.Validate(
                reservationLedger,
                reservationCount,
                reservationCapacity);
        if (!evidence.ReservationValidationValue.Succeeded)
        {
            return Fail(
                M01LivePagingStatus.ReservationLedgerRejected,
                checkpoint,
                evidence);
        }

        if (!FindExactReservation(
                reservationLedger,
                reservationCount,
                code,
                OS.Kernel.Memory.BootReservationKind
                    .Cr3TransitionStub,
                out evidence.Cr3CodeReservationIndexValue,
                out OS.Kernel.Memory.BootReservedRange
                    codeReservation)
            || !FindExactReservation(
                reservationLedger,
                reservationCount,
                probe,
                OS.Kernel.Memory.BootReservationKind
                    .PageProtectionProbe,
                out evidence.ProtectionProbeReservationIndexValue,
                out OS.Kernel.Memory.BootReservedRange
                    probeReservation)
            || !FindExactReservation(
                reservationLedger,
                reservationCount,
                tableState,
                OS.Kernel.Memory.BootReservationKind.PageTableArena,
                out evidence.TableStateReservationIndexValue,
                out OS.Kernel.Memory.BootReservedRange
                    tableStateReservation)
            || !ContainsExactReservation(
                reservationLedger,
                reservationCount,
                stageTwoState->ArenaReservation)
            || !ContainsExactReservation(
                reservationLedger,
                reservationCount,
                stackSwitchPlan.Plan.StubReservation)
            || !ContainsExactReservation(
                reservationLedger,
                reservationCount,
                stackSwitchPlan.Plan.StackLayout.GuardReservation)
            || !ContainsExactReservation(
                reservationLedger,
                reservationCount,
                stackSwitchPlan.Plan.StackLayout.StackReservation))
        {
            return Fail(
                M01LivePagingStatus.ReservationIdentityRejected,
                checkpoint,
                evidence);
        }
        checkpoint = M01LivePagingCheckpoint.ReservationsValidated;

        if (!ValidateLegacyIdentityMappingBindings(
                mappingRequirements,
                mappingRequirementCount,
                reservationLedger,
                reservationCount,
                stageOneStart,
                stageOneEnd,
                stackSwitchPlan.Plan,
                stageTwoState->ArenaReservation,
                codeReservation,
                probeReservation,
                tableStateReservation))
        {
            return Fail(
                M01LivePagingStatus.IdentityMappingRejected,
                checkpoint,
                evidence);
        }

        evidence.MappingPlanValue =
            OS.Kernel.Boot.X64BootIdentityMappingPlan.Create(
                mappingRequirements,
                mappingRequirementCount,
                mappingRequirementCapacity,
                mappingRanges,
                mappingRangeCapacity,
                pageImageCapacity,
                pageTablePreconditions);
        if (!evidence.MappingPlanValue.Succeeded
            || evidence.MappingPlanValue.TotalTablePageCount
                == MaximumUnsignedLong
            || !ValidateAllocationForIndex(
                tableState,
                TableStateAllocationRecordIndex,
                requireExactTablePages: true,
                expectedTablePages:
                    evidence.MappingPlanValue.TotalTablePageCount + 1))
        {
            return Fail(
                M01LivePagingStatus.IdentityMappingRejected,
                checkpoint,
                evidence);
        }
        checkpoint =
            M01LivePagingCheckpoint.IdentityMappingPlanned;

        if (byteMode != M01LivePagingByteMode.EmitAndVerify
            && byteMode != M01LivePagingByteMode.VerifyExisting)
        {
            return Fail(
                M01LivePagingStatus.ByteModeRejected,
                checkpoint,
                evidence);
        }

        if (cr3CodePage == null
            || protectionProbePage == null
            || cr3CodePageCapacity != PageSize
            || protectionProbePageCapacity != PageSize
            || (ulong)cr3CodePage != code.VirtualStart
            || (ulong)protectionProbePage != probe.VirtualStart)
        {
            return Fail(
                M01LivePagingStatus.CoordinatorStateRejected,
                checkpoint,
                evidence);
        }

        if (byteMode == M01LivePagingByteMode.EmitAndVerify)
        {
            ZeroBytes(cr3CodePage, cr3CodePageCapacity);
            ZeroBytes(
                protectionProbePage,
                protectionProbePageCapacity);
        }

        OS.Kernel.Memory.X64PagingModePreflightResult
            preflightState = default;
        if (byteMode == M01LivePagingByteMode.VerifyExisting)
        {
            evidence.PreflightReadbackValue =
                OS.Kernel.Memory.X64PagingModePreflightStubPlan
                    .TryValidateMaterialized(
                        cr3CodePage,
                        cr3CodePageCapacity,
                        PreflightEntryOffset,
                        OS.Kernel.Memory
                            .X64PagingModePreflightStubPlan
                            .RequiredByteLength,
                        preflightObservation,
                        OS.Kernel.Memory
                            .X64PagingModePreflightObservation
                            .StructureSize,
                        &preflightState,
                        OS.Kernel.Memory.X64PagingModePreflightResult
                            .StructureSize);
            if (!evidence.PreflightReadbackValue.Succeeded)
            {
                return Fail(
                    M01LivePagingStatus.PreflightStubRejected,
                    checkpoint,
                    evidence);
            }

            evidence.PreflightMaterializationValue =
                new OS.Kernel.Memory.X64PagingModePreflightStubResult(
                    OS.Kernel.Memory.X64PagingModePreflightStatus
                        .Success,
                    OS.Kernel.Memory.X64PagingModePreflightOperation
                        .Materialize,
                    OS.Kernel.Memory.X64PagingModePreflightStubPlan
                        .RequiredByteLength,
                    cr3CodePageCapacity,
                    PreflightEntryOffset,
                    code.VirtualStart + PreflightEntryOffset,
                    OS.Kernel.Memory.X64PagingModePreflightStubPlan
                        .RequiredByteLength,
                    OS.Kernel.Memory.X64PagingModePreflightStubResult
                        .NoMismatchOffset,
                    exactBytes: true);
        }
        else
        {
            evidence.PreflightMaterializationValue =
                OS.Kernel.Memory.X64PagingModePreflightStubPlan
                    .TryMaterialize(
                        cr3CodePage,
                        cr3CodePageCapacity,
                        PreflightEntryOffset,
                        OS.Kernel.Memory
                            .X64PagingModePreflightStubPlan
                            .RequiredByteLength,
                        preflightObservation,
                        OS.Kernel.Memory
                            .X64PagingModePreflightObservation
                            .StructureSize,
                        &preflightState,
                        OS.Kernel.Memory.X64PagingModePreflightResult
                            .StructureSize);
        }
        evidence.PreflightReadbackValue =
            OS.Kernel.Memory.X64PagingModePreflightStubPlan
                .TryValidateMaterialized(
                    cr3CodePage,
                    cr3CodePageCapacity,
                    PreflightEntryOffset,
                    OS.Kernel.Memory.X64PagingModePreflightStubPlan
                        .RequiredByteLength,
                    preflightObservation,
                    OS.Kernel.Memory
                        .X64PagingModePreflightObservation
                        .StructureSize,
                    &preflightState,
                    OS.Kernel.Memory.X64PagingModePreflightResult
                        .StructureSize);
        if (!evidence.PreflightMaterializationValue.Succeeded
            || !evidence.PreflightReadbackValue.Succeeded
            || !evidence.PreflightReadbackValue.ExactBytes)
        {
            return Fail(
                M01LivePagingStatus.PreflightStubRejected,
                checkpoint,
                evidence);
        }

        byte* transitionEntry =
            cr3CodePage + TransitionEntryOffset;
        ulong transitionEntryAddress =
            code.VirtualStart + TransitionEntryOffset;
        ulong transitionCapacity =
            cr3CodePageCapacity - TransitionEntryOffset;
        if (byteMode == M01LivePagingByteMode.VerifyExisting)
        {
            evidence.TransitionReadbackValue =
                OS.Kernel.Memory
                    .X64ReversibleCr3TransitionStubPlan.Validate(
                        transitionEntry,
                        OS.Kernel.Memory
                            .X64ReversibleCr3TransitionStubPlan
                            .RequiredByteLength,
                        transitionCapacity,
                        transitionEntryAddress);
            if (!evidence.TransitionReadbackValue.Succeeded)
            {
                return Fail(
                    M01LivePagingStatus.TransitionStubRejected,
                    checkpoint,
                    evidence);
            }

            evidence.TransitionMaterializationValue =
                evidence.TransitionReadbackValue;
        }
        else
        {
            evidence.TransitionMaterializationValue =
                OS.Kernel.Memory
                    .X64ReversibleCr3TransitionStubPlan.Emit(
                        transitionEntry,
                        transitionCapacity,
                        transitionEntryAddress);
        }
        evidence.TransitionReadbackValue =
            OS.Kernel.Memory.X64ReversibleCr3TransitionStubPlan
                .Validate(
                    transitionEntry,
                    OS.Kernel.Memory
                        .X64ReversibleCr3TransitionStubPlan
                        .RequiredByteLength,
                    transitionCapacity,
                    transitionEntryAddress);
        if (!evidence.TransitionMaterializationValue.Succeeded
            || !evidence.TransitionReadbackValue.Succeeded)
        {
            return Fail(
                M01LivePagingStatus.TransitionStubRejected,
                checkpoint,
                evidence);
        }

        OS.Kernel.Memory.X64TlbInvalidationStatus rangeStatus =
            OS.Kernel.Memory.X64TlbInvalidationPlan.TryCreate(
                ToVirtual(probe.VirtualStart),
                PageSize,
                out OS.Kernel.Memory.X64TlbInvalidationRange
                    invalidationRange);
        OS.Kernel.Memory.X64TlbInvalidationRecord*
            invalidationRecords =
                stackalloc OS.Kernel.Memory
                    .X64TlbInvalidationRecord[1];
        OS.Kernel.Memory.X64TlbInvalidationPlanResult
            invalidationPlan =
                rangeStatus
                    == OS.Kernel.Memory.X64TlbInvalidationStatus
                        .Success
                ? OS.Kernel.Memory.X64TlbInvalidationPlan
                    .Materialize(
                        invalidationRange,
                        invalidationRecords,
                        1)
                : default;
        if (!invalidationPlan.Succeeded)
        {
            return Fail(
                M01LivePagingStatus.InvlpgStubRejected,
                checkpoint,
                evidence);
        }

        if (byteMode == M01LivePagingByteMode.VerifyExisting)
        {
            evidence.InvlpgReadbackValue =
                OS.Kernel.Memory.X64InvlpgStubPlan.ValidateReadback(
                    cr3CodePage,
                    code.VirtualStart,
                    cr3CodePageCapacity,
                    CreateExpectedInvlpgMaterialization(
                        code,
                        invalidationRecords[0]));
            if (!evidence.InvlpgReadbackValue.ReadBackVerified)
            {
                return Fail(
                    M01LivePagingStatus.InvlpgStubRejected,
                    checkpoint,
                    evidence);
            }

            evidence.InvlpgMaterializationValue =
                CreateExpectedInvlpgMaterialization(
                    code,
                    invalidationRecords[0]);
        }
        else
        {
            evidence.InvlpgMaterializationValue =
                OS.Kernel.Memory.X64InvlpgStubPlan.Emit(
                    cr3CodePage,
                    code.VirtualStart,
                    cr3CodePageCapacity,
                    ToVirtual(code.VirtualStart),
                    InvlpgEntryOffset,
                    invalidationRecords[0]);
        }
        evidence.InvlpgReadbackValue =
            OS.Kernel.Memory.X64InvlpgStubPlan.ValidateReadback(
                cr3CodePage,
                code.VirtualStart,
                cr3CodePageCapacity,
                evidence.InvlpgMaterializationValue);
        if (!evidence.InvlpgMaterializationValue.Succeeded
            || !evidence.InvlpgReadbackValue.ReadBackVerified
            || StubRangesOverlap())
        {
            return Fail(
                M01LivePagingStatus.InvlpgStubRejected,
                checkpoint,
                evidence);
        }

        if (byteMode == M01LivePagingByteMode.VerifyExisting)
        {
            evidence.ProbeReadbackValue =
                OS.Kernel.Memory.X64ProtectionProbeStubPlan
                    .ValidateReadback(
                        protectionProbePage,
                        probe.VirtualStart,
                        protectionProbePageCapacity,
                        CreateExpectedProbeMaterialization(
                            probe,
                            probeReservation));
            if (!evidence.ProbeReadbackValue.ReadBackVerified)
            {
                return Fail(
                    M01LivePagingStatus
                        .ProtectionProbeStubRejected,
                    checkpoint,
                    evidence);
            }

            evidence.ProbeMaterializationValue =
                CreateExpectedProbeMaterialization(
                    probe,
                    probeReservation);
        }
        else
        {
            evidence.ProbeMaterializationValue =
                OS.Kernel.Memory.X64ProtectionProbeStubPlan.Emit(
                    protectionProbePage,
                    probe.VirtualStart,
                    protectionProbePageCapacity,
                    probe,
                    probeReservation,
                    ProtectionProbeEntryOffset);
        }
        evidence.ProbeReadbackValue =
            OS.Kernel.Memory.X64ProtectionProbeStubPlan
                .ValidateReadback(
                    protectionProbePage,
                    probe.VirtualStart,
                    protectionProbePageCapacity,
                    evidence.ProbeMaterializationValue);
        if (!evidence.ProbeMaterializationValue.Succeeded
            || !evidence.ProbeReadbackValue.ReadBackVerified
            || !AuditStubPageLayouts(
                cr3CodePage,
                protectionProbePage))
        {
            return Fail(
                M01LivePagingStatus.ProtectionProbeStubRejected,
                checkpoint,
                evidence);
        }
        checkpoint = M01LivePagingCheckpoint.StubBytesVerified;

        evidence.TableBuildValue = BuildTableImage(
            evidence.MappingPlanValue,
            mappingRanges,
            evidence.MappingPlanValue.OutputRangeCount,
            mappingRangeCapacity,
            tableState,
            code,
            probe,
            stageTwoState->StageOneStateAddress,
            pageImages,
            pageImageCapacity,
            pageTableEntries,
            pageTableEntryCapacity,
            pageTablePreconditions,
            requireStageOneStubMapping: true);
        if (!evidence.TableBuildValue.Succeeded)
        {
            return Fail(
                evidence.TableBuildValue.Status
                    == M01LivePagingTableBuildStatus
                        .InvalidTableStorage
                    ? M01LivePagingStatus.TableStorageRejected
                    : M01LivePagingStatus.TableConstructionRejected,
                checkpoint,
                evidence);
        }
        checkpoint = M01LivePagingCheckpoint.TableImageBuilt;

        PhysicalAddress root =
            evidence.TableBuildValue.RootTable;
        OS.Kernel.Memory.X64PagingModePreflightRequest
            preflightRequest =
                OS.Kernel.Memory.X64PagingModePreflightRequest
                    .CreateEnabled(
                        root,
                        ToPhysical(tableState.PhysicalStart),
                        evidence.MappingPlanValue
                            .TotalTableByteLength);
        evidence.PreflightValue =
            OS.Kernel.Memory.X64PagingModePreflightStubPlan.Evaluate(
                &preflightRequest,
                1,
                1,
                preflightObservation,
                1,
                preflightObservationCapacity,
                &preflightState,
                1,
                1);
        if (!evidence.PreflightValue.Succeeded)
        {
            return Fail(
                M01LivePagingStatus.PreflightRejected,
                checkpoint,
                evidence);
        }
        if ((evidence.PreflightValue.Rflags
                & RflagsDirectionFlagBit) != 0)
        {
            return Fail(
                M01LivePagingStatus.PreflightRejected,
                checkpoint,
                evidence);
        }
        checkpoint = M01LivePagingCheckpoint.PagingModeValidated;

        ulong statePageAddress =
            tableState.VirtualStart
            + evidence.MappingPlanValue.TotalTableByteLength;
        ulong canaryAddress =
            statePageAddress
            + OS.Kernel.Memory.X64Cr3ProbeStorageLayout
                .RequiredStateRecordByteLength;
        if (transitionRuntimeState == null
            || (ulong)transitionRuntimeState != statePageAddress
            || !TryGetSpanEnd(
                tableState.VirtualStart,
                tableState.ByteLength,
                1,
                out ulong tableStateVirtualEnd)
            || canaryAddress < statePageAddress
            || canaryAddress
                > tableStateVirtualEnd
                    - OS.Kernel.Memory.X64Cr3ProbeStorageLayout
                        .RequiredCanaryByteLength
            || ToVirtualValue(
                    statePageAddress,
                    out VirtualAddress stateAddress)
                != AddressValueStatus.Success
            || ToVirtualValue(
                    canaryAddress,
                    out VirtualAddress canaryVirtualAddress)
                != AddressValueStatus.Success)
        {
            return Fail(
                M01LivePagingStatus.CoordinatorStateRejected,
                checkpoint,
                evidence);
        }

        OS.Kernel.Memory.X64Cr3ProbeStorageLayout storage =
            new OS.Kernel.Memory.X64Cr3ProbeStorageLayout(
                stateAddress,
                OS.Kernel.Memory.X64Cr3ProbeStorageLayout
                    .RequiredStateRecordByteLength,
                canaryVirtualAddress,
                OS.Kernel.Memory.X64Cr3ProbeStorageLayout
                    .RequiredCanaryByteLength,
                transitionRuntimeState->ExpectedCanary,
                transitionRuntimeState->ReplacementCanary,
                OS.Kernel.Memory.X64Cr3ProbeStorageProvenance
                    .CallerAttestedOwnedLoaderData);
        evidence.TransitionEvidenceValue =
            OS.Kernel.Memory.X64ReversibleCr3TransitionStubPlan
                .Evaluate(
                    transitionRuntimeState,
                    statePageAddress,
                    OS.Kernel.Memory
                        .X64ReversibleCr3TransitionTerminalKind
                        .Returned);
        if (!evidence.TransitionEvidenceValue.Succeeded)
        {
            return Fail(
                M01LivePagingStatus.TransitionEvidenceRejected,
                checkpoint,
                evidence);
        }

        BootMemoryRegionRecord* cr3Allocations =
            stackalloc BootMemoryRegionRecord[2];
        cr3Allocations[0] = code;
        cr3Allocations[1] = tableState;
        evidence.Cr3PlanValue =
            OS.Kernel.Memory.X64Cr3LoadStubPlan.Create(
                cr3Policy,
                cr3Allocations,
                2,
                2,
                evidence.MappingPlanValue,
                mappingRanges,
                evidence.MappingPlanValue.OutputRangeCount,
                mappingRangeCapacity,
                stackSwitchPlan,
                storage,
                evidence.PreflightValue,
                evidence.PreflightMaterializationValue,
                evidence.PreflightReadbackValue,
                evidence.TransitionMaterializationValue,
                evidence.TransitionReadbackValue,
                evidence.TransitionEvidenceValue,
                PreflightEntryOffset,
                TransitionEntryOffset);
        if (!evidence.Cr3PlanValue.Succeeded)
        {
            return Fail(
                M01LivePagingStatus.Cr3PlanRejected,
                checkpoint,
                evidence);
        }
        checkpoint = M01LivePagingCheckpoint.Cr3PlanValidated;

        evidence.Cr3AuditValue =
            evidence.Cr3PlanValue.Plan.Audit(
                cr3Events,
                cr3EventCount,
                cr3EventCapacity);
        if (!evidence.Cr3AuditValue.Succeeded)
        {
            return Fail(
                M01LivePagingStatus.Cr3TranscriptRejected,
                checkpoint,
                evidence);
        }
        checkpoint = M01LivePagingCheckpoint.Cr3TranscriptValidated;

        OS.Kernel.Memory.PageProtection readExecute =
            (OS.Kernel.Memory.PageProtection)(
                (byte)OS.Kernel.Memory.PageProtection.Read
                | (byte)OS.Kernel.Memory.PageProtection.Execute);
        evidence.ProtectionMutationValue =
            OS.Kernel.Memory.X64PageMap.Apply(
                OS.Kernel.Memory.X64PageMapRequest.CreateProtect(
                    root,
                    ToVirtual(probe.VirtualStart),
                    readExecute,
                    OS.Kernel.Memory.X64HardwareStatePolicy.Preserve,
                    pageTablePreconditions),
                pageImages,
                evidence.TableBuildValue.TablePageCount,
                pageImageCapacity,
                pageTableEntries,
                evidence.TableBuildValue.EntryCount,
                pageTableEntryCapacity,
                null,
                0,
                0);
        if (!evidence.ProtectionMutationValue.Succeeded)
        {
            return Fail(
                M01LivePagingStatus.ProtectionMutationRejected,
                checkpoint,
                evidence);
        }

        if (evidence.ProtectionMutationValue.TargetEntryIndex
            != evidence.TableBuildValue.ProbeLeafEntryIndex)
        {
            _ = RestoreModeledProbeLeaf(
                pageTableEntries,
                evidence.TableBuildValue.EntryCount,
                pageTableEntryCapacity,
                evidence.ProtectionMutationValue);
            return Fail(
                M01LivePagingStatus.ProtectionMutationRejected,
                checkpoint,
                evidence);
        }

        ulong leafReadback =
            pageTableEntries[
                evidence.ProtectionMutationValue.TargetEntryIndex];
        OS.Kernel.Memory.X64InvlpgInvocationRequest
            invlpgRequest =
                OS.Kernel.Memory.X64InvlpgInvocationRequest.Create(
                    evidence.InvlpgReadbackValue.EntryAddress,
                    invalidationRecords[0],
                    readExecute,
                    1);
        OS.Kernel.Memory.X64InvlpgInvocationPlanResult
            invlpgInvocation =
                OS.Kernel.Memory.X64InvlpgStubPlan
                    .CreateInvocationPlan(
                        evidence.InvlpgReadbackValue,
                        invlpgRequest);
        evidence.ProtectionPlanValue =
            OS.Kernel.Memory.X64ProtectionProbeStubPlan.Audit(
                probe,
                probeReservation,
                code,
                tableState,
                (OS.Kernel.Memory.PageProtection)(
                    (byte)OS.Kernel.Memory.PageProtection.Read
                    | (byte)OS.Kernel.Memory.PageProtection.Write),
                evidence.ProbeReadbackValue,
                evidence.ProtectionMutationValue,
                leafReadback,
                evidence.InvlpgReadbackValue,
                invlpgInvocation,
                protectionObservation,
                protectionEvents,
                protectionEventCount,
                protectionEventCapacity);
        bool restoredInitialProbeLeaf =
            RestoreModeledProbeLeaf(
                pageTableEntries,
                evidence.TableBuildValue.EntryCount,
                pageTableEntryCapacity,
                evidence.ProtectionMutationValue);
        if (!evidence.ProtectionPlanValue.Succeeded
            || !restoredInitialProbeLeaf)
        {
            return Fail(
                M01LivePagingStatus.ProtectionProbeRejected,
                checkpoint,
                evidence);
        }
        checkpoint =
            M01LivePagingCheckpoint.ProtectionPlanValidated;

        evidence.PageZeroAuditValue =
            OS.Kernel.Memory.PageZeroMappingPolicy.AuditTableImage(
                root,
                pageTablePreconditions,
                pageImages,
                evidence.TableBuildValue.TablePageCount,
                pageImageCapacity,
                pageTableEntries,
                evidence.TableBuildValue.EntryCount,
                pageTableEntryCapacity);
        if (!evidence.PageZeroAuditValue
                .ProvesPageZeroAbsentInSuppliedImage)
        {
            return Fail(
                M01LivePagingStatus.PageZeroRejected,
                checkpoint,
                evidence);
        }
        checkpoint = M01LivePagingCheckpoint.PageZeroValidated;

        M01LivePagingStaticState candidate = default;
        candidate.HeaderSize =
            (uint)M01LivePagingStaticState.RequiredStorageBytes;
        candidate.HeaderVersion =
            M01LivePagingStaticState.CurrentVersion;
        candidate.CoordinatorStatus = M01LivePagingStatus.Success;
        candidate.CoordinatorCheckpoint =
            M01LivePagingCheckpoint.Completed;
        candidate.StageTwoStateAddressValue = (ulong)stageTwoState;
        candidate.StageTwoStateCapacityValue = stageTwoStateCapacity;
        candidate.RefreshedMapKeyValue =
            mapLedgerEvidence.RefreshedMap.MapKey;
        candidate.RefreshedMapGenerationValue =
            mapLedgerEvidence.RefreshedMapGeneration;
        candidate.Cr3CodeReservationValue = codeReservation;
        candidate.ProtectionProbeReservationValue =
            probeReservation;
        candidate.TableStateReservationValue =
            tableStateReservation;
        candidate.OwnedRootValue = root;
        candidate.RawFirmwareCr3Value =
            evidence.PreflightValue.RawFirmwareCr3;
        candidate.RawCr4Value = evidence.PreflightValue.RawCr4;
        candidate.OriginalRflagsValue =
            evidence.PreflightValue.Rflags;
        candidate.FinalCanaryValue =
            evidence.Cr3AuditValue.FinalCanaryValue;
        candidate.Cr3ElapsedTicksValue =
            evidence.Cr3AuditValue.ElapsedTicks;
        candidate.Cr3EventCountValue =
            evidence.Cr3AuditValue.ValidatedEventCount;
        candidate.ProtectionEventCountValue =
            evidence.ProtectionPlanValue.ValidatedEventCount;
        candidate.TablePageCountValue =
            evidence.TableBuildValue.TablePageCount;
        candidate.MappedPageCountValue =
            evidence.TableBuildValue.MappedPageCount;
        candidate.PageZeroAbsentValue = 1;

        if (!IsCompletedState(candidate))
        {
            return Fail(
                M01LivePagingStatus.IllegalCoordinatorState,
                checkpoint,
                evidence);
        }

        M01LivePagingIntegrationResult result =
            M01LivePagingIntegrationResult.Success(
                evidence,
                candidate);
        if (!result.Succeeded)
        {
            return Fail(
                M01LivePagingStatus.IllegalCoordinatorState,
                checkpoint,
                evidence);
        }

        *staticState = candidate;
        return result;
    }

    public static bool IsCompletedState(
        M01LivePagingStaticState state)
    {
        return state.HeaderSize
                == M01LivePagingStaticState.RequiredStorageBytes
            && state.HeaderVersion
                == M01LivePagingStaticState.CurrentVersion
            && state.CoordinatorStatus
                == M01LivePagingStatus.Success
            && state.CoordinatorCheckpoint
                == M01LivePagingCheckpoint.Completed
            && state.Reserved0 == 0
            && state.Reserved1 == 0
            && state.Reserved2 == 0
            && state.Reserved3 == 0
            && state.Reserved4 == 0
            && state.Reserved5 == 0
            && state.Reserved6 == 0
            && state.Reserved7 == 0
            && state.Reserved8 == 0
            && state.StageTwoStateAddressValue != 0
            && state.StageTwoStateCapacityValue
                == M01LiveAllocatorStaticState.RequiredStorageBytes
            && state.RefreshedMapKeyValue != 0
            && state.RefreshedMapGenerationValue != 0
            && state.Cr3CodeReservationValue.IsValid
            && state.Cr3CodeReservationValue.Kind
                == OS.Kernel.Memory.BootReservationKind
                    .Cr3TransitionStub
            && state.ProtectionProbeReservationValue.IsValid
            && state.ProtectionProbeReservationValue.Kind
                == OS.Kernel.Memory.BootReservationKind
                    .PageProtectionProbe
            && state.TableStateReservationValue.IsValid
            && state.TableStateReservationValue.Kind
                == OS.Kernel.Memory.BootReservationKind.PageTableArena
            && state.OwnedRootValue.IsValid
            && state.RawFirmwareCr3Value != 0
            && state.RawCr4Value != 0
            && state.OriginalRflagsValue != 0
            && state.FinalCanaryValue != 0
            && state.Cr3EventCountValue
                == OS.Kernel.Memory.X64Cr3LoadStubPlan
                    .RequiredEventCount
            && state.ProtectionEventCountValue
                == OS.Kernel.Memory.X64ProtectionProbeStubPlan
                    .RequiredEventCount
            && state.TablePageCountValue != 0
            && state.MappedPageCountValue != 0
            && state.PageZeroAbsentValue == 1;
    }

    public static bool IsPreparedState(
        M01LivePagingPreparationState state)
    {
        return state.HeaderSize
                == M01LivePagingPreparationState.RequiredStorageBytes
            && state.HeaderVersion
                == M01LivePagingPreparationState.CurrentVersion
            && state.PreparationStatus
                == M01LivePagingStatus.Success
            && state.PreparationCheckpoint
                == M01LivePagingCheckpoint
                    .PreparedForCompositeTransition
            && state.ExecutionModeValue
                == M01LivePagingExecutionMode.PreparationOnly
            && state.Reserved0 == 0
            && state.Reserved1 == 0
            && state.Reserved2 == 0
            && state.StageTwoStateAddressValue != 0
            && state.StageTwoStateCountValue == 1
            && state.StageTwoStateCapacityValue == 1
            && state.AllocationTableAddressValue != 0
            && state.AllocationRecordCountValue
                == RequiredAllocationRecordCount
            && state.AllocationRecordCapacityValue
                == state.AllocationRecordCountValue
            && state.ReservationLedgerAddressValue != 0
            && state.ReservationCountValue != 0
            && state.ReservationCountValue
                <= state.ReservationCapacityValue
            && state.ReservationCapacityValue
                <= OS.Kernel.Memory.BootReservationTableValidator
                    .MaximumReservationCount
            && state.MappingRequirementAddressValue != 0
            && state.MappingRequirementCountValue
                == RequiredLiveIntervalMappingRequirementCount
            && state.MappingRequirementCountValue
                <= state.MappingRequirementCapacityValue
            && state.MappingRequirementCapacityValue
                <= OS.Kernel.Boot.X64BootIdentityMappingPlan
                    .MaximumSourceCount
            && state.MappingRangeAddressValue != 0
            && state.MappingRangeCountValue != 0
            && state.MappingRangeCountValue
                <= state.MappingRangeCapacityValue
            && state.MappingRangeCapacityValue
                <= OS.Kernel.Boot.X64BootIdentityMappingPlan
                    .MaximumDestinationCount
            && state.PageImageAddressValue != 0
            && state.PageImageCountValue != 0
            && state.PageImageCountValue
                <= state.PageImageCapacityValue
            && state.PageImageCapacityValue
                <= OS.Kernel.Memory.X64PageMap.MaximumPageImages
            && state.PageEntryAddressValue != 0
            && state.PageEntryCountValue != 0
            && state.PageEntryCountValue
                <= state.PageEntryCapacityValue
            && state.PageEntryCapacityValue
                <= OS.Kernel.Memory.X64PageMap.MaximumEntryCount
            && state.Cr3CodePageAddressValue != 0
            && state.Cr3CodePageCapacityValue == PageSize
            && state.ProtectionProbePageAddressValue != 0
            && state.ProtectionProbePageCapacityValue == PageSize
            && state.PreflightObservationAddressValue != 0
            && state.PreflightObservationCountValue == 1
            && state.PreflightObservationCapacityValue == 1
            && state.RefreshedMapKeyValue != 0
            && state.RefreshedMapGenerationValue != 0
            && state.Cr3CodeReservationValue.IsValid
            && state.Cr3CodeReservationValue.Kind
                == OS.Kernel.Memory.BootReservationKind
                    .Cr3TransitionStub
            && state.ProtectionProbeReservationValue.IsValid
            && state.ProtectionProbeReservationValue.Kind
                == OS.Kernel.Memory.BootReservationKind
                    .PageProtectionProbe
            && state.TableStateReservationValue.IsValid
            && state.TableStateReservationValue.Kind
                == OS.Kernel.Memory.BootReservationKind
                    .PageTableArena
            && state.OwnedRootValue.IsValid
            && state.RawFirmwareCr3Value != 0
            && state.RawCr4Value != 0
            && state.OriginalRflagsValue != 0
            && state.TablePageCountValue != 0
            && state.TablePageCountValue == state.PageImageCountValue
            && state.MappedPageCountValue != 0
            && state.ProbeLeafEntryIndexValue
                < state.PageEntryCountValue
            && state.PageZeroAbsentValue == 1
            && state.StubBytesVerifiedValue == 1
            && state.InitialProbeLeafRestoredValue == 1
            && state.CleanupPlannedValue == 1;
    }

    private static M01LivePagingTableBuildResult BuildTableImage(
        OS.Kernel.Boot.X64BootIdentityMappingPlanResult plan,
        OS.Kernel.Boot.X64BootIdentityMappingRange* ranges,
        ulong rangeCount,
        ulong rangeCapacity,
        BootMemoryRegionRecord tableState,
        BootMemoryRegionRecord code,
        BootMemoryRegionRecord probe,
        ulong stageOneStateAddress,
        OS.Kernel.Memory.X64PageTableImagePage* pageImages,
        ulong pageImageCapacity,
        ulong* entries,
        ulong entryCapacity,
        OS.Kernel.Memory.X64PageTableEncodingPreconditions
            preconditions,
        bool requireStageOneStubMapping)
    {
        if (!plan.Succeeded
            || rangeCount != plan.OutputRangeCount
            || rangeCapacity != plan.DestinationCapacity)
        {
            return TableFailure(
                M01LivePagingTableBuildStatus.InvalidMappingPlan,
                rangeCount);
        }

        if (ranges == null
            || (((ulong)ranges) & (RequiredPointerAlignment - 1)) != 0
            || !TryGetSpanEnd(
                (ulong)ranges,
                rangeCapacity,
                OS.Kernel.Boot.X64BootIdentityMappingRange
                    .StructureSize,
                out ulong rangeEnd))
        {
            return TableFailure(
                M01LivePagingTableBuildStatus
                    .InvalidMappingRangeStorage,
                rangeCount);
        }

        ulong tablePageCount = plan.TotalTablePageCount;
        if (pageImages == null
            || entries == null
            || pageImageCapacity < tablePageCount
            || pageImageCapacity
                > OS.Kernel.Memory.X64PageMap.MaximumPageImages
            || tablePageCount
                > MaximumUnsignedLong
                    / OS.Kernel.Memory.X64PageTableImagePage
                        .EntryCountPerPage)
        {
            return TableFailure(
                M01LivePagingTableBuildStatus.InvalidTableStorage,
                rangeCount);
        }

        ulong requiredEntries =
            tablePageCount
            * OS.Kernel.Memory.X64PageTableImagePage
                .EntryCountPerPage;
        if (entryCapacity < requiredEntries
            || entryCapacity
                > OS.Kernel.Memory.X64PageMap.MaximumEntryCount
            || (((ulong)pageImages)
                & (RequiredPointerAlignment - 1)) != 0
            || (((ulong)entries)
                & (RequiredPointerAlignment - 1)) != 0
            || !TryGetSpanEnd(
                (ulong)pageImages,
                pageImageCapacity,
                OS.Kernel.Memory.X64PageTableImagePage.StructureSize,
                out ulong pageImageEnd)
            || !TryGetSpanEnd(
                (ulong)entries,
                entryCapacity,
                sizeof(ulong),
                out ulong entryEnd))
        {
            return TableFailure(
                M01LivePagingTableBuildStatus.InvalidTableStorage,
                rangeCount);
        }

        if (!AreDisjoint(
                (ulong)ranges,
                rangeEnd,
                (ulong)pageImages,
                pageImageEnd)
            || !AreDisjoint(
                (ulong)ranges,
                rangeEnd,
                (ulong)entries,
                entryEnd)
            || !AreDisjoint(
                (ulong)pageImages,
                pageImageEnd,
                (ulong)entries,
                entryEnd))
        {
            return TableFailure(
                M01LivePagingTableBuildStatus.TableStorageOverlap,
                rangeCount);
        }

        for (ulong index = 0; index < pageImageCapacity; index++)
        {
            pageImages[index] = default;
        }
        for (ulong index = 0; index < entryCapacity; index++)
        {
            entries[index] = 0;
        }

        PhysicalAddress root =
            ToPhysical(tableState.PhysicalStart);
        if (!root.IsValid
            || OS.Kernel.Memory.X64PageTableImagePage.TryCreate(
                root,
                0,
                out pageImages[0])
                != OS.Kernel.Memory.X64PageMapStatus.Success)
        {
            return TableFailure(
                M01LivePagingTableBuildStatus
                    .PageImageCreationFailed,
                rangeCount,
                root: root);
        }

        ulong allocatedTablePages = 1;
        ulong mappedPages = 0;
        ulong probeLeafIndex =
            M01LivePagingTableBuildResult.NoIndex;
        ulong validatedRanges = 0;
        for (ulong rangeIndex = 0;
            rangeIndex < rangeCount;
            rangeIndex++)
        {
            OS.Kernel.Boot.X64BootIdentityMappingRange range =
                ranges[rangeIndex];
            if (!range.IsInitialized
                || !range.PhysicalStart.IsValid
                || !range.VirtualStart.IsValid
                || range.PageCount == 0
                || range.ByteLength
                    != range.PageCount * PageSize
                || range.Protection
                    == OS.Kernel.Memory.PageProtection.None
                || range.CachePolicy
                    == OS.Kernel.Memory.PageCachePolicy.Uninitialized)
            {
                return TableFailure(
                    M01LivePagingTableBuildStatus.InvalidRange,
                    rangeCount,
                    validatedRanges,
                    allocatedTablePages,
                    requiredEntries,
                    mappedPages,
                    rangeIndex,
                    M01LivePagingTableBuildResult.NoIndex,
                    probeLeafIndex,
                    root: root);
            }

            byte protection = (byte)range.Protection;
            if ((protection
                & ((byte)OS.Kernel.Memory.PageProtection.Write
                    | (byte)OS.Kernel.Memory.PageProtection.Execute))
                == ((byte)OS.Kernel.Memory.PageProtection.Write
                    | (byte)OS.Kernel.Memory.PageProtection.Execute))
            {
                return TableFailure(
                    M01LivePagingTableBuildStatus
                        .WritableExecutableRejected,
                    rangeCount,
                    validatedRanges,
                    allocatedTablePages,
                    requiredEntries,
                    mappedPages,
                    rangeIndex,
                    0,
                    probeLeafIndex,
                    root: root);
            }

            range.PhysicalStart
                .TryGetRawValueForHardwareRegisterBoundary(
                    out ulong physicalStart);
            range.VirtualStart
                .TryGetRawValueForHardwareRegisterBoundary(
                    out ulong virtualStart);
            if (physicalStart == 0
                || virtualStart == 0
                || physicalStart != virtualStart)
            {
                return TableFailure(
                    physicalStart == 0 || virtualStart == 0
                        ? M01LivePagingTableBuildStatus
                            .PageZeroRejected
                        : M01LivePagingTableBuildStatus
                            .IdentityAddressMismatch,
                    rangeCount,
                    validatedRanges,
                    allocatedTablePages,
                    requiredEntries,
                    mappedPages,
                    rangeIndex,
                    0,
                    probeLeafIndex,
                    root: root);
            }

            for (ulong pageOrdinal = 0;
                pageOrdinal < range.PageCount;
                pageOrdinal++)
            {
                ulong pageOffset = pageOrdinal * PageSize;
                ulong rawAddress = virtualStart + pageOffset;
                M01LivePagingTableBuildStatus mapStatus =
                    MapIdentityPage(
                        rawAddress,
                        range.Protection,
                        range.CachePolicy,
                        tableState.PhysicalStart,
                        tablePageCount,
                        pageImages,
                        entries,
                        preconditions,
                        ref allocatedTablePages,
                        out ulong leafIndex,
                        out OS.Kernel.Memory
                            .X64PageTableEntryStatus entryStatus);
                if (mapStatus
                    != M01LivePagingTableBuildStatus.Success)
                {
                    return TableFailure(
                        mapStatus,
                        rangeCount,
                        validatedRanges,
                        allocatedTablePages,
                        requiredEntries,
                        mappedPages,
                        rangeIndex,
                        pageOrdinal,
                        probeLeafIndex,
                        entryStatus,
                        root);
                }

                if (rawAddress == probe.VirtualStart)
                {
                    probeLeafIndex = leafIndex;
                }
                mappedPages++;
            }
            validatedRanges++;
        }

        if (allocatedTablePages != tablePageCount)
        {
            return TableFailure(
                M01LivePagingTableBuildStatus
                    .TablePageCountMismatch,
                rangeCount,
                validatedRanges,
                allocatedTablePages,
                requiredEntries,
                mappedPages,
                M01LivePagingTableBuildResult.NoIndex,
                M01LivePagingTableBuildResult.NoIndex,
                probeLeafIndex,
                root: root);
        }

        if (mappedPages != plan.UnionPageCount)
        {
            return TableFailure(
                M01LivePagingTableBuildStatus
                    .MappingPageCountMismatch,
                rangeCount,
                validatedRanges,
                allocatedTablePages,
                requiredEntries,
                mappedPages,
                M01LivePagingTableBuildResult.NoIndex,
                M01LivePagingTableBuildResult.NoIndex,
                probeLeafIndex,
                root: root);
        }

        if (!HasExactLeafProtection(
                code.VirtualStart,
                OS.Kernel.Memory.PageProtection.Read
                    | OS.Kernel.Memory.PageProtection.Execute,
                root,
                pageImages,
                tablePageCount,
                entries,
                requiredEntries,
                preconditions)
            || !HasExactLeafProtection(
                probe.VirtualStart,
                OS.Kernel.Memory.PageProtection.Read
                    | OS.Kernel.Memory.PageProtection.Write,
                root,
                pageImages,
                tablePageCount,
                entries,
                requiredEntries,
                preconditions)
            || !HasExactAllocationProtection(
                tableState,
                OS.Kernel.Memory.PageProtection.Read
                    | OS.Kernel.Memory.PageProtection.Write,
                root,
                pageImages,
                tablePageCount,
                entries,
                requiredEntries,
                preconditions))
        {
            M01LivePagingTableBuildStatus status =
                !HasExactLeafProtection(
                    code.VirtualStart,
                    OS.Kernel.Memory.PageProtection.Read
                        | OS.Kernel.Memory.PageProtection.Execute,
                    root,
                    pageImages,
                    tablePageCount,
                    entries,
                    requiredEntries,
                    preconditions)
                ? M01LivePagingTableBuildStatus
                    .MissingCr3CodeMapping
                : !HasExactLeafProtection(
                    probe.VirtualStart,
                    OS.Kernel.Memory.PageProtection.Read
                        | OS.Kernel.Memory.PageProtection.Write,
                    root,
                    pageImages,
                    tablePageCount,
                    entries,
                    requiredEntries,
                    preconditions)
                    ? M01LivePagingTableBuildStatus
                        .MissingProtectionProbeMapping
                    : M01LivePagingTableBuildStatus
                        .MissingTableStateMapping;
            return TableFailure(
                status,
                rangeCount,
                validatedRanges,
                allocatedTablePages,
                requiredEntries,
                mappedPages,
                M01LivePagingTableBuildResult.NoIndex,
                M01LivePagingTableBuildResult.NoIndex,
                probeLeafIndex,
                root: root);
        }

        if (stageOneStateAddress == 0)
        {
            return TableFailure(
                M01LivePagingTableBuildStatus
                    .MissingOwnedStackMapping,
                rangeCount,
                validatedRanges,
                allocatedTablePages,
                requiredEntries,
                mappedPages,
                M01LivePagingTableBuildResult.NoIndex,
                M01LivePagingTableBuildResult.NoIndex,
                probeLeafIndex,
                root: root);
        }
        M01LoadedImageStackStaticState* stageOne =
            (M01LoadedImageStackStaticState*)stageOneStateAddress;
        OwnedBootStackSwitchPlan stageOnePlan =
            stageOne->SwitchState.Plan;
        if (requireStageOneStubMapping)
        {
            BootMemoryRegionRecord stageOneCode =
                stageOnePlan.CodeAllocation;
            ulong stageOneCodeStart = stageOneCode.VirtualStart;
            for (ulong pageOrdinal = 0;
                pageOrdinal < stageOneCode.PageCount;
                pageOrdinal++)
            {
                if (!HasExactLeafProtection(
                        stageOneCodeStart + pageOrdinal * PageSize,
                        OS.Kernel.Memory.PageProtection.Read
                            | OS.Kernel.Memory.PageProtection.Execute,
                        root,
                        pageImages,
                        tablePageCount,
                        entries,
                        requiredEntries,
                        preconditions))
                {
                    return TableFailure(
                        M01LivePagingTableBuildStatus
                            .MissingStageOneStubMapping,
                        rangeCount,
                        validatedRanges,
                        allocatedTablePages,
                        requiredEntries,
                        mappedPages,
                        M01LivePagingTableBuildResult.NoIndex,
                        pageOrdinal,
                        probeLeafIndex,
                        root: root);
                }
            }
        }

        ulong stackStart = Raw(
            stageOnePlan.StackLayout.StackReservation.PhysicalStart);
        for (ulong pageOrdinal = 0;
            pageOrdinal
                < stageOnePlan.StackLayout.StackReservation.PageCount;
            pageOrdinal++)
        {
            if (!HasWritableNonExecutableLeaf(
                    stackStart + pageOrdinal * PageSize,
                    root,
                    pageImages,
                    tablePageCount,
                    entries,
                    requiredEntries,
                    preconditions))
            {
                return TableFailure(
                    M01LivePagingTableBuildStatus
                        .MissingOwnedStackMapping,
                    rangeCount,
                    validatedRanges,
                    allocatedTablePages,
                    requiredEntries,
                    mappedPages,
                    M01LivePagingTableBuildResult.NoIndex,
                    pageOrdinal,
                    probeLeafIndex,
                    root: root);
            }
        }

        ulong guardStart = Raw(
            stageOnePlan.StackLayout.GuardReservation.PhysicalStart);
        for (ulong pageOrdinal = 0;
            pageOrdinal
                < stageOnePlan.StackLayout.GuardReservation.PageCount;
            pageOrdinal++)
        {
            if (IsPresentLeaf(
                    guardStart + pageOrdinal * PageSize,
                    root,
                    pageImages,
                    tablePageCount,
                    entries,
                    requiredEntries,
                    preconditions))
            {
                return TableFailure(
                    M01LivePagingTableBuildStatus.GuardMapped,
                    rangeCount,
                    validatedRanges,
                    allocatedTablePages,
                    requiredEntries,
                    mappedPages,
                    M01LivePagingTableBuildResult.NoIndex,
                    pageOrdinal,
                    probeLeafIndex,
                    root: root);
            }
        }

        return new M01LivePagingTableBuildResult(
            M01LivePagingTableBuildStatus.Success,
            OS.Kernel.Memory.X64PageTableEntryStatus.Success,
            root,
            rangeCount,
            validatedRanges,
            allocatedTablePages,
            requiredEntries,
            mappedPages,
            M01LivePagingTableBuildResult.NoIndex,
            M01LivePagingTableBuildResult.NoIndex,
            probeLeafIndex);
    }

    private static M01LivePagingTableBuildStatus MapIdentityPage(
        ulong rawAddress,
        OS.Kernel.Memory.PageProtection protection,
        OS.Kernel.Memory.PageCachePolicy cachePolicy,
        ulong tablePhysicalStart,
        ulong tablePageCapacity,
        OS.Kernel.Memory.X64PageTableImagePage* pageImages,
        ulong* entries,
        OS.Kernel.Memory.X64PageTableEncodingPreconditions
            preconditions,
        ref ulong allocatedTablePages,
        out ulong leafEntryIndex,
        out OS.Kernel.Memory.X64PageTableEntryStatus entryStatus)
    {
        leafEntryIndex = M01LivePagingTableBuildResult.NoIndex;
        entryStatus =
            OS.Kernel.Memory.X64PageTableEntryStatus.Success;
        if (rawAddress == 0
            || (rawAddress & (PageSize - 1)) != 0)
        {
            return M01LivePagingTableBuildStatus.PageZeroRejected;
        }

        ulong currentPageIndex = 0;
        OS.Kernel.Memory.X64PageTableLevel level =
            OS.Kernel.Memory.X64PageTableLevel.Pml4e;
        for (uint depth = 0; depth < 3; depth++)
        {
            uint entryOffset = GetPageTableIndex(rawAddress, level);
            ulong entryIndex =
                pageImages[currentPageIndex].EntryStartIndex
                + entryOffset;
            ulong rawEntry = entries[entryIndex];
            ulong childPageIndex;
            if (rawEntry == 0)
            {
                if (allocatedTablePages >= tablePageCapacity)
                {
                    return M01LivePagingTableBuildStatus
                        .TablePageCapacityExhausted;
                }

                childPageIndex = allocatedTablePages;
                ulong childPhysicalRaw =
                    tablePhysicalStart
                    + childPageIndex * PageSize;
                PhysicalAddress childPhysical =
                    ToPhysical(childPhysicalRaw);
                if (!childPhysical.IsValid
                    || OS.Kernel.Memory.X64PageTableImagePage
                        .TryCreate(
                            childPhysical,
                            childPageIndex
                                * OS.Kernel.Memory
                                    .X64PageTableImagePage
                                    .EntryCountPerPage,
                            out pageImages[childPageIndex])
                        != OS.Kernel.Memory.X64PageMapStatus.Success)
                {
                    return M01LivePagingTableBuildStatus
                        .PageImageCreationFailed;
                }

                OS.Kernel.Memory.X64PageTableEntryResult encoded =
                    OS.Kernel.Memory.X64PageTableEntry.TryEncode(
                        new OS.Kernel.Memory.X64PageTableEntryRequest(
                            level,
                            OS.Kernel.Memory
                                .X64PageTableEntryKind.TablePointer,
                            childPhysical,
                            OS.Kernel.Memory.PageProtection.None,
                            OS.Kernel.Memory.PageCachePolicy.WriteBack),
                        preconditions);
                if (!encoded.Succeeded)
                {
                    entryStatus = encoded.Status;
                    return M01LivePagingTableBuildStatus
                        .EntryEncodingFailed;
                }

                entries[entryIndex] = encoded.Entry.RawValue;
                allocatedTablePages++;
            }
            else
            {
                OS.Kernel.Memory.X64PageTableEntryResult decoded =
                    OS.Kernel.Memory.X64PageTableEntry.TryDecode(
                        level,
                        rawEntry,
                        preconditions);
                if (!decoded.Succeeded
                    || decoded.Kind
                        != OS.Kernel.Memory
                            .X64PageTableEntryKind.TablePointer)
                {
                    entryStatus = decoded.Status;
                    return M01LivePagingTableBuildStatus
                        .MalformedExistingEntry;
                }

                childPageIndex = FindPageImage(
                    pageImages,
                    allocatedTablePages,
                    decoded.PhysicalAddress);
                if (childPageIndex
                    == M01LivePagingTableBuildResult.NoIndex)
                {
                    return M01LivePagingTableBuildStatus
                        .MissingChildPage;
                }
            }

            currentPageIndex = childPageIndex;
            level = level
                == OS.Kernel.Memory.X64PageTableLevel.Pml4e
                ? OS.Kernel.Memory.X64PageTableLevel.Pdpte
                : level
                    == OS.Kernel.Memory.X64PageTableLevel.Pdpte
                    ? OS.Kernel.Memory.X64PageTableLevel.Pde
                    : OS.Kernel.Memory.X64PageTableLevel.Pte;
        }

        leafEntryIndex =
            pageImages[currentPageIndex].EntryStartIndex
            + GetPageTableIndex(
                rawAddress,
                OS.Kernel.Memory.X64PageTableLevel.Pte);
        if (entries[leafEntryIndex] != 0)
        {
            return M01LivePagingTableBuildStatus.DuplicateLeaf;
        }

        PhysicalAddress physical = ToPhysical(rawAddress);
        OS.Kernel.Memory.X64PageTableEntryResult leaf =
            OS.Kernel.Memory.X64PageTableEntry.TryEncode(
                new OS.Kernel.Memory.X64PageTableEntryRequest(
                    OS.Kernel.Memory.X64PageTableLevel.Pte,
                    OS.Kernel.Memory.X64PageTableEntryKind.Leaf,
                    physical,
                    protection,
                    cachePolicy),
                preconditions);
        if (!leaf.Succeeded)
        {
            entryStatus = leaf.Status;
            return M01LivePagingTableBuildStatus.EntryEncodingFailed;
        }

        entries[leafEntryIndex] = leaf.Entry.RawValue;
        return M01LivePagingTableBuildStatus.Success;
    }

    private static bool HasExactLeafProtection(
        ulong rawVirtual,
        OS.Kernel.Memory.PageProtection expected,
        PhysicalAddress root,
        OS.Kernel.Memory.X64PageTableImagePage* pages,
        ulong pageCount,
        ulong* entries,
        ulong entryCount,
        OS.Kernel.Memory.X64PageTableEncodingPreconditions
            preconditions)
    {
        if (!TryReadLeaf(
                rawVirtual,
                root,
                pages,
                pageCount,
                entries,
                entryCount,
                preconditions,
                out OS.Kernel.Memory.X64PageTableEntryResult leaf))
        {
            return false;
        }

        return leaf.Kind
                == OS.Kernel.Memory.X64PageTableEntryKind.Leaf
            && leaf.Protection == expected
            && leaf.CachePolicy
                == OS.Kernel.Memory.PageCachePolicy.WriteBack;
    }

    private static bool HasExactAllocationProtection(
        BootMemoryRegionRecord allocation,
        OS.Kernel.Memory.PageProtection expected,
        PhysicalAddress root,
        OS.Kernel.Memory.X64PageTableImagePage* pages,
        ulong pageCount,
        ulong* entries,
        ulong entryCount,
        OS.Kernel.Memory.X64PageTableEncodingPreconditions
            preconditions)
    {
        if (allocation.PageCount == 0
            || allocation.VirtualStart == 0
            || allocation.PageCount
                > MaximumUnsignedLong / PageSize)
        {
            return false;
        }

        for (ulong pageOrdinal = 0;
            pageOrdinal < allocation.PageCount;
            pageOrdinal++)
        {
            if (!HasExactLeafProtection(
                    allocation.VirtualStart
                        + pageOrdinal * PageSize,
                    expected,
                    root,
                    pages,
                    pageCount,
                    entries,
                    entryCount,
                    preconditions))
            {
                return false;
            }
        }
        return true;
    }

    private static bool RestoreModeledProbeLeaf(
        ulong* entries,
        ulong entryCount,
        ulong entryCapacity,
        OS.Kernel.Memory.X64PageMapResult mutation)
    {
        if (entries == null
            || !mutation.Succeeded
            || mutation.TargetEntryIndex >= entryCount
            || entryCount > entryCapacity
            || entries[mutation.TargetEntryIndex]
                != mutation.ReplacementEntry.RawValue)
        {
            return false;
        }

        entries[mutation.TargetEntryIndex] =
            mutation.PreviousEntry.RawValue;
        return entries[mutation.TargetEntryIndex]
            == mutation.PreviousEntry.RawValue;
    }

    private static bool HasWritableNonExecutableLeaf(
        ulong rawVirtual,
        PhysicalAddress root,
        OS.Kernel.Memory.X64PageTableImagePage* pages,
        ulong pageCount,
        ulong* entries,
        ulong entryCount,
        OS.Kernel.Memory.X64PageTableEncodingPreconditions
            preconditions)
    {
        if (!TryReadLeaf(
                rawVirtual,
                root,
                pages,
                pageCount,
                entries,
                entryCount,
                preconditions,
                out OS.Kernel.Memory.X64PageTableEntryResult leaf))
        {
            return false;
        }

        byte protection = (byte)leaf.Protection;
        return (protection
                & (byte)OS.Kernel.Memory.PageProtection.Write) != 0
            && (protection
                & (byte)OS.Kernel.Memory.PageProtection.Execute) == 0;
    }

    private static bool IsPresentLeaf(
        ulong rawVirtual,
        PhysicalAddress root,
        OS.Kernel.Memory.X64PageTableImagePage* pages,
        ulong pageCount,
        ulong* entries,
        ulong entryCount,
        OS.Kernel.Memory.X64PageTableEncodingPreconditions
            preconditions)
    {
        return TryReadLeaf(
            rawVirtual,
            root,
            pages,
            pageCount,
            entries,
            entryCount,
            preconditions,
            out OS.Kernel.Memory.X64PageTableEntryResult leaf)
            && leaf.Kind
                == OS.Kernel.Memory.X64PageTableEntryKind.Leaf;
    }

    private static bool TryReadLeaf(
        ulong rawVirtual,
        PhysicalAddress root,
        OS.Kernel.Memory.X64PageTableImagePage* pages,
        ulong pageCount,
        ulong* entries,
        ulong entryCount,
        OS.Kernel.Memory.X64PageTableEncodingPreconditions
            preconditions,
        out OS.Kernel.Memory.X64PageTableEntryResult leaf)
    {
        leaf = default;
        if (rawVirtual == 0
            || !root.IsValid
            || pages == null
            || entries == null)
        {
            return false;
        }

        ulong currentPageIndex =
            FindPageImage(pages, pageCount, root);
        if (currentPageIndex
            == M01LivePagingTableBuildResult.NoIndex)
        {
            return false;
        }

        OS.Kernel.Memory.X64PageTableLevel level =
            OS.Kernel.Memory.X64PageTableLevel.Pml4e;
        for (uint depth = 0; depth < 3; depth++)
        {
            ulong entryIndex =
                pages[currentPageIndex].EntryStartIndex
                + GetPageTableIndex(rawVirtual, level);
            if (entryIndex >= entryCount)
            {
                return false;
            }

            OS.Kernel.Memory.X64PageTableEntryResult decoded =
                OS.Kernel.Memory.X64PageTableEntry.TryDecode(
                    level,
                    entries[entryIndex],
                    preconditions);
            if (!decoded.Succeeded
                || decoded.Kind
                    != OS.Kernel.Memory
                        .X64PageTableEntryKind.TablePointer)
            {
                return false;
            }

            currentPageIndex = FindPageImage(
                pages,
                pageCount,
                decoded.PhysicalAddress);
            if (currentPageIndex
                == M01LivePagingTableBuildResult.NoIndex)
            {
                return false;
            }

            level = level
                == OS.Kernel.Memory.X64PageTableLevel.Pml4e
                ? OS.Kernel.Memory.X64PageTableLevel.Pdpte
                : level
                    == OS.Kernel.Memory.X64PageTableLevel.Pdpte
                    ? OS.Kernel.Memory.X64PageTableLevel.Pde
                    : OS.Kernel.Memory.X64PageTableLevel.Pte;
        }

        ulong leafIndex =
            pages[currentPageIndex].EntryStartIndex
            + GetPageTableIndex(
                rawVirtual,
                OS.Kernel.Memory.X64PageTableLevel.Pte);
        if (leafIndex >= entryCount)
        {
            return false;
        }

        leaf = OS.Kernel.Memory.X64PageTableEntry.TryDecode(
            OS.Kernel.Memory.X64PageTableLevel.Pte,
            entries[leafIndex],
            preconditions);
        return leaf.Succeeded;
    }

    private static ulong FindPageImage(
        OS.Kernel.Memory.X64PageTableImagePage* pages,
        ulong pageCount,
        PhysicalAddress physical)
    {
        for (ulong index = 0; index < pageCount; index++)
        {
            if (pages[index].PhysicalStart.Equals(physical))
            {
                return index;
            }
        }
        return M01LivePagingTableBuildResult.NoIndex;
    }

    private static uint GetPageTableIndex(
        ulong rawVirtual,
        OS.Kernel.Memory.X64PageTableLevel level)
    {
        int shift = level
            == OS.Kernel.Memory.X64PageTableLevel.Pml4e
            ? 39
            : level == OS.Kernel.Memory.X64PageTableLevel.Pdpte
                ? 30
                : level == OS.Kernel.Memory.X64PageTableLevel.Pde
                    ? 21
                    : 12;
        return (uint)((rawVirtual >> shift) & 0x1ffUL);
    }

    private static OS.Kernel.Memory.X64InvlpgStubPlanResult
        CreateExpectedInvlpgMaterialization(
            BootMemoryRegionRecord code,
            OS.Kernel.Memory.X64TlbInvalidationRecord target)
    {
        if (ToVirtual(code.VirtualStart).TryGetEndExclusive(
                code.ByteLength,
                out VirtualAddress codeEnd)
                != AddressValueStatus.Success
            || ToVirtual(code.VirtualStart).TryAdd(
                InvlpgEntryOffset,
                out VirtualAddress entry)
                != AddressValueStatus.Success)
        {
            return OS.Kernel.Memory.X64InvlpgStubPlanResult.Fail(
                OS.Kernel.Memory.X64InvlpgStubPlanStatus
                    .DestinationSpanOverflow,
                target,
                code.ByteLength);
        }

        return OS.Kernel.Memory.X64InvlpgStubPlanResult.Success(
            OS.Kernel.Memory.X64InvlpgStubPhase.Materialized,
            ToVirtual(code.VirtualStart),
            codeEnd,
            entry,
            InvlpgEntryOffset,
            target,
            code.ByteLength);
    }

    private static OS.Kernel.Memory
        .X64ProtectionProbeStubPlanResult
        CreateExpectedProbeMaterialization(
            BootMemoryRegionRecord probe,
            OS.Kernel.Memory.BootReservedRange reservation)
    {
        if (!reservation.IsValid
            || ToVirtual(probe.VirtualStart).TryGetEndExclusive(
                probe.ByteLength,
                out VirtualAddress probeEnd)
                != AddressValueStatus.Success
            || ToVirtual(probe.VirtualStart).TryAdd(
                ProtectionProbeEntryOffset,
                out VirtualAddress entry)
                != AddressValueStatus.Success)
        {
            return OS.Kernel.Memory
                .X64ProtectionProbeStubPlanResult.Fail(
                    OS.Kernel.Memory.X64ProtectionProbeStatus
                        .InvalidProbeReservation,
                    probe.ByteLength);
        }

        return OS.Kernel.Memory
            .X64ProtectionProbeStubPlanResult.Success(
                OS.Kernel.Memory
                    .X64ProtectionProbeStubPhase.Materialized,
                ToVirtual(probe.VirtualStart),
                probeEnd,
                entry,
                ProtectionProbeEntryOffset,
                probe.ByteLength);
    }

    private static bool AuditStubPageLayouts(
        byte* codePage,
        byte* probePage)
    {
        if (codePage == null || probePage == null)
        {
            return false;
        }

        ulong preflightEnd =
            PreflightEntryOffset
            + OS.Kernel.Memory.X64PagingModePreflightStubPlan
                .RequiredByteLength;
        ulong transitionEnd =
            TransitionEntryOffset
            + OS.Kernel.Memory.X64ReversibleCr3TransitionStubPlan
                .RequiredByteLength;
        ulong invlpgEnd =
            InvlpgEntryOffset
            + OS.Kernel.Memory.X64InvlpgStubPlan
                .RequiredByteLength;
        ulong probeEnd =
            ProtectionProbeEntryOffset
            + OS.Kernel.Memory.X64ProtectionProbeStubPlan
                .RequiredByteLength;

        for (ulong index = 0; index < PageSize; index++)
        {
            bool belongsToCodeStub =
                (index >= PreflightEntryOffset
                    && index < preflightEnd)
                || (index >= TransitionEntryOffset
                    && index < transitionEnd)
                || (index >= InvlpgEntryOffset
                    && index < invlpgEnd);
            if (!belongsToCodeStub && codePage[index] != 0)
            {
                return false;
            }

            bool belongsToProbeStub =
                index >= ProtectionProbeEntryOffset
                && index < probeEnd;
            if (!belongsToProbeStub && probePage[index] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static void ZeroBytes(byte* destination, ulong length)
    {
        for (ulong index = 0; index < length; index++)
        {
            destination[index] = 0;
        }
    }

    private static bool StubRangesOverlap()
    {
        ulong preflightEnd =
            PreflightEntryOffset
            + OS.Kernel.Memory.X64PagingModePreflightStubPlan
                .RequiredByteLength;
        ulong transitionEnd =
            TransitionEntryOffset
            + OS.Kernel.Memory.X64ReversibleCr3TransitionStubPlan
                .RequiredByteLength;
        ulong invlpgEnd =
            InvlpgEntryOffset
            + OS.Kernel.Memory.X64InvlpgStubPlan
                .RequiredByteLength;
        return preflightEnd > TransitionEntryOffset
            || transitionEnd > InvlpgEntryOffset
            || invlpgEnd > PageSize;
    }

    private static bool ValidateStageTwoState(
        M01LiveAllocatorStaticState* state,
        ulong capacity,
        out ulong start,
        out ulong end)
    {
        start = (ulong)state;
        end = 0;
        return state != null
            && capacity
                == M01LiveAllocatorStaticState.RequiredStorageBytes
            && (start & (RequiredPointerAlignment - 1)) == 0
            && TryGetSpanEnd(start, capacity, 1, out end)
            && state->IsCompleted
            && state->StageOneStateAddress != 0
            && state->StageOneStateCapacity
                == M01LoadedImageStackStaticState
                    .RequiredStorageBytes;
    }

    private static bool ValidatePreparationBufferTopology(
        ulong stageOneStart,
        ulong stageOneEnd,
        ulong stageTwoStart,
        ulong stageTwoEnd,
        ulong allocationTableStart,
        ulong allocationTableEnd,
        ulong reservationTableStart,
        ulong reservationTableEnd,
        ulong preparationStateStart,
        ulong preparationStateEnd,
        FirmwareMemoryMapRecord map,
        OS.Kernel.Boot.X64BootIdentityMappingRequirement*
            mappingRequirements,
        ulong mappingRequirementCount,
        ulong mappingRequirementCapacity,
        OS.Kernel.Boot.X64BootIdentityMappingRange* mappingRanges,
        ulong mappingRangeCapacity,
        OS.Kernel.Memory.X64PageTableImagePage* pageImages,
        ulong pageImageCapacity,
        ulong* pageTableEntries,
        ulong pageTableEntryCapacity,
        byte* cr3CodePage,
        ulong cr3CodePageCapacity,
        BootMemoryRegionRecord code,
        byte* protectionProbePage,
        ulong protectionProbePageCapacity,
        BootMemoryRegionRecord probe,
        BootMemoryRegionRecord tableState,
        OS.Kernel.Memory.X64PagingModePreflightObservation*
            preflightObservation,
        ulong preflightObservationCount,
        ulong preflightObservationCapacity)
    {
        if (mappingRequirementCount == 0
            || mappingRequirementCount > mappingRequirementCapacity
            || mappingRequirementCapacity
                > OS.Kernel.Boot.X64BootIdentityMappingPlan
                    .MaximumSourceCount
            || mappingRangeCapacity == 0
            || mappingRangeCapacity
                > OS.Kernel.Boot.X64BootIdentityMappingPlan
                    .MaximumDestinationCount
            || pageImageCapacity == 0
            || pageImageCapacity
                > OS.Kernel.Memory.X64PageMap.MaximumPageImages
            || pageTableEntryCapacity == 0
            || pageTableEntryCapacity
                > OS.Kernel.Memory.X64PageMap.MaximumEntryCount
            || cr3CodePageCapacity != PageSize
            || protectionProbePageCapacity != PageSize
            || (ulong)cr3CodePage != code.VirtualStart
            || (ulong)protectionProbePage != probe.VirtualStart
            || preflightObservationCount != 1
            || preflightObservationCapacity != 1)
        {
            return false;
        }

        CoordinatorStorageSpan* spans =
            stackalloc CoordinatorStorageSpan[15];
        ulong spanCount = 0;
        return TryAddExistingSpan(
                spans,
                ref spanCount,
                15,
                stageOneStart,
                stageOneEnd)
            && TryAddExistingSpan(
                spans,
                ref spanCount,
                15,
                stageTwoStart,
                stageTwoEnd)
            && TryAddExistingSpan(
                spans,
                ref spanCount,
                15,
                allocationTableStart,
                allocationTableEnd)
            && TryAddExistingSpan(
                spans,
                ref spanCount,
                15,
                reservationTableStart,
                reservationTableEnd)
            && TryAddExistingSpan(
                spans,
                ref spanCount,
                15,
                preparationStateStart,
                preparationStateEnd)
            && TryAddStorageSpan(
                spans,
                ref spanCount,
                15,
                map.BufferAddress,
                map.BufferSize,
                1,
                RequiredPointerAlignment)
            && TryAddStorageSpan(
                spans,
                ref spanCount,
                15,
                map.SourceBufferAddress,
                map.SourceBufferSize,
                1,
                RequiredPointerAlignment)
            && TryAddStorageSpan(
                spans,
                ref spanCount,
                15,
                (ulong)mappingRequirements,
                mappingRequirementCapacity,
                OS.Kernel.Boot.X64BootIdentityMappingRequirement
                    .StructureSize,
                RequiredPointerAlignment)
            && TryAddStorageSpan(
                spans,
                ref spanCount,
                15,
                (ulong)mappingRanges,
                mappingRangeCapacity,
                OS.Kernel.Boot.X64BootIdentityMappingRange
                    .StructureSize,
                RequiredPointerAlignment)
            && TryAddStorageSpan(
                spans,
                ref spanCount,
                15,
                (ulong)pageImages,
                pageImageCapacity,
                OS.Kernel.Memory.X64PageTableImagePage.StructureSize,
                RequiredPointerAlignment)
            && TryAddStorageSpan(
                spans,
                ref spanCount,
                15,
                (ulong)pageTableEntries,
                pageTableEntryCapacity,
                sizeof(ulong),
                RequiredPointerAlignment)
            && TryAddStorageSpan(
                spans,
                ref spanCount,
                15,
                (ulong)cr3CodePage,
                cr3CodePageCapacity,
                1,
                PageSize)
            && TryAddStorageSpan(
                spans,
                ref spanCount,
                15,
                (ulong)protectionProbePage,
                protectionProbePageCapacity,
                1,
                PageSize)
            && TryAddStorageSpan(
                spans,
                ref spanCount,
                15,
                (ulong)preflightObservation,
                preflightObservationCapacity,
                OS.Kernel.Memory.X64PagingModePreflightObservation
                    .StructureSize,
                RequiredPointerAlignment)
            && TryAddStorageSpan(
                spans,
                ref spanCount,
                15,
                tableState.VirtualStart,
                tableState.ByteLength,
                1,
                PageSize);
    }

    private static bool ValidateCoordinatorBufferTopology(
        ulong stageOneStart,
        ulong stageOneEnd,
        ulong stageTwoStart,
        ulong stageTwoEnd,
        ulong allocationTableStart,
        ulong allocationTableEnd,
        ulong reservationTableStart,
        ulong reservationTableEnd,
        ulong staticStateStart,
        ulong staticStateEnd,
        FirmwareMemoryMapRecord map,
        OS.Kernel.Boot.X64BootIdentityMappingRequirement*
            mappingRequirements,
        ulong mappingRequirementCount,
        ulong mappingRequirementCapacity,
        OS.Kernel.Boot.X64BootIdentityMappingRange* mappingRanges,
        ulong mappingRangeCapacity,
        OS.Kernel.Memory.X64PageTableImagePage* pageImages,
        ulong pageImageCapacity,
        ulong* pageTableEntries,
        ulong pageTableEntryCapacity,
        byte* cr3CodePage,
        ulong cr3CodePageCapacity,
        BootMemoryRegionRecord code,
        byte* protectionProbePage,
        ulong protectionProbePageCapacity,
        BootMemoryRegionRecord probe,
        BootMemoryRegionRecord tableState,
        OS.Kernel.Memory.X64PagingModePreflightObservation*
            preflightObservation,
        ulong preflightObservationCapacity,
        OS.Kernel.Memory.X64ReversibleCr3TransitionRuntimeState*
            transitionRuntimeState,
        OS.Kernel.Memory.X64Cr3ProbeEvent* cr3Events,
        ulong cr3EventCount,
        ulong cr3EventCapacity,
        OS.Kernel.Memory.X64ProtectionProbeEvent* protectionEvents,
        ulong protectionEventCount,
        ulong protectionEventCapacity)
    {
        if (mappingRequirementCount == 0
            || mappingRequirementCount > mappingRequirementCapacity
            || mappingRequirementCapacity
                > OS.Kernel.Boot.X64BootIdentityMappingPlan
                    .MaximumSourceCount
            || mappingRangeCapacity == 0
            || mappingRangeCapacity
                > OS.Kernel.Boot.X64BootIdentityMappingPlan
                    .MaximumDestinationCount
            || pageImageCapacity == 0
            || pageImageCapacity
                > OS.Kernel.Memory.X64PageMap.MaximumPageImages
            || pageTableEntryCapacity == 0
            || pageTableEntryCapacity
                > OS.Kernel.Memory.X64PageMap.MaximumEntryCount
            || cr3CodePageCapacity != PageSize
            || protectionProbePageCapacity != PageSize
            || (ulong)cr3CodePage != code.VirtualStart
            || (ulong)protectionProbePage != probe.VirtualStart
            || preflightObservationCapacity != 1
            || transitionRuntimeState == null
            || (((ulong)transitionRuntimeState)
                & (RequiredPointerAlignment - 1)) != 0
            || (ulong)transitionRuntimeState
                < tableState.VirtualStart
            || !TryGetSpanEnd(
                (ulong)transitionRuntimeState,
                1,
                OS.Kernel.Memory
                    .X64ReversibleCr3TransitionRuntimeState
                    .StructureSize
                    + OS.Kernel.Memory.X64Cr3ProbeStorageLayout
                        .RequiredCanaryByteLength,
                out ulong transitionStorageEnd)
            || !TryGetSpanEnd(
                tableState.VirtualStart,
                tableState.ByteLength,
                1,
                out ulong tableStateVirtualEnd)
            || transitionStorageEnd
                > tableStateVirtualEnd
            || cr3EventCount > cr3EventCapacity
            || cr3EventCapacity == 0
            || cr3EventCapacity
                > OS.Kernel.Memory.X64Cr3LoadStubPlan
                    .MaximumEventCount
            || protectionEventCount > protectionEventCapacity
            || protectionEventCapacity == 0
            || protectionEventCapacity
                > OS.Kernel.Memory.X64ProtectionProbeStubPlan
                    .RequiredEventCount)
        {
            return false;
        }

        CoordinatorStorageSpan* spans =
            stackalloc CoordinatorStorageSpan[17];
        ulong spanCount = 0;
        return TryAddExistingSpan(
                spans,
                ref spanCount,
                17,
                stageOneStart,
                stageOneEnd)
            && TryAddExistingSpan(
                spans,
                ref spanCount,
                17,
                stageTwoStart,
                stageTwoEnd)
            && TryAddExistingSpan(
                spans,
                ref spanCount,
                17,
                allocationTableStart,
                allocationTableEnd)
            && TryAddExistingSpan(
                spans,
                ref spanCount,
                17,
                reservationTableStart,
                reservationTableEnd)
            && TryAddExistingSpan(
                spans,
                ref spanCount,
                17,
                staticStateStart,
                staticStateEnd)
            && TryAddStorageSpan(
                spans,
                ref spanCount,
                17,
                map.BufferAddress,
                map.BufferSize,
                1,
                RequiredPointerAlignment)
            && TryAddStorageSpan(
                spans,
                ref spanCount,
                17,
                map.SourceBufferAddress,
                map.SourceBufferSize,
                1,
                RequiredPointerAlignment)
            && TryAddStorageSpan(
                spans,
                ref spanCount,
                17,
                (ulong)mappingRequirements,
                mappingRequirementCapacity,
                OS.Kernel.Boot.X64BootIdentityMappingRequirement
                    .StructureSize,
                RequiredPointerAlignment)
            && TryAddStorageSpan(
                spans,
                ref spanCount,
                17,
                (ulong)mappingRanges,
                mappingRangeCapacity,
                OS.Kernel.Boot.X64BootIdentityMappingRange
                    .StructureSize,
                RequiredPointerAlignment)
            && TryAddStorageSpan(
                spans,
                ref spanCount,
                17,
                (ulong)pageImages,
                pageImageCapacity,
                OS.Kernel.Memory.X64PageTableImagePage.StructureSize,
                RequiredPointerAlignment)
            && TryAddStorageSpan(
                spans,
                ref spanCount,
                17,
                (ulong)pageTableEntries,
                pageTableEntryCapacity,
                sizeof(ulong),
                RequiredPointerAlignment)
            && TryAddStorageSpan(
                spans,
                ref spanCount,
                17,
                (ulong)cr3CodePage,
                cr3CodePageCapacity,
                1,
                PageSize)
            && TryAddStorageSpan(
                spans,
                ref spanCount,
                17,
                (ulong)protectionProbePage,
                protectionProbePageCapacity,
                1,
                PageSize)
            && TryAddStorageSpan(
                spans,
                ref spanCount,
                17,
                (ulong)preflightObservation,
                1,
                OS.Kernel.Memory.X64PagingModePreflightObservation
                    .StructureSize,
                RequiredPointerAlignment)
            && TryAddStorageSpan(
                spans,
                ref spanCount,
                17,
                tableState.VirtualStart,
                tableState.ByteLength,
                1,
                PageSize)
            && TryAddStorageSpan(
                spans,
                ref spanCount,
                17,
                (ulong)cr3Events,
                cr3EventCapacity,
                OS.Kernel.Memory.X64Cr3ProbeEvent.StructureSize,
                RequiredPointerAlignment)
            && TryAddStorageSpan(
                spans,
                ref spanCount,
                17,
                (ulong)protectionEvents,
                protectionEventCapacity,
                OS.Kernel.Memory.X64ProtectionProbeEvent.StructureSize,
                RequiredPointerAlignment);
    }

    private static bool TryAddExistingSpan(
        CoordinatorStorageSpan* spans,
        ref ulong spanCount,
        ulong spanCapacity,
        ulong start,
        ulong end)
    {
        if (start == 0 || end <= start)
        {
            return false;
        }

        return TryAddCheckedSpan(
            spans,
            ref spanCount,
            spanCapacity,
            start,
            end);
    }

    private static bool TryAddStorageSpan(
        CoordinatorStorageSpan* spans,
        ref ulong spanCount,
        ulong spanCapacity,
        ulong start,
        ulong elementCount,
        ulong elementSize,
        ulong alignment)
    {
        if (start == 0
            || elementCount == 0
            || elementSize == 0
            || alignment == 0
            || (alignment & (alignment - 1)) != 0
            || (start & (alignment - 1)) != 0
            || !TryGetSpanEnd(
                start,
                elementCount,
                elementSize,
                out ulong end)
            || end <= start)
        {
            return false;
        }

        return TryAddCheckedSpan(
            spans,
            ref spanCount,
            spanCapacity,
            start,
            end);
    }

    private static bool TryAddCheckedSpan(
        CoordinatorStorageSpan* spans,
        ref ulong spanCount,
        ulong spanCapacity,
        ulong start,
        ulong end)
    {
        if (spans == null || spanCount >= spanCapacity)
        {
            return false;
        }

        for (ulong index = 0; index < spanCount; index++)
        {
            if (!AreDisjoint(
                    start,
                    end,
                    spans[index].Start,
                    spans[index].End))
            {
                return false;
            }
        }

        spans[spanCount].Start = start;
        spans[spanCount].End = end;
        spanCount++;
        return true;
    }

    private static bool ValidateStageOneState(
        M01LoadedImageStackStaticState* state,
        ulong capacity,
        OwnedBootStackSwitchPlanResult stackSwitchPlan,
        out ulong start,
        out ulong end)
    {
        start = (ulong)state;
        end = 0;
        if (state == null
            || capacity
                != M01LoadedImageStackStaticState.RequiredStorageBytes
            || (start
                & (M01LoadedImageStackStaticState
                    .RequiredStorageAlignment - 1)) != 0
            || !TryGetSpanEnd(start, capacity, 1, out end)
            || !stackSwitchPlan.Succeeded)
        {
            return false;
        }

        OwnedBootStackSwitchPlan plan = state->SwitchState.Plan;
        return state->IsInitialized
            && state->Status
                == M01LoadedImageStackStatus.Success
            && state->Checkpoint
                == M01LoadedImageStackCheckpoint
                    .ContinuationRspContained
            && state->SwitchState.State
                == OwnedBootStackSwitchState.ContinuationObserved
            && state->StackLayout.Lifetime
                == OwnedBootStackLifetime.Active
            && (ulong)&state->HandoffStorage - start
                == M01LoadedImageStackStaticState
                    .HandoffStorageOffset
            && (ulong)&state->SwitchStateStorage - start
                == M01LoadedImageStackStaticState
                    .SwitchStateStorageOffset
            && state->SwitchState.StorageAddress
                == (ulong)&state->SwitchStateStorage
            && (ulong)&state->StackLayoutStorage - start
                == M01LoadedImageStackStaticState
                    .StackLayoutStorageOffset
            && plan.IsValid
            && plan.Equals(stackSwitchPlan.Plan);
    }

    private static bool ValidateAllocationStorage(
        BootMemoryRegionRecord* records,
        ulong count,
        ulong capacity,
        out ulong start,
        out ulong end)
    {
        start = (ulong)records;
        end = 0;
        return count == RequiredAllocationRecordCount
            && capacity == count
            && capacity <= MaximumAllocationRecordCapacity
            && records != null
            && (start & (RequiredPointerAlignment - 1)) == 0
            && TryGetSpanEnd(
                start,
                capacity,
                (ulong)sizeof(BootMemoryRegionRecord),
                out end);
    }

    private static bool ValidateReservationStorage(
        OS.Kernel.Memory.BootReservedRange* ledger,
        ulong count,
        ulong capacity,
        out ulong start,
        out ulong end)
    {
        start = (ulong)ledger;
        end = 0;
        return count != 0
            && count <= capacity
            && capacity
                <= OS.Kernel.Memory.BootReservationTableValidator
                    .MaximumReservationCount
            && ledger != null
            && (start & (RequiredPointerAlignment - 1)) == 0
            && TryGetSpanEnd(
                start,
                capacity,
                (ulong)sizeof(
                    OS.Kernel.Memory.BootReservedRange),
                out end);
    }

    private static bool ValidateStaticStateStorage(
        M01LivePagingStaticState* state,
        ulong capacity,
        out ulong start,
        out ulong end)
    {
        start = (ulong)state;
        end = 0;
        return state != null
            && capacity == M01LivePagingStaticState.RequiredStorageBytes
            && (start
                & (M01LivePagingStaticState.RequiredStorageAlignment
                    - 1)) == 0
            && TryGetSpanEnd(start, capacity, 1, out end);
    }

    private static bool ValidatePreparationStateStorage(
        M01LivePagingPreparationState* state,
        ulong count,
        ulong capacity,
        out ulong start,
        out ulong end)
    {
        start = (ulong)state;
        end = 0;
        return state != null
            && count == 1
            && capacity == 1
            && (start
                & (M01LivePagingPreparationState
                    .RequiredStorageAlignment - 1)) == 0
            && TryGetSpanEnd(
                start,
                capacity,
                M01LivePagingPreparationState.RequiredStorageBytes,
                out end);
    }

    private static bool ValidateAllocationForIndex(
        BootMemoryRegionRecord allocation,
        ulong index,
        bool requireExactTablePages,
        ulong expectedTablePages)
    {
        uint expectedType =
            index == TableStateAllocationRecordIndex
                ? (uint)UefiMemoryType.LoaderData
                : (uint)UefiMemoryType.LoaderCode;
        ulong expectedPages =
            index == TableStateAllocationRecordIndex
                ? expectedTablePages
                : 1;
        if (index >= RequiredAllocationRecordCount
            || allocation.UefiType != expectedType
            || allocation.PhysicalStart == 0
            || allocation.VirtualStart != allocation.PhysicalStart
            || (allocation.PhysicalStart & (PageSize - 1)) != 0
            || allocation.PageCount == 0
            || allocation.PageCount > MaximumUnsignedLong / PageSize
            || allocation.ByteLength
                != allocation.PageCount * PageSize
            || allocation.PhysicalStart
                > MaximumUnsignedLong - allocation.ByteLength
            || allocation.PhysicalEndExclusive
                != allocation.PhysicalStart + allocation.ByteLength
            || allocation.VirtualStart
                >= OS.Kernel.Memory.X64PageMap
                    .LowCanonicalEndExclusive
            || allocation.VirtualStart
                > OS.Kernel.Memory.X64PageMap
                    .LowCanonicalEndExclusive
                    - allocation.ByteLength)
        {
            return false;
        }

        return index == TableStateAllocationRecordIndex
            ? !requireExactTablePages
                || allocation.PageCount == expectedPages
            : allocation.PageCount == expectedPages;
    }

    private static bool AllocationsOverlap(
        BootMemoryRegionRecord left,
        BootMemoryRegionRecord right)
    {
        return left.PhysicalStart < right.PhysicalEndExclusive
            && right.PhysicalStart < left.PhysicalEndExclusive;
    }

    private static bool FindExactReservation(
        OS.Kernel.Memory.BootReservedRange* ledger,
        ulong count,
        BootMemoryRegionRecord allocation,
        OS.Kernel.Memory.BootReservationKind kind,
        out ulong foundIndex,
        out OS.Kernel.Memory.BootReservedRange found)
    {
        foundIndex = NoIndex;
        found = default;
        for (ulong index = 0; index < count; index++)
        {
            OS.Kernel.Memory.BootReservedRange current =
                ledger[index];
            if (current.Kind != kind)
            {
                continue;
            }

            if (foundIndex != NoIndex
                || !RangeMatchesAllocation(current, allocation))
            {
                return false;
            }

            foundIndex = index;
            found = current;
        }
        return foundIndex != NoIndex;
    }

    private static bool ContainsExactReservation(
        OS.Kernel.Memory.BootReservedRange* ledger,
        ulong count,
        OS.Kernel.Memory.BootReservedRange expected)
    {
        ulong matches = 0;
        for (ulong index = 0; index < count; index++)
        {
            if (ledger[index].Equals(expected))
            {
                matches++;
            }
        }
        return matches == 1;
    }

    private static bool RangeMatchesAllocation(
        OS.Kernel.Memory.BootReservedRange range,
        BootMemoryRegionRecord allocation)
    {
        return range.IsValid
            && Raw(range.PhysicalStart) == allocation.PhysicalStart
            && Raw(range.PhysicalEndExclusive)
                == allocation.PhysicalEndExclusive
            && range.PageCount == allocation.PageCount
            && range.ByteLength == allocation.ByteLength;
    }

    private static bool ValidateLiveIntervalIdentityMappingBindings(
        OS.Kernel.Boot.X64BootIdentityMappingRequirement*
            requirements,
        ulong requirementCount,
        OS.Kernel.Memory.BootReservedRange* ledger,
        ulong reservationCount,
        OwnedBootStackSwitchPlan stackPlan,
        OS.Kernel.Memory.BootReservedRange codeReservation,
        OS.Kernel.Memory.BootReservedRange probeReservation,
        OS.Kernel.Memory.BootReservedRange tableStateReservation)
    {
        if (requirements == null
            || ledger == null
            || reservationCount == 0
            || requirementCount
                != RequiredLiveIntervalMappingRequirementCount
            || !stackPlan.IsValid
            || !codeReservation.IsValid
            || !probeReservation.IsValid
            || !tableStateReservation.IsValid
            || !ContainsExactReservation(
                ledger,
                reservationCount,
                stackPlan.StackLayout.GuardReservation)
            || !ContainsExactReservation(
                ledger,
                reservationCount,
                stackPlan.StackLayout.StackReservation)
            || tableStateReservation.PageCount < 2
            || tableStateReservation.ByteLength
                < 2 * PageSize)
        {
            return false;
        }

        ulong tableStart = Raw(tableStateReservation.PhysicalStart);
        ulong tableEnd =
            Raw(tableStateReservation.PhysicalEndExclusive);
        if (tableStart == 0
            || tableStart > MaximumUnsignedLong - PageSize
            || tableEnd < PageSize
            || tableStart + PageSize >= tableEnd)
        {
            return false;
        }

        ulong statePageStart = tableEnd - PageSize;
        ulong rootPageEnd = tableStart + PageSize;
        OS.Kernel.Memory.PageProtection readWrite =
            (OS.Kernel.Memory.PageProtection)(
                (byte)OS.Kernel.Memory.PageProtection.Read
                | (byte)OS.Kernel.Memory.PageProtection.Write);
        OS.Kernel.Memory.PageProtection readExecute =
            (OS.Kernel.Memory.PageProtection)(
                (byte)OS.Kernel.Memory.PageProtection.Read
                | (byte)OS.Kernel.Memory.PageProtection.Execute);

        ulong seenPurposeMask = 0;
        for (ulong index = 0; index < requirementCount; index++)
        {
            OS.Kernel.Boot.X64BootIdentityMappingRequirement
                requirement = requirements[index];
            bool isGuard = requirement.Purpose
                == OS.Kernel.Boot.X64BootIdentityMappingPurpose
                    .BootStackGuard;
            if (!requirement.IsInitialized
                || (!isGuard
                    && requirement.CachePolicy
                        != OS.Kernel.Memory.PageCachePolicy.WriteBack))
            {
                return false;
            }

            bool matches;
            switch (requirement.Purpose)
            {
                case OS.Kernel.Boot.X64BootIdentityMappingPurpose
                    .BootStackGuard:
                    matches = RequirementMatchesReservation(
                        requirement,
                        stackPlan.StackLayout.GuardReservation,
                        OS.Kernel.Memory.PageProtection.None)
                        && requirement.CachePolicy
                            == OS.Kernel.Memory.PageCachePolicy
                                .Uninitialized;
                    break;
                case OS.Kernel.Boot.X64BootIdentityMappingPurpose
                    .CompositeCodeAndCallGate:
                    matches = RequirementMatchesReservation(
                        requirement,
                        codeReservation,
                        readExecute);
                    break;
                case OS.Kernel.Boot.X64BootIdentityMappingPurpose
                    .CompositeOwnedStack:
                    matches = RequirementMatchesReservation(
                        requirement,
                        stackPlan.StackLayout.StackReservation,
                        readWrite);
                    break;
                case OS.Kernel.Boot.X64BootIdentityMappingPurpose
                    .ProtectionProbePage:
                    matches = RequirementMatchesReservation(
                        requirement,
                        probeReservation,
                        readWrite);
                    break;
                case OS.Kernel.Boot.X64BootIdentityMappingPurpose
                    .CompleteTableAndStateArena:
                    matches = RequirementMatchesReservation(
                        requirement,
                        tableStateReservation,
                        readWrite);
                    break;
                case OS.Kernel.Boot.X64BootIdentityMappingPurpose
                    .RuntimeStateAndCanaryPage:
                    matches = RequirementMatchesRawRange(
                        requirement,
                        statePageStart,
                        tableEnd)
                        && requirement.Protection == readWrite;
                    break;
                case OS.Kernel.Boot.X64BootIdentityMappingPurpose
                    .OwnedRootPage:
                    matches = RequirementMatchesRawRange(
                        requirement,
                        tableStart,
                        rootPageEnd)
                        && requirement.Protection == readWrite;
                    break;
                default:
                    return false;
            }

            ulong purposeBit =
                1UL << (byte)requirement.Purpose;
            if (!matches
                || (seenPurposeMask & purposeBit) != 0)
            {
                return false;
            }
            seenPurposeMask |= purposeBit;
        }

        return seenPurposeMask
            == (OS.Kernel.Boot.X64BootIdentityMappingPlan
                    .RequiredLiveIntervalPurposeMask
                | (1UL << (byte)OS.Kernel.Boot
                    .X64BootIdentityMappingPurpose.BootStackGuard));
    }

    private static bool ValidateLegacyIdentityMappingBindings(
        OS.Kernel.Boot.X64BootIdentityMappingRequirement*
            requirements,
        ulong requirementCount,
        OS.Kernel.Memory.BootReservedRange* ledger,
        ulong reservationCount,
        ulong stageOneStart,
        ulong stageOneEnd,
        OwnedBootStackSwitchPlan stackPlan,
        OS.Kernel.Memory.BootReservedRange allocatorReservation,
        OS.Kernel.Memory.BootReservedRange codeReservation,
        OS.Kernel.Memory.BootReservedRange probeReservation,
        OS.Kernel.Memory.BootReservedRange tableStateReservation)
    {
        if (requirements == null
            || ledger == null
            || reservationCount == 0
            || !stackPlan.IsValid
            || !allocatorReservation.IsValid
            || allocatorReservation.Kind
                != OS.Kernel.Memory.BootReservationKind.AllocatorArena
            || !codeReservation.IsValid
            || !probeReservation.IsValid
            || !tableStateReservation.IsValid
            || !TryFindUniqueReservationByKind(
                ledger,
                reservationCount,
                OS.Kernel.Memory.BootReservationKind.LoadedImage,
                out OS.Kernel.Memory.BootReservedRange loadedImage)
            || !TryFindUniqueReservationByKind(
                ledger,
                reservationCount,
                OS.Kernel.Memory.BootReservationKind.AcpiScratch,
                out OS.Kernel.Memory.BootReservedRange acpiScratch)
            || !TryFindUniqueReservationByKind(
                ledger,
                reservationCount,
                OS.Kernel.Memory.BootReservationKind
                    .FramebufferMetadata,
                out OS.Kernel.Memory.BootReservedRange
                    framebufferMetadata)
            || loadedImage.PageCount == 0
            || loadedImage.PageCount
                > OS.Kernel.Boot.X64BootIdentityMappingPlan
                    .MaximumSourceCount
                    - RequiredNonImageMappingRequirementCount
            || requirementCount
                != loadedImage.PageCount
                    + RequiredNonImageMappingRequirementCount
            || !ReservationContainsSpan(
                loadedImage,
                stageOneStart,
                stageOneEnd)
            || !TryGetRawSpan(
                stackPlan.Handoff.StorageStart,
                stackPlan.Handoff.StorageEndExclusive,
                out ulong handoffStart,
                out ulong handoffEnd)
            || !ReservationContainsSpan(
                loadedImage,
                handoffStart,
                handoffEnd)
            || handoffEnd > MaximumUnsignedLong - (PageSize - 1)
            || !TryGetOneByteSpan(
                stackPlan.Handoff.Continuation,
                out ulong continuationStart,
                out ulong continuationEnd)
            || !ReservationContainsSpan(
                loadedImage,
                continuationStart,
                continuationEnd))
        {
            return false;
        }
        ulong handoffPageStart =
            handoffStart & ~(PageSize - 1);
        ulong handoffPageEnd =
            (handoffEnd + PageSize - 1)
            & ~(PageSize - 1);
        ulong continuationPageStart =
            continuationStart & ~(PageSize - 1);
        OS.Kernel.Memory.PageProtection handoffProtection =
            handoffPageStart == continuationPageStart
                ? (OS.Kernel.Memory.PageProtection)(
                    (byte)OS.Kernel.Memory.PageProtection.Read
                    | (byte)OS.Kernel.Memory.PageProtection.Execute)
                : OS.Kernel.Memory.PageProtection.Read;

        OS.Kernel.Memory.PageProtection read =
            OS.Kernel.Memory.PageProtection.Read;
        OS.Kernel.Memory.PageProtection readWrite =
            (OS.Kernel.Memory.PageProtection)(
                (byte)OS.Kernel.Memory.PageProtection.Read
                | (byte)OS.Kernel.Memory.PageProtection.Write);
        OS.Kernel.Memory.PageProtection readExecute =
            (OS.Kernel.Memory.PageProtection)(
                (byte)OS.Kernel.Memory.PageProtection.Read
                | (byte)OS.Kernel.Memory.PageProtection.Execute);
        ulong executingImagePageCount = 0;
        ulong ownedStackCount = 0;
        ulong handoffCount = 0;
        ulong serialStubCount = 0;
        ulong acpiScratchCount = 0;
        ulong framebufferCount = 0;
        ulong guardCount = 0;
        bool codeMapped = false;
        bool probeMapped = false;
        bool tableStateMapped = false;
        bool allocatorMapped = false;

        for (ulong index = 0; index < requirementCount; index++)
        {
            OS.Kernel.Boot.X64BootIdentityMappingRequirement
                requirement = requirements[index];
            if (!requirement.IsInitialized)
            {
                return false;
            }
            OS.Kernel.Memory.BootReservedRange reservation;
            if (requirement.Purpose
                == OS.Kernel.Boot.X64BootIdentityMappingPurpose
                    .ExecutingImage)
            {
                if (!RequirementIsOnePageInsideReservation(
                        requirement,
                        loadedImage)
                    || HasPriorRequirementForPage(
                        requirements,
                        index,
                        OS.Kernel.Boot
                            .X64BootIdentityMappingPurpose
                            .ExecutingImage,
                        Raw(requirement.PhysicalStart)))
                {
                    return false;
                }
                reservation = loadedImage;
            }
            else if (requirement.Purpose
                == OS.Kernel.Boot.X64BootIdentityMappingPurpose
                    .BootHandoff)
            {
                if (!RequirementMatchesRawRange(
                        requirement,
                        handoffPageStart,
                        handoffPageEnd))
                {
                    return false;
                }
                reservation = loadedImage;
            }
            else if (!TryFindExactRequirementReservation(
                ledger,
                reservationCount,
                requirement,
                out reservation))
            {
                return false;
            }

            bool writeBack =
                requirement.CachePolicy
                    == OS.Kernel.Memory.PageCachePolicy.WriteBack;
            switch (requirement.Purpose)
            {
                case OS.Kernel.Boot
                    .X64BootIdentityMappingPurpose.ExecutingImage:
                    if (requirement.Protection
                            != (Raw(requirement.PhysicalStart)
                                    == continuationPageStart
                                ? readExecute
                                : read)
                        || !writeBack)
                    {
                        return false;
                    }
                    executingImagePageCount++;
                    break;
                case OS.Kernel.Boot
                    .X64BootIdentityMappingPurpose.OwnedStack:
                    if (!reservation.Equals(
                            stackPlan.StackLayout.StackReservation)
                        || requirement.Protection != readWrite
                        || !writeBack)
                    {
                        return false;
                    }
                    ownedStackCount++;
                    break;
                case OS.Kernel.Boot
                    .X64BootIdentityMappingPurpose.BootHandoff:
                    if (requirement.Protection != handoffProtection
                        || !writeBack)
                    {
                        return false;
                    }
                    handoffCount++;
                    break;
                case OS.Kernel.Boot
                    .X64BootIdentityMappingPurpose.SerialStub:
                    // M01-012 froze this required purpose before owned
                    // port-I/O existed. Bind it to reviewed RX memory so an
                    // arbitrary executable range cannot enter the model, but
                    // never promote this compatibility alias to serial proof.
                    if (!reservation.Equals(
                            stackPlan.StubReservation)
                        || requirement.Protection != readExecute
                        || !writeBack)
                    {
                        return false;
                    }
                    serialStubCount++;
                    break;
                case OS.Kernel.Boot
                    .X64BootIdentityMappingPurpose.AcpiScratch:
                    if (!reservation.Equals(acpiScratch)
                        || requirement.Protection != read
                        || !writeBack)
                    {
                        return false;
                    }
                    acpiScratchCount++;
                    break;
                case OS.Kernel.Boot
                    .X64BootIdentityMappingPurpose
                        .FramebufferMetadata:
                    if (!reservation.Equals(framebufferMetadata)
                        || requirement.Protection != read
                        || !writeBack)
                    {
                        return false;
                    }
                    framebufferCount++;
                    break;
                case OS.Kernel.Boot
                    .X64BootIdentityMappingPurpose.BootStackGuard:
                    if (!reservation.Equals(
                            stackPlan.StackLayout.GuardReservation)
                        || requirement.Protection
                            != OS.Kernel.Memory.PageProtection.None
                        || requirement.CachePolicy
                            != OS.Kernel.Memory.PageCachePolicy
                                .Uninitialized)
                    {
                        return false;
                    }
                    guardCount++;
                    break;
                case OS.Kernel.Boot
                    .X64BootIdentityMappingPurpose
                        .AdditionalRequired:
                    if (reservation.Equals(codeReservation)
                        && requirement.Protection == readExecute
                        && writeBack)
                    {
                        if (codeMapped)
                        {
                            return false;
                        }
                        codeMapped = true;
                    }
                    else if (reservation.Equals(probeReservation)
                        && requirement.Protection == readWrite
                        && writeBack)
                    {
                        if (probeMapped)
                        {
                            return false;
                        }
                        probeMapped = true;
                    }
                    else if (reservation.Equals(
                            tableStateReservation)
                        && requirement.Protection == readWrite
                        && writeBack)
                    {
                        if (tableStateMapped)
                        {
                            return false;
                        }
                        tableStateMapped = true;
                    }
                    else if (reservation.Equals(
                            allocatorReservation)
                        && requirement.Protection == readWrite
                        && writeBack)
                    {
                        if (allocatorMapped)
                        {
                            return false;
                        }
                        allocatorMapped = true;
                    }
                    else
                    {
                        return false;
                    }
                    break;
                default:
                    return false;
            }
        }

        return executingImagePageCount == loadedImage.PageCount
            && ownedStackCount == 1
            && handoffCount == 1
            && serialStubCount == 1
            && acpiScratchCount == 1
            && framebufferCount == 1
            && guardCount == 1
            && codeMapped
            && probeMapped
            && tableStateMapped
            && allocatorMapped;
    }

    private static bool TryFindUniqueReservationByKind(
        OS.Kernel.Memory.BootReservedRange* ledger,
        ulong count,
        OS.Kernel.Memory.BootReservationKind kind,
        out OS.Kernel.Memory.BootReservedRange found)
    {
        found = default;
        ulong matches = 0;
        for (ulong index = 0; index < count; index++)
        {
            if (ledger[index].Kind == kind)
            {
                found = ledger[index];
                matches++;
            }
        }
        return matches == 1 && found.IsValid;
    }

    private static bool TryFindExactRequirementReservation(
        OS.Kernel.Memory.BootReservedRange* ledger,
        ulong count,
        OS.Kernel.Boot.X64BootIdentityMappingRequirement requirement,
        out OS.Kernel.Memory.BootReservedRange found)
    {
        found = default;
        ulong matches = 0;
        for (ulong index = 0; index < count; index++)
        {
            OS.Kernel.Memory.BootReservedRange candidate =
                ledger[index];
            if (candidate.IsValid
                && candidate.PhysicalStart.Equals(
                    requirement.PhysicalStart)
                && candidate.PhysicalEndExclusive.Equals(
                    requirement.PhysicalEndExclusive)
                && candidate.PageCount == requirement.PageCount
                && candidate.ByteLength == requirement.ByteLength)
            {
                found = candidate;
                matches++;
            }
        }
        return matches == 1;
    }

    private static bool RequirementMatchesReservation(
        OS.Kernel.Boot.X64BootIdentityMappingRequirement requirement,
        OS.Kernel.Memory.BootReservedRange reservation,
        OS.Kernel.Memory.PageProtection protection)
    {
        return reservation.IsValid
            && RequirementMatchesRawRange(
                requirement,
                Raw(reservation.PhysicalStart),
                Raw(reservation.PhysicalEndExclusive))
            && requirement.Protection == protection;
    }

    private static bool RequirementMatchesRawRange(
        OS.Kernel.Boot.X64BootIdentityMappingRequirement requirement,
        ulong expectedStart,
        ulong expectedEnd)
    {
        return requirement.IsInitialized
            && expectedStart != 0
            && expectedStart < expectedEnd
            && Raw(requirement.PhysicalStart) == expectedStart
            && Raw(requirement.PhysicalEndExclusive) == expectedEnd
            && requirement.ByteLength == expectedEnd - expectedStart
            && requirement.PageCount
                == requirement.ByteLength / PageSize
            && requirement.ByteLength % PageSize == 0;
    }

    private static bool RequirementIsOnePageInsideReservation(
        OS.Kernel.Boot.X64BootIdentityMappingRequirement requirement,
        OS.Kernel.Memory.BootReservedRange reservation)
    {
        ulong start = Raw(requirement.PhysicalStart);
        ulong end = Raw(requirement.PhysicalEndExclusive);
        return requirement.IsInitialized
            && reservation.IsValid
            && start != 0
            && (start & (PageSize - 1)) == 0
            && start <= MaximumUnsignedLong - PageSize
            && end == start + PageSize
            && requirement.PageCount == 1
            && requirement.ByteLength == PageSize
            && Raw(reservation.PhysicalStart) <= start
            && end <= Raw(reservation.PhysicalEndExclusive);
    }

    private static bool HasPriorRequirementForPage(
        OS.Kernel.Boot.X64BootIdentityMappingRequirement*
            requirements,
        ulong currentIndex,
        OS.Kernel.Boot.X64BootIdentityMappingPurpose purpose,
        ulong pageStart)
    {
        for (ulong index = 0; index < currentIndex; index++)
        {
            if (requirements[index].Purpose == purpose
                && Raw(requirements[index].PhysicalStart)
                    == pageStart)
            {
                return true;
            }
        }
        return false;
    }

    private static bool ReservationContainsSpan(
        OS.Kernel.Memory.BootReservedRange reservation,
        ulong start,
        ulong end)
    {
        return reservation.IsValid
            && start != 0
            && start < end
            && Raw(reservation.PhysicalStart) <= start
            && end <= Raw(reservation.PhysicalEndExclusive);
    }

    private static bool TryGetRawSpan(
        VirtualAddress start,
        VirtualAddress end,
        out ulong rawStart,
        out ulong rawEnd)
    {
        rawStart = Raw(start);
        rawEnd = Raw(end);
        return rawStart != 0 && rawStart < rawEnd;
    }

    private static bool TryGetOneByteSpan(
        VirtualAddress address,
        out ulong rawStart,
        out ulong rawEnd)
    {
        rawStart = Raw(address);
        rawEnd = 0;
        return rawStart != 0
            && TryGetSpanEnd(rawStart, 1, 1, out rawEnd);
    }

    private static bool MetadataOverlapsMap(
        ulong start,
        ulong end,
        FirmwareMemoryMapRecord map)
    {
        ulong bufferEnd = map.BufferAddress + map.BufferSize;
        ulong sourceEnd =
            map.SourceBufferAddress + map.SourceBufferSize;
        return !AreDisjoint(
                start,
                end,
                map.BufferAddress,
                bufferEnd)
            || !AreDisjoint(
                start,
                end,
                map.SourceBufferAddress,
                sourceEnd);
    }

    private static bool IsDefaultState(
        M01LivePagingStaticState state)
    {
        byte* bytes = (byte*)&state;
        for (ulong index = 0;
            index < M01LivePagingStaticState.RequiredStorageBytes;
            index++)
        {
            if (bytes[index] != 0)
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsDefaultPreparationState(
        M01LivePagingPreparationState state)
    {
        byte* bytes = (byte*)&state;
        for (ulong index = 0;
            index
                < M01LivePagingPreparationState.RequiredStorageBytes;
            index++)
        {
            if (bytes[index] != 0)
            {
                return false;
            }
        }
        return true;
    }

    private static bool TryValidateConventionalDescriptor(
        BootMemoryRegionRecord descriptor,
        out ulong physicalStart,
        out ulong physicalEndExclusive)
    {
        physicalStart = descriptor.PhysicalStart;
        physicalEndExclusive = descriptor.PhysicalEndExclusive;
        if (descriptor.UefiType
                != (uint)UefiMemoryType.ConventionalMemory
            || descriptor.PageCount == 0
            || descriptor.PageCount
                > MaximumUnsignedLong / PageSize
            || (physicalStart & (PageSize - 1)) != 0
            || physicalStart
                > MaximumUnsignedLong
                    - descriptor.PageCount * PageSize)
        {
            return false;
        }

        ulong expectedByteLength =
            descriptor.PageCount * PageSize;
        return descriptor.ByteLength == expectedByteLength
            && physicalEndExclusive
                == physicalStart + expectedByteLength
            && physicalEndExclusive > physicalStart
            && (physicalEndExclusive & (PageSize - 1)) == 0;
    }

    private static bool TryWriteArenaRequirements(
        OS.Kernel.Boot.X64BootIdentityMappingRequirement*
            requirementWorkspace,
        PhysicalAddress candidatePhysicalBase,
        ulong totalPageCount,
        out PhysicalAddress rootTable,
        out PhysicalAddress statePage,
        out PhysicalAddress arenaEndExclusive,
        out AddressValueStatus addressStatus)
    {
        rootTable = default;
        statePage = default;
        arenaEndExclusive = default;
        addressStatus = AddressValueStatus.Uninitialized;
        if (requirementWorkspace == null
            || totalPageCount < RequiredArenaStatePageCount + 1
            || totalPageCount > MaximumArenaTotalPageCount
            || totalPageCount
                > MaximumUnsignedLong / PageSize)
        {
            addressStatus = AddressValueStatus.InvalidAddress;
            return false;
        }

        ulong totalByteLength = totalPageCount * PageSize;
        ulong statePageOffset =
            (totalPageCount - RequiredArenaStatePageCount)
            * PageSize;
        addressStatus =
            candidatePhysicalBase.TryGetEndExclusive(
                totalByteLength,
                out arenaEndExclusive);
        if (addressStatus != AddressValueStatus.Success)
        {
            return false;
        }

        addressStatus =
            candidatePhysicalBase.TryAdd(
                statePageOffset,
                out statePage);
        if (addressStatus != AddressValueStatus.Success)
        {
            return false;
        }

        addressStatus =
            candidatePhysicalBase.TryGetEndExclusive(
                PageSize,
                out PhysicalAddress rootEndExclusive);
        if (addressStatus != AddressValueStatus.Success)
        {
            return false;
        }

        rootTable = candidatePhysicalBase;
        OS.Kernel.Memory.PageProtection readWrite =
            (OS.Kernel.Memory.PageProtection)(
                (byte)OS.Kernel.Memory.PageProtection.Read
                | (byte)OS.Kernel.Memory.PageProtection.Write);
        requirementWorkspace[4] =
            new OS.Kernel.Boot.X64BootIdentityMappingRequirement(
                OS.Kernel.Boot.X64BootIdentityMappingPurpose
                    .CompleteTableAndStateArena,
                candidatePhysicalBase,
                arenaEndExclusive,
                totalPageCount,
                totalByteLength,
                readWrite,
                OS.Kernel.Memory.PageCachePolicy.WriteBack);
        requirementWorkspace[5] =
            new OS.Kernel.Boot.X64BootIdentityMappingRequirement(
                OS.Kernel.Boot.X64BootIdentityMappingPurpose
                    .RuntimeStateAndCanaryPage,
                statePage,
                arenaEndExclusive,
                RequiredArenaStatePageCount,
                PageSize,
                readWrite,
                OS.Kernel.Memory.PageCachePolicy.WriteBack);
        requirementWorkspace[6] =
            new OS.Kernel.Boot.X64BootIdentityMappingRequirement(
                OS.Kernel.Boot.X64BootIdentityMappingPurpose
                    .OwnedRootPage,
                rootTable,
                rootEndExclusive,
                1,
                PageSize,
                readWrite,
                OS.Kernel.Memory.PageCachePolicy.WriteBack);
        addressStatus = AddressValueStatus.Success;
        return true;
    }

    private static M01LivePagingArenaPlanResult ArenaPlanSuccess(
        ArenaPlanState state)
    {
        return ArenaPlanResult(
            M01LivePagingArenaPlanStatus.Success,
            state);
    }

    private static M01LivePagingArenaPlanResult ArenaPlanFailure(
        M01LivePagingArenaPlanStatus status,
        ArenaPlanState state)
    {
        return ArenaPlanResult(status, state);
    }

    private static M01LivePagingArenaPlanResult ArenaPlanResult(
        M01LivePagingArenaPlanStatus status,
        ArenaPlanState state)
    {
        ulong requiredEntryCount =
            state.TablePageCount
                <= MaximumUnsignedLong
                    / OS.Kernel.Memory
                        .X64PageTableImagePage.EntryCountPerPage
            ? state.TablePageCount
                * OS.Kernel.Memory
                    .X64PageTableImagePage.EntryCountPerPage
            : 0;
        return new M01LivePagingArenaPlanResult(
            status,
            state.AddressStatus,
            state.ReservationStatus,
            state.MappingStatus,
            state.PageTableEntryStatus,
            state.PageTablePreconditions,
            state.AllocationRecord,
            state.MappingPlan,
            state.CandidatePhysicalBase,
            state.RootTable,
            state.StatePage,
            state.ArenaEndExclusive,
            state.IterationCount,
            state.IterationLimit,
            state.TablePageCount,
            RequiredArenaStatePageCount,
            state.TotalPageCount,
            state.TableByteLength,
            state.TotalByteLength,
            state.AvailablePageCountFromCandidate,
            RequiredArenaFixedRequirementCount,
            RequiredArenaRequirementWorkspaceCount,
            state.RequiredRangeWorkspaceCount,
            state.TablePageCount,
            requiredEntryCount,
            state.BootStackGuardRequirement,
            state.CodeAndCallGateRequirement,
            state.OwnedStackRequirement,
            state.ProtectionProbeRequirement);
    }

    private static bool IsCanonicalArenaPlan(
        M01LivePagingArenaPlanResult plan)
    {
        if (!plan.Succeeded
            || plan.RequiredFixedRequirementCount
                != RequiredArenaFixedRequirementCount
            || plan.RequiredRequirementWorkspaceCount
                != RequiredArenaRequirementWorkspaceCount
            || plan.StatePageCount
                != RequiredArenaStatePageCount
            || plan.TablePageCount == 0
            || plan.TablePageCount
                > OS.Kernel.Memory.X64PageMap.MaximumPageImages
            || plan.TotalPageCount
                < MinimumArenaTotalPageCount
            || plan.TotalPageCount
                > MaximumArenaTotalPageCount
            || plan.TotalPageCount
                != plan.TablePageCount
                    + RequiredArenaStatePageCount
            || plan.IterationCount == 0
            || plan.IterationCount > plan.IterationLimit
            || plan.IterationLimit == 0
            || plan.IterationLimit > MaximumArenaIterationCount
            || plan.TablePageCount
                > MaximumUnsignedLong / PageSize
            || plan.TotalPageCount
                > MaximumUnsignedLong / PageSize
            || plan.TableByteLength
                != plan.TablePageCount * PageSize
            || plan.TotalByteLength
                != plan.TotalPageCount * PageSize
            || plan.TotalByteLength
                > MaximumArenaTotalByteLength
            || plan.AvailablePageCountFromCandidate
                < plan.TotalPageCount
            || plan.RequiredPageImageCount
                != plan.TablePageCount
            || plan.TablePageCount
                > MaximumUnsignedLong
                    / OS.Kernel.Memory
                        .X64PageTableImagePage
                        .EntryCountPerPage
            || plan.RequiredPageTableEntryCount
                != plan.TablePageCount
                    * OS.Kernel.Memory
                        .X64PageTableImagePage
                        .EntryCountPerPage
            || plan.RequiredPageTableEntryCount
                > OS.Kernel.Memory.X64PageMap.MaximumEntryCount
            || plan.MappingPlan.SourceCount
                != RequiredArenaRequirementWorkspaceCount
            || plan.MappingPlan.OutputRangeCount
                != plan.RequiredRangeWorkspaceCount
            || plan.MappingPlan.TotalTablePageCount
                != plan.TablePageCount
            || plan.MappingPlan.TotalTableByteLength
                != plan.TableByteLength
            || plan.PageTablePreconditions.Validate()
                != OS.Kernel.Memory
                    .X64PageTableEntryStatus.Success)
        {
            return false;
        }

        ulong candidateStart =
            Raw(plan.CandidatePhysicalBase);
        ulong rootStart = Raw(plan.RootTable);
        ulong stateStart = Raw(plan.StatePage);
        ulong arenaEnd = Raw(plan.ArenaEndExclusive);
        if (candidateStart == 0
            || rootStart != candidateStart
            || candidateStart
                > MaximumUnsignedLong - plan.TableByteLength
            || stateStart
                != candidateStart + plan.TableByteLength
            || candidateStart
                > MaximumUnsignedLong - plan.TotalByteLength
            || arenaEnd
                != candidateStart + plan.TotalByteLength
            || arenaEnd
                > (1UL << plan.PageTablePreconditions
                    .PhysicalAddressWidth))
        {
            return false;
        }

        BootMemoryRegionRecord expected =
            new BootMemoryRegionRecord(
                (uint)UefiMemoryType.LoaderData,
                candidateStart,
                candidateStart,
                plan.TotalPageCount,
                plan.AllocationRecord.Attribute,
                plan.TotalByteLength,
                arenaEnd);
        return AllocationRecordsEqual(
            plan.AllocationRecord,
            expected);
    }

    private static bool AllocationRecordsEqual(
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

    private static bool TryRederiveArenaMappingPlan(
        M01LivePagingArenaPlanResult plan,
        OS.Kernel.Boot.X64BootIdentityMappingRequirement*
            requirements,
        OS.Kernel.Boot.X64BootIdentityMappingRange* ranges,
        out OS.Kernel.Boot.X64BootIdentityMappingPlanResult
            mappingPlan)
    {
        mappingPlan = default;
        if (requirements == null || ranges == null)
        {
            return false;
        }

        requirements[0] = plan.BootStackGuardRequirement;
        requirements[1] = plan.CodeAndCallGateRequirement;
        requirements[2] = plan.OwnedStackRequirement;
        requirements[3] = plan.ProtectionProbeRequirement;
        if (!TryWriteArenaRequirements(
                requirements,
                plan.CandidatePhysicalBase,
                plan.TotalPageCount,
                out PhysicalAddress rootTable,
                out PhysicalAddress statePage,
                out PhysicalAddress arenaEndExclusive,
                out AddressValueStatus addressStatus)
            || addressStatus != AddressValueStatus.Success
            || !rootTable.Equals(plan.RootTable)
            || !statePage.Equals(plan.StatePage)
            || !arenaEndExclusive.Equals(
                plan.ArenaEndExclusive))
        {
            return false;
        }

        mappingPlan =
            OS.Kernel.Boot.X64BootIdentityMappingPlan
                .CreateLiveInterval(
                    requirements,
                    RequiredArenaRequirementWorkspaceCount,
                    RequiredArenaRequirementWorkspaceCount,
                    ranges,
                    RequiredArenaRequirementWorkspaceCount,
                    OS.Kernel.Memory.X64PageMap
                        .MaximumPageImages,
                    plan.PageTablePreconditions);
        return mappingPlan.Succeeded;
    }

    private static bool MappingPlansEqual(
        OS.Kernel.Boot.X64BootIdentityMappingPlanResult left,
        OS.Kernel.Boot.X64BootIdentityMappingPlanResult right)
    {
        return left.IsInitialized == right.IsInitialized
            && left.Succeeded == right.Succeeded
            && left.Status == right.Status
            && left.AddressStatus == right.AddressStatus
            && left.PageTableEntryStatus
                == right.PageTableEntryStatus
            && left.SourceCount == right.SourceCount
            && left.ValidatedSourceCount
                == right.ValidatedSourceCount
            && left.SourceCapacity == right.SourceCapacity
            && left.DestinationCapacity
                == right.DestinationCapacity
            && left.RequiredDestinationCapacity
                == right.RequiredDestinationCapacity
            && left.OutputRangeCount == right.OutputRangeCount
            && left.AvailableTablePageCapacity
                == right.AvailableTablePageCapacity
            && left.RequiredPurposeMask
                == right.RequiredPurposeMask
            && left.ObservedPurposeMask
                == right.ObservedPurposeMask
            && left.MissingPurposeMask
                == right.MissingPurposeMask
            && left.InvalidSourceIndex
                == right.InvalidSourceIndex
            && left.ConflictingSourceIndex
                == right.ConflictingSourceIndex
            && left.GuardSourceIndex == right.GuardSourceIndex
            && left.UnionPageCount == right.UnionPageCount
            && left.UnionByteLength == right.UnionByteLength
            && left.Pml4PageCount == right.Pml4PageCount
            && left.PdptPageCount == right.PdptPageCount
            && left.PdPageCount == right.PdPageCount
            && left.PtPageCount == right.PtPageCount
            && left.TotalTablePageCount
                == right.TotalTablePageCount
            && left.TotalTableByteLength
                == right.TotalTableByteLength;
    }

    private static M01LivePagingArenaMaterializationStatus
        AuditArenaHierarchyPage(
            OS.Kernel.Memory.X64PageTableImagePage* pageImages,
            ulong pageImageCount,
            ulong* pageTableEntries,
            ulong pageTableEntryCount,
            OS.Kernel.Memory.X64PageTableEncodingPreconditions
                preconditions,
            OS.Kernel.Boot.X64BootIdentityMappingRange*
                normalizedRanges,
            ulong normalizedRangeCount,
            ulong* rangeLeafCounts,
            ulong pageIndex,
            OS.Kernel.Memory.X64PageTableLevel level,
            ulong virtualPrefix,
            ref ulong visitedMask,
            ref ArenaMaterializationState state)
    {
        if (pageIndex >= pageImageCount
            || normalizedRanges == null
            || rangeLeafCounts == null
            || normalizedRangeCount == 0
            || normalizedRangeCount
                > RequiredArenaRequirementWorkspaceCount)
        {
            state.InvalidPageOrdinal = pageIndex;
            return M01LivePagingArenaMaterializationStatus
                .MissingChildPage;
        }

        int virtualShift =
            level
                == OS.Kernel.Memory.X64PageTableLevel.Pml4e
            ? 39
            : level
                == OS.Kernel.Memory.X64PageTableLevel.Pdpte
                ? 30
                : level
                    == OS.Kernel.Memory.X64PageTableLevel.Pde
                    ? 21
                    : 12;
        ulong entryStart =
            pageImages[pageIndex].EntryStartIndex;
        for (ulong ordinal = 0;
            ordinal
                < OS.Kernel.Memory.X64PageTableImagePage
                    .EntryCountPerPage;
            ordinal++)
        {
            ulong entryIndex = entryStart + ordinal;
            if (entryIndex >= pageTableEntryCount)
            {
                state.InvalidPageOrdinal = pageIndex;
                state.InvalidEntryIndex = entryIndex;
                state.PageImageStatus =
                    OS.Kernel.Memory.X64PageMapStatus
                        .InvalidPageImageRecord;
                return M01LivePagingArenaMaterializationStatus
                    .HierarchyMalformed;
            }

            ulong rawEntry = pageTableEntries[entryIndex];
            ulong entryVirtualStart =
                virtualPrefix | (ordinal << virtualShift);
            OS.Kernel.Memory.X64PageTableEntryResult decoded =
                OS.Kernel.Memory.X64PageTableEntry.TryDecode(
                    level,
                    rawEntry,
                    preconditions);
            if (!decoded.Succeeded)
            {
                state.InvalidPageOrdinal = pageIndex;
                state.InvalidEntryIndex = entryIndex;
                state.ExpectedEntry = rawEntry;
                state.PageTableEntryStatus = decoded.Status;
                state.PageImageStatus =
                    OS.Kernel.Memory.X64PageMapStatus
                        .MalformedEntry;
                return M01LivePagingArenaMaterializationStatus
                    .HierarchyMalformed;
            }

            if (decoded.Kind
                == OS.Kernel.Memory.X64PageTableEntryKind.Unmapped)
            {
                continue;
            }

            if (level
                == OS.Kernel.Memory.X64PageTableLevel.Pte)
            {
                if (decoded.Kind
                    != OS.Kernel.Memory
                        .X64PageTableEntryKind.Leaf
                    || decoded.PageSizeBytes != PageSize
                    || Raw(decoded.PhysicalAddress)
                        != entryVirtualStart
                    || !TryMatchArenaSemanticLeaf(
                        normalizedRanges,
                        normalizedRangeCount,
                        entryVirtualStart,
                        decoded.Protection,
                        decoded.CachePolicy,
                        out ulong matchingRangeIndex))
                {
                    state.InvalidPageOrdinal = pageIndex;
                    state.InvalidEntryIndex = entryIndex;
                    state.ExpectedEntry = rawEntry;
                    state.PageImageStatus =
                        OS.Kernel.Memory.X64PageMapStatus
                            .MalformedEntry;
                    return M01LivePagingArenaMaterializationStatus
                        .SemanticMappingMismatch;
                }
                OS.Kernel.Memory.X64PageTableEntryResult
                    strictLeaf =
                        OS.Kernel.Memory.X64PageTableEntry
                            .TryEncode(
                                new OS.Kernel.Memory
                                    .X64PageTableEntryRequest(
                                        OS.Kernel.Memory
                                            .X64PageTableLevel.Pte,
                                        OS.Kernel.Memory
                                            .X64PageTableEntryKind.Leaf,
                                        decoded.PhysicalAddress,
                                        decoded.Protection,
                                        decoded.CachePolicy),
                                preconditions);
                if (!strictLeaf.Succeeded
                    || strictLeaf.Entry.RawValue != rawEntry)
                {
                    state.InvalidPageOrdinal = pageIndex;
                    state.InvalidEntryIndex = entryIndex;
                    state.ExpectedEntry = strictLeaf.Succeeded
                        ? strictLeaf.Entry.RawValue
                        : 0;
                    state.ObservedEntry = rawEntry;
                    state.PageTableEntryStatus =
                        strictLeaf.Status;
                    return M01LivePagingArenaMaterializationStatus
                        .SemanticMappingMismatch;
                }
                rangeLeafCounts[matchingRangeIndex]++;
                state.MappedLeafCount++;
                continue;
            }

            if (decoded.Kind
                != OS.Kernel.Memory
                    .X64PageTableEntryKind.TablePointer
                || decoded.Protection
                    != OS.Kernel.Memory.PageProtection.None
                || decoded.CachePolicy
                    != OS.Kernel.Memory.PageCachePolicy.WriteBack
                || decoded.PageSizeBytes != 0
                || decoded.IsLargePage)
            {
                state.InvalidPageOrdinal = pageIndex;
                state.InvalidEntryIndex = entryIndex;
                state.ExpectedEntry = rawEntry;
                state.PageImageStatus =
                    OS.Kernel.Memory.X64PageMapStatus
                        .LargePageCollision;
                return M01LivePagingArenaMaterializationStatus
                    .HierarchyMalformed;
            }

            OS.Kernel.Memory.X64PageTableEntryResult
                strictTablePointer =
                    OS.Kernel.Memory.X64PageTableEntry.TryEncode(
                        new OS.Kernel.Memory
                            .X64PageTableEntryRequest(
                                level,
                                OS.Kernel.Memory
                                    .X64PageTableEntryKind
                                    .TablePointer,
                                decoded.PhysicalAddress,
                                OS.Kernel.Memory
                                    .PageProtection.None,
                                OS.Kernel.Memory
                                    .PageCachePolicy.WriteBack),
                        preconditions);
            if (!strictTablePointer.Succeeded
                || strictTablePointer.Entry.RawValue != rawEntry)
            {
                state.InvalidPageOrdinal = pageIndex;
                state.InvalidEntryIndex = entryIndex;
                state.ExpectedEntry =
                    strictTablePointer.Succeeded
                        ? strictTablePointer.Entry.RawValue
                        : 0;
                state.ObservedEntry = rawEntry;
                state.PageTableEntryStatus =
                    strictTablePointer.Status;
                return M01LivePagingArenaMaterializationStatus
                    .SemanticMappingMismatch;
            }

            ulong childPageIndex = FindPageImage(
                pageImages,
                pageImageCount,
                decoded.PhysicalAddress);
            if (childPageIndex
                == M01LivePagingTableBuildResult.NoIndex)
            {
                state.InvalidPageOrdinal = pageIndex;
                state.InvalidEntryIndex = entryIndex;
                state.ExpectedEntry = rawEntry;
                state.PageImageStatus =
                    OS.Kernel.Memory.X64PageMapStatus
                        .TablePointerTargetMissing;
                return M01LivePagingArenaMaterializationStatus
                    .MissingChildPage;
            }

            ulong childBit = 1UL << (int)childPageIndex;
            if ((visitedMask & childBit) != 0)
            {
                state.InvalidPageOrdinal = childPageIndex;
                state.InvalidEntryIndex = entryIndex;
                state.ExpectedEntry = rawEntry;
                state.PageImageStatus =
                    OS.Kernel.Memory.X64PageMapStatus
                        .PageImagePhysicalOverlap;
                return M01LivePagingArenaMaterializationStatus
                    .DuplicateChildOrCycle;
            }

            visitedMask |= childBit;
            state.ReachablePageCount++;
            OS.Kernel.Memory.X64PageTableLevel childLevel =
                level
                    == OS.Kernel.Memory.X64PageTableLevel.Pml4e
                ? OS.Kernel.Memory.X64PageTableLevel.Pdpte
                : level
                    == OS.Kernel.Memory.X64PageTableLevel.Pdpte
                    ? OS.Kernel.Memory.X64PageTableLevel.Pde
                    : OS.Kernel.Memory.X64PageTableLevel.Pte;
            M01LivePagingArenaMaterializationStatus childStatus =
                AuditArenaHierarchyPage(
                    pageImages,
                    pageImageCount,
                    pageTableEntries,
                    pageTableEntryCount,
                    preconditions,
                    normalizedRanges,
                    normalizedRangeCount,
                    rangeLeafCounts,
                    childPageIndex,
                    childLevel,
                    entryVirtualStart,
                    ref visitedMask,
                    ref state);
            if (childStatus
                != M01LivePagingArenaMaterializationStatus
                    .Success)
            {
                return childStatus;
            }
        }

        return M01LivePagingArenaMaterializationStatus.Success;
    }

    private static bool TryMatchArenaSemanticLeaf(
        OS.Kernel.Boot.X64BootIdentityMappingRange*
            normalizedRanges,
        ulong normalizedRangeCount,
        ulong virtualPageStart,
        OS.Kernel.Memory.PageProtection protection,
        OS.Kernel.Memory.PageCachePolicy cachePolicy,
        out ulong matchingRangeIndex)
    {
        matchingRangeIndex =
            M01LivePagingArenaMaterializationResult.NoIndex;
        for (ulong rangeIndex = 0;
            rangeIndex < normalizedRangeCount;
            rangeIndex++)
        {
            OS.Kernel.Boot.X64BootIdentityMappingRange range =
                normalizedRanges[rangeIndex];
            ulong virtualStart = Raw(range.VirtualStart);
            ulong virtualEnd = Raw(range.VirtualEndExclusive);
            ulong physicalStart = Raw(range.PhysicalStart);
            ulong physicalEnd =
                Raw(range.PhysicalEndExclusive);
            if (!range.IsInitialized
                || virtualStart == 0
                || virtualStart >= virtualEnd
                || physicalStart != virtualStart
                || physicalEnd != virtualEnd
                || range.ByteLength != virtualEnd - virtualStart
                || range.PageCount
                    != range.ByteLength / PageSize
                || range.ByteLength % PageSize != 0)
            {
                return false;
            }

            if (virtualPageStart < virtualStart
                || virtualPageStart >= virtualEnd)
            {
                continue;
            }

            if (matchingRangeIndex
                    != M01LivePagingArenaMaterializationResult
                        .NoIndex
                || protection != range.Protection
                || cachePolicy != range.CachePolicy)
            {
                return false;
            }
            matchingRangeIndex = rangeIndex;
        }

        return matchingRangeIndex
            != M01LivePagingArenaMaterializationResult.NoIndex;
    }

    private static ulong Fnv1aArenaImage(
        OS.Kernel.Memory.X64PageTableImagePage* pageImages,
        ulong pageImageCount,
        ulong* pageTableEntries,
        bool useBackingEntries,
        byte* arenaBacking)
    {
        ulong hash = 14695981039346656037UL;
        hash = Fnv1aAppendUlong(hash, pageImageCount);
        for (ulong pageIndex = 0;
            pageIndex < pageImageCount;
            pageIndex++)
        {
            hash = Fnv1aAppendUlong(
                hash,
                Raw(pageImages[pageIndex].PhysicalStart));
            hash = Fnv1aAppendUlong(
                hash,
                pageImages[pageIndex].EntryStartIndex);
            ulong* backingPage =
                (ulong*)(arenaBacking + pageIndex * PageSize);
            ulong sourceStart =
                pageImages[pageIndex].EntryStartIndex;
            for (ulong entryOrdinal = 0;
                entryOrdinal
                    < OS.Kernel.Memory
                        .X64PageTableImagePage
                        .EntryCountPerPage;
                entryOrdinal++)
            {
                ulong entry = useBackingEntries
                    ? backingPage[entryOrdinal]
                    : pageTableEntries[
                        sourceStart + entryOrdinal];
                hash = Fnv1aAppendUlong(hash, entry);
            }
        }
        return hash;
    }

    private static ulong Fnv1aBytes(
        byte* bytes,
        ulong byteCount)
    {
        ulong hash = 14695981039346656037UL;
        for (ulong index = 0; index < byteCount; index++)
        {
            hash ^= bytes[index];
            hash *= 1099511628211UL;
        }
        return hash;
    }

    private static ulong Fnv1aAppendUlong(
        ulong hash,
        ulong value)
    {
        for (int shift = 0; shift < 64; shift += 8)
        {
            hash ^= (byte)(value >> shift);
            hash *= 1099511628211UL;
        }
        return hash;
    }

    private static M01LivePagingArenaMaterializationResult
        ArenaMaterializationSuccess(
            ArenaMaterializationState state)
    {
        state.PageTableEntryStatus =
            OS.Kernel.Memory.X64PageTableEntryStatus.Success;
        state.PageImageStatus =
            OS.Kernel.Memory.X64PageMapStatus.Success;
        return ArenaMaterializationResult(
            M01LivePagingArenaMaterializationStatus.Success,
            state);
    }

    private static M01LivePagingArenaMaterializationResult
        ArenaMaterializationFailure(
            M01LivePagingArenaMaterializationStatus status,
            ArenaMaterializationState state)
    {
        return ArenaMaterializationResult(status, state);
    }

    private static M01LivePagingArenaMaterializationResult
        ArenaMaterializationResult(
            M01LivePagingArenaMaterializationStatus status,
            ArenaMaterializationState state)
    {
        return new M01LivePagingArenaMaterializationResult(
            status,
            state.PageTableEntryStatus,
            state.PageImageStatus,
            state.RootTable,
            state.TablePageCount,
            state.VerifiedPageCount,
            state.EntryCount,
            state.VerifiedEntryCount,
            state.InvalidPageOrdinal,
            state.InvalidEntryIndex,
            state.ExpectedEntry,
            state.ObservedEntry,
            state.SourceFingerprint,
            state.ObservedFingerprint,
            state.StateFingerprintBefore,
            state.StateFingerprintAfter,
            state.BytesWritten,
            state.ReachablePageCount,
            state.MappedLeafCount,
            state.VerifiedStateByteCount,
            state.InvalidStateByteIndex);
    }

    private static bool TryGetSpanEnd(
        ulong start,
        ulong capacity,
        ulong elementSize,
        out ulong end)
    {
        end = 0;
        if (capacity != 0
            && elementSize > MaximumUnsignedLong / capacity)
        {
            return false;
        }
        ulong length = capacity * elementSize;
        if (start > MaximumUnsignedLong - length)
        {
            return false;
        }
        end = start + length;
        return true;
    }

    private static bool AreDisjoint(
        ulong firstStart,
        ulong firstEnd,
        ulong secondStart,
        ulong secondEnd)
    {
        return firstEnd <= secondStart || secondEnd <= firstStart;
    }

    private static PhysicalAddress ToPhysical(ulong value)
    {
        return PhysicalAddress
                .TryCreateAlignedFromHardwareRegisterBoundary(
                    value,
                    PageSize,
                    out PhysicalAddress result)
                == AddressValueStatus.Success
            ? result
            : default;
    }

    private static VirtualAddress ToVirtual(ulong value)
    {
        return VirtualAddress
                .TryCreateAlignedFromHardwareRegisterBoundary(
                    value,
                    PageSize,
                    out VirtualAddress result)
                == AddressValueStatus.Success
            ? result
            : default;
    }

    private static AddressValueStatus ToVirtualValue(
        ulong value,
        out VirtualAddress address)
    {
        return VirtualAddress.TryCreateFromHardwareRegisterBoundary(
            value,
            out address);
    }

    private static ulong Raw(PhysicalAddress address)
    {
        return address.TryGetRawValueForHardwareRegisterBoundary(
                out ulong value)
                == AddressValueStatus.Success
            ? value
            : 0;
    }

    private static ulong Raw(VirtualAddress address)
    {
        return address.TryGetRawValueForHardwareRegisterBoundary(
                out ulong value)
                == AddressValueStatus.Success
            ? value
            : 0;
    }

    private static M01LivePagingCleanupPlan CleanupFailure(
        M01LivePagingCleanupStatus status,
        ulong completed)
    {
        return new M01LivePagingCleanupPlan(
            status,
            completed,
            0,
            M01LivePagingCleanupPlan.NoIndex,
            M01LivePagingCleanupPlan.NoIndex,
            M01LivePagingCleanupPlan.NoIndex);
    }

    private static M01LivePagingTableBuildResult TableFailure(
        M01LivePagingTableBuildStatus status,
        ulong rangeCount,
        ulong validatedRangeCount = 0,
        ulong tablePageCount = 0,
        ulong entryCount = 0,
        ulong mappedPageCount = 0,
        ulong invalidRangeIndex =
            M01LivePagingTableBuildResult.NoIndex,
        ulong invalidPageOrdinal =
            M01LivePagingTableBuildResult.NoIndex,
        ulong probeLeafEntryIndex =
            M01LivePagingTableBuildResult.NoIndex,
        OS.Kernel.Memory.X64PageTableEntryStatus entryStatus =
            OS.Kernel.Memory.X64PageTableEntryStatus.Uninitialized,
        PhysicalAddress root = default)
    {
        return new M01LivePagingTableBuildResult(
            status,
            entryStatus,
            root,
            rangeCount,
            validatedRangeCount,
            tablePageCount,
            entryCount,
            mappedPageCount,
            invalidRangeIndex,
            invalidPageOrdinal,
            probeLeafEntryIndex);
    }

    private static M01LivePagingIntegrationResult Fail(
        M01LivePagingStatus status,
        M01LivePagingCheckpoint checkpoint,
        M01LivePagingEvidence evidence)
    {
        return M01LivePagingIntegrationResult.Fail(
            status,
            checkpoint,
            evidence);
    }

    private static M01LivePagingPreparationResult FailPreparation(
        M01LivePagingStatus status,
        M01LivePagingCheckpoint checkpoint,
        M01LivePagingPreparationEvidence evidence)
    {
        return M01LivePagingPreparationResult.Fail(
            status,
            checkpoint,
            evidence);
    }
}
