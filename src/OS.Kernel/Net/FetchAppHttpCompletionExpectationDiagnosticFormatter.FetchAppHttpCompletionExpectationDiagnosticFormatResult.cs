using OS.Std.Text;

namespace OS.Net;
public readonly struct FetchAppHttpCompletionExpectationDiagnosticFormatResult
{
    private const uint InitializedFlag = 1;
    public FetchAppHttpCompletionExpectationDiagnosticFormatResult(FetchAppHttpCompletionExpectationDiagnosticFormatStatus status, FetchAppHttpCompletionExpectationStatus expectationStatus, int bytesWritten, int requiredBytes, int availableBytes, uint flags = InitializedFlag)
    {
        Status = status;
        ExpectationStatus = expectationStatus;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        Flags = flags;
    }

    public FetchAppHttpCompletionExpectationDiagnosticFormatStatus Status { get; }
    public FetchAppHttpCompletionExpectationStatus ExpectationStatus { get; }
    public int BytesWritten { get; }
    public int RequiredBytes { get; }
    public int AvailableBytes { get; }
    public uint Flags { get; }
    public bool IsInitialized => (Flags & InitializedFlag) != 0;
    public bool Succeeded => IsInitialized && Status == FetchAppHttpCompletionExpectationDiagnosticFormatStatus.Success && RequiredBytes > 0 && BytesWritten == RequiredBytes && RequiredBytes <= AvailableBytes;

    public static FetchAppHttpCompletionExpectationDiagnosticFormatResult Fail(FetchAppHttpCompletionExpectationDiagnosticFormatStatus status, FetchAppHttpCompletionExpectationStatus expectationStatus, int requiredBytes, int availableBytes)
    {
        return new FetchAppHttpCompletionExpectationDiagnosticFormatResult(status, expectationStatus, bytesWritten: 0, requiredBytes, availableBytes);
    }
}
