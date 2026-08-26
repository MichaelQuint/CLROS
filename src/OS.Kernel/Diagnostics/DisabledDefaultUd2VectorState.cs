namespace OS.Kernel.Diagnostics;

/// <summary>
/// Stable status contract for the disabled-by-default M02-013 host leaf.
/// Values are append-only. ReadyDisabled and ObservationAccepted are host
/// metadata results only; neither status is guest execution evidence.
/// </summary>
public enum DisabledDefaultUd2VectorStateStatus : byte
{
    Uninitialized = 0,
    ReadyDisabled = 1,
    ObservationAccepted = 2,
    InvalidRequestState = 3,
    EnabledByDefaultRejected = 4,
    ExecutionRequestedRejected = 5,
    ExecutionAttemptedRejected = 6,
    OwnedSerialUnavailable = 7,
    OwnedGdtTssUnavailable = 8,
    OwnedIdtUnavailable = 9,
    InvalidOpcodeHandlerUnavailable = 10,
    NullUd2Address = 11,
    NonCanonicalUd2Address = 12,
    Ud2AddressSpanOverflow = 13,
    NonCanonicalUd2AddressSpan = 14,
    InvalidExpectedState = 15,
    VectorMismatch = 16,
    ErrorCodeMetadataMismatch = 17,
    ErrorCodeMismatch = 18,
    RegisterStateMismatch = 19
}

/// <summary>
/// Identifies the first deterministic synthetic-frame comparison.
/// </summary>
public enum DisabledDefaultUd2VectorField : byte
{
    None = 0,
    Vector = 1,
    HasErrorCode = 2,
    ErrorCode = 3,
    Rip = 4,
    Rsp = 5,
    Rbp = 6,
    Rflags = 7,
    Cr2 = 8,
    Cr3 = 9,
    Rax = 10,
    Rbx = 11,
    Rcx = 12,
    Rdx = 13,
    Rsi = 14,
    Rdi = 15,
    R8 = 16,
    R9 = 17,
    R10 = 18,
    R11 = 19,
    R12 = 20,
    R13 = 21,
    R14 = 22,
    R15 = 23
}

public enum DisabledDefaultUd2VectorStateOperation : byte
{
    Uninitialized = 0,
    Plan = 1,
    AuditSyntheticObservation = 2
}

