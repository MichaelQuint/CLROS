namespace OS.Kernel.Memory;

/// <summary>
/// Identifies the caller-owned materialization state of the constant-return
/// protection probe.
/// </summary>
public enum X64ProtectionProbeStubPhase : byte
{
    Uninitialized = 0,
    Materialized = 1,
    ReadBackVerified = 2
}

/// <summary>
/// Classifies the modeled terminal outcome. A modeled return remains host
/// transcript evidence and is never live guest evidence.
/// </summary>
public enum X64ProtectionProbeTerminalKind : byte
{
    Uninitialized = 0,
    ModeledReturn = 1,
    Timeout = 2,
    Reset = 3,
    Fault = 4,
    UnexpectedReturnValue = 5,
    CanaryMismatch = 6
}

/// <summary>
/// Exact modeled order for the future RW+NX to RO+X boundary.
/// </summary>
public enum X64ProtectionProbeEventKind : byte
{
    Uninitialized = 0,
    StubMaterialized = 1,
    StubReadBackVerified = 2,
    InitialReadWriteNoExecuteVerified = 3,
    ProtectionChangedReadExecute = 4,
    LeafReadBackVerified = 5,
    TargetInvalidationPlanned = 6,
    InvlpgInvocationPlanned = 7,
    ProbeInvocationPlanned = 8,
    ConstantReturnAndCanaryObserved = 9
}

/// <summary>
/// Stable status contract for materialization and aggregate audit.
/// Values are append-only.
/// </summary>
public enum X64ProtectionProbeStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidProbeAllocation = 2,
    ProbeAllocationMustBeLoaderCode = 3,
    ProbeAllocationMustBeOnePage = 4,
    ProbeAllocationNotIdentityMapped = 5,
    InvalidProbeReservation = 6,
    ProbeReservationKindMismatch = 7,
    ProbeReservationRangeMismatch = 8,
    InvalidEntryOffset = 9,
    MisalignedEntry = 10,
    EntryOutsideProbePage = 11,
    MissingDestination = 12,
    MisalignedDestination = 13,
    DestinationCapacityTooSmall = 14,
    DestinationCapacityExceedsPage = 15,
    DestinationSpanOverflow = 16,
    DestinationAddressMismatch = 17,
    MissingSource = 18,
    MisalignedSource = 19,
    SourceCapacityTooSmall = 20,
    SourceCapacityExceedsPage = 21,
    SourceSpanOverflow = 22,
    SourceAddressMismatch = 23,
    InvalidExpectedStub = 24,
    ExpectedStubNotMaterialized = 25,
    ByteReadbackMismatch = 26,
    InvalidTransitionCodeAllocation = 27,
    InvalidTableStateAllocation = 28,
    AllocationOverlap = 29,
    StubAllocationMismatch = 30,
    InvalidProtectionMutation = 31,
    ProtectionTargetMismatch = 32,
    InitialProtectionMismatch = 33,
    FinalProtectionMismatch = 34,
    ProtectionPhysicalMismatch = 35,
    ProtectionCacheMismatch = 36,
    LeafReadbackMismatch = 37,
    InvalidInvlpgStub = 38,
    InvlpgCodeAllocationMismatch = 39,
    InvalidInvlpgInvocation = 40,
    InvlpgTargetMismatch = 41,
    InvalidObservation = 42,
    InvocationEntryMismatch = 43,
    InvocationCountMustBeOne = 44,
    UnexpectedReturnValue = 45,
    CanaryMismatch = 46,
    InvalidTerminalKind = 47,
    InvalidEventCount = 48,
    EventCapacityTooSmall = 49,
    EventCapacityExceedsLimit = 50,
    MissingEvents = 51,
    MisalignedEvents = 52,
    EventSpanOverflow = 53,
    InvalidEvent = 54,
    EventOrderMismatch = 55,
    ExecuteBeforeProtectRejected = 56,
    ContractLayoutMismatch = 57,
    InvalidTableStateProtection = 58,
    TableImageOutsideStateAllocation = 59
}

