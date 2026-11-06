namespace OS.Kernel.Diagnostics;
/// <summary>
/// A disabled plan or a comparison of supplied values. Even an accepted
/// comparison cannot attest that CR2, RIP, or any frame came from hardware.
/// </summary>

public readonly struct SeparatePageFaultResult
{
    private readonly byte _initialized;
    internal SeparatePageFaultResult(SeparatePageFaultStatus status, SeparatePageFaultRequest request, SeparatePageFaultField firstMismatchField = SeparatePageFaultField.None, byte comparedFieldCount = 0, ulong expectedValue = 0, ulong observedValue = 0)
    {
        _initialized = 1;
        Status = status;
        Request = request;
        FirstMismatchField = firstMismatchField;
        ComparedFieldCount = comparedFieldCount;
        ExpectedValue = expectedValue;
        ObservedValue = observedValue;
    }

    public bool IsInitialized => _initialized == 1 && Status > SeparatePageFaultStatus.Uninitialized && Status <= SeparatePageFaultStatus.InstructionAddressMismatch && FirstMismatchField <= SeparatePageFaultField.Rip && ComparedFieldCount <= SeparateDisabledDefaultPageFaultCR2ErrorCode.AuditFieldCount;
    public SeparatePageFaultStatus Status
    {
        get;
    }
    public SeparatePageFaultRequest Request
    {
        get;
    }
    public SeparatePageFaultField FirstMismatchField
    {
        get;
    }
    public byte ComparedFieldCount
    {
        get;
    }
    public ulong ExpectedValue
    {
        get;
    }
    public ulong ObservedValue
    {
        get;
    }
    public bool ReadyDisabled => IsConsistentSuccess && Status == SeparatePageFaultStatus.ReadyDisabled && ComparedFieldCount == 0;
    public bool SyntheticObservationAccepted => IsConsistentSuccess && Status == SeparatePageFaultStatus.SyntheticObservationAccepted && ComparedFieldCount == SeparateDisabledDefaultPageFaultCR2ErrorCode.AuditFieldCount;
    public bool Succeeded => ReadyDisabled || SyntheticObservationAccepted;
    public bool IsGuestEvidence => false;
    public bool CapturedHardwareState => false;
    public bool FaultTriggered => false;
    private bool IsConsistentSuccess => IsInitialized && FirstMismatchField == SeparatePageFaultField.None && ExpectedValue == 0 && ObservedValue == 0 && SeparateDisabledDefaultPageFaultCR2ErrorCode.ValidateRequest(Request) == SeparatePageFaultStatus.ReadyDisabled;
}
