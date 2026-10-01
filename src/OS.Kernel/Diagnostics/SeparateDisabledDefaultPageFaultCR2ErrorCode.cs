namespace OS.Kernel.Diagnostics;

public enum SeparatePageFaultStatus : byte
{
    Uninitialized = 0,
    ReadyDisabled = 1,
    SyntheticObservationAccepted = 2,
    InvalidRequest = 3,
    EnabledByDefaultRejected = 4,
    ExecutionRequestedRejected = 5,
    ExecutionAttemptedRejected = 6,
    NullReadPlanUnavailable = 7,
    OwnedSerialUnavailable = 8,
    OwnedGdtTssUnavailable = 9,
    OwnedIdtUnavailable = 10,
    PageFaultHandlerUnavailable = 11,
    InstructionAddressInPageZero = 12,
    NonCanonicalInstructionAddress = 13,
    VectorMismatch = 14,
    ErrorCodePresenceMismatch = 15,
    ErrorCodeMismatch = 16,
    Cr2Mismatch = 17,
    InstructionAddressMismatch = 18
}

public enum SeparatePageFaultField : byte
{
    None = 0,
    Vector = 1,
    HasErrorCode = 2,
    ErrorCode = 3,
    Cr2 = 4,
    Rip = 5
}

/// <summary>
/// Pointer-free host intent. The M01-015 plan describes an audited supplied
/// image; readiness flags are caller attestations, not loaded processor state.
/// This is an internal value contract, not a serialized or hardware ABI.
/// </summary>
public readonly struct SeparatePageFaultRequest
{
    private readonly byte _initialized;
    private readonly byte _executionFlags;
    private readonly byte _readinessFlags;

    private SeparatePageFaultRequest(
        NullPageFaultProbePlanResult nullReadPlan,
        ulong faultInstructionAddress,
        bool enabledByDefault,
        bool executionRequested,
        bool executionAttempted,
        bool ownedSerialReady,
        bool ownedGdtTssReady,
        bool ownedIdtReady,
        bool pageFaultHandlerReady)
    {
        _initialized = 1;
        _executionFlags = (byte)((enabledByDefault ? 1 : 0) |
            (executionRequested ? 2 : 0) | (executionAttempted ? 4 : 0));
        _readinessFlags = (byte)((ownedSerialReady ? 1 : 0) |
            (ownedGdtTssReady ? 2 : 0) | (ownedIdtReady ? 4 : 0) |
            (pageFaultHandlerReady ? 8 : 0));
        NullReadPlan = nullReadPlan;
        FaultInstructionAddress = faultInstructionAddress;
    }

    public bool IsInitialized => _initialized == 1 &&
        (_executionFlags & ~7) == 0 && (_readinessFlags & ~15) == 0;
    public bool EnabledByDefault => (_executionFlags & 1) != 0;
    public bool ExecutionRequested => (_executionFlags & 2) != 0;
    public bool ExecutionAttempted => (_executionFlags & 4) != 0;
    public bool OwnedSerialReady => (_readinessFlags & 1) != 0;
    public bool OwnedGdtTssReady => (_readinessFlags & 2) != 0;
    public bool OwnedIdtReady => (_readinessFlags & 4) != 0;
    public bool PageFaultHandlerReady => (_readinessFlags & 8) != 0;
    public NullPageFaultProbePlanResult NullReadPlan { get; }
    public ulong FaultInstructionAddress { get; }

    public static SeparatePageFaultRequest CreateDisabled(
        NullPageFaultProbePlanResult nullReadPlan,
        ulong faultInstructionAddress,
        bool callerAttestsOwnedSerialReady,
        bool callerAttestsOwnedGdtTssReady,
        bool callerAttestsOwnedIdtReady,
        bool callerAttestsPageFaultHandlerReady)
    {
        return CreateForValidation(nullReadPlan, faultInstructionAddress,
            SeparateDisabledDefaultPageFaultCR2ErrorCode.EnabledByDefault,
            executionRequested: false, executionAttempted: false,
            callerAttestsOwnedSerialReady, callerAttestsOwnedGdtTssReady,
            callerAttestsOwnedIdtReady, callerAttestsPageFaultHandlerReady);
    }

    public static SeparatePageFaultRequest CreateForValidation(
        NullPageFaultProbePlanResult nullReadPlan,
        ulong faultInstructionAddress,
        bool enabledByDefault,
        bool executionRequested,
        bool executionAttempted,
        bool callerAttestsOwnedSerialReady,
        bool callerAttestsOwnedGdtTssReady,
        bool callerAttestsOwnedIdtReady,
        bool callerAttestsPageFaultHandlerReady)
    {
        return new SeparatePageFaultRequest(nullReadPlan,
            faultInstructionAddress, enabledByDefault, executionRequested,
            executionAttempted, callerAttestsOwnedSerialReady,
            callerAttestsOwnedGdtTssReady, callerAttestsOwnedIdtReady,
            callerAttestsPageFaultHandlerReady);
    }
}

