namespace OS.Kernel.Diagnostics;
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
    private SeparatePageFaultRequest(NullPageFaultProbePlanResult nullReadPlan, ulong faultInstructionAddress, bool enabledByDefault, bool executionRequested, bool executionAttempted, bool ownedSerialReady, bool ownedGdtTssReady, bool ownedIdtReady, bool pageFaultHandlerReady)
    {
        _initialized = 1;
        _executionFlags = (byte)((enabledByDefault ? 1 : 0) | (executionRequested ? 2 : 0) | (executionAttempted ? 4 : 0));
        _readinessFlags = (byte)((ownedSerialReady ? 1 : 0) | (ownedGdtTssReady ? 2 : 0) | (ownedIdtReady ? 4 : 0) | (pageFaultHandlerReady ? 8 : 0));
        NullReadPlan = nullReadPlan;
        FaultInstructionAddress = faultInstructionAddress;
    }

    public bool IsInitialized => _initialized == 1 && (_executionFlags & ~7) == 0 && (_readinessFlags & ~15) == 0;
    public bool EnabledByDefault => (_executionFlags & 1) != 0;
    public bool ExecutionRequested => (_executionFlags & 2) != 0;
    public bool ExecutionAttempted => (_executionFlags & 4) != 0;
    public bool OwnedSerialReady => (_readinessFlags & 1) != 0;
    public bool OwnedGdtTssReady => (_readinessFlags & 2) != 0;
    public bool OwnedIdtReady => (_readinessFlags & 4) != 0;
    public bool PageFaultHandlerReady => (_readinessFlags & 8) != 0;
    public NullPageFaultProbePlanResult NullReadPlan { get; }
    public ulong FaultInstructionAddress { get; }

    public static SeparatePageFaultRequest CreateDisabled(NullPageFaultProbePlanResult nullReadPlan, ulong faultInstructionAddress, bool callerAttestsOwnedSerialReady, bool callerAttestsOwnedGdtTssReady, bool callerAttestsOwnedIdtReady, bool callerAttestsPageFaultHandlerReady)
    {
        return CreateForValidation(nullReadPlan, faultInstructionAddress, SeparateDisabledDefaultPageFaultCR2ErrorCode.EnabledByDefault, executionRequested: false, executionAttempted: false, callerAttestsOwnedSerialReady, callerAttestsOwnedGdtTssReady, callerAttestsOwnedIdtReady, callerAttestsPageFaultHandlerReady);
    }

    public static SeparatePageFaultRequest CreateForValidation(NullPageFaultProbePlanResult nullReadPlan, ulong faultInstructionAddress, bool enabledByDefault, bool executionRequested, bool executionAttempted, bool callerAttestsOwnedSerialReady, bool callerAttestsOwnedGdtTssReady, bool callerAttestsOwnedIdtReady, bool callerAttestsPageFaultHandlerReady)
    {
        return new SeparatePageFaultRequest(nullReadPlan, faultInstructionAddress, enabledByDefault, executionRequested, executionAttempted, callerAttestsOwnedSerialReady, callerAttestsOwnedGdtTssReady, callerAttestsOwnedIdtReady, callerAttestsPageFaultHandlerReady);
    }
}