/// <summary>
/// Exact result of writing or validating the six-byte constant-return stream.
/// </summary>
public readonly struct X64ProtectionProbeStubPlanResult
{
    public const ulong StructureSize = 80;
    public const ushort StructureVersion = 1;

    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly X64ProtectionProbeStatus _status;
    private readonly X64ProtectionProbeStubPhase _phase;
    private readonly byte _reserved0;
    private readonly ushort _reserved1;
    private readonly uint _reserved2;
    private readonly OS.Boot.VirtualAddress _probePageStart;
    private readonly OS.Boot.VirtualAddress _probePageEndExclusive;
    private readonly OS.Boot.VirtualAddress _entryAddress;
    private readonly ulong _entryOffset;
    private readonly ulong _availableCapacity;
    private readonly ulong _requiredByteCount;
    private readonly ulong _bytesWrittenOrVerified;
    private readonly uint _returnValue;
    private readonly uint _instructionCount;

    private X64ProtectionProbeStubPlanResult(
        X64ProtectionProbeStatus status,
        X64ProtectionProbeStubPhase phase,
        OS.Boot.VirtualAddress probePageStart,
        OS.Boot.VirtualAddress probePageEndExclusive,
        OS.Boot.VirtualAddress entryAddress,
        ulong entryOffset,
        ulong availableCapacity,
        ulong requiredByteCount,
        ulong bytesWrittenOrVerified,
        uint returnValue,
        uint instructionCount)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _status = status;
        _phase = phase;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _probePageStart = probePageStart;
        _probePageEndExclusive = probePageEndExclusive;
        _entryAddress = entryAddress;
        _entryOffset = entryOffset;
        _availableCapacity = availableCapacity;
        _requiredByteCount = requiredByteCount;
        _bytesWrittenOrVerified = bytesWrittenOrVerified;
        _returnValue = returnValue;
        _instructionCount = instructionCount;
    }

    public bool IsInitialized =>
        _structureSize == StructureSize
        && _structureVersion == StructureVersion
        && _isInitialized == 1
        && _reserved0 == 0
        && _reserved1 == 0
        && _reserved2 == 0;

    public X64ProtectionProbeStatus Status => _status;

    public X64ProtectionProbeStubPhase Phase => _phase;

    public OS.Boot.VirtualAddress ProbePageStart =>
        _probePageStart;

    public OS.Boot.VirtualAddress ProbePageEndExclusive =>
        _probePageEndExclusive;

    public OS.Boot.VirtualAddress EntryAddress => _entryAddress;

    public ulong EntryOffset => _entryOffset;

    public ulong AvailableCapacity => _availableCapacity;

    public ulong RequiredByteCount => _requiredByteCount;

    public ulong BytesWrittenOrVerified =>
        _bytesWrittenOrVerified;

    public uint ReturnValue => _returnValue;

    public uint InstructionCount => _instructionCount;

    public bool ReadBackVerified =>
        Succeeded
        && _phase
            == X64ProtectionProbeStubPhase.ReadBackVerified;

    public bool Succeeded =>
        IsInitialized
        && _status == X64ProtectionProbeStatus.Success
        && (_phase
                == X64ProtectionProbeStubPhase.Materialized ||
            _phase
                == X64ProtectionProbeStubPhase.ReadBackVerified)
        && _probePageStart.IsValid
        && _probePageStart.IsAligned(
            X64ProtectionProbeStubPlan.PageSize)
        && _probePageStart.TryGetEndExclusive(
                X64ProtectionProbeStubPlan.PageSize,
                out OS.Boot.VirtualAddress expectedEnd)
            == OS.Boot.AddressValueStatus.Success
        && expectedEnd.Equals(_probePageEndExclusive)
        && _entryOffset
            <= X64ProtectionProbeStubPlan.PageSize
                - X64ProtectionProbeStubPlan.RequiredByteLength
        && (_entryOffset
                & (X64ProtectionProbeStubPlan
                    .EntryAlignment - 1)) == 0
        && _probePageStart.TryAdd(
                _entryOffset,
                out OS.Boot.VirtualAddress expectedEntry)
            == OS.Boot.AddressValueStatus.Success
        && expectedEntry.Equals(_entryAddress)
        && _entryAddress.IsAligned(
            X64ProtectionProbeStubPlan.EntryAlignment)
        && _availableCapacity
            == X64ProtectionProbeStubPlan.PageSize
        && _requiredByteCount
            == X64ProtectionProbeStubPlan.RequiredByteLength
        && _bytesWrittenOrVerified == _requiredByteCount
        && _returnValue
            == X64ProtectionProbeStubPlan.ExpectedReturnValue
        && _instructionCount
            == X64ProtectionProbeStubPlan
                .AuditedInstructionCount;

    internal static X64ProtectionProbeStubPlanResult Success(
        X64ProtectionProbeStubPhase phase,
        OS.Boot.VirtualAddress probePageStart,
        OS.Boot.VirtualAddress probePageEndExclusive,
        OS.Boot.VirtualAddress entryAddress,
        ulong entryOffset,
        ulong availableCapacity)
    {
        return new X64ProtectionProbeStubPlanResult(
            X64ProtectionProbeStatus.Success,
            phase,
            probePageStart,
            probePageEndExclusive,
            entryAddress,
            entryOffset,
            availableCapacity,
            X64ProtectionProbeStubPlan.RequiredByteLength,
            X64ProtectionProbeStubPlan.RequiredByteLength,
            X64ProtectionProbeStubPlan.ExpectedReturnValue,
            (uint)X64ProtectionProbeStubPlan
                .AuditedInstructionCount);
    }

    internal static X64ProtectionProbeStubPlanResult Fail(
        X64ProtectionProbeStatus status,
        ulong availableCapacity = 0)
    {
        return new X64ProtectionProbeStubPlanResult(
            status,
            X64ProtectionProbeStubPhase.Uninitialized,
            default,
            default,
            default,
            0,
            availableCapacity,
            X64ProtectionProbeStubPlan.RequiredByteLength,
            0,
            X64ProtectionProbeStubPlan.ExpectedReturnValue,
            (uint)X64ProtectionProbeStubPlan
                .AuditedInstructionCount);
    }
}

/// <summary>
/// One exact event in the modeled future execution transcript.
/// </summary>
public readonly struct X64ProtectionProbeEvent
{
    public const ulong StructureSize = 24;
    public const ushort StructureVersion = 1;

    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly X64ProtectionProbeEventKind _kind;
    private readonly ulong _ordinal;
    private readonly ulong _reserved;

    public X64ProtectionProbeEvent(
        X64ProtectionProbeEventKind kind,
        ulong ordinal)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _kind = kind;
        _ordinal = ordinal;
        _reserved = 0;
    }

    public bool IsInitialized =>
        _structureSize == StructureSize
        && _structureVersion == StructureVersion
        && _isInitialized == 1
        && _reserved == 0;

    public X64ProtectionProbeEventKind Kind => _kind;

    public ulong Ordinal => _ordinal;

    public X64ProtectionProbeStatus Validate()
    {
        if (!IsInitialized ||
            _kind < X64ProtectionProbeEventKind.StubMaterialized ||
            _kind
                > X64ProtectionProbeEventKind
                    .ConstantReturnAndCanaryObserved)
        {
            return X64ProtectionProbeStatus.InvalidEvent;
        }

        return X64ProtectionProbeStatus.Success;
    }
}

