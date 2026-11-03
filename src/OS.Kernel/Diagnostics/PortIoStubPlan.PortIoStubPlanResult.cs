namespace OS.Kernel.Diagnostics;

public readonly struct PortIoStubPlanResult
{
    private PortIoStubPlanResult(PortIoStubPlanStatus status, int bytesWritten, int requiredBytes)
    {
        IsInitialized = true;
        Status = status;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
    }

    public bool IsInitialized
    {
        get;
    }
    public PortIoStubPlanStatus Status
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
    public bool Succeeded => IsInitialized && Status == PortIoStubPlanStatus.Success && RequiredBytes > 0 && BytesWritten == RequiredBytes;

    public static PortIoStubPlanResult Success(int bytesWritten, int requiredBytes)
    {
        return new PortIoStubPlanResult(PortIoStubPlanStatus.Success, bytesWritten, requiredBytes);
    }

    public static PortIoStubPlanResult Fail(PortIoStubPlanStatus status, int requiredBytes)
    {
        return new PortIoStubPlanResult(status, 0, requiredBytes);
    }
}
