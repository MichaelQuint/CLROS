using OS.Std.Text;

namespace OS.Net;

public readonly struct HttpClientGetTransactionDiagnosticFormatResult
{
    private const uint InitializedFlag = 1;
    public HttpClientGetTransactionDiagnosticFormatResult(HttpClientGetTransactionDiagnosticFormatStatus status, HttpClientGetTransactionStatus transactionStatus, int bytesWritten, int requiredBytes, int availableBytes, uint flags = InitializedFlag)
    {
        Status = status;
        TransactionStatus = transactionStatus;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        Flags = flags;
    }

    public HttpClientGetTransactionDiagnosticFormatStatus Status
    {
        get;
    }
    public HttpClientGetTransactionStatus TransactionStatus
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
    public bool Succeeded => IsInitialized && Status == HttpClientGetTransactionDiagnosticFormatStatus.Success && RequiredBytes > 0 && BytesWritten == RequiredBytes && RequiredBytes <= AvailableBytes;

    public static HttpClientGetTransactionDiagnosticFormatResult Fail(HttpClientGetTransactionDiagnosticFormatStatus status, HttpClientGetTransactionStatus transactionStatus, int requiredBytes, int availableBytes)
    {
        return new HttpClientGetTransactionDiagnosticFormatResult(status, transactionStatus, bytesWritten: 0, requiredBytes, availableBytes);
    }
}