/// <summary>
/// Caller-supplied modeled outcome. It intentionally carries no live-evidence
/// bit; IsLiveEvidence is always false.
/// </summary>
public readonly struct X64ProtectionProbeObservation
{
    public const ulong StructureSize = 56;
    public const ushort StructureVersion = 1;

    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly X64ProtectionProbeTerminalKind _terminalKind;
    private readonly OS.Boot.VirtualAddress _entryAddress;
    private readonly ulong _invocationCount;
    private readonly uint _expectedReturnValue;
    private readonly uint _observedReturnValue;
    private readonly ulong _canaryBefore;
    private readonly ulong _canaryAfter;
    private readonly ulong _reserved;

    private X64ProtectionProbeObservation(
        X64ProtectionProbeTerminalKind terminalKind,
        OS.Boot.VirtualAddress entryAddress,
        ulong invocationCount,
        uint expectedReturnValue,
        uint observedReturnValue,
        ulong canaryBefore,
        ulong canaryAfter)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _terminalKind = terminalKind;
        _entryAddress = entryAddress;
        _invocationCount = invocationCount;
        _expectedReturnValue = expectedReturnValue;
        _observedReturnValue = observedReturnValue;
        _canaryBefore = canaryBefore;
        _canaryAfter = canaryAfter;
        _reserved = 0;
    }

    public bool IsInitialized =>
        _structureSize == StructureSize
        && _structureVersion == StructureVersion
        && _isInitialized == 1
        && _reserved == 0;

    public X64ProtectionProbeTerminalKind TerminalKind =>
        _terminalKind;

    public OS.Boot.VirtualAddress EntryAddress => _entryAddress;

    public ulong InvocationCount => _invocationCount;

    public uint ExpectedReturnValue => _expectedReturnValue;

    public uint ObservedReturnValue => _observedReturnValue;

    public ulong CanaryBefore => _canaryBefore;

    public ulong CanaryAfter => _canaryAfter;

    public bool IsLiveEvidence => false;

    public static X64ProtectionProbeObservation CreateModeled(
        X64ProtectionProbeTerminalKind terminalKind,
        OS.Boot.VirtualAddress entryAddress,
        ulong invocationCount,
        uint observedReturnValue,
        ulong canaryBefore,
        ulong canaryAfter)
    {
        return new X64ProtectionProbeObservation(
            terminalKind,
            entryAddress,
            invocationCount,
            X64ProtectionProbeStubPlan.ExpectedReturnValue,
            observedReturnValue,
            canaryBefore,
            canaryAfter);
    }

    public X64ProtectionProbeStatus Validate()
    {
        if (!IsInitialized || !_entryAddress.IsValid)
        {
            return X64ProtectionProbeStatus.InvalidObservation;
        }

        if (_terminalKind
            != X64ProtectionProbeTerminalKind.ModeledReturn)
        {
            return X64ProtectionProbeStatus.InvalidTerminalKind;
        }

        if (_invocationCount != 1)
        {
            return X64ProtectionProbeStatus
                .InvocationCountMustBeOne;
        }

        if (_expectedReturnValue
                != X64ProtectionProbeStubPlan
                    .ExpectedReturnValue ||
            _observedReturnValue != _expectedReturnValue)
        {
            return X64ProtectionProbeStatus.UnexpectedReturnValue;
        }

        if (_canaryBefore == 0 ||
            _canaryAfter != _canaryBefore)
        {
            return X64ProtectionProbeStatus.CanaryMismatch;
        }

        return X64ProtectionProbeStatus.Success;
    }
}

