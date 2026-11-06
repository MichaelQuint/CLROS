using OS.Std.Text;

namespace OS.Net;

public readonly struct HttpTcpClientGetPlanDiagnosticFormatResult
{
    private const uint InitializedFlag = 1;
    public HttpTcpClientGetPlanDiagnosticFormatResult(HttpTcpClientGetPlanDiagnosticFormatStatus status, HttpTcpClientGetPlanStatus planStatus, int bytesWritten, int requiredBytes, int availableBytes, uint flags = InitializedFlag)
    {
        Status = status;
        PlanStatus = planStatus;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        Flags = flags;
    }

    public HttpTcpClientGetPlanDiagnosticFormatStatus Status
    {
        get;
    }
    public HttpTcpClientGetPlanStatus PlanStatus
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
    public int AvailableBytes
    {
        get;
    }
    public uint Flags
    {
        get;
    }
    public bool IsInitialized => (Flags & InitializedFlag) != 0;
    public bool Succeeded => IsInitialized && Status == HttpTcpClientGetPlanDiagnosticFormatStatus.Success && RequiredBytes > 0 && BytesWritten == RequiredBytes && RequiredBytes <= AvailableBytes;

    public static HttpTcpClientGetPlanDiagnosticFormatResult Fail(HttpTcpClientGetPlanDiagnosticFormatStatus status, HttpTcpClientGetPlanStatus planStatus, int requiredBytes, int availableBytes)
    {
        return new HttpTcpClientGetPlanDiagnosticFormatResult(status, planStatus, bytesWritten: 0, requiredBytes, availableBytes);
    }
}
