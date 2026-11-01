namespace OS.Kernel.Diagnostics;
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

    public static SeparatePageFaultResult AuditSyntheticObservation(SeparatePageFaultRequest request, CpuFaultFrame observed)
    {
        SeparatePageFaultStatus status = ValidateRequest(request);
        if (status != SeparatePageFaultStatus.ReadyDisabled)
            return new SeparatePageFaultResult(status, request);
        if (observed.Vector != ExpectedVector)
            return Mismatch(request, SeparatePageFaultStatus.VectorMismatch, SeparatePageFaultField.Vector, ExpectedVector, unchecked((ulong)(long)observed.Vector));
        if (!observed.HasErrorCode)
            return Mismatch(request, SeparatePageFaultStatus.ErrorCodePresenceMismatch, SeparatePageFaultField.HasErrorCode, 1, 0);
        // A hardware error code is present even when its numeric value is zero.
        // Compare every bit: a write/user/protection/fetch/extended/reserved bit
        // cannot be hidden by M01-015's descriptive low-five-bit mask.
        if (observed.ErrorCode != ExpectedErrorCode)
            return Mismatch(request, SeparatePageFaultStatus.ErrorCodeMismatch, SeparatePageFaultField.ErrorCode, ExpectedErrorCode, observed.ErrorCode);
        if (observed.Cr2 != ExpectedCr2)
            return Mismatch(request, SeparatePageFaultStatus.Cr2Mismatch, SeparatePageFaultField.Cr2, ExpectedCr2, observed.Cr2);
        if (observed.Rip != request.FaultInstructionAddress)
            return Mismatch(request, SeparatePageFaultStatus.InstructionAddressMismatch, SeparatePageFaultField.Rip, request.FaultInstructionAddress, observed.Rip);
        return new SeparatePageFaultResult(SeparatePageFaultStatus.SyntheticObservationAccepted, request, comparedFieldCount: AuditFieldCount);
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

    private static SeparatePageFaultResult Mismatch(SeparatePageFaultRequest request, SeparatePageFaultStatus status, SeparatePageFaultField field, ulong expected, ulong observed)
    {
        return new SeparatePageFaultResult(status, request, field, (byte)field, expected, observed);
    }
}