/// <summary>
/// Aggregate caller-owned audit result for the future protection probe.
/// </summary>
public readonly struct X64ProtectionProbePlanResult
{
    public const ulong StructureSize = 744;
    public const ushort StructureVersion = 1;
    public const ulong NoIndex = ~0UL;

    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly X64ProtectionProbeStatus _status;
    private readonly X64ProtectionProbeStatus _stubStatus;
    private readonly X64PageMapStatus _pageMapStatus;
    private readonly X64InvlpgStubPlanStatus _invlpgStatus;
    private readonly X64ProtectionProbeTerminalKind _terminalKind;
    private readonly uint _reserved;
    private readonly OS.Boot.BootMemoryRegionRecord _probeAllocation;
    private readonly BootReservedRange _probeReservation;
    private readonly X64ProtectionProbeStubPlanResult _probeStub;
    private readonly X64PageMapResult _protectionMutation;
    private readonly X64InvlpgStubPlanResult _invlpgStub;
    private readonly X64InvlpgInvocationPlanResult _invlpgInvocation;
    private readonly X64ProtectionProbeObservation _observation;
    private readonly ulong _eventCount;
    private readonly ulong _eventCapacity;
    private readonly ulong _validatedEventCount;
    private readonly ulong _invalidEventIndex;
    private readonly ulong _leafReadbackRaw;

    private X64ProtectionProbePlanResult(
        X64ProtectionProbeStatus status,
        X64ProtectionProbeStatus stubStatus,
        X64PageMapStatus pageMapStatus,
        X64InvlpgStubPlanStatus invlpgStatus,
        X64ProtectionProbeTerminalKind terminalKind,
        OS.Boot.BootMemoryRegionRecord probeAllocation,
        BootReservedRange probeReservation,
        X64ProtectionProbeStubPlanResult probeStub,
        X64PageMapResult protectionMutation,
        X64InvlpgStubPlanResult invlpgStub,
        X64InvlpgInvocationPlanResult invlpgInvocation,
        X64ProtectionProbeObservation observation,
        ulong eventCount,
        ulong eventCapacity,
        ulong validatedEventCount,
        ulong invalidEventIndex,
        ulong leafReadbackRaw)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _status = status;
        _stubStatus = stubStatus;
        _pageMapStatus = pageMapStatus;
        _invlpgStatus = invlpgStatus;
        _terminalKind = terminalKind;
        _reserved = 0;
        _probeAllocation = probeAllocation;
        _probeReservation = probeReservation;
        _probeStub = probeStub;
        _protectionMutation = protectionMutation;
        _invlpgStub = invlpgStub;
        _invlpgInvocation = invlpgInvocation;
        _observation = observation;
        _eventCount = eventCount;
        _eventCapacity = eventCapacity;
        _validatedEventCount = validatedEventCount;
        _invalidEventIndex = invalidEventIndex;
        _leafReadbackRaw = leafReadbackRaw;
    }

    public bool IsInitialized =>
        _structureSize == StructureSize
        && _structureVersion == StructureVersion
        && _isInitialized == 1
        && _reserved == 0;

    public X64ProtectionProbeStatus Status => _status;

    public X64ProtectionProbeStatus StubStatus => _stubStatus;

    public X64PageMapStatus PageMapStatus => _pageMapStatus;

    public X64InvlpgStubPlanStatus InvlpgStatus =>
        _invlpgStatus;

    public X64ProtectionProbeTerminalKind TerminalKind =>
        _terminalKind;

    public OS.Boot.BootMemoryRegionRecord ProbeAllocation =>
        _probeAllocation;

    public BootReservedRange ProbeReservation => _probeReservation;

    public X64ProtectionProbeStubPlanResult ProbeStub =>
        _probeStub;

    public X64PageMapResult ProtectionMutation =>
        _protectionMutation;

    public X64InvlpgStubPlanResult InvlpgStub => _invlpgStub;

    public X64InvlpgInvocationPlanResult InvlpgInvocation =>
        _invlpgInvocation;

    public X64ProtectionProbeObservation Observation =>
        _observation;

    public ulong EventCount => _eventCount;

    public ulong EventCapacity => _eventCapacity;

    public ulong ValidatedEventCount => _validatedEventCount;

    public ulong InvalidEventIndex => _invalidEventIndex;

    public ulong LeafReadbackRaw => _leafReadbackRaw;

    public bool Succeeded =>
        IsInitialized
        && _status == X64ProtectionProbeStatus.Success
        && _stubStatus == X64ProtectionProbeStatus.Success
        && _pageMapStatus == X64PageMapStatus.Success
        && _invlpgStatus == X64InvlpgStubPlanStatus.Success
        && _terminalKind
            == X64ProtectionProbeTerminalKind.ModeledReturn
        && _probeStub.ReadBackVerified
        && _protectionMutation.Succeeded
        && _invlpgStub.ReadBackVerified
        && _invlpgInvocation.Succeeded
        && _observation.Validate()
            == X64ProtectionProbeStatus.Success
        && _eventCount
            == X64ProtectionProbeStubPlan.RequiredEventCount
        && _eventCapacity >= _eventCount
        && _eventCapacity
            <= X64ProtectionProbeStubPlan.RequiredEventCount
        && _validatedEventCount == _eventCount
        && _invalidEventIndex == NoIndex
        && _leafReadbackRaw
            == _protectionMutation.ReplacementEntry.RawValue;

    internal static X64ProtectionProbePlanResult Success(
        OS.Boot.BootMemoryRegionRecord probeAllocation,
        BootReservedRange probeReservation,
        X64ProtectionProbeStubPlanResult probeStub,
        X64PageMapResult protectionMutation,
        X64InvlpgStubPlanResult invlpgStub,
        X64InvlpgInvocationPlanResult invlpgInvocation,
        X64ProtectionProbeObservation observation,
        ulong eventCount,
        ulong eventCapacity,
        ulong leafReadbackRaw)
    {
        return new X64ProtectionProbePlanResult(
            X64ProtectionProbeStatus.Success,
            X64ProtectionProbeStatus.Success,
            X64PageMapStatus.Success,
            X64InvlpgStubPlanStatus.Success,
            observation.TerminalKind,
            probeAllocation,
            probeReservation,
            probeStub,
            protectionMutation,
            invlpgStub,
            invlpgInvocation,
            observation,
            eventCount,
            eventCapacity,
            eventCount,
            NoIndex,
            leafReadbackRaw);
    }

    internal static X64ProtectionProbePlanResult Fail(
        X64ProtectionProbeStatus status,
        OS.Boot.BootMemoryRegionRecord probeAllocation = default,
        BootReservedRange probeReservation = default,
        X64ProtectionProbeStubPlanResult probeStub = default,
        X64PageMapResult protectionMutation = default,
        X64InvlpgStubPlanResult invlpgStub = default,
        X64InvlpgInvocationPlanResult invlpgInvocation = default,
        X64ProtectionProbeObservation observation = default,
        ulong eventCount = 0,
        ulong eventCapacity = 0,
        ulong validatedEventCount = 0,
        ulong invalidEventIndex = NoIndex,
        ulong leafReadbackRaw = 0)
    {
        X64InvlpgStubPlanStatus invlpgStatus =
            invlpgStub.Status
                != X64InvlpgStubPlanStatus.Success
            ? invlpgStub.Status
            : invlpgInvocation.Status;
        return new X64ProtectionProbePlanResult(
            status,
            probeStub.Status,
            protectionMutation.Status,
            invlpgStatus,
            observation.TerminalKind,
            probeAllocation,
            probeReservation,
            probeStub,
            protectionMutation,
            invlpgStub,
            invlpgInvocation,
            observation,
            eventCount,
            eventCapacity,
            validatedEventCount,
            invalidEventIndex,
            leafReadbackRaw);
    }
}

/// <summary>
/// Emits and audits the exact MOV EAX,imm32; RET probe as ordinary data and
/// composes the host-only RW+NX to RO+X model. No emitted byte is invoked by
/// this type or by its host tests.
/// </summary>
public static unsafe class X64ProtectionProbeStubPlan
{
    public const ulong PageSize = 4096;
    public const ulong EntryAlignment = 16;
    public const ulong RequiredPointerAlignment = 16;
    public const ulong RequiredByteLength = 6;
    public const ulong AuditedInstructionCount = 2;
    public const uint ExpectedReturnValue = 0x4f53_4f4b;
    public const ulong RequiredEventCount = 9;
    public const bool HasExternalRelocations = false;
    public const bool UsesWindowsX64Abi = true;
    public const bool TouchesNonvolatileRegisters = false;
    public const bool TouchesXmm6ThroughXmm15 = false;
    public const bool PerformsLiveProtectionChange = false;
    public const bool PerformsLiveExecution = false;
    public const bool HostTestsInvokeBytes = false;
    public const bool IsLiveEvidence = false;