/// <summary>
/// Versioned, unmanaged caller input. Readiness fields are caller
/// attestations, not observations of active guest descriptor or serial state.
/// </summary>
public readonly struct DisabledDefaultUd2VectorStateRequest
{
    public const uint StructureSize = 32;
    public const ushort StructureVersion = 1;

    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly byte _enabledByDefault;
    private readonly byte _executionRequested;
    private readonly byte _executionAttempted;
    private readonly byte _ownedSerialReady;
    private readonly byte _ownedGdtTssReady;
    private readonly byte _ownedIdtReady;
    private readonly byte _invalidOpcodeHandlerReady;
    private readonly ushort _reserved0;
    private readonly ulong _ud2Address;
    private readonly ulong _reserved1;

    private DisabledDefaultUd2VectorStateRequest(
        ulong ud2Address,
        bool enabledByDefault,
        bool executionRequested,
        bool executionAttempted,
        bool ownedSerialReady,
        bool ownedGdtTssReady,
        bool ownedIdtReady,
        bool invalidOpcodeHandlerReady)
    {
        _structureSize = StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _enabledByDefault = enabledByDefault ? (byte)1 : (byte)0;
        _executionRequested = executionRequested ? (byte)1 : (byte)0;
        _executionAttempted = executionAttempted ? (byte)1 : (byte)0;
        _ownedSerialReady = ownedSerialReady ? (byte)1 : (byte)0;
        _ownedGdtTssReady = ownedGdtTssReady ? (byte)1 : (byte)0;
        _ownedIdtReady = ownedIdtReady ? (byte)1 : (byte)0;
        _invalidOpcodeHandlerReady =
            invalidOpcodeHandlerReady ? (byte)1 : (byte)0;
        _reserved0 = 0;
        _ud2Address = ud2Address;
        _reserved1 = 0;
    }

    public bool IsInitialized =>
        _structureSize == StructureSize &&
        _structureVersion == StructureVersion &&
        _isInitialized == 1 &&
        _enabledByDefault <= 1 &&
        _executionRequested <= 1 &&
        _executionAttempted <= 1 &&
        _ownedSerialReady <= 1 &&
        _ownedGdtTssReady <= 1 &&
        _ownedIdtReady <= 1 &&
        _invalidOpcodeHandlerReady <= 1 &&
        _reserved0 == 0 &&
        _reserved1 == 0;

    public bool EnabledByDefault => _enabledByDefault == 1;

    public bool ExecutionRequested => _executionRequested == 1;

    public bool ExecutionAttempted => _executionAttempted == 1;

    public bool OwnedSerialReady => _ownedSerialReady == 1;

    public bool OwnedGdtTssReady => _ownedGdtTssReady == 1;

    public bool OwnedIdtReady => _ownedIdtReady == 1;

    public bool InvalidOpcodeHandlerReady =>
        _invalidOpcodeHandlerReady == 1;

    public ulong Ud2Address => _ud2Address;

    public static DisabledDefaultUd2VectorStateRequest CreateDisabled(
        ulong ud2Address,
        bool callerAttestsOwnedSerialReady,
        bool callerAttestsOwnedGdtTssReady,
        bool callerAttestsOwnedIdtReady,
        bool callerAttestsInvalidOpcodeHandlerReady)
    {
        return new DisabledDefaultUd2VectorStateRequest(
            ud2Address,
            DisabledDefaultUd2VectorState.EnabledByDefault,
            executionRequested: false,
            executionAttempted: false,
            callerAttestsOwnedSerialReady,
            callerAttestsOwnedGdtTssReady,
            callerAttestsOwnedIdtReady,
            callerAttestsInvalidOpcodeHandlerReady);
    }

    public static DisabledDefaultUd2VectorStateRequest CreateForValidation(
        ulong ud2Address,
        bool enabledByDefault,
        bool executionRequested,
        bool executionAttempted,
        bool callerAttestsOwnedSerialReady,
        bool callerAttestsOwnedGdtTssReady,
        bool callerAttestsOwnedIdtReady,
        bool callerAttestsInvalidOpcodeHandlerReady)
    {
        return new DisabledDefaultUd2VectorStateRequest(
            ud2Address,
            enabledByDefault,
            executionRequested,
            executionAttempted,
            callerAttestsOwnedSerialReady,
            callerAttestsOwnedGdtTssReady,
            callerAttestsOwnedIdtReady,
            callerAttestsInvalidOpcodeHandlerReady);
    }
}