/// <summary>
/// A disabled plan or a comparison of supplied values. Even an accepted
/// comparison cannot attest that CR2, RIP, or any frame came from hardware.
/// </summary>
public readonly struct SeparatePageFaultResult
{
    private readonly byte _initialized;

    internal SeparatePageFaultResult(
        SeparatePageFaultStatus status,
        SeparatePageFaultRequest request,
        SeparatePageFaultField firstMismatchField = SeparatePageFaultField.None,
        byte comparedFieldCount = 0,
        ulong expectedValue = 0,
        ulong observedValue = 0)
    {
        _initialized = 1;
        Status = status;
        Request = request;
        FirstMismatchField = firstMismatchField;
        ComparedFieldCount = comparedFieldCount;
        ExpectedValue = expectedValue;
        ObservedValue = observedValue;
    }

    public bool IsInitialized => _initialized == 1 &&
        Status > SeparatePageFaultStatus.Uninitialized &&
        Status <= SeparatePageFaultStatus.InstructionAddressMismatch &&
        FirstMismatchField <= SeparatePageFaultField.Rip &&
        ComparedFieldCount <= SeparateDisabledDefaultPageFaultCR2ErrorCode.AuditFieldCount;
    public SeparatePageFaultStatus Status { get; }
    public SeparatePageFaultRequest Request { get; }
    public SeparatePageFaultField FirstMismatchField { get; }
    public byte ComparedFieldCount { get; }
    public ulong ExpectedValue { get; }
    public ulong ObservedValue { get; }

    public bool ReadyDisabled => IsConsistentSuccess &&
        Status == SeparatePageFaultStatus.ReadyDisabled && ComparedFieldCount == 0;
    public bool SyntheticObservationAccepted => IsConsistentSuccess &&
        Status == SeparatePageFaultStatus.SyntheticObservationAccepted &&
        ComparedFieldCount == SeparateDisabledDefaultPageFaultCR2ErrorCode.AuditFieldCount;
    public bool Succeeded => ReadyDisabled || SyntheticObservationAccepted;
    public bool IsGuestEvidence => false;
    public bool CapturedHardwareState => false;
    public bool FaultTriggered => false;

    private bool IsConsistentSuccess => IsInitialized &&
        FirstMismatchField == SeparatePageFaultField.None &&
        ExpectedValue == 0 && ObservedValue == 0 &&
        SeparateDisabledDefaultPageFaultCR2ErrorCode.ValidateRequest(Request) ==
            SeparatePageFaultStatus.ReadyDisabled;
}

/// <summary>
/// M02-014 host leaf for the separate M01-015 supervisor null READ intent.
/// No address is dereferenced, no instruction is emitted or executed, and
/// no hardware state is captured. Allocation, mapping lifetime, descriptor
/// installation, instruction bytes and real fault delivery belong to a later
/// integration. The legacy null-write byte plan is deliberately not used.
/// </summary>
public static class SeparateDisabledDefaultPageFaultCR2ErrorCode
{
    public const bool EnabledByDefault = Probes.EnableNullPointerPanicProbe;
    public const ushort ExpectedVector = NullPageFaultProbePlan.ExpectedPageFaultVector;
    public const ulong ExpectedErrorCode = NullPageFaultProbePlan.ExpectedErrorCode;
    public const ulong ExpectedCr2 = NullPageFaultProbePlan.ExpectedCr2Raw;
    public const byte AuditFieldCount = 5;