    public static X64ProtectionProbeStubPlanResult Emit(
        byte* destination,
        ulong modeledDestinationAddress,
        ulong destinationCapacity,
        OS.Boot.BootMemoryRegionRecord probeAllocation,
        BootReservedRange probeReservation,
        ulong entryOffset)
    {
        X64ProtectionProbeStatus geometryStatus =
            ValidateProbeGeometry(
                probeAllocation,
                probeReservation,
                entryOffset,
                out OS.Boot.VirtualAddress pageStart,
                out OS.Boot.VirtualAddress pageEnd,
                out OS.Boot.VirtualAddress entryAddress);
        if (geometryStatus != X64ProtectionProbeStatus.Success)
        {
            return X64ProtectionProbeStubPlanResult.Fail(
                geometryStatus,
                destinationCapacity);
        }

        X64ProtectionProbeStatus storageStatus =
            ValidateStorage(
                destination,
                modeledDestinationAddress,
                destinationCapacity,
                probeAllocation.VirtualStart,
                X64ProtectionProbeStatus.MissingDestination,
                X64ProtectionProbeStatus.MisalignedDestination,
                X64ProtectionProbeStatus
                    .DestinationCapacityTooSmall,
                X64ProtectionProbeStatus
                    .DestinationCapacityExceedsPage,
                X64ProtectionProbeStatus
                    .DestinationSpanOverflow,
                X64ProtectionProbeStatus
                    .DestinationAddressMismatch);
        if (storageStatus != X64ProtectionProbeStatus.Success)
        {
            return X64ProtectionProbeStubPlanResult.Fail(
                storageStatus,
                destinationCapacity);
        }

        byte* entry = destination + entryOffset;
        entry[0] = 0xb8;
        entry[1] = (byte)(ExpectedReturnValue & 0xffU);
        entry[2] = (byte)((ExpectedReturnValue >> 8) & 0xffU);
        entry[3] = (byte)((ExpectedReturnValue >> 16) & 0xffU);
        entry[4] = (byte)((ExpectedReturnValue >> 24) & 0xffU);
        entry[5] = 0xc3;

        return X64ProtectionProbeStubPlanResult.Success(
            X64ProtectionProbeStubPhase.Materialized,
            pageStart,
            pageEnd,
            entryAddress,
            entryOffset,
            destinationCapacity);
    }

    public static X64ProtectionProbeStubPlanResult ValidateReadback(
        byte* source,
        ulong modeledSourceAddress,
        ulong sourceCapacity,
        X64ProtectionProbeStubPlanResult expected)
    {
        if (!expected.Succeeded ||
            expected.Phase
                != X64ProtectionProbeStubPhase.Materialized)
        {
            return X64ProtectionProbeStubPlanResult.Fail(
                X64ProtectionProbeStatus
                    .ExpectedStubNotMaterialized,
                sourceCapacity);
        }

        if (expected.ProbePageStart
                .TryGetRawValueForHardwareRegisterBoundary(
                    out ulong rawPageStart)
            != OS.Boot.AddressValueStatus.Success)
        {
            return X64ProtectionProbeStubPlanResult.Fail(
                X64ProtectionProbeStatus.InvalidExpectedStub,
                sourceCapacity);
        }

        X64ProtectionProbeStatus storageStatus =
            ValidateStorage(
                source,
                modeledSourceAddress,
                sourceCapacity,
                rawPageStart,
                X64ProtectionProbeStatus.MissingSource,
                X64ProtectionProbeStatus.MisalignedSource,
                X64ProtectionProbeStatus.SourceCapacityTooSmall,
                X64ProtectionProbeStatus.SourceCapacityExceedsPage,
                X64ProtectionProbeStatus.SourceSpanOverflow,
                X64ProtectionProbeStatus.SourceAddressMismatch);
        if (storageStatus != X64ProtectionProbeStatus.Success)
        {
            return X64ProtectionProbeStubPlanResult.Fail(
                storageStatus,
                sourceCapacity);
        }

        byte* entry = source + expected.EntryOffset;
        if (entry[0] != 0xb8 ||
            entry[1] != (byte)(ExpectedReturnValue & 0xffU) ||
            entry[2] != (byte)((ExpectedReturnValue >> 8) & 0xffU) ||
            entry[3] != (byte)((ExpectedReturnValue >> 16) & 0xffU) ||
            entry[4] != (byte)((ExpectedReturnValue >> 24) & 0xffU) ||
            entry[5] != 0xc3)
        {
            return X64ProtectionProbeStubPlanResult.Fail(
                X64ProtectionProbeStatus.ByteReadbackMismatch,
                sourceCapacity);
        }

        return X64ProtectionProbeStubPlanResult.Success(
            X64ProtectionProbeStubPhase.ReadBackVerified,
            expected.ProbePageStart,
            expected.ProbePageEndExclusive,
            expected.EntryAddress,
            expected.EntryOffset,
            sourceCapacity);
    }