/// <summary>
/// Versioned, unmanaged result for request validation or a synthetic
/// CpuFaultFrame comparison. ExpectedValue and ObservedValue describe the
/// first rejected field and remain zero when no field was rejected.
/// </summary>
public readonly struct DisabledDefaultUd2VectorStateResult
{
    public const uint StructureSize = 72;
    public const ushort StructureVersion = 1;

    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly DisabledDefaultUd2VectorStateStatus _status;
    private readonly DisabledDefaultUd2VectorStateOperation _operation;
    private readonly DisabledDefaultUd2VectorField _firstMismatchField;
    private readonly ushort _comparedFieldCount;
    private readonly uint _reserved0;
    private readonly DisabledDefaultUd2VectorStateRequest _request;
    private readonly ulong _expectedValue;
    private readonly ulong _observedValue;
    private readonly ulong _reserved1;

    private DisabledDefaultUd2VectorStateResult(
        DisabledDefaultUd2VectorStateOperation operation,
        DisabledDefaultUd2VectorStateStatus status,
        DisabledDefaultUd2VectorStateRequest request,
        DisabledDefaultUd2VectorField firstMismatchField,
        ushort comparedFieldCount,
        ulong expectedValue,
        ulong observedValue)
    {
        _structureSize = StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _status = status;
        _operation = operation;
        _firstMismatchField = firstMismatchField;
        _comparedFieldCount = comparedFieldCount;
        _reserved0 = 0;
        _request = request;
        _expectedValue = expectedValue;
        _observedValue = observedValue;
        _reserved1 = 0;
    }

    public bool IsInitialized =>
        _structureSize == StructureSize &&
        _structureVersion == StructureVersion &&
        _isInitialized == 1 &&
        _status <= DisabledDefaultUd2VectorStateStatus.RegisterStateMismatch &&
        _operation <= DisabledDefaultUd2VectorStateOperation
            .AuditSyntheticObservation &&
        _firstMismatchField <= DisabledDefaultUd2VectorField.R15 &&
        _comparedFieldCount <=
            DisabledDefaultUd2VectorState.CpuFaultFrameFieldCount &&
        _reserved0 == 0 &&
        _reserved1 == 0;

    public DisabledDefaultUd2VectorStateStatus Status => _status;

    public DisabledDefaultUd2VectorStateOperation Operation => _operation;

    public DisabledDefaultUd2VectorStateRequest Request => _request;

    public DisabledDefaultUd2VectorField FirstMismatchField =>
        _firstMismatchField;

    public ushort ComparedFieldCount => _comparedFieldCount;

    public ulong ExpectedValue => _expectedValue;

    public ulong ObservedValue => _observedValue;

    public bool ReadyDisabled =>
        IsInitialized &&
        _operation == DisabledDefaultUd2VectorStateOperation.Plan &&
        _status == DisabledDefaultUd2VectorStateStatus.ReadyDisabled &&
        _firstMismatchField == DisabledDefaultUd2VectorField.None &&
        _comparedFieldCount == 0 &&
        _expectedValue == 0 &&
        _observedValue == 0 &&
        DisabledDefaultUd2VectorState.IsReadyDisabledRequest(_request);

    public bool ObservationAccepted =>
        IsInitialized &&
        _operation ==
            DisabledDefaultUd2VectorStateOperation.AuditSyntheticObservation &&
        _status ==
            DisabledDefaultUd2VectorStateStatus.ObservationAccepted &&
        _request.IsInitialized &&
        _firstMismatchField == DisabledDefaultUd2VectorField.None &&
        _comparedFieldCount ==
            DisabledDefaultUd2VectorState.CpuFaultFrameFieldCount &&
        _expectedValue == 0 &&
        _observedValue == 0 &&
        DisabledDefaultUd2VectorState.IsReadyDisabledRequest(_request);

    public bool Succeeded => ReadyDisabled || ObservationAccepted;

    public bool IsGuestEvidence => false;

    internal static DisabledDefaultUd2VectorStateResult Create(
        DisabledDefaultUd2VectorStateOperation operation,
        DisabledDefaultUd2VectorStateStatus status,
        DisabledDefaultUd2VectorStateRequest request,
        DisabledDefaultUd2VectorField firstMismatchField =
            DisabledDefaultUd2VectorField.None,
        ushort comparedFieldCount = 0,
        ulong expectedValue = 0,
        ulong observedValue = 0)
    {
        return new DisabledDefaultUd2VectorStateResult(
            operation,
            status,
            request,
            firstMismatchField,
            comparedFieldCount,
            expectedValue,
            observedValue);
    }
}

/// <summary>
/// Validates a disabled two-byte UD2 intent and compares caller-supplied
/// synthetic CpuFaultFrame values. This class emits and invokes no bytes,
/// performs no I/O, changes no descriptor state, and triggers no fault.
/// </summary>
public static class DisabledDefaultUd2VectorState
{
    public const byte InvalidOpcodeVector = FaultVectorCatalog.InvalidOpcode;
    public const byte Ud2InstructionByteCount = 2;
    public const ushort CpuFaultFrameFieldCount = 23;
    public const bool EnabledByDefault =
        Probes.EnableInvalidOpcodePanicProbe;
    public const bool PerformsLiveFault = false;
    public const bool ExecutesProbe = false;
    public const bool EmitsExecutableBytes = false;
    public const bool PerformsIo = false;
    public const bool LoadsIdtr = false;
    public const bool InstallsHandler = false;
    public const bool IsGuestEvidence = false;
    public const bool UsesSyntheticObservation = true;

