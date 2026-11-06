namespace OS.Kernel.Diagnostics;

public readonly struct PortIoOperationPlanResult
{
    private PortIoOperationPlanResult(PortIoOperationPlanStatus status, int bytesWritten, int requiredBytes)
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
    public PortIoOperationPlanStatus Status
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
    public bool Succeeded => IsInitialized && Status == PortIoOperationPlanStatus.Success && RequiredBytes > 0 && BytesWritten == RequiredBytes;

    public static PortIoOperationPlanResult Success(int bytesWritten)
    {
        return new PortIoOperationPlanResult(PortIoOperationPlanStatus.Success, bytesWritten, PortIoOperationPlan.RequiredByteLength);
    }

    public static PortIoOperationPlanResult Fail(PortIoOperationPlanStatus status)
    {
        return new PortIoOperationPlanResult(status, 0, PortIoOperationPlan.RequiredByteLength);
    }
}