    public static X64ProtectionProbePlanResult Audit(
        OS.Boot.BootMemoryRegionRecord probeAllocation,
        BootReservedRange probeReservation,
        OS.Boot.BootMemoryRegionRecord transitionCodeAllocation,
        OS.Boot.BootMemoryRegionRecord tableStateAllocation,
        PageProtection tableStateProtection,
        X64ProtectionProbeStubPlanResult probeStub,
        X64PageMapResult protectionMutation,
        ulong leafReadbackRaw,
        X64InvlpgStubPlanResult invlpgStub,
        X64InvlpgInvocationPlanResult invlpgInvocation,
        X64ProtectionProbeObservation observation,
        X64ProtectionProbeEvent* events,
        ulong eventCount,
        ulong eventCapacity)
    {
        X64ProtectionProbeStatus allocationStatus =
            ValidateAllocationAndReservation(
                probeAllocation,
                probeReservation);
        if (allocationStatus != X64ProtectionProbeStatus.Success)
        {
            return X64ProtectionProbePlanResult.Fail(
                allocationStatus,
                probeAllocation,
                probeReservation,
                probeStub,
                protectionMutation,
                invlpgStub,
                invlpgInvocation,
                observation,
                eventCount,
                eventCapacity,
                leafReadbackRaw: leafReadbackRaw);
        }

        if (!ValidateAllocation(
                transitionCodeAllocation,
                (uint)OS.Boot.UefiMemoryType.LoaderCode,
                requireOnePage: true))
        {
            return Fail(
                X64ProtectionProbeStatus
                    .InvalidTransitionCodeAllocation);
        }

        if (!ValidateAllocation(
                tableStateAllocation,
                (uint)OS.Boot.UefiMemoryType.LoaderData,
                requireOnePage: false))
        {
            return Fail(
                X64ProtectionProbeStatus
                    .InvalidTableStateAllocation);
        }

        PageProtection requiredTableStateProtection =
            (PageProtection)(
                (byte)PageProtection.Read
                | (byte)PageProtection.Write);
        if (tableStateProtection
            != requiredTableStateProtection)
        {
            return Fail(
                X64ProtectionProbeStatus
                    .InvalidTableStateProtection);
        }

        if (AllocationsOverlap(
                probeAllocation,
                transitionCodeAllocation) ||
            AllocationsOverlap(
                probeAllocation,
                tableStateAllocation) ||
            AllocationsOverlap(
                transitionCodeAllocation,
                tableStateAllocation))
        {
            return Fail(
                X64ProtectionProbeStatus.AllocationOverlap);
        }

        if (!probeStub.ReadBackVerified ||
            !TryGetVirtualAddress(
                probeAllocation.VirtualStart,
                out OS.Boot.VirtualAddress probePageStart) ||
            !probeStub.ProbePageStart.Equals(probePageStart))
        {
            return Fail(
                X64ProtectionProbeStatus.StubAllocationMismatch);
        }

        X64ProtectionProbeStatus protectionStatus =
            ValidateProtectionMutation(
                probeAllocation,
                protectionMutation,
                leafReadbackRaw);
        if (protectionStatus != X64ProtectionProbeStatus.Success)
        {
            return Fail(protectionStatus);
        }

        if (!TableImageIsContained(
                tableStateAllocation,
                protectionMutation))
        {
            return Fail(
                X64ProtectionProbeStatus
                    .TableImageOutsideStateAllocation);
        }

        if (!invlpgStub.ReadBackVerified)
        {
            return Fail(
                X64ProtectionProbeStatus.InvalidInvlpgStub);
        }

        if (!TryGetVirtualAddress(
                transitionCodeAllocation.VirtualStart,
                out OS.Boot.VirtualAddress transitionCodeStart) ||
            !invlpgStub.CodePageStart.Equals(
                transitionCodeStart))
        {
            return Fail(
                X64ProtectionProbeStatus
                    .InvlpgCodeAllocationMismatch);
        }

        if (!invlpgInvocation.Succeeded)
        {
            return Fail(
                X64ProtectionProbeStatus
                    .InvalidInvlpgInvocation);
        }

        if (!invlpgStub.Target.Target.Equals(
                protectionMutation.InvalidationRange
                    .VirtualStart) ||
            invlpgStub.Target.Ordinal != 0 ||
            !invlpgInvocation.Request.Target.Target.Equals(
                invlpgStub.Target.Target) ||
            invlpgInvocation.Request.Target.Ordinal
                != invlpgStub.Target.Ordinal ||
            !invlpgInvocation.Request.EntryAddress.Equals(
                invlpgStub.EntryAddress))
        {
            return Fail(
                X64ProtectionProbeStatus.InvlpgTargetMismatch);
        }

        X64ProtectionProbeStatus observationStatus =
            observation.Validate();
        if (observationStatus != X64ProtectionProbeStatus.Success)
        {
            return Fail(observationStatus);
        }

        if (!observation.EntryAddress.Equals(
                probeStub.EntryAddress))
        {
            return Fail(
                X64ProtectionProbeStatus.InvocationEntryMismatch);
        }

        X64ProtectionProbeStatus eventStatus =
            ValidateEvents(
                events,
                eventCount,
                eventCapacity,
                out ulong validatedEventCount,
                out ulong invalidEventIndex);
        if (eventStatus != X64ProtectionProbeStatus.Success)
        {
            return X64ProtectionProbePlanResult.Fail(
                eventStatus,
                probeAllocation,
                probeReservation,
                probeStub,
                protectionMutation,
                invlpgStub,
                invlpgInvocation,
                observation,
                eventCount,
                eventCapacity,
                validatedEventCount,
                invalidEventIndex,
                leafReadbackRaw);
        }

        return X64ProtectionProbePlanResult.Success(
            probeAllocation,
            probeReservation,
            probeStub,
            protectionMutation,
            invlpgStub,
            invlpgInvocation,
            observation,
            eventCount,
            eventCapacity,
            leafReadbackRaw);

        X64ProtectionProbePlanResult Fail(
            X64ProtectionProbeStatus status)
        {
            return X64ProtectionProbePlanResult.Fail(
                status,
                probeAllocation,
                probeReservation,
                probeStub,
                protectionMutation,
                invlpgStub,
                invlpgInvocation,
                observation,
                eventCount,
                eventCapacity,
                leafReadbackRaw: leafReadbackRaw);
        }
    }