    public static DisabledDefaultUd2VectorStateResult Plan(
        DisabledDefaultUd2VectorStateRequest request)
    {
        if (!request.IsInitialized)
        {
            return CreatePlanFailure(
                DisabledDefaultUd2VectorStateStatus.InvalidRequestState,
                request);
        }

        if (EnabledByDefault || request.EnabledByDefault)
        {
            return CreatePlanFailure(
                DisabledDefaultUd2VectorStateStatus
                    .EnabledByDefaultRejected,
                request);
        }

        if (request.ExecutionRequested)
        {
            return CreatePlanFailure(
                DisabledDefaultUd2VectorStateStatus
                    .ExecutionRequestedRejected,
                request);
        }

        if (request.ExecutionAttempted)
        {
            return CreatePlanFailure(
                DisabledDefaultUd2VectorStateStatus
                    .ExecutionAttemptedRejected,
                request);
        }

        if (!request.OwnedSerialReady)
        {
            return CreatePlanFailure(
                DisabledDefaultUd2VectorStateStatus.OwnedSerialUnavailable,
                request);
        }

        if (!request.OwnedGdtTssReady)
        {
            return CreatePlanFailure(
                DisabledDefaultUd2VectorStateStatus.OwnedGdtTssUnavailable,
                request);
        }

        if (!request.OwnedIdtReady)
        {
            return CreatePlanFailure(
                DisabledDefaultUd2VectorStateStatus.OwnedIdtUnavailable,
                request);
        }

        if (!request.InvalidOpcodeHandlerReady)
        {
            return CreatePlanFailure(
                DisabledDefaultUd2VectorStateStatus
                    .InvalidOpcodeHandlerUnavailable,
                request);
        }

        ulong ud2Address = request.Ud2Address;
        if (ud2Address == 0)
        {
            return CreatePlanFailure(
                DisabledDefaultUd2VectorStateStatus.NullUd2Address,
                request);
        }

        if (!IsCanonicalX64Address(ud2Address))
        {
            return CreatePlanFailure(
                DisabledDefaultUd2VectorStateStatus.NonCanonicalUd2Address,
                request);
        }

        if (ud2Address > ~0UL -
            (Ud2InstructionByteCount - 1UL))
        {
            return CreatePlanFailure(
                DisabledDefaultUd2VectorStateStatus.Ud2AddressSpanOverflow,
                request);
        }

        ulong finalByteAddress = ud2Address +
            (Ud2InstructionByteCount - 1UL);
        if (!IsCanonicalX64Address(finalByteAddress))
        {
            return CreatePlanFailure(
                DisabledDefaultUd2VectorStateStatus
                    .NonCanonicalUd2AddressSpan,
                request);
        }

        return DisabledDefaultUd2VectorStateResult.Create(
            DisabledDefaultUd2VectorStateOperation.Plan,
            DisabledDefaultUd2VectorStateStatus.ReadyDisabled,
            request);
    }

