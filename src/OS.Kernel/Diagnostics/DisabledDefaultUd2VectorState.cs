namespace OS.Kernel.Diagnostics;
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
    public const bool EnabledByDefault = Probes.EnableInvalidOpcodePanicProbe;
    public const bool PerformsLiveFault = false;
    public const bool ExecutesProbe = false;
    public const bool EmitsExecutableBytes = false;
    public const bool PerformsIo = false;
    public const bool LoadsIdtr = false;
    public const bool InstallsHandler = false;
    public const bool IsGuestEvidence = false;
    public const bool UsesSyntheticObservation = true;
    public static DisabledDefaultUd2VectorStateResult Plan(DisabledDefaultUd2VectorStateRequest request)
    {
        if (!request.IsInitialized)
        {
            return CreatePlanFailure(DisabledDefaultUd2VectorStateStatus.InvalidRequestState, request);
        }

        if (EnabledByDefault || request.EnabledByDefault)
        {
            return CreatePlanFailure(DisabledDefaultUd2VectorStateStatus.EnabledByDefaultRejected, request);
        }

        if (request.ExecutionRequested)
        {
            return CreatePlanFailure(DisabledDefaultUd2VectorStateStatus.ExecutionRequestedRejected, request);
        }

        if (request.ExecutionAttempted)
        {
            return CreatePlanFailure(DisabledDefaultUd2VectorStateStatus.ExecutionAttemptedRejected, request);
        }

        if (!request.OwnedSerialReady)
        {
            return CreatePlanFailure(DisabledDefaultUd2VectorStateStatus.OwnedSerialUnavailable, request);
        }

        if (!request.OwnedGdtTssReady)
        {
            return CreatePlanFailure(DisabledDefaultUd2VectorStateStatus.OwnedGdtTssUnavailable, request);
        }

        if (!request.OwnedIdtReady)
        {
            return CreatePlanFailure(DisabledDefaultUd2VectorStateStatus.OwnedIdtUnavailable, request);
        }

        if (!request.InvalidOpcodeHandlerReady)
        {
            return CreatePlanFailure(DisabledDefaultUd2VectorStateStatus.InvalidOpcodeHandlerUnavailable, request);
        }

        ulong ud2Address = request.Ud2Address;
        if (ud2Address == 0)
        {
            return CreatePlanFailure(DisabledDefaultUd2VectorStateStatus.NullUd2Address, request);
        }

        if (!IsCanonicalX64Address(ud2Address))
        {
            return CreatePlanFailure(DisabledDefaultUd2VectorStateStatus.NonCanonicalUd2Address, request);
        }

        if (ud2Address > ~0UL - (Ud2InstructionByteCount - 1UL))
        {
            return CreatePlanFailure(DisabledDefaultUd2VectorStateStatus.Ud2AddressSpanOverflow, request);
        }

        ulong finalByteAddress = ud2Address + (Ud2InstructionByteCount - 1UL);
        if (!IsCanonicalX64Address(finalByteAddress))
        {
            return CreatePlanFailure(DisabledDefaultUd2VectorStateStatus.NonCanonicalUd2AddressSpan, request);
        }

        return DisabledDefaultUd2VectorStateResult.Create(DisabledDefaultUd2VectorStateOperation.Plan, DisabledDefaultUd2VectorStateStatus.ReadyDisabled, request);
    }

    public static DisabledDefaultUd2VectorStateResult AuditSyntheticObservation(DisabledDefaultUd2VectorStateRequest request, in CpuFaultFrame expected, in CpuFaultFrame observed)
    {
        DisabledDefaultUd2VectorStateResult plan = Plan(request);
        if (!plan.ReadyDisabled)
        {
            return DisabledDefaultUd2VectorStateResult.Create(DisabledDefaultUd2VectorStateOperation.AuditSyntheticObservation, plan.Status, request);
        }

        if (expected.Vector != InvalidOpcodeVector)
        {
            return InvalidExpected(request, DisabledDefaultUd2VectorField.Vector, InvalidOpcodeVector, (ulong)expected.Vector);
        }

        if (expected.HasErrorCode)
        {
            return InvalidExpected(request, DisabledDefaultUd2VectorField.HasErrorCode, expectedValue: 0, observedValue: 1);
        }

        if (expected.ErrorCode != 0)
        {
            return InvalidExpected(request, DisabledDefaultUd2VectorField.ErrorCode, expectedValue: 0, expected.ErrorCode);
        }

        if (expected.Rip != request.Ud2Address)
        {
            return InvalidExpected(request, DisabledDefaultUd2VectorField.Rip, request.Ud2Address, expected.Rip);
        }

        DisabledDefaultUd2VectorStateResult mismatch;
        if (TryMismatch(request, DisabledDefaultUd2VectorField.Vector, 1, (ulong)expected.Vector, (ulong)observed.Vector, DisabledDefaultUd2VectorStateStatus.VectorMismatch, out mismatch) || TryMismatch(request, DisabledDefaultUd2VectorField.HasErrorCode, 2, expected.HasErrorCode ? 1UL : 0UL, observed.HasErrorCode ? 1UL : 0UL, DisabledDefaultUd2VectorStateStatus.ErrorCodeMetadataMismatch, out mismatch) || TryMismatch(request, DisabledDefaultUd2VectorField.ErrorCode, 3, expected.ErrorCode, observed.ErrorCode, DisabledDefaultUd2VectorStateStatus.ErrorCodeMismatch, out mismatch) || TryRegisterMismatch(request, DisabledDefaultUd2VectorField.Rip, 4, expected.Rip, observed.Rip, out mismatch) || TryRegisterMismatch(request, DisabledDefaultUd2VectorField.Rsp, 5, expected.Rsp, observed.Rsp, out mismatch) || TryRegisterMismatch(request, DisabledDefaultUd2VectorField.Rbp, 6, expected.Rbp, observed.Rbp, out mismatch) || TryRegisterMismatch(request, DisabledDefaultUd2VectorField.Rflags, 7, expected.Rflags, observed.Rflags, out mismatch) || TryRegisterMismatch(request, DisabledDefaultUd2VectorField.Cr2, 8, expected.Cr2, observed.Cr2, out mismatch) || TryRegisterMismatch(request, DisabledDefaultUd2VectorField.Cr3, 9, expected.Cr3, observed.Cr3, out mismatch) || TryRegisterMismatch(request, DisabledDefaultUd2VectorField.Rax, 10, expected.Rax, observed.Rax, out mismatch) || TryRegisterMismatch(request, DisabledDefaultUd2VectorField.Rbx, 11, expected.Rbx, observed.Rbx, out mismatch) || TryRegisterMismatch(request, DisabledDefaultUd2VectorField.Rcx, 12, expected.Rcx, observed.Rcx, out mismatch) || TryRegisterMismatch(request, DisabledDefaultUd2VectorField.Rdx, 13, expected.Rdx, observed.Rdx, out mismatch) || TryRegisterMismatch(request, DisabledDefaultUd2VectorField.Rsi, 14, expected.Rsi, observed.Rsi, out mismatch) || TryRegisterMismatch(request, DisabledDefaultUd2VectorField.Rdi, 15, expected.Rdi, observed.Rdi, out mismatch) || TryRegisterMismatch(request, DisabledDefaultUd2VectorField.R8, 16, expected.R8, observed.R8, out mismatch) || TryRegisterMismatch(request, DisabledDefaultUd2VectorField.R9, 17, expected.R9, observed.R9, out mismatch) || TryRegisterMismatch(request, DisabledDefaultUd2VectorField.R10, 18, expected.R10, observed.R10, out mismatch) || TryRegisterMismatch(request, DisabledDefaultUd2VectorField.R11, 19, expected.R11, observed.R11, out mismatch) || TryRegisterMismatch(request, DisabledDefaultUd2VectorField.R12, 20, expected.R12, observed.R12, out mismatch) || TryRegisterMismatch(request, DisabledDefaultUd2VectorField.R13, 21, expected.R13, observed.R13, out mismatch) || TryRegisterMismatch(request, DisabledDefaultUd2VectorField.R14, 22, expected.R14, observed.R14, out mismatch) || TryRegisterMismatch(request, DisabledDefaultUd2VectorField.R15, 23, expected.R15, observed.R15, out mismatch))
        {
            return mismatch;
        }

        return DisabledDefaultUd2VectorStateResult.Create(DisabledDefaultUd2VectorStateOperation.AuditSyntheticObservation, DisabledDefaultUd2VectorStateStatus.ObservationAccepted, request, comparedFieldCount: CpuFaultFrameFieldCount);
    }

    private static DisabledDefaultUd2VectorStateResult CreatePlanFailure(DisabledDefaultUd2VectorStateStatus status, DisabledDefaultUd2VectorStateRequest request)
    {
        return DisabledDefaultUd2VectorStateResult.Create(DisabledDefaultUd2VectorStateOperation.Plan, status, request);
    }

    private static DisabledDefaultUd2VectorStateResult InvalidExpected(DisabledDefaultUd2VectorStateRequest request, DisabledDefaultUd2VectorField field, ulong expectedValue, ulong observedValue)
    {
        return DisabledDefaultUd2VectorStateResult.Create(DisabledDefaultUd2VectorStateOperation.AuditSyntheticObservation, DisabledDefaultUd2VectorStateStatus.InvalidExpectedState, request, field, comparedFieldCount: 0, expectedValue, observedValue);
    }

    private static bool TryRegisterMismatch(DisabledDefaultUd2VectorStateRequest request, DisabledDefaultUd2VectorField field, ushort comparedFieldCount, ulong expectedValue, ulong observedValue, out DisabledDefaultUd2VectorStateResult result)
    {
        return TryMismatch(request, field, comparedFieldCount, expectedValue, observedValue, DisabledDefaultUd2VectorStateStatus.RegisterStateMismatch, out result);
    }

    private static bool TryMismatch(DisabledDefaultUd2VectorStateRequest request, DisabledDefaultUd2VectorField field, ushort comparedFieldCount, ulong expectedValue, ulong observedValue, DisabledDefaultUd2VectorStateStatus status, out DisabledDefaultUd2VectorStateResult result)
    {
        if (expectedValue == observedValue)
        {
            result = default;
            return false;
        }

        result = DisabledDefaultUd2VectorStateResult.Create(DisabledDefaultUd2VectorStateOperation.AuditSyntheticObservation, status, request, field, comparedFieldCount, expectedValue, observedValue);
        return true;
    }

    private static bool IsCanonicalX64Address(ulong address)
    {
        ulong high = address >> 48;
        bool sign = (address & 0x0000_8000_0000_0000UL) != 0;
        return sign ? high == 0xffffUL : high == 0;
    }

    internal static bool IsReadyDisabledRequest(DisabledDefaultUd2VectorStateRequest request)
    {
        if (!request.IsInitialized || EnabledByDefault || request.EnabledByDefault || request.ExecutionRequested || request.ExecutionAttempted || !request.OwnedSerialReady || !request.OwnedGdtTssReady || !request.OwnedIdtReady || !request.InvalidOpcodeHandlerReady || request.Ud2Address == 0 || !IsCanonicalX64Address(request.Ud2Address) || request.Ud2Address > ~0UL - (Ud2InstructionByteCount - 1UL))
        {
            return false;
        }

        return IsCanonicalX64Address(request.Ud2Address + (Ud2InstructionByteCount - 1UL));
    }
}