    private static X64ProtectionProbeStatus
        ValidateProtectionMutation(
            OS.Boot.BootMemoryRegionRecord probeAllocation,
            X64PageMapResult protectionMutation,
            ulong leafReadbackRaw)
    {
        if (!protectionMutation.Succeeded ||
            protectionMutation.Request.Operation
                != X64PageMapOperation.Protect)
        {
            return X64ProtectionProbeStatus
                .InvalidProtectionMutation;
        }

        if (!TryGetVirtualAddress(
                probeAllocation.VirtualStart,
                out OS.Boot.VirtualAddress virtualStart) ||
            !protectionMutation.Request.VirtualAddress.Equals(
                virtualStart) ||
            !protectionMutation.InvalidationRange
                .VirtualStart.Equals(virtualStart))
        {
            return X64ProtectionProbeStatus
                .ProtectionTargetMismatch;
        }

        X64PageTableEntryResult before =
            X64PageTableEntry.TryDecode(
                X64PageTableLevel.Pte,
                protectionMutation.PreviousEntry.RawValue,
                protectionMutation.Request.Preconditions);
        X64PageTableEntryResult after =
            X64PageTableEntry.TryDecode(
                X64PageTableLevel.Pte,
                protectionMutation.ReplacementEntry.RawValue,
                protectionMutation.Request.Preconditions);
        if (!before.Succeeded || !after.Succeeded)
        {
            return X64ProtectionProbeStatus
                .InvalidProtectionMutation;
        }

        PageProtection readWrite = (PageProtection)(
            (byte)PageProtection.Read
            | (byte)PageProtection.Write);
        PageProtection readExecute = (PageProtection)(
            (byte)PageProtection.Read
            | (byte)PageProtection.Execute);
        if (before.Protection != readWrite)
        {
            return X64ProtectionProbeStatus
                .InitialProtectionMismatch;
        }

        if (after.Protection != readExecute)
        {
            return X64ProtectionProbeStatus
                .FinalProtectionMismatch;
        }

        if (!before.PhysicalAddress.Equals(
                after.PhysicalAddress) ||
            before.PhysicalAddress
                .TryGetRawValueForHardwareRegisterBoundary(
                    out ulong rawPhysical)
                != OS.Boot.AddressValueStatus.Success ||
            rawPhysical != probeAllocation.PhysicalStart)
        {
            return X64ProtectionProbeStatus
                .ProtectionPhysicalMismatch;
        }

        if (before.CachePolicy != PageCachePolicy.WriteBack ||
            after.CachePolicy != PageCachePolicy.WriteBack)
        {
            return X64ProtectionProbeStatus
                .ProtectionCacheMismatch;
        }

        if (leafReadbackRaw
            != protectionMutation.ReplacementEntry.RawValue)
        {
            return X64ProtectionProbeStatus.LeafReadbackMismatch;
        }

        return X64ProtectionProbeStatus.Success;
    }

    private static X64ProtectionProbeStatus ValidateEvents(
        X64ProtectionProbeEvent* events,
        ulong eventCount,
        ulong eventCapacity,
        out ulong validatedEventCount,
        out ulong invalidEventIndex)
    {
        validatedEventCount = 0;
        invalidEventIndex = X64ProtectionProbePlanResult.NoIndex;
        if (eventCount != RequiredEventCount)
        {
            return X64ProtectionProbeStatus.InvalidEventCount;
        }

        if (eventCapacity < eventCount)
        {
            return X64ProtectionProbeStatus.EventCapacityTooSmall;
        }

        if (eventCapacity > RequiredEventCount)
        {
            return X64ProtectionProbeStatus
                .EventCapacityExceedsLimit;
        }

        if (events == null)
        {
            return X64ProtectionProbeStatus.MissingEvents;
        }

        ulong address = (ulong)events;
        if ((address & 7) != 0)
        {
            return X64ProtectionProbeStatus.MisalignedEvents;
        }

        if (eventCapacity
                > ~0UL / X64ProtectionProbeEvent.StructureSize ||
            address
                > ~0UL
                    - eventCapacity
                        * X64ProtectionProbeEvent.StructureSize)
        {
            return X64ProtectionProbeStatus.EventSpanOverflow;
        }

        for (ulong index = 0; index < eventCount; index++)
        {
            X64ProtectionProbeStatus eventStatus =
                events[index].Validate();
            if (eventStatus != X64ProtectionProbeStatus.Success)
            {
                invalidEventIndex = index;
                return eventStatus;
            }

            X64ProtectionProbeEventKind expected =
                (X64ProtectionProbeEventKind)(index + 1);
            if (events[index].Ordinal != index ||
                events[index].Kind != expected)
            {
                invalidEventIndex = index;
                if (events[index].Kind
                        == X64ProtectionProbeEventKind
                            .ProbeInvocationPlanned &&
                    index <= 3)
                {
                    return X64ProtectionProbeStatus
                        .ExecuteBeforeProtectRejected;
                }

                return X64ProtectionProbeStatus
                    .EventOrderMismatch;
            }

            validatedEventCount++;
        }

        return X64ProtectionProbeStatus.Success;
    }

    private static bool TableImageIsContained(
        OS.Boot.BootMemoryRegionRecord tableStateAllocation,
        X64PageMapResult protectionMutation)
    {
        if (!protectionMutation.Succeeded ||
            protectionMutation.PageImagePhysicalStart
                .TryGetRawValueForHardwareRegisterBoundary(
                    out ulong pageImageStart)
                != OS.Boot.AddressValueStatus.Success ||
            protectionMutation.PageImagePhysicalEndExclusive
                .TryGetRawValueForHardwareRegisterBoundary(
                    out ulong pageImageEnd)
                != OS.Boot.AddressValueStatus.Success)
        {
            return false;
        }

        return pageImageStart
                >= tableStateAllocation.PhysicalStart
            && pageImageEnd > pageImageStart
            && pageImageEnd
                <= tableStateAllocation.PhysicalEndExclusive;
    }

    private static X64ProtectionProbeStatus ValidateProbeGeometry(
        OS.Boot.BootMemoryRegionRecord probeAllocation,
        BootReservedRange probeReservation,
        ulong entryOffset,
        out OS.Boot.VirtualAddress pageStart,
        out OS.Boot.VirtualAddress pageEnd,
        out OS.Boot.VirtualAddress entryAddress)
    {
        pageStart = default;
        pageEnd = default;
        entryAddress = default;
        X64ProtectionProbeStatus status =
            ValidateAllocationAndReservation(
                probeAllocation,
                probeReservation);
        if (status != X64ProtectionProbeStatus.Success)
        {
            return status;
        }

        if (!TryGetVirtualAddress(
                probeAllocation.VirtualStart,
                out pageStart) ||
            pageStart.TryGetEndExclusive(
                    PageSize,
                    out pageEnd)
                != OS.Boot.AddressValueStatus.Success)
        {
            return X64ProtectionProbeStatus
                .InvalidProbeAllocation;
        }

        if (entryOffset > PageSize - RequiredByteLength)
        {
            return X64ProtectionProbeStatus
                .EntryOutsideProbePage;
        }

        if ((entryOffset & (EntryAlignment - 1)) != 0)
        {
            return X64ProtectionProbeStatus.MisalignedEntry;
        }

        if (pageStart.TryAdd(
                entryOffset,
                out entryAddress)
            != OS.Boot.AddressValueStatus.Success ||
            !entryAddress.IsAligned(EntryAlignment))
        {
            return X64ProtectionProbeStatus.InvalidEntryOffset;
        }

        return X64ProtectionProbeStatus.Success;
    }