    public static SeparatePageFaultResult Plan(SeparatePageFaultRequest request)
    {
        return new SeparatePageFaultResult(ValidateRequest(request), request);
    }

    public static SeparatePageFaultResult AuditSyntheticObservation(
        SeparatePageFaultRequest request, CpuFaultFrame observed)
    {
        SeparatePageFaultStatus status = ValidateRequest(request);
        if (status != SeparatePageFaultStatus.ReadyDisabled)
            return new SeparatePageFaultResult(status, request);

        if (observed.Vector != ExpectedVector)
            return Mismatch(request, SeparatePageFaultStatus.VectorMismatch,
                SeparatePageFaultField.Vector, ExpectedVector,
                unchecked((ulong)(long)observed.Vector));
        if (!observed.HasErrorCode)
            return Mismatch(request, SeparatePageFaultStatus.ErrorCodePresenceMismatch,
                SeparatePageFaultField.HasErrorCode, 1, 0);

        // A hardware error code is present even when its numeric value is zero.
        // Compare every bit: a write/user/protection/fetch/extended/reserved bit
        // cannot be hidden by M01-015's descriptive low-five-bit mask.
        if (observed.ErrorCode != ExpectedErrorCode)
            return Mismatch(request, SeparatePageFaultStatus.ErrorCodeMismatch,
                SeparatePageFaultField.ErrorCode, ExpectedErrorCode, observed.ErrorCode);
        if (observed.Cr2 != ExpectedCr2)
            return Mismatch(request, SeparatePageFaultStatus.Cr2Mismatch,
                SeparatePageFaultField.Cr2, ExpectedCr2, observed.Cr2);
        if (observed.Rip != request.FaultInstructionAddress)
            return Mismatch(request, SeparatePageFaultStatus.InstructionAddressMismatch,
                SeparatePageFaultField.Rip, request.FaultInstructionAddress, observed.Rip);

        return new SeparatePageFaultResult(
            SeparatePageFaultStatus.SyntheticObservationAccepted, request,
            comparedFieldCount: AuditFieldCount);
    }

    internal static SeparatePageFaultStatus ValidateRequest(SeparatePageFaultRequest request)
    {
        if (!request.IsInitialized)
            return SeparatePageFaultStatus.InvalidRequest;
        if (EnabledByDefault || request.EnabledByDefault)
            return SeparatePageFaultStatus.EnabledByDefaultRejected;
        if (request.ExecutionRequested)
            return SeparatePageFaultStatus.ExecutionRequestedRejected;
        if (request.ExecutionAttempted)
            return SeparatePageFaultStatus.ExecutionAttemptedRejected;
        if (!request.NullReadPlan.Succeeded)
            return SeparatePageFaultStatus.NullReadPlanUnavailable;
        if (!request.OwnedSerialReady)
            return SeparatePageFaultStatus.OwnedSerialUnavailable;
        if (!request.OwnedGdtTssReady)
            return SeparatePageFaultStatus.OwnedGdtTssUnavailable;
        if (!request.OwnedIdtReady)
            return SeparatePageFaultStatus.OwnedIdtUnavailable;
        if (!request.PageFaultHandlerReady)
            return SeparatePageFaultStatus.PageFaultHandlerUnavailable;

        // Only a modeled instruction address is checked here. x64 instructions
        // need no alignment. Their complete byte span and executable mapping
        // are not represented by this leaf and remain integration prerequisites.
        ulong rip = request.FaultInstructionAddress;
        if (rip < OS.Kernel.Memory.PageZeroMappingPolicy.PageSize)
            return SeparatePageFaultStatus.InstructionAddressInPageZero;
        if (rip >= 0x0000_8000_0000_0000UL && rip < 0xffff_8000_0000_0000UL)
            return SeparatePageFaultStatus.NonCanonicalInstructionAddress;
        return SeparatePageFaultStatus.ReadyDisabled;
    }

    private static SeparatePageFaultResult Mismatch(
        SeparatePageFaultRequest request, SeparatePageFaultStatus status,
        SeparatePageFaultField field, ulong expected, ulong observed)
    {
        return new SeparatePageFaultResult(status, request, field,
            (byte)field, expected, observed);
    }
}