    public static DisabledDefaultUd2VectorStateResult
        AuditSyntheticObservation(
            DisabledDefaultUd2VectorStateRequest request,
            in CpuFaultFrame expected,
            in CpuFaultFrame observed)
    {
        DisabledDefaultUd2VectorStateResult plan = Plan(request);
        if (!plan.ReadyDisabled)
        {
            return DisabledDefaultUd2VectorStateResult.Create(
                DisabledDefaultUd2VectorStateOperation
                    .AuditSyntheticObservation,
                plan.Status,
                request);
        }

        if (expected.Vector != InvalidOpcodeVector)
        {
            return InvalidExpected(
                request,
                DisabledDefaultUd2VectorField.Vector,
                InvalidOpcodeVector,
                (ulong)expected.Vector);
        }

        if (expected.HasErrorCode)
        {
            return InvalidExpected(
                request,
                DisabledDefaultUd2VectorField.HasErrorCode,
                expectedValue: 0,
                observedValue: 1);
        }

        if (expected.ErrorCode != 0)
        {
            return InvalidExpected(
                request,
                DisabledDefaultUd2VectorField.ErrorCode,
                expectedValue: 0,
                expected.ErrorCode);
        }

        if (expected.Rip != request.Ud2Address)
        {
            return InvalidExpected(
                request,
                DisabledDefaultUd2VectorField.Rip,
                request.Ud2Address,
                expected.Rip);
        }

        DisabledDefaultUd2VectorStateResult mismatch;
        if (TryMismatch(
                request,
                DisabledDefaultUd2VectorField.Vector,
                1,
                (ulong)expected.Vector,
                (ulong)observed.Vector,
                DisabledDefaultUd2VectorStateStatus.VectorMismatch,
                out mismatch) ||
            TryMismatch(
                request,
                DisabledDefaultUd2VectorField.HasErrorCode,
                2,
                expected.HasErrorCode ? 1UL : 0UL,
                observed.HasErrorCode ? 1UL : 0UL,
                DisabledDefaultUd2VectorStateStatus
                    .ErrorCodeMetadataMismatch,
                out mismatch) ||
            TryMismatch(
                request,
                DisabledDefaultUd2VectorField.ErrorCode,
                3,
                expected.ErrorCode,
                observed.ErrorCode,
                DisabledDefaultUd2VectorStateStatus.ErrorCodeMismatch,
                out mismatch) ||
            TryRegisterMismatch(request, DisabledDefaultUd2VectorField.Rip,
                4, expected.Rip, observed.Rip, out mismatch) ||
            TryRegisterMismatch(request, DisabledDefaultUd2VectorField.Rsp,
                5, expected.Rsp, observed.Rsp, out mismatch) ||
            TryRegisterMismatch(request, DisabledDefaultUd2VectorField.Rbp,
                6, expected.Rbp, observed.Rbp, out mismatch) ||
            TryRegisterMismatch(request, DisabledDefaultUd2VectorField.Rflags,
                7, expected.Rflags, observed.Rflags, out mismatch) ||
            TryRegisterMismatch(request, DisabledDefaultUd2VectorField.Cr2,
                8, expected.Cr2, observed.Cr2, out mismatch) ||
            TryRegisterMismatch(request, DisabledDefaultUd2VectorField.Cr3,
                9, expected.Cr3, observed.Cr3, out mismatch) ||
            TryRegisterMismatch(request, DisabledDefaultUd2VectorField.Rax,
                10, expected.Rax, observed.Rax, out mismatch) ||
            TryRegisterMismatch(request, DisabledDefaultUd2VectorField.Rbx,
                11, expected.Rbx, observed.Rbx, out mismatch) ||
            TryRegisterMismatch(request, DisabledDefaultUd2VectorField.Rcx,
                12, expected.Rcx, observed.Rcx, out mismatch) ||
            TryRegisterMismatch(request, DisabledDefaultUd2VectorField.Rdx,
                13, expected.Rdx, observed.Rdx, out mismatch) ||
            TryRegisterMismatch(request, DisabledDefaultUd2VectorField.Rsi,
                14, expected.Rsi, observed.Rsi, out mismatch) ||
            TryRegisterMismatch(request, DisabledDefaultUd2VectorField.Rdi,
                15, expected.Rdi, observed.Rdi, out mismatch) ||
            TryRegisterMismatch(request, DisabledDefaultUd2VectorField.R8,
                16, expected.R8, observed.R8, out mismatch) ||
            TryRegisterMismatch(request, DisabledDefaultUd2VectorField.R9,
                17, expected.R9, observed.R9, out mismatch) ||
            TryRegisterMismatch(request, DisabledDefaultUd2VectorField.R10,
                18, expected.R10, observed.R10, out mismatch) ||
            TryRegisterMismatch(request, DisabledDefaultUd2VectorField.R11,
                19, expected.R11, observed.R11, out mismatch) ||
            TryRegisterMismatch(request, DisabledDefaultUd2VectorField.R12,
                20, expected.R12, observed.R12, out mismatch) ||
            TryRegisterMismatch(request, DisabledDefaultUd2VectorField.R13,
                21, expected.R13, observed.R13, out mismatch) ||
            TryRegisterMismatch(request, DisabledDefaultUd2VectorField.R14,
                22, expected.R14, observed.R14, out mismatch) ||
            TryRegisterMismatch(request, DisabledDefaultUd2VectorField.R15,
                23, expected.R15, observed.R15, out mismatch))
        {
            return mismatch;
        }

        return DisabledDefaultUd2VectorStateResult.Create(
            DisabledDefaultUd2VectorStateOperation
                .AuditSyntheticObservation,
            DisabledDefaultUd2VectorStateStatus.ObservationAccepted,
            request,
            comparedFieldCount: CpuFaultFrameFieldCount);
    }

