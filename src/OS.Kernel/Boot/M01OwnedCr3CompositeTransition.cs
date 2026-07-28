namespace OS.Boot;

// Reference boundary:
// - references/SharpOS-main/bootasm/BootAsm.Generator/BootAsmGenerator.cs,
//   local snapshot SHA-256
//   B24C79DCD38757DCBE243DC6C4C63C1AF628525F06C4F5D296B6AFB0C9387500,
//   influenced only the deterministic, position-independent byte-plan model.
// - references/SharpOS-main/OS/src/Kernel/Paging/X64PageTable.cs,
//   local snapshot SHA-256
//   35503B543628E5A462073D498704050EEEF7D79DEB2B84F01CACA29714200BAC,
//   influenced only the four-level hierarchy walk.
// No reference implementation is copied. This first-party file emits and
// audits caller-owned bytes and POD. It does not execute privileged code.

/// <summary>
/// Reports the first rejected host or observed-runtime boundary. Existing
/// values are append-only; the zero-valued default is never success.
/// </summary>
public enum M01OwnedCr3CompositeTransitionStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    MissingDestination = 2,
    DestinationCapacityTooSmall = 3,
    CapacityExceedsLimit = 4,
    MisalignedDestination = 5,
    InvalidEntryAddress = 6,
    MisalignedEntryAddress = 7,
    EntryAddressOverflow = 8,
    MissingSemanticDestination = 9,
    SemanticCapacityTooSmall = 10,
    MisalignedSemanticDestination = 11,
    DestinationSemanticOverlap = 12,
    InternalEncodingFailure = 13,
    MissingSource = 14,
    InvalidSourceCount = 15,
    SourceCapacityTooSmall = 16,
    MisalignedSource = 17,
    SourceSpanOverflow = 18,
    ByteMismatch = 19,
    MissingSemanticSource = 20,
    InvalidSemanticCount = 21,
    SemanticSourceCapacityTooSmall = 22,
    MisalignedSemanticSource = 23,
    SemanticSpanOverflow = 24,
    SemanticMismatch = 25,
    MissingAllocationRecords = 26,
    InvalidAllocationRecordCount = 27,
    AllocationRecordCapacityTooSmall = 28,
    MissingReservationRecords = 29,
    InvalidReservationRecordCount = 30,
    ReservationRecordCapacityTooSmall = 31,
    MissingIdentityMappings = 32,
    InvalidIdentityMappingCount = 33,
    IdentityMappingCapacityTooSmall = 34,
    MissingRangeBindings = 35,
    InvalidRangeBindingCount = 36,
    RangeBindingCapacityTooSmall = 37,
    MisalignedInputTable = 38,
    InputTableSpanOverflow = 39,
    InputOutputAlias = 40,
    InvalidRangeBinding = 41,
    DuplicateRangeRole = 42,
    MissingRangeRole = 43,
    AllocationProvenanceMismatch = 44,
    ReservationProvenanceMismatch = 45,
    IdentityMappingProvenanceMismatch = 46,
    InvalidProtection = 47,
    WritableExecutableRejected = 48,
    InvalidOwnedCr3 = 49,
    InvalidProbePte = 50,
    InvalidCanary = 51,
    InvalidProbeContract = 52,
    MissingObservedState = 53,
    InvalidObservedStateCount = 54,
    ObservedStateCapacityTooSmall = 55,
    MisalignedObservedState = 56,
    InvalidTerminalKind = 57,
    Timeout = 58,
    Reset = 59,
    TripleFault = 60,
    DefaultRuntimeResult = 61,
    RuntimeReportedFailure = 62,
    ModePrerequisiteMismatch = 63,
    FirmwareCallbackObserved = 64,
    InterruptStateMismatch = 65,
    RawCr3BitLoss = 66,
    RawCr4BitLoss = 67,
    StackEscape = 68,
    ReturnAddressMismatch = 69,
    CanaryMismatch = 70,
    ProbePteAddressMismatch = 71,
    ProbePteReadbackMismatch = 72,
    InvlpgMismatch = 73,
    ProbeMismatch = 74,
    PageZeroPresent = 75,
    PageZeroMalformed = 76,
    CheckpointMismatch = 77,
    FirmwareCr3ReadbackMismatch = 78,
    Cr4RestoreReadbackMismatch = 79,
    RflagsRestoreReadbackMismatch = 80,
    SeparateLeavesNotCompositeEvidence = 81,
    ReservedStateNonzero = 82,
    MissingPageImages = 83,
    InvalidPageImageCount = 84,
    PageImageCapacityTooSmall = 85,
    MissingPageEntries = 86,
    InvalidPageEntryCount = 87,
    PageEntryCapacityTooSmall = 88,
    InvalidPageImage = 89,
    PageImageEntryMismatch = 90,
    MissingStateDestination = 91,
    InvalidStateDestinationCount = 92,
    StateDestinationCapacityTooSmall = 93,
    MisalignedStateDestination = 94,
    StateDestinationSpanOverflow = 95,
    StateAddressMismatch = 96,
    MissingPriorPlanningEvidence = 97,
    InvalidPriorPlanningEvidence = 98,
    MissingCanaryDestination = 99,
    InvalidCanaryDestinationCount = 100,
    CanaryDestinationCapacityTooSmall = 101,
    MisalignedCanaryDestination = 102,
    CanaryDestinationSpanOverflow = 103,
    RflagsRestoreIntentMismatch = 104
}

/// <summary>
/// Status returned in EAX and recorded by the emitted stream after CLI.
/// Pre-CLI rejection returns one of these values without changing the state.
/// </summary>
public enum M01OwnedCr3CompositeTransitionRuntimeStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidState = 2,
    FirmwareCallbackForbidden = 3,
    InvalidCodeRange = 4,
    InvalidStackRange = 5,
    InvalidReturnAddress = 6,
    InvalidTableArena = 7,
    InvalidOwnedCr3 = 8,
    InvalidProbeContract = 9,
    InvalidCanary = 10,
    InvalidMode = 11,
    PhysicalAddressWidthUnsupported = 12,
    InterruptDisableReadbackMismatch = 13,
    PgeClearReadbackMismatch = 14,
    OwnedCr3ReadbackMismatch = 15,
    CanaryBeforeMismatch = 16,
    CanaryAfterMismatch = 17,
    ProbeHierarchyMalformed = 18,
    ProbePteAddressMismatch = 19,
    ProbePteBeforeMismatch = 20,
    ProbePteAfterMismatch = 21,
    ProbeReturnMismatch = 22,
    PageZeroPresent = 23,
    PageZeroMalformed = 24,
    FirmwareCr3ReadbackMismatch = 25,
    Cr4RestoreReadbackMismatch = 26,
    RflagsRestoreReadbackMismatch = 27,
    RspRestoreMismatch = 28,
    PostRestoreCanaryMismatch = 29
}

public enum M01OwnedCr3CompositeTransitionCheckpoint : byte
{
    Uninitialized = 0,
    EntryValidated = 1,
    ModeValidated = 2,
    InterruptsDisabled = 3,
    PgeCleared = 4,
    OwnedCr3Loaded = 5,
    CanaryUpdated = 6,
    ProbePteLocated = 7,
    ProbePteCommitted = 8,
    ProbeInvalidated = 9,
    ProbePteVerified = 10,
    ProbeReturned = 11,
    PageZeroAbsent = 12,
    FirmwareCr3Restored = 13,
    Cr4Restored = 14,
    RflagsRestored = 15,
    Returned = 16,
    ReturnPrepared = 17
}

public enum M01OwnedCr3CompositeTransitionTerminalKind : byte
{
    Uninitialized = 0,
    Returned = 1,
    Timeout = 2,
    Reset = 3,
    TripleFault = 4
}

public enum M01OwnedCr3CompositeTransitionResultPhase : byte
{
    Uninitialized = 0,
    StatePrepared = 1,
    Materialized = 2,
    ReadbackVerified = 3,
    ObservedAudit = 4
}

public enum M01OwnedCr3CompositeTransitionPageZeroLevel : byte
{
    Uninitialized = 0,
    AbsentAtPml4 = 1,
    AbsentAtPdpt = 2,
    AbsentAtPd = 3,
    AbsentAtPt = 4,
    Present = 5,
    Malformed = 6
}

public enum M01OwnedCr3CompositeTransitionRangeRole : byte
{
    Uninitialized = 0,
    CompositeCode = 1,
    OwnedStack = 2,
    LoadedImageContinuationPage = 3,
    ProbePage = 4,
    CompleteTableArena = 5,
    RuntimeStateAndCanary = 6,
    OwnedRoot = 7,
    CompositeCodeAndCallGate = 8,
    CompositeOwnedStack = 9,
    ProtectionProbePage = 10,
    CompleteTableAndStateArena = 11,
    RuntimeStateAndCanaryPage = 12,
    OwnedRootPage = 13
}

/// <summary>
/// Exact source ledgers which must agree for a range binding.
/// </summary>
public enum M01OwnedCr3CompositeTransitionRangeProvenance : byte
{
    Uninitialized = 0,
    AllocationReservationAndIdentityMapping = 1,
    ReservationAndIdentityMapping = 2,
    ContainedInAllocationReservationAndIdentityMapping = 3
}

public enum M01OwnedCr3CompositeTransitionOperation : byte
{
    Uninitialized = 0,
    ValidateState = 1,
    ValidateMode = 2,
    DisableInterrupts = 3,
    ClearPge = 4,
    LoadOwnedCr3 = 5,
    UpdateCanary = 6,
    WalkProbeHierarchy = 7,
    CommitProbePte = 8,
    InvalidateProbePage = 9,
    VerifyProbePte = 10,
    InvokeExactProbe = 11,
    AuditPageZero = 12,
    RestoreFirmwareCr3 = 13,
    RestoreCr4 = 14,
    RestoreRflags = 15,
    Return = 16
}

public enum M01OwnedCr3CompositeTransitionRecoveryClass : byte
{
    Uninitialized = 0,
    PreCliNoStateMutation = 1,
    RestoreRflags = 2,
    RestoreCr4ThenRflags = 3,
    RestoreCr3ThenCr4ThenRflags = 4,
    CommittedProbeRemainsReadOnlyExecutableThenRestoreRegisters = 5,
    SuccessRestoreRegisters = 6,
    MutationAttemptedThenRestoreRegisters = 7
}

/// <summary>
/// Cross-binds one runtime-dereferenced range to existing allocation,
/// reservation, and normalized identity-mapping ledgers.
/// </summary>
public readonly struct M01OwnedCr3CompositeTransitionRangeBinding
{
    public const ulong StructureSize = 80;
    public const ulong NoIndex = ~0UL;

    private readonly M01OwnedCr3CompositeTransitionRangeRole _role;
    private readonly M01OwnedCr3CompositeTransitionRangeProvenance
        _provenance;
    private readonly ushort _reserved0;
    private readonly uint _reserved1;
    private readonly PhysicalAddress _physicalStart;
    private readonly PhysicalAddress _physicalEndExclusive;
    private readonly VirtualAddress _virtualStart;
    private readonly VirtualAddress _virtualEndExclusive;
    private readonly ulong _allocationIndex;
    private readonly ulong _reservationIndex;
    private readonly ulong _identityMappingIndex;
    private readonly ulong _reserved2;
    private readonly ulong _reserved3;

    public M01OwnedCr3CompositeTransitionRangeBinding(
        M01OwnedCr3CompositeTransitionRangeRole role,
        M01OwnedCr3CompositeTransitionRangeProvenance provenance,
        PhysicalAddress physicalStart,
        PhysicalAddress physicalEndExclusive,
        VirtualAddress virtualStart,
        VirtualAddress virtualEndExclusive,
        ulong allocationIndex,
        ulong reservationIndex,
        ulong identityMappingIndex)
    {
        _role = role;
        _provenance = provenance;
        _reserved0 = 0;
        _reserved1 = 0;
        _physicalStart = physicalStart;
        _physicalEndExclusive = physicalEndExclusive;
        _virtualStart = virtualStart;
        _virtualEndExclusive = virtualEndExclusive;
        _allocationIndex = allocationIndex;
        _reservationIndex = reservationIndex;
        _identityMappingIndex = identityMappingIndex;
        _reserved2 = 0;
        _reserved3 = 0;
    }

    public M01OwnedCr3CompositeTransitionRangeRole Role => _role;

    public M01OwnedCr3CompositeTransitionRangeProvenance Provenance =>
        _provenance;

    public PhysicalAddress PhysicalStart => _physicalStart;

    public PhysicalAddress PhysicalEndExclusive =>
        _physicalEndExclusive;

    public VirtualAddress VirtualStart => _virtualStart;

    public VirtualAddress VirtualEndExclusive => _virtualEndExclusive;

    public ulong AllocationIndex => _allocationIndex;

    public ulong ReservationIndex => _reservationIndex;

    public ulong IdentityMappingIndex => _identityMappingIndex;

    internal bool ReservedFieldsAreZero =>
        _reserved0 == 0
        && _reserved1 == 0
        && _reserved2 == 0
        && _reserved3 == 0;
}

/// <summary>
/// One stable semantic interval in the relocation-free byte plan.
/// </summary>
public readonly struct M01OwnedCr3CompositeTransitionSemanticRecord
{
    public const ulong StructureSize = 24;
    public const uint NoBranchTarget = ~0U;

    private readonly M01OwnedCr3CompositeTransitionOperation _operation;
    private readonly M01OwnedCr3CompositeTransitionRecoveryClass
        _recoveryClass;
    private readonly ushort _reserved;
    private readonly uint _byteOffset;
    private readonly uint _byteLength;
    private readonly uint _branchTargetOffset;
    private readonly ulong _stateOffsetOrConstant;

    internal M01OwnedCr3CompositeTransitionSemanticRecord(
        M01OwnedCr3CompositeTransitionOperation operation,
        M01OwnedCr3CompositeTransitionRecoveryClass recoveryClass,
        uint byteOffset,
        uint byteLength,
        uint branchTargetOffset,
        ulong stateOffsetOrConstant)
    {
        _operation = operation;
        _recoveryClass = recoveryClass;
        _reserved = 0;
        _byteOffset = byteOffset;
        _byteLength = byteLength;
        _branchTargetOffset = branchTargetOffset;
        _stateOffsetOrConstant = stateOffsetOrConstant;
    }

    public M01OwnedCr3CompositeTransitionOperation Operation =>
        _operation;

    public M01OwnedCr3CompositeTransitionRecoveryClass RecoveryClass =>
        _recoveryClass;

    public uint ByteOffset => _byteOffset;

    public uint ByteLength => _byteLength;

    public uint BranchTargetOffset => _branchTargetOffset;

    public ulong StateOffsetOrConstant => _stateOffsetOrConstant;

    internal bool ReservedFieldIsZero => _reserved == 0;
}

/// <summary>
/// Identity-mapped, caller-owned input and readback POD consumed by the
/// emitted Windows-x64 ABI stream. Raw addresses exist here only at the
/// register boundary.
/// </summary>
public struct M01OwnedCr3CompositeTransitionRuntimeState
{
    public const uint StructureSize = 480;
    public const ushort StructureVersion = 1;

    private uint _structureSize;
    private ushort _structureVersion;
    private byte _isInitialized;
    private byte _reserved0;
    private ulong _ownedCr3Raw;
    private ulong _tablePageArenaStart;
    private ulong _tablePageArenaEndExclusive;
    private ulong _tableStateArenaStart;
    private ulong _tableStateArenaEndExclusive;
    private ulong _compositeCodeStart;
    private ulong _compositeCodeEndExclusive;
    private ulong _continuationPageStart;
    private ulong _continuationPageEndExclusive;
    private ulong _expectedReturnAddress;
    private ulong _stackStart;
    private ulong _stackEndExclusive;
    private ulong _probePageStart;
    private ulong _probePageEndExclusive;
    private ulong _probeEntry;
    private ulong _expectedProbePteAddress;
    private ulong _initialProbePteRaw;
    private ulong _finalProbePteRaw;
    private ulong _canaryAddress;
    private ulong _expectedCanary;
    private ulong _replacementCanary;
    private uint _expectedProbeReturn;
    private uint _prohibitedFirmwareCallbackCount;
    private ulong _expectedSelfAddress;
    // The low bits carry the audited critical-page count. The remaining
    // permitted bits bind the exact PDPT, PD, and PT page ordinals followed
    // by the prepared probe walk. Keeping this as one immutable word
    // preserves the v1 POD size while preventing an arena-valid alias chain
    // from being promoted as the prepared hierarchy.
    private ulong _expectedProbeHierarchyIdentity;
    private ulong _originalRflags;
    private ulong _originalCr0;
    private ulong _originalCr3Raw;
    private ulong _originalCr4Raw;
    private ulong _originalEfer;
    private ulong _physicalAddressMask;
    private ulong _entryRsp;
    private ulong _observedReturnAddress;
    private ulong _observedRip;
    private ulong _cliRflagsReadback;
    private ulong _clearedCr4Readback;
    private ulong _ownedCr3Readback;
    private ulong _canaryBefore;
    private ulong _canaryAfter;
    private ulong _probePml4e;
    private ulong _probePdpte;
    private ulong _probePde;
    private ulong _probePteBefore;
    private ulong _probePteAfter;
    private ulong _invlpgTarget;
    private ulong _probeReturn;
    private ulong _pageZeroPml4e;
    private ulong _pageZeroPdpte;
    private ulong _pageZeroPde;
    private ulong _pageZeroPte;
    private ulong _committedProbePteReadback;
    private ulong _firmwareCr3Readback;
    private ulong _postRestoreCanaryReadback;
    private ulong _restoredCr4Readback;
    private ulong _restoredRflagsIntent;
    private ulong _exitRsp;
    private ulong _invlpgCount;
    private ulong _probeInvocationCount;
    private M01OwnedCr3CompositeTransitionRuntimeStatus _runtimeStatus;
    private M01OwnedCr3CompositeTransitionCheckpoint _checkpoint;
    private M01OwnedCr3CompositeTransitionPageZeroLevel _pageZeroLevel;
    private byte _mutationCommitted;
    private byte _pgeWasCleared;
    private byte _ownedRootWasLoaded;
    private byte _probeReturned;
    private M01OwnedCr3CompositeTransitionCheckpoint
        _progressCheckpoint;
    private ulong _reservedOutput1;

    internal M01OwnedCr3CompositeTransitionRuntimeState(
        ulong ownedCr3Raw,
        ulong tablePageArenaStart,
        ulong tablePageArenaEndExclusive,
        ulong tableStateArenaStart,
        ulong tableStateArenaEndExclusive,
        ulong compositeCodeStart,
        ulong compositeCodeEndExclusive,
        ulong continuationPageStart,
        ulong continuationPageEndExclusive,
        ulong expectedReturnAddress,
        ulong stackStart,
        ulong stackEndExclusive,
        ulong probePageStart,
        ulong probePageEndExclusive,
        ulong probeEntry,
        ulong expectedProbePteAddress,
        ulong initialProbePteRaw,
        ulong finalProbePteRaw,
        ulong canaryAddress,
        ulong expectedCanary,
        ulong replacementCanary,
        uint expectedProbeReturn,
        ulong expectedSelfAddress,
        ulong expectedProbeHierarchyIdentity)
    {
        this = default;
        _structureSize = StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _reserved0 = 0;
        _ownedCr3Raw = ownedCr3Raw;
        _tablePageArenaStart = tablePageArenaStart;
        _tablePageArenaEndExclusive = tablePageArenaEndExclusive;
        _tableStateArenaStart = tableStateArenaStart;
        _tableStateArenaEndExclusive = tableStateArenaEndExclusive;
        _compositeCodeStart = compositeCodeStart;
        _compositeCodeEndExclusive = compositeCodeEndExclusive;
        _continuationPageStart = continuationPageStart;
        _continuationPageEndExclusive = continuationPageEndExclusive;
        _expectedReturnAddress = expectedReturnAddress;
        _stackStart = stackStart;
        _stackEndExclusive = stackEndExclusive;
        _probePageStart = probePageStart;
        _probePageEndExclusive = probePageEndExclusive;
        _probeEntry = probeEntry;
        _expectedProbePteAddress = expectedProbePteAddress;
        _initialProbePteRaw = initialProbePteRaw;
        _finalProbePteRaw = finalProbePteRaw;
        _canaryAddress = canaryAddress;
        _expectedCanary = expectedCanary;
        _replacementCanary = replacementCanary;
        _expectedProbeReturn = expectedProbeReturn;
        _prohibitedFirmwareCallbackCount = 0;
        _expectedSelfAddress = expectedSelfAddress;
        _expectedProbeHierarchyIdentity =
            expectedProbeHierarchyIdentity;
        _progressCheckpoint =
            M01OwnedCr3CompositeTransitionCheckpoint.Uninitialized;
        _reservedOutput1 = 0;
    }

    public uint Size => _structureSize;
    public ushort Version => _structureVersion;
    public bool IsInitialized => _isInitialized == 1;
    public ulong OwnedCr3Raw => _ownedCr3Raw;
    public ulong TablePageArenaStart => _tablePageArenaStart;
    public ulong TablePageArenaEndExclusive =>
        _tablePageArenaEndExclusive;
    public ulong TableStateArenaStart => _tableStateArenaStart;
    public ulong TableStateArenaEndExclusive =>
        _tableStateArenaEndExclusive;
    public ulong CompositeCodeStart => _compositeCodeStart;
    public ulong CompositeCodeEndExclusive =>
        _compositeCodeEndExclusive;
    public ulong CompositeCodeAndCallGatePageStart =>
        _compositeCodeStart;
    public ulong CompositeCodeAndCallGatePageEndExclusive =>
        _compositeCodeEndExclusive;
    public ulong ContinuationPageStart => _continuationPageStart;
    public ulong ContinuationPageEndExclusive =>
        _continuationPageEndExclusive;
    public ulong ExpectedReturnAddress => _expectedReturnAddress;
    public ulong ExactPostCallReturnAddress =>
        _expectedReturnAddress;
    public ulong StackStart => _stackStart;
    public ulong StackEndExclusive => _stackEndExclusive;
    public ulong ProbePageStart => _probePageStart;
    public ulong ProbePageEndExclusive => _probePageEndExclusive;
    public ulong ProbeEntry => _probeEntry;
    public ulong ExpectedProbePteAddress =>
        _expectedProbePteAddress;
    public ulong InitialProbePteRaw => _initialProbePteRaw;
    public ulong FinalProbePteRaw => _finalProbePteRaw;
    public ulong CanaryAddress => _canaryAddress;
    public ulong ExpectedCanary => _expectedCanary;
    public ulong ReplacementCanary => _replacementCanary;
    public uint ExpectedProbeReturn => _expectedProbeReturn;
    public uint ProhibitedFirmwareCallbackCount =>
        _prohibitedFirmwareCallbackCount;
    public ulong ExpectedSelfAddress => _expectedSelfAddress;
    public ulong ExpectedProbeHierarchyIdentity =>
        _expectedProbeHierarchyIdentity;
    public ulong ExpectedCriticalMappingPageCount =>
        _expectedProbeHierarchyIdentity
            & M01OwnedCr3CompositeTransition
                .CriticalMappingPageCountMask;
    public ulong ExpectedProbePdptPageOrdinal =>
        (_expectedProbeHierarchyIdentity
            >> M01OwnedCr3CompositeTransition
                .ProbePdptPageOrdinalShift)
        & M01OwnedCr3CompositeTransition
            .ProbeHierarchyPageOrdinalMask;
    public ulong ExpectedProbePdPageOrdinal =>
        (_expectedProbeHierarchyIdentity
            >> M01OwnedCr3CompositeTransition
                .ProbePdPageOrdinalShift)
        & M01OwnedCr3CompositeTransition
            .ProbeHierarchyPageOrdinalMask;
    public ulong ExpectedProbePtPageOrdinal =>
        (_expectedProbeHierarchyIdentity
            >> M01OwnedCr3CompositeTransition
                .ProbePtPageOrdinalShift)
        & M01OwnedCr3CompositeTransition
            .ProbeHierarchyPageOrdinalMask;
    public ulong OriginalRflags => _originalRflags;
    public ulong OriginalCr0 => _originalCr0;
    public ulong OriginalCr3Raw => _originalCr3Raw;
    public ulong OriginalCr4Raw => _originalCr4Raw;
    public ulong OriginalEfer => _originalEfer;
    public ulong PhysicalAddressMask => _physicalAddressMask;
    public ulong EntryRsp => _entryRsp;
    public ulong ObservedReturnAddress => _observedReturnAddress;
    public ulong ObservedRip => _observedRip;
    public ulong CliRflagsReadback => _cliRflagsReadback;
    public ulong ClearedCr4Readback => _clearedCr4Readback;
    public ulong OwnedCr3Readback => _ownedCr3Readback;
    public ulong CanaryBefore => _canaryBefore;
    public ulong CanaryAfter => _canaryAfter;
    public ulong ProbePml4e => _probePml4e;
    public ulong ProbePdpte => _probePdpte;
    public ulong ProbePde => _probePde;
    public ulong ProbePteBefore => _probePteBefore;
    public ulong ProbePteAfter => _probePteAfter;
    public ulong InvlpgTarget => _invlpgTarget;
    public ulong ProbeReturn => _probeReturn;
    public ulong PageZeroPml4e => _pageZeroPml4e;
    public ulong PageZeroPdpte => _pageZeroPdpte;
    public ulong PageZeroPde => _pageZeroPde;
    public ulong PageZeroPte => _pageZeroPte;
    public ulong CommittedProbePteReadback =>
        _committedProbePteReadback;
    public ulong FirmwareCr3Readback => _firmwareCr3Readback;
    public ulong PostRestoreCanaryReadback =>
        _postRestoreCanaryReadback;
    public ulong RestoredCr4Readback => _restoredCr4Readback;
    public ulong RestoredRflagsIntent => _restoredRflagsIntent;
    public ulong ExitRsp => _exitRsp;
    public ulong InvlpgCount => _invlpgCount;
    public ulong ProbeInvocationCount => _probeInvocationCount;
    public M01OwnedCr3CompositeTransitionRuntimeStatus RuntimeStatus =>
        _runtimeStatus;
    public M01OwnedCr3CompositeTransitionCheckpoint Checkpoint =>
        _checkpoint;
    public M01OwnedCr3CompositeTransitionPageZeroLevel PageZeroLevel =>
        _pageZeroLevel;
    public bool MutationCommitted => _mutationCommitted == 1;
    public bool OriginalPgeWasSet => _pgeWasCleared == 1;
    // Compatibility alias retained for the initial host-test contract.
    public bool PgeWasCleared => _pgeWasCleared == 1;
    public bool OwnedRootWasLoaded => _ownedRootWasLoaded == 1;
    public bool ProbeReturned => _probeReturned == 1;
    public M01OwnedCr3CompositeTransitionCheckpoint ProgressCheckpoint =>
        _progressCheckpoint;

#if !NOSTDLIB_EFI
    public static M01OwnedCr3CompositeTransitionRuntimeState
        CreateObserved(
            M01OwnedCr3CompositeTransitionRuntimeState initializedState,
            ulong originalRflags,
            ulong originalCr0,
            ulong originalCr3Raw,
            ulong originalCr4Raw,
            ulong originalEfer,
            ulong physicalAddressMask,
            ulong entryRsp,
            ulong observedReturnAddress,
            ulong observedRip,
            ulong cliRflagsReadback,
            ulong clearedCr4Readback,
            ulong ownedCr3Readback,
            ulong canaryBefore,
            ulong canaryAfter,
            ulong probePml4e,
            ulong probePdpte,
            ulong probePde,
            ulong probePteBefore,
            ulong probePteAfter,
            ulong invlpgTarget,
            ulong probeReturn,
            ulong pageZeroPml4e,
            ulong pageZeroPdpte,
            ulong pageZeroPde,
            ulong pageZeroPte,
            ulong committedProbePteReadback,
            ulong firmwareCr3Readback,
            ulong postRestoreCanaryReadback,
            ulong restoredCr4Readback,
            ulong restoredRflagsIntent,
            ulong exitRsp,
            ulong invlpgCount,
            ulong probeInvocationCount,
            uint firmwareCallbackCount,
            M01OwnedCr3CompositeTransitionRuntimeStatus runtimeStatus,
            M01OwnedCr3CompositeTransitionCheckpoint checkpoint,
            M01OwnedCr3CompositeTransitionPageZeroLevel pageZeroLevel,
            bool mutationCommitted,
            bool pgeWasCleared,
            bool ownedRootWasLoaded,
            bool probeReturned,
            M01OwnedCr3CompositeTransitionCheckpoint
                progressCheckpoint =
                    M01OwnedCr3CompositeTransitionCheckpoint
                        .Uninitialized)
    {
        initializedState._originalRflags = originalRflags;
        initializedState._originalCr0 = originalCr0;
        initializedState._originalCr3Raw = originalCr3Raw;
        initializedState._originalCr4Raw = originalCr4Raw;
        initializedState._originalEfer = originalEfer;
        initializedState._physicalAddressMask = physicalAddressMask;
        initializedState._entryRsp = entryRsp;
        initializedState._observedReturnAddress =
            observedReturnAddress;
        initializedState._observedRip = observedRip;
        initializedState._cliRflagsReadback = cliRflagsReadback;
        initializedState._clearedCr4Readback =
            clearedCr4Readback;
        initializedState._ownedCr3Readback = ownedCr3Readback;
        initializedState._canaryBefore = canaryBefore;
        initializedState._canaryAfter = canaryAfter;
        initializedState._probePml4e = probePml4e;
        initializedState._probePdpte = probePdpte;
        initializedState._probePde = probePde;
        initializedState._probePteBefore = probePteBefore;
        initializedState._probePteAfter = probePteAfter;
        initializedState._invlpgTarget = invlpgTarget;
        initializedState._probeReturn = probeReturn;
        initializedState._pageZeroPml4e = pageZeroPml4e;
        initializedState._pageZeroPdpte = pageZeroPdpte;
        initializedState._pageZeroPde = pageZeroPde;
        initializedState._pageZeroPte = pageZeroPte;
        initializedState._committedProbePteReadback =
            committedProbePteReadback;
        initializedState._firmwareCr3Readback =
            firmwareCr3Readback;
        initializedState._postRestoreCanaryReadback =
            postRestoreCanaryReadback;
        initializedState._restoredCr4Readback =
            restoredCr4Readback;
        initializedState._restoredRflagsIntent =
            restoredRflagsIntent;
        initializedState._exitRsp = exitRsp;
        initializedState._invlpgCount = invlpgCount;
        initializedState._probeInvocationCount =
            probeInvocationCount;
        initializedState._prohibitedFirmwareCallbackCount =
            firmwareCallbackCount;
        initializedState._runtimeStatus = runtimeStatus;
        initializedState._checkpoint = checkpoint;
        initializedState._pageZeroLevel = pageZeroLevel;
        initializedState._mutationCommitted =
            mutationCommitted ? (byte)1 : (byte)0;
        initializedState._pgeWasCleared =
            pgeWasCleared ? (byte)1 : (byte)0;
        initializedState._ownedRootWasLoaded =
            ownedRootWasLoaded ? (byte)1 : (byte)0;
        initializedState._probeReturned =
            probeReturned ? (byte)1 : (byte)0;
        initializedState._progressCheckpoint = progressCheckpoint;
        return initializedState;
    }
#endif

    internal bool ReservedFieldsAreZero =>
        _reserved0 == 0
        && _reservedOutput1 == 0;
}

/// <summary>
/// Complete value-only result for materialization, range binding, byte
/// readback, or observed-state evaluation.
/// </summary>
public readonly struct M01OwnedCr3CompositeTransitionResult
{
    public const ulong NoIndex = ~0UL;

    private readonly M01OwnedCr3CompositeTransitionStatus _status;
    private readonly M01OwnedCr3CompositeTransitionResultPhase _phase;
    private readonly M01OwnedCr3CompositeTransitionTerminalKind
        _terminalKind;
    private readonly M01OwnedCr3CompositeTransitionRuntimeState _state;
    private readonly ulong _entryAddress;
    private readonly ulong _byteCount;
    private readonly ulong _byteCapacity;
    private readonly ulong _semanticCount;
    private readonly ulong _semanticCapacity;
    private readonly ulong _invalidIndex;
    private readonly ulong _expectedValue;
    private readonly ulong _observedValue;
    private readonly ulong _fnv1a64;

    internal M01OwnedCr3CompositeTransitionResult(
        M01OwnedCr3CompositeTransitionStatus status,
        M01OwnedCr3CompositeTransitionResultPhase phase,
        M01OwnedCr3CompositeTransitionTerminalKind terminalKind,
        M01OwnedCr3CompositeTransitionRuntimeState state,
        ulong entryAddress,
        ulong byteCount,
        ulong byteCapacity,
        ulong semanticCount,
        ulong semanticCapacity,
        ulong invalidIndex,
        ulong expectedValue,
        ulong observedValue,
        ulong fnv1a64)
    {
        _status = status;
        _phase = phase;
        _terminalKind = terminalKind;
        _state = state;
        _entryAddress = entryAddress;
        _byteCount = byteCount;
        _byteCapacity = byteCapacity;
        _semanticCount = semanticCount;
        _semanticCapacity = semanticCapacity;
        _invalidIndex = invalidIndex;
        _expectedValue = expectedValue;
        _observedValue = observedValue;
        _fnv1a64 = fnv1a64;
    }

    public M01OwnedCr3CompositeTransitionStatus Status => _status;
    public bool IsSuccess =>
        _status == M01OwnedCr3CompositeTransitionStatus.Success;
    public M01OwnedCr3CompositeTransitionResultPhase Phase => _phase;
    public bool IsStatePrepared =>
        IsSuccess
        && _phase
            == M01OwnedCr3CompositeTransitionResultPhase.StatePrepared;
    public bool IsMaterialized =>
        IsSuccess
        && _phase
            == M01OwnedCr3CompositeTransitionResultPhase.Materialized;
    public bool IsReadbackVerified =>
        IsSuccess
        && _phase
            == M01OwnedCr3CompositeTransitionResultPhase
                .ReadbackVerified;
    public bool IsObservedAudit =>
        IsSuccess
        && _phase
            == M01OwnedCr3CompositeTransitionResultPhase.ObservedAudit;
    public bool ProvesCompositeEncodingReadback =>
        IsReadbackVerified;
    public M01OwnedCr3CompositeTransitionTerminalKind TerminalKind =>
        _terminalKind;
    public M01OwnedCr3CompositeTransitionRuntimeState State => _state;
    public ulong EntryAddress => _entryAddress;
    public ulong ByteCount => _byteCount;
    public ulong ByteCapacity => _byteCapacity;
    public ulong RequiredByteCount =>
        M01OwnedCr3CompositeTransition.MaterializedPageByteLength;
    public ulong RequiredInstructionByteLength =>
        M01OwnedCr3CompositeTransition.RequiredByteLength;
    public ulong RequiredMaterializedPageByteLength =>
        M01OwnedCr3CompositeTransition.MaterializedPageByteLength;
    public ulong SemanticCount => _semanticCount;
    public ulong SemanticCapacity => _semanticCapacity;
    public ulong RequiredSemanticCount =>
        M01OwnedCr3CompositeTransition.AuditedSemanticRecordCount;
    public ulong InvalidIndex => _invalidIndex;
    public ulong ExpectedValue => _expectedValue;
    public ulong ObservedValue => _observedValue;
    public ulong Fnv1a64 => _fnv1a64;
    public bool DefinesSingleCompositeOwnedCr3Interval =>
        M01OwnedCr3CompositeTransition
            .DefinesSingleCompositeOwnedCr3Interval;
    public bool ExecutesCompositeOwnedCr3Interval =>
        M01OwnedCr3CompositeTransition
            .ExecutesCompositeOwnedCr3Interval;
    public bool IsGuestEvidence =>
        M01OwnedCr3CompositeTransition.IsGuestEvidence;
}

/// <summary>
/// Reports the first rejected composite call-gate boundary. The zero-valued
/// default is never success and existing values are append-only.
/// </summary>
public enum M01OwnedCr3CompositeCallGateStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    MissingPriorPlanningEvidence = 2,
    InvalidPriorPlanningEvidence = 3,
    MissingCompositePreparation = 4,
    InvalidCompositePreparation = 5,
    MissingAllocationRecords = 6,
    InvalidAllocationRecordCount = 7,
    AllocationRecordCapacityMismatch = 8,
    MissingReservationRecords = 9,
    InvalidReservationRecordCount = 10,
    ReservationRecordCapacityMismatch = 11,
    MissingIdentityMappings = 12,
    InvalidIdentityMappingCount = 13,
    IdentityMappingCapacityMismatch = 14,
    MissingCodeBinding = 15,
    InvalidCodeBindingCount = 16,
    CodeBindingCapacityMismatch = 17,
    InvalidCodeBinding = 18,
    MissingObservation = 19,
    InvalidObservationCount = 20,
    ObservationCapacityMismatch = 21,
    MisalignedObservation = 22,
    ObservationSpanOverflow = 23,
    ObservationAlias = 24,
    ObservationNotPristine = 25,
    RepeatedUse = 26,
    InvalidCodeBase = 27,
    MisalignedCodeBase = 28,
    CodeRangeOverflow = 29,
    InvalidCompositeEntry = 30,
    InvalidGateEntry = 31,
    InvalidReturnSite = 32,
    InvalidCallTarget = 33,
    CodeLayoutOverlap = 34,
    InvalidModePreflight = 35,
    TrapFlagSet = 36,
    DirectionFlagSet = 37,
    CetUnsupported = 38,
    FiveLevelPagingUnsupported = 39,
    PcideUnsupported = 40,
    InvalidInterruptPolicy = 41,
    InvalidPgePolicy = 42,
    MissingCodeStorage = 43,
    InvalidCodeStorageCount = 44,
    CodeStorageCapacityMismatch = 45,
    MisalignedCodeStorage = 46,
    CodeStorageSpanOverflow = 47,
    StorageAlias = 48,
    MissingSemanticStorage = 49,
    InvalidSemanticCount = 50,
    SemanticCapacityMismatch = 51,
    MisalignedSemanticStorage = 52,
    SemanticSpanOverflow = 53,
    InternalEncodingFailure = 54,
    ByteMismatch = 55,
    MissingEvidence = 56,
    InvalidEvidence = 57,
    InvalidTerminalKind = 58,
    Timeout = 59,
    Reset = 60,
    TripleFault = 61,
    CompletionSignatureMismatch = 62,
    CallCountMismatch = 63,
    ObservationCountMismatch = 64,
    CompositeReturnMismatch = 65,
    PostReturnRflagsMismatch = 66,
    PostReturnInterruptMismatch = 67,
    FirmwareCr3Mismatch = 68,
    FirmwareCr4Mismatch = 69,
    BoundaryCanaryMismatch = 70,
    CompositeStateAuditFailed = 71,
    ReservedStateNonzero = 72,
    InvalidGateStackTranscript = 73
}

public enum M01OwnedCr3CompositeCallGatePhase : byte
{
    Uninitialized = 0,
    Prepared = 1,
    Materialized = 2,
    ReadbackVerified = 3,
    ObservedAudit = 4
}

public enum M01OwnedCr3CompositeCallGateObservationLifecycle : byte
{
    Uninitialized = 0,
    Armed = 1,
    InProgress = 2,
    Completed = 3
}

public enum M01OwnedCr3CompositeCallGateInterruptPolicy : byte
{
    Uninitialized = 0,
    PreserveEntryState = 1
}

public enum M01OwnedCr3CompositeCallGatePgePolicy : byte
{
    Uninitialized = 0,
    ClearDuringOwnedIntervalAndRestoreExact = 1
}

/// <summary>
/// Caller-owned input/output POD used by the emitted Windows-x64 call gate.
/// The fixed canary is outside the composite runtime-state mutation.
/// </summary>
public struct M01OwnedCr3CompositeCallGateObservation
{
    public const uint StructureSize = 160;
    public const ushort StructureVersion = 1;
    public const uint CompletionSignature = 0x4b47_534f;

    internal uint HeaderSize;
    internal ushort HeaderVersion;
    internal byte IsInitializedValue;
    internal M01OwnedCr3CompositeCallGateObservationLifecycle
        LifecycleValue;
    internal ulong ExpectedSelfAddressValue;
    internal ulong ExpectedCodeBaseValue;
    internal ulong ExpectedCompositeEntryAddressValue;
    internal ulong ExpectedGateEntryAddressValue;
    internal ulong ExpectedReturnSiteAddressValue;
    internal ulong ExpectedOriginalRflagsValue;
    internal ulong ExpectedFirmwareCr3RawValue;
    internal ulong ExpectedFirmwareCr4RawValue;
    internal ulong ExpectedBoundaryCanaryValue;
    internal uint ExpectedCompositeReturnValueValue;
    internal byte ExpectedInterruptEnabledValue;
    internal byte ExpectedPgeWasSetValue;
    internal M01OwnedCr3CompositeCallGateInterruptPolicy
        InterruptPolicyValue;
    internal M01OwnedCr3CompositeCallGatePgePolicy PgePolicyValue;
    internal ulong BoundaryCanaryValue;
    internal ulong CanaryBeforeValue;
    internal ulong PostReturnRflagsValue;
    internal ulong PostReturnCr3RawValue;
    internal ulong PostReturnCr4RawValue;
    internal ulong CanaryAfterValue;
    internal uint CompositeReturnValueValue;
    internal byte CallCountValue;
    internal byte PostReturnObservationCountValue;
    internal ushort ReservedOutput0;
    internal uint CompletionSignatureValue;
    internal uint ReservedOutput1;
    internal ulong ReservedOutput2;

    public uint Size => HeaderSize;
    public ushort Version => HeaderVersion;
    public bool IsInitialized => IsInitializedValue == 1;
    public M01OwnedCr3CompositeCallGateObservationLifecycle Lifecycle =>
        LifecycleValue;
    public bool IsArmed =>
        HasCanonicalHeader
        && LifecycleValue
            == M01OwnedCr3CompositeCallGateObservationLifecycle.Armed
        && OutputsAreZero;
    public bool IsCompleted =>
        HasCanonicalHeader
        && LifecycleValue
            == M01OwnedCr3CompositeCallGateObservationLifecycle.Completed
        && CompletionSignatureValue == CompletionSignature;
    public ulong ExpectedSelfAddress => ExpectedSelfAddressValue;
    public ulong ExpectedCodeBase => ExpectedCodeBaseValue;
    public ulong ExpectedCompositeEntryAddress =>
        ExpectedCompositeEntryAddressValue;
    public ulong ExpectedGateEntryAddress =>
        ExpectedGateEntryAddressValue;
    public ulong ExpectedReturnSiteAddress =>
        ExpectedReturnSiteAddressValue;
    public ulong ExpectedOriginalRflags => ExpectedOriginalRflagsValue;
    public ulong ExpectedFirmwareCr3Raw =>
        ExpectedFirmwareCr3RawValue;
    public ulong ExpectedFirmwareCr4Raw =>
        ExpectedFirmwareCr4RawValue;
    public ulong ExpectedBoundaryCanary =>
        ExpectedBoundaryCanaryValue;
    public uint ExpectedCompositeReturnValue =>
        ExpectedCompositeReturnValueValue;
    public bool ExpectedInterruptEnabled =>
        ExpectedInterruptEnabledValue == 1;
    public bool ExpectedPgeWasSet => ExpectedPgeWasSetValue == 1;
    public M01OwnedCr3CompositeCallGateInterruptPolicy
        InterruptPolicy => InterruptPolicyValue;
    public M01OwnedCr3CompositeCallGatePgePolicy PgePolicy =>
        PgePolicyValue;
    public ulong BoundaryCanary => BoundaryCanaryValue;
    public ulong CanaryBefore => CanaryBeforeValue;
    public ulong PostReturnRflags => PostReturnRflagsValue;
    public ulong PostReturnCr3Raw => PostReturnCr3RawValue;
    public ulong PostReturnCr4Raw => PostReturnCr4RawValue;
    public ulong CanaryAfter => CanaryAfterValue;
    public uint CompositeReturnValue => CompositeReturnValueValue;
    public uint CallCount => CallCountValue;
    public uint PostReturnObservationCount =>
        PostReturnObservationCountValue;
    public uint ObservedCompletionSignature =>
        CompletionSignatureValue;

    internal bool HasCanonicalHeader =>
        HeaderSize == StructureSize
        && HeaderVersion == StructureVersion
        && IsInitializedValue == 1
        && ExpectedSelfAddressValue != 0
        && ExpectedCodeBaseValue != 0
        && ExpectedCompositeEntryAddressValue != 0
        && ExpectedGateEntryAddressValue != 0
        && ExpectedReturnSiteAddressValue != 0
        && ExpectedOriginalRflagsValue != 0
        && ExpectedFirmwareCr3RawValue != 0
        && ExpectedFirmwareCr4RawValue != 0
        && ExpectedBoundaryCanaryValue != 0
        && ExpectedCompositeReturnValueValue
            == (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success
        && ExpectedInterruptEnabledValue <= 1
        && ExpectedPgeWasSetValue <= 1
        && InterruptPolicyValue
            == M01OwnedCr3CompositeCallGateInterruptPolicy
                .PreserveEntryState
        && PgePolicyValue
            == M01OwnedCr3CompositeCallGatePgePolicy
                .ClearDuringOwnedIntervalAndRestoreExact
        && BoundaryCanaryValue == ExpectedBoundaryCanaryValue
        && ReservedOutput0 == 0
        && ReservedOutput1 == 0
        && ReservedOutput2 == 0;

    internal bool OutputsAreZero =>
        CanaryBeforeValue == 0
        && PostReturnRflagsValue == 0
        && PostReturnCr3RawValue == 0
        && PostReturnCr4RawValue == 0
        && CanaryAfterValue == 0
        && CompositeReturnValueValue == 0
        && CallCountValue == 0
        && PostReturnObservationCountValue == 0
        && CompletionSignatureValue == 0
        && ReservedOutput0 == 0
        && ReservedOutput1 == 0
        && ReservedOutput2 == 0;

    public static M01OwnedCr3CompositeCallGateObservation
        CreateObserved(
            M01OwnedCr3CompositeCallGateObservation
                initializedObservation,
            M01OwnedCr3CompositeCallGateObservationLifecycle lifecycle,
            ulong boundaryCanary,
            ulong canaryBefore,
            ulong postReturnRflags,
            ulong postReturnCr3Raw,
            ulong postReturnCr4Raw,
            ulong canaryAfter,
            uint compositeReturnValue,
            uint callCount,
            uint postReturnObservationCount,
            uint completionSignature,
            ulong reservedOutput0 = 0,
            uint reservedOutput1 = 0,
            ulong reservedOutput2 = 0)
    {
        initializedObservation.LifecycleValue = lifecycle;
        initializedObservation.BoundaryCanaryValue =
            boundaryCanary;
        initializedObservation.CanaryBeforeValue = canaryBefore;
        initializedObservation.PostReturnRflagsValue =
            postReturnRflags;
        initializedObservation.PostReturnCr3RawValue =
            postReturnCr3Raw;
        initializedObservation.PostReturnCr4RawValue =
            postReturnCr4Raw;
        initializedObservation.CanaryAfterValue = canaryAfter;
        initializedObservation.CompositeReturnValueValue =
            compositeReturnValue;
        initializedObservation.CallCountValue = (byte)callCount;
        initializedObservation.PostReturnObservationCountValue =
            (byte)postReturnObservationCount;
        initializedObservation.CompletionSignatureValue =
            completionSignature;
        initializedObservation.ReservedOutput0 =
            (ushort)reservedOutput0;
        initializedObservation.ReservedOutput1 =
            callCount > 255U
                || postReturnObservationCount > 255U
                || reservedOutput0 > 65535UL
                ? 1U
                : reservedOutput1;
        initializedObservation.ReservedOutput2 = reservedOutput2;
        return initializedObservation;
    }
}

/// <summary>
/// Pointer-free result for call-gate preparation, combined-page
/// materialization/readback, or modeled post-return observation audit.
/// </summary>
public readonly struct M01OwnedCr3CompositeCallGateResult
{
    public const ulong StructureSize = 776;
    public const ulong NoIndex = ~0UL;

    private readonly M01OwnedCr3CompositeCallGateStatus _status;
    private readonly M01OwnedCr3CompositeCallGatePhase _phase;
    private readonly M01OwnedCr3CompositeTransitionTerminalKind
        _terminalKind;
    private readonly byte _reserved0;
    private readonly uint _reserved1;
    private readonly M01OwnedCr3CompositeCallGateObservation _observation;
    private readonly M01OwnedCr3CompositeTransitionRuntimeState
        _preparedState;
    private readonly ulong _codeBase;
    private readonly ulong _codeCapacity;
    private readonly ulong _compositeOffset;
    private readonly ulong _compositeLength;
    private readonly ulong _gateOffset;
    private readonly ulong _gateLength;
    private readonly ulong _gateEntryAddress;
    private readonly ulong _callInstructionOffset;
    private readonly ulong _returnSiteOffset;
    private readonly ulong _returnSiteAddress;
    private readonly ulong _fullPageByteCount;
    private readonly ulong _instructionCount;
    private readonly ulong _invalidIndex;
    private readonly ulong _expectedValue;
    private readonly ulong _observedValue;
    private readonly ulong _fnv1a64;

    internal M01OwnedCr3CompositeCallGateResult(
        M01OwnedCr3CompositeCallGateStatus status,
        M01OwnedCr3CompositeCallGatePhase phase,
        M01OwnedCr3CompositeTransitionTerminalKind terminalKind,
        M01OwnedCr3CompositeCallGateObservation observation,
        M01OwnedCr3CompositeTransitionRuntimeState preparedState,
        ulong codeBase,
        ulong codeCapacity,
        ulong compositeOffset,
        ulong compositeLength,
        ulong gateOffset,
        ulong gateLength,
        ulong gateEntryAddress,
        ulong callInstructionOffset,
        ulong returnSiteOffset,
        ulong returnSiteAddress,
        ulong fullPageByteCount,
        ulong instructionCount,
        ulong invalidIndex,
        ulong expectedValue,
        ulong observedValue,
        ulong fnv1a64)
    {
        _status = status;
        _phase = phase;
        _terminalKind = terminalKind;
        _reserved0 = 0;
        _reserved1 = 0;
        _observation = observation;
        _preparedState = preparedState;
        _codeBase = codeBase;
        _codeCapacity = codeCapacity;
        _compositeOffset = compositeOffset;
        _compositeLength = compositeLength;
        _gateOffset = gateOffset;
        _gateLength = gateLength;
        _gateEntryAddress = gateEntryAddress;
        _callInstructionOffset = callInstructionOffset;
        _returnSiteOffset = returnSiteOffset;
        _returnSiteAddress = returnSiteAddress;
        _fullPageByteCount = fullPageByteCount;
        _instructionCount = instructionCount;
        _invalidIndex = invalidIndex;
        _expectedValue = expectedValue;
        _observedValue = observedValue;
        _fnv1a64 = fnv1a64;
    }

    public M01OwnedCr3CompositeCallGateStatus Status => _status;
    public M01OwnedCr3CompositeCallGatePhase Phase => _phase;
    public M01OwnedCr3CompositeTransitionTerminalKind TerminalKind =>
        _terminalKind;
    public bool IsSuccess =>
        _status == M01OwnedCr3CompositeCallGateStatus.Success
        && _reserved0 == 0
        && _reserved1 == 0;
    public bool IsPrepared =>
        IsSuccess
        && _phase == M01OwnedCr3CompositeCallGatePhase.Prepared;
    public bool IsMaterialized =>
        IsSuccess
        && _phase == M01OwnedCr3CompositeCallGatePhase.Materialized;
    public bool IsReadbackVerified =>
        IsSuccess
        && _phase
            == M01OwnedCr3CompositeCallGatePhase.ReadbackVerified;
    public bool IsObservedAudit =>
        IsSuccess
        && _phase == M01OwnedCr3CompositeCallGatePhase.ObservedAudit;
    public M01OwnedCr3CompositeCallGateObservation Observation =>
        _observation;
    public M01OwnedCr3CompositeTransitionRuntimeState PreparedState =>
        _preparedState;
    public ulong CodeBase => _codeBase;
    public ulong CodeCapacity => _codeCapacity;
    public ulong CompositeOffset => _compositeOffset;
    public ulong CompositeLength => _compositeLength;
    public ulong GateOffset => _gateOffset;
    public ulong GateLength => _gateLength;
    public ulong GateEntryAddress => _gateEntryAddress;
    public ulong CallInstructionOffset => _callInstructionOffset;
    public ulong ReturnSiteOffset => _returnSiteOffset;
    public ulong ReturnSiteAddress => _returnSiteAddress;
    public ulong FullPageByteCount => _fullPageByteCount;
    public ulong InstructionCount => _instructionCount;
    public ulong InvalidIndex => _invalidIndex;
    public ulong ExpectedValue => _expectedValue;
    public ulong ObservedValue => _observedValue;
    public ulong Fnv1a64 => _fnv1a64;
    public bool UsesWindowsX64Abi => true;
    public bool HasExternalRelocations => false;
    public bool InvokesFirmwareCallbacks => false;
    public bool InvokesManagedCallbacks => false;
    public bool TouchesXmm6ThroughXmm15 => false;
    public bool ExecutesGeneratedCode => false;
    public bool PerformsLiveCr3Load => false;
    public bool PerformsLiveControlRegisterRead => false;
    public bool IsGuestEvidence => false;
}

/// <summary>
/// Emits and validates a single relocation-free owned-CR3 interval. Public
/// methods are host-only planners/auditors and never invoke emitted bytes.
/// </summary>
public static unsafe class M01OwnedCr3CompositeTransition
{
    public const ulong PageSize = 4096;
    public const ulong EntryAlignment = 16;
    public const ulong RequiredPointerAlignment = 8;
    public const ulong MaximumByteCapacity = PageSize;
    public const ulong MaterializedPageByteLength = PageSize;
    public const ulong RequiredByteLength = 3830;
    public const ulong AuditedInstructionCount = 732;
    public const ulong RequiredFnv1a64 = 0xf004_46f6_1be1_3cddUL;
    public const ulong AuditedSemanticRecordCount = 16;
    public const ulong CompositeEntryOffset = 0;
    public const ulong CallGateOffset = 0x0f00;
    public const ulong CallGateByteLength = 0x100;
    public const ulong MaximumCallGateByteLength = 0x100;
    public const ulong CallGateInstructionCount = 74;
    public const ulong CallInstructionOffset = 0x0fc4;
    public const ulong CallNextReturnSiteOffset = 0x0fc9;
    public const ulong CallGateObservationPointerBias = 127;
    public const ulong CallGateRequiredEntryRspModulo = 8;
    public const ulong CallGateRequiredLowerHeadroomBytes = 128;
    public const ulong CallGateRequiredUpperFrameBytes = 40;
    public const ulong GateToCompositeEntryRspDelta = 48;
    public const ulong CallGateGapByteLength =
        CallGateOffset - RequiredByteLength;
    public const ulong CallGateTailByteLength =
        PageSize - CallGateOffset - CallGateByteLength;
    public const ulong CombinedInstructionCount =
        AuditedInstructionCount + CallGateInstructionCount;
    public const ulong RequiredCallGateFnv1a64 =
        0xf903_c5de_16d3_a5b2UL;
    public const ulong RequiredCombinedFnv1a64 =
        0x42ca_f3b4_a6c2_119aUL;
    public const uint CallToCompositeRel32 = 0xffff_f037;
    public const uint CallGateRejectedReturnValue =
        (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.InvalidState;
    public const ulong ExternalPatchCount = 0;
    public const bool HasExternalRelocations = false;
    public const bool UsesWindowsX64Abi = true;
    public const bool DefinesSingleCompositeOwnedCr3Interval = true;
    public const bool ExecutesCompositeOwnedCr3Interval = false;
    public const bool IsGuestEvidence = false;
    public const bool CrossBindsOwnedSerialStub = false;
    public const bool CrossBindsOwnedMapIdentity = false;
    public const bool CrossBindsDiagnosticState = false;
    public const bool SerialPurposeIsCompatibilityAliasOnly = true;
    public const bool UsesExactLiveIntervalBindings = true;
    public const bool AcceptsHistoricalIdentityAliases = false;
    public const bool RequiresLoadedImageContinuationPage = false;
    public const bool InvokesFirmwareCallbacks = false;
    public const bool InvokesArbitraryCallbacks = false;
    public const bool CallsFirmware = false;
    public const bool CallsConsole = false;
    public const bool CallsSerialIo = false;
    public const bool CallsCom1 = false;
    public const bool AllocatesFirmwarePages = false;
    public const bool PerformsLiveCr3Load = false;
    public const bool PerformsLivePageProtection = false;
    public const bool PerformsLiveTlbInvalidation = false;
    public const bool PerformsLiveProbeExecution = false;
    public const bool DereferencesPageZero = false;
    public const bool CrossBindsAcpiProvenance = false;
    public const bool CrossBindsFramebufferProvenance = false;
    public const bool SupportsPcide = false;
    public const bool SupportsFiveLevelPaging = false;
    public const bool SupportsSupervisorCet = false;
    public const bool SupportsTrapFlagEntry = false;
    // These in-stream checks are defense in depth only: TF can raise #DB
    // after the first prologue instruction and supervisor IBT can reject the
    // non-ENDBR indirect target before any stream instruction executes.
    // M01-025 must establish both caller-side pre-transfer conditions.
    public const bool RejectsCr4CetBeforeCli = true;
    public const bool RejectsEntryTrapFlagBeforeCli = true;
    public const bool RequiresExternalSupervisorIbtDisabledPrecondition =
        true;
    public const bool SelfEstablishesCetSafety = false;
    public const bool RequiresExternalTrapFlagClearPrecondition = true;
    public const bool SelfEstablishesTrapFlagSafety = false;
    public const bool EliminatesNmiOrSmiRisk = false;
    public const bool ProvesAcpiOrFramebufferSourceProvenance = false;
    public const bool AcceptsSeparateLeavesAsCompositeEvidence = false;
    public const bool IsEfiExecutionEvidence = false;
    public const bool IsQemuEvidence = false;
    public const bool PreservesRbp = true;
    public const bool PreservesRdi = true;
    public const bool PreservesR12 = true;
    public const bool PreservesR13 = true;
    public const bool PreservesR14 = true;
    public const bool PreservesR15 = true;
    public const bool PreservesRsp = true;
    public const bool ExactProbeIsSoleInternalTransfer = true;
    public const bool RetainsCommittedProbePteOnRecoverableFailure = true;
    public const bool ProvesFinalRflagsHardwareReadback = false;
    public const bool LegacyRflagsReadbackAliasIsEvidence = false;
    public const bool FinalRflagsPopfqIsImmediatelyFollowedByRet = true;
    public const bool PreservesRbx = true;
    public const bool PreservesRsi = true;
    public const bool TouchesXmm6ThroughXmm15 = false;
    public const bool CallGateExecutesGeneratedCode = false;
    public const bool CallGatePerformsLiveControlRegisterRead = false;
    public const bool CallGateProvidesGuestEvidence = false;
    public const bool CallGateInvokesFirmwareCallbacks = false;
    public const bool CallGateInvokesManagedCallbacks = false;
    public const bool CallGateTouchesXmm6ThroughXmm15 = false;
    public const bool CallGateUsesOnlyVolatileRegistersBeforeGuardsPass =
        true;
    public const bool CallGateRejectPathWritesObservation = false;
    public const bool CallGateRejectPathInvokesComposite = false;
    public const bool CallGateBytePlanReadsCr3AndCr4AfterReturn = true;
    public const bool CallGateRequiresExternalPointerMappingTrust = true;
    public const bool CallGateSelfValidatesPointerMapping = false;
    public const bool CallGateRequiresExternalPageAlignedStateRoot =
        true;
    public const bool CallGateSelfValidatesStatePageAlignment = false;
    public const bool CallGateRequiresMappedStateAndObservation = true;
    public const bool CallGateRequiresExternalObservationIsArmedCheck =
        true;
    public const bool CallGateSelfChecksPackedArmedHeader = true;
    public const bool CallGateSelfChecksPristineCountsAndSignature =
        true;
    public const bool CallGateSelfChecksEveryObservationOutput = false;
    public const bool CallGateRequiresExternalTrapFlagClear = true;
    public const bool CallGateRequiresExternalSupervisorCetDisabled =
        true;
    public const bool
        CallGateRestoresPreparedRflagsImmediatelyBeforeCompositeCall =
            true;
    public const bool CallGateSelfValidatesLiveEntryRflags = false;
    public const bool
        CallGateRequiresExternalEntryRflagsMatchPreparedSnapshot =
            true;
    public const ulong CallGateExactCompositeCallCount = 1;
    public const ulong CallGateExactPostReturnObservationCount = 1;
    public const ulong ExactInvlpgCount = 1;
    public const ulong ExactProbeInvocationCount = 1;
    public const ulong RequiredCallerShadowSpaceBytes = 32;
    public const ulong RequiredStackHeadroomBytes = 64;
    public const ulong Cr3PermittedLowBitsMask = 0x18;
    public const ulong Cr3ArchitecturalBaseMask =
        0x000f_ffff_ffff_f000UL;
    public const ulong Cr4PageGlobalEnableBit = 1UL << 7;
    public const ulong Cr4FiveLevelPagingBit = 1UL << 12;
    public const ulong Cr4PcideBit = 1UL << 17;
    public const ulong Cr4ControlFlowEnforcementBit = 1UL << 23;
    public const ulong RflagsTrapFlagBit = 1UL << 8;
    public const ulong RflagsInterruptEnableBit = 1UL << 9;
    public const ulong PagePresentBit = 1UL;
    public const ulong PageWritableBit = 1UL << 1;
    public const ulong PageAccessedBit = 1UL << 5;
    public const ulong PageDirtyBit = 1UL << 6;
    public const ulong PageLargeBit = 1UL << 7;
    public const ulong PageNoExecuteBit = 1UL << 63;
    public const uint ExactProbeReturnValue = 0x4f53_4f4b;
    public const uint NoObservedRuntimeReturnValue = ~0U;
    public const ulong ExactProbeByteLength = 6;
    public const ulong RequiredRangeBindingCount = 7;
    public const ulong RequiredLiveIntervalRangeBindingCount = 6;
    public const ulong RequiredLiveIntervalRoleMask = 0x3f00;
    public const ulong MaximumAllocationRecordCount = 64;
    public const ulong MaximumReservationRecordCount = 64;
    public const ulong MaximumIdentityMappingCount =
        OS.Kernel.Boot.X64BootIdentityMappingPlan
            .MaximumDestinationCount;
    public const ulong MaximumPageImageCount = 128;
    public const ulong MaximumPageEntryCount =
        MaximumPageImageCount * 512;
    public const ulong MaximumCriticalMappingPageCount = 256;
    public const ulong CriticalMappingPageCountMask = 0x1ff;
    public const byte ProbePdptPageOrdinalShift = 9;
    public const byte ProbePdPageOrdinalShift = 16;
    public const byte ProbePtPageOrdinalShift = 23;
    public const ulong ProbeHierarchyPageOrdinalMask = 0x7f;
    public const ulong ProbeHierarchyIdentityPermittedMask =
        CriticalMappingPageCountMask
        | (ProbeHierarchyPageOrdinalMask
            << ProbePdptPageOrdinalShift)
        | (ProbeHierarchyPageOrdinalMask
            << ProbePdPageOrdinalShift)
        | (ProbeHierarchyPageOrdinalMask
            << ProbePtPageOrdinalShift);
    public const ulong LowCanonicalExclusiveCeiling =
        0x0000_8000_0000_0000UL;

    private const long MinimumRel32Displacement = -2_147_483_648L;
    private const long MaximumRel32Displacement = 2_147_483_647L;
    private const ulong MaximumUInt32Value = 0xffff_ffffUL;

    public const uint StateStructureSizeOffset = 0;
    public const uint StateStructureVersionOffset = 4;
    public const uint StateIsInitializedOffset = 6;
    public const uint StateReserved0Offset = 7;
    public const uint StateOwnedCr3RawOffset = 8;
    public const uint StateTablePageArenaStartOffset = 16;
    public const uint StateTablePageArenaEndOffset = 24;
    public const uint StateTableStateArenaStartOffset = 32;
    public const uint StateTableStateArenaEndOffset = 40;
    public const uint StateCompositeCodeStartOffset = 48;
    public const uint StateCompositeCodeEndOffset = 56;
    public const uint StateContinuationPageStartOffset = 64;
    public const uint StateContinuationPageEndOffset = 72;
    public const uint StateExpectedReturnAddressOffset = 80;
    public const uint StateStackStartOffset = 88;
    public const uint StateStackEndOffset = 96;
    public const uint StateProbePageStartOffset = 104;
    public const uint StateProbePageEndOffset = 112;
    public const uint StateProbeEntryOffset = 120;
    public const uint StateExpectedProbePteAddressOffset = 128;
    public const uint StateInitialProbePteRawOffset = 136;
    public const uint StateFinalProbePteRawOffset = 144;
    public const uint StateCanaryAddressOffset = 152;
    public const uint StateExpectedCanaryOffset = 160;
    public const uint StateReplacementCanaryOffset = 168;
    public const uint StateExpectedProbeReturnOffset = 176;
    public const uint StateFirmwareCallbackCountOffset = 180;
    public const uint StateExpectedSelfAddressOffset = 184;
    public const uint StateExpectedProbeHierarchyIdentityOffset = 192;
    public const uint StateExpectedCriticalMappingPageCountOffset =
        StateExpectedProbeHierarchyIdentityOffset;
    public const uint StateReservedInput1Offset =
        StateExpectedCriticalMappingPageCountOffset;
    public const uint StateOriginalRflagsOffset = 200;
    public const uint StateOriginalCr0Offset = 208;
    public const uint StateOriginalCr3RawOffset = 216;
    public const uint StateOriginalCr4RawOffset = 224;
    public const uint StateOriginalEferOffset = 232;
    public const uint StatePhysicalAddressMaskOffset = 240;
    public const uint StateEntryRspOffset = 248;
    public const uint StateObservedReturnAddressOffset = 256;
    public const uint StateObservedRipOffset = 264;
    public const uint StateCliRflagsReadbackOffset = 272;
    public const uint StateClearedCr4ReadbackOffset = 280;
    public const uint StateOwnedCr3ReadbackOffset = 288;
    public const uint StateCanaryBeforeOffset = 296;
    public const uint StateCanaryAfterOffset = 304;
    public const uint StateProbePml4eOffset = 312;
    public const uint StateProbePdpteOffset = 320;
    public const uint StateProbePdeOffset = 328;
    public const uint StateProbePteBeforeOffset = 336;
    public const uint StateProbePteAfterOffset = 344;
    public const uint StateInvlpgTargetOffset = 352;
    public const uint StateProbeReturnOffset = 360;
    public const uint StatePageZeroPml4eOffset = 368;
    public const uint StatePageZeroPdpteOffset = 376;
    public const uint StatePageZeroPdeOffset = 384;
    public const uint StatePageZeroPteOffset = 392;
    public const uint StateCommittedProbePteReadbackOffset = 400;
    public const uint StateFirmwareCr3ReadbackOffset = 408;
    public const uint StatePostRestoreCanaryOffset = 416;
    public const uint StateRestoredCr4Offset = 424;
    public const uint StateRestoredRflagsIntentOffset = 432;
    public const uint StateExitRspOffset = 440;
    public const uint StateInvlpgCountOffset = 448;
    public const uint StateProbeInvocationCountOffset = 456;
    public const uint StateRuntimeStatusOffset = 464;
    public const uint StateCheckpointOffset = 465;
    public const uint StatePageZeroLevelOffset = 466;
    public const uint StateMutationCommittedOffset = 467;
    public const uint StateOriginalPgeWasSetOffset = 468;
    public const uint StatePgeWasClearedOffset =
        StateOriginalPgeWasSetOffset;
    public const uint StateOwnedRootWasLoadedOffset = 469;
    public const uint StateProbeReturnedOffset = 470;
    public const uint StateProgressCheckpointOffset = 471;
    public const uint StateReservedOutput0Offset =
        StateProgressCheckpointOffset;
    public const uint StateReservedOutput1Offset = 472;

    public const uint CallGateObservationStructureSizeOffset = 0;
    public const uint CallGateObservationStructureVersionOffset = 4;
    public const uint CallGateObservationInitializedOffset = 6;
    public const uint CallGateObservationLifecycleOffset = 7;
    public const uint CallGateObservationExpectedSelfAddressOffset = 8;
    public const uint CallGateObservationExpectedCodeBaseOffset = 16;
    public const uint
        CallGateObservationExpectedCompositeEntryAddressOffset = 24;
    public const uint CallGateObservationExpectedGateEntryAddressOffset =
        32;
    public const uint CallGateObservationExpectedReturnSiteAddressOffset =
        40;
    public const uint CallGateObservationExpectedOriginalRflagsOffset = 48;
    public const uint CallGateObservationExpectedFirmwareCr3Offset = 56;
    public const uint CallGateObservationExpectedFirmwareCr4Offset = 64;
    public const uint CallGateObservationExpectedBoundaryCanaryOffset =
        72;
    public const uint CallGateObservationExpectedCompositeReturnOffset =
        80;
    public const uint CallGateObservationExpectedInterruptEnabledOffset =
        84;
    public const uint CallGateObservationExpectedPgeWasSetOffset = 85;
    public const uint CallGateObservationInterruptPolicyOffset = 86;
    public const uint CallGateObservationPgePolicyOffset = 87;
    public const uint CallGateObservationBoundaryCanaryOffset = 88;
    public const uint CallGateObservationCanaryBeforeOffset = 96;
    public const uint CallGateObservationPostReturnRflagsOffset = 104;
    public const uint CallGateObservationPostReturnCr3Offset = 112;
    public const uint CallGateObservationPostReturnCr4Offset = 120;
    public const uint CallGateObservationCanaryAfterOffset = 128;
    public const uint CallGateObservationCompositeReturnOffset = 136;
    public const uint CallGateObservationCallCountOffset = 140;
    public const uint CallGateObservationPostReturnCountOffset = 141;
    public const uint CallGateObservationReservedOutput0Offset = 142;
    public const uint CallGateObservationCompletionSignatureOffset = 144;
    public const uint CallGateObservationReservedOutput1Offset = 148;
    public const uint CallGateObservationReservedOutput2Offset = 152;
    public const uint CallGateObservationPristineQwordOffset =
        CallGateObservationCallCountOffset;

    public static M01OwnedCr3CompositeTransitionResult
        CreateRuntimeState(
            M01LivePagingPreparationResult* priorPlanningEvidence,
            ulong priorPlanningEvidenceCount,
            ulong priorPlanningEvidenceCapacity,
            BootMemoryRegionRecord* allocationRecords,
            ulong allocationRecordCount,
            ulong allocationRecordCapacity,
            OS.Kernel.Memory.BootReservedRange* reservationRecords,
            ulong reservationRecordCount,
            ulong reservationRecordCapacity,
            OS.Kernel.Boot.X64BootIdentityMappingRange*
                identityMappings,
            ulong identityMappingCount,
            ulong identityMappingCapacity,
            OS.Kernel.Memory.X64PageTableImagePage* pageImages,
            ulong pageImageCount,
            ulong pageImageCapacity,
            ulong* pageEntries,
            ulong pageEntryCount,
            ulong pageEntryCapacity,
            M01OwnedCr3CompositeTransitionRangeBinding* rangeBindings,
            ulong rangeBindingCount,
            ulong rangeBindingCapacity,
            M01OwnedCr3CompositeTransitionRuntimeState*
                stateDestination,
            ulong stateDestinationCount,
            ulong stateDestinationCapacity,
            ulong* canaryDestination,
            ulong canaryDestinationCount,
            ulong canaryDestinationCapacity,
            VirtualAddress runtimeStateAddress,
            ulong ownedCr3Raw,
            VirtualAddress expectedReturnAddress,
            VirtualAddress canaryAddress,
            ulong expectedCanary,
            ulong replacementCanary)
    {
        M01OwnedCr3CompositeTransitionStatus tableStatus =
            ValidateInputTable(
                priorPlanningEvidence,
                priorPlanningEvidenceCount,
                priorPlanningEvidenceCapacity,
                1,
                (ulong)sizeof(M01LivePagingPreparationResult),
                M01OwnedCr3CompositeTransitionStatus
                    .MissingPriorPlanningEvidence,
                M01OwnedCr3CompositeTransitionStatus
                    .InvalidPriorPlanningEvidence,
                M01OwnedCr3CompositeTransitionStatus
                    .InvalidPriorPlanningEvidence,
                out ulong priorEvidenceStart,
                out ulong priorEvidenceEnd);
        if (tableStatus
                != M01OwnedCr3CompositeTransitionStatus.Success
            || priorPlanningEvidenceCount != 1
            || priorPlanningEvidenceCapacity != 1
            || !priorPlanningEvidence[0].Succeeded
            || !priorPlanningEvidence[0]
                .PreparedForCompositeTransition
            || !priorPlanningEvidence[0].IsPreparationOnly
            || priorPlanningEvidence[0].IsModelOnly
            || priorPlanningEvidence[0].IsExecutionEvidence
            || priorPlanningEvidence[0].ExecutionMode
                != M01LivePagingExecutionMode.PreparationOnly
            || !priorPlanningEvidence[0].State.IsPrepared)
        {
            return Fail(
                tableStatus
                    == M01OwnedCr3CompositeTransitionStatus.Success
                    ? M01OwnedCr3CompositeTransitionStatus
                        .InvalidPriorPlanningEvidence
                    : tableStatus);
        }

        tableStatus =
            ValidateInputTable(
                allocationRecords,
                allocationRecordCount,
                allocationRecordCapacity,
                MaximumAllocationRecordCount,
                (ulong)sizeof(BootMemoryRegionRecord),
                M01OwnedCr3CompositeTransitionStatus
                    .MissingAllocationRecords,
                M01OwnedCr3CompositeTransitionStatus
                    .InvalidAllocationRecordCount,
                M01OwnedCr3CompositeTransitionStatus
                    .AllocationRecordCapacityTooSmall,
                out ulong allocationsStart,
                out ulong allocationsEnd);
        if (tableStatus
            != M01OwnedCr3CompositeTransitionStatus.Success)
        {
            return Fail(
                tableStatus,
                invalidIndex: M01OwnedCr3CompositeTransitionResult.NoIndex,
                expectedValue: allocationRecordCount,
                observedValue: allocationRecordCapacity);
        }

        tableStatus = ValidateInputTable(
            reservationRecords,
            reservationRecordCount,
            reservationRecordCapacity,
            MaximumReservationRecordCount,
            (ulong)sizeof(OS.Kernel.Memory.BootReservedRange),
            M01OwnedCr3CompositeTransitionStatus
                .MissingReservationRecords,
            M01OwnedCr3CompositeTransitionStatus
                .InvalidReservationRecordCount,
            M01OwnedCr3CompositeTransitionStatus
                .ReservationRecordCapacityTooSmall,
            out ulong reservationsStart,
            out ulong reservationsEnd);
        if (tableStatus
            != M01OwnedCr3CompositeTransitionStatus.Success)
        {
            return Fail(
                tableStatus,
                invalidIndex: M01OwnedCr3CompositeTransitionResult.NoIndex,
                expectedValue: reservationRecordCount,
                observedValue: reservationRecordCapacity);
        }

        tableStatus = ValidateInputTable(
            identityMappings,
            identityMappingCount,
            identityMappingCapacity,
            MaximumIdentityMappingCount,
            (ulong)sizeof(
                OS.Kernel.Boot.X64BootIdentityMappingRange),
            M01OwnedCr3CompositeTransitionStatus
                .MissingIdentityMappings,
            M01OwnedCr3CompositeTransitionStatus
                .InvalidIdentityMappingCount,
            M01OwnedCr3CompositeTransitionStatus
                .IdentityMappingCapacityTooSmall,
            out ulong mappingsStart,
            out ulong mappingsEnd);
        if (tableStatus
            != M01OwnedCr3CompositeTransitionStatus.Success)
        {
            return Fail(
                tableStatus,
                invalidIndex: M01OwnedCr3CompositeTransitionResult.NoIndex,
                expectedValue: identityMappingCount,
                observedValue: identityMappingCapacity);
        }

        tableStatus = ValidateInputTable(
            pageImages,
            pageImageCount,
            pageImageCapacity,
            MaximumPageImageCount,
            (ulong)sizeof(OS.Kernel.Memory.X64PageTableImagePage),
            M01OwnedCr3CompositeTransitionStatus.MissingPageImages,
            M01OwnedCr3CompositeTransitionStatus.InvalidPageImageCount,
            M01OwnedCr3CompositeTransitionStatus
                .PageImageCapacityTooSmall,
            out ulong pageImagesStart,
            out ulong pageImagesEnd);
        if (tableStatus
            != M01OwnedCr3CompositeTransitionStatus.Success)
        {
            return Fail(
                tableStatus,
                expectedValue: pageImageCount,
                observedValue: pageImageCapacity);
        }

        if (pageImageCount > ~0UL / 512
            || pageEntryCount != pageImageCount * 512)
        {
            return Fail(
                M01OwnedCr3CompositeTransitionStatus
                    .InvalidPageEntryCount,
                expectedValue: pageImageCount * 512,
                observedValue: pageEntryCount);
        }

        tableStatus = ValidateInputTable(
            pageEntries,
            pageEntryCount,
            pageEntryCapacity,
            MaximumPageEntryCount,
            sizeof(ulong),
            M01OwnedCr3CompositeTransitionStatus.MissingPageEntries,
            M01OwnedCr3CompositeTransitionStatus
                .InvalidPageEntryCount,
            M01OwnedCr3CompositeTransitionStatus
                .PageEntryCapacityTooSmall,
            out ulong pageEntriesStart,
            out ulong pageEntriesEnd);
        if (tableStatus
            != M01OwnedCr3CompositeTransitionStatus.Success)
        {
            return Fail(
                tableStatus,
                expectedValue: pageEntryCount,
                observedValue: pageEntryCapacity);
        }

        if (rangeBindingCount
            != RequiredLiveIntervalRangeBindingCount)
        {
            return Fail(
                M01OwnedCr3CompositeTransitionStatus
                    .InvalidRangeBindingCount,
                invalidIndex: M01OwnedCr3CompositeTransitionResult.NoIndex,
                expectedValue:
                    RequiredLiveIntervalRangeBindingCount,
                observedValue: rangeBindingCount);
        }

        tableStatus = ValidateInputTable(
            rangeBindings,
            rangeBindingCount,
            rangeBindingCapacity,
            RequiredLiveIntervalRangeBindingCount,
            (ulong)sizeof(
                M01OwnedCr3CompositeTransitionRangeBinding),
            M01OwnedCr3CompositeTransitionStatus
                .MissingRangeBindings,
            M01OwnedCr3CompositeTransitionStatus
                .InvalidRangeBindingCount,
            M01OwnedCr3CompositeTransitionStatus
                .RangeBindingCapacityTooSmall,
            out ulong bindingsStart,
            out ulong bindingsEnd);
        if (tableStatus
            != M01OwnedCr3CompositeTransitionStatus.Success)
        {
            return Fail(
                tableStatus,
                invalidIndex: M01OwnedCr3CompositeTransitionResult.NoIndex,
                expectedValue: rangeBindingCount,
                observedValue: rangeBindingCapacity);
        }

        if (stateDestinationCount != 1)
        {
            return Fail(
                M01OwnedCr3CompositeTransitionStatus
                    .InvalidStateDestinationCount,
                expectedValue: 1,
                observedValue: stateDestinationCount);
        }

        tableStatus = ValidateInputTable(
            stateDestination,
            stateDestinationCount,
            stateDestinationCapacity,
            1,
            (ulong)sizeof(
                M01OwnedCr3CompositeTransitionRuntimeState),
            M01OwnedCr3CompositeTransitionStatus
                .MissingStateDestination,
            M01OwnedCr3CompositeTransitionStatus
                .InvalidStateDestinationCount,
            M01OwnedCr3CompositeTransitionStatus
                .StateDestinationCapacityTooSmall,
            out ulong stateDestinationStart,
            out ulong stateDestinationEnd);
        if (tableStatus
            != M01OwnedCr3CompositeTransitionStatus.Success)
        {
            return Fail(
                tableStatus
                    == M01OwnedCr3CompositeTransitionStatus
                        .MisalignedInputTable
                    ? M01OwnedCr3CompositeTransitionStatus
                        .MisalignedStateDestination
                    : tableStatus
                        == M01OwnedCr3CompositeTransitionStatus
                            .InputTableSpanOverflow
                        ? M01OwnedCr3CompositeTransitionStatus
                            .StateDestinationSpanOverflow
                        : tableStatus);
        }

        if (canaryDestinationCount != 1)
        {
            return Fail(
                M01OwnedCr3CompositeTransitionStatus
                    .InvalidCanaryDestinationCount,
                expectedValue: 1,
                observedValue: canaryDestinationCount);
        }

        tableStatus = ValidateInputTable(
            canaryDestination,
            canaryDestinationCount,
            canaryDestinationCapacity,
            1,
            sizeof(ulong),
            M01OwnedCr3CompositeTransitionStatus
                .MissingCanaryDestination,
            M01OwnedCr3CompositeTransitionStatus
                .InvalidCanaryDestinationCount,
            M01OwnedCr3CompositeTransitionStatus
                .CanaryDestinationCapacityTooSmall,
            out ulong canaryDestinationStart,
            out ulong canaryDestinationEnd);
        if (tableStatus
            != M01OwnedCr3CompositeTransitionStatus.Success)
        {
            return Fail(
                tableStatus
                    == M01OwnedCr3CompositeTransitionStatus
                        .MisalignedInputTable
                    ? M01OwnedCr3CompositeTransitionStatus
                        .MisalignedCanaryDestination
                    : tableStatus
                        == M01OwnedCr3CompositeTransitionStatus
                            .InputTableSpanOverflow
                        ? M01OwnedCr3CompositeTransitionStatus
                            .CanaryDestinationSpanOverflow
                        : tableStatus);
        }

        if (!TryGetRawVirtual(
                runtimeStateAddress,
                out ulong runtimeStateRaw)
            || !TryGetRawVirtual(
                expectedReturnAddress,
                out ulong expectedReturnRaw)
            || !TryGetRawVirtual(
                canaryAddress,
                out ulong canaryRaw)
            || (runtimeStateRaw
                & (RequiredPointerAlignment - 1)) != 0
            || runtimeStateRaw >= LowCanonicalExclusiveCeiling
            || runtimeStateRaw
                > LowCanonicalExclusiveCeiling
                    - M01OwnedCr3CompositeTransitionRuntimeState
                        .StructureSize
            || stateDestinationStart != runtimeStateRaw
            || stateDestinationEnd
                != runtimeStateRaw
                    + M01OwnedCr3CompositeTransitionRuntimeState
                        .StructureSize
            || canaryDestinationStart != canaryRaw
            || canaryDestinationEnd != canaryRaw + sizeof(ulong))
        {
            return Fail(
                M01OwnedCr3CompositeTransitionStatus
                    .StateAddressMismatch,
                observedValue: runtimeStateRaw);
        }

        if (!EightSpansArePairwiseDisjoint(
                priorEvidenceStart,
                priorEvidenceEnd,
                allocationsStart,
                allocationsEnd,
                reservationsStart,
                reservationsEnd,
                mappingsStart,
                mappingsEnd,
                pageImagesStart,
                pageImagesEnd,
                pageEntriesStart,
                pageEntriesEnd,
                bindingsStart,
                bindingsEnd,
                stateDestinationStart,
                stateDestinationEnd)
            || RangesOverlap(
                canaryDestinationStart,
                canaryDestinationEnd,
                priorEvidenceStart,
                priorEvidenceEnd)
            || RangesOverlap(
                canaryDestinationStart,
                canaryDestinationEnd,
                allocationsStart,
                allocationsEnd)
            || RangesOverlap(
                canaryDestinationStart,
                canaryDestinationEnd,
                reservationsStart,
                reservationsEnd)
            || RangesOverlap(
                canaryDestinationStart,
                canaryDestinationEnd,
                mappingsStart,
                mappingsEnd)
            || RangesOverlap(
                canaryDestinationStart,
                canaryDestinationEnd,
                pageImagesStart,
                pageImagesEnd)
            || RangesOverlap(
                canaryDestinationStart,
                canaryDestinationEnd,
                pageEntriesStart,
                pageEntriesEnd)
            || RangesOverlap(
                canaryDestinationStart,
                canaryDestinationEnd,
                bindingsStart,
                bindingsEnd)
            || RangesOverlap(
                canaryDestinationStart,
                canaryDestinationEnd,
                stateDestinationStart,
                stateDestinationEnd))
        {
            return Fail(
                M01OwnedCr3CompositeTransitionStatus.InputOutputAlias);
        }

        ulong seenRoleMask = 0;
        M01OwnedCr3CompositeTransitionRangeBinding codeAndCallGate =
            default;
        M01OwnedCr3CompositeTransitionRangeBinding ownedStack =
            default;
        M01OwnedCr3CompositeTransitionRangeBinding probe = default;
        M01OwnedCr3CompositeTransitionRangeBinding tableArena =
            default;
        M01OwnedCr3CompositeTransitionRangeBinding stateAndCanary =
            default;
        M01OwnedCr3CompositeTransitionRangeBinding ownedRoot =
            default;
        M01LivePagingPreparationResult prior =
            priorPlanningEvidence[0];

        for (ulong index = 0; index < rangeBindingCount; index++)
        {
            M01OwnedCr3CompositeTransitionRangeBinding binding =
                rangeBindings[index];
            M01OwnedCr3CompositeTransitionStatus bindingStatus =
                ValidateRangeBinding(
                    binding,
                    allocationRecords,
                    allocationRecordCount,
                    reservationRecords,
                    reservationRecordCount,
                    identityMappings,
                    identityMappingCount,
                    prior.Evidence.Cr3CodeReservationIndex,
                    prior.Evidence.ProtectionProbeReservationIndex,
                    prior.Evidence.TableStateReservationIndex);
            if (bindingStatus
                != M01OwnedCr3CompositeTransitionStatus.Success)
            {
                return Fail(
                    bindingStatus,
                    invalidIndex: index);
            }

            ulong roleBit = 1UL << (byte)binding.Role;
            if ((seenRoleMask & roleBit) != 0)
            {
                return Fail(
                    M01OwnedCr3CompositeTransitionStatus
                        .DuplicateRangeRole,
                    invalidIndex: index,
                    observedValue: (ulong)binding.Role);
            }

            seenRoleMask |= roleBit;
            switch (binding.Role)
            {
                case M01OwnedCr3CompositeTransitionRangeRole
                    .CompositeCodeAndCallGate:
                    codeAndCallGate = binding;
                    break;
                case M01OwnedCr3CompositeTransitionRangeRole
                    .CompositeOwnedStack:
                    ownedStack = binding;
                    break;
                case M01OwnedCr3CompositeTransitionRangeRole
                    .ProtectionProbePage:
                    probe = binding;
                    break;
                case M01OwnedCr3CompositeTransitionRangeRole
                    .CompleteTableAndStateArena:
                    tableArena = binding;
                    break;
                case M01OwnedCr3CompositeTransitionRangeRole
                    .RuntimeStateAndCanaryPage:
                    stateAndCanary = binding;
                    break;
                case M01OwnedCr3CompositeTransitionRangeRole
                    .OwnedRootPage:
                    ownedRoot = binding;
                    break;
                default:
                    return Fail(
                        M01OwnedCr3CompositeTransitionStatus
                            .InvalidRangeBinding,
                        invalidIndex: index);
            }
        }

        if (seenRoleMask != RequiredLiveIntervalRoleMask)
        {
            return Fail(
                M01OwnedCr3CompositeTransitionStatus.MissingRangeRole,
                expectedValue: RequiredLiveIntervalRoleMask,
                observedValue: seenRoleMask);
        }

        if (!TryGetRawRange(
                codeAndCallGate,
                out ulong codeStart,
                out ulong codeEnd)
            || !TryGetRawRange(
                ownedStack,
                out ulong stackStart,
                out ulong stackEnd)
            || !TryGetRawRange(
                probe,
                out ulong probeStart,
                out ulong probeEnd)
            || !TryGetRawRange(
                tableArena,
                out ulong tableStart,
                out ulong tableEnd)
            || !TryGetRawRange(
                stateAndCanary,
                out ulong stateStart,
                out ulong stateEnd)
            || !TryGetRawRange(
                ownedRoot,
                out ulong rootStart,
                out ulong rootEnd))
        {
            return Fail(
                M01OwnedCr3CompositeTransitionStatus
                    .InvalidRangeBinding);
        }

        if (pageImageCount > (~0UL / PageSize)
            || tableStart > ~0UL - pageImageCount * PageSize)
        {
            return Fail(
                M01OwnedCr3CompositeTransitionStatus.InvalidPageImage,
                observedValue: pageImageCount);
        }

        ulong tablePageEnd =
            tableStart + pageImageCount * PageSize;
        if (!IsPageRange(codeStart, codeEnd, exactlyOnePage: true)
            || !IsPageRange(
                probeStart,
                probeEnd,
                exactlyOnePage: true)
            || !IsPageRange(
                rootStart,
                rootEnd,
                exactlyOnePage: true)
            || !IsPageRange(
                tableStart,
                tableEnd,
                exactlyOnePage: false)
            || !IsPageRange(
                stateStart,
                stateEnd,
                exactlyOnePage: true)
            || !IsPageRange(
                stackStart,
                stackEnd,
                exactlyOnePage: false)
            || !ContainsRange(tableStart, tableEnd, rootStart, rootEnd)
            || !ContainsRange(tableStart, tableEnd, stateStart, stateEnd)
            || tablePageEnd != stateStart
            || stateEnd != tableEnd
            || runtimeStateRaw != stateStart
            || canaryRaw
                != runtimeStateRaw
                    + M01OwnedCr3CompositeTransitionRuntimeState
                        .StructureSize
            || stateEnd - stateStart
                < M01OwnedCr3CompositeTransitionRuntimeState
                    .StructureSize + sizeof(ulong))
        {
            return Fail(
                M01OwnedCr3CompositeTransitionStatus
                    .InvalidRangeBinding);
        }

        if (RangesOverlap(codeStart, codeEnd, probeStart, probeEnd)
            || RangesOverlap(codeStart, codeEnd, stackStart, stackEnd)
            || RangesOverlap(codeStart, codeEnd, tableStart, tableEnd)
            || RangesOverlap(probeStart, probeEnd, stackStart, stackEnd)
            || RangesOverlap(probeStart, probeEnd, tableStart, tableEnd)
            || RangesOverlap(stackStart, stackEnd, tableStart, tableEnd))
        {
            return Fail(
                M01OwnedCr3CompositeTransitionStatus.InputOutputAlias);
        }

        ulong ownedRootBase =
            ownedCr3Raw & Cr3ArchitecturalBaseMask;
        if (ownedRootBase != rootStart
            || (ownedCr3Raw
                & ~(Cr3ArchitecturalBaseMask
                    | Cr3PermittedLowBitsMask)) != 0)
        {
            return Fail(
                M01OwnedCr3CompositeTransitionStatus.InvalidOwnedCr3,
                expectedValue: rootStart,
                observedValue: ownedCr3Raw);
        }

        if (!ContainsAddress(
                codeStart,
                codeEnd,
                expectedReturnRaw))
        {
            return Fail(
                M01OwnedCr3CompositeTransitionStatus
                    .InvalidProbeContract,
                expectedValue: codeStart,
                observedValue: expectedReturnRaw);
        }
        if (expectedReturnRaw < codeStart + RequiredByteLength)
        {
            return Fail(
                M01OwnedCr3CompositeTransitionStatus
                    .InvalidProbeContract,
                expectedValue: codeStart + RequiredByteLength,
                observedValue: expectedReturnRaw);
        }

        M01LivePagingAllocationEvidence priorAllocation =
            prior.Evidence.AllocationEvidence;
        M01LivePagingMapLedgerEvidence priorLedger =
            prior.Evidence.MapLedgerEvidence;
        M01LivePagingTableBuildResult priorTableBuild =
            prior.Evidence.TableBuild;
        ulong priorFullPhysicalAddressMask =
            prior.Evidence.Preflight.PhysicalAddressMask;
        byte priorPhysicalAddressWidth =
            prior.Evidence.Preflight.PhysicalAddressWidth;
        ulong expectedFullPhysicalAddressMask =
            (1UL << priorPhysicalAddressWidth) - 1;
        ulong priorPhysicalPageMask =
            priorFullPhysicalAddressMask & ~0xfffUL;
        if (priorAllocation.AllocationTableAddress
                != (ulong)allocationRecords
            || priorAllocation.AllocationRecordCount
                != allocationRecordCount
            || priorAllocation.AllocationRecordCapacity
                != allocationRecordCapacity
            || priorLedger.LedgerAddress != (ulong)reservationRecords
            || priorLedger.LedgerCount != reservationRecordCount
            || priorLedger.LedgerCapacity != reservationRecordCapacity
            || prior.State.AllocationTableAddress
                != (ulong)allocationRecords
            || prior.State.AllocationRecordCount
                != allocationRecordCount
            || prior.State.AllocationRecordCapacity
                != allocationRecordCapacity
            || prior.State.ReservationLedgerAddress
                != (ulong)reservationRecords
            || prior.State.ReservationCount
                != reservationRecordCount
            || prior.State.ReservationCapacity
                != reservationRecordCapacity
            || prior.State.MappingRequirementCount
                != M01LivePagingIntegration
                    .RequiredLiveIntervalMappingRequirementCount
            || prior.State.MappingRequirementCapacity
                < prior.State.MappingRequirementCount
            || prior.State.MappingRangeAddress
                != (ulong)identityMappings
            || prior.State.MappingRangeCount
                != identityMappingCount
            || prior.State.MappingRangeCapacity
                != identityMappingCapacity
            || prior.State.PageImageAddress != (ulong)pageImages
            || prior.State.PageImageCount != pageImageCount
            || prior.State.PageImageCapacity != pageImageCapacity
            || prior.State.PageEntryAddress != (ulong)pageEntries
            || prior.State.PageEntryCount != pageEntryCount
            || prior.State.PageEntryCapacity != pageEntryCapacity
            || prior.State.Cr3CodePageAddress != codeStart
            || prior.State.Cr3CodePageCapacity != PageSize
            || prior.State.ProtectionProbePageAddress
                != probeStart
            || prior.State.ProtectionProbePageCapacity != PageSize
            || !prior.State.PageZeroAbsent
            || !prior.State.StubBytesVerified
            || !prior.State.InitialProbeLeafRestored
            || prior.Evidence.MappingPlan.OutputRangeCount
                != identityMappingCount
            || prior.Evidence.MappingPlan.DestinationCapacity
                != identityMappingCapacity
            || prior.Evidence.MappingPlan.RequiredPurposeMask
                != OS.Kernel.Boot.X64BootIdentityMappingPlan
                    .RequiredLiveIntervalPurposeMask
            || prior.Evidence.MappingPlan.GuardSourceIndex
                == OS.Kernel.Boot
                    .X64BootIdentityMappingPlanResult.NoIndex
            || prior.Evidence.MappingPlan.MissingPurposeMask != 0
            || !prior.Evidence.Preflight.Succeeded
            || priorPhysicalAddressWidth < 36
            || priorPhysicalAddressWidth > 52
            || priorFullPhysicalAddressMask
                != expectedFullPhysicalAddressMask
             || priorPhysicalPageMask == 0
             || (priorPhysicalPageMask
                 & ~Cr3ArchitecturalBaseMask) != 0
             || (prior.Evidence.Preflight.RawFirmwareCr3
                    & priorPhysicalPageMask)
                == (ownedCr3Raw & priorPhysicalPageMask)
             || !priorTableBuild.Succeeded
            || priorTableBuild.TablePageCount != pageImageCount
            || priorTableBuild.EntryCount != pageEntryCount
            || !TryGetRawPhysical(
                priorTableBuild.RootTable,
                out ulong priorRoot)
            || priorRoot != rootStart
            || prior.State.TablePageCount != pageImageCount
            || !TryGetRawPhysical(
                prior.State.OwnedRoot,
                out ulong priorStaticRoot)
            || priorStaticRoot != rootStart)
        {
            return Fail(
                M01OwnedCr3CompositeTransitionStatus
                    .InvalidPriorPlanningEvidence);
        }

        if (!TryDeriveProbeLeaf(
                pageImages,
                pageImageCount,
                pageEntries,
                pageEntryCount,
                tableStart,
                tablePageEnd,
                rootStart,
                probeStart,
                priorPhysicalPageMask,
                out ulong expectedProbePteAddress,
                out ulong probeLeafFlatIndex,
                out ulong initialProbePteRaw,
                out ulong expectedProbePdptPage,
                out ulong expectedProbePdPage,
                out ulong expectedProbePtPage)
            || priorTableBuild.ProbeLeafEntryIndex
                != probeLeafFlatIndex
            || !ValidateProbePte(
                initialProbePteRaw,
                probeStart,
                priorPhysicalPageMask))
        {
            return Fail(
                M01OwnedCr3CompositeTransitionStatus.InvalidProbePte,
                expectedValue: probeStart,
                observedValue: initialProbePteRaw);
        }

        OS.Kernel.Memory.X64PageMapResult priorMutation =
            prior.Evidence.ProtectionMutation;
        OS.Kernel.Memory.X64ProtectionProbeStubPlanResult
            priorProbeReadback = prior.Evidence.ProbeReadback;
        if (!priorMutation.Succeeded
            || priorMutation.PageImageCount != pageImageCount
            || priorMutation.PageImageCapacity != pageImageCapacity
            || priorMutation.EntryCount != pageEntryCount
            || priorMutation.EntryCapacity != pageEntryCapacity
            || !TryGetRawPhysical(
                priorMutation.PageImagePhysicalStart,
                out ulong priorPageStart)
            || !TryGetRawPhysical(
                priorMutation.PageImagePhysicalEndExclusive,
                out ulong priorPageEnd)
            || priorPageStart != tableStart
            || priorPageEnd != tablePageEnd
            || priorMutation.TargetEntryIndex
                != probeLeafFlatIndex
            || !EqualsWithMonotonicAccessedDirty(
                initialProbePteRaw,
                priorMutation.PreviousEntry.RawValue)
            || !EqualsWithMonotonicAccessedDirty(
                initialProbePteRaw
                    & ~(PageWritableBit | PageNoExecuteBit),
                priorMutation.ReplacementEntry.RawValue)
            || !priorProbeReadback.ReadBackVerified
            || !TryGetRawVirtual(
                priorProbeReadback.EntryAddress,
                out ulong priorProbeEntry)
            || priorProbeEntry != probeStart
            || priorProbeReadback.RequiredByteCount
                != ExactProbeByteLength
            || priorProbeReadback.ReturnValue
                != ExactProbeReturnValue)
        {
            return Fail(
                M01OwnedCr3CompositeTransitionStatus
                    .InvalidPriorPlanningEvidence);
        }

        if (!TryFindUniqueReservationRange(
                reservationRecords,
                reservationRecordCount,
                OS.Kernel.Memory.BootReservationKind.BootStackGuard,
                out ulong guardStart,
                out ulong guardEnd)
            || !ValidateAbsentIdentityRange(
                pageImages,
                pageImageCount,
                pageEntries,
                pageEntryCount,
                rootStart,
                tableStart,
                tablePageEnd,
                priorPhysicalPageMask,
                guardStart,
                guardEnd))
        {
            return Fail(
                M01OwnedCr3CompositeTransitionStatus
                    .IdentityMappingProvenanceMismatch);
        }

        ulong auditedCriticalPages = 0;
        if (!ValidateCriticalRangeMapping(
                pageImages,
                pageImageCount,
                pageEntries,
                pageEntryCount,
                rootStart,
                tableStart,
                tablePageEnd,
                priorPhysicalPageMask,
                codeStart,
                codeEnd,
                OS.Kernel.Memory.PageProtection.Read
                    | OS.Kernel.Memory.PageProtection.Execute,
                ref auditedCriticalPages)
            || !ValidateCriticalRangeMapping(
                pageImages,
                pageImageCount,
                pageEntries,
                pageEntryCount,
                rootStart,
                tableStart,
                tablePageEnd,
                priorPhysicalPageMask,
                stackStart,
                stackEnd,
                OS.Kernel.Memory.PageProtection.Read
                    | OS.Kernel.Memory.PageProtection.Write,
                ref auditedCriticalPages)
            || !ValidateCriticalRangeMapping(
                pageImages,
                pageImageCount,
                pageEntries,
                pageEntryCount,
                rootStart,
                tableStart,
                tablePageEnd,
                priorPhysicalPageMask,
                probeStart,
                probeEnd,
                OS.Kernel.Memory.PageProtection.Read
                    | OS.Kernel.Memory.PageProtection.Write,
                ref auditedCriticalPages)
            || !ValidateCriticalRangeMapping(
                pageImages,
                pageImageCount,
                pageEntries,
                pageEntryCount,
                rootStart,
                tableStart,
                tablePageEnd,
                priorPhysicalPageMask,
                tableStart,
                tableEnd,
                OS.Kernel.Memory.PageProtection.Read
                    | OS.Kernel.Memory.PageProtection.Write,
                ref auditedCriticalPages))
        {
            return Fail(
                M01OwnedCr3CompositeTransitionStatus
                    .IdentityMappingProvenanceMismatch);
        }

        ulong finalProbePteRaw =
            initialProbePteRaw
            & ~(PageWritableBit | PageNoExecuteBit);
        if (!ValidateProbePtePair(
                initialProbePteRaw,
                finalProbePteRaw))
        {
            return Fail(
                M01OwnedCr3CompositeTransitionStatus.InvalidProbePte,
                expectedValue: finalProbePteRaw,
                observedValue: initialProbePteRaw);
        }

        if (!ContainsRange(
                stateStart,
                stateEnd,
                canaryRaw,
                TryAdd(canaryRaw, sizeof(ulong)))
            || RangesOverlap(
                runtimeStateRaw,
                runtimeStateRaw
                    + M01OwnedCr3CompositeTransitionRuntimeState
                        .StructureSize,
                canaryRaw,
                canaryRaw + sizeof(ulong))
            || (canaryRaw & (sizeof(ulong) - 1)) != 0
            || expectedCanary == 0
            || replacementCanary == 0
            || expectedCanary == replacementCanary)
        {
            return Fail(
                M01OwnedCr3CompositeTransitionStatus.InvalidCanary,
                expectedValue: expectedCanary,
                observedValue: replacementCanary);
        }

        if (!TryEncodeProbeHierarchyIdentity(
                auditedCriticalPages,
                tableStart,
                tablePageEnd,
                expectedProbePdptPage,
                expectedProbePdPage,
                expectedProbePtPage,
                out ulong expectedProbeHierarchyIdentity))
        {
            return Fail(
                M01OwnedCr3CompositeTransitionStatus
                    .InvalidProbeContract);
        }

        M01OwnedCr3CompositeTransitionRuntimeState state = new(
            ownedCr3Raw,
            tableStart,
            tablePageEnd,
            stateStart,
            stateEnd,
            codeStart,
            codeEnd,
            codeStart,
            codeEnd,
            expectedReturnRaw,
            stackStart,
            stackEnd,
            probeStart,
            probeEnd,
            probeStart,
            expectedProbePteAddress,
            initialProbePteRaw,
            finalProbePteRaw,
            canaryRaw,
            expectedCanary,
            replacementCanary,
            ExactProbeReturnValue,
            runtimeStateRaw,
            expectedProbeHierarchyIdentity);

        *stateDestination = state;
        *canaryDestination = expectedCanary;

        return new M01OwnedCr3CompositeTransitionResult(
            M01OwnedCr3CompositeTransitionStatus.Success,
            M01OwnedCr3CompositeTransitionResultPhase.StatePrepared,
            M01OwnedCr3CompositeTransitionTerminalKind.Uninitialized,
            state,
            codeStart,
            0,
            0,
            0,
            0,
            M01OwnedCr3CompositeTransitionResult.NoIndex,
            0,
            0,
            0);
    }

    public static M01OwnedCr3CompositeTransitionResult TryMaterialize(
        M01OwnedCr3CompositeTransitionRuntimeState* preparedState,
        ulong preparedStateCount,
        ulong preparedStateCapacity,
        byte* destination,
        ulong destinationCapacity,
        ulong entryAddress,
        M01OwnedCr3CompositeTransitionSemanticRecord*
            semanticDestination,
        ulong semanticDestinationCapacity)
    {
        M01OwnedCr3CompositeTransitionStatus preflight =
            ValidatePlanStorage(
                preparedState,
                preparedStateCount,
                preparedStateCapacity,
                destination,
                destinationCapacity,
                entryAddress,
                semanticDestination,
                semanticDestinationCapacity,
                isReadback: false,
                byteCount: 0,
                semanticCount: 0);
        if (preflight
            != M01OwnedCr3CompositeTransitionStatus.Success)
        {
            return Fail(preflight);
        }

        PlanWriter measurement = new(
            PlanWriterMode.Measure,
            null,
            MaximumByteCapacity,
            null,
            AuditedSemanticRecordCount);
        BuildPlan(ref measurement);
        if (!measurement.Succeeded
            || measurement.Position == 0
            || measurement.Position > MaximumByteCapacity
            || measurement.SemanticCount
                != AuditedSemanticRecordCount
            || measurement.Position != RequiredByteLength
            || measurement.InstructionCount
                != AuditedInstructionCount
            || destinationCapacity < measurement.Position)
        {
            return new M01OwnedCr3CompositeTransitionResult(
                M01OwnedCr3CompositeTransitionStatus
                    .InternalEncodingFailure,
                M01OwnedCr3CompositeTransitionResultPhase
                    .Uninitialized,
                M01OwnedCr3CompositeTransitionTerminalKind
                    .Uninitialized,
                *preparedState,
                entryAddress,
                measurement.Position,
                destinationCapacity,
                measurement.SemanticCount,
                semanticDestinationCapacity,
                measurement.InvalidIndex,
                RequiredByteLength,
                measurement.Position,
                0);
        }

        for (ulong index = 0;
            index < MaterializedPageByteLength;
            index++)
        {
            destination[index] = 0;
        }

        PlanWriter writer = new(
            PlanWriterMode.Emit,
            destination,
            destinationCapacity,
            semanticDestination,
            semanticDestinationCapacity);
        BuildPlan(ref writer);
        if (!writer.Succeeded
            || writer.Position != measurement.Position
            || writer.InstructionCount
                != measurement.InstructionCount
            || writer.SemanticCount != measurement.SemanticCount)
        {
            return new M01OwnedCr3CompositeTransitionResult(
                M01OwnedCr3CompositeTransitionStatus
                    .InternalEncodingFailure,
                M01OwnedCr3CompositeTransitionResultPhase
                    .Uninitialized,
                M01OwnedCr3CompositeTransitionTerminalKind
                    .Uninitialized,
                *preparedState,
                entryAddress,
                writer.Position,
                destinationCapacity,
                writer.SemanticCount,
                semanticDestinationCapacity,
                writer.InvalidIndex,
                measurement.Position,
                writer.Position,
                0);
        }

        ulong materializedHash = ComputeFnv1a64(
            destination,
            MaterializedPageByteLength);
        if (materializedHash != RequiredFnv1a64)
        {
            return new M01OwnedCr3CompositeTransitionResult(
                M01OwnedCr3CompositeTransitionStatus
                    .InternalEncodingFailure,
                M01OwnedCr3CompositeTransitionResultPhase
                    .Uninitialized,
                M01OwnedCr3CompositeTransitionTerminalKind
                    .Uninitialized,
                *preparedState,
                entryAddress,
                MaterializedPageByteLength,
                destinationCapacity,
                writer.SemanticCount,
                semanticDestinationCapacity,
                M01OwnedCr3CompositeTransitionResult.NoIndex,
                RequiredFnv1a64,
                materializedHash,
                materializedHash);
        }

        return new M01OwnedCr3CompositeTransitionResult(
            M01OwnedCr3CompositeTransitionStatus.Success,
            M01OwnedCr3CompositeTransitionResultPhase.Materialized,
            M01OwnedCr3CompositeTransitionTerminalKind.Uninitialized,
            *preparedState,
            entryAddress,
            MaterializedPageByteLength,
            destinationCapacity,
            writer.SemanticCount,
            semanticDestinationCapacity,
            M01OwnedCr3CompositeTransitionResult.NoIndex,
            writer.InstructionCount,
            writer.InstructionCount,
            materializedHash);
    }

    public static M01OwnedCr3CompositeTransitionResult
        ValidateReadback(
            M01OwnedCr3CompositeTransitionRuntimeState* preparedState,
            ulong preparedStateCount,
            ulong preparedStateCapacity,
            byte* source,
            ulong sourceCount,
            ulong sourceCapacity,
            ulong entryAddress,
            M01OwnedCr3CompositeTransitionSemanticRecord*
                semanticSource,
            ulong semanticCount,
            ulong semanticCapacity)
    {
        M01OwnedCr3CompositeTransitionStatus preflight =
            ValidatePlanStorage(
                preparedState,
                preparedStateCount,
                preparedStateCapacity,
                source,
                sourceCapacity,
                entryAddress,
                semanticSource,
                semanticCapacity,
                isReadback: true,
                sourceCount,
                semanticCount);
        if (preflight
            != M01OwnedCr3CompositeTransitionStatus.Success)
        {
            return Fail(preflight);
        }

        PlanWriter reader = new(
            PlanWriterMode.Compare,
            source,
            sourceCapacity,
            semanticSource,
            semanticCapacity);
        BuildPlan(ref reader);
        if (!reader.Succeeded)
        {
            M01OwnedCr3CompositeTransitionStatus status =
                reader.SemanticMismatch
                    ? M01OwnedCr3CompositeTransitionStatus
                        .SemanticMismatch
                    : M01OwnedCr3CompositeTransitionStatus.ByteMismatch;
            return new M01OwnedCr3CompositeTransitionResult(
                status,
                M01OwnedCr3CompositeTransitionResultPhase
                    .Uninitialized,
                M01OwnedCr3CompositeTransitionTerminalKind
                    .Uninitialized,
                *preparedState,
                entryAddress,
                sourceCount,
                sourceCapacity,
                semanticCount,
                semanticCapacity,
                reader.InvalidIndex,
                reader.ExpectedValue,
                reader.ObservedValue,
                ComputeFnv1a64(source, sourceCount));
        }

        if (reader.Position == 0
            || reader.SemanticCount != semanticCount
            || reader.Position != RequiredByteLength
            || reader.InstructionCount
                != AuditedInstructionCount)
        {
            return Fail(
                M01OwnedCr3CompositeTransitionStatus
                    .InternalEncodingFailure);
        }

        for (ulong index = reader.Position;
            index < MaterializedPageByteLength;
            index++)
        {
            if (source[index] != 0)
            {
                return new M01OwnedCr3CompositeTransitionResult(
                    M01OwnedCr3CompositeTransitionStatus.ByteMismatch,
                    M01OwnedCr3CompositeTransitionResultPhase
                        .Uninitialized,
                    M01OwnedCr3CompositeTransitionTerminalKind
                        .Uninitialized,
                    *preparedState,
                    entryAddress,
                    sourceCount,
                    sourceCapacity,
                    semanticCount,
                    semanticCapacity,
                    index,
                    0,
                    source[index],
                    ComputeFnv1a64(
                        source,
                        MaterializedPageByteLength));
            }
        }

        ulong readbackHash = ComputeFnv1a64(
            source,
            MaterializedPageByteLength);
        if (readbackHash != RequiredFnv1a64)
        {
            return new M01OwnedCr3CompositeTransitionResult(
                M01OwnedCr3CompositeTransitionStatus.ByteMismatch,
                M01OwnedCr3CompositeTransitionResultPhase
                    .Uninitialized,
                M01OwnedCr3CompositeTransitionTerminalKind
                    .Uninitialized,
                *preparedState,
                entryAddress,
                sourceCount,
                sourceCapacity,
                semanticCount,
                semanticCapacity,
                M01OwnedCr3CompositeTransitionResult.NoIndex,
                RequiredFnv1a64,
                readbackHash,
                readbackHash);
        }

        return new M01OwnedCr3CompositeTransitionResult(
            M01OwnedCr3CompositeTransitionStatus.Success,
            M01OwnedCr3CompositeTransitionResultPhase
                .ReadbackVerified,
            M01OwnedCr3CompositeTransitionTerminalKind.Uninitialized,
            *preparedState,
            entryAddress,
            sourceCount,
            sourceCapacity,
            semanticCount,
            semanticCapacity,
            M01OwnedCr3CompositeTransitionResult.NoIndex,
            reader.InstructionCount,
            reader.InstructionCount,
            readbackHash);
    }

    public static M01OwnedCr3CompositeTransitionResult
        AuditObservedState(
            M01OwnedCr3CompositeTransitionRuntimeState* preparedState,
            ulong preparedStateCount,
            ulong preparedStateCapacity,
            M01OwnedCr3CompositeTransitionRuntimeState* observedState,
            ulong observedStateCount,
            ulong observedStateCapacity,
            M01OwnedCr3CompositeTransitionTerminalKind terminalKind,
            uint observedRuntimeReturnValue)
    {
        return AuditResult(
            M01OwnedCr3CompositeTransitionStatus
                .SeparateLeavesNotCompositeEvidence,
            terminalKind,
            default);
    }

    public static M01OwnedCr3CompositeTransitionResult
        AuditObservedState(
            M01OwnedCr3CompositeTransitionResult* readbackEvidence,
            ulong readbackEvidenceCount,
            ulong readbackEvidenceCapacity,
            M01OwnedCr3CompositeTransitionRuntimeState* observedState,
            ulong observedStateCount,
            ulong observedStateCapacity,
            M01OwnedCr3CompositeTransitionTerminalKind terminalKind,
            uint observedRuntimeReturnValue)
    {
        if (readbackEvidence == null
            || readbackEvidenceCount != 1
            || readbackEvidenceCapacity != 1)
        {
            return AuditResult(
                M01OwnedCr3CompositeTransitionStatus
                    .SeparateLeavesNotCompositeEvidence,
                terminalKind,
                default,
                expectedValue: 1,
                observedValue: readbackEvidenceCount);
        }

        ulong evidenceStart = (ulong)readbackEvidence;
        if ((evidenceStart & (RequiredPointerAlignment - 1)) != 0
            || evidenceStart
                > ~0UL
                    - (ulong)sizeof(
                        M01OwnedCr3CompositeTransitionResult))
        {
            return AuditResult(
                M01OwnedCr3CompositeTransitionStatus
                    .SeparateLeavesNotCompositeEvidence,
                terminalKind,
                default);
        }

        M01OwnedCr3CompositeTransitionResult readback =
            *readbackEvidence;
        if (!readback.IsReadbackVerified
            || !readback.ProvesCompositeEncodingReadback
            || readback.Status
                != M01OwnedCr3CompositeTransitionStatus.Success
            || readback.Phase
                != M01OwnedCr3CompositeTransitionResultPhase
                    .ReadbackVerified
            || readback.EntryAddress == 0
            || readback.ByteCount != MaterializedPageByteLength
            || readback.ByteCapacity != MaterializedPageByteLength
            || readback.SemanticCount != AuditedSemanticRecordCount
            || readback.SemanticCapacity != AuditedSemanticRecordCount
            || readback.InvalidIndex
                != M01OwnedCr3CompositeTransitionResult.NoIndex
            || readback.ExpectedValue != AuditedInstructionCount
            || readback.ObservedValue != AuditedInstructionCount
            || RequiredFnv1a64 == 0
            || readback.Fnv1a64 != RequiredFnv1a64)
        {
            return AuditResult(
                M01OwnedCr3CompositeTransitionStatus
                    .SeparateLeavesNotCompositeEvidence,
                terminalKind,
                readback.State,
                expectedValue: RequiredFnv1a64,
                observedValue: readback.Fnv1a64);
        }

        M01OwnedCr3CompositeTransitionRuntimeState baseline =
            readback.State;
        if (readback.EntryAddress != baseline.CompositeCodeStart)
        {
            return AuditResult(
                M01OwnedCr3CompositeTransitionStatus
                    .SeparateLeavesNotCompositeEvidence,
                terminalKind,
                readback.State);
        }

        return AuditObservedStateCore(
            &baseline,
            1,
            1,
            observedState,
            observedStateCount,
            observedStateCapacity,
            terminalKind,
            observedRuntimeReturnValue);
    }

    public static M01OwnedCr3CompositeCallGateResult PrepareCallGate(
        M01LivePagingPreparationResult* priorPlanningEvidence,
        ulong priorPlanningEvidenceCount,
        ulong priorPlanningEvidenceCapacity,
        M01OwnedCr3CompositeTransitionResult* compositePreparation,
        ulong compositePreparationCount,
        ulong compositePreparationCapacity,
        BootMemoryRegionRecord* allocationRecords,
        ulong allocationRecordCount,
        ulong allocationRecordCapacity,
        OS.Kernel.Memory.BootReservedRange* reservationRecords,
        ulong reservationRecordCount,
        ulong reservationRecordCapacity,
        OS.Kernel.Boot.X64BootIdentityMappingRange* identityMappings,
        ulong identityMappingCount,
        ulong identityMappingCapacity,
        M01OwnedCr3CompositeTransitionRangeBinding*
            codeAndGateBinding,
        ulong codeAndGateBindingCount,
        ulong codeAndGateBindingCapacity,
        M01OwnedCr3CompositeCallGateObservation*
            observationDestination,
        ulong observationDestinationCount,
        ulong observationDestinationCapacity,
        VirtualAddress observationAddress,
        ulong expectedBoundaryCanary)
    {
        if (priorPlanningEvidence == null)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus
                    .MissingPriorPlanningEvidence);
        }
        if (priorPlanningEvidenceCount != 1
            || priorPlanningEvidenceCapacity != 1)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus
                    .InvalidPriorPlanningEvidence,
                expectedValue: 1,
                observedValue:
                    priorPlanningEvidenceCount != 1
                        ? priorPlanningEvidenceCount
                        : priorPlanningEvidenceCapacity);
        }

        if (compositePreparation == null)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus
                    .MissingCompositePreparation);
        }
        if (compositePreparationCount != 1
            || compositePreparationCapacity != 1)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus
                    .InvalidCompositePreparation,
                expectedValue: 1,
                observedValue:
                    compositePreparationCount != 1
                        ? compositePreparationCount
                        : compositePreparationCapacity);
        }

        if (allocationRecords == null)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus
                    .MissingAllocationRecords);
        }
        if (allocationRecordCount == 0
            || allocationRecordCount > MaximumAllocationRecordCount)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus
                    .InvalidAllocationRecordCount,
                observedValue: allocationRecordCount);
        }
        if (allocationRecordCapacity < allocationRecordCount
            || allocationRecordCapacity > MaximumAllocationRecordCount)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus
                    .AllocationRecordCapacityMismatch,
                expectedValue: allocationRecordCount,
                observedValue: allocationRecordCapacity);
        }

        if (reservationRecords == null)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus
                    .MissingReservationRecords);
        }
        if (reservationRecordCount == 0
            || reservationRecordCount
                > MaximumReservationRecordCount)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus
                    .InvalidReservationRecordCount,
                observedValue: reservationRecordCount);
        }
        if (reservationRecordCapacity < reservationRecordCount
            || reservationRecordCapacity
                > MaximumReservationRecordCount)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus
                    .ReservationRecordCapacityMismatch,
                expectedValue: reservationRecordCount,
                observedValue: reservationRecordCapacity);
        }

        if (identityMappings == null)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus
                    .MissingIdentityMappings);
        }
        if (identityMappingCount == 0
            || identityMappingCount > MaximumIdentityMappingCount)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus
                    .InvalidIdentityMappingCount,
                observedValue: identityMappingCount);
        }
        if (identityMappingCapacity < identityMappingCount
            || identityMappingCapacity > MaximumIdentityMappingCount)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus
                    .IdentityMappingCapacityMismatch,
                expectedValue: identityMappingCount,
                observedValue: identityMappingCapacity);
        }

        if (codeAndGateBinding == null)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus.MissingCodeBinding);
        }
        if (codeAndGateBindingCount != 1)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus
                    .InvalidCodeBindingCount,
                expectedValue: 1,
                observedValue: codeAndGateBindingCount);
        }
        if (codeAndGateBindingCapacity != 1)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus
                    .CodeBindingCapacityMismatch,
                expectedValue: 1,
                observedValue: codeAndGateBindingCapacity);
        }

        if (observationDestination == null)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus.MissingObservation);
        }
        if (observationDestinationCount != 1)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus
                    .InvalidObservationCount,
                expectedValue: 1,
                observedValue: observationDestinationCount);
        }
        if (observationDestinationCapacity != 1)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus
                    .ObservationCapacityMismatch,
                expectedValue: 1,
                observedValue: observationDestinationCapacity);
        }

        ulong priorStart = 0;
        ulong priorEnd = 0;
        ulong compositeResultStart = 0;
        ulong compositeResultEnd = 0;
        ulong allocationsStart = 0;
        ulong allocationsEnd = 0;
        ulong reservationsStart = 0;
        ulong reservationsEnd = 0;
        ulong mappingsStart = 0;
        ulong mappingsEnd = 0;
        ulong bindingStart = 0;
        ulong bindingEnd = 0;
        if (!TryGetCallGateSpan(
                priorPlanningEvidence,
                1,
                (ulong)sizeof(M01LivePagingPreparationResult),
                out priorStart,
                out priorEnd)
            || !TryGetCallGateSpan(
                compositePreparation,
                1,
                (ulong)sizeof(
                    M01OwnedCr3CompositeTransitionResult),
                out compositeResultStart,
                out compositeResultEnd)
            || !TryGetCallGateSpan(
                allocationRecords,
                allocationRecordCapacity,
                (ulong)sizeof(BootMemoryRegionRecord),
                out allocationsStart,
                out allocationsEnd)
            || !TryGetCallGateSpan(
                reservationRecords,
                reservationRecordCapacity,
                (ulong)sizeof(
                    OS.Kernel.Memory.BootReservedRange),
                out reservationsStart,
                out reservationsEnd)
            || !TryGetCallGateSpan(
                identityMappings,
                identityMappingCapacity,
                (ulong)sizeof(
                    OS.Kernel.Boot.X64BootIdentityMappingRange),
                out mappingsStart,
                out mappingsEnd)
            || !TryGetCallGateSpan(
                codeAndGateBinding,
                1,
                (ulong)sizeof(
                    M01OwnedCr3CompositeTransitionRangeBinding),
                out bindingStart,
                out bindingEnd))
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus
                    .InvalidPriorPlanningEvidence);
        }

        ulong observationStart = (ulong)observationDestination;
        if ((observationStart & (RequiredPointerAlignment - 1)) != 0)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus
                    .MisalignedObservation);
        }
        if (observationStart
            > ~0UL
                - M01OwnedCr3CompositeCallGateObservation.StructureSize)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus
                    .ObservationSpanOverflow);
        }
        ulong observationEnd =
            observationStart
            + M01OwnedCr3CompositeCallGateObservation.StructureSize;

        M01LivePagingPreparationResult prior =
            *priorPlanningEvidence;
        M01OwnedCr3CompositeTransitionResult composite =
            *compositePreparation;
        if (!prior.Succeeded
            || !prior.PreparedForCompositeTransition
            || !prior.IsPreparationOnly
            || prior.IsModelOnly
            || prior.IsExecutionEvidence
            || !prior.UsesExactLiveIntervalBindings
            || !prior.Evidence.Preflight.Succeeded)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus
                    .InvalidPriorPlanningEvidence);
        }
        if (!composite.IsStatePrepared
            || composite.Status
                != M01OwnedCr3CompositeTransitionStatus.Success
            || composite.Phase
                != M01OwnedCr3CompositeTransitionResultPhase
                    .StatePrepared
            || composite.TerminalKind
                != M01OwnedCr3CompositeTransitionTerminalKind
                    .Uninitialized
            || composite.EntryAddress == 0
            || composite.ByteCount != 0
            || composite.ByteCapacity != 0
            || composite.SemanticCount != 0
            || composite.SemanticCapacity != 0
            || composite.InvalidIndex
                != M01OwnedCr3CompositeTransitionResult.NoIndex
            || composite.ExpectedValue != 0
            || composite.ObservedValue != 0
            || composite.Fnv1a64 != 0)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus
                    .InvalidCompositePreparation);
        }

        M01OwnedCr3CompositeTransitionRuntimeState state =
            composite.State;
        ulong codeBase = state.CompositeCodeStart;
        if (codeBase == 0
            || state.CompositeCodeEndExclusive <= codeBase)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus.InvalidCodeBase);
        }
        if ((codeBase & (EntryAlignment - 1)) != 0)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus.MisalignedCodeBase);
        }
        if (codeBase > ~0UL - PageSize
            || state.CompositeCodeEndExclusive != codeBase + PageSize)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus.CodeRangeOverflow);
        }

        ulong compositeEntry = codeBase + CompositeEntryOffset;
        ulong gateEntry = codeBase + CallGateOffset;
        ulong returnSite = codeBase + CallNextReturnSiteOffset;
        if (composite.EntryAddress != compositeEntry)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus
                    .InvalidCompositeEntry,
                expectedValue: compositeEntry,
                observedValue: composite.EntryAddress);
        }
        if ((gateEntry & (EntryAlignment - 1)) != 0
            || gateEntry < codeBase
            || gateEntry >= state.CompositeCodeEndExclusive)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus.InvalidGateEntry,
                expectedValue: codeBase + CallGateOffset,
                observedValue: gateEntry);
        }
        if (state.ExpectedReturnAddress != returnSite
            || returnSite <= gateEntry
            || returnSite >= state.CompositeCodeEndExclusive)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus.InvalidReturnSite,
                expectedValue: returnSite,
                observedValue: state.ExpectedReturnAddress);
        }
        if (!HasCanonicalCallGateLayout())
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus.CodeLayoutOverlap);
        }

        if (prior.State.Cr3CodePageAddress != codeBase
            || prior.State.Cr3CodePageCapacity != PageSize
            || prior.State.RawFirmwareCr3
                != prior.Evidence.Preflight.RawFirmwareCr3
            || prior.State.RawCr4 != prior.Evidence.Preflight.RawCr4
            || prior.State.OriginalRflags
                != prior.Evidence.Preflight.Rflags
            || !TryGetRawPhysical(
                prior.State.OwnedRoot,
                out ulong priorRoot)
            || (state.OwnedCr3Raw & Cr3ArchitecturalBaseMask)
                != priorRoot
            || prior.State.AllocationTableAddress
                != (ulong)allocationRecords
            || prior.State.AllocationRecordCount
                != allocationRecordCount
            || prior.State.AllocationRecordCapacity
                != allocationRecordCapacity
            || prior.State.ReservationLedgerAddress
                != (ulong)reservationRecords
            || prior.State.ReservationCount != reservationRecordCount
            || prior.State.ReservationCapacity
                != reservationRecordCapacity
            || prior.State.MappingRangeAddress != (ulong)identityMappings
            || prior.State.MappingRangeCount != identityMappingCount
            || prior.State.MappingRangeCapacity
                != identityMappingCapacity)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus
                    .InvalidPriorPlanningEvidence);
        }

        M01OwnedCr3CompositeTransitionRangeBinding binding =
            codeAndGateBinding[0];
        M01OwnedCr3CompositeTransitionStatus bindingStatus =
            ValidateRangeBinding(
                binding,
                allocationRecords,
                allocationRecordCount,
                reservationRecords,
                reservationRecordCount,
                identityMappings,
                identityMappingCount,
                prior.Evidence.Cr3CodeReservationIndex,
                prior.Evidence.ProtectionProbeReservationIndex,
                prior.Evidence.TableStateReservationIndex);
        if (bindingStatus
                != M01OwnedCr3CompositeTransitionStatus.Success
            || binding.Role
                != M01OwnedCr3CompositeTransitionRangeRole
                    .CompositeCodeAndCallGate
            || !TryGetRawRange(
                binding,
                out ulong bindingCodeStart,
                out ulong bindingCodeEnd)
            || bindingCodeStart != codeBase
            || bindingCodeEnd != codeBase + PageSize)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus.InvalidCodeBinding,
                expectedValue:
                    (ulong)M01OwnedCr3CompositeTransitionStatus.Success,
                observedValue: (ulong)bindingStatus);
        }

        OS.Kernel.Memory.X64PagingModePreflightResult preflight =
            prior.Evidence.Preflight;
        if (!preflight.Succeeded
            || !preflight.ProbeEnabled
            || !preflight.PagingEnabled
            || !preflight.WriteProtectEnabled
            || !preflight.PaeEnabled
            || !preflight.LongModeEnabled
            || !preflight.LongModeActive
            || !preflight.NxeEnabled
            || !preflight.CpuidAddressLeafAvailable
            || !preflight.OwnedRootAligned
            || !preflight.OwnedRootInsideArena)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus.InvalidModePreflight);
        }
        if ((preflight.Rflags & RflagsTrapFlagBit) != 0)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus.TrapFlagSet);
        }
        if ((preflight.Rflags & (1UL << 10)) != 0)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus.DirectionFlagSet);
        }
        if ((preflight.RawCr4
            & Cr4ControlFlowEnforcementBit) != 0)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus.CetUnsupported);
        }
        if (preflight.La57Enabled
            || (preflight.RawCr4 & Cr4FiveLevelPagingBit) != 0)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus
                    .FiveLevelPagingUnsupported);
        }
        if (preflight.PcideEnabled
            || (preflight.RawCr4 & Cr4PcideBit) != 0)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus.PcideUnsupported);
        }
        if ((preflight.Rflags & (1UL << 1)) == 0)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus
                    .InvalidInterruptPolicy);
        }
        if (!TryGetRawVirtual(
                observationAddress,
                out ulong observationAddressRaw)
            || observationAddressRaw != observationStart
            || observationEnd > LowCanonicalExclusiveCeiling
            || !ContainsRange(
                state.TableStateArenaStart,
                state.TableStateArenaEndExclusive,
                observationStart,
                observationEnd))
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus
                    .MisalignedObservation,
                expectedValue: observationStart,
                observedValue: observationAddressRaw);
        }

        ulong stateEnd = TryAdd(
            state.ExpectedSelfAddress,
            M01OwnedCr3CompositeTransitionRuntimeState.StructureSize);
        ulong compositeCanaryEnd = TryAdd(
            state.CanaryAddress,
            sizeof(ulong));
        if (stateEnd == 0
            || compositeCanaryEnd == 0
            || RangesOverlap(
                observationStart,
                observationEnd,
                state.ExpectedSelfAddress,
                stateEnd)
            || RangesOverlap(
                observationStart,
                observationEnd,
                state.CanaryAddress,
                compositeCanaryEnd)
            || RangesOverlap(
                observationStart,
                observationEnd,
                codeBase,
                codeBase + PageSize)
            || RangesOverlap(
                observationStart,
                observationEnd,
                priorStart,
                priorEnd)
            || RangesOverlap(
                observationStart,
                observationEnd,
                compositeResultStart,
                compositeResultEnd)
            || RangesOverlap(
                observationStart,
                observationEnd,
                allocationsStart,
                allocationsEnd)
            || RangesOverlap(
                observationStart,
                observationEnd,
                reservationsStart,
                reservationsEnd)
            || RangesOverlap(
                observationStart,
                observationEnd,
                mappingsStart,
                mappingsEnd)
            || RangesOverlap(
                observationStart,
                observationEnd,
                bindingStart,
                bindingEnd))
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus.ObservationAlias);
        }

        if (!IsAllZero(
                (byte*)observationDestination,
                M01OwnedCr3CompositeCallGateObservation.StructureSize))
        {
            return FailCallGate(
                observationDestination->IsInitialized
                    ? M01OwnedCr3CompositeCallGateStatus.RepeatedUse
                    : M01OwnedCr3CompositeCallGateStatus
                        .ObservationNotPristine);
        }
        if (expectedBoundaryCanary == 0)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus
                    .BoundaryCanaryMismatch);
        }

        M01OwnedCr3CompositeCallGateObservation observation = default;
        observation.HeaderSize =
            M01OwnedCr3CompositeCallGateObservation.StructureSize;
        observation.HeaderVersion =
            M01OwnedCr3CompositeCallGateObservation.StructureVersion;
        observation.IsInitializedValue = 1;
        observation.LifecycleValue =
            M01OwnedCr3CompositeCallGateObservationLifecycle.Armed;
        observation.ExpectedSelfAddressValue = observationStart;
        observation.ExpectedCodeBaseValue = codeBase;
        observation.ExpectedCompositeEntryAddressValue = compositeEntry;
        observation.ExpectedGateEntryAddressValue = gateEntry;
        observation.ExpectedReturnSiteAddressValue = returnSite;
        observation.ExpectedOriginalRflagsValue = preflight.Rflags;
        observation.ExpectedFirmwareCr3RawValue =
            preflight.RawFirmwareCr3;
        observation.ExpectedFirmwareCr4RawValue = preflight.RawCr4;
        observation.ExpectedBoundaryCanaryValue =
            expectedBoundaryCanary;
        observation.ExpectedCompositeReturnValueValue =
            (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success;
        observation.ExpectedInterruptEnabledValue =
            (preflight.Rflags & RflagsInterruptEnableBit) != 0
                ? (byte)1
                : (byte)0;
        observation.ExpectedPgeWasSetValue =
            (preflight.RawCr4 & Cr4PageGlobalEnableBit) != 0
                ? (byte)1
                : (byte)0;
        observation.InterruptPolicyValue =
            M01OwnedCr3CompositeCallGateInterruptPolicy
                .PreserveEntryState;
        observation.PgePolicyValue =
            M01OwnedCr3CompositeCallGatePgePolicy
                .ClearDuringOwnedIntervalAndRestoreExact;
        observation.BoundaryCanaryValue = expectedBoundaryCanary;
        observation.CanaryBeforeValue = 0;
        observation.PostReturnRflagsValue = 0;
        observation.PostReturnCr3RawValue = 0;
        observation.PostReturnCr4RawValue = 0;
        observation.CanaryAfterValue = 0;
        observation.CompositeReturnValueValue = 0;
        observation.CallCountValue = 0;
        observation.PostReturnObservationCountValue = 0;
        observation.ReservedOutput0 = 0;
        observation.CompletionSignatureValue = 0;
        observation.ReservedOutput1 = 0;
        observation.ReservedOutput2 = 0;
        *observationDestination = observation;

        return CallGateSuccess(
            M01OwnedCr3CompositeCallGatePhase.Prepared,
            M01OwnedCr3CompositeTransitionTerminalKind.Uninitialized,
            observation,
            state,
            codeBase,
            0,
            0);
    }

    public static M01OwnedCr3CompositeCallGateResult
        TryMaterializeCallGate(
            M01OwnedCr3CompositeCallGateResult* preparationEvidence,
            ulong preparationEvidenceCount,
            ulong preparationEvidenceCapacity,
            M01OwnedCr3CompositeTransitionRuntimeState* preparedState,
            ulong preparedStateCount,
            ulong preparedStateCapacity,
            byte* destination,
            ulong destinationCount,
            ulong destinationCapacity,
            ulong codeBase,
            M01OwnedCr3CompositeTransitionSemanticRecord*
                semanticDestination,
            ulong semanticDestinationCount,
            ulong semanticDestinationCapacity)
    {
        M01OwnedCr3CompositeCallGateStatus storageStatus =
            ValidateCallGateStorage(
                preparationEvidence,
                preparationEvidenceCount,
                preparationEvidenceCapacity,
                M01OwnedCr3CompositeCallGatePhase.Prepared,
                preparedState,
                preparedStateCount,
                preparedStateCapacity,
                destination,
                destinationCount,
                destinationCapacity,
                codeBase,
                semanticDestination,
                semanticDestinationCount,
                semanticDestinationCapacity);
        if (storageStatus
            != M01OwnedCr3CompositeCallGateStatus.Success)
        {
            return FailCallGate(storageStatus);
        }

        M01OwnedCr3CompositeCallGateResult preparation =
            *preparationEvidence;
        M01OwnedCr3CompositeTransitionRuntimeState state =
            *preparedState;

        PlanWriter gateMeasurement = new(
            PlanWriterMode.Measure,
            null,
            CallGateByteLength,
            null,
            0);
        BuildCallGate(ref gateMeasurement);
        if (!gateMeasurement.Succeeded
            || gateMeasurement.Position != CallGateByteLength
            || gateMeasurement.InstructionCount
                != CallGateInstructionCount
            || gateMeasurement.SemanticCount != 0)
        {
            return CallGateFailure(
                M01OwnedCr3CompositeCallGateStatus
                    .InternalEncodingFailure,
                M01OwnedCr3CompositeCallGatePhase.Materialized,
                M01OwnedCr3CompositeTransitionTerminalKind
                    .Uninitialized,
                preparation.Observation,
                state,
                codeBase,
                gateMeasurement.InvalidIndex,
                CallGateByteLength,
                gateMeasurement.Position,
                0);
        }

        M01OwnedCr3CompositeTransitionResult compositeResult =
            TryMaterialize(
                preparedState,
                preparedStateCount,
                preparedStateCapacity,
                destination,
                destinationCapacity,
                codeBase,
                semanticDestination,
                semanticDestinationCapacity);
        if (!compositeResult.IsMaterialized
            || compositeResult.Status
                != M01OwnedCr3CompositeTransitionStatus.Success
            || compositeResult.ByteCount
                != MaterializedPageByteLength
            || compositeResult.SemanticCount
                != AuditedSemanticRecordCount
            || compositeResult.Fnv1a64 != RequiredFnv1a64)
        {
            return CallGateFailure(
                M01OwnedCr3CompositeCallGateStatus
                    .InternalEncodingFailure,
                M01OwnedCr3CompositeCallGatePhase.Materialized,
                M01OwnedCr3CompositeTransitionTerminalKind
                    .Uninitialized,
                preparation.Observation,
                state,
                codeBase,
                compositeResult.InvalidIndex,
                (ulong)M01OwnedCr3CompositeTransitionStatus.Success,
                (ulong)compositeResult.Status,
                compositeResult.Fnv1a64);
        }

        PlanWriter gateWriter = new(
            PlanWriterMode.Emit,
            destination + CallGateOffset,
            CallGateByteLength,
            null,
            0);
        BuildCallGate(ref gateWriter);
        if (!gateWriter.Succeeded
            || gateWriter.Position != CallGateByteLength
            || gateWriter.InstructionCount
                != CallGateInstructionCount
            || gateWriter.SemanticCount != 0)
        {
            return CallGateFailure(
                M01OwnedCr3CompositeCallGateStatus
                    .InternalEncodingFailure,
                M01OwnedCr3CompositeCallGatePhase.Materialized,
                M01OwnedCr3CompositeTransitionTerminalKind
                    .Uninitialized,
                preparation.Observation,
                state,
                codeBase,
                gateWriter.InvalidIndex == M01OwnedCr3CompositeTransitionResult
                    .NoIndex
                    ? gateWriter.InvalidIndex
                    : CallGateOffset + gateWriter.InvalidIndex,
                CallGateByteLength,
                gateWriter.Position,
                0);
        }

        ulong gateHash = ComputeFnv1a64(
            destination + CallGateOffset,
            CallGateByteLength);
        if (gateHash != RequiredCallGateFnv1a64)
        {
            return CallGateFailure(
                M01OwnedCr3CompositeCallGateStatus
                    .InternalEncodingFailure,
                M01OwnedCr3CompositeCallGatePhase.Materialized,
                M01OwnedCr3CompositeTransitionTerminalKind
                    .Uninitialized,
                preparation.Observation,
                state,
                codeBase,
                M01OwnedCr3CompositeCallGateResult.NoIndex,
                RequiredCallGateFnv1a64,
                gateHash,
                gateHash);
        }

        ulong nonzeroIndex = FindFirstCombinedPaddingNonzero(
            destination);
        if (nonzeroIndex
            != M01OwnedCr3CompositeCallGateResult.NoIndex)
        {
            return CallGateFailure(
                M01OwnedCr3CompositeCallGateStatus.ByteMismatch,
                M01OwnedCr3CompositeCallGatePhase.Materialized,
                M01OwnedCr3CompositeTransitionTerminalKind
                    .Uninitialized,
                preparation.Observation,
                state,
                codeBase,
                nonzeroIndex,
                0,
                destination[nonzeroIndex],
                ComputeFnv1a64(
                    destination,
                    MaterializedPageByteLength));
        }

        ulong combinedHash = ComputeFnv1a64(
            destination,
            MaterializedPageByteLength);
        if (combinedHash != RequiredCombinedFnv1a64)
        {
            return CallGateFailure(
                M01OwnedCr3CompositeCallGateStatus
                    .InternalEncodingFailure,
                M01OwnedCr3CompositeCallGatePhase.Materialized,
                M01OwnedCr3CompositeTransitionTerminalKind
                    .Uninitialized,
                preparation.Observation,
                state,
                codeBase,
                M01OwnedCr3CompositeCallGateResult.NoIndex,
                RequiredCombinedFnv1a64,
                combinedHash,
                combinedHash);
        }

        return CallGateSuccess(
            M01OwnedCr3CompositeCallGatePhase.Materialized,
            M01OwnedCr3CompositeTransitionTerminalKind.Uninitialized,
            preparation.Observation,
            state,
            codeBase,
            MaterializedPageByteLength,
            combinedHash);
    }

    public static M01OwnedCr3CompositeCallGateResult
        ValidateCallGateReadback(
            M01OwnedCr3CompositeCallGateResult*
                materializationEvidence,
            ulong materializationEvidenceCount,
            ulong materializationEvidenceCapacity,
            M01OwnedCr3CompositeTransitionRuntimeState* preparedState,
            ulong preparedStateCount,
            ulong preparedStateCapacity,
            byte* source,
            ulong sourceCount,
            ulong sourceCapacity,
            ulong codeBase,
            M01OwnedCr3CompositeTransitionSemanticRecord*
                semanticSource,
            ulong semanticCount,
            ulong semanticCapacity)
    {
        M01OwnedCr3CompositeCallGateStatus storageStatus =
            ValidateCallGateStorage(
                materializationEvidence,
                materializationEvidenceCount,
                materializationEvidenceCapacity,
                M01OwnedCr3CompositeCallGatePhase.Materialized,
                preparedState,
                preparedStateCount,
                preparedStateCapacity,
                source,
                sourceCount,
                sourceCapacity,
                codeBase,
                semanticSource,
                semanticCount,
                semanticCapacity);
        if (storageStatus
            != M01OwnedCr3CompositeCallGateStatus.Success)
        {
            return FailCallGate(storageStatus);
        }

        M01OwnedCr3CompositeCallGateResult materialization =
            *materializationEvidence;
        M01OwnedCr3CompositeTransitionRuntimeState state =
            *preparedState;

        PlanWriter compositeReader = new(
            PlanWriterMode.Compare,
            source,
            sourceCapacity,
            semanticSource,
            semanticCapacity);
        BuildPlan(ref compositeReader);
        if (!compositeReader.Succeeded
            || compositeReader.Position != RequiredByteLength
            || compositeReader.InstructionCount
                != AuditedInstructionCount
            || compositeReader.SemanticCount
                != AuditedSemanticRecordCount)
        {
            return CallGateFailure(
                compositeReader.Succeeded
                    ? M01OwnedCr3CompositeCallGateStatus
                        .InternalEncodingFailure
                    : M01OwnedCr3CompositeCallGateStatus.ByteMismatch,
                M01OwnedCr3CompositeCallGatePhase.ReadbackVerified,
                M01OwnedCr3CompositeTransitionTerminalKind
                    .Uninitialized,
                materialization.Observation,
                state,
                codeBase,
                compositeReader.InvalidIndex,
                compositeReader.ExpectedValue,
                compositeReader.ObservedValue,
                ComputeFnv1a64(
                    source,
                    MaterializedPageByteLength));
        }

        PlanWriter gateReader = new(
            PlanWriterMode.Compare,
            source + CallGateOffset,
            CallGateByteLength,
            null,
            0);
        BuildCallGate(ref gateReader);
        if (!gateReader.Succeeded
            || gateReader.Position != CallGateByteLength
            || gateReader.InstructionCount
                != CallGateInstructionCount
            || gateReader.SemanticCount != 0)
        {
            ulong invalidIndex = gateReader.InvalidIndex;
            if (invalidIndex
                != M01OwnedCr3CompositeTransitionResult.NoIndex)
            {
                invalidIndex += CallGateOffset;
            }
            return CallGateFailure(
                gateReader.Succeeded
                    ? M01OwnedCr3CompositeCallGateStatus
                        .InternalEncodingFailure
                    : M01OwnedCr3CompositeCallGateStatus.ByteMismatch,
                M01OwnedCr3CompositeCallGatePhase.ReadbackVerified,
                M01OwnedCr3CompositeTransitionTerminalKind
                    .Uninitialized,
                materialization.Observation,
                state,
                codeBase,
                invalidIndex,
                gateReader.ExpectedValue,
                gateReader.ObservedValue,
                ComputeFnv1a64(
                    source,
                    MaterializedPageByteLength));
        }

        ulong nonzeroIndex = FindFirstCombinedPaddingNonzero(source);
        if (nonzeroIndex
            != M01OwnedCr3CompositeCallGateResult.NoIndex)
        {
            return CallGateFailure(
                M01OwnedCr3CompositeCallGateStatus.ByteMismatch,
                M01OwnedCr3CompositeCallGatePhase.ReadbackVerified,
                M01OwnedCr3CompositeTransitionTerminalKind
                    .Uninitialized,
                materialization.Observation,
                state,
                codeBase,
                nonzeroIndex,
                0,
                source[nonzeroIndex],
                ComputeFnv1a64(
                    source,
                    MaterializedPageByteLength));
        }

        ulong gateHash = ComputeFnv1a64(
            source + CallGateOffset,
            CallGateByteLength);
        if (gateHash != RequiredCallGateFnv1a64)
        {
            return CallGateFailure(
                M01OwnedCr3CompositeCallGateStatus.ByteMismatch,
                M01OwnedCr3CompositeCallGatePhase.ReadbackVerified,
                M01OwnedCr3CompositeTransitionTerminalKind
                    .Uninitialized,
                materialization.Observation,
                state,
                codeBase,
                M01OwnedCr3CompositeCallGateResult.NoIndex,
                RequiredCallGateFnv1a64,
                gateHash,
                gateHash);
        }

        ulong readbackHash = ComputeFnv1a64(
            source,
            MaterializedPageByteLength);
        if (readbackHash != RequiredCombinedFnv1a64)
        {
            return CallGateFailure(
                M01OwnedCr3CompositeCallGateStatus.ByteMismatch,
                M01OwnedCr3CompositeCallGatePhase.ReadbackVerified,
                M01OwnedCr3CompositeTransitionTerminalKind
                    .Uninitialized,
                materialization.Observation,
                state,
                codeBase,
                M01OwnedCr3CompositeCallGateResult.NoIndex,
                RequiredCombinedFnv1a64,
                readbackHash,
                readbackHash);
        }

        return CallGateSuccess(
            M01OwnedCr3CompositeCallGatePhase.ReadbackVerified,
            M01OwnedCr3CompositeTransitionTerminalKind.Uninitialized,
            materialization.Observation,
            state,
            codeBase,
            sourceCount,
            readbackHash);
    }

    public static M01OwnedCr3CompositeCallGateResult
        AuditCallGateObservation(
            M01OwnedCr3CompositeCallGateResult* readbackEvidence,
            ulong readbackEvidenceCount,
            ulong readbackEvidenceCapacity,
            M01OwnedCr3CompositeTransitionRuntimeState* observedState,
            ulong observedStateCount,
            ulong observedStateCapacity,
            M01OwnedCr3CompositeCallGateObservation*
                observedObservation,
            ulong observedObservationCount,
            ulong observedObservationCapacity,
            M01OwnedCr3CompositeTransitionTerminalKind terminalKind,
            uint observedRuntimeReturnValue)
    {
        if (terminalKind
                < M01OwnedCr3CompositeTransitionTerminalKind.Returned
            || terminalKind
                > M01OwnedCr3CompositeTransitionTerminalKind.TripleFault)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus.InvalidTerminalKind,
                invalidIndex:
                    M01OwnedCr3CompositeCallGateResult.NoIndex,
                expectedValue:
                    (ulong)M01OwnedCr3CompositeTransitionTerminalKind
                        .Returned,
                observedValue: (ulong)terminalKind);
        }
        if (readbackEvidence == null)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus.MissingEvidence);
        }
        if (readbackEvidenceCount != 1
            || readbackEvidenceCapacity != 1)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus.InvalidEvidence,
                expectedValue: 1,
                observedValue:
                    readbackEvidenceCount != 1
                        ? readbackEvidenceCount
                        : readbackEvidenceCapacity);
        }
        if (observedState == null)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus
                    .MissingCompositePreparation);
        }
        if (observedStateCount != 1
            || observedStateCapacity != 1)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus
                    .InvalidCompositePreparation,
                expectedValue: 1,
                observedValue:
                    observedStateCount != 1
                        ? observedStateCount
                        : observedStateCapacity);
        }
        if (observedObservation == null)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus.MissingObservation);
        }
        if (observedObservationCount != 1)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus
                    .InvalidObservationCount,
                expectedValue: 1,
                observedValue: observedObservationCount);
        }
        if (observedObservationCapacity != 1)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus
                    .ObservationCapacityMismatch,
                expectedValue: 1,
                observedValue: observedObservationCapacity);
        }

        ulong evidenceStart = 0;
        ulong evidenceEnd = 0;
        ulong stateStart = 0;
        ulong stateEnd = 0;
        ulong observationStart = 0;
        ulong observationEnd = 0;
        if (!TryGetCallGateSpan(
                readbackEvidence,
                1,
                (ulong)sizeof(
                    M01OwnedCr3CompositeCallGateResult),
                out evidenceStart,
                out evidenceEnd))
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus.InvalidEvidence);
        }
        if (!TryGetCallGateSpan(
                observedState,
                1,
                M01OwnedCr3CompositeTransitionRuntimeState
                    .StructureSize,
                out stateStart,
                out stateEnd))
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus
                    .InvalidCompositePreparation);
        }
        if (!TryGetCallGateSpan(
                observedObservation,
                1,
                M01OwnedCr3CompositeCallGateObservation.StructureSize,
                out observationStart,
                out observationEnd)
            || (observationStart
                & (RequiredPointerAlignment - 1)) != 0)
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus
                    .MisalignedObservation);
        }
        if (RangesOverlap(
                evidenceStart,
                evidenceEnd,
                stateStart,
                stateEnd)
            || RangesOverlap(
                evidenceStart,
                evidenceEnd,
                observationStart,
                observationEnd)
            || RangesOverlap(
                stateStart,
                stateEnd,
                observationStart,
                observationEnd))
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus.ObservationAlias);
        }

        M01OwnedCr3CompositeCallGateResult readback =
            *readbackEvidence;
        if (!IsCanonicalCallGateEvidence(
                readback,
                M01OwnedCr3CompositeCallGatePhase.ReadbackVerified))
        {
            return FailCallGate(
                M01OwnedCr3CompositeCallGateStatus.InvalidEvidence);
        }
        if (stateStart != readback.PreparedState.ExpectedSelfAddress
            || observationStart
                != readback.Observation.ExpectedSelfAddress)
        {
            return CallGateFailure(
                M01OwnedCr3CompositeCallGateStatus.InvalidEvidence,
                M01OwnedCr3CompositeCallGatePhase.ObservedAudit,
                terminalKind,
                *observedObservation,
                readback.PreparedState,
                readback.CodeBase,
                M01OwnedCr3CompositeCallGateResult.NoIndex,
                readback.Observation.ExpectedSelfAddress,
                observationStart,
                readback.Fnv1a64);
        }

        M01OwnedCr3CompositeTransitionRuntimeState baseline =
            readback.PreparedState;
        M01OwnedCr3CompositeTransitionResult stateAudit =
            AuditObservedStateCore(
                &baseline,
                1,
                1,
                observedState,
                observedStateCount,
                observedStateCapacity,
                terminalKind,
                observedRuntimeReturnValue);
        M01OwnedCr3CompositeCallGateObservation observation =
            *observedObservation;
        if (terminalKind
            != M01OwnedCr3CompositeTransitionTerminalKind.Returned)
        {
            M01OwnedCr3CompositeCallGateStatus terminalStatus =
                terminalKind
                    == M01OwnedCr3CompositeTransitionTerminalKind.Timeout
                ? M01OwnedCr3CompositeCallGateStatus.Timeout
                : terminalKind
                    == M01OwnedCr3CompositeTransitionTerminalKind.Reset
                    ? M01OwnedCr3CompositeCallGateStatus.Reset
                    : M01OwnedCr3CompositeCallGateStatus.TripleFault;
            return CallGateFailure(
                terminalStatus,
                M01OwnedCr3CompositeCallGatePhase.ObservedAudit,
                terminalKind,
                observation,
                baseline,
                readback.CodeBase,
                M01OwnedCr3CompositeCallGateResult.NoIndex,
                (ulong)terminalStatus,
                (ulong)stateAudit.Status,
                readback.Fnv1a64);
        }
        if (!stateAudit.IsObservedAudit
            || stateAudit.Status
                != M01OwnedCr3CompositeTransitionStatus.Success)
        {
            return CallGateFailure(
                M01OwnedCr3CompositeCallGateStatus
                    .CompositeStateAuditFailed,
                M01OwnedCr3CompositeCallGatePhase.ObservedAudit,
                terminalKind,
                observation,
                baseline,
                readback.CodeBase,
                stateAudit.InvalidIndex,
                (ulong)M01OwnedCr3CompositeTransitionStatus.Success,
                (ulong)stateAudit.Status,
                readback.Fnv1a64);
        }

        M01OwnedCr3CompositeTransitionRuntimeState auditedState =
            stateAudit.State;
        if (auditedState.EntryRsp
                > ~0UL - GateToCompositeEntryRspDelta)
        {
            return CallGateFailure(
                M01OwnedCr3CompositeCallGateStatus
                    .InvalidGateStackTranscript,
                M01OwnedCr3CompositeCallGatePhase.ObservedAudit,
                terminalKind,
                observation,
                baseline,
                readback.CodeBase,
                StateEntryRspOffset,
                GateToCompositeEntryRspDelta,
                auditedState.EntryRsp,
                readback.Fnv1a64);
        }

        ulong gateEntryRsp =
            auditedState.EntryRsp + GateToCompositeEntryRspDelta;
        if ((gateEntryRsp & (EntryAlignment - 1))
                != CallGateRequiredEntryRspModulo
            || gateEntryRsp < CallGateRequiredLowerHeadroomBytes
            || gateEntryRsp - CallGateRequiredLowerHeadroomBytes
                < auditedState.StackStart
            || gateEntryRsp
                > ~0UL - CallGateRequiredUpperFrameBytes
            || gateEntryRsp + CallGateRequiredUpperFrameBytes
                > auditedState.StackEndExclusive)
        {
            return CallGateFailure(
                M01OwnedCr3CompositeCallGateStatus
                    .InvalidGateStackTranscript,
                M01OwnedCr3CompositeCallGatePhase.ObservedAudit,
                terminalKind,
                observation,
                baseline,
                readback.CodeBase,
                StateEntryRspOffset,
                1,
                0,
                readback.Fnv1a64);
        }

        if (!ObservationInputsMatch(
                readback.Observation,
                observation))
        {
            return CallGateFailure(
                M01OwnedCr3CompositeCallGateStatus.InvalidEvidence,
                M01OwnedCr3CompositeCallGatePhase.ObservedAudit,
                terminalKind,
                observation,
                baseline,
                readback.CodeBase,
                M01OwnedCr3CompositeCallGateResult.NoIndex,
                readback.Observation.ExpectedSelfAddress,
                observation.ExpectedSelfAddress,
                readback.Fnv1a64);
        }
        if (auditedState.OriginalRflags
            != observation.ExpectedOriginalRflags)
        {
            return CallGateFailure(
                M01OwnedCr3CompositeCallGateStatus.InvalidEvidence,
                M01OwnedCr3CompositeCallGatePhase.ObservedAudit,
                terminalKind,
                observation,
                baseline,
                readback.CodeBase,
                StateOriginalRflagsOffset,
                observation.ExpectedOriginalRflags,
                auditedState.OriginalRflags,
                readback.Fnv1a64);
        }
        if (observation.Lifecycle
            != M01OwnedCr3CompositeCallGateObservationLifecycle
                .Completed)
        {
            return CallGateFailure(
                M01OwnedCr3CompositeCallGateStatus.InvalidEvidence,
                M01OwnedCr3CompositeCallGatePhase.ObservedAudit,
                terminalKind,
                observation,
                baseline,
                readback.CodeBase,
                CallGateObservationLifecycleOffset,
                (ulong)M01OwnedCr3CompositeCallGateObservationLifecycle
                    .Completed,
                (ulong)observation.Lifecycle,
                readback.Fnv1a64);
        }
        if (observation.ObservedCompletionSignature
            != M01OwnedCr3CompositeCallGateObservation
                .CompletionSignature)
        {
            return CallGateFailure(
                M01OwnedCr3CompositeCallGateStatus
                    .CompletionSignatureMismatch,
                M01OwnedCr3CompositeCallGatePhase.ObservedAudit,
                terminalKind,
                observation,
                baseline,
                readback.CodeBase,
                CallGateObservationCompletionSignatureOffset,
                M01OwnedCr3CompositeCallGateObservation
                    .CompletionSignature,
                observation.ObservedCompletionSignature,
                readback.Fnv1a64);
        }
        if (observation.CallCount != CallGateExactCompositeCallCount)
        {
            return CallGateFailure(
                M01OwnedCr3CompositeCallGateStatus.CallCountMismatch,
                M01OwnedCr3CompositeCallGatePhase.ObservedAudit,
                terminalKind,
                observation,
                baseline,
                readback.CodeBase,
                CallGateObservationCallCountOffset,
                CallGateExactCompositeCallCount,
                observation.CallCount,
                readback.Fnv1a64);
        }
        if (observation.PostReturnObservationCount
            != CallGateExactPostReturnObservationCount)
        {
            return CallGateFailure(
                M01OwnedCr3CompositeCallGateStatus
                    .ObservationCountMismatch,
                M01OwnedCr3CompositeCallGatePhase.ObservedAudit,
                terminalKind,
                observation,
                baseline,
                readback.CodeBase,
                CallGateObservationPostReturnCountOffset,
                CallGateExactPostReturnObservationCount,
                observation.PostReturnObservationCount,
                readback.Fnv1a64);
        }
        if (observedRuntimeReturnValue
                != observation.ExpectedCompositeReturnValue
            || observation.CompositeReturnValue
                != observedRuntimeReturnValue)
        {
            return CallGateFailure(
                M01OwnedCr3CompositeCallGateStatus
                    .CompositeReturnMismatch,
                M01OwnedCr3CompositeCallGatePhase.ObservedAudit,
                terminalKind,
                observation,
                baseline,
                readback.CodeBase,
                CallGateObservationCompositeReturnOffset,
                observation.ExpectedCompositeReturnValue,
                observation.CompositeReturnValue,
                readback.Fnv1a64);
        }
        if (observation.PostReturnRflags
            != observation.ExpectedOriginalRflags)
        {
            return CallGateFailure(
                M01OwnedCr3CompositeCallGateStatus
                    .PostReturnRflagsMismatch,
                M01OwnedCr3CompositeCallGatePhase.ObservedAudit,
                terminalKind,
                observation,
                baseline,
                readback.CodeBase,
                CallGateObservationPostReturnRflagsOffset,
                observation.ExpectedOriginalRflags,
                observation.PostReturnRflags,
                readback.Fnv1a64);
        }
        bool postReturnInterruptEnabled =
            (observation.PostReturnRflags
                & RflagsInterruptEnableBit) != 0;
        if (postReturnInterruptEnabled
            != observation.ExpectedInterruptEnabled)
        {
            return CallGateFailure(
                M01OwnedCr3CompositeCallGateStatus
                    .PostReturnInterruptMismatch,
                M01OwnedCr3CompositeCallGatePhase.ObservedAudit,
                terminalKind,
                observation,
                baseline,
                readback.CodeBase,
                CallGateObservationPostReturnRflagsOffset,
                observation.ExpectedInterruptEnabled ? 1UL : 0UL,
                postReturnInterruptEnabled ? 1UL : 0UL,
                readback.Fnv1a64);
        }
        if (observation.PostReturnCr3Raw
            != observation.ExpectedFirmwareCr3Raw)
        {
            return CallGateFailure(
                M01OwnedCr3CompositeCallGateStatus.FirmwareCr3Mismatch,
                M01OwnedCr3CompositeCallGatePhase.ObservedAudit,
                terminalKind,
                observation,
                baseline,
                readback.CodeBase,
                CallGateObservationPostReturnCr3Offset,
                observation.ExpectedFirmwareCr3Raw,
                observation.PostReturnCr3Raw,
                readback.Fnv1a64);
        }
        if (observation.PostReturnCr4Raw
            != observation.ExpectedFirmwareCr4Raw)
        {
            return CallGateFailure(
                M01OwnedCr3CompositeCallGateStatus.FirmwareCr4Mismatch,
                M01OwnedCr3CompositeCallGatePhase.ObservedAudit,
                terminalKind,
                observation,
                baseline,
                readback.CodeBase,
                CallGateObservationPostReturnCr4Offset,
                observation.ExpectedFirmwareCr4Raw,
                observation.PostReturnCr4Raw,
                readback.Fnv1a64);
        }
        if (observation.BoundaryCanary
                != observation.ExpectedBoundaryCanary
            || observation.CanaryBefore
                != observation.ExpectedBoundaryCanary
            || observation.CanaryAfter
                != observation.ExpectedBoundaryCanary)
        {
            return CallGateFailure(
                M01OwnedCr3CompositeCallGateStatus
                    .BoundaryCanaryMismatch,
                M01OwnedCr3CompositeCallGatePhase.ObservedAudit,
                terminalKind,
                observation,
                baseline,
                readback.CodeBase,
                CallGateObservationCanaryAfterOffset,
                observation.ExpectedBoundaryCanary,
                observation.CanaryAfter,
                readback.Fnv1a64);
        }
        if (!observation.IsCompleted)
        {
            return CallGateFailure(
                M01OwnedCr3CompositeCallGateStatus
                    .ReservedStateNonzero,
                M01OwnedCr3CompositeCallGatePhase.ObservedAudit,
                terminalKind,
                observation,
                baseline,
                readback.CodeBase,
                CallGateObservationReservedOutput2Offset,
                0,
                1,
                readback.Fnv1a64);
        }

        return CallGateSuccess(
            M01OwnedCr3CompositeCallGatePhase.ObservedAudit,
            terminalKind,
            observation,
            baseline,
            readback.CodeBase,
            MaterializedPageByteLength,
            readback.Fnv1a64);
    }

    private static M01OwnedCr3CompositeTransitionResult
        AuditObservedStateCore(
            M01OwnedCr3CompositeTransitionRuntimeState* preparedState,
            ulong preparedStateCount,
            ulong preparedStateCapacity,
            M01OwnedCr3CompositeTransitionRuntimeState* observedState,
            ulong observedStateCount,
            ulong observedStateCapacity,
            M01OwnedCr3CompositeTransitionTerminalKind terminalKind,
            uint observedRuntimeReturnValue)
    {
        if (terminalKind
                < M01OwnedCr3CompositeTransitionTerminalKind.Returned
            || terminalKind
                > M01OwnedCr3CompositeTransitionTerminalKind.TripleFault)
        {
            return AuditResult(
                M01OwnedCr3CompositeTransitionStatus.InvalidTerminalKind,
                terminalKind,
                default,
                expectedValue:
                    (ulong)M01OwnedCr3CompositeTransitionTerminalKind
                        .Returned,
                observedValue: (ulong)terminalKind);
        }

        if (preparedState == null || observedState == null)
        {
            return AuditResult(
                M01OwnedCr3CompositeTransitionStatus.MissingObservedState,
                terminalKind,
                default);
        }

        if (preparedStateCount != 1 || observedStateCount != 1)
        {
            return AuditResult(
                M01OwnedCr3CompositeTransitionStatus
                    .InvalidObservedStateCount,
                terminalKind,
                default,
                expectedValue: 1,
                observedValue: preparedStateCount != 1
                    ? preparedStateCount
                    : observedStateCount);
        }

        if (preparedStateCapacity != 1 || observedStateCapacity != 1)
        {
            return AuditResult(
                M01OwnedCr3CompositeTransitionStatus
                    .ObservedStateCapacityTooSmall,
                terminalKind,
                default,
                expectedValue: 1,
                observedValue: preparedStateCapacity != 1
                    ? preparedStateCapacity
                    : observedStateCapacity);
        }

        ulong preparedStart = (ulong)preparedState;
        ulong observedStart = (ulong)observedState;
        const ulong StateSize =
            M01OwnedCr3CompositeTransitionRuntimeState.StructureSize;
        if ((preparedStart & (RequiredPointerAlignment - 1)) != 0
            || (observedStart & (RequiredPointerAlignment - 1)) != 0
            || preparedStart > ~0UL - StateSize
            || observedStart > ~0UL - StateSize)
        {
            return AuditResult(
                M01OwnedCr3CompositeTransitionStatus
                    .MisalignedObservedState,
                terminalKind,
                default);
        }

        if (RangesOverlap(
                preparedStart,
                preparedStart + StateSize,
                observedStart,
                observedStart + StateSize))
        {
            return AuditResult(
                M01OwnedCr3CompositeTransitionStatus.InputOutputAlias,
                terminalKind,
                default);
        }

        M01OwnedCr3CompositeTransitionRuntimeState baseline =
            *preparedState;
        M01OwnedCr3CompositeTransitionRuntimeState observed =
            *observedState;
        if (!IsCanonicalPreparedState(preparedState)
            || baseline.ExpectedSelfAddress != observedStart)
        {
            return AuditResult(
                M01OwnedCr3CompositeTransitionStatus.InvalidProbeContract,
                terminalKind,
                observed,
                expectedValue: observedStart,
                observedValue: baseline.ExpectedSelfAddress);
        }

        ulong inputMismatch = FindFirstByteMismatch(
            (byte*)preparedState,
            (byte*)observedState,
            StateFirmwareCallbackCountOffset);
        if (inputMismatch
            == M01OwnedCr3CompositeTransitionResult.NoIndex)
        {
            ulong suffixMismatch = FindFirstByteMismatch(
                (byte*)preparedState
                    + StateExpectedSelfAddressOffset,
                (byte*)observedState
                    + StateExpectedSelfAddressOffset,
                StateOriginalRflagsOffset
                    - StateExpectedSelfAddressOffset);
            if (suffixMismatch
                != M01OwnedCr3CompositeTransitionResult.NoIndex)
            {
                inputMismatch =
                    StateExpectedSelfAddressOffset + suffixMismatch;
            }
        }
        if (inputMismatch != M01OwnedCr3CompositeTransitionResult.NoIndex)
        {
            return AuditResult(
                M01OwnedCr3CompositeTransitionStatus.InvalidProbeContract,
                terminalKind,
                observed,
                invalidIndex: inputMismatch,
                expectedValue: ((byte*)preparedState)[inputMismatch],
                observedValue: ((byte*)observedState)[inputMismatch]);
        }

        if (observed.ProhibitedFirmwareCallbackCount != 0)
        {
            return AuditResult(
                M01OwnedCr3CompositeTransitionStatus
                    .FirmwareCallbackObserved,
                terminalKind,
                observed,
                expectedValue: 0,
                observedValue:
                    observed.ProhibitedFirmwareCallbackCount);
        }

        if (terminalKind
            != M01OwnedCr3CompositeTransitionTerminalKind.Returned)
        {
            if (observedRuntimeReturnValue
                != NoObservedRuntimeReturnValue)
            {
                return AuditResult(
                    M01OwnedCr3CompositeTransitionStatus
                        .DefaultRuntimeResult,
                    terminalKind,
                    observed,
                    expectedValue: NoObservedRuntimeReturnValue,
                    observedValue: observedRuntimeReturnValue);
            }

            M01OwnedCr3CompositeTransitionStatus terminalStatus =
                terminalKind
                    == M01OwnedCr3CompositeTransitionTerminalKind.Timeout
                ? M01OwnedCr3CompositeTransitionStatus.Timeout
                : terminalKind
                    == M01OwnedCr3CompositeTransitionTerminalKind.Reset
                    ? M01OwnedCr3CompositeTransitionStatus.Reset
                    : M01OwnedCr3CompositeTransitionStatus.TripleFault;
            return AuditResult(
                terminalStatus,
                terminalKind,
                observed);
        }

        if (observedRuntimeReturnValue
            == NoObservedRuntimeReturnValue)
        {
            return AuditResult(
                M01OwnedCr3CompositeTransitionStatus.DefaultRuntimeResult,
                terminalKind,
                observed,
                observedValue: observedRuntimeReturnValue);
        }

        bool observedEqualsBaseline = FindFirstByteMismatch(
            (byte*)preparedState,
            (byte*)observedState,
            StateSize)
            == M01OwnedCr3CompositeTransitionResult.NoIndex;
        if (observedEqualsBaseline)
        {
            if (IsPreCliRuntimeStatus(observedRuntimeReturnValue))
            {
                return AuditResult(
                    M01OwnedCr3CompositeTransitionStatus
                        .RuntimeReportedFailure,
                    terminalKind,
                    observed,
                    observedValue: observedRuntimeReturnValue);
            }

            return AuditResult(
                M01OwnedCr3CompositeTransitionStatus
                    .SeparateLeavesNotCompositeEvidence,
                terminalKind,
                observed,
                expectedValue:
                    (uint)M01OwnedCr3CompositeTransitionRuntimeStatus
                        .Success,
                observedValue: observedRuntimeReturnValue);
        }

        byte* observedBytes = (byte*)observedState;
        if (!observed.ReservedFieldsAreZero
            || observedBytes[StateMutationCommittedOffset] > 1
            || observedBytes[StateOriginalPgeWasSetOffset] > 1
            || observedBytes[StateOwnedRootWasLoadedOffset] > 1
            || observedBytes[StateProbeReturnedOffset] > 1)
        {
            return AuditResult(
                M01OwnedCr3CompositeTransitionStatus
                    .ReservedStateNonzero,
                terminalKind,
                observed);
        }

        uint maximumRuntimeStatus =
            (uint)M01OwnedCr3CompositeTransitionRuntimeStatus
                .PostRestoreCanaryMismatch;
        if (observedRuntimeReturnValue > maximumRuntimeStatus
            || (uint)observed.RuntimeStatus
                != observedRuntimeReturnValue)
        {
            return AuditResult(
                M01OwnedCr3CompositeTransitionStatus
                    .RuntimeReportedFailure,
                terminalKind,
                observed,
                expectedValue: (uint)observed.RuntimeStatus,
                observedValue: observedRuntimeReturnValue);
        }

        if (!IsValidProgressForRuntimeStatus(
                observed.RuntimeStatus,
                observed.ProgressCheckpoint))
        {
            return AuditResult(
                M01OwnedCr3CompositeTransitionStatus
                    .CheckpointMismatch,
                terminalKind,
                observed,
                expectedValue: (ulong)observed.RuntimeStatus,
                observedValue: (ulong)observed.ProgressCheckpoint);
        }

        if (!HasSupportedObservedMode(observed))
        {
            return AuditResult(
                M01OwnedCr3CompositeTransitionStatus
                    .ModePrerequisiteMismatch,
                terminalKind,
                observed);
        }

        bool ownedCr3WasAttempted =
            observed.OwnedRootWasLoaded
            || observed.OwnedCr3Readback != 0;
        bool firmwareCr3RestoreMismatch =
            ownedCr3WasAttempted
            && observed.FirmwareCr3Readback
                != observed.OriginalCr3Raw;

        bool cr4WasMutated =
            observed.ClearedCr4Readback != 0
            || observed.ProgressCheckpoint
                >= M01OwnedCr3CompositeTransitionCheckpoint.PgeCleared;
        bool cr4RestoreMismatch =
            cr4WasMutated
            && observed.RestoredCr4Readback
                != observed.OriginalCr4Raw;

        if (observed.OriginalRflags == 0
            || observed.RestoredRflagsIntent
                != observed.OriginalRflags)
        {
            return AuditResult(
                M01OwnedCr3CompositeTransitionStatus
                    .RflagsRestoreIntentMismatch,
                terminalKind,
                observed,
                expectedValue: observed.OriginalRflags,
                observedValue: observed.RestoredRflagsIntent);
        }

        bool exactPostRestoreCanaryFailure =
            observed.RuntimeStatus
                == M01OwnedCr3CompositeTransitionRuntimeStatus
                    .PostRestoreCanaryMismatch;
        if (observed.RuntimeStatus
                != M01OwnedCr3CompositeTransitionRuntimeStatus.Success
            && !exactPostRestoreCanaryFailure)
        {
            if (!HasValidFailureStageShape(observed))
            {
                return AuditResult(
                    M01OwnedCr3CompositeTransitionStatus
                        .CheckpointMismatch,
                    terminalKind,
                    observed,
                    expectedValue: (ulong)observed.RuntimeStatus,
                    observedValue: (ulong)observed.ProgressCheckpoint);
            }

            if (observed.Checkpoint
                    != M01OwnedCr3CompositeTransitionCheckpoint
                        .ReturnPrepared
                || observed.ExitRsp != observed.EntryRsp)
            {
                return AuditResult(
                    observed.ExitRsp != observed.EntryRsp
                        ? M01OwnedCr3CompositeTransitionStatus
                            .StackEscape
                        : M01OwnedCr3CompositeTransitionStatus
                            .CheckpointMismatch,
                    terminalKind,
                    observed,
                    expectedValue:
                        (ulong)M01OwnedCr3CompositeTransitionCheckpoint
                            .ReturnPrepared,
                    observedValue: (ulong)observed.Checkpoint);
            }

            bool failureRequiresVerifiedMutation =
                observed.RuntimeStatus
                    == M01OwnedCr3CompositeTransitionRuntimeStatus
                        .ProbeReturnMismatch
                || observed.RuntimeStatus
                    == M01OwnedCr3CompositeTransitionRuntimeStatus
                        .PageZeroPresent
                || observed.RuntimeStatus
                    == M01OwnedCr3CompositeTransitionRuntimeStatus
                        .PageZeroMalformed;
            if (failureRequiresVerifiedMutation
                && !observed.MutationCommitted)
            {
                return AuditResult(
                    M01OwnedCr3CompositeTransitionStatus
                        .CheckpointMismatch,
                    terminalKind,
                    observed);
            }

            if (observed.MutationCommitted)
            {
                bool postRestoreCanaryHasExactFailureShape =
                    observed.RuntimeStatus
                        == M01OwnedCr3CompositeTransitionRuntimeStatus
                            .FirmwareCr3ReadbackMismatch
                    ? observed.PostRestoreCanaryReadback == 0
                    : observed.PostRestoreCanaryReadback
                        == observed.ReplacementCanary;
                if (!EqualsWithMonotonicAccessedDirty(
                        observed.ProbePteAfter,
                        observed.FinalProbePteRaw)
                    || !EqualsWithMonotonicAccessedDirty(
                        observed.CommittedProbePteReadback,
                        observed.FinalProbePteRaw)
                    || observed.ProbePteAfter
                        != observed.CommittedProbePteReadback
                    || (observed.CommittedProbePteReadback
                        & (PageWritableBit | PageNoExecuteBit)) != 0
                    || observed.InvlpgTarget != observed.ProbeEntry
                    || observed.InvlpgCount != ExactInvlpgCount
                    || observed.CanaryBefore
                        != observed.ExpectedCanary
                    || observed.CanaryAfter
                        != observed.ReplacementCanary
                    || !postRestoreCanaryHasExactFailureShape)
                {
                    return AuditResult(
                        M01OwnedCr3CompositeTransitionStatus
                            .ProbePteReadbackMismatch,
                        terminalKind,
                        observed,
                        expectedValue: observed.FinalProbePteRaw,
                        observedValue:
                            observed.CommittedProbePteReadback);
                }
            }
            else if (observed.CommittedProbePteReadback != 0)
            {
                return AuditResult(
                    M01OwnedCr3CompositeTransitionStatus
                        .ProbePteReadbackMismatch,
                    terminalKind,
                    observed,
                    expectedValue: 0,
                    observedValue:
                        observed.CommittedProbePteReadback);
            }

            if (observed.RuntimeStatus
                    == M01OwnedCr3CompositeTransitionRuntimeStatus
                        .ProbeReturnMismatch
                && (!observed.ProbeReturned
                    || observed.ProbeInvocationCount
                        != ExactProbeInvocationCount))
            {
                return AuditResult(
                    M01OwnedCr3CompositeTransitionStatus.ProbeMismatch,
                    terminalKind,
                    observed);
            }

            if (observed.RuntimeStatus
                    == M01OwnedCr3CompositeTransitionRuntimeStatus
                        .PageZeroPresent
                && observed.PageZeroLevel
                    != M01OwnedCr3CompositeTransitionPageZeroLevel
                        .Present)
            {
                return AuditResult(
                    M01OwnedCr3CompositeTransitionStatus
                        .PageZeroMalformed,
                    terminalKind,
                    observed);
            }

            if (observed.RuntimeStatus
                    == M01OwnedCr3CompositeTransitionRuntimeStatus
                        .PageZeroMalformed
                && observed.PageZeroLevel
                    != M01OwnedCr3CompositeTransitionPageZeroLevel
                        .Malformed)
            {
                return AuditResult(
                    M01OwnedCr3CompositeTransitionStatus
                        .PageZeroMalformed,
                    terminalKind,
                    observed);
            }

            return AuditResult(
                MapRuntimeFailure(observed.RuntimeStatus),
                terminalKind,
                observed,
                observedValue: (uint)observed.RuntimeStatus);
        }

        if (cr4RestoreMismatch)
        {
            return AuditResult(
                M01OwnedCr3CompositeTransitionStatus
                    .Cr4RestoreReadbackMismatch,
                terminalKind,
                observed,
                expectedValue: observed.OriginalCr4Raw,
                observedValue: observed.RestoredCr4Readback);
        }

        if (firmwareCr3RestoreMismatch)
        {
            return AuditResult(
                M01OwnedCr3CompositeTransitionStatus
                    .FirmwareCr3ReadbackMismatch,
                terminalKind,
                observed,
                expectedValue: observed.OriginalCr3Raw,
                observedValue: observed.FirmwareCr3Readback);
        }

        ulong physicalPageMask =
            observed.PhysicalAddressMask & Cr3ArchitecturalBaseMask;
        ulong firmwareCr3Base =
            observed.OriginalCr3Raw & physicalPageMask;
        ulong ownedCr3Base =
            observed.OwnedCr3Raw & physicalPageMask;
        if (firmwareCr3Base == ownedCr3Base)
        {
            return AuditResult(
                M01OwnedCr3CompositeTransitionStatus.RawCr3BitLoss,
                terminalKind,
                observed,
                expectedValue: ownedCr3Base,
                observedValue: firmwareCr3Base);
        }

        if ((observed.OriginalCr3Raw
                & ~(physicalPageMask | Cr3PermittedLowBitsMask)) != 0
            || (observed.OwnedCr3Raw
                & ~(physicalPageMask | Cr3PermittedLowBitsMask)) != 0
            || observed.OwnedCr3Readback != observed.OwnedCr3Raw)
        {
            return AuditResult(
                M01OwnedCr3CompositeTransitionStatus.RawCr3BitLoss,
                terminalKind,
                observed,
                expectedValue: observed.OwnedCr3Raw,
                observedValue: observed.OwnedCr3Readback);
        }

        ulong expectedClearedCr4 =
            observed.OriginalCr4Raw & ~Cr4PageGlobalEnableBit;
        if (observed.ClearedCr4Readback != expectedClearedCr4
            || observed.OriginalPgeWasSet
                != ((observed.OriginalCr4Raw
                    & Cr4PageGlobalEnableBit) != 0))
        {
            return AuditResult(
                M01OwnedCr3CompositeTransitionStatus.RawCr4BitLoss,
                terminalKind,
                observed,
                expectedValue: expectedClearedCr4,
                observedValue: observed.ClearedCr4Readback);
        }

        if ((observed.OriginalRflags & (1UL << 1)) == 0
            || (observed.OriginalRflags & (1UL << 10)) != 0
            || !HasValidCliReadback(
                observed.OriginalRflags,
                observed.CliRflagsReadback))
        {
            return AuditResult(
                M01OwnedCr3CompositeTransitionStatus
                    .InterruptStateMismatch,
                terminalKind,
                observed,
                expectedValue: 1UL << 1,
                observedValue: observed.CliRflagsReadback);
        }

        ulong stackHeadroomEnd = TryAdd(
            observed.StackStart,
            RequiredStackHeadroomBytes);
        ulong callerFrameEnd = TryAdd(
            observed.EntryRsp,
            RequiredCallerShadowSpaceBytes + sizeof(ulong));
        if ((observed.EntryRsp & 15) != 8
            || stackHeadroomEnd == 0
            || callerFrameEnd == 0
            || observed.EntryRsp < stackHeadroomEnd
            || callerFrameEnd > observed.StackEndExclusive
            || observed.ExitRsp != observed.EntryRsp)
        {
            return AuditResult(
                M01OwnedCr3CompositeTransitionStatus.StackEscape,
                terminalKind,
                observed,
                expectedValue: observed.EntryRsp,
                observedValue: observed.ExitRsp);
        }

        if (observed.ObservedReturnAddress
                != observed.ExpectedReturnAddress
            || !ContainsAddress(
                observed.ContinuationPageStart,
                observed.ContinuationPageEndExclusive,
                observed.ObservedReturnAddress)
            || !ContainsAddress(
                observed.CompositeCodeStart,
                observed.CompositeCodeEndExclusive,
                observed.ObservedRip))
        {
            return AuditResult(
                M01OwnedCr3CompositeTransitionStatus
                    .ReturnAddressMismatch,
                terminalKind,
                observed,
                expectedValue: observed.ExpectedReturnAddress,
                observedValue: observed.ObservedReturnAddress);
        }

        if (observed.CanaryBefore != observed.ExpectedCanary
            || observed.CanaryAfter != observed.ReplacementCanary)
        {
            return AuditResult(
                M01OwnedCr3CompositeTransitionStatus.CanaryMismatch,
                terminalKind,
                observed,
                expectedValue: observed.ReplacementCanary,
                observedValue:
                    observed.CanaryAfter);
        }

        if (exactPostRestoreCanaryFailure)
        {
            if (observed.PostRestoreCanaryReadback
                == observed.ReplacementCanary)
            {
                return AuditResult(
                    M01OwnedCr3CompositeTransitionStatus
                        .RuntimeReportedFailure,
                    terminalKind,
                    observed,
                    expectedValue: 0,
                    observedValue:
                        observed.PostRestoreCanaryReadback);
            }
        }
        else if (observed.PostRestoreCanaryReadback
            != observed.ReplacementCanary)
        {
            return AuditResult(
                M01OwnedCr3CompositeTransitionStatus.CanaryMismatch,
                terminalKind,
                observed,
                expectedValue: observed.ReplacementCanary,
                observedValue:
                    observed.PostRestoreCanaryReadback);
        }

        if (!ValidateObservedProbeChain(
                observed,
                physicalPageMask))
        {
            return AuditResult(
                M01OwnedCr3CompositeTransitionStatus
                    .ProbePteAddressMismatch,
                terminalKind,
                observed);
        }

        if (!EqualsWithMonotonicAccessedDirty(
                observed.ProbePteBefore,
                observed.InitialProbePteRaw))
        {
            return AuditResult(
                M01OwnedCr3CompositeTransitionStatus
                    .ProbePteReadbackMismatch,
                terminalKind,
                observed,
                expectedValue: observed.InitialProbePteRaw,
                observedValue: observed.ProbePteBefore);
        }

        if (!EqualsWithMonotonicAccessedDirty(
                observed.ProbePteAfter,
                observed.FinalProbePteRaw)
            || !EqualsWithMonotonicAccessedDirty(
                observed.CommittedProbePteReadback,
                observed.FinalProbePteRaw)
            || observed.ProbePteAfter
                != observed.CommittedProbePteReadback
            || (observed.CommittedProbePteReadback
                & (PageWritableBit | PageNoExecuteBit)) != 0)
        {
            return AuditResult(
                M01OwnedCr3CompositeTransitionStatus
                    .ProbePteReadbackMismatch,
                terminalKind,
                observed,
                expectedValue: observed.FinalProbePteRaw,
                observedValue:
                    observed.CommittedProbePteReadback);
        }

        if (observed.InvlpgTarget != observed.ProbeEntry
            || observed.InvlpgCount != ExactInvlpgCount)
        {
            return AuditResult(
                M01OwnedCr3CompositeTransitionStatus.InvlpgMismatch,
                terminalKind,
                observed,
                expectedValue: ExactInvlpgCount,
                observedValue: observed.InvlpgCount);
        }

        if (!observed.ProbeReturned
            || observed.ProbeReturn != observed.ExpectedProbeReturn
            || observed.ProbeInvocationCount
                != ExactProbeInvocationCount)
        {
            return AuditResult(
                M01OwnedCr3CompositeTransitionStatus.ProbeMismatch,
                terminalKind,
                observed,
                expectedValue: observed.ExpectedProbeReturn,
                observedValue: observed.ProbeReturn);
        }

        M01OwnedCr3CompositeTransitionStatus pageZeroStatus =
            ValidateObservedPageZero(
                observed,
                physicalPageMask);
        if (pageZeroStatus
            != M01OwnedCr3CompositeTransitionStatus.Success)
        {
            return AuditResult(
                pageZeroStatus,
                terminalKind,
                observed);
        }

        if (!observed.MutationCommitted
            || !observed.OwnedRootWasLoaded
            || observed.Checkpoint
                != M01OwnedCr3CompositeTransitionCheckpoint
                    .ReturnPrepared)
        {
            return AuditResult(
                M01OwnedCr3CompositeTransitionStatus
                    .CheckpointMismatch,
                terminalKind,
                observed,
                expectedValue:
                    (ulong)M01OwnedCr3CompositeTransitionCheckpoint
                        .ReturnPrepared,
                observedValue: (ulong)observed.Checkpoint);
        }

        return AuditResult(
            exactPostRestoreCanaryFailure
                ? M01OwnedCr3CompositeTransitionStatus.CanaryMismatch
                : M01OwnedCr3CompositeTransitionStatus.Success,
            terminalKind,
            observed,
            expectedValue: exactPostRestoreCanaryFailure
                ? observed.ReplacementCanary
                : 0,
            observedValue: exactPostRestoreCanaryFailure
                ? observed.PostRestoreCanaryReadback
                : 0);
    }

    private static M01OwnedCr3CompositeTransitionResult AuditResult(
        M01OwnedCr3CompositeTransitionStatus status,
        M01OwnedCr3CompositeTransitionTerminalKind terminalKind,
        M01OwnedCr3CompositeTransitionRuntimeState state,
        ulong invalidIndex =
            M01OwnedCr3CompositeTransitionResult.NoIndex,
        ulong expectedValue = 0,
        ulong observedValue = 0)
    {
        return new M01OwnedCr3CompositeTransitionResult(
            status,
            M01OwnedCr3CompositeTransitionResultPhase.ObservedAudit,
            terminalKind,
            state,
            state.CompositeCodeStart,
            0,
            0,
            0,
            0,
            invalidIndex,
            expectedValue,
            observedValue,
            0);
    }

    private static ulong FindFirstByteMismatch(
        byte* expected,
        byte* observed,
        ulong count)
    {
        for (ulong index = 0; index < count; index++)
        {
            if (expected[index] != observed[index])
            {
                return index;
            }
        }

        return M01OwnedCr3CompositeTransitionResult.NoIndex;
    }

    private static bool HasSupportedObservedMode(
        M01OwnedCr3CompositeTransitionRuntimeState state)
    {
        const ulong RequiredCr0Bits = 0x8001_0001UL;
        const ulong RequiredEferBits = 0x0d00UL;
        const ulong RequiredRflagsBit = 1UL << 1;
        const ulong DirectionFlagBit = 1UL << 10;
        if (!IsSupportedPhysicalAddressMask(
                state.PhysicalAddressMask))
        {
            return false;
        }

        ulong physicalPageMask =
            state.PhysicalAddressMask & Cr3ArchitecturalBaseMask;
        return (state.OriginalCr0 & RequiredCr0Bits)
                == RequiredCr0Bits
            && (state.OriginalCr4Raw
                & (Cr4FiveLevelPagingBit
                    | Cr4PcideBit
                    | Cr4ControlFlowEnforcementBit)) == 0
            && (state.OriginalCr4Raw & (1UL << 5)) != 0
            && (state.OriginalEfer & RequiredEferBits)
                == RequiredEferBits
            && (state.OriginalRflags & RequiredRflagsBit) != 0
            && (state.OriginalRflags
                & (DirectionFlagBit | RflagsTrapFlagBit)) == 0
            && (state.OriginalCr3Raw & physicalPageMask)
                != (state.OwnedCr3Raw & physicalPageMask);
    }

    private static bool HasValidCliReadback(
        ulong originalRflags,
        ulong cliRflags)
    {
        return HasMatchingCliStableFlags(originalRflags, cliRflags)
            && (cliRflags & RflagsInterruptEnableBit) == 0;
    }

    private static bool HasMatchingCliStableFlags(
        ulong originalRflags,
        ulong cliRflags)
    {
        const ulong ArithmeticFlags =
            (1UL << 0)
            | (1UL << 2)
            | (1UL << 4)
            | (1UL << 6)
            | (1UL << 7)
            | (1UL << 11);
        const ulong MutableBeforeReadback =
            ArithmeticFlags | RflagsInterruptEnableBit;
        return (cliRflags & ~MutableBeforeReadback)
            == (originalRflags & ~MutableBeforeReadback);
    }

    private static bool IsPreCliRuntimeStatus(uint status)
    {
        return status
                >= (uint)M01OwnedCr3CompositeTransitionRuntimeStatus
                    .InvalidState
            && status
                <= (uint)M01OwnedCr3CompositeTransitionRuntimeStatus
                    .PhysicalAddressWidthUnsupported;
    }

    private static bool IsSupportedPhysicalAddressMask(ulong mask)
    {
        for (uint width = 36; width <= 52; width++)
        {
            if (mask == ((1UL << (int)width) - 1))
            {
                return true;
            }
        }

        return false;
    }

    private static bool EqualsWithMonotonicAccessedDirty(
        ulong observed,
        ulong baseline)
    {
        const ulong Ignored =
            PageAccessedBit | PageDirtyBit;
        return (observed & ~Ignored) == (baseline & ~Ignored)
            && (observed & baseline & Ignored)
                == (baseline & Ignored);
    }

    private static bool ValidateObservedParentEntry(
        ulong entry,
        ulong physicalPageMask,
        ulong tableStart,
        ulong tableEnd)
    {
        const ulong Required =
            PagePresentBit | PageWritableBit;
        ulong permitted =
            physicalPageMask | Required | PageAccessedBit;
        ulong childPage = entry & physicalPageMask;
        return (entry & ~permitted) == 0
            && (entry & Required) == Required
            && (childPage & (PageSize - 1)) == 0
            && ContainsRange(
                tableStart,
                tableEnd,
                childPage,
                TryAdd(childPage, PageSize));
    }

    private static bool ValidateObservedProbeChain(
        M01OwnedCr3CompositeTransitionRuntimeState state,
        ulong physicalPageMask)
    {
        ulong root = state.OwnedCr3Raw & physicalPageMask;
        if (!TryGetExpectedProbeHierarchyPages(
                state,
                out ulong expectedPdpt,
                out ulong expectedPd,
                out ulong expectedPt))
        {
            return false;
        }

        if (!ContainsRange(
                state.TablePageArenaStart,
                state.TablePageArenaEndExclusive,
                root,
                TryAdd(root, PageSize))
            || !ValidateObservedParentEntry(
                state.ProbePml4e,
                physicalPageMask,
                state.TablePageArenaStart,
                state.TablePageArenaEndExclusive)
            || !ValidateObservedParentEntry(
                state.ProbePdpte,
                physicalPageMask,
                state.TablePageArenaStart,
                state.TablePageArenaEndExclusive)
            || !ValidateObservedParentEntry(
                state.ProbePde,
                physicalPageMask,
                state.TablePageArenaStart,
                state.TablePageArenaEndExclusive)
            || (state.ProbePml4e & physicalPageMask) != expectedPdpt
            || (state.ProbePdpte & physicalPageMask) != expectedPd
            || (state.ProbePde & physicalPageMask) != expectedPt)
        {
            return false;
        }

        ulong pml4Slot = TryAdd(
            root,
            ((state.ProbeEntry >> 39) & 0x1ff) * sizeof(ulong));
        ulong pdptSlot = TryAdd(
            expectedPdpt,
            ((state.ProbeEntry >> 30) & 0x1ff) * sizeof(ulong));
        ulong pdSlot = TryAdd(
            expectedPd,
            ((state.ProbeEntry >> 21) & 0x1ff) * sizeof(ulong));
        ulong pteSlot = TryAdd(
            expectedPt,
            ((state.ProbeEntry >> 12) & 0x1ff) * sizeof(ulong));
        return pml4Slot != 0
            && pdptSlot != 0
            && pdSlot != 0
            && pteSlot != 0
            && ContainsRange(
                state.TablePageArenaStart,
                state.TablePageArenaEndExclusive,
                pml4Slot,
                TryAdd(pml4Slot, sizeof(ulong)))
            && ContainsRange(
                state.TablePageArenaStart,
                state.TablePageArenaEndExclusive,
                pdptSlot,
                TryAdd(pdptSlot, sizeof(ulong)))
            && ContainsRange(
                state.TablePageArenaStart,
                state.TablePageArenaEndExclusive,
                pdSlot,
                TryAdd(pdSlot, sizeof(ulong)))
            && pteSlot == state.ExpectedProbePteAddress;
    }

    private static bool TryGetExpectedProbeHierarchyPages(
        M01OwnedCr3CompositeTransitionRuntimeState state,
        out ulong pdptPage,
        out ulong pdPage,
        out ulong ptPage)
    {
        pdptPage = 0;
        pdPage = 0;
        ptPage = 0;
        if ((state.ExpectedProbeHierarchyIdentity
                & ~ProbeHierarchyIdentityPermittedMask) != 0
            || state.ExpectedCriticalMappingPageCount == 0
            || state.ExpectedCriticalMappingPageCount
                > MaximumCriticalMappingPageCount)
        {
            return false;
        }

        ulong pdptOffset =
            state.ExpectedProbePdptPageOrdinal * PageSize;
        ulong pdOffset =
            state.ExpectedProbePdPageOrdinal * PageSize;
        ulong ptOffset =
            state.ExpectedProbePtPageOrdinal * PageSize;
        pdptPage = TryAdd(state.TablePageArenaStart, pdptOffset);
        pdPage = TryAdd(state.TablePageArenaStart, pdOffset);
        ptPage = TryAdd(state.TablePageArenaStart, ptOffset);
        return pdptPage != 0
            && pdPage != 0
            && ptPage != 0
            && ContainsRange(
                state.TablePageArenaStart,
                state.TablePageArenaEndExclusive,
                pdptPage,
                TryAdd(pdptPage, PageSize))
            && ContainsRange(
                state.TablePageArenaStart,
                state.TablePageArenaEndExclusive,
                pdPage,
                TryAdd(pdPage, PageSize))
            && ContainsRange(
                state.TablePageArenaStart,
                state.TablePageArenaEndExclusive,
                ptPage,
                TryAdd(ptPage, PageSize))
            && (state.ExpectedProbePteAddress & ~(PageSize - 1))
                == ptPage;
    }

    private static bool HasExactMalformedProbeHierarchy(
        M01OwnedCr3CompositeTransitionRuntimeState state,
        ulong physicalPageMask)
    {
        if (!TryGetExpectedProbeHierarchyPages(
                state,
                out ulong expectedPdpt,
                out ulong expectedPd,
                out ulong expectedPt))
        {
            return false;
        }

        bool pml4Valid = ValidateObservedParentEntry(
            state.ProbePml4e,
            physicalPageMask,
            state.TablePageArenaStart,
            state.TablePageArenaEndExclusive);
        if (!pml4Valid)
        {
            return state.ProbePdpte == 0
                && state.ProbePde == 0;
        }
        if ((state.ProbePml4e & physicalPageMask) != expectedPdpt)
        {
            return false;
        }

        bool pdptValid = ValidateObservedParentEntry(
            state.ProbePdpte,
            physicalPageMask,
            state.TablePageArenaStart,
            state.TablePageArenaEndExclusive);
        if (!pdptValid)
        {
            return state.ProbePde == 0;
        }
        if ((state.ProbePdpte & physicalPageMask) != expectedPd)
        {
            return false;
        }

        bool pdValid = ValidateObservedParentEntry(
            state.ProbePde,
            physicalPageMask,
            state.TablePageArenaStart,
            state.TablePageArenaEndExclusive);
        return !pdValid;
    }

    private static bool HasExactProbeHierarchyAddressMismatch(
        M01OwnedCr3CompositeTransitionRuntimeState state,
        ulong physicalPageMask)
    {
        if (!TryGetExpectedProbeHierarchyPages(
                state,
                out ulong expectedPdpt,
                out ulong expectedPd,
                out ulong expectedPt)
            || !ValidateObservedParentEntry(
                state.ProbePml4e,
                physicalPageMask,
                state.TablePageArenaStart,
                state.TablePageArenaEndExclusive))
        {
            return false;
        }

        if ((state.ProbePml4e & physicalPageMask) != expectedPdpt)
        {
            return state.ProbePdpte == 0
                && state.ProbePde == 0;
        }
        if (!ValidateObservedParentEntry(
                state.ProbePdpte,
                physicalPageMask,
                state.TablePageArenaStart,
                state.TablePageArenaEndExclusive))
        {
            return false;
        }
        if ((state.ProbePdpte & physicalPageMask) != expectedPd)
        {
            return state.ProbePde == 0;
        }
        if (!ValidateObservedParentEntry(
                state.ProbePde,
                physicalPageMask,
                state.TablePageArenaStart,
                state.TablePageArenaEndExclusive))
        {
            return false;
        }
        if ((state.ProbePde & physicalPageMask) != expectedPt)
        {
            return true;
        }

        ulong derivedPte = TryAdd(
            expectedPt,
            ((state.ProbeEntry >> 12) & 0x1ff) * sizeof(ulong));
        return derivedPte != 0
            && derivedPte != state.ExpectedProbePteAddress;
    }

    private static bool HasExactMalformedPageZero(
        M01OwnedCr3CompositeTransitionRuntimeState state,
        ulong physicalPageMask)
    {
        if (state.PageZeroLevel
            != M01OwnedCr3CompositeTransitionPageZeroLevel.Malformed)
        {
            return false;
        }

        bool pml4Valid = ValidateObservedParentEntry(
            state.PageZeroPml4e,
            physicalPageMask,
            state.TablePageArenaStart,
            state.TablePageArenaEndExclusive);
        if (!pml4Valid)
        {
            return state.PageZeroPml4e != 0
                && state.PageZeroPdpte == 0
                && state.PageZeroPde == 0
                && state.PageZeroPte == 0;
        }

        bool pdptValid = ValidateObservedParentEntry(
            state.PageZeroPdpte,
            physicalPageMask,
            state.TablePageArenaStart,
            state.TablePageArenaEndExclusive);
        if (!pdptValid)
        {
            return state.PageZeroPdpte != 0
                && state.PageZeroPde == 0
                && state.PageZeroPte == 0;
        }

        bool pdValid = ValidateObservedParentEntry(
            state.PageZeroPde,
            physicalPageMask,
            state.TablePageArenaStart,
            state.TablePageArenaEndExclusive);
        if (!pdValid)
        {
            return state.PageZeroPde != 0
                && state.PageZeroPte == 0;
        }

        return state.PageZeroPte != 0
            && (state.PageZeroPte & PagePresentBit) == 0;
    }

    private static M01OwnedCr3CompositeTransitionStatus
        ValidateObservedPageZero(
            M01OwnedCr3CompositeTransitionRuntimeState state,
            ulong physicalPageMask)
    {
        bool pml4Valid = ValidateObservedParentEntry(
            state.PageZeroPml4e,
            physicalPageMask,
            state.TablePageArenaStart,
            state.TablePageArenaEndExclusive);
        bool pdptValid = ValidateObservedParentEntry(
            state.PageZeroPdpte,
            physicalPageMask,
            state.TablePageArenaStart,
            state.TablePageArenaEndExclusive);
        bool pdValid = ValidateObservedParentEntry(
            state.PageZeroPde,
            physicalPageMask,
            state.TablePageArenaStart,
            state.TablePageArenaEndExclusive);

        switch (state.PageZeroLevel)
        {
            case M01OwnedCr3CompositeTransitionPageZeroLevel.AbsentAtPml4:
                return state.PageZeroPml4e == 0
                    && state.PageZeroPdpte == 0
                    && state.PageZeroPde == 0
                    && state.PageZeroPte == 0
                    ? M01OwnedCr3CompositeTransitionStatus.Success
                    : M01OwnedCr3CompositeTransitionStatus
                        .PageZeroMalformed;
            case M01OwnedCr3CompositeTransitionPageZeroLevel.AbsentAtPdpt:
                return pml4Valid
                    && state.PageZeroPdpte == 0
                    && state.PageZeroPde == 0
                    && state.PageZeroPte == 0
                    ? M01OwnedCr3CompositeTransitionStatus.Success
                    : M01OwnedCr3CompositeTransitionStatus
                        .PageZeroMalformed;
            case M01OwnedCr3CompositeTransitionPageZeroLevel.AbsentAtPd:
                return pml4Valid
                    && pdptValid
                    && state.PageZeroPde == 0
                    && state.PageZeroPte == 0
                    ? M01OwnedCr3CompositeTransitionStatus.Success
                    : M01OwnedCr3CompositeTransitionStatus
                        .PageZeroMalformed;
            case M01OwnedCr3CompositeTransitionPageZeroLevel.AbsentAtPt:
                return pml4Valid
                    && pdptValid
                    && pdValid
                    && state.PageZeroPte == 0
                    ? M01OwnedCr3CompositeTransitionStatus.Success
                    : M01OwnedCr3CompositeTransitionStatus
                        .PageZeroMalformed;
            case M01OwnedCr3CompositeTransitionPageZeroLevel.Present:
                return pml4Valid
                    && pdptValid
                    && pdValid
                    && (state.PageZeroPte & PagePresentBit) != 0
                    ? M01OwnedCr3CompositeTransitionStatus.PageZeroPresent
                    : M01OwnedCr3CompositeTransitionStatus
                        .PageZeroMalformed;
            case M01OwnedCr3CompositeTransitionPageZeroLevel.Malformed:
                return M01OwnedCr3CompositeTransitionStatus
                    .PageZeroMalformed;
            default:
                return M01OwnedCr3CompositeTransitionStatus
                    .PageZeroMalformed;
        }
    }

    private static M01OwnedCr3CompositeTransitionStatus
        MapRuntimeFailure(
            M01OwnedCr3CompositeTransitionRuntimeStatus status)
    {
        switch (status)
        {
            case M01OwnedCr3CompositeTransitionRuntimeStatus.InvalidMode:
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .PhysicalAddressWidthUnsupported:
                return M01OwnedCr3CompositeTransitionStatus
                    .ModePrerequisiteMismatch;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .FirmwareCallbackForbidden:
                return M01OwnedCr3CompositeTransitionStatus
                    .FirmwareCallbackObserved;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .InterruptDisableReadbackMismatch:
                return M01OwnedCr3CompositeTransitionStatus
                    .InterruptStateMismatch;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .PgeClearReadbackMismatch:
                return M01OwnedCr3CompositeTransitionStatus.RawCr4BitLoss;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .Cr4RestoreReadbackMismatch:
                return M01OwnedCr3CompositeTransitionStatus
                    .Cr4RestoreReadbackMismatch;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .OwnedCr3ReadbackMismatch:
                return M01OwnedCr3CompositeTransitionStatus.RawCr3BitLoss;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .CanaryBeforeMismatch:
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .CanaryAfterMismatch:
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .PostRestoreCanaryMismatch:
                return M01OwnedCr3CompositeTransitionStatus.CanaryMismatch;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .ProbeHierarchyMalformed:
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .ProbePteAddressMismatch:
                return M01OwnedCr3CompositeTransitionStatus
                    .ProbePteAddressMismatch;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .ProbePteBeforeMismatch:
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .ProbePteAfterMismatch:
                return M01OwnedCr3CompositeTransitionStatus
                    .ProbePteReadbackMismatch;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .ProbeReturnMismatch:
                return M01OwnedCr3CompositeTransitionStatus.ProbeMismatch;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .PageZeroPresent:
                return M01OwnedCr3CompositeTransitionStatus.PageZeroPresent;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .PageZeroMalformed:
                return M01OwnedCr3CompositeTransitionStatus
                    .PageZeroMalformed;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .FirmwareCr3ReadbackMismatch:
                return M01OwnedCr3CompositeTransitionStatus
                    .FirmwareCr3ReadbackMismatch;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .RflagsRestoreReadbackMismatch:
                return M01OwnedCr3CompositeTransitionStatus
                    .RflagsRestoreIntentMismatch;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .RspRestoreMismatch:
                return M01OwnedCr3CompositeTransitionStatus.StackEscape;
            default:
                return M01OwnedCr3CompositeTransitionStatus
                    .RuntimeReportedFailure;
        }
    }

    private static bool IsValidProgressForRuntimeStatus(
        M01OwnedCr3CompositeTransitionRuntimeStatus status,
        M01OwnedCr3CompositeTransitionCheckpoint progress)
    {
        switch (status)
        {
            case M01OwnedCr3CompositeTransitionRuntimeStatus.Success:
                return progress
                    == M01OwnedCr3CompositeTransitionCheckpoint
                        .PageZeroAbsent;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .InterruptDisableReadbackMismatch:
                return progress
                    == M01OwnedCr3CompositeTransitionCheckpoint
                        .Uninitialized;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .PgeClearReadbackMismatch:
                return progress
                    == M01OwnedCr3CompositeTransitionCheckpoint
                        .InterruptsDisabled;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .OwnedCr3ReadbackMismatch:
                return progress
                    == M01OwnedCr3CompositeTransitionCheckpoint.PgeCleared;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .CanaryBeforeMismatch:
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .CanaryAfterMismatch:
                return progress
                    == M01OwnedCr3CompositeTransitionCheckpoint
                        .OwnedCr3Loaded;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .ProbeHierarchyMalformed:
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .ProbePteAddressMismatch:
                return progress
                    == M01OwnedCr3CompositeTransitionCheckpoint
                        .CanaryUpdated;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .ProbePteBeforeMismatch:
                return progress
                    == M01OwnedCr3CompositeTransitionCheckpoint
                        .ProbePteLocated;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .ProbePteAfterMismatch:
                return progress
                    == M01OwnedCr3CompositeTransitionCheckpoint
                        .ProbeInvalidated;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .ProbeReturnMismatch:
                return progress
                    == M01OwnedCr3CompositeTransitionCheckpoint
                        .ProbePteVerified;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .PageZeroPresent:
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .PageZeroMalformed:
                return progress
                    == M01OwnedCr3CompositeTransitionCheckpoint
                        .ProbeReturned;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .FirmwareCr3ReadbackMismatch:
                return progress
                    >= M01OwnedCr3CompositeTransitionCheckpoint.PgeCleared
                    && progress
                    <= M01OwnedCr3CompositeTransitionCheckpoint
                        .PageZeroAbsent;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .Cr4RestoreReadbackMismatch:
                return progress
                    >= M01OwnedCr3CompositeTransitionCheckpoint
                        .InterruptsDisabled
                    && progress
                    <= M01OwnedCr3CompositeTransitionCheckpoint
                        .PageZeroAbsent;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .RspRestoreMismatch:
                return progress
                    <= M01OwnedCr3CompositeTransitionCheckpoint
                        .PageZeroAbsent;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .RflagsRestoreReadbackMismatch:
                return false;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .PostRestoreCanaryMismatch:
                return progress
                    == M01OwnedCr3CompositeTransitionCheckpoint
                        .PageZeroAbsent;
            default:
                return false;
        }
    }

    private static bool HasValidFailureStageShape(
        M01OwnedCr3CompositeTransitionRuntimeState state)
    {
        if (!IsSupportedPhysicalAddressMask(
                state.PhysicalAddressMask))
        {
            return false;
        }

        ulong physicalPageMask =
            state.PhysicalAddressMask & Cr3ArchitecturalBaseMask;
        ulong expectedClearedCr4 =
            state.OriginalCr4Raw & ~Cr4PageGlobalEnableBit;
        bool cliSucceeded =
            HasValidCliReadback(
                state.OriginalRflags,
                state.CliRflagsReadback);
        bool pgeSucceeded =
            cliSucceeded
            && state.ClearedCr4Readback == expectedClearedCr4
            && state.OriginalPgeWasSet
                == ((state.OriginalCr4Raw
                    & Cr4PageGlobalEnableBit) != 0);
        bool ownedRootSucceeded =
            pgeSucceeded
            && state.OwnedCr3Readback == state.OwnedCr3Raw
            && state.OwnedRootWasLoaded;
        bool canarySucceeded =
            ownedRootSucceeded
            && state.CanaryBefore == state.ExpectedCanary
            && state.CanaryAfter == state.ReplacementCanary;
        bool probeChainSucceeded =
            canarySucceeded
            && ValidateObservedProbeChain(state, physicalPageMask);
        bool probePteBeforeSucceeded =
            probeChainSucceeded
            && EqualsWithMonotonicAccessedDirty(
                state.ProbePteBefore,
                state.InitialProbePteRaw);
        bool invalidationSucceeded =
            probePteBeforeSucceeded
            && state.InvlpgTarget == state.ProbeEntry
            && state.InvlpgCount == ExactInvlpgCount;
        bool probePteCommitSucceeded =
            invalidationSucceeded
            && EqualsWithMonotonicAccessedDirty(
                state.ProbePteAfter,
                state.FinalProbePteRaw)
            && EqualsWithMonotonicAccessedDirty(
                state.CommittedProbePteReadback,
                state.FinalProbePteRaw)
            && state.ProbePteAfter
                == state.CommittedProbePteReadback
            && (state.CommittedProbePteReadback
                & (PageWritableBit | PageNoExecuteBit)) == 0
            && state.MutationCommitted;
        bool probeInvocationSucceeded =
            probePteCommitSucceeded
            && state.ProbeReturned
            && state.ProbeInvocationCount
                == ExactProbeInvocationCount
            && state.ProbeReturn == state.ExpectedProbeReturn;
        M01OwnedCr3CompositeTransitionStatus pageZeroStatus =
            ValidateObservedPageZero(state, physicalPageMask);
        bool pageZeroAbsent =
            probeInvocationSucceeded
            && pageZeroStatus
                == M01OwnedCr3CompositeTransitionStatus.Success;

        bool allPageZeroOutputsZero =
            state.PageZeroPml4e == 0
            && state.PageZeroPdpte == 0
            && state.PageZeroPde == 0
            && state.PageZeroPte == 0
            && state.PageZeroLevel
                == M01OwnedCr3CompositeTransitionPageZeroLevel
                    .Uninitialized;
        bool allProbeInvocationOutputsZero =
            state.ProbeReturn == 0
            && state.ProbeInvocationCount == 0
            && !state.ProbeReturned
            && allPageZeroOutputsZero;
        bool allMutationOutputsZero =
            state.ProbePteAfter == 0
            && state.InvlpgTarget == 0
            && state.InvlpgCount == 0
            && state.CommittedProbePteReadback == 0
            && !state.MutationCommitted
            && allProbeInvocationOutputsZero;
        bool allProbeOutputsZero =
            state.ProbePml4e == 0
            && state.ProbePdpte == 0
            && state.ProbePde == 0
            && state.ProbePteBefore == 0
            && allMutationOutputsZero;
        bool noCanaryOrProbe =
            state.CanaryBefore == 0
            && state.CanaryAfter == 0
            && allProbeOutputsZero;

        bool interruptDisableFailure =
            (state.CliRflagsReadback
                & RflagsInterruptEnableBit) != 0
            && HasMatchingCliStableFlags(
                state.OriginalRflags,
                state.CliRflagsReadback)
            && state.ClearedCr4Readback == 0
            && state.OwnedCr3Readback == 0
            && !state.OriginalPgeWasSet
            && !state.OwnedRootWasLoaded
            && noCanaryOrProbe;
        bool pgeClearFailure =
            cliSucceeded
            && state.ClearedCr4Readback != expectedClearedCr4
            && state.OwnedCr3Readback == 0
            && !state.OriginalPgeWasSet
            && !state.OwnedRootWasLoaded
            && noCanaryOrProbe;
        bool ownedCr3Failure =
            pgeSucceeded
            && state.OwnedCr3Readback != state.OwnedCr3Raw
            && !state.OwnedRootWasLoaded
            && noCanaryOrProbe;
        bool canaryBeforeFailure =
            ownedRootSucceeded
            && state.CanaryBefore != state.ExpectedCanary
            && state.CanaryAfter == 0
            && allProbeOutputsZero;
        bool canaryAfterFailure =
            ownedRootSucceeded
            && state.CanaryBefore == state.ExpectedCanary
            && state.CanaryAfter != state.ReplacementCanary
            && allProbeOutputsZero;
        bool hierarchyFailure =
            canarySucceeded
            && HasExactMalformedProbeHierarchy(
                state,
                physicalPageMask)
            && state.ProbePteBefore == 0
            && allMutationOutputsZero;
        bool hierarchyAddressFailure =
            canarySucceeded
            && HasExactProbeHierarchyAddressMismatch(
                state,
                physicalPageMask)
            && state.ProbePteBefore == 0
            && allMutationOutputsZero;
        bool probePteBeforeFailure =
            probeChainSucceeded
            && !EqualsWithMonotonicAccessedDirty(
                state.ProbePteBefore,
                state.InitialProbePteRaw)
            && allMutationOutputsZero;
        ulong expectedProbePteAfter =
            state.ProbePteBefore
                & ~(PageWritableBit | PageNoExecuteBit);
        bool probePteAfterFailure =
            probePteBeforeSucceeded
            && state.InvlpgTarget == state.ProbeEntry
            && state.InvlpgCount == ExactInvlpgCount
            && state.ProbePteAfter != expectedProbePteAfter
            && state.CommittedProbePteReadback == 0
            && !state.MutationCommitted
            && allProbeInvocationOutputsZero;
        bool probeReturnFailure =
            probePteCommitSucceeded
            && state.ProbeReturned
            && state.ProbeInvocationCount
                == ExactProbeInvocationCount
            && state.ProbeReturn != state.ExpectedProbeReturn
            && allPageZeroOutputsZero;
        bool pageZeroPresentFailure =
            probeInvocationSucceeded
            && pageZeroStatus
                == M01OwnedCr3CompositeTransitionStatus.PageZeroPresent;
        bool pageZeroMalformedFailure =
            probeInvocationSucceeded
            && HasExactMalformedPageZero(
                state,
                physicalPageMask);

        bool progressShape;
        switch (state.ProgressCheckpoint)
        {
            case M01OwnedCr3CompositeTransitionCheckpoint.Uninitialized:
                progressShape = interruptDisableFailure;
                break;
            case M01OwnedCr3CompositeTransitionCheckpoint
                .InterruptsDisabled:
                progressShape = pgeClearFailure;
                break;
            case M01OwnedCr3CompositeTransitionCheckpoint.PgeCleared:
                progressShape = ownedCr3Failure;
                break;
            case M01OwnedCr3CompositeTransitionCheckpoint.OwnedCr3Loaded:
                progressShape =
                    canaryBeforeFailure || canaryAfterFailure;
                break;
            case M01OwnedCr3CompositeTransitionCheckpoint.CanaryUpdated:
                progressShape =
                    hierarchyFailure || hierarchyAddressFailure;
                break;
            case M01OwnedCr3CompositeTransitionCheckpoint.ProbePteLocated:
                progressShape = probePteBeforeFailure;
                break;
            case M01OwnedCr3CompositeTransitionCheckpoint.ProbeInvalidated:
                progressShape = probePteAfterFailure;
                break;
            case M01OwnedCr3CompositeTransitionCheckpoint.ProbePteVerified:
                progressShape = probeReturnFailure;
                break;
            case M01OwnedCr3CompositeTransitionCheckpoint.ProbeReturned:
                progressShape =
                    pageZeroPresentFailure
                    || pageZeroMalformedFailure;
                break;
            case M01OwnedCr3CompositeTransitionCheckpoint.PageZeroAbsent:
                progressShape = pageZeroAbsent;
                break;
            default:
                progressShape = false;
                break;
        }

        switch (state.RuntimeStatus)
        {
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .InterruptDisableReadbackMismatch:
                return interruptDisableFailure
                    && state.FirmwareCr3Readback == 0
                    && state.RestoredCr4Readback == 0
                    && state.PostRestoreCanaryReadback == 0;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .PgeClearReadbackMismatch:
                return pgeClearFailure
                    && state.FirmwareCr3Readback == 0
                    && state.RestoredCr4Readback
                        == state.OriginalCr4Raw
                    && state.PostRestoreCanaryReadback == 0;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .OwnedCr3ReadbackMismatch:
                return ownedCr3Failure
                    && state.FirmwareCr3Readback
                        == state.OriginalCr3Raw
                    && state.RestoredCr4Readback
                        == state.OriginalCr4Raw
                    && state.PostRestoreCanaryReadback
                        == state.ExpectedCanary;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .CanaryBeforeMismatch:
                return canaryBeforeFailure
                    && state.FirmwareCr3Readback
                        == state.OriginalCr3Raw
                    && state.RestoredCr4Readback
                        == state.OriginalCr4Raw
                    && state.PostRestoreCanaryReadback
                        == state.CanaryBefore;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .CanaryAfterMismatch:
                return canaryAfterFailure
                    && state.FirmwareCr3Readback
                        == state.OriginalCr3Raw
                    && state.RestoredCr4Readback
                        == state.OriginalCr4Raw
                    && state.PostRestoreCanaryReadback
                        == state.CanaryAfter;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .ProbeHierarchyMalformed:
                return hierarchyFailure
                    && state.FirmwareCr3Readback
                        == state.OriginalCr3Raw
                    && state.RestoredCr4Readback
                        == state.OriginalCr4Raw
                    && state.PostRestoreCanaryReadback
                        == state.ReplacementCanary;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .ProbePteAddressMismatch:
                return hierarchyAddressFailure
                    && state.FirmwareCr3Readback
                        == state.OriginalCr3Raw
                    && state.RestoredCr4Readback
                        == state.OriginalCr4Raw
                    && state.PostRestoreCanaryReadback
                        == state.ReplacementCanary;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .ProbePteBeforeMismatch:
                return probePteBeforeFailure
                    && state.FirmwareCr3Readback
                        == state.OriginalCr3Raw
                    && state.RestoredCr4Readback
                        == state.OriginalCr4Raw
                    && state.PostRestoreCanaryReadback
                        == state.ReplacementCanary;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .ProbePteAfterMismatch:
                return probePteAfterFailure
                    && state.FirmwareCr3Readback
                        == state.OriginalCr3Raw
                    && state.RestoredCr4Readback
                        == state.OriginalCr4Raw
                    && state.PostRestoreCanaryReadback
                        == state.ReplacementCanary;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .ProbeReturnMismatch:
                return probeReturnFailure
                    && state.FirmwareCr3Readback
                        == state.OriginalCr3Raw
                    && state.RestoredCr4Readback
                        == state.OriginalCr4Raw
                    && state.PostRestoreCanaryReadback
                        == state.ReplacementCanary;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .PageZeroPresent:
                return pageZeroPresentFailure
                    && state.FirmwareCr3Readback
                        == state.OriginalCr3Raw
                    && state.RestoredCr4Readback
                        == state.OriginalCr4Raw
                    && state.PostRestoreCanaryReadback
                        == state.ReplacementCanary;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .PageZeroMalformed:
                return pageZeroMalformedFailure
                    && state.FirmwareCr3Readback
                        == state.OriginalCr3Raw
                    && state.RestoredCr4Readback
                        == state.OriginalCr4Raw
                    && state.PostRestoreCanaryReadback
                        == state.ReplacementCanary;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .FirmwareCr3ReadbackMismatch:
                return progressShape
                    && HasExactCleanupForProgress(
                        state,
                        firmwareMustMismatch: true,
                        firmwareMayMismatch: true,
                        cr4MustMismatch: false,
                        cr4MayMismatch: false)
                    && state.ExitRsp == state.EntryRsp;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .Cr4RestoreReadbackMismatch:
                return progressShape
                    && HasExactCleanupForProgress(
                        state,
                        firmwareMustMismatch: false,
                        firmwareMayMismatch: true,
                        cr4MustMismatch: true,
                        cr4MayMismatch: true)
                    && state.ExitRsp == state.EntryRsp;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .RspRestoreMismatch:
                return progressShape
                    && HasExactCleanupForProgress(
                        state,
                        firmwareMustMismatch: false,
                        firmwareMayMismatch: true,
                        cr4MustMismatch: false,
                        cr4MayMismatch: true)
                    && state.ExitRsp != state.EntryRsp;
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .RflagsRestoreReadbackMismatch:
            case M01OwnedCr3CompositeTransitionRuntimeStatus
                .PostRestoreCanaryMismatch:
                // RFLAGS cannot be read after the final POPFQ without adding
                // post-restore instructions. The post-restore canary status
                // is instead routed through the complete success-stage audit.
                return false;
            default:
                return false;
        }
    }

    private static bool HasExactCleanupForProgress(
        M01OwnedCr3CompositeTransitionRuntimeState state,
        bool firmwareMustMismatch,
        bool firmwareMayMismatch,
        bool cr4MustMismatch,
        bool cr4MayMismatch)
    {
        bool cr3WasAttempted =
            state.ProgressCheckpoint
                >= M01OwnedCr3CompositeTransitionCheckpoint.PgeCleared;
        bool firmwareMismatch =
            state.FirmwareCr3Readback != state.OriginalCr3Raw;
        if (!cr3WasAttempted)
        {
            if (firmwareMustMismatch
                || state.FirmwareCr3Readback != 0
                || state.PostRestoreCanaryReadback != 0)
            {
                return false;
            }
        }
        else
        {
            if ((firmwareMustMismatch && !firmwareMismatch)
                || (!firmwareMayMismatch && firmwareMismatch))
            {
                return false;
            }

            if (firmwareMismatch)
            {
                if (state.PostRestoreCanaryReadback != 0)
                {
                    return false;
                }
            }
            else
            {
                ulong expectedPostRestoreCanary;
                if (state.ProgressCheckpoint
                    == M01OwnedCr3CompositeTransitionCheckpoint.PgeCleared)
                {
                    expectedPostRestoreCanary = state.ExpectedCanary;
                }
                else if (state.ProgressCheckpoint
                    == M01OwnedCr3CompositeTransitionCheckpoint
                        .OwnedCr3Loaded)
                {
                    expectedPostRestoreCanary =
                        state.CanaryBefore != state.ExpectedCanary
                            ? state.CanaryBefore
                            : state.CanaryAfter;
                }
                else
                {
                    expectedPostRestoreCanary =
                        state.ReplacementCanary;
                }

                if (state.PostRestoreCanaryReadback
                    != expectedPostRestoreCanary)
                {
                    return false;
                }
            }
        }

        bool cr4WasAttempted =
            state.ProgressCheckpoint
                >= M01OwnedCr3CompositeTransitionCheckpoint
                    .InterruptsDisabled;
        bool cr4Mismatch =
            state.RestoredCr4Readback != state.OriginalCr4Raw;
        if (!cr4WasAttempted)
        {
            return !cr4MustMismatch
                && state.RestoredCr4Readback == 0;
        }

        return (!cr4MustMismatch || cr4Mismatch)
            && (cr4MayMismatch || !cr4Mismatch);
    }

    private enum PlanWriterMode : byte
    {
        Measure = 0,
        Emit = 1,
        Compare = 2
    }

    private enum PlanRegister : byte
    {
        Rax = 0,
        Rcx = 1,
        Rdx = 2,
        Rbx = 3,
        Rsp = 4,
        Rbp = 5,
        Rsi = 6,
        Rdi = 7,
        R8 = 8,
        R9 = 9,
        R10 = 10,
        R11 = 11,
        R12 = 12
    }

    private enum PlanCondition : byte
    {
        Below = 0x82,
        AboveOrEqual = 0x83,
        Equal = 0x84,
        NotEqual = 0x85,
        BelowOrEqual = 0x86,
        Above = 0x87
    }

    private struct PlanWriter
    {
        internal PlanWriterMode Mode;
        internal byte* Bytes;
        internal ulong ByteCapacity;
        internal M01OwnedCr3CompositeTransitionSemanticRecord*
            Semantics;
        internal ulong SemanticCapacity;
        internal ulong Position;
        internal ulong InstructionCount;
        internal ulong SemanticCount;
        internal ulong SemanticStart;
        internal M01OwnedCr3CompositeTransitionOperation
            SemanticOperation;
        internal M01OwnedCr3CompositeTransitionRecoveryClass
            SemanticRecovery;
        internal bool SemanticOpen;
        internal bool Succeeded;
        internal bool SemanticMismatch;
        internal bool SkipPlaceholderComparison;
        internal ulong InvalidIndex;
        internal ulong ExpectedValue;
        internal ulong ObservedValue;
        internal ulong PreCliFailureTarget;

        internal PlanWriter(
            PlanWriterMode mode,
            byte* bytes,
            ulong byteCapacity,
            M01OwnedCr3CompositeTransitionSemanticRecord* semantics,
            ulong semanticCapacity)
        {
            Mode = mode;
            Bytes = bytes;
            ByteCapacity = byteCapacity;
            Semantics = semantics;
            SemanticCapacity = semanticCapacity;
            Position = 0;
            InstructionCount = 0;
            SemanticCount = 0;
            SemanticStart = 0;
            SemanticOperation =
                M01OwnedCr3CompositeTransitionOperation.Uninitialized;
            SemanticRecovery =
                M01OwnedCr3CompositeTransitionRecoveryClass
                    .Uninitialized;
            SemanticOpen = false;
            Succeeded = true;
            SemanticMismatch = false;
            SkipPlaceholderComparison = false;
            InvalidIndex =
                M01OwnedCr3CompositeTransitionResult.NoIndex;
            ExpectedValue = 0;
            ObservedValue = 0;
            PreCliFailureTarget =
                M01OwnedCr3CompositeTransitionResult.NoIndex;
        }

        internal void Instruction()
        {
            if (Succeeded)
            {
                InstructionCount++;
            }
        }

        internal void RequirePosition(ulong expected)
        {
            if (Succeeded && Position != expected)
            {
                Fail(
                    Position,
                    expected,
                    Position,
                    semantic: false);
            }
        }

        internal void Byte(byte value)
        {
            if (!Succeeded)
            {
                return;
            }

            if (Position >= ByteCapacity)
            {
                Fail(Position, value, 0, semantic: false);
                return;
            }

            if (Mode == PlanWriterMode.Emit)
            {
                Bytes[Position] = value;
            }
            else if (Mode == PlanWriterMode.Compare
                && !SkipPlaceholderComparison
                && Bytes[Position] != value)
            {
                Fail(Position, value, Bytes[Position], semantic: false);
                return;
            }
            Position++;
        }

        internal void UInt16(ushort value)
        {
            Byte((byte)value);
            Byte((byte)(value >> 8));
        }

        internal void UInt32(uint value)
        {
            Byte((byte)value);
            Byte((byte)(value >> 8));
            Byte((byte)(value >> 16));
            Byte((byte)(value >> 24));
        }

        internal void UInt64(ulong value)
        {
            UInt32((uint)value);
            UInt32((uint)(value >> 32));
        }

        internal ulong Rel32Placeholder()
        {
            ulong offset = Position;
            SkipPlaceholderComparison = true;
            UInt32(0);
            SkipPlaceholderComparison = false;
            return offset;
        }

        internal ulong Rel8Placeholder()
        {
            ulong offset = Position;
            SkipPlaceholderComparison = true;
            Byte(0);
            SkipPlaceholderComparison = false;
            return offset;
        }

        internal void PatchRel8(ulong placeholder, ulong target)
        {
            if (!Succeeded || placeholder >= Position)
            {
                Fail(placeholder, target, Position, semantic: false);
                return;
            }

            long displacement =
                (long)target - (long)(placeholder + 1);
            if (displacement < -128 || displacement > 127)
            {
                Fail(
                    placeholder,
                    target,
                    (ulong)displacement,
                    semantic: false);
                return;
            }

            byte encoded = unchecked((byte)(sbyte)displacement);
            if (Mode == PlanWriterMode.Emit)
            {
                Bytes[placeholder] = encoded;
            }
            else if (Mode == PlanWriterMode.Compare
                && Bytes[placeholder] != encoded)
            {
                Fail(
                    placeholder,
                    encoded,
                    Bytes[placeholder],
                    semantic: false);
            }
        }

        internal void PatchRel32(ulong placeholder, ulong target)
        {
            if (!Succeeded
                || placeholder > Position
                || placeholder > ~0UL - 4)
            {
                Fail(placeholder, target, Position, semantic: false);
                return;
            }

            long displacement =
                (long)target - (long)(placeholder + 4);
            if (displacement < MinimumRel32Displacement
                || displacement > MaximumRel32Displacement)
            {
                Fail(
                    placeholder,
                    target,
                    (ulong)displacement,
                    semantic: false);
                return;
            }

            uint encoded = unchecked((uint)(int)displacement);
            if (Mode == PlanWriterMode.Emit)
            {
                Bytes[placeholder] = (byte)encoded;
                Bytes[placeholder + 1] = (byte)(encoded >> 8);
                Bytes[placeholder + 2] = (byte)(encoded >> 16);
                Bytes[placeholder + 3] = (byte)(encoded >> 24);
            }
            else if (Mode == PlanWriterMode.Compare)
            {
                uint observed =
                    (uint)Bytes[placeholder]
                    | ((uint)Bytes[placeholder + 1] << 8)
                    | ((uint)Bytes[placeholder + 2] << 16)
                    | ((uint)Bytes[placeholder + 3] << 24);
                if (observed != encoded)
                {
                    Fail(
                        placeholder,
                        encoded,
                        observed,
                        semantic: false);
                }
            }
        }

        internal void BeginSemantic(
            M01OwnedCr3CompositeTransitionOperation operation,
            M01OwnedCr3CompositeTransitionRecoveryClass recovery)
        {
            if (!Succeeded
                || SemanticOpen
                || operation
                    == M01OwnedCr3CompositeTransitionOperation
                        .Uninitialized
                || recovery
                    == M01OwnedCr3CompositeTransitionRecoveryClass
                        .Uninitialized)
            {
                Fail(
                    SemanticCount,
                    (ulong)operation,
                    (ulong)recovery,
                    semantic: true);
                return;
            }

            SemanticOpen = true;
            SemanticStart = Position;
            SemanticOperation = operation;
            SemanticRecovery = recovery;
        }

        internal void EndSemantic(
            ulong stateOffsetOrConstant = 0,
            uint branchTargetOffset =
                M01OwnedCr3CompositeTransitionSemanticRecord
                    .NoBranchTarget)
        {
            if (!Succeeded
                || !SemanticOpen
                || SemanticCount >= SemanticCapacity
                || Position < SemanticStart
                || Position - SemanticStart > MaximumUInt32Value
                || SemanticStart > MaximumUInt32Value)
            {
                Fail(
                    SemanticCount,
                    SemanticStart,
                    Position,
                    semantic: true);
                return;
            }

            M01OwnedCr3CompositeTransitionSemanticRecord record =
                new(
                    SemanticOperation,
                    SemanticRecovery,
                    (uint)SemanticStart,
                    (uint)(Position - SemanticStart),
                    branchTargetOffset,
                    stateOffsetOrConstant);
            if (Mode == PlanWriterMode.Emit)
            {
                Semantics[SemanticCount] = record;
            }
            else if (Mode == PlanWriterMode.Compare)
            {
                M01OwnedCr3CompositeTransitionSemanticRecord observed =
                    Semantics[SemanticCount];
                if (!SemanticEquals(record, observed))
                {
                    Fail(
                        SemanticCount,
                        PackSemantic(record),
                        PackSemantic(observed),
                        semantic: true);
                    return;
                }
            }

            SemanticCount++;
            SemanticOpen = false;
        }

        private static bool SemanticEquals(
            M01OwnedCr3CompositeTransitionSemanticRecord expected,
            M01OwnedCr3CompositeTransitionSemanticRecord observed)
        {
            return observed.ReservedFieldIsZero
                && expected.Operation == observed.Operation
                && expected.RecoveryClass == observed.RecoveryClass
                && expected.ByteOffset == observed.ByteOffset
                && expected.ByteLength == observed.ByteLength
                && expected.BranchTargetOffset
                    == observed.BranchTargetOffset
                && expected.StateOffsetOrConstant
                    == observed.StateOffsetOrConstant;
        }

        private static ulong PackSemantic(
            M01OwnedCr3CompositeTransitionSemanticRecord record)
        {
            return (ulong)record.ByteOffset
                | ((ulong)record.ByteLength << 32);
        }

        private void Fail(
            ulong invalidIndex,
            ulong expected,
            ulong observed,
            bool semantic)
        {
            if (!Succeeded)
            {
                return;
            }

            Succeeded = false;
            SemanticMismatch = semantic;
            InvalidIndex = invalidIndex;
            ExpectedValue = expected;
            ObservedValue = observed;
        }
    }

    private static ulong EmitJump(ref PlanWriter writer)
    {
        writer.Instruction();
        writer.Byte(0xe9);
        return writer.Rel32Placeholder();
    }

    private static ulong EmitJcc(
        ref PlanWriter writer,
        PlanCondition condition)
    {
        writer.Instruction();
        writer.Byte(0x0f);
        writer.Byte((byte)condition);
        return writer.Rel32Placeholder();
    }

    private static void EmitPushReg(
        ref PlanWriter writer,
        PlanRegister register)
    {
        writer.Instruction();
        byte value = (byte)register;
        if (value >= 8)
        {
            writer.Byte(0x41);
        }
        writer.Byte((byte)(0x50 + (value & 7)));
    }

    private static void EmitPopReg(
        ref PlanWriter writer,
        PlanRegister register)
    {
        writer.Instruction();
        byte value = (byte)register;
        if (value >= 8)
        {
            writer.Byte(0x41);
        }
        writer.Byte((byte)(0x58 + (value & 7)));
    }

    private static void EmitPushfq(ref PlanWriter writer)
    {
        writer.Instruction();
        writer.Byte(0x9c);
    }

    private static void EmitPopfq(ref PlanWriter writer)
    {
        writer.Instruction();
        writer.Byte(0x9d);
    }

    private static void EmitCli(ref PlanWriter writer)
    {
        writer.Instruction();
        writer.Byte(0xfa);
    }

    private static void EmitRet(ref PlanWriter writer)
    {
        writer.Instruction();
        writer.Byte(0xc3);
    }

    private static void EmitCpuid(ref PlanWriter writer)
    {
        writer.Instruction();
        writer.Byte(0x0f);
        writer.Byte(0xa2);
    }

    private static void EmitRdmsr(ref PlanWriter writer)
    {
        writer.Instruction();
        writer.Byte(0x0f);
        writer.Byte(0x32);
    }

    private static void EmitMfence(ref PlanWriter writer)
    {
        writer.Instruction();
        writer.Byte(0x0f);
        writer.Byte(0xae);
        writer.Byte(0xf0);
    }

    private static void EmitInvlpgRcx(ref PlanWriter writer)
    {
        writer.Instruction();
        writer.Byte(0x0f);
        writer.Byte(0x01);
        writer.Byte(0x39);
    }

    private static void EmitMovRegReg(
        ref PlanWriter writer,
        PlanRegister destination,
        PlanRegister source)
    {
        writer.Instruction();
        byte destinationValue = (byte)destination;
        byte sourceValue = (byte)source;
        writer.Byte((byte)(
            0x48
            | (sourceValue >= 8 ? 4 : 0)
            | (destinationValue >= 8 ? 1 : 0)));
        writer.Byte(0x89);
        writer.Byte((byte)(
            0xc0
            | ((sourceValue & 7) << 3)
            | (destinationValue & 7)));
    }

    private static void EmitMovReg32Reg32(
        ref PlanWriter writer,
        PlanRegister destination,
        PlanRegister source)
    {
        writer.Instruction();
        byte destinationValue = (byte)destination;
        byte sourceValue = (byte)source;
        byte rex = (byte)(
            0x40
            | (sourceValue >= 8 ? 4 : 0)
            | (destinationValue >= 8 ? 1 : 0));
        if (rex != 0x40)
        {
            writer.Byte(rex);
        }
        writer.Byte(0x89);
        writer.Byte((byte)(
            0xc0
            | ((sourceValue & 7) << 3)
            | (destinationValue & 7)));
    }

    private static void EmitMovRegImmediate64(
        ref PlanWriter writer,
        PlanRegister register,
        ulong value)
    {
        writer.Instruction();
        byte registerValue = (byte)register;
        writer.Byte((byte)(0x48 | (registerValue >= 8 ? 1 : 0)));
        writer.Byte((byte)(0xb8 + (registerValue & 7)));
        writer.UInt64(value);
    }

    private static void EmitMovReg32Immediate(
        ref PlanWriter writer,
        PlanRegister register,
        uint value)
    {
        writer.Instruction();
        byte registerValue = (byte)register;
        if (registerValue >= 8)
        {
            writer.Byte(0x41);
        }
        writer.Byte((byte)(0xb8 + (registerValue & 7)));
        writer.UInt32(value);
    }

    private static void EmitMovRegFromState(
        ref PlanWriter writer,
        PlanRegister destination,
        uint stateOffset)
    {
        writer.Instruction();
        byte destinationValue = (byte)destination;
        writer.Byte((byte)(0x48 | (destinationValue >= 8 ? 4 : 0)));
        writer.Byte(0x8b);
        writer.Byte((byte)(0x83 | ((destinationValue & 7) << 3)));
        writer.UInt32(stateOffset);
    }

    private static void EmitMovStateFromReg(
        ref PlanWriter writer,
        uint stateOffset,
        PlanRegister source)
    {
        writer.Instruction();
        byte sourceValue = (byte)source;
        writer.Byte((byte)(0x48 | (sourceValue >= 8 ? 4 : 0)));
        writer.Byte(0x89);
        writer.Byte((byte)(0x83 | ((sourceValue & 7) << 3)));
        writer.UInt32(stateOffset);
    }

    private static void EmitMovStateByteImmediate(
        ref PlanWriter writer,
        uint stateOffset,
        byte value)
    {
        writer.Instruction();
        writer.Byte(0xc6);
        writer.Byte(0x83);
        writer.UInt32(stateOffset);
        writer.Byte(value);
    }

    private static void EmitMovStateByteFromReg(
        ref PlanWriter writer,
        uint stateOffset,
        PlanRegister source)
    {
        writer.Instruction();
        byte sourceValue = (byte)source;
        byte rex = (byte)(0x40 | (sourceValue >= 8 ? 4 : 0));
        if (rex != 0x40 || (sourceValue & 7) >= 4)
        {
            writer.Byte(rex);
        }
        writer.Byte(0x88);
        writer.Byte((byte)(0x83 | ((sourceValue & 7) << 3)));
        writer.UInt32(stateOffset);
    }

    private static void EmitCmpStateDwordImmediate(
        ref PlanWriter writer,
        uint stateOffset,
        uint value)
    {
        writer.Instruction();
        writer.Byte(0x81);
        writer.Byte(0xbb);
        writer.UInt32(stateOffset);
        writer.UInt32(value);
    }

    private static void EmitCmpStateWordImmediate(
        ref PlanWriter writer,
        uint stateOffset,
        ushort value)
    {
        writer.Instruction();
        writer.Byte(0x66);
        writer.Byte(0x81);
        writer.Byte(0xbb);
        writer.UInt32(stateOffset);
        writer.UInt16(value);
    }

    private static void EmitCmpStateByteImmediate(
        ref PlanWriter writer,
        uint stateOffset,
        byte value)
    {
        writer.Instruction();
        writer.Byte(0x80);
        writer.Byte(0xbb);
        writer.UInt32(stateOffset);
        writer.Byte(value);
    }

    private static void EmitTestRegReg(
        ref PlanWriter writer,
        PlanRegister left,
        PlanRegister right)
    {
        writer.Instruction();
        byte leftValue = (byte)left;
        byte rightValue = (byte)right;
        writer.Byte((byte)(
            0x48
            | (rightValue >= 8 ? 4 : 0)
            | (leftValue >= 8 ? 1 : 0)));
        writer.Byte(0x85);
        writer.Byte((byte)(
            0xc0
            | ((rightValue & 7) << 3)
            | (leftValue & 7)));
    }

    private static void EmitCmpRegReg(
        ref PlanWriter writer,
        PlanRegister left,
        PlanRegister right)
    {
        writer.Instruction();
        byte leftValue = (byte)left;
        byte rightValue = (byte)right;
        writer.Byte((byte)(
            0x48
            | (rightValue >= 8 ? 4 : 0)
            | (leftValue >= 8 ? 1 : 0)));
        writer.Byte(0x39);
        writer.Byte((byte)(
            0xc0
            | ((rightValue & 7) << 3)
            | (leftValue & 7)));
    }

    private static void EmitCmpRegImmediate32(
        ref PlanWriter writer,
        PlanRegister register,
        uint value)
    {
        writer.Instruction();
        byte registerValue = (byte)register;
        writer.Byte((byte)(0x48 | (registerValue >= 8 ? 1 : 0)));
        writer.Byte(0x81);
        writer.Byte((byte)(0xf8 | (registerValue & 7)));
        writer.UInt32(value);
    }

    private static void EmitCmpReg32Immediate(
        ref PlanWriter writer,
        PlanRegister register,
        uint value)
    {
        writer.Instruction();
        byte registerValue = (byte)register;
        if (registerValue >= 8)
        {
            writer.Byte(0x41);
        }
        writer.Byte(0x81);
        writer.Byte((byte)(0xf8 | (registerValue & 7)));
        writer.UInt32(value);
    }

    private static void EmitTestRegImmediate32(
        ref PlanWriter writer,
        PlanRegister register,
        uint value)
    {
        writer.Instruction();
        byte registerValue = (byte)register;
        writer.Byte((byte)(0x48 | (registerValue >= 8 ? 1 : 0)));
        writer.Byte(0xf7);
        writer.Byte((byte)(0xc0 | (registerValue & 7)));
        writer.UInt32(value);
    }

    private static void EmitAndRegReg(
        ref PlanWriter writer,
        PlanRegister destination,
        PlanRegister source)
    {
        writer.Instruction();
        byte destinationValue = (byte)destination;
        byte sourceValue = (byte)source;
        writer.Byte((byte)(
            0x48
            | (sourceValue >= 8 ? 4 : 0)
            | (destinationValue >= 8 ? 1 : 0)));
        writer.Byte(0x21);
        writer.Byte((byte)(
            0xc0
            | ((sourceValue & 7) << 3)
            | (destinationValue & 7)));
    }

    private static void EmitOrRegReg(
        ref PlanWriter writer,
        PlanRegister destination,
        PlanRegister source)
    {
        writer.Instruction();
        byte destinationValue = (byte)destination;
        byte sourceValue = (byte)source;
        writer.Byte((byte)(
            0x48
            | (sourceValue >= 8 ? 4 : 0)
            | (destinationValue >= 8 ? 1 : 0)));
        writer.Byte(0x09);
        writer.Byte((byte)(
            0xc0
            | ((sourceValue & 7) << 3)
            | (destinationValue & 7)));
    }

    private static void EmitAndRegImmediate32(
        ref PlanWriter writer,
        PlanRegister register,
        uint value)
    {
        writer.Instruction();
        byte registerValue = (byte)register;
        writer.Byte((byte)(0x48 | (registerValue >= 8 ? 1 : 0)));
        writer.Byte(0x81);
        writer.Byte((byte)(0xe0 | (registerValue & 7)));
        writer.UInt32(value);
    }

    private static void EmitShiftRightImmediate(
        ref PlanWriter writer,
        PlanRegister register,
        byte count)
    {
        writer.Instruction();
        byte registerValue = (byte)register;
        writer.Byte((byte)(0x48 | (registerValue >= 8 ? 1 : 0)));
        writer.Byte(0xc1);
        writer.Byte((byte)(0xe8 | (registerValue & 7)));
        writer.Byte(count);
    }

    private static void EmitShiftLeftCl(
        ref PlanWriter writer,
        PlanRegister register)
    {
        writer.Instruction();
        byte registerValue = (byte)register;
        writer.Byte((byte)(0x48 | (registerValue >= 8 ? 1 : 0)));
        writer.Byte(0xd3);
        writer.Byte((byte)(0xe0 | (registerValue & 7)));
    }

    private static void EmitDecReg(
        ref PlanWriter writer,
        PlanRegister register)
    {
        writer.Instruction();
        byte registerValue = (byte)register;
        writer.Byte((byte)(0x48 | (registerValue >= 8 ? 1 : 0)));
        writer.Byte(0xff);
        writer.Byte((byte)(0xc8 | (registerValue & 7)));
    }

    private static void EmitAddRegImmediate8(
        ref PlanWriter writer,
        PlanRegister register,
        byte value)
    {
        writer.Instruction();
        byte registerValue = (byte)register;
        writer.Byte((byte)(0x48 | (registerValue >= 8 ? 1 : 0)));
        writer.Byte(0x83);
        writer.Byte((byte)(0xc0 | (registerValue & 7)));
        writer.Byte(value);
    }

    private static void EmitSubRegImmediate8(
        ref PlanWriter writer,
        PlanRegister register,
        byte value)
    {
        writer.Instruction();
        byte registerValue = (byte)register;
        writer.Byte((byte)(0x48 | (registerValue >= 8 ? 1 : 0)));
        writer.Byte(0x83);
        writer.Byte((byte)(0xe8 | (registerValue & 7)));
        writer.Byte(value);
    }

    private static void EmitLeaRegFromRsp(
        ref PlanWriter writer,
        PlanRegister destination,
        byte displacement)
    {
        writer.Instruction();
        byte destinationValue = (byte)destination;
        writer.Byte((byte)(0x48 | (destinationValue >= 8 ? 4 : 0)));
        writer.Byte(0x8d);
        writer.Byte((byte)(0x44 | ((destinationValue & 7) << 3)));
        writer.Byte(0x24);
        writer.Byte(displacement);
    }

    private static void EmitLeaRip(
        ref PlanWriter writer,
        PlanRegister destination)
    {
        writer.Instruction();
        byte destinationValue = (byte)destination;
        writer.Byte((byte)(0x48 | (destinationValue >= 8 ? 4 : 0)));
        writer.Byte(0x8d);
        writer.Byte((byte)(0x05 | ((destinationValue & 7) << 3)));
        writer.UInt32(0);
    }

    private static void EmitMovRegFromAddress(
        ref PlanWriter writer,
        PlanRegister destination,
        PlanRegister address)
    {
        writer.Instruction();
        byte destinationValue = (byte)destination;
        byte addressValue = (byte)address;
        writer.Byte((byte)(
            0x48
            | (destinationValue >= 8 ? 4 : 0)
            | (addressValue >= 8 ? 1 : 0)));
        writer.Byte(0x8b);
        if ((addressValue & 7) == 4)
        {
            writer.Byte((byte)(0x04 | ((destinationValue & 7) << 3)));
            writer.Byte(0x24);
        }
        else if ((addressValue & 7) == 5)
        {
            writer.Byte((byte)(0x45 | ((destinationValue & 7) << 3)));
            writer.Byte(0);
        }
        else
        {
            writer.Byte((byte)(
                ((destinationValue & 7) << 3)
                | (addressValue & 7)));
        }
    }

    private static void EmitMovAddressFromReg(
        ref PlanWriter writer,
        PlanRegister address,
        PlanRegister source)
    {
        writer.Instruction();
        byte addressValue = (byte)address;
        byte sourceValue = (byte)source;
        writer.Byte((byte)(
            0x48
            | (sourceValue >= 8 ? 4 : 0)
            | (addressValue >= 8 ? 1 : 0)));
        writer.Byte(0x89);
        if ((addressValue & 7) == 4)
        {
            writer.Byte((byte)(0x04 | ((sourceValue & 7) << 3)));
            writer.Byte(0x24);
        }
        else if ((addressValue & 7) == 5)
        {
            writer.Byte((byte)(0x45 | ((sourceValue & 7) << 3)));
            writer.Byte(0);
        }
        else
        {
            writer.Byte((byte)(
                ((sourceValue & 7) << 3)
                | (addressValue & 7)));
        }
    }

    private static void EmitMovRdxFromRdxRcx8(
        ref PlanWriter writer)
    {
        writer.Instruction();
        writer.Byte(0x48);
        writer.Byte(0x8b);
        writer.Byte(0x14);
        writer.Byte(0xca);
    }

    private static void EmitLeaRdxFromRdxRcx8(
        ref PlanWriter writer)
    {
        writer.Instruction();
        writer.Byte(0x48);
        writer.Byte(0x8d);
        writer.Byte(0x14);
        writer.Byte(0xca);
    }

    private static void EmitCmpMemoryDwordImmediate(
        ref PlanWriter writer,
        PlanRegister address,
        byte displacement,
        uint value)
    {
        writer.Instruction();
        byte addressValue = (byte)address;
        writer.Byte((byte)(0x40 | (addressValue >= 8 ? 1 : 0)));
        writer.Byte(0x81);
        writer.Byte((byte)(0x78 | (addressValue & 7)));
        writer.Byte(displacement);
        writer.UInt32(value);
    }

    private static void EmitCmpMemoryWordImmediate(
        ref PlanWriter writer,
        PlanRegister address,
        byte displacement,
        ushort value)
    {
        writer.Instruction();
        byte addressValue = (byte)address;
        writer.Byte(0x66);
        if (addressValue >= 8)
        {
            writer.Byte(0x41);
        }
        writer.Byte(0x81);
        writer.Byte((byte)(0x78 | (addressValue & 7)));
        writer.Byte(displacement);
        writer.UInt16(value);
    }

    private static void EmitMovRegFromCr(
        ref PlanWriter writer,
        PlanRegister destination,
        byte controlRegister)
    {
        writer.Instruction();
        byte destinationValue = (byte)destination;
        writer.Byte((byte)(0x48 | (destinationValue >= 8 ? 1 : 0)));
        writer.Byte(0x0f);
        writer.Byte(0x20);
        writer.Byte((byte)(
            0xc0
            | ((controlRegister & 7) << 3)
            | (destinationValue & 7)));
    }

    private static void EmitMovCrFromReg(
        ref PlanWriter writer,
        byte controlRegister,
        PlanRegister source)
    {
        writer.Instruction();
        byte sourceValue = (byte)source;
        writer.Byte((byte)(0x48 | (sourceValue >= 8 ? 1 : 0)));
        writer.Byte(0x0f);
        writer.Byte(0x22);
        writer.Byte((byte)(
            0xc0
            | ((controlRegister & 7) << 3)
            | (sourceValue & 7)));
    }

    private static void EmitMovzxEcxAl(ref PlanWriter writer)
    {
        writer.Instruction();
        writer.Byte(0x0f);
        writer.Byte(0xb6);
        writer.Byte(0xc8);
    }

    private static void EmitCallReg(
        ref PlanWriter writer,
        PlanRegister register)
    {
        writer.Instruction();
        byte value = (byte)register;
        if (value >= 8)
        {
            writer.Byte(0x41);
        }
        writer.Byte(0xff);
        writer.Byte((byte)(0xd0 | (value & 7)));
    }

    private static void EmitBtrRegImmediate(
        ref PlanWriter writer,
        PlanRegister register,
        byte bit)
    {
        writer.Instruction();
        byte value = (byte)register;
        writer.Byte((byte)(0x48 | (value >= 8 ? 1 : 0)));
        writer.Byte(0x0f);
        writer.Byte(0xba);
        writer.Byte((byte)(0xf0 | (value & 7)));
        writer.Byte(bit);
    }

    private static void EmitPreCliGuard(
        ref PlanWriter writer,
        PlanCondition successCondition,
        M01OwnedCr3CompositeTransitionRuntimeStatus failureStatus)
    {
        ulong success = EmitJcc(ref writer, successCondition);
        EmitMovReg32Immediate(
            ref writer,
            PlanRegister.Rax,
            (uint)failureStatus);
        ulong failure = EmitJump(ref writer);
        writer.PatchRel32(failure, writer.PreCliFailureTarget);
        writer.PatchRel32(success, writer.Position);
    }

    private static void EmitRuntimeGuard(
        ref PlanWriter writer,
        PlanCondition successCondition,
        M01OwnedCr3CompositeTransitionRuntimeStatus failureStatus,
        ulong* cleanupFixups,
        ref ulong cleanupFixupCount)
    {
        ulong success = EmitJcc(ref writer, successCondition);
        EmitMovReg32Immediate(
            ref writer,
            PlanRegister.R12,
            (uint)failureStatus);
        cleanupFixups[cleanupFixupCount++] = EmitJump(ref writer);
        writer.PatchRel32(success, writer.Position);
    }

    private static void EmitProgressCheckpoint(
        ref PlanWriter writer,
        M01OwnedCr3CompositeTransitionCheckpoint checkpoint)
    {
        EmitMovStateByteImmediate(
            ref writer,
            StateCheckpointOffset,
            (byte)checkpoint);
        EmitMovStateByteImmediate(
            ref writer,
            StateProgressCheckpointOffset,
            (byte)checkpoint);
    }

    private static void EmitPreCliCriticalGeometry(
        ref PlanWriter writer)
    {
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rax,
            StateCompositeCodeStartOffset);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rcx,
            StateCompositeCodeEndOffset);
        EmitMovRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.Rax);
        EmitOrRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.Rcx);
        EmitTestRegImmediate32(
            ref writer,
            PlanRegister.Rdx,
            (uint)(PageSize - 1));
        EmitPreCliGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .InvalidCodeRange);
        EmitAddRegImmediate32(
            ref writer,
            PlanRegister.Rax,
            (uint)PageSize);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.Rcx);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .InvalidCodeRange);
        EmitLeaRip(ref writer, PlanRegister.Rdx);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rax,
            StateCompositeCodeStartOffset);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.Rax);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.AboveOrEqual,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .InvalidCodeRange);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.Rcx);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.Below,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .InvalidCodeRange);

        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rax,
            StateTablePageArenaStartOffset);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rcx,
            StateTablePageArenaEndOffset);
        EmitMovRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.Rax);
        EmitOrRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.Rcx);
        EmitTestRegImmediate32(
            ref writer,
            PlanRegister.Rdx,
            (uint)(PageSize - 1));
        EmitPreCliGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .InvalidTableArena);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.Rcx,
            PlanRegister.Rax);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.Above,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .InvalidTableArena);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rdx,
            StateTableStateArenaStartOffset);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.Rcx);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .InvalidTableArena);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.Rbx);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .InvalidTableArena);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.R9,
            StateTableStateArenaEndOffset);
        EmitAddRegImmediate32(
            ref writer,
            PlanRegister.Rdx,
            (uint)PageSize);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.R9);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .InvalidTableArena);
        EmitMovRegImmediate64(
            ref writer,
            PlanRegister.R10,
            LowCanonicalExclusiveCeiling);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.R9,
            PlanRegister.R10);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.BelowOrEqual,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .InvalidTableArena);

        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rdx,
            StateOwnedCr3RawOffset);
        EmitMovRegReg(
            ref writer,
            PlanRegister.R9,
            PlanRegister.Rdx);
        EmitMovRegImmediate64(
            ref writer,
            PlanRegister.R10,
            Cr3ArchitecturalBaseMask | Cr3PermittedLowBitsMask);
        EmitAndRegReg(
            ref writer,
            PlanRegister.R9,
            PlanRegister.R10);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.R9,
            PlanRegister.Rdx);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .InvalidOwnedCr3);
        EmitMovRegImmediate64(
            ref writer,
            PlanRegister.R10,
            Cr3ArchitecturalBaseMask);
        EmitAndRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.R10);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.Rax);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.AboveOrEqual,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .InvalidOwnedCr3);
        EmitAddRegImmediate32(
            ref writer,
            PlanRegister.Rdx,
            (uint)PageSize);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.Rcx);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.BelowOrEqual,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .InvalidOwnedCr3);
    }

    private static ulong EmitCallGateShortBranch(
        ref PlanWriter writer,
        byte opcode)
    {
        writer.Instruction();
        writer.Byte(opcode);
        return writer.Rel8Placeholder();
    }

    private static void EmitCallGateBranchTo(
        ref PlanWriter writer,
        byte opcode,
        ulong target)
    {
        ulong fixup = EmitCallGateShortBranch(ref writer, opcode);
        writer.PatchRel8(fixup, target);
    }

    private static ulong EmitCallGateLeaR8Rip(
        ref PlanWriter writer)
    {
        writer.Instruction();
        writer.Byte(0x4c);
        writer.Byte(0x8d);
        writer.Byte(0x05);
        return writer.Rel32Placeholder();
    }

    private static void EmitCallGateDirectCompositeCall(
        ref PlanWriter writer)
    {
        writer.Instruction();
        writer.Byte(0xe8);
        writer.UInt32(CallToCompositeRel32);
    }

    private static void BuildCallGate(ref PlanWriter writer)
    {
        ulong* rejectFixups = stackalloc ulong[16];
        ulong rejectFixupCount = 0;

        // Guard group A uses only volatile registers and performs no push,
        // write, or call. JRCXZ rejects a null state before dereference.
        rejectFixups[rejectFixupCount++] =
            EmitCallGateShortBranch(ref writer, 0xe3);
        EmitMovRegReg(
            ref writer,
            PlanRegister.R8,
            PlanRegister.Rcx);
        EmitOrRegReg(
            ref writer,
            PlanRegister.R8,
            PlanRegister.Rdx);
        writer.Instruction();
        writer.Byte(0x41);
        writer.Byte(0xf6);
        writer.Byte(0xc0);
        writer.Byte(0x07);
        rejectFixups[rejectFixupCount++] =
            EmitCallGateShortBranch(ref writer, 0x75);
        EmitShiftRightImmediate(
            ref writer,
            PlanRegister.R8,
            47);
        rejectFixups[rejectFixupCount++] =
            EmitCallGateShortBranch(ref writer, 0x75);

        EmitMovRegImmediate64(
            ref writer,
            PlanRegister.R8,
            0x0001_0001_0000_01e0UL);
        writer.Instruction();
        writer.Byte(0x4c);
        writer.Byte(0x39);
        writer.Byte(0x01);
        rejectFixups[rejectFixupCount++] =
            EmitCallGateShortBranch(ref writer, 0x75);

        writer.Instruction();
        writer.Byte(0x48);
        writer.Byte(0x39);
        writer.Byte(0x89);
        writer.UInt32(StateExpectedSelfAddressOffset);
        rejectFixups[rejectFixupCount++] =
            EmitCallGateShortBranch(ref writer, 0x75);

        // The prepared state page is exactly the table-state arena.
        writer.Instruction();
        writer.Byte(0x48);
        writer.Byte(0x39);
        writer.Byte(0x49);
        writer.Byte((byte)StateTableStateArenaStartOffset);
        rejectFixups[rejectFixupCount++] =
            EmitCallGateShortBranch(ref writer, 0x75);

        // Windows-x64 gate entry requires RSP mod 16 == 8.
        EmitLeaRegFromRsp(
            ref writer,
            PlanRegister.R8,
            unchecked((byte)-8));
        writer.Instruction();
        writer.Byte(0x41);
        writer.Byte(0xf6);
        writer.Byte(0xc0);
        writer.Byte(0x0f);
        rejectFixups[rejectFixupCount++] =
            EmitCallGateShortBranch(ref writer, 0x75);

        // Checked RSP - StackStart must be at least 128 bytes.
        EmitMovRegReg(
            ref writer,
            PlanRegister.R8,
            PlanRegister.Rsp);
        writer.Instruction();
        writer.Byte(0x4c);
        writer.Byte(0x2b);
        writer.Byte(0x41);
        writer.Byte((byte)StateStackStartOffset);
        rejectFixups[rejectFixupCount++] =
            EmitCallGateShortBranch(ref writer, 0x72);
        EmitShiftRightImmediate(
            ref writer,
            PlanRegister.R8,
            7);
        rejectFixups[rejectFixupCount++] =
            EmitCallGateShortBranch(ref writer, 0x74);

        // Checked StackEnd - RSP must include return plus shadow bytes.
        writer.Instruction();
        writer.Byte(0x4c);
        writer.Byte(0x8b);
        writer.Byte(0x41);
        writer.Byte((byte)StateStackEndOffset);
        writer.Instruction();
        writer.Byte(0x4c);
        writer.Byte(0x2b);
        writer.Byte(0xc4);
        rejectFixups[rejectFixupCount++] =
            EmitCallGateShortBranch(ref writer, 0x72);
        writer.Instruction();
        writer.Byte(0x49);
        writer.Byte(0x83);
        writer.Byte(0xf8);
        writer.Byte((byte)CallGateRequiredUpperFrameBytes);
        rejectFixups[rejectFixupCount++] =
            EmitCallGateShortBranch(ref writer, 0x72);

        ulong continueFixup =
            EmitCallGateShortBranch(ref writer, 0xeb);
        ulong rejectTarget = writer.Position;
        writer.Instruction();
        writer.Byte(0x31);
        writer.Byte(0xc0);
        writer.Instruction();
        writer.Byte(0xb0);
        writer.Byte((byte)CallGateRejectedReturnValue);
        EmitRet(ref writer);
        ulong continueTarget = writer.Position;
        writer.PatchRel8(continueFixup, continueTarget);
        for (ulong index = 0; index < rejectFixupCount; index++)
        {
            writer.PatchRel8(rejectFixups[index], rejectTarget);
        }

        // RDX is still not dereferenced: first prove [RDX,RDX+160) is
        // contained by the exact state page without arithmetic wrap.
        writer.Instruction();
        writer.Byte(0x48);
        writer.Byte(0x3b);
        writer.Byte(0x51);
        writer.Byte((byte)StateTableStateArenaStartOffset);
        EmitCallGateBranchTo(ref writer, 0x72, rejectTarget);
        writer.Instruction();
        writer.Byte(0x4c);
        writer.Byte(0x8d);
        writer.Byte(0x81);
        writer.UInt32((uint)PageSize);
        writer.Instruction();
        writer.Byte(0x4c);
        writer.Byte(0x2b);
        writer.Byte(0xc2);
        EmitCallGateBranchTo(ref writer, 0x72, rejectTarget);
        writer.Instruction();
        writer.Byte(0x49);
        writer.Byte(0x81);
        writer.Byte(0xf8);
        writer.UInt32(
            M01OwnedCr3CompositeCallGateObservation.StructureSize);
        EmitCallGateBranchTo(ref writer, 0x72, rejectTarget);

        EmitMovRegImmediate64(
            ref writer,
            PlanRegister.R8,
            0x0101_0001_0000_00a0UL);
        writer.Instruction();
        writer.Byte(0x4c);
        writer.Byte(0x39);
        writer.Byte(0x02);
        EmitCallGateBranchTo(ref writer, 0x75, rejectTarget);
        writer.Instruction();
        writer.Byte(0x48);
        writer.Byte(0x39);
        writer.Byte(0x52);
        writer.Byte(0x08);
        EmitCallGateBranchTo(ref writer, 0x75, rejectTarget);

        // Counts, padding, and the full completion signature must all be
        // pristine before the first observation write.
        writer.Instruction();
        writer.Byte(0x48);
        writer.Byte(0x83);
        writer.Byte(0xba);
        writer.UInt32(CallGateObservationPristineQwordOffset);
        writer.Byte(0x00);
        EmitCallGateBranchTo(ref writer, 0x75, rejectTarget);

        ulong returnAddressFixup = EmitCallGateLeaR8Rip(ref writer);
        writer.Instruction();
        writer.Byte(0x4c);
        writer.Byte(0x39);
        writer.Byte(0x41);
        writer.Byte((byte)StateExpectedReturnAddressOffset);
        EmitCallGateBranchTo(ref writer, 0x75, rejectTarget);

        // The guarded body preserves RSI, provides 32-byte shadow space,
        // and leaves RCX unchanged for the composite entry.
        EmitPushReg(ref writer, PlanRegister.Rsi);
        EmitSubRegImmediate8(ref writer, PlanRegister.Rsp, 0x20);
        writer.Instruction();
        writer.Byte(0x48);
        writer.Byte(0x8d);
        writer.Byte(0x72);
        writer.Byte((byte)CallGateObservationPointerBias);

        // The packed Armed-header guard proves lifecycle starts at one.
        writer.Instruction();
        writer.Byte(0xfe);
        writer.Byte(0x46);
        writer.Byte(unchecked((byte)-120));
        writer.Instruction();
        writer.Byte(0x4c);
        writer.Byte(0x8b);
        writer.Byte(0x5e);
        writer.Byte(unchecked((byte)-39));
        writer.Instruction();
        writer.Byte(0x4c);
        writer.Byte(0x89);
        writer.Byte(0x5e);
        writer.Byte(unchecked((byte)-31));
        // The pristine-count guard proves the byte starts at zero.
        writer.Instruction();
        writer.Byte(0xfe);
        writer.Byte(0x46);
        writer.Byte(0x0d);

        // Preceding guards, the frame adjustment, and INC writes have mutated
        // arithmetic flags. Restore the prepared preflight snapshot
        // as the final instruction pair before CALL so the composite captures
        // the intended entry RFLAGS rather than gate-internal flags.
        writer.Instruction();
        writer.Byte(0xff);
        writer.Byte(0x72);
        writer.Byte(
            (byte)CallGateObservationExpectedOriginalRflagsOffset);
        EmitPopfq(ref writer);

        writer.RequirePosition(CallInstructionOffset - CallGateOffset);
        EmitCallGateDirectCompositeCall(ref writer);
        ulong returnTarget = writer.Position;
        writer.RequirePosition(
            CallNextReturnSiteOffset - CallGateOffset);
        writer.PatchRel32(returnAddressFixup, returnTarget);

        // This must remain the first instruction at the exact return site.
        EmitPushfq(ref writer);
        EmitPopReg(ref writer, PlanRegister.R10);
        writer.Instruction();
        writer.Byte(0x4c);
        writer.Byte(0x89);
        writer.Byte(0x56);
        writer.Byte(unchecked((byte)-23));
        EmitMovRegFromCr(ref writer, PlanRegister.R10, 3);
        writer.Instruction();
        writer.Byte(0x4c);
        writer.Byte(0x89);
        writer.Byte(0x56);
        writer.Byte(unchecked((byte)-15));
        EmitMovRegFromCr(ref writer, PlanRegister.R10, 4);
        writer.Instruction();
        writer.Byte(0x4c);
        writer.Byte(0x89);
        writer.Byte(0x56);
        writer.Byte(unchecked((byte)-7));
        writer.Instruction();
        writer.Byte(0x89);
        writer.Byte(0x46);
        writer.Byte(0x09);

        // R11 preserves EAX while rereading the boundary canary.
        writer.Instruction();
        writer.Byte(0x4c);
        writer.Byte(0x8b);
        writer.Byte(0x5e);
        writer.Byte(unchecked((byte)-39));
        writer.Instruction();
        writer.Byte(0x4c);
        writer.Byte(0x89);
        writer.Byte(0x5e);
        writer.Byte(0x01);
        writer.Instruction();
        writer.Byte(0xc6);
        writer.Byte(0x46);
        writer.Byte(0x0e);
        writer.Byte((byte)CallGateExactPostReturnObservationCount);
        writer.Instruction();
        writer.Byte(0xc6);
        writer.Byte(0x46);
        writer.Byte(unchecked((byte)-120));
        writer.Byte(
            (byte)M01OwnedCr3CompositeCallGateObservationLifecycle
                .Completed);

        // The completion signature is the final observation write.
        writer.Instruction();
        writer.Byte(0xc7);
        writer.Byte(0x46);
        writer.Byte(0x11);
        writer.UInt32(
            M01OwnedCr3CompositeCallGateObservation
                .CompletionSignature);
        EmitAddRegImmediate8(ref writer, PlanRegister.Rsp, 0x20);
        EmitPopReg(ref writer, PlanRegister.Rsi);
        EmitRet(ref writer);
    }

    private static void BuildPlan(ref PlanWriter writer)
    {
        ulong* restoreFlags = stackalloc ulong[16];
        ulong* restoreCr4 = stackalloc ulong[16];
        ulong* restoreCr3 = stackalloc ulong[64];
        ulong restoreFlagsCount = 0;
        ulong restoreCr4Count = 0;
        ulong restoreCr3Count = 0;

        writer.BeginSemantic(
            M01OwnedCr3CompositeTransitionOperation.ValidateState,
            M01OwnedCr3CompositeTransitionRecoveryClass
                .PreCliNoStateMutation);
        EmitPushReg(ref writer, PlanRegister.Rbx);
        EmitPushReg(ref writer, PlanRegister.Rsi);
        EmitPushReg(ref writer, PlanRegister.R12);
        EmitPushfq(ref writer);
        EmitPopReg(ref writer, PlanRegister.R8);
        ulong validateStateBody = EmitJump(ref writer);
        writer.PreCliFailureTarget = writer.Position;
        EmitMovRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.R8);
        EmitPopReg(ref writer, PlanRegister.R12);
        EmitPopReg(ref writer, PlanRegister.Rsi);
        EmitPopReg(ref writer, PlanRegister.Rbx);
        EmitPushReg(ref writer, PlanRegister.Rdx);
        EmitPopfq(ref writer);
        EmitRet(ref writer);
        writer.PatchRel32(validateStateBody, writer.Position);
        EmitTestRegReg(ref writer, PlanRegister.Rcx, PlanRegister.Rcx);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.NotEqual,
            M01OwnedCr3CompositeTransitionRuntimeStatus.InvalidState);
        EmitMovRegReg(
            ref writer,
            PlanRegister.Rbx,
            PlanRegister.Rcx);
        EmitTestRegImmediate32(ref writer, PlanRegister.Rbx, 7);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus.InvalidState);
        EmitCmpStateDwordImmediate(
            ref writer,
            StateStructureSizeOffset,
            M01OwnedCr3CompositeTransitionRuntimeState.StructureSize);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus.InvalidState);
        EmitCmpStateWordImmediate(
            ref writer,
            StateStructureVersionOffset,
            M01OwnedCr3CompositeTransitionRuntimeState.StructureVersion);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus.InvalidState);
        EmitCmpStateByteImmediate(
            ref writer,
            StateIsInitializedOffset,
            1);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus.InvalidState);
        EmitCmpStateByteImmediate(ref writer, StateReserved0Offset, 0);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus.InvalidState);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rax,
            StateExpectedSelfAddressOffset);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.Rbx);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus.InvalidState);
        EmitCmpStateDwordImmediate(
            ref writer,
            StateFirmwareCallbackCountOffset,
            0);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .FirmwareCallbackForbidden);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rax,
            StateExpectedProbeHierarchyIdentityOffset);
        EmitMovRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.Rax);
        EmitMovRegImmediate64(
            ref writer,
            PlanRegister.Rcx,
            ~ProbeHierarchyIdentityPermittedMask);
        EmitAndRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.Rcx);
        EmitTestRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.Rdx);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus.InvalidState);
        EmitAndRegImmediate32(
            ref writer,
            PlanRegister.Rax,
            (uint)CriticalMappingPageCountMask);
        EmitTestRegReg(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.Rax);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.NotEqual,
            M01OwnedCr3CompositeTransitionRuntimeStatus.InvalidState);
        EmitCmpRegImmediate32(
            ref writer,
            PlanRegister.Rax,
            (uint)MaximumCriticalMappingPageCount);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.BelowOrEqual,
            M01OwnedCr3CompositeTransitionRuntimeStatus.InvalidState);
        EmitMovRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.Rbx);
        EmitMovRegImmediate64(
            ref writer,
            PlanRegister.Rax,
            StateOriginalRflagsOffset);
        EmitAddRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.Rax);
        EmitMovRegReg(
            ref writer,
            PlanRegister.Rcx,
            PlanRegister.Rbx);
        EmitMovRegImmediate64(
            ref writer,
            PlanRegister.Rax,
            M01OwnedCr3CompositeTransitionRuntimeState.StructureSize);
        EmitAddRegReg(
            ref writer,
            PlanRegister.Rcx,
            PlanRegister.Rax);
        ulong pristineOutputLoop = writer.Position;
        EmitMovRegFromAddress(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.Rdx);
        EmitTestRegReg(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.Rax);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus.InvalidState);
        EmitAddRegImmediate8(
            ref writer,
            PlanRegister.Rdx,
            sizeof(ulong));
        EmitCmpRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.Rcx);
        ulong pristineOutputLoopBack =
            EmitJcc(ref writer, PlanCondition.Below);
        writer.PatchRel32(
            pristineOutputLoopBack,
            pristineOutputLoop);

        EmitPreCliCriticalGeometry(ref writer);

        EmitLeaRegFromRsp(ref writer, PlanRegister.Rax, 24);
        EmitMovRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.Rax);
        EmitAndRegImmediate32(ref writer, PlanRegister.Rdx, 15);
        EmitCmpRegImmediate32(ref writer, PlanRegister.Rdx, 8);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .InvalidStackRange);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rdx,
            StateStackStartOffset);
        EmitAddRegImmediate8(ref writer, PlanRegister.Rdx, 64);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.Rdx);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.AboveOrEqual,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .InvalidStackRange);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rdx,
            StateStackEndOffset);
        EmitMovRegReg(
            ref writer,
            PlanRegister.Rcx,
            PlanRegister.Rax);
        EmitAddRegImmediate8(ref writer, PlanRegister.Rcx, 40);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.Rcx,
            PlanRegister.Rdx);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.BelowOrEqual,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .InvalidStackRange);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.Rdx);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.Below,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .InvalidStackRange);
        EmitMovRegFromAddress(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.Rax);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rcx,
            StateExpectedReturnAddressOffset);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.Rcx);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .InvalidReturnAddress);

        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rax,
            StateCanaryAddressOffset);
        EmitMovRegImmediate64(
            ref writer,
            PlanRegister.Rdx,
            M01OwnedCr3CompositeTransitionRuntimeState.StructureSize);
        EmitAddRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.Rbx);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.Rdx);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus.InvalidCanary);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rax,
            StateProbeEntryOffset);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rdx,
            StateProbePageStartOffset);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.Rdx);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .InvalidProbeContract);
        EmitMovRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.Rax);
        EmitAddRegImmediate8(
            ref writer,
            PlanRegister.Rdx,
            (byte)ExactProbeByteLength);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rcx,
            StateProbePageEndOffset);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.Rcx);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.BelowOrEqual,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .InvalidProbeContract);
        EmitCmpMemoryDwordImmediate(
            ref writer,
            PlanRegister.Rax,
            0,
            0x534f_4bb8);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .InvalidProbeContract);
        EmitCmpMemoryWordImmediate(
            ref writer,
            PlanRegister.Rax,
            4,
            0xc34f);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .InvalidProbeContract);
        EmitTestRegImmediate32(
            ref writer,
            PlanRegister.R8,
            (uint)((1UL << 10) | (1UL << 1)));
        EmitMovRegReg(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.R8);
        EmitAndRegImmediate32(
            ref writer,
            PlanRegister.Rax,
            (uint)((1UL << 10) | (1UL << 1)));
        EmitCmpRegImmediate32(
            ref writer,
            PlanRegister.Rax,
            (uint)(1UL << 1));
        EmitPreCliGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus.InvalidState);
        writer.EndSemantic(StateExpectedSelfAddressOffset);

        writer.BeginSemantic(
            M01OwnedCr3CompositeTransitionOperation.ValidateMode,
            M01OwnedCr3CompositeTransitionRecoveryClass
                .PreCliNoStateMutation);
        EmitTestRegImmediate32(
            ref writer,
            PlanRegister.R8,
            (uint)RflagsTrapFlagBit);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus.InvalidMode);
        EmitPushReg(ref writer, PlanRegister.Rbx);
        EmitMovReg32Immediate(
            ref writer,
            PlanRegister.Rax,
            0x8000_0000);
        EmitCpuid(ref writer);
        EmitMovReg32Reg32(
            ref writer,
            PlanRegister.Rcx,
            PlanRegister.Rax);
        EmitPopReg(ref writer, PlanRegister.Rbx);
        EmitCmpReg32Immediate(
            ref writer,
            PlanRegister.Rcx,
            0x8000_0008);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.AboveOrEqual,
            M01OwnedCr3CompositeTransitionRuntimeStatus.InvalidMode);
        EmitPushReg(ref writer, PlanRegister.Rbx);
        EmitMovReg32Immediate(
            ref writer,
            PlanRegister.Rax,
            0x8000_0008);
        EmitCpuid(ref writer);
        EmitMovzxEcxAl(ref writer);
        EmitPopReg(ref writer, PlanRegister.Rbx);
        EmitCmpReg32Immediate(ref writer, PlanRegister.Rcx, 36);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.AboveOrEqual,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .PhysicalAddressWidthUnsupported);
        EmitCmpReg32Immediate(ref writer, PlanRegister.Rcx, 52);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.BelowOrEqual,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .PhysicalAddressWidthUnsupported);
        EmitMovReg32Immediate(ref writer, PlanRegister.Rsi, 1);
        EmitShiftLeftCl(ref writer, PlanRegister.Rsi);
        EmitDecReg(ref writer, PlanRegister.Rsi);
        EmitMovRegFromCr(ref writer, PlanRegister.R9, 0);
        EmitMovRegReg(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.R9);
        EmitMovRegImmediate64(
            ref writer,
            PlanRegister.Rdx,
            0x8001_0001);
        EmitAndRegReg(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.Rdx);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.Rdx);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus.InvalidMode);
        EmitMovRegFromCr(ref writer, PlanRegister.R10, 3);
        EmitMovRegFromCr(ref writer, PlanRegister.R11, 4);
        EmitMovRegReg(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.R10);
        EmitAndRegReg(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.Rsi);
        EmitAndRegImmediate32(
            ref writer,
            PlanRegister.Rax,
            0xffff_f000);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rdx,
            StateOwnedCr3RawOffset);
        EmitAndRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.Rsi);
        EmitAndRegImmediate32(
            ref writer,
            PlanRegister.Rdx,
            0xffff_f000);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.Rdx);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.NotEqual,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .InvalidOwnedCr3);
        EmitTestRegImmediate32(
            ref writer,
            PlanRegister.R11,
            (uint)(Cr4FiveLevelPagingBit | Cr4PcideBit));
        EmitPreCliGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus.InvalidMode);
        EmitTestRegImmediate32(
            ref writer,
            PlanRegister.R11,
            (uint)Cr4ControlFlowEnforcementBit);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus.InvalidMode);
        EmitTestRegImmediate32(
            ref writer,
            PlanRegister.R11,
            1U << 5);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.NotEqual,
            M01OwnedCr3CompositeTransitionRuntimeStatus.InvalidMode);
        EmitMovReg32Immediate(
            ref writer,
            PlanRegister.Rcx,
            0xc000_0080);
        EmitRdmsr(ref writer);
        // RDMSR writes EDX:EAX and each 32-bit write zero-extends.
        EmitShiftLeftImmediate32(
            ref writer,
            PlanRegister.Rdx,
            32);
        EmitOrRegReg(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.Rdx);
        EmitMovRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.Rax);
        EmitAndRegImmediate32(ref writer, PlanRegister.Rdx, 0x0d00);
        EmitCmpRegImmediate32(ref writer, PlanRegister.Rdx, 0x0d00);
        EmitPreCliGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus.InvalidMode);
        writer.EndSemantic(StateOriginalCr0Offset);

        writer.BeginSemantic(
            M01OwnedCr3CompositeTransitionOperation.DisableInterrupts,
            M01OwnedCr3CompositeTransitionRecoveryClass.RestoreRflags);
        EmitCli(ref writer);
        EmitMovStateFromReg(
            ref writer,
            StateOriginalEferOffset,
            PlanRegister.Rax);
        EmitMovStateFromReg(
            ref writer,
            StateOriginalRflagsOffset,
            PlanRegister.R8);
        EmitMovStateFromReg(
            ref writer,
            StateOriginalCr0Offset,
            PlanRegister.R9);
        EmitMovStateFromReg(
            ref writer,
            StateOriginalCr3RawOffset,
            PlanRegister.R10);
        EmitMovStateFromReg(
            ref writer,
            StateOriginalCr4RawOffset,
            PlanRegister.R11);
        EmitMovStateFromReg(
            ref writer,
            StatePhysicalAddressMaskOffset,
            PlanRegister.Rsi);
        EmitLeaRegFromRsp(ref writer, PlanRegister.Rax, 24);
        EmitMovStateFromReg(
            ref writer,
            StateEntryRspOffset,
            PlanRegister.Rax);
        EmitMovRegFromAddress(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.Rax);
        EmitMovStateFromReg(
            ref writer,
            StateObservedReturnAddressOffset,
            PlanRegister.Rdx);
        EmitLeaRip(ref writer, PlanRegister.Rdx);
        EmitMovStateFromReg(
            ref writer,
            StateObservedRipOffset,
            PlanRegister.Rdx);
        EmitPushfq(ref writer);
        EmitPopReg(ref writer, PlanRegister.Rax);
        EmitMovStateFromReg(
            ref writer,
            StateCliRflagsReadbackOffset,
            PlanRegister.Rax);
        EmitTestRegImmediate32(
            ref writer,
            PlanRegister.Rax,
            (uint)RflagsInterruptEnableBit);
        EmitRuntimeGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .InterruptDisableReadbackMismatch,
            restoreFlags,
            ref restoreFlagsCount);
        EmitProgressCheckpoint(
            ref writer,
            M01OwnedCr3CompositeTransitionCheckpoint.InterruptsDisabled);
        EmitMovReg32Immediate(
            ref writer,
            PlanRegister.R12,
            (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success);
        writer.EndSemantic(StateCliRflagsReadbackOffset);

        writer.BeginSemantic(
            M01OwnedCr3CompositeTransitionOperation.ClearPge,
            M01OwnedCr3CompositeTransitionRecoveryClass
                .RestoreCr4ThenRflags);
        EmitMovRegReg(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.R11);
        EmitAndRegImmediate32(
            ref writer,
            PlanRegister.Rax,
            unchecked((uint)~Cr4PageGlobalEnableBit));
        EmitMovCrFromReg(ref writer, 4, PlanRegister.Rax);
        EmitMovRegFromCr(ref writer, PlanRegister.Rax, 4);
        EmitMovStateFromReg(
            ref writer,
            StateClearedCr4ReadbackOffset,
            PlanRegister.Rax);
        EmitMovRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.R11);
        EmitAndRegImmediate32(
            ref writer,
            PlanRegister.Rdx,
            unchecked((uint)~Cr4PageGlobalEnableBit));
        EmitCmpRegReg(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.Rdx);
        EmitRuntimeGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .PgeClearReadbackMismatch,
            restoreCr4,
            ref restoreCr4Count);
        EmitMovRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.R11);
        EmitShiftRightImmediate(ref writer, PlanRegister.Rdx, 7);
        EmitAndRegImmediate32(ref writer, PlanRegister.Rdx, 1);
        EmitMovStateByteFromReg(
            ref writer,
            StatePgeWasClearedOffset,
            PlanRegister.Rdx);
        EmitProgressCheckpoint(
            ref writer,
            M01OwnedCr3CompositeTransitionCheckpoint.PgeCleared);
        writer.EndSemantic(StateClearedCr4ReadbackOffset);

        writer.BeginSemantic(
            M01OwnedCr3CompositeTransitionOperation.LoadOwnedCr3,
            M01OwnedCr3CompositeTransitionRecoveryClass
                .RestoreCr3ThenCr4ThenRflags);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rax,
            StateOwnedCr3RawOffset);
        EmitMovCrFromReg(ref writer, 3, PlanRegister.Rax);
        EmitMovRegFromCr(ref writer, PlanRegister.Rax, 3);
        EmitMovStateFromReg(
            ref writer,
            StateOwnedCr3ReadbackOffset,
            PlanRegister.Rax);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rdx,
            StateOwnedCr3RawOffset);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.Rdx);
        EmitRuntimeGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .OwnedCr3ReadbackMismatch,
            restoreCr3,
            ref restoreCr3Count);
        EmitMovStateByteImmediate(
            ref writer,
            StateOwnedRootWasLoadedOffset,
            1);
        EmitProgressCheckpoint(
            ref writer,
            M01OwnedCr3CompositeTransitionCheckpoint.OwnedCr3Loaded);
        writer.EndSemantic(StateOwnedCr3ReadbackOffset);

        writer.BeginSemantic(
            M01OwnedCr3CompositeTransitionOperation.UpdateCanary,
            M01OwnedCr3CompositeTransitionRecoveryClass
                .RestoreCr3ThenCr4ThenRflags);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.R11,
            StateCanaryAddressOffset);
        EmitMovRegFromAddress(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.R11);
        EmitMovStateFromReg(
            ref writer,
            StateCanaryBeforeOffset,
            PlanRegister.Rax);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rdx,
            StateExpectedCanaryOffset);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.Rdx);
        EmitRuntimeGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .CanaryBeforeMismatch,
            restoreCr3,
            ref restoreCr3Count);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rax,
            StateReplacementCanaryOffset);
        EmitMovAddressFromReg(
            ref writer,
            PlanRegister.R11,
            PlanRegister.Rax);
        EmitMovRegFromAddress(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.R11);
        EmitMovStateFromReg(
            ref writer,
            StateCanaryAfterOffset,
            PlanRegister.Rax);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rdx,
            StateReplacementCanaryOffset);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.Rdx);
        EmitRuntimeGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .CanaryAfterMismatch,
            restoreCr3,
            ref restoreCr3Count);
        EmitProgressCheckpoint(
            ref writer,
            M01OwnedCr3CompositeTransitionCheckpoint.CanaryUpdated);
        writer.EndSemantic(StateCanaryAfterOffset);

        writer.BeginSemantic(
            M01OwnedCr3CompositeTransitionOperation
                .WalkProbeHierarchy,
            M01OwnedCr3CompositeTransitionRecoveryClass
                .RestoreCr3ThenCr4ThenRflags);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rdx,
            StateOwnedCr3RawOffset);
        EmitAndRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.Rsi);
        EmitAndRegImmediate32(
            ref writer,
            PlanRegister.Rdx,
            0xffff_f000);
        EmitWalkParent(
            ref writer,
            39,
            ProbePdptPageOrdinalShift,
            StateProbePml4eOffset,
            restoreCr3,
            ref restoreCr3Count);
        EmitWalkParent(
            ref writer,
            30,
            ProbePdPageOrdinalShift,
            StateProbePdpteOffset,
            restoreCr3,
            ref restoreCr3Count);
        EmitWalkParent(
            ref writer,
            21,
            ProbePtPageOrdinalShift,
            StateProbePdeOffset,
            restoreCr3,
            ref restoreCr3Count);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rcx,
            StateProbeEntryOffset);
        EmitShiftRightImmediate(ref writer, PlanRegister.Rcx, 12);
        EmitAndRegImmediate32(ref writer, PlanRegister.Rcx, 0x1ff);
        EmitLeaRdxFromRdxRcx8(ref writer);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rax,
            StateExpectedProbePteAddressOffset);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.Rax);
        EmitRuntimeGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .ProbePteAddressMismatch,
            restoreCr3,
            ref restoreCr3Count);
        EmitProgressCheckpoint(
            ref writer,
            M01OwnedCr3CompositeTransitionCheckpoint.ProbePteLocated);
        EmitMovRegFromAddress(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.Rdx);
        EmitMovStateFromReg(
            ref writer,
            StateProbePteBeforeOffset,
            PlanRegister.Rax);
        EmitMovRegReg(
            ref writer,
            PlanRegister.R9,
            PlanRegister.Rax);
        EmitBtrRegImmediate(ref writer, PlanRegister.R9, 5);
        EmitBtrRegImmediate(ref writer, PlanRegister.R9, 6);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rcx,
            StateInitialProbePteRawOffset);
        EmitBtrRegImmediate(ref writer, PlanRegister.Rcx, 5);
        EmitBtrRegImmediate(ref writer, PlanRegister.Rcx, 6);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.R9,
            PlanRegister.Rcx);
        EmitRuntimeGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .ProbePteBeforeMismatch,
            restoreCr3,
            ref restoreCr3Count);
        EmitMovRegReg(
            ref writer,
            PlanRegister.R9,
            PlanRegister.Rax);
        EmitAndRegImmediate32(
            ref writer,
            PlanRegister.R9,
            (uint)(PageAccessedBit | PageDirtyBit));
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rcx,
            StateInitialProbePteRawOffset);
        EmitAndRegImmediate32(
            ref writer,
            PlanRegister.Rcx,
            (uint)(PageAccessedBit | PageDirtyBit));
        EmitAndRegReg(
            ref writer,
            PlanRegister.R9,
            PlanRegister.Rcx);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.R9,
            PlanRegister.Rcx);
        EmitRuntimeGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .ProbePteBeforeMismatch,
            restoreCr3,
            ref restoreCr3Count);
        writer.EndSemantic(StateExpectedProbePteAddressOffset);

        writer.BeginSemantic(
            M01OwnedCr3CompositeTransitionOperation.CommitProbePte,
            M01OwnedCr3CompositeTransitionRecoveryClass
                .MutationAttemptedThenRestoreRegisters);
        EmitMovRegReg(
            ref writer,
            PlanRegister.R9,
            PlanRegister.Rax);
        EmitBtrRegImmediate(ref writer, PlanRegister.R9, 1);
        EmitBtrRegImmediate(ref writer, PlanRegister.R9, 63);
        EmitMovAddressFromReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.R9);
        writer.EndSemantic(PageWritableBit | PageNoExecuteBit);

        writer.BeginSemantic(
            M01OwnedCr3CompositeTransitionOperation
                .InvalidateProbePage,
            M01OwnedCr3CompositeTransitionRecoveryClass
                .MutationAttemptedThenRestoreRegisters);
        EmitMfence(ref writer);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rcx,
            StateProbeEntryOffset);
        EmitInvlpgRcx(ref writer);
        EmitMovStateFromReg(
            ref writer,
            StateInvlpgTargetOffset,
            PlanRegister.Rcx);
        EmitMovRegImmediate64(ref writer, PlanRegister.Rax, 1);
        EmitMovStateFromReg(
            ref writer,
            StateInvlpgCountOffset,
            PlanRegister.Rax);
        EmitProgressCheckpoint(
            ref writer,
            M01OwnedCr3CompositeTransitionCheckpoint.ProbeInvalidated);
        writer.EndSemantic(ExactInvlpgCount);

        writer.BeginSemantic(
            M01OwnedCr3CompositeTransitionOperation.VerifyProbePte,
            M01OwnedCr3CompositeTransitionRecoveryClass
                .MutationAttemptedThenRestoreRegisters);
        EmitMovRegFromAddress(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.Rdx);
        EmitMovStateFromReg(
            ref writer,
            StateProbePteAfterOffset,
            PlanRegister.Rax);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.R9);
        EmitRuntimeGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .ProbePteAfterMismatch,
            restoreCr3,
            ref restoreCr3Count);
        EmitMovStateFromReg(
            ref writer,
            StateCommittedProbePteReadbackOffset,
            PlanRegister.Rax);
        EmitMovStateByteImmediate(
            ref writer,
            StateMutationCommittedOffset,
            1);
        EmitProgressCheckpoint(
            ref writer,
            M01OwnedCr3CompositeTransitionCheckpoint.ProbePteVerified);
        writer.EndSemantic(StateProbePteAfterOffset);

        writer.BeginSemantic(
            M01OwnedCr3CompositeTransitionOperation.InvokeExactProbe,
            M01OwnedCr3CompositeTransitionRecoveryClass
                .CommittedProbeRemainsReadOnlyExecutableThenRestoreRegisters);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.R11,
            StateProbeEntryOffset);
        EmitSubRegImmediate8(ref writer, PlanRegister.Rsp, 32);
        EmitCallReg(ref writer, PlanRegister.R11);
        EmitAddRegImmediate8(ref writer, PlanRegister.Rsp, 32);
        EmitMovStateFromReg(
            ref writer,
            StateProbeReturnOffset,
            PlanRegister.Rax);
        EmitMovRegImmediate64(ref writer, PlanRegister.Rdx, 1);
        EmitMovStateFromReg(
            ref writer,
            StateProbeInvocationCountOffset,
            PlanRegister.Rdx);
        EmitMovStateByteImmediate(
            ref writer,
            StateProbeReturnedOffset,
            1);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rdx,
            StateExpectedProbeReturnOffset);
        EmitCmpReg32Immediate(
            ref writer,
            PlanRegister.Rax,
            ExactProbeReturnValue);
        EmitRuntimeGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .ProbeReturnMismatch,
            restoreCr3,
            ref restoreCr3Count);
        EmitProgressCheckpoint(
            ref writer,
            M01OwnedCr3CompositeTransitionCheckpoint.ProbeReturned);
        writer.EndSemantic(ExactProbeReturnValue);

        writer.BeginSemantic(
            M01OwnedCr3CompositeTransitionOperation.AuditPageZero,
            M01OwnedCr3CompositeTransitionRecoveryClass
                .CommittedProbeRemainsReadOnlyExecutableThenRestoreRegisters);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rdx,
            StateOwnedCr3RawOffset);
        EmitAndRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.Rsi);
        EmitAndRegImmediate32(
            ref writer,
            PlanRegister.Rdx,
            0xffff_f000);
        ulong* absentFixups = stackalloc ulong[4];
        ulong absentCount = 0;
        EmitPageZeroParent(
            ref writer,
            StatePageZeroPml4eOffset,
            M01OwnedCr3CompositeTransitionPageZeroLevel.AbsentAtPml4,
            restoreCr3,
            ref restoreCr3Count,
            absentFixups,
            ref absentCount);
        EmitPageZeroParent(
            ref writer,
            StatePageZeroPdpteOffset,
            M01OwnedCr3CompositeTransitionPageZeroLevel.AbsentAtPdpt,
            restoreCr3,
            ref restoreCr3Count,
            absentFixups,
            ref absentCount);
        EmitPageZeroParent(
            ref writer,
            StatePageZeroPdeOffset,
            M01OwnedCr3CompositeTransitionPageZeroLevel.AbsentAtPd,
            restoreCr3,
            ref restoreCr3Count,
            absentFixups,
            ref absentCount);
        EmitMovRegFromAddress(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.Rdx);
        EmitMovStateFromReg(
            ref writer,
            StatePageZeroPteOffset,
            PlanRegister.Rax);
        EmitMovStateByteImmediate(
            ref writer,
            StatePageZeroLevelOffset,
            (byte)M01OwnedCr3CompositeTransitionPageZeroLevel
                .Malformed);
        EmitMovReg32Immediate(
            ref writer,
            PlanRegister.R8,
            (uint)M01OwnedCr3CompositeTransitionPageZeroLevel
                .AbsentAtPt);
        EmitTestRegReg(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.Rax);
        absentFixups[absentCount++] =
            EmitJcc(ref writer, PlanCondition.Equal);
        EmitTestRegImmediate32(
            ref writer,
            PlanRegister.Rax,
            (uint)PagePresentBit);
        EmitRuntimeGuard(
            ref writer,
            PlanCondition.NotEqual,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .PageZeroMalformed,
            restoreCr3,
            ref restoreCr3Count);
        EmitMovStateByteImmediate(
            ref writer,
            StatePageZeroLevelOffset,
            (byte)M01OwnedCr3CompositeTransitionPageZeroLevel.Present);
        EmitMovReg32Immediate(
            ref writer,
            PlanRegister.R12,
            (uint)M01OwnedCr3CompositeTransitionRuntimeStatus
                .PageZeroPresent);
        restoreCr3[restoreCr3Count++] = EmitJump(ref writer);
        ulong pageZeroAbsent = writer.Position;
        for (ulong index = 0; index < absentCount; index++)
        {
            writer.PatchRel32(absentFixups[index], pageZeroAbsent);
        }
        EmitMovStateByteFromReg(
            ref writer,
            StatePageZeroLevelOffset,
            PlanRegister.R8);
        EmitProgressCheckpoint(
            ref writer,
            M01OwnedCr3CompositeTransitionCheckpoint.PageZeroAbsent);
        restoreCr3[restoreCr3Count++] = EmitJump(ref writer);
        writer.EndSemantic(StatePageZeroPml4eOffset);

        ulong cleanupCr3 = writer.Position;
        for (ulong index = 0; index < restoreCr3Count; index++)
        {
            writer.PatchRel32(restoreCr3[index], cleanupCr3);
        }
        writer.BeginSemantic(
            M01OwnedCr3CompositeTransitionOperation
                .RestoreFirmwareCr3,
            M01OwnedCr3CompositeTransitionRecoveryClass
                .SuccessRestoreRegisters);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rax,
            StateOriginalCr3RawOffset);
        EmitMovCrFromReg(ref writer, 3, PlanRegister.Rax);
        EmitMovRegFromCr(ref writer, PlanRegister.Rax, 3);
        EmitMovStateFromReg(
            ref writer,
            StateFirmwareCr3ReadbackOffset,
            PlanRegister.Rax);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rdx,
            StateOriginalCr3RawOffset);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.Rdx);
        ulong cr3Matched = EmitJcc(ref writer, PlanCondition.Equal);
        EmitMovReg32Immediate(
            ref writer,
            PlanRegister.R12,
            (uint)M01OwnedCr3CompositeTransitionRuntimeStatus
                .FirmwareCr3ReadbackMismatch);
        ulong skipPostCanary = EmitJump(ref writer);
        writer.PatchRel32(cr3Matched, writer.Position);
        EmitMovStateByteImmediate(
            ref writer,
            StateCheckpointOffset,
            (byte)M01OwnedCr3CompositeTransitionCheckpoint
                .FirmwareCr3Restored);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.R11,
            StateCanaryAddressOffset);
        EmitMovRegFromAddress(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.R11);
        EmitMovStateFromReg(
            ref writer,
            StatePostRestoreCanaryOffset,
            PlanRegister.Rax);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rdx,
            StateReplacementCanaryOffset);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.Rdx);
        ulong canaryMatched = EmitJcc(ref writer, PlanCondition.Equal);
        EmitCmpReg32Immediate(
            ref writer,
            PlanRegister.R12,
            (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success);
        ulong keepPrimary = EmitJcc(ref writer, PlanCondition.NotEqual);
        EmitMovReg32Immediate(
            ref writer,
            PlanRegister.R12,
            (uint)M01OwnedCr3CompositeTransitionRuntimeStatus
                .PostRestoreCanaryMismatch);
        writer.PatchRel32(canaryMatched, writer.Position);
        writer.PatchRel32(keepPrimary, writer.Position);
        writer.EndSemantic(StateFirmwareCr3ReadbackOffset);

        ulong cleanupCr4 = writer.Position;
        writer.PatchRel32(skipPostCanary, cleanupCr4);
        for (ulong index = 0; index < restoreCr4Count; index++)
        {
            writer.PatchRel32(restoreCr4[index], cleanupCr4);
        }
        writer.BeginSemantic(
            M01OwnedCr3CompositeTransitionOperation.RestoreCr4,
            M01OwnedCr3CompositeTransitionRecoveryClass
                .SuccessRestoreRegisters);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rax,
            StateOriginalCr4RawOffset);
        EmitMovCrFromReg(ref writer, 4, PlanRegister.Rax);
        EmitMovRegFromCr(ref writer, PlanRegister.Rax, 4);
        EmitMovStateFromReg(
            ref writer,
            StateRestoredCr4Offset,
            PlanRegister.Rax);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rdx,
            StateOriginalCr4RawOffset);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.Rdx);
        ulong cr4Matched = EmitJcc(ref writer, PlanCondition.Equal);
        EmitMovReg32Immediate(
            ref writer,
            PlanRegister.R12,
            (uint)M01OwnedCr3CompositeTransitionRuntimeStatus
                .Cr4RestoreReadbackMismatch);
        ulong skipCr4Checkpoint = EmitJump(ref writer);
        writer.PatchRel32(cr4Matched, writer.Position);
        EmitMovStateByteImmediate(
            ref writer,
            StateCheckpointOffset,
            (byte)M01OwnedCr3CompositeTransitionCheckpoint.Cr4Restored);
        writer.EndSemantic(StateRestoredCr4Offset);

        ulong cleanupFlags = writer.Position;
        writer.PatchRel32(skipCr4Checkpoint, cleanupFlags);
        for (ulong index = 0; index < restoreFlagsCount; index++)
        {
            writer.PatchRel32(restoreFlags[index], cleanupFlags);
        }
        writer.BeginSemantic(
            M01OwnedCr3CompositeTransitionOperation.RestoreRflags,
            M01OwnedCr3CompositeTransitionRecoveryClass
                .SuccessRestoreRegisters);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rax,
            StateOriginalRflagsOffset);
        EmitMovStateFromReg(
            ref writer,
            StateRestoredRflagsIntentOffset,
            PlanRegister.Rax);
        EmitLeaRegFromRsp(ref writer, PlanRegister.Rax, 24);
        EmitMovStateFromReg(
            ref writer,
            StateExitRspOffset,
            PlanRegister.Rax);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rdx,
            StateEntryRspOffset);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.Rdx);
        ulong rspMatched = EmitJcc(ref writer, PlanCondition.Equal);
        EmitMovReg32Immediate(
            ref writer,
            PlanRegister.R12,
            (uint)M01OwnedCr3CompositeTransitionRuntimeStatus
                .RspRestoreMismatch);
        writer.PatchRel32(rspMatched, writer.Position);
        writer.EndSemantic(StateRestoredRflagsIntentOffset);

        writer.BeginSemantic(
            M01OwnedCr3CompositeTransitionOperation.Return,
            M01OwnedCr3CompositeTransitionRecoveryClass
                .SuccessRestoreRegisters);
        EmitMovStateByteImmediate(
            ref writer,
            StateCheckpointOffset,
            (byte)M01OwnedCr3CompositeTransitionCheckpoint
                .ReturnPrepared);
        EmitMovStateByteFromReg(
            ref writer,
            StateRuntimeStatusOffset,
            PlanRegister.R12);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rdx,
            StateOriginalRflagsOffset);
        EmitMovReg32Reg32(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.R12);
        EmitPopReg(ref writer, PlanRegister.R12);
        EmitPopReg(ref writer, PlanRegister.Rsi);
        EmitPopReg(ref writer, PlanRegister.Rbx);
        EmitPushReg(ref writer, PlanRegister.Rdx);
        EmitPopfq(ref writer);
        EmitRet(ref writer);
        writer.EndSemantic(StateRuntimeStatusOffset);
    }

    private static void EmitAddRegReg(
        ref PlanWriter writer,
        PlanRegister destination,
        PlanRegister source)
    {
        writer.Instruction();
        byte destinationValue = (byte)destination;
        byte sourceValue = (byte)source;
        writer.Byte((byte)(
            0x48
            | (sourceValue >= 8 ? 4 : 0)
            | (destinationValue >= 8 ? 1 : 0)));
        writer.Byte(0x01);
        writer.Byte((byte)(
            0xc0
            | ((sourceValue & 7) << 3)
            | (destinationValue & 7)));
    }

    private static void EmitAddRegImmediate32(
        ref PlanWriter writer,
        PlanRegister register,
        uint value)
    {
        writer.Instruction();
        byte registerValue = (byte)register;
        writer.Byte((byte)(0x48 | (registerValue >= 8 ? 1 : 0)));
        writer.Byte(0x81);
        writer.Byte((byte)(0xc0 | (registerValue & 7)));
        writer.UInt32(value);
    }

    private static void EmitShiftLeftImmediate32(
        ref PlanWriter writer,
        PlanRegister register,
        byte count)
    {
        writer.Instruction();
        byte registerValue = (byte)register;
        writer.Byte((byte)(0x48 | (registerValue >= 8 ? 1 : 0)));
        writer.Byte(0xc1);
        writer.Byte((byte)(0xe0 | (registerValue & 7)));
        writer.Byte(count);
    }

    private static void EmitWalkParent(
        ref PlanWriter writer,
        byte shift,
        byte expectedPageOrdinalShift,
        uint readbackOffset,
        ulong* cleanupFixups,
        ref ulong cleanupFixupCount)
    {
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rcx,
            StateProbeEntryOffset);
        EmitShiftRightImmediate(ref writer, PlanRegister.Rcx, shift);
        EmitAndRegImmediate32(ref writer, PlanRegister.Rcx, 0x1ff);
        EmitMovRdxFromRdxRcx8(ref writer);
        EmitMovStateFromReg(
            ref writer,
            readbackOffset,
            PlanRegister.Rdx);
        EmitMovRegReg(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.Rsi);
        EmitAndRegImmediate32(
            ref writer,
            PlanRegister.Rax,
            0xffff_f000);
        EmitMovRegImmediate64(
            ref writer,
            PlanRegister.Rcx,
            PagePresentBit | PageWritableBit | PageAccessedBit);
        EmitOrRegReg(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.Rcx);
        EmitMovRegReg(
            ref writer,
            PlanRegister.R9,
            PlanRegister.Rdx);
        EmitAndRegReg(
            ref writer,
            PlanRegister.R9,
            PlanRegister.Rax);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.R9,
            PlanRegister.Rdx);
        EmitRuntimeGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .ProbeHierarchyMalformed,
            cleanupFixups,
            ref cleanupFixupCount);
        EmitMovRegReg(
            ref writer,
            PlanRegister.R9,
            PlanRegister.Rdx);
        EmitAndRegImmediate32(
            ref writer,
            PlanRegister.R9,
            (uint)(PagePresentBit | PageWritableBit));
        EmitCmpRegImmediate32(
            ref writer,
            PlanRegister.R9,
            (uint)(PagePresentBit | PageWritableBit));
        EmitRuntimeGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .ProbeHierarchyMalformed,
            cleanupFixups,
            ref cleanupFixupCount);
        EmitAndRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.Rsi);
        EmitAndRegImmediate32(
            ref writer,
            PlanRegister.Rdx,
            0xffff_f000);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rax,
            StateTablePageArenaStartOffset);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.Rax);
        EmitRuntimeGuard(
            ref writer,
            PlanCondition.AboveOrEqual,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .ProbeHierarchyMalformed,
            cleanupFixups,
            ref cleanupFixupCount);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rax,
            StateTablePageArenaEndOffset);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.Rax);
        EmitRuntimeGuard(
            ref writer,
            PlanCondition.Below,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .ProbeHierarchyMalformed,
            cleanupFixups,
            ref cleanupFixupCount);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rax,
            StateExpectedProbeHierarchyIdentityOffset);
        EmitShiftRightImmediate(
            ref writer,
            PlanRegister.Rax,
            expectedPageOrdinalShift);
        EmitAndRegImmediate32(
            ref writer,
            PlanRegister.Rax,
            (uint)ProbeHierarchyPageOrdinalMask);
        EmitShiftLeftImmediate32(
            ref writer,
            PlanRegister.Rax,
            12);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.R9,
            StateTablePageArenaStartOffset);
        EmitAddRegReg(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.R9);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.Rax);
        EmitRuntimeGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .ProbePteAddressMismatch,
            cleanupFixups,
            ref cleanupFixupCount);
    }

    private static void EmitPageZeroParent(
        ref PlanWriter writer,
        uint readbackOffset,
        M01OwnedCr3CompositeTransitionPageZeroLevel absentLevel,
        ulong* cleanupFixups,
        ref ulong cleanupFixupCount,
        ulong* absentFixups,
        ref ulong absentFixupCount)
    {
        EmitMovStateByteImmediate(
            ref writer,
            StatePageZeroLevelOffset,
            (byte)M01OwnedCr3CompositeTransitionPageZeroLevel
                .Malformed);
        EmitMovRegFromAddress(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.Rdx);
        EmitMovStateFromReg(
            ref writer,
            readbackOffset,
            PlanRegister.Rax);
        EmitMovReg32Immediate(
            ref writer,
            PlanRegister.R8,
            (uint)absentLevel);
        EmitTestRegReg(
            ref writer,
            PlanRegister.Rax,
            PlanRegister.Rax);
        absentFixups[absentFixupCount++] =
            EmitJcc(ref writer, PlanCondition.Equal);
        EmitMovRegReg(
            ref writer,
            PlanRegister.R9,
            PlanRegister.Rsi);
        EmitAndRegImmediate32(
            ref writer,
            PlanRegister.R9,
            0xffff_f000);
        EmitMovRegImmediate64(
            ref writer,
            PlanRegister.Rcx,
            PagePresentBit | PageWritableBit | PageAccessedBit);
        EmitOrRegReg(
            ref writer,
            PlanRegister.R9,
            PlanRegister.Rcx);
        EmitMovRegReg(
            ref writer,
            PlanRegister.Rcx,
            PlanRegister.Rax);
        EmitAndRegReg(
            ref writer,
            PlanRegister.Rcx,
            PlanRegister.R9);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.Rcx,
            PlanRegister.Rax);
        EmitRuntimeGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .PageZeroMalformed,
            cleanupFixups,
            ref cleanupFixupCount);
        EmitMovRegReg(
            ref writer,
            PlanRegister.Rcx,
            PlanRegister.Rax);
        EmitAndRegImmediate32(
            ref writer,
            PlanRegister.Rcx,
            (uint)(PagePresentBit | PageWritableBit));
        EmitCmpRegImmediate32(
            ref writer,
            PlanRegister.Rcx,
            (uint)(PagePresentBit | PageWritableBit));
        EmitRuntimeGuard(
            ref writer,
            PlanCondition.Equal,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .PageZeroMalformed,
            cleanupFixups,
            ref cleanupFixupCount);
        EmitMovRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.Rax);
        EmitAndRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.Rsi);
        EmitAndRegImmediate32(
            ref writer,
            PlanRegister.Rdx,
            0xffff_f000);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rcx,
            StateTablePageArenaStartOffset);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.Rcx);
        EmitRuntimeGuard(
            ref writer,
            PlanCondition.AboveOrEqual,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .PageZeroMalformed,
            cleanupFixups,
            ref cleanupFixupCount);
        EmitMovRegFromState(
            ref writer,
            PlanRegister.Rcx,
            StateTablePageArenaEndOffset);
        EmitCmpRegReg(
            ref writer,
            PlanRegister.Rdx,
            PlanRegister.Rcx);
        EmitRuntimeGuard(
            ref writer,
            PlanCondition.Below,
            M01OwnedCr3CompositeTransitionRuntimeStatus
                .PageZeroMalformed,
            cleanupFixups,
            ref cleanupFixupCount);
    }

    private static M01OwnedCr3CompositeTransitionStatus
        ValidatePlanStorage(
            M01OwnedCr3CompositeTransitionRuntimeState* preparedState,
            ulong preparedStateCount,
            ulong preparedStateCapacity,
            byte* bytes,
            ulong byteCapacity,
            ulong entryAddress,
            M01OwnedCr3CompositeTransitionSemanticRecord* semantics,
            ulong semanticCapacity,
            bool isReadback,
            ulong byteCount,
            ulong semanticCount)
    {
        if (preparedState == null)
        {
            return M01OwnedCr3CompositeTransitionStatus
                .MissingObservedState;
        }

        if (preparedStateCount != 1)
        {
            return M01OwnedCr3CompositeTransitionStatus
                .InvalidObservedStateCount;
        }

        if (preparedStateCapacity != 1)
        {
            return M01OwnedCr3CompositeTransitionStatus
                .ObservedStateCapacityTooSmall;
        }

        ulong stateStart = (ulong)preparedState;
        if (stateStart
            > ~0UL
                - M01OwnedCr3CompositeTransitionRuntimeState
                    .StructureSize)
        {
            return M01OwnedCr3CompositeTransitionStatus
                .MisalignedObservedState;
        }

        ulong stateEnd = stateStart
            + M01OwnedCr3CompositeTransitionRuntimeState.StructureSize;
        if ((stateStart & (RequiredPointerAlignment - 1)) != 0
            || stateEnd <= stateStart)
        {
            return M01OwnedCr3CompositeTransitionStatus
                .MisalignedObservedState;
        }

        M01OwnedCr3CompositeTransitionRuntimeState state =
            *preparedState;
        if (state.ExpectedSelfAddress != stateStart
            || !IsCanonicalPreparedState(preparedState))
        {
            return M01OwnedCr3CompositeTransitionStatus
                .InvalidProbeContract;
        }

        if (bytes == null)
        {
            return isReadback
                ? M01OwnedCr3CompositeTransitionStatus.MissingSource
                : M01OwnedCr3CompositeTransitionStatus
                    .MissingDestination;
        }

        if (byteCapacity != PageSize)
        {
            return byteCapacity < PageSize
                ? isReadback
                    ? M01OwnedCr3CompositeTransitionStatus
                        .SourceCapacityTooSmall
                    : M01OwnedCr3CompositeTransitionStatus
                        .DestinationCapacityTooSmall
                : M01OwnedCr3CompositeTransitionStatus
                    .CapacityExceedsLimit;
        }

        ulong byteStart = (ulong)bytes;
        if (byteStart > ~0UL - byteCapacity)
        {
            return isReadback
                ? M01OwnedCr3CompositeTransitionStatus
                    .SourceSpanOverflow
                : M01OwnedCr3CompositeTransitionStatus
                    .EntryAddressOverflow;
        }

        ulong byteEnd = byteStart + byteCapacity;
        if (byteStart != entryAddress
            || entryAddress != state.CompositeCodeStart
            || entryAddress > ~0UL - PageSize
            || state.CompositeCodeEndExclusive
                != entryAddress + PageSize
            || (entryAddress & (EntryAlignment - 1)) != 0)
        {
            return M01OwnedCr3CompositeTransitionStatus
                .InvalidEntryAddress;
        }

        if (semantics == null)
        {
            return isReadback
                ? M01OwnedCr3CompositeTransitionStatus
                    .MissingSemanticSource
                : M01OwnedCr3CompositeTransitionStatus
                    .MissingSemanticDestination;
        }

        if (semanticCapacity != AuditedSemanticRecordCount)
        {
            return M01OwnedCr3CompositeTransitionStatus
                .SemanticCapacityTooSmall;
        }

        ulong semanticStart = (ulong)semantics;
        ulong semanticBytes =
            semanticCapacity
            * (ulong)sizeof(
                M01OwnedCr3CompositeTransitionSemanticRecord);
        if ((semanticStart & (RequiredPointerAlignment - 1)) != 0
            || semanticStart > ~0UL - semanticBytes)
        {
            return M01OwnedCr3CompositeTransitionStatus
                .MisalignedSemanticDestination;
        }

        ulong semanticEnd = semanticStart + semanticBytes;
        if (RangesOverlap(
                byteStart,
                byteEnd,
                semanticStart,
                semanticEnd)
            || RangesOverlap(
                stateStart,
                stateEnd,
                byteStart,
                byteEnd)
            || RangesOverlap(
                stateStart,
                stateEnd,
                semanticStart,
                semanticEnd))
        {
            return M01OwnedCr3CompositeTransitionStatus
                .DestinationSemanticOverlap;
        }

        if (isReadback)
        {
            if (byteCount != MaterializedPageByteLength)
            {
                return M01OwnedCr3CompositeTransitionStatus
                    .InvalidSourceCount;
            }

            if (semanticCount != AuditedSemanticRecordCount)
            {
                return M01OwnedCr3CompositeTransitionStatus
                    .InvalidSemanticCount;
            }
        }

        return M01OwnedCr3CompositeTransitionStatus.Success;
    }

    private static bool IsCanonicalPreparedState(
        M01OwnedCr3CompositeTransitionRuntimeState* statePointer)
    {
        M01OwnedCr3CompositeTransitionRuntimeState state =
            *statePointer;
        ulong stateStructureEnd = TryAdd(
            state.ExpectedSelfAddress,
            M01OwnedCr3CompositeTransitionRuntimeState.StructureSize);
        ulong canaryEnd = TryAdd(state.CanaryAddress, sizeof(ulong));
        ulong expectedProbeEnd = TryAdd(
            state.ProbeEntry,
            ExactProbeByteLength);
        ulong expectedPteEnd = TryAdd(
            state.ExpectedProbePteAddress,
            sizeof(ulong));
        ulong ownedRootBase =
            state.OwnedCr3Raw & Cr3ArchitecturalBaseMask;
        bool expectedHierarchyIsCanonical =
            TryGetExpectedProbeHierarchyPages(
                state,
                out ulong expectedPdptPage,
                out ulong expectedPdPage,
                out ulong expectedPtPage);
        if (!state.IsInitialized
            || state.Size
                != M01OwnedCr3CompositeTransitionRuntimeState
                    .StructureSize
            || state.Version
                != M01OwnedCr3CompositeTransitionRuntimeState
                    .StructureVersion
            || !state.ReservedFieldsAreZero
            || state.ProhibitedFirmwareCallbackCount != 0
            || state.ExpectedSelfAddress == 0
            || state.ExpectedCriticalMappingPageCount == 0
            || state.ExpectedCriticalMappingPageCount
                > MaximumCriticalMappingPageCount
            || !expectedHierarchyIsCanonical
            || expectedPdptPage == expectedPdPage
            || expectedPdptPage == expectedPtPage
            || expectedPdPage == expectedPtPage
            || !IsCanonicalPageRange(
                state.CompositeCodeStart,
                state.CompositeCodeEndExclusive,
                exactlyOnePage: true)
            || !IsCanonicalPageRange(
                state.ContinuationPageStart,
                state.ContinuationPageEndExclusive,
                exactlyOnePage: true)
            || !IsCanonicalPageRange(
                state.StackStart,
                state.StackEndExclusive,
                exactlyOnePage: false)
            || !IsCanonicalPageRange(
                state.ProbePageStart,
                state.ProbePageEndExclusive,
                exactlyOnePage: true)
            || !IsCanonicalPageRange(
                state.TablePageArenaStart,
                state.TablePageArenaEndExclusive,
                exactlyOnePage: false)
            || !IsCanonicalPageRange(
                state.TableStateArenaStart,
                state.TableStateArenaEndExclusive,
                exactlyOnePage: true)
            || state.TablePageArenaEndExclusive
                != state.TableStateArenaStart
            || state.TableStateArenaStart
                != state.ExpectedSelfAddress
            || stateStructureEnd == 0
            || !ContainsRange(
                state.TableStateArenaStart,
                state.TableStateArenaEndExclusive,
                state.ExpectedSelfAddress,
                stateStructureEnd)
            || state.CanaryAddress
                != stateStructureEnd
            || canaryEnd == 0
            || !ContainsRange(
                state.TableStateArenaStart,
                state.TableStateArenaEndExclusive,
                state.CanaryAddress,
                canaryEnd)
            || !ContainsAddress(
                state.ContinuationPageStart,
                state.ContinuationPageEndExclusive,
                state.ExpectedReturnAddress)
            || state.ExpectedReturnAddress
                < state.CompositeCodeStart + RequiredByteLength
            || expectedProbeEnd == 0
            || !ContainsRange(
                state.ProbePageStart,
                state.ProbePageEndExclusive,
                state.ProbeEntry,
                expectedProbeEnd)
            || (state.ExpectedProbePteAddress
                    & (RequiredPointerAlignment - 1)) != 0
            || expectedPteEnd == 0
            || !ContainsRange(
                state.TablePageArenaStart,
                state.TablePageArenaEndExclusive,
                state.ExpectedProbePteAddress,
                expectedPteEnd)
            || ownedRootBase == 0
            || (state.OwnedCr3Raw
                    & ~(Cr3ArchitecturalBaseMask
                        | Cr3PermittedLowBitsMask)) != 0
            || (ownedRootBase & (PageSize - 1)) != 0
            || !ContainsRange(
                state.TablePageArenaStart,
                state.TablePageArenaEndExclusive,
                ownedRootBase,
                TryAdd(ownedRootBase, PageSize))
            || (state.InitialProbePteRaw
                    & Cr3ArchitecturalBaseMask)
                != state.ProbePageStart
            || !ValidateProbePte(
                state.InitialProbePteRaw,
                state.ProbePageStart,
                Cr3ArchitecturalBaseMask)
            || !ValidateProbePtePair(
                state.InitialProbePteRaw,
                state.FinalProbePteRaw)
            || state.ExpectedProbeReturn != ExactProbeReturnValue
            || state.ExpectedCanary == 0
            || state.ReplacementCanary == 0
            || state.ExpectedCanary == state.ReplacementCanary
            || state.ContinuationPageStart
                != state.CompositeCodeStart
            || state.ContinuationPageEndExclusive
                != state.CompositeCodeEndExclusive
            || RangesOverlap(
                state.CompositeCodeStart,
                state.CompositeCodeEndExclusive,
                state.StackStart,
                state.StackEndExclusive)
            || RangesOverlap(
                state.CompositeCodeStart,
                state.CompositeCodeEndExclusive,
                state.ProbePageStart,
                state.ProbePageEndExclusive)
            || RangesOverlap(
                state.CompositeCodeStart,
                state.CompositeCodeEndExclusive,
                state.TablePageArenaStart,
                state.TableStateArenaEndExclusive)
            || RangesOverlap(
                state.StackStart,
                state.StackEndExclusive,
                state.ProbePageStart,
                state.ProbePageEndExclusive)
            || RangesOverlap(
                state.StackStart,
                state.StackEndExclusive,
                state.TablePageArenaStart,
                state.TableStateArenaEndExclusive)
            || RangesOverlap(
                state.ProbePageStart,
                state.ProbePageEndExclusive,
                state.TablePageArenaStart,
                state.TableStateArenaEndExclusive))
        {
            return false;
        }

        byte* stateBytes = (byte*)statePointer;
        for (uint offset = StateOriginalRflagsOffset;
            offset < M01OwnedCr3CompositeTransitionRuntimeState
                .StructureSize;
            offset += sizeof(ulong))
        {
            if (*(ulong*)(stateBytes + offset) != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasCanonicalCallGateLayout()
    {
        return RequiredByteLength <= CallGateOffset
            && (CallGateOffset & (EntryAlignment - 1)) == 0
            && CallGateByteLength != 0
            && CallGateByteLength <= MaximumCallGateByteLength
            && CallGateOffset <= PageSize - CallGateByteLength
            && CallInstructionOffset >= CallGateOffset
            && CallInstructionOffset + 5
                == CallNextReturnSiteOffset
            && CallNextReturnSiteOffset
                < CallGateOffset + CallGateByteLength
            && CallToCompositeRel32
                == unchecked((uint)(
                    (long)CompositeEntryOffset
                    - (long)CallNextReturnSiteOffset))
            && CombinedInstructionCount
                == AuditedInstructionCount
                    + CallGateInstructionCount
            && RequiredCallGateFnv1a64 != 0
            && RequiredCombinedFnv1a64 != 0;
    }

    private static M01OwnedCr3CompositeCallGateStatus
        ValidateCallGateStorage(
            M01OwnedCr3CompositeCallGateResult* evidence,
            ulong evidenceCount,
            ulong evidenceCapacity,
            M01OwnedCr3CompositeCallGatePhase expectedPhase,
            M01OwnedCr3CompositeTransitionRuntimeState* preparedState,
            ulong preparedStateCount,
            ulong preparedStateCapacity,
            byte* bytes,
            ulong byteCount,
            ulong byteCapacity,
            ulong codeBase,
            M01OwnedCr3CompositeTransitionSemanticRecord* semantics,
            ulong semanticCount,
            ulong semanticCapacity)
    {
        if (evidence == null)
        {
            return M01OwnedCr3CompositeCallGateStatus.MissingEvidence;
        }
        if (evidenceCount != 1 || evidenceCapacity != 1)
        {
            return M01OwnedCr3CompositeCallGateStatus.InvalidEvidence;
        }
        if (preparedState == null)
        {
            return M01OwnedCr3CompositeCallGateStatus
                .MissingCompositePreparation;
        }
        if (preparedStateCount != 1 || preparedStateCapacity != 1)
        {
            return M01OwnedCr3CompositeCallGateStatus
                .InvalidCompositePreparation;
        }
        if (bytes == null)
        {
            return M01OwnedCr3CompositeCallGateStatus
                .MissingCodeStorage;
        }
        if (byteCount != MaterializedPageByteLength)
        {
            return M01OwnedCr3CompositeCallGateStatus
                .InvalidCodeStorageCount;
        }
        if (byteCapacity != MaterializedPageByteLength)
        {
            return M01OwnedCr3CompositeCallGateStatus
                .CodeStorageCapacityMismatch;
        }
        if (semantics == null)
        {
            return M01OwnedCr3CompositeCallGateStatus
                .MissingSemanticStorage;
        }
        if (semanticCount != AuditedSemanticRecordCount)
        {
            return M01OwnedCr3CompositeCallGateStatus
                .InvalidSemanticCount;
        }
        if (semanticCapacity != AuditedSemanticRecordCount)
        {
            return M01OwnedCr3CompositeCallGateStatus
                .SemanticCapacityMismatch;
        }

        ulong evidenceStart = 0;
        ulong evidenceEnd = 0;
        ulong stateStart = 0;
        ulong stateEnd = 0;
        ulong byteStart = 0;
        ulong byteEnd = 0;
        ulong semanticStart = 0;
        ulong semanticEnd = 0;
        if (!TryGetCallGateSpan(
                evidence,
                1,
                (ulong)sizeof(
                    M01OwnedCr3CompositeCallGateResult),
                out evidenceStart,
                out evidenceEnd)
            || (evidenceStart
                & (RequiredPointerAlignment - 1)) != 0)
        {
            return M01OwnedCr3CompositeCallGateStatus.InvalidEvidence;
        }
        if (!TryGetCallGateSpan(
                preparedState,
                1,
                M01OwnedCr3CompositeTransitionRuntimeState
                    .StructureSize,
                out stateStart,
                out stateEnd)
            || (stateStart & (RequiredPointerAlignment - 1)) != 0)
        {
            return M01OwnedCr3CompositeCallGateStatus
                .InvalidCompositePreparation;
        }
        byteStart = (ulong)bytes;
        if ((byteStart & (PageSize - 1)) != 0)
        {
            return M01OwnedCr3CompositeCallGateStatus
                .MisalignedCodeStorage;
        }
        if (byteStart > ~0UL - byteCapacity)
        {
            return M01OwnedCr3CompositeCallGateStatus
                .CodeStorageSpanOverflow;
        }
        byteEnd = byteStart + byteCapacity;

        semanticStart = (ulong)semantics;
        if ((semanticStart
            & (RequiredPointerAlignment - 1)) != 0)
        {
            return M01OwnedCr3CompositeCallGateStatus
                .MisalignedSemanticStorage;
        }
        ulong semanticElementSize = (ulong)sizeof(
            M01OwnedCr3CompositeTransitionSemanticRecord);
        if (semanticCapacity > ~0UL / semanticElementSize)
        {
            return M01OwnedCr3CompositeCallGateStatus
                .SemanticSpanOverflow;
        }
        ulong semanticByteCount =
            semanticCapacity * semanticElementSize;
        if (semanticStart > ~0UL - semanticByteCount)
        {
            return M01OwnedCr3CompositeCallGateStatus
                .SemanticSpanOverflow;
        }
        semanticEnd = semanticStart + semanticByteCount;

        M01OwnedCr3CompositeCallGateResult evidenceValue = *evidence;
        if (!IsCanonicalCallGateEvidence(
                evidenceValue,
                expectedPhase))
        {
            return M01OwnedCr3CompositeCallGateStatus.InvalidEvidence;
        }

        M01OwnedCr3CompositeTransitionRuntimeState state =
            *preparedState;
        M01OwnedCr3CompositeTransitionRuntimeState evidenceState =
            evidenceValue.PreparedState;
        if (state.ExpectedSelfAddress != stateStart
            || !IsCanonicalPreparedState(preparedState)
            || FindFirstByteMismatch(
                (byte*)&evidenceState,
                (byte*)preparedState,
                M01OwnedCr3CompositeTransitionRuntimeState
                    .StructureSize)
                != M01OwnedCr3CompositeTransitionResult.NoIndex)
        {
            return M01OwnedCr3CompositeCallGateStatus
                .InvalidCompositePreparation;
        }

        if (codeBase == 0
            || codeBase != byteStart
            || codeBase != evidenceValue.CodeBase
            || codeBase != state.CompositeCodeStart
            || byteEnd != codeBase + MaterializedPageByteLength
            || state.CompositeCodeEndExclusive != byteEnd)
        {
            return M01OwnedCr3CompositeCallGateStatus.InvalidCodeBase;
        }

        if (RangesOverlap(
                evidenceStart,
                evidenceEnd,
                stateStart,
                stateEnd)
            || RangesOverlap(
                evidenceStart,
                evidenceEnd,
                byteStart,
                byteEnd)
            || RangesOverlap(
                evidenceStart,
                evidenceEnd,
                semanticStart,
                semanticEnd)
            || RangesOverlap(
                stateStart,
                stateEnd,
                byteStart,
                byteEnd)
            || RangesOverlap(
                stateStart,
                stateEnd,
                semanticStart,
                semanticEnd)
            || RangesOverlap(
                byteStart,
                byteEnd,
                semanticStart,
                semanticEnd))
        {
            return M01OwnedCr3CompositeCallGateStatus.StorageAlias;
        }

        return M01OwnedCr3CompositeCallGateStatus.Success;
    }

    private static bool IsCanonicalCallGateEvidence(
        M01OwnedCr3CompositeCallGateResult evidence,
        M01OwnedCr3CompositeCallGatePhase expectedPhase)
    {
        if (!evidence.IsSuccess
            || evidence.Phase != expectedPhase
            || evidence.TerminalKind
                != M01OwnedCr3CompositeTransitionTerminalKind
                    .Uninitialized
            || evidence.CodeBase == 0
            || evidence.CodeCapacity != MaterializedPageByteLength
            || evidence.CompositeOffset != CompositeEntryOffset
            || evidence.CompositeLength != RequiredByteLength
            || evidence.GateOffset != CallGateOffset
            || evidence.GateLength != CallGateByteLength
            || evidence.GateEntryAddress
                != evidence.CodeBase + CallGateOffset
            || evidence.CallInstructionOffset
                != CallInstructionOffset
            || evidence.ReturnSiteOffset
                != CallNextReturnSiteOffset
            || evidence.ReturnSiteAddress
                != evidence.CodeBase + CallNextReturnSiteOffset
            || evidence.InvalidIndex
                != M01OwnedCr3CompositeCallGateResult.NoIndex
            || !evidence.Observation.IsArmed)
        {
            return false;
        }

        M01OwnedCr3CompositeTransitionRuntimeState state =
            evidence.PreparedState;
        if (!IsCanonicalPreparedState(&state)
            || state.CompositeCodeStart != evidence.CodeBase
            || state.CompositeCodeEndExclusive
                != evidence.CodeBase + MaterializedPageByteLength
            || state.ExpectedReturnAddress
                != evidence.ReturnSiteAddress
            || evidence.Observation.ExpectedCodeBase
                != evidence.CodeBase
            || evidence.Observation.ExpectedCompositeEntryAddress
                != evidence.CodeBase + CompositeEntryOffset
            || evidence.Observation.ExpectedGateEntryAddress
                != evidence.GateEntryAddress
            || evidence.Observation.ExpectedReturnSiteAddress
                != evidence.ReturnSiteAddress
            || evidence.Observation.ExpectedInterruptEnabled
                != ((evidence.Observation.ExpectedOriginalRflags
                    & RflagsInterruptEnableBit) != 0)
            || evidence.Observation.ExpectedPgeWasSet
                != ((evidence.Observation.ExpectedFirmwareCr4Raw
                    & Cr4PageGlobalEnableBit) != 0))
        {
            return false;
        }

        if (expectedPhase
            == M01OwnedCr3CompositeCallGatePhase.Prepared)
        {
            return evidence.FullPageByteCount == 0
                && evidence.InstructionCount == 0
                && evidence.ExpectedValue == 0
                && evidence.ObservedValue == 0
                && evidence.Fnv1a64 == 0;
        }

        return (expectedPhase
                    == M01OwnedCr3CompositeCallGatePhase.Materialized
                || expectedPhase
                    == M01OwnedCr3CompositeCallGatePhase
                        .ReadbackVerified)
            && evidence.FullPageByteCount
                == MaterializedPageByteLength
            && evidence.InstructionCount == CombinedInstructionCount
            && evidence.ExpectedValue == CombinedInstructionCount
            && evidence.ObservedValue == CombinedInstructionCount
            && RequiredCombinedFnv1a64 != 0
            && evidence.Fnv1a64 == RequiredCombinedFnv1a64;
    }

    private static bool ObservationInputsMatch(
        M01OwnedCr3CompositeCallGateObservation expected,
        M01OwnedCr3CompositeCallGateObservation observed)
    {
        return expected.IsArmed
            && observed.HasCanonicalHeader
            && observed.Size == expected.Size
            && observed.Version == expected.Version
            && observed.IsInitialized == expected.IsInitialized
            && observed.ExpectedSelfAddress
                == expected.ExpectedSelfAddress
            && observed.ExpectedCodeBase
                == expected.ExpectedCodeBase
            && observed.ExpectedCompositeEntryAddress
                == expected.ExpectedCompositeEntryAddress
            && observed.ExpectedGateEntryAddress
                == expected.ExpectedGateEntryAddress
            && observed.ExpectedReturnSiteAddress
                == expected.ExpectedReturnSiteAddress
            && observed.ExpectedOriginalRflags
                == expected.ExpectedOriginalRflags
            && observed.ExpectedFirmwareCr3Raw
                == expected.ExpectedFirmwareCr3Raw
            && observed.ExpectedFirmwareCr4Raw
                == expected.ExpectedFirmwareCr4Raw
            && observed.ExpectedBoundaryCanary
                == expected.ExpectedBoundaryCanary
            && observed.ExpectedCompositeReturnValue
                == expected.ExpectedCompositeReturnValue
            && observed.ExpectedInterruptEnabled
                == expected.ExpectedInterruptEnabled
            && observed.ExpectedPgeWasSet
                == expected.ExpectedPgeWasSet
            && observed.InterruptPolicy == expected.InterruptPolicy
            && observed.PgePolicy == expected.PgePolicy
            && observed.BoundaryCanary == expected.BoundaryCanary;
    }

    private static ulong FindFirstCombinedPaddingNonzero(byte* bytes)
    {
        for (ulong index = RequiredByteLength;
            index < CallGateOffset;
            index++)
        {
            if (bytes[index] != 0)
            {
                return index;
            }
        }

        for (ulong index = CallGateOffset + CallGateByteLength;
            index < MaterializedPageByteLength;
            index++)
        {
            if (bytes[index] != 0)
            {
                return index;
            }
        }

        return M01OwnedCr3CompositeCallGateResult.NoIndex;
    }

    private static bool TryGetCallGateSpan(
        void* pointer,
        ulong count,
        ulong elementSize,
        out ulong start,
        out ulong end)
    {
        start = (ulong)pointer;
        end = 0;
        if (pointer == null
            || count == 0
            || elementSize == 0
            || count > ~0UL / elementSize)
        {
            return false;
        }

        ulong byteCount = count * elementSize;
        if (start > ~0UL - byteCount)
        {
            return false;
        }

        end = start + byteCount;
        return end > start;
    }

    private static bool IsAllZero(byte* bytes, ulong count)
    {
        for (ulong index = 0; index < count; index++)
        {
            if (bytes[index] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static ulong ComputeFnv1a64(byte* bytes, ulong count)
    {
        const ulong OffsetBasis = 14695981039346656037UL;
        const ulong Prime = 1099511628211UL;
        ulong hash = OffsetBasis;
        for (ulong index = 0; index < count; index++)
        {
            hash ^= bytes[index];
            hash *= Prime;
        }
        return hash;
    }

    private static ulong MeasurePlanByteLength()
    {
        PlanWriter measurement = new(
            PlanWriterMode.Measure,
            null,
            MaximumByteCapacity,
            null,
            AuditedSemanticRecordCount);
        BuildPlan(ref measurement);
        return measurement.Succeeded ? measurement.Position : 0;
    }

    private static ulong MeasurePlanInstructionCount()
    {
        PlanWriter measurement = new(
            PlanWriterMode.Measure,
            null,
            MaximumByteCapacity,
            null,
            AuditedSemanticRecordCount);
        BuildPlan(ref measurement);
        return measurement.Succeeded
            ? measurement.InstructionCount
            : 0;
    }

    private static ulong MeasurePlanFnv1a64()
    {
        byte* bytes = stackalloc byte[(int)MaterializedPageByteLength];
        M01OwnedCr3CompositeTransitionSemanticRecord* semantics =
            stackalloc M01OwnedCr3CompositeTransitionSemanticRecord[
                (int)AuditedSemanticRecordCount];
        for (ulong index = 0;
            index < MaterializedPageByteLength;
            index++)
        {
            bytes[index] = 0;
        }

        PlanWriter writer = new(
            PlanWriterMode.Emit,
            bytes,
            MaterializedPageByteLength,
            semantics,
            AuditedSemanticRecordCount);
        BuildPlan(ref writer);
        return writer.Succeeded
                && writer.SemanticCount == AuditedSemanticRecordCount
            ? ComputeFnv1a64(bytes, MaterializedPageByteLength)
            : 0;
    }

    private static M01OwnedCr3CompositeTransitionStatus
        ValidateRangeBinding(
            M01OwnedCr3CompositeTransitionRangeBinding binding,
            BootMemoryRegionRecord* allocations,
            ulong allocationCount,
            OS.Kernel.Memory.BootReservedRange* reservations,
            ulong reservationCount,
            OS.Kernel.Boot.X64BootIdentityMappingRange* mappings,
            ulong mappingCount,
            ulong expectedCodeReservationIndex,
            ulong expectedProbeReservationIndex,
            ulong expectedTableReservationIndex)
    {
        if (!binding.ReservedFieldsAreZero
            || binding.Role
                < M01OwnedCr3CompositeTransitionRangeRole
                    .CompositeCodeAndCallGate
            || binding.Role
                > M01OwnedCr3CompositeTransitionRangeRole
                    .OwnedRootPage
            || binding.Provenance
                < M01OwnedCr3CompositeTransitionRangeProvenance
                    .AllocationReservationAndIdentityMapping
            || binding.Provenance
                > M01OwnedCr3CompositeTransitionRangeProvenance
                    .ContainedInAllocationReservationAndIdentityMapping
            || !TryGetRawPhysical(
                binding.PhysicalStart,
                out ulong physicalStart)
            || !TryGetRawPhysical(
                binding.PhysicalEndExclusive,
                out ulong physicalEnd)
            || !TryGetRawVirtual(
                binding.VirtualStart,
                out ulong virtualStart)
            || !TryGetRawVirtual(
                binding.VirtualEndExclusive,
                out ulong virtualEnd)
            || physicalStart != virtualStart
            || physicalEnd != virtualEnd
            || physicalStart >= physicalEnd
            || physicalEnd > LowCanonicalExclusiveCeiling)
        {
            return M01OwnedCr3CompositeTransitionStatus
                .InvalidRangeBinding;
        }

        M01OwnedCr3CompositeTransitionRangeProvenance
            expectedProvenance;
        ulong expectedAllocationIndex;
        ulong expectedReservationIndex;
        switch (binding.Role)
        {
            case M01OwnedCr3CompositeTransitionRangeRole
                .CompositeCodeAndCallGate:
                expectedProvenance =
                    M01OwnedCr3CompositeTransitionRangeProvenance
                        .AllocationReservationAndIdentityMapping;
                expectedAllocationIndex =
                    M01LivePagingIntegration
                        .Cr3CodeAllocationRecordIndex;
                expectedReservationIndex =
                    expectedCodeReservationIndex;
                break;
            case M01OwnedCr3CompositeTransitionRangeRole
                .ProtectionProbePage:
                expectedProvenance =
                    M01OwnedCr3CompositeTransitionRangeProvenance
                        .AllocationReservationAndIdentityMapping;
                expectedAllocationIndex =
                    M01LivePagingIntegration
                        .ProtectionProbeAllocationRecordIndex;
                expectedReservationIndex =
                    expectedProbeReservationIndex;
                break;
            case M01OwnedCr3CompositeTransitionRangeRole
                .CompleteTableAndStateArena:
                expectedProvenance =
                    M01OwnedCr3CompositeTransitionRangeProvenance
                        .AllocationReservationAndIdentityMapping;
                expectedAllocationIndex =
                    M01LivePagingIntegration
                        .TableStateAllocationRecordIndex;
                expectedReservationIndex =
                    expectedTableReservationIndex;
                break;
            case M01OwnedCr3CompositeTransitionRangeRole
                .RuntimeStateAndCanaryPage:
            case M01OwnedCr3CompositeTransitionRangeRole
                .OwnedRootPage:
                expectedProvenance =
                    M01OwnedCr3CompositeTransitionRangeProvenance
                        .ContainedInAllocationReservationAndIdentityMapping;
                expectedAllocationIndex =
                    M01LivePagingIntegration
                        .TableStateAllocationRecordIndex;
                expectedReservationIndex =
                    expectedTableReservationIndex;
                break;
            case M01OwnedCr3CompositeTransitionRangeRole
                .CompositeOwnedStack:
                expectedProvenance =
                    M01OwnedCr3CompositeTransitionRangeProvenance
                        .ReservationAndIdentityMapping;
                expectedAllocationIndex =
                    M01OwnedCr3CompositeTransitionRangeBinding.NoIndex;
                expectedReservationIndex =
                    M01OwnedCr3CompositeTransitionRangeBinding.NoIndex;
                break;
            default:
                return M01OwnedCr3CompositeTransitionStatus
                    .InvalidRangeBinding;
        }

        if (binding.Provenance != expectedProvenance
            || binding.AllocationIndex != expectedAllocationIndex)
        {
            return M01OwnedCr3CompositeTransitionStatus
                .AllocationProvenanceMismatch;
        }

        if (expectedReservationIndex
                != M01OwnedCr3CompositeTransitionRangeBinding.NoIndex
            && binding.ReservationIndex != expectedReservationIndex)
        {
            return M01OwnedCr3CompositeTransitionStatus
                .ReservationProvenanceMismatch;
        }

        bool allocationRequired =
            binding.Provenance
            != M01OwnedCr3CompositeTransitionRangeProvenance
                .ReservationAndIdentityMapping;
        if (!allocationRequired)
        {
            if (binding.AllocationIndex
                != M01OwnedCr3CompositeTransitionRangeBinding.NoIndex)
            {
                return M01OwnedCr3CompositeTransitionStatus
                    .AllocationProvenanceMismatch;
            }
        }
        else
        {
            if (binding.AllocationIndex >= allocationCount)
            {
                return M01OwnedCr3CompositeTransitionStatus
                    .AllocationProvenanceMismatch;
            }

            BootMemoryRegionRecord allocation =
                allocations[binding.AllocationIndex];
            if (!ValidateAllocationRecord(allocation)
                || allocation.UefiType
                    != ExpectedUefiAllocationType(binding.Role)
                || (binding.Provenance
                    == M01OwnedCr3CompositeTransitionRangeProvenance
                        .AllocationReservationAndIdentityMapping
                    ? allocation.PhysicalStart != physicalStart
                        || allocation.PhysicalEndExclusive != physicalEnd
                    : !ContainsRange(
                        allocation.PhysicalStart,
                        allocation.PhysicalEndExclusive,
                        physicalStart,
                        physicalEnd)))
            {
                return M01OwnedCr3CompositeTransitionStatus
                    .AllocationProvenanceMismatch;
            }
        }

        if (binding.ReservationIndex >= reservationCount)
        {
            return M01OwnedCr3CompositeTransitionStatus
                .ReservationProvenanceMismatch;
        }

        OS.Kernel.Memory.BootReservedRange reservation =
            reservations[binding.ReservationIndex];
        bool reservationMustBeExact =
            binding.Role
                == M01OwnedCr3CompositeTransitionRangeRole
                    .CompositeCodeAndCallGate
            || binding.Role
                == M01OwnedCr3CompositeTransitionRangeRole
                    .CompositeOwnedStack
            || binding.Role
                == M01OwnedCr3CompositeTransitionRangeRole
                    .ProtectionProbePage
            || binding.Role
                == M01OwnedCr3CompositeTransitionRangeRole
                    .CompleteTableAndStateArena;
        if (!reservation.IsValid
            || !TryGetRawPhysical(
                reservation.PhysicalStart,
                out ulong reservationStart)
            || !TryGetRawPhysical(
                reservation.PhysicalEndExclusive,
                out ulong reservationEnd)
            || !ContainsRange(
                reservationStart,
                reservationEnd,
                physicalStart,
                physicalEnd)
            || (reservationMustBeExact
                && (reservationStart != physicalStart
                    || reservationEnd != physicalEnd))
            || reservation.Kind != ExpectedReservationKind(binding.Role))
        {
            return M01OwnedCr3CompositeTransitionStatus
                .ReservationProvenanceMismatch;
        }

        if (binding.IdentityMappingIndex >= mappingCount)
        {
            return M01OwnedCr3CompositeTransitionStatus
                .IdentityMappingProvenanceMismatch;
        }

        OS.Kernel.Boot.X64BootIdentityMappingRange mapping =
            mappings[binding.IdentityMappingIndex];
        if (!mapping.IsInitialized
            || !TryGetRawPhysical(
                mapping.PhysicalStart,
                out ulong mappingPhysicalStart)
            || !TryGetRawPhysical(
                mapping.PhysicalEndExclusive,
                out ulong mappingPhysicalEnd)
            || !TryGetRawVirtual(
                mapping.VirtualStart,
                out ulong mappingVirtualStart)
            || !TryGetRawVirtual(
                mapping.VirtualEndExclusive,
                out ulong mappingVirtualEnd)
            || mappingPhysicalStart != mappingVirtualStart
            || mappingPhysicalEnd != mappingVirtualEnd
            || mapping.PageCount == 0
            || mapping.PageCount > ~0UL / PageSize
            || mapping.ByteLength != mapping.PageCount * PageSize
            || mappingPhysicalEnd - mappingPhysicalStart
                != mapping.ByteLength
            || mapping.ContributingPurposeMask == 0
            || (mapping.ContributingPurposeMask
                & ~OS.Kernel.Boot.X64BootIdentityMappingPlan
                    .RequiredLiveIntervalPurposeMask) != 0
            || !ContainsRange(
                mappingPhysicalStart,
                mappingPhysicalEnd,
                physicalStart,
                physicalEnd)
            || (mapping.ContributingPurposeMask
                & RequiredMappingPurposeBit(binding.Role)) == 0
            || mapping.CachePolicy
                != OS.Kernel.Memory.PageCachePolicy.WriteBack)
        {
            return M01OwnedCr3CompositeTransitionStatus
                .IdentityMappingProvenanceMismatch;
        }

        byte protection = (byte)mapping.Protection;
        if ((protection
                & ((byte)OS.Kernel.Memory.PageProtection.Write
                    | (byte)OS.Kernel.Memory.PageProtection.Execute))
            == ((byte)OS.Kernel.Memory.PageProtection.Write
                | (byte)OS.Kernel.Memory.PageProtection.Execute))
        {
            return M01OwnedCr3CompositeTransitionStatus
                .WritableExecutableRejected;
        }

        OS.Kernel.Memory.PageProtection expectedProtection =
            ExpectedProtection(binding.Role);
        if (mapping.Protection != expectedProtection)
        {
            return M01OwnedCr3CompositeTransitionStatus
                .InvalidProtection;
        }

        return M01OwnedCr3CompositeTransitionStatus.Success;
    }

    private static uint ExpectedUefiAllocationType(
        M01OwnedCr3CompositeTransitionRangeRole role)
    {
        switch (role)
        {
            case M01OwnedCr3CompositeTransitionRangeRole
                .CompositeCodeAndCallGate:
            case M01OwnedCr3CompositeTransitionRangeRole
                .ProtectionProbePage:
                return (uint)UefiMemoryType.LoaderCode;
            case M01OwnedCr3CompositeTransitionRangeRole
                .CompleteTableAndStateArena:
            case M01OwnedCr3CompositeTransitionRangeRole
                .RuntimeStateAndCanaryPage:
            case M01OwnedCr3CompositeTransitionRangeRole
                .OwnedRootPage:
                return (uint)UefiMemoryType.LoaderData;
            default:
                return 0;
        }
    }

    private static OS.Kernel.Memory.BootReservationKind
        ExpectedReservationKind(
            M01OwnedCr3CompositeTransitionRangeRole role)
    {
        switch (role)
        {
            case M01OwnedCr3CompositeTransitionRangeRole
                .CompositeCodeAndCallGate:
                return OS.Kernel.Memory.BootReservationKind
                    .Cr3TransitionStub;
            case M01OwnedCr3CompositeTransitionRangeRole
                .CompositeOwnedStack:
                return OS.Kernel.Memory.BootReservationKind.BootStack;
            case M01OwnedCr3CompositeTransitionRangeRole
                .ProtectionProbePage:
                return OS.Kernel.Memory.BootReservationKind
                    .PageProtectionProbe;
            case M01OwnedCr3CompositeTransitionRangeRole
                .CompleteTableAndStateArena:
            case M01OwnedCr3CompositeTransitionRangeRole
                .RuntimeStateAndCanaryPage:
            case M01OwnedCr3CompositeTransitionRangeRole
                .OwnedRootPage:
                return OS.Kernel.Memory.BootReservationKind
                    .PageTableArena;
            default:
                return OS.Kernel.Memory.BootReservationKind
                    .Uninitialized;
        }
    }

    private static ulong RequiredMappingPurposeBit(
        M01OwnedCr3CompositeTransitionRangeRole role)
    {
        OS.Kernel.Boot.X64BootIdentityMappingPurpose purpose;
        switch (role)
        {
            case M01OwnedCr3CompositeTransitionRangeRole
                .CompositeCodeAndCallGate:
                purpose = OS.Kernel.Boot.X64BootIdentityMappingPurpose
                    .CompositeCodeAndCallGate;
                break;
            case M01OwnedCr3CompositeTransitionRangeRole
                .CompositeOwnedStack:
                purpose = OS.Kernel.Boot.X64BootIdentityMappingPurpose
                    .CompositeOwnedStack;
                break;
            case M01OwnedCr3CompositeTransitionRangeRole
                .ProtectionProbePage:
                purpose = OS.Kernel.Boot.X64BootIdentityMappingPurpose
                    .ProtectionProbePage;
                break;
            case M01OwnedCr3CompositeTransitionRangeRole
                .CompleteTableAndStateArena:
                purpose = OS.Kernel.Boot.X64BootIdentityMappingPurpose
                    .CompleteTableAndStateArena;
                break;
            case M01OwnedCr3CompositeTransitionRangeRole
                .RuntimeStateAndCanaryPage:
                purpose = OS.Kernel.Boot.X64BootIdentityMappingPurpose
                    .RuntimeStateAndCanaryPage;
                break;
            case M01OwnedCr3CompositeTransitionRangeRole
                .OwnedRootPage:
                purpose = OS.Kernel.Boot.X64BootIdentityMappingPurpose
                    .OwnedRootPage;
                break;
            default:
                return 0;
        }

        return 1UL << (byte)purpose;
    }

    private static OS.Kernel.Memory.PageProtection ExpectedProtection(
        M01OwnedCr3CompositeTransitionRangeRole role)
    {
        switch (role)
        {
            case M01OwnedCr3CompositeTransitionRangeRole
                .CompositeCodeAndCallGate:
                return OS.Kernel.Memory.PageProtection.Read
                    | OS.Kernel.Memory.PageProtection.Execute;
            case M01OwnedCr3CompositeTransitionRangeRole
                .CompositeOwnedStack:
            case M01OwnedCr3CompositeTransitionRangeRole
                .ProtectionProbePage:
            case M01OwnedCr3CompositeTransitionRangeRole
                .CompleteTableAndStateArena:
            case M01OwnedCr3CompositeTransitionRangeRole
                .RuntimeStateAndCanaryPage:
            case M01OwnedCr3CompositeTransitionRangeRole
                .OwnedRootPage:
                return OS.Kernel.Memory.PageProtection.Read
                    | OS.Kernel.Memory.PageProtection.Write;
            default:
                return OS.Kernel.Memory.PageProtection.None;
        }
    }

    private static bool ValidateAllocationRecord(
        BootMemoryRegionRecord record)
    {
        if (record.PhysicalStart == 0
            || record.PhysicalEndExclusive <= record.PhysicalStart
            || (record.PhysicalStart & (PageSize - 1)) != 0
            || (record.PhysicalEndExclusive & (PageSize - 1)) != 0
            || record.PageCount == 0
            || record.PageCount > (~0UL / PageSize)
            || record.ByteLength != record.PageCount * PageSize
            || record.PhysicalEndExclusive - record.PhysicalStart
                != record.ByteLength)
        {
            return false;
        }

        return record.VirtualStart == 0
            || record.VirtualStart == record.PhysicalStart;
    }

    private static M01OwnedCr3CompositeTransitionStatus
        ValidateInputTable(
            void* table,
            ulong count,
            ulong capacity,
            ulong maximumCount,
            ulong recordSize,
            M01OwnedCr3CompositeTransitionStatus missingStatus,
            M01OwnedCr3CompositeTransitionStatus invalidCountStatus,
            M01OwnedCr3CompositeTransitionStatus capacityStatus,
            out ulong tableStart,
            out ulong tableEnd)
    {
        tableStart = (ulong)table;
        tableEnd = 0;
        if (count == 0 || count > maximumCount)
        {
            return invalidCountStatus;
        }

        if (capacity < count || capacity > maximumCount)
        {
            return capacityStatus;
        }

        if (table == null)
        {
            return missingStatus;
        }

        if ((tableStart & (RequiredPointerAlignment - 1)) != 0)
        {
            return M01OwnedCr3CompositeTransitionStatus
                .MisalignedInputTable;
        }

        if (capacity > ~0UL / recordSize)
        {
            return M01OwnedCr3CompositeTransitionStatus
                .InputTableSpanOverflow;
        }

        ulong byteLength = capacity * recordSize;
        if (tableStart > ~0UL - byteLength)
        {
            return M01OwnedCr3CompositeTransitionStatus
                .InputTableSpanOverflow;
        }

        tableEnd = tableStart + byteLength;
        return M01OwnedCr3CompositeTransitionStatus.Success;
    }

    private static bool TryGetRawRange(
        M01OwnedCr3CompositeTransitionRangeBinding binding,
        out ulong start,
        out ulong end)
    {
        start = 0;
        end = 0;
        return TryGetRawVirtual(binding.VirtualStart, out start)
            && TryGetRawVirtual(binding.VirtualEndExclusive, out end)
            && start < end;
    }

    private static bool TryFindUniqueReservationRange(
        OS.Kernel.Memory.BootReservedRange* reservations,
        ulong reservationCount,
        OS.Kernel.Memory.BootReservationKind kind,
        out ulong start,
        out ulong end)
    {
        start = 0;
        end = 0;
        if (reservations == null
            || reservationCount == 0
            || kind == OS.Kernel.Memory.BootReservationKind.Uninitialized)
        {
            return false;
        }

        ulong matchCount = 0;
        for (ulong index = 0; index < reservationCount; index++)
        {
            OS.Kernel.Memory.BootReservedRange reservation =
                reservations[index];
            if (reservation.Kind != kind)
            {
                continue;
            }

            if (!reservation.IsValid
                || !TryGetRawPhysical(
                    reservation.PhysicalStart,
                    out ulong candidateStart)
                || !TryGetRawPhysical(
                    reservation.PhysicalEndExclusive,
                    out ulong candidateEnd)
                || candidateStart >= candidateEnd)
            {
                return false;
            }

            start = candidateStart;
            end = candidateEnd;
            matchCount++;
        }

        return matchCount == 1;
    }

    private static bool TryGetRawPhysical(
        PhysicalAddress address,
        out ulong raw)
    {
        return address.TryGetRawValueForUefiBoundary(out raw)
            == AddressValueStatus.Success;
    }

    private static bool TryGetRawVirtual(
        VirtualAddress address,
        out ulong raw)
    {
        return address.TryGetRawValueForUefiBoundary(out raw)
            == AddressValueStatus.Success;
    }

    private static bool IsPageRange(
        ulong start,
        ulong end,
        bool exactlyOnePage)
    {
        if (start == 0
            || start >= end
            || (start & (PageSize - 1)) != 0
            || (end & (PageSize - 1)) != 0)
        {
            return false;
        }

        return !exactlyOnePage || end - start == PageSize;
    }

    private static bool IsCanonicalPageRange(
        ulong start,
        ulong end,
        bool exactlyOnePage)
    {
        return IsPageRange(start, end, exactlyOnePage)
            && start < LowCanonicalExclusiveCeiling
            && end <= LowCanonicalExclusiveCeiling;
    }

    private static bool ValidateProbePtePair(
        ulong initial,
        ulong final)
    {
        const ulong RequiredDifference =
            PageWritableBit | PageNoExecuteBit;
        if ((initial & PagePresentBit) == 0
            || (initial & PageWritableBit) == 0
            || (initial & PageNoExecuteBit) == 0
            || (final & PagePresentBit) == 0
            || (final & PageWritableBit) != 0
            || (final & PageNoExecuteBit) != 0
            || (initial ^ final) != RequiredDifference)
        {
            return false;
        }

        return true;
    }

    private static bool ValidateProbePte(
        ulong entry,
        ulong expectedPhysicalPage,
        ulong physicalAddressMask)
    {
        const ulong PermittedPteBits =
            PagePresentBit
            | PageWritableBit
            | PageAccessedBit
            | PageDirtyBit
            | PageNoExecuteBit;
        ulong permittedBits = physicalAddressMask | PermittedPteBits;
        return (entry & ~permittedBits) == 0
            && (entry & physicalAddressMask)
                == expectedPhysicalPage
            && (entry & PagePresentBit) != 0
            && (entry & PageWritableBit) != 0
            && (entry & PageNoExecuteBit) != 0;
    }

    private static bool TryDeriveProbeLeaf(
        OS.Kernel.Memory.X64PageTableImagePage* pageImages,
        ulong pageImageCount,
        ulong* entries,
        ulong entryCount,
        ulong tableStart,
        ulong tableEnd,
        ulong rootStart,
        ulong probeAddress,
        ulong physicalAddressMask,
        out ulong pteAddress,
        out ulong flatEntryIndex,
        out ulong leafEntry,
        out ulong pdptPage,
        out ulong pdPage,
        out ulong ptPage)
    {
        pteAddress = 0;
        flatEntryIndex = M01OwnedCr3CompositeTransitionResult.NoIndex;
        leafEntry = 0;
        pdptPage = 0;
        pdPage = 0;
        ptPage = 0;
        if (pageImages == null
            || entries == null
            || pageImageCount < 4
            || entryCount != pageImageCount * 512
            || rootStart < tableStart
            || rootStart >= tableEnd
            || ((rootStart - tableStart) & (PageSize - 1)) != 0)
        {
            return false;
        }

        for (ulong pageIndex = 0;
            pageIndex < pageImageCount;
            pageIndex++)
        {
            if (!pageImages[pageIndex].IsInitialized
                || pageImages[pageIndex].EntryStartIndex
                    != pageIndex * 512
                || !TryGetRawPhysical(
                    pageImages[pageIndex].PhysicalStart,
                    out ulong physicalStart)
                || physicalStart
                    != tableStart + pageIndex * PageSize)
            {
                return false;
            }
        }

        ulong currentPageIndex = (rootStart - tableStart) / PageSize;
        for (byte level = 0; level < 3; level++)
        {
            int shift = level == 0 ? 39 : level == 1 ? 30 : 21;
            ulong entryIndex =
                (probeAddress >> shift) & 0x1ffUL;
            ulong currentFlatIndex =
                pageImages[currentPageIndex].EntryStartIndex
                + entryIndex;
            if (currentFlatIndex >= entryCount)
            {
                return false;
            }

            ulong entry = entries[currentFlatIndex];
            const ulong PermittedParentFlags =
                PagePresentBit | PageWritableBit | PageAccessedBit;
            ulong permittedParentBits =
                physicalAddressMask | PermittedParentFlags;
            if ((entry & ~permittedParentBits) != 0
                || (entry & PagePresentBit) == 0
                || (entry & PageWritableBit) == 0
                || (entry & PageNoExecuteBit) != 0
                || (entry & PageLargeBit) != 0)
            {
                return false;
            }

            ulong child = entry & physicalAddressMask;
            if (child < tableStart
                || child >= tableEnd
                || ((child - tableStart) & (PageSize - 1)) != 0)
            {
                return false;
            }

            currentPageIndex = (child - tableStart) / PageSize;
            if (currentPageIndex >= pageImageCount)
            {
                return false;
            }

            if (level == 0)
            {
                pdptPage = child;
            }
            else if (level == 1)
            {
                pdPage = child;
            }
            else
            {
                ptPage = child;
            }
        }

        ulong pteIndex = (probeAddress >> 12) & 0x1ffUL;
        flatEntryIndex =
            pageImages[currentPageIndex].EntryStartIndex + pteIndex;
        if (flatEntryIndex >= entryCount)
        {
            flatEntryIndex =
                M01OwnedCr3CompositeTransitionResult.NoIndex;
            return false;
        }

        ulong pagePhysical;
        if (!TryGetRawPhysical(
                pageImages[currentPageIndex].PhysicalStart,
                out pagePhysical)
            || pagePhysical > ~0UL - pteIndex * sizeof(ulong))
        {
            flatEntryIndex =
                M01OwnedCr3CompositeTransitionResult.NoIndex;
            return false;
        }

        pteAddress = pagePhysical + pteIndex * sizeof(ulong);
        leafEntry = entries[flatEntryIndex];
        return true;
    }

    private static bool TryEncodeProbeHierarchyIdentity(
        ulong criticalMappingPageCount,
        ulong tableStart,
        ulong tableEnd,
        ulong pdptPage,
        ulong pdPage,
        ulong ptPage,
        out ulong identity)
    {
        identity = 0;
        if (criticalMappingPageCount == 0
            || criticalMappingPageCount
                > MaximumCriticalMappingPageCount
            || !TryGetTablePageOrdinal(
                tableStart,
                tableEnd,
                pdptPage,
                out ulong pdptOrdinal)
            || !TryGetTablePageOrdinal(
                tableStart,
                tableEnd,
                pdPage,
                out ulong pdOrdinal)
            || !TryGetTablePageOrdinal(
                tableStart,
                tableEnd,
                ptPage,
                out ulong ptOrdinal))
        {
            return false;
        }

        identity = criticalMappingPageCount
            | (pdptOrdinal << ProbePdptPageOrdinalShift)
            | (pdOrdinal << ProbePdPageOrdinalShift)
            | (ptOrdinal << ProbePtPageOrdinalShift);
        return (identity & ~ProbeHierarchyIdentityPermittedMask) == 0;
    }

    private static bool TryGetTablePageOrdinal(
        ulong tableStart,
        ulong tableEnd,
        ulong page,
        out ulong ordinal)
    {
        ordinal = 0;
        if (tableStart >= tableEnd
            || page < tableStart
            || page >= tableEnd
            || ((page - tableStart) & (PageSize - 1)) != 0)
        {
            return false;
        }

        ordinal = (page - tableStart) / PageSize;
        return ordinal <= ProbeHierarchyPageOrdinalMask;
    }

    private static bool ValidateCriticalRangeMapping(
        OS.Kernel.Memory.X64PageTableImagePage* pageImages,
        ulong pageImageCount,
        ulong* entries,
        ulong entryCount,
        ulong rootStart,
        ulong tableStart,
        ulong tablePageEnd,
        ulong physicalPageMask,
        ulong rangeStart,
        ulong rangeEnd,
        OS.Kernel.Memory.PageProtection expectedProtection,
        ref ulong auditedPageCount)
    {
        if (!IsPageRange(
                rangeStart,
                rangeEnd,
                exactlyOnePage: false))
        {
            return false;
        }

        ulong pageCount = (rangeEnd - rangeStart) / PageSize;
        if (pageCount == 0
            || auditedPageCount > MaximumCriticalMappingPageCount
            || pageCount
                > MaximumCriticalMappingPageCount
                    - auditedPageCount)
        {
            return false;
        }

        for (ulong pageOrdinal = 0;
            pageOrdinal < pageCount;
            pageOrdinal++)
        {
            ulong address = rangeStart + pageOrdinal * PageSize;
            if (!TryReadIdentityLeaf(
                    pageImages,
                    pageImageCount,
                    entries,
                    entryCount,
                    rootStart,
                    tableStart,
                    tablePageEnd,
                    physicalPageMask,
                    address,
                    out ulong leaf)
                || !HasExpectedLeafProtection(
                    leaf,
                    address,
                    physicalPageMask,
                    expectedProtection))
            {
                return false;
            }
        }

        auditedPageCount += pageCount;
        return true;
    }

    private static bool ValidateAbsentIdentityRange(
        OS.Kernel.Memory.X64PageTableImagePage* pageImages,
        ulong pageImageCount,
        ulong* entries,
        ulong entryCount,
        ulong rootStart,
        ulong tableStart,
        ulong tablePageEnd,
        ulong physicalPageMask,
        ulong rangeStart,
        ulong rangeEnd)
    {
        if (!IsPageRange(
                rangeStart,
                rangeEnd,
                exactlyOnePage: false))
        {
            return false;
        }

        ulong pageCount = (rangeEnd - rangeStart) / PageSize;
        for (ulong pageOrdinal = 0;
            pageOrdinal < pageCount;
            pageOrdinal++)
        {
            if (!IsIdentityAddressAbsent(
                    pageImages,
                    pageImageCount,
                    entries,
                    entryCount,
                    rootStart,
                    tableStart,
                    tablePageEnd,
                    physicalPageMask,
                    rangeStart + pageOrdinal * PageSize))
            {
                return false;
            }
        }

        return pageCount != 0;
    }

    private static bool IsIdentityAddressAbsent(
        OS.Kernel.Memory.X64PageTableImagePage* pageImages,
        ulong pageImageCount,
        ulong* entries,
        ulong entryCount,
        ulong rootStart,
        ulong tableStart,
        ulong tablePageEnd,
        ulong physicalPageMask,
        ulong virtualAddress)
    {
        if (pageImages == null
            || entries == null
            || rootStart < tableStart
            || rootStart >= tablePageEnd
            || ((rootStart - tableStart) & (PageSize - 1)) != 0)
        {
            return false;
        }

        ulong currentPage = (rootStart - tableStart) / PageSize;
        for (byte level = 0; level < 3; level++)
        {
            if (currentPage >= pageImageCount
                || !pageImages[currentPage].IsInitialized)
            {
                return false;
            }

            int shift = level == 0 ? 39 : level == 1 ? 30 : 21;
            ulong index = (virtualAddress >> shift) & 0x1ff;
            ulong flat =
                pageImages[currentPage].EntryStartIndex + index;
            if (flat >= entryCount)
            {
                return false;
            }

            ulong entry = entries[flat];
            if ((entry & PagePresentBit) == 0)
            {
                return entry == 0;
            }

            ulong allowed =
                physicalPageMask
                | PagePresentBit
                | PageWritableBit
                | PageAccessedBit;
            if ((entry & ~allowed) != 0
                || (entry
                    & (PagePresentBit | PageWritableBit))
                    != (PagePresentBit | PageWritableBit))
            {
                return false;
            }

            ulong child = entry & physicalPageMask;
            if (child < tableStart
                || child >= tablePageEnd
                || ((child - tableStart) & (PageSize - 1)) != 0)
            {
                return false;
            }

            currentPage = (child - tableStart) / PageSize;
        }

        if (currentPage >= pageImageCount
            || !pageImages[currentPage].IsInitialized)
        {
            return false;
        }

        ulong leafIndex = (virtualAddress >> 12) & 0x1ff;
        ulong leafFlat =
            pageImages[currentPage].EntryStartIndex + leafIndex;
        return leafFlat < entryCount && entries[leafFlat] == 0;
    }

    private static bool TryReadIdentityLeaf(
        OS.Kernel.Memory.X64PageTableImagePage* pageImages,
        ulong pageImageCount,
        ulong* entries,
        ulong entryCount,
        ulong rootStart,
        ulong tableStart,
        ulong tablePageEnd,
        ulong physicalPageMask,
        ulong virtualAddress,
        out ulong leaf)
    {
        leaf = 0;
        if (rootStart < tableStart
            || rootStart >= tablePageEnd
            || ((rootStart - tableStart) & (PageSize - 1)) != 0)
        {
            return false;
        }

        ulong currentPage = (rootStart - tableStart) / PageSize;
        for (byte level = 0; level < 3; level++)
        {
            int shift = level == 0 ? 39 : level == 1 ? 30 : 21;
            ulong index = (virtualAddress >> shift) & 0x1ff;
            if (currentPage >= pageImageCount)
            {
                return false;
            }

            ulong flat =
                pageImages[currentPage].EntryStartIndex + index;
            if (flat >= entryCount)
            {
                return false;
            }

            ulong entry = entries[flat];
            ulong allowed =
                physicalPageMask
                | PagePresentBit
                | PageWritableBit
                | PageAccessedBit;
            if ((entry & ~allowed) != 0
                || (entry
                    & (PagePresentBit | PageWritableBit))
                    != (PagePresentBit | PageWritableBit))
            {
                return false;
            }

            ulong child = entry & physicalPageMask;
            if (child < tableStart
                || child >= tablePageEnd
                || ((child - tableStart) & (PageSize - 1)) != 0)
            {
                return false;
            }
            currentPage = (child - tableStart) / PageSize;
        }

        ulong leafIndex = (virtualAddress >> 12) & 0x1ff;
        if (currentPage >= pageImageCount)
        {
            return false;
        }
        ulong leafFlat =
            pageImages[currentPage].EntryStartIndex + leafIndex;
        if (leafFlat >= entryCount)
        {
            return false;
        }

        leaf = entries[leafFlat];
        return true;
    }

    private static bool HasExpectedLeafProtection(
        ulong leaf,
        ulong expectedPhysicalPage,
        ulong physicalPageMask,
        OS.Kernel.Memory.PageProtection protection)
    {
        bool writable =
            (((byte)protection
                & (byte)OS.Kernel.Memory.PageProtection.Write) != 0);
        bool executable =
            (((byte)protection
                & (byte)OS.Kernel.Memory.PageProtection.Execute) != 0);
        ulong allowed =
            physicalPageMask
            | PagePresentBit
            | PageAccessedBit
            | (writable ? PageWritableBit | PageDirtyBit : 0)
            | (executable ? 0 : PageNoExecuteBit);
        if ((leaf & ~allowed) != 0
            || (leaf & physicalPageMask) != expectedPhysicalPage
            || (leaf & PagePresentBit) == 0
            || ((leaf & PageWritableBit) != 0) != writable
            || ((leaf & PageNoExecuteBit) == 0) != executable)
        {
            return false;
        }

        return true;
    }

    private static bool ContainsAddress(
        ulong start,
        ulong end,
        ulong address)
    {
        return start < end && address >= start && address < end;
    }

    private static bool ContainsRange(
        ulong outerStart,
        ulong outerEnd,
        ulong innerStart,
        ulong innerEnd)
    {
        return outerStart < outerEnd
            && innerStart < innerEnd
            && innerStart >= outerStart
            && innerEnd <= outerEnd;
    }

    private static bool RangesOverlap(
        ulong firstStart,
        ulong firstEnd,
        ulong secondStart,
        ulong secondEnd)
    {
        return firstStart < secondEnd && secondStart < firstEnd;
    }

    private static bool EightSpansArePairwiseDisjoint(
        ulong start0,
        ulong end0,
        ulong start1,
        ulong end1,
        ulong start2,
        ulong end2,
        ulong start3,
        ulong end3,
        ulong start4,
        ulong end4,
        ulong start5,
        ulong end5,
        ulong start6,
        ulong end6,
        ulong start7,
        ulong end7)
    {
        return !RangesOverlap(start0, end0, start1, end1)
            && !RangesOverlap(start0, end0, start2, end2)
            && !RangesOverlap(start0, end0, start3, end3)
            && !RangesOverlap(start0, end0, start4, end4)
            && !RangesOverlap(start0, end0, start5, end5)
            && !RangesOverlap(start0, end0, start6, end6)
            && !RangesOverlap(start0, end0, start7, end7)
            && !RangesOverlap(start1, end1, start2, end2)
            && !RangesOverlap(start1, end1, start3, end3)
            && !RangesOverlap(start1, end1, start4, end4)
            && !RangesOverlap(start1, end1, start5, end5)
            && !RangesOverlap(start1, end1, start6, end6)
            && !RangesOverlap(start1, end1, start7, end7)
            && !RangesOverlap(start2, end2, start3, end3)
            && !RangesOverlap(start2, end2, start4, end4)
            && !RangesOverlap(start2, end2, start5, end5)
            && !RangesOverlap(start2, end2, start6, end6)
            && !RangesOverlap(start2, end2, start7, end7)
            && !RangesOverlap(start3, end3, start4, end4)
            && !RangesOverlap(start3, end3, start5, end5)
            && !RangesOverlap(start3, end3, start6, end6)
            && !RangesOverlap(start3, end3, start7, end7)
            && !RangesOverlap(start4, end4, start5, end5)
            && !RangesOverlap(start4, end4, start6, end6)
            && !RangesOverlap(start4, end4, start7, end7)
            && !RangesOverlap(start5, end5, start6, end6)
            && !RangesOverlap(start5, end5, start7, end7)
            && !RangesOverlap(start6, end6, start7, end7);
    }

    private static ulong TryAdd(ulong value, ulong addition)
    {
        return value <= ~0UL - addition ? value + addition : 0;
    }

    private static M01OwnedCr3CompositeCallGateResult CallGateSuccess(
        M01OwnedCr3CompositeCallGatePhase phase,
        M01OwnedCr3CompositeTransitionTerminalKind terminalKind,
        M01OwnedCr3CompositeCallGateObservation observation,
        M01OwnedCr3CompositeTransitionRuntimeState preparedState,
        ulong codeBase,
        ulong fullPageByteCount,
        ulong fnv1a64)
    {
        bool hasCombinedEncoding =
            phase
                == M01OwnedCr3CompositeCallGatePhase.Materialized
            || phase
                == M01OwnedCr3CompositeCallGatePhase.ReadbackVerified
            || phase
                == M01OwnedCr3CompositeCallGatePhase.ObservedAudit;
        ulong instructionCount = hasCombinedEncoding
            ? CombinedInstructionCount
            : 0;
        return new M01OwnedCr3CompositeCallGateResult(
            M01OwnedCr3CompositeCallGateStatus.Success,
            phase,
            terminalKind,
            observation,
            preparedState,
            codeBase,
            MaterializedPageByteLength,
            CompositeEntryOffset,
            RequiredByteLength,
            CallGateOffset,
            CallGateByteLength,
            codeBase == 0 ? 0 : codeBase + CallGateOffset,
            CallInstructionOffset,
            CallNextReturnSiteOffset,
            codeBase == 0
                ? 0
                : codeBase + CallNextReturnSiteOffset,
            fullPageByteCount,
            instructionCount,
            M01OwnedCr3CompositeCallGateResult.NoIndex,
            instructionCount,
            instructionCount,
            fnv1a64);
    }

    private static M01OwnedCr3CompositeCallGateResult CallGateFailure(
        M01OwnedCr3CompositeCallGateStatus status,
        M01OwnedCr3CompositeCallGatePhase phase,
        M01OwnedCr3CompositeTransitionTerminalKind terminalKind,
        M01OwnedCr3CompositeCallGateObservation observation,
        M01OwnedCr3CompositeTransitionRuntimeState preparedState,
        ulong codeBase,
        ulong invalidIndex,
        ulong expectedValue,
        ulong observedValue,
        ulong fnv1a64)
    {
        bool hasCombinedEncoding =
            phase
                == M01OwnedCr3CompositeCallGatePhase.Materialized
            || phase
                == M01OwnedCr3CompositeCallGatePhase.ReadbackVerified
            || phase
                == M01OwnedCr3CompositeCallGatePhase.ObservedAudit;
        return new M01OwnedCr3CompositeCallGateResult(
            status,
            phase,
            terminalKind,
            observation,
            preparedState,
            codeBase,
            codeBase == 0 ? 0 : MaterializedPageByteLength,
            CompositeEntryOffset,
            RequiredByteLength,
            CallGateOffset,
            CallGateByteLength,
            codeBase == 0 ? 0 : codeBase + CallGateOffset,
            CallInstructionOffset,
            CallNextReturnSiteOffset,
            codeBase == 0
                ? 0
                : codeBase + CallNextReturnSiteOffset,
            hasCombinedEncoding ? MaterializedPageByteLength : 0,
            hasCombinedEncoding ? CombinedInstructionCount : 0,
            invalidIndex,
            expectedValue,
            observedValue,
            fnv1a64);
    }

    private static M01OwnedCr3CompositeCallGateResult FailCallGate(
        M01OwnedCr3CompositeCallGateStatus status,
        ulong invalidIndex =
            M01OwnedCr3CompositeCallGateResult.NoIndex,
        ulong expectedValue = 0,
        ulong observedValue = 0)
    {
        return CallGateFailure(
            status,
            M01OwnedCr3CompositeCallGatePhase.Uninitialized,
            M01OwnedCr3CompositeTransitionTerminalKind.Uninitialized,
            default,
            default,
            0,
            invalidIndex,
            expectedValue,
            observedValue,
            0);
    }

    private static M01OwnedCr3CompositeTransitionResult Fail(
        M01OwnedCr3CompositeTransitionStatus status)
    {
        return Fail(
            status,
            M01OwnedCr3CompositeTransitionResult.NoIndex,
            0,
            0);
    }

    private static M01OwnedCr3CompositeTransitionResult Fail(
        M01OwnedCr3CompositeTransitionStatus status,
        ulong invalidIndex =
            M01OwnedCr3CompositeTransitionResult.NoIndex,
        ulong expectedValue = 0,
        ulong observedValue = 0)
    {
        return new M01OwnedCr3CompositeTransitionResult(
            status,
            M01OwnedCr3CompositeTransitionResultPhase.Uninitialized,
            M01OwnedCr3CompositeTransitionTerminalKind.Uninitialized,
            default,
            0,
            0,
            0,
            0,
            0,
            invalidIndex,
            expectedValue,
            observedValue,
            0);
    }
}
