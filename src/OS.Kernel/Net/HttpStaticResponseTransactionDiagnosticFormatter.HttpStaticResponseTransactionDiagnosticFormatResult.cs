using OS.Std.Text;

namespace OS.Net;
public readonly struct HttpStaticResponseTransactionDiagnosticFormatResult
{
    private const uint InitializedFlag = 1;
    public HttpStaticResponseTransactionDiagnosticFormatResult(HttpStaticResponseTransactionDiagnosticFormatStatus status, HttpStaticResponseTransactionStatus transactionStatus, int bytesWritten, int requiredBytes, int availableBytes, uint flags = InitializedFlag)
    {
        Status = status;
        TransactionStatus = transactionStatus;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        Flags = flags;
    }

    public HttpStaticResponseTransactionDiagnosticFormatStatus Status { get; }
    public HttpStaticResponseTransactionStatus TransactionStatus { get; }
    public int BytesWritten { get; }
    public int RequiredBytes { get; }
    public int AvailableBytes { get; }
    public uint Flags { get; }
    public bool IsInitialized => (Flags & InitializedFlag) != 0;
    public bool Succeeded => IsInitialized && Status == HttpStaticResponseTransactionDiagnosticFormatStatus.Success && RequiredBytes > 0 && BytesWritten == RequiredBytes && RequiredBytes <= AvailableBytes;

    public static HttpStaticResponseTransactionDiagnosticFormatResult Fail(HttpStaticResponseTransactionDiagnosticFormatStatus status, HttpStaticResponseTransactionStatus transactionStatus, int requiredBytes, int availableBytes)
    {
        return new HttpStaticResponseTransactionDiagnosticFormatResult(status, transactionStatus, bytesWritten: 0, requiredBytes, availableBytes);
    }
}