    private static DisabledDefaultUd2VectorStateResult CreatePlanFailure(
        DisabledDefaultUd2VectorStateStatus status,
        DisabledDefaultUd2VectorStateRequest request)
    {
        return DisabledDefaultUd2VectorStateResult.Create(
            DisabledDefaultUd2VectorStateOperation.Plan,
            status,
            request);
    }

    private static DisabledDefaultUd2VectorStateResult InvalidExpected(
        DisabledDefaultUd2VectorStateRequest request,
        DisabledDefaultUd2VectorField field,
        ulong expectedValue,
        ulong observedValue)
    {
        return DisabledDefaultUd2VectorStateResult.Create(
            DisabledDefaultUd2VectorStateOperation
                .AuditSyntheticObservation,
            DisabledDefaultUd2VectorStateStatus.InvalidExpectedState,
            request,
            field,
            comparedFieldCount: 0,
            expectedValue,
            observedValue);
    }

    private static bool TryRegisterMismatch(
        DisabledDefaultUd2VectorStateRequest request,
        DisabledDefaultUd2VectorField field,
        ushort comparedFieldCount,
        ulong expectedValue,
        ulong observedValue,
        out DisabledDefaultUd2VectorStateResult result)
    {
        return TryMismatch(
            request,
            field,
            comparedFieldCount,
            expectedValue,
            observedValue,
            DisabledDefaultUd2VectorStateStatus.RegisterStateMismatch,
            out result);
    }

    private static bool TryMismatch(
        DisabledDefaultUd2VectorStateRequest request,
        DisabledDefaultUd2VectorField field,
        ushort comparedFieldCount,
        ulong expectedValue,
        ulong observedValue,
        DisabledDefaultUd2VectorStateStatus status,
        out DisabledDefaultUd2VectorStateResult result)
    {
        if (expectedValue == observedValue)
        {
            result = default;
            return false;
        }

        result = DisabledDefaultUd2VectorStateResult.Create(
            DisabledDefaultUd2VectorStateOperation
                .AuditSyntheticObservation,
            status,
            request,
            field,
            comparedFieldCount,
            expectedValue,
            observedValue);
        return true;
    }

    private static bool IsCanonicalX64Address(ulong address)
    {
        ulong high = address >> 48;
        bool sign = (address & 0x0000_8000_0000_0000UL) != 0;
        return sign ? high == 0xffffUL : high == 0;
    }

    internal static bool IsReadyDisabledRequest(
        DisabledDefaultUd2VectorStateRequest request)
    {
        if (!request.IsInitialized ||
            EnabledByDefault ||
            request.EnabledByDefault ||
            request.ExecutionRequested ||
            request.ExecutionAttempted ||
            !request.OwnedSerialReady ||
            !request.OwnedGdtTssReady ||
            !request.OwnedIdtReady ||
            !request.InvalidOpcodeHandlerReady ||
            request.Ud2Address == 0 ||
            !IsCanonicalX64Address(request.Ud2Address) ||
            request.Ud2Address > ~0UL -
                (Ud2InstructionByteCount - 1UL))
        {
            return false;
        }

        return IsCanonicalX64Address(
            request.Ud2Address + (Ud2InstructionByteCount - 1UL));
    }
}
