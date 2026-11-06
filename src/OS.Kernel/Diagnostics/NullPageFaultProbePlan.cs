namespace OS.Kernel.Diagnostics;
/// <summary>
/// Produces host-testable metadata for a disabled future #PF probe. It emits
/// no bytes, performs no access, and cannot invoke a probe.
/// </summary>

public static class NullPageFaultProbePlan
{
    public const ushort ExpectedPageFaultVector = 14;
    public const uint ExpectedErrorCode = 0;
    public const uint CheckedErrorCodeMask = 0x1f;
    public const ulong ExpectedCr2Raw = 0;
    public const bool EnabledByDefault = false;
    public const bool PerformsLiveFault = false;
    public const bool DereferencesPageZero = false;
    public const bool EmitsExecutableBytes = false;
    public const bool ExecutesProbe = false;
    public const bool IsGuestEvidence = false;
    public const bool ExecutionDeferredToM02014 = true;
    public static NullPageFaultProbePlanResult Plan(NullPageFaultProbeRequest request)
    {
        if (!request.IsInitialized)
        {
            return NullPageFaultProbePlanResult.Create(NullPageFaultProbePlanStatus.InvalidRequestState, request);
        }

        if (!request.PageZeroPolicy.ProvesPageZeroAbsentInSuppliedImage)
        {
            return NullPageFaultProbePlanResult.Create(NullPageFaultProbePlanStatus.PageZeroPolicyUnavailable, request);
        }

        if (request.EnabledByDefault)
        {
            return NullPageFaultProbePlanResult.Create(NullPageFaultProbePlanStatus.EnabledByDefaultRejected, request);
        }

        if (request.ExecutionAttempted)
        {
            return NullPageFaultProbePlanResult.Create(NullPageFaultProbePlanStatus.ExecutionAttemptedRejected, request);
        }

        if (request.ExecutionRequested)
        {
            return NullPageFaultProbePlanResult.Create(NullPageFaultProbePlanStatus.ExecutionRequestedRejected, request);
        }

        if (!request.OwnedDiagnosticsReady)
        {
            return NullPageFaultProbePlanResult.Create(NullPageFaultProbePlanStatus.OwnedDiagnosticsUnavailable, request);
        }

        if (!request.PageFaultHandlerReady)
        {
            return NullPageFaultProbePlanResult.Create(NullPageFaultProbePlanStatus.PageFaultHandlerUnavailable, request);
        }

        return NullPageFaultProbePlanResult.Create(NullPageFaultProbePlanStatus.ReadyDisabled, request);
    }
}