    private static X64ProtectionProbeStatus
        ValidateAllocationAndReservation(
            OS.Boot.BootMemoryRegionRecord allocation,
            BootReservedRange reservation)
    {
        if (!ValidateAllocation(
                allocation,
                (uint)OS.Boot.UefiMemoryType.LoaderCode,
                requireOnePage: false))
        {
            return allocation.UefiType
                    == (uint)OS.Boot.UefiMemoryType.LoaderCode
                ? X64ProtectionProbeStatus
                    .InvalidProbeAllocation
                : X64ProtectionProbeStatus
                    .ProbeAllocationMustBeLoaderCode;
        }

        if (allocation.PageCount != 1 ||
            allocation.ByteLength != PageSize)
        {
            return X64ProtectionProbeStatus
                .ProbeAllocationMustBeOnePage;
        }

        if (allocation.VirtualStart
            != allocation.PhysicalStart)
        {
            return X64ProtectionProbeStatus
                .ProbeAllocationNotIdentityMapped;
        }

        if (!reservation.IsValid)
        {
            return X64ProtectionProbeStatus
                .InvalidProbeReservation;
        }

        if (reservation.Kind
            != BootReservationKind.PageProtectionProbe)
        {
            return X64ProtectionProbeStatus
                .ProbeReservationKindMismatch;
        }

        if (reservation.PhysicalStart
                .TryGetRawValueForHardwareRegisterBoundary(
                    out ulong reservedStart)
            != OS.Boot.AddressValueStatus.Success ||
            reservation.PhysicalEndExclusive
                .TryGetRawValueForHardwareRegisterBoundary(
                    out ulong reservedEnd)
                != OS.Boot.AddressValueStatus.Success ||
            reservedStart != allocation.PhysicalStart ||
            reservedEnd != allocation.PhysicalEndExclusive ||
            reservation.PageCount != allocation.PageCount ||
            reservation.ByteLength != allocation.ByteLength)
        {
            return X64ProtectionProbeStatus
                .ProbeReservationRangeMismatch;
        }

        return X64ProtectionProbeStatus.Success;
    }

    private static bool ValidateAllocation(
        OS.Boot.BootMemoryRegionRecord allocation,
        uint expectedType,
        bool requireOnePage)
    {
        if (allocation.UefiType != expectedType ||
            allocation.PhysicalStart == 0 ||
            (allocation.PhysicalStart & (PageSize - 1)) != 0 ||
            allocation.VirtualStart == 0 ||
            (allocation.VirtualStart & (PageSize - 1)) != 0 ||
            allocation.PageCount == 0 ||
            allocation.PageCount > ~0UL / PageSize ||
            allocation.ByteLength
                != allocation.PageCount * PageSize ||
            allocation.PhysicalStart
                > ~0UL - allocation.ByteLength ||
            allocation.PhysicalEndExclusive
                != allocation.PhysicalStart
                    + allocation.ByteLength ||
            allocation.VirtualStart
                > ~0UL - allocation.ByteLength ||
            allocation.VirtualStart
                >= X64TlbInvalidationPlan
                    .LowCanonicalEndExclusive ||
            allocation.VirtualStart + allocation.ByteLength
                > X64TlbInvalidationPlan
                    .LowCanonicalEndExclusive)
        {
            return false;
        }

        return !requireOnePage ||
            allocation.PageCount == 1;
    }

    private static bool AllocationsOverlap(
        OS.Boot.BootMemoryRegionRecord first,
        OS.Boot.BootMemoryRegionRecord second)
    {
        bool physicalOverlap =
            first.PhysicalStart < second.PhysicalEndExclusive &&
            second.PhysicalStart < first.PhysicalEndExclusive;
        ulong firstVirtualEnd =
            first.VirtualStart + first.ByteLength;
        ulong secondVirtualEnd =
            second.VirtualStart + second.ByteLength;
        bool virtualOverlap =
            first.VirtualStart < secondVirtualEnd &&
            second.VirtualStart < firstVirtualEnd;
        return physicalOverlap || virtualOverlap;
    }

    private static bool TryGetVirtualAddress(
        ulong rawAddress,
        out OS.Boot.VirtualAddress address)
    {
        return OS.Boot.VirtualAddress
                .TryCreateAlignedFromHardwareRegisterBoundary(
                    rawAddress,
                    PageSize,
                    out address)
            == OS.Boot.AddressValueStatus.Success;
    }

    private static X64ProtectionProbeStatus ValidateStorage(
        byte* storage,
        ulong modeledAddress,
        ulong capacity,
        ulong expectedModeledAddress,
        X64ProtectionProbeStatus missingStatus,
        X64ProtectionProbeStatus misalignedStatus,
        X64ProtectionProbeStatus capacitySmallStatus,
        X64ProtectionProbeStatus capacityLargeStatus,
        X64ProtectionProbeStatus spanOverflowStatus,
        X64ProtectionProbeStatus addressMismatchStatus)
    {
        if (capacity < RequiredByteLength)
        {
            return capacitySmallStatus;
        }

        if (capacity != PageSize)
        {
            return capacityLargeStatus;
        }

        if (storage == null)
        {
            return missingStatus;
        }

        ulong storageAddress = (ulong)storage;
        if ((storageAddress
                & (RequiredPointerAlignment - 1)) != 0)
        {
            return misalignedStatus;
        }

        if (storageAddress > ~0UL - capacity)
        {
            return spanOverflowStatus;
        }

        if (modeledAddress != expectedModeledAddress)
        {
            return addressMismatchStatus;
        }

        return X64ProtectionProbeStatus.Success;
    }
}
