using OS.Std.Text;

namespace OS.Net;
public readonly struct HttpStaticTcpResponsePlanDiagnosticFormatResult
{
    private const uint InitializedFlag = 1;
    public HttpStaticTcpResponsePlanDiagnosticFormatResult(HttpStaticTcpResponsePlanDiagnosticFormatStatus status, HttpStaticTcpResponsePlanStatus planStatus, int bytesWritten, int requiredBytes, int availableBytes, uint flags = InitializedFlag)
    {
        Status = status;
        PlanStatus = planStatus;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        Flags = flags;
    }

    public HttpStaticTcpResponsePlanDiagnosticFormatStatus Status { get; }
    public HttpStaticTcpResponsePlanStatus PlanStatus { get; }
    public int BytesWritten { get; }
    public int RequiredBytes { get; }
    public int AvailableBytes { get; }
    public uint Flags { get; }
    public bool IsInitialized => (Flags & InitializedFlag) != 0;
    public bool Succeeded => IsInitialized && Status == HttpStaticTcpResponsePlanDiagnosticFormatStatus.Success && RequiredBytes > 0 && BytesWritten == RequiredBytes && RequiredBytes <= AvailableBytes;

    public static HttpStaticTcpResponsePlanDiagnosticFormatResult Fail(HttpStaticTcpResponsePlanDiagnosticFormatStatus status, HttpStaticTcpResponsePlanStatus planStatus, int requiredBytes, int availableBytes)
    {
        return new HttpStaticTcpResponsePlanDiagnosticFormatResult(status, planStatus, bytesWritten: 0, requiredBytes, availableBytes);
    }
}
