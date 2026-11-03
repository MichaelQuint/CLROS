namespace OS.Kernel.Diagnostics;

public readonly struct FaultProbeStubPlanResult
{
    private FaultProbeStubPlanResult(bool isInitialized, FaultProbeStubPlanStatus status, FaultProbeStubKind kind, int bytesWritten, int requiredBytes)
    {
        IsInitialized = isInitialized;
        Status = status;
        Kind = kind;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
    }

    public bool IsInitialized
    {
        get;
    }
    public FaultProbeStubPlanStatus Status
    {
        get;
    }
    public FaultProbeStubKind Kind
    {
        get;
    }
    public int BytesWritten
    {
        get;
    }
    public int RequiredBytes
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == FaultProbeStubPlanStatus.Success && Kind != FaultProbeStubKind.Unknown && BytesWritten > 0 && BytesWritten == RequiredBytes;

    public static FaultProbeStubPlanResult Success(FaultProbeStubKind kind, int bytesWritten)
    {
        return new FaultProbeStubPlanResult(true, FaultProbeStubPlanStatus.Success, kind, bytesWritten, bytesWritten);
    }

    public static FaultProbeStubPlanResult Fail(FaultProbeStubPlanStatus status, FaultProbeStubKind kind, int requiredBytes)
    {
        return new FaultProbeStubPlanResult(true, status, kind, 0, requiredBytes);
    }
}
