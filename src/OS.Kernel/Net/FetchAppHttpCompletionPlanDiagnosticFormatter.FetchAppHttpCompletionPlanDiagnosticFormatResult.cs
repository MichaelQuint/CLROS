using OS.Std.Text;

namespace OS.Net;
public readonly struct FetchAppHttpCompletionPlanDiagnosticFormatResult
{
    private const uint InitializedFlag = 1;
    public FetchAppHttpCompletionPlanDiagnosticFormatResult(FetchAppHttpCompletionPlanDiagnosticFormatStatus status, FetchAppHttpCompletionPlanStatus completionStatus, HttpTcpClientGetPlanStatus httpPlanStatus, int bytesWritten, int requiredBytes, int availableBytes, uint flags = InitializedFlag)
    {
        Status = status;
        CompletionStatus = completionStatus;
        HttpPlanStatus = httpPlanStatus;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        Flags = flags;
    }

    public FetchAppHttpCompletionPlanDiagnosticFormatStatus Status { get; }
    public FetchAppHttpCompletionPlanStatus CompletionStatus { get; }
    public HttpTcpClientGetPlanStatus HttpPlanStatus { get; }
    public int BytesWritten { get; }
    public int RequiredBytes { get; }
    public int AvailableBytes { get; }
    public uint Flags { get; }
    public bool IsInitialized => (Flags & InitializedFlag) != 0;
    public bool Succeeded => IsInitialized && Status == FetchAppHttpCompletionPlanDiagnosticFormatStatus.Success && RequiredBytes > 0 && BytesWritten == RequiredBytes && RequiredBytes <= AvailableBytes;

    public static FetchAppHttpCompletionPlanDiagnosticFormatResult Fail(FetchAppHttpCompletionPlanDiagnosticFormatStatus status, FetchAppHttpCompletionPlanStatus completionStatus, HttpTcpClientGetPlanStatus httpPlanStatus, int requiredBytes, int availableBytes)
    {
        return new FetchAppHttpCompletionPlanDiagnosticFormatResult(status, completionStatus, httpPlanStatus, bytesWritten: 0, requiredBytes, availableBytes);
    }
}
