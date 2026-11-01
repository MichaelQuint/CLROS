using OS.Std.Text;

namespace OS.Net;
public readonly struct FetchAppHttpTcpExchangePlanDiagnosticFormatResult
{
    private const uint InitializedFlag = 1;
    public FetchAppHttpTcpExchangePlanDiagnosticFormatResult(FetchAppHttpTcpExchangePlanDiagnosticFormatStatus status, FetchAppHttpTcpExchangePlanStatus exchangeStatus, int bytesWritten, int requiredBytes, int availableBytes, uint flags = InitializedFlag)
    {
        Status = status;
        ExchangeStatus = exchangeStatus;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        Flags = flags;
    }

    public FetchAppHttpTcpExchangePlanDiagnosticFormatStatus Status { get; }
    public FetchAppHttpTcpExchangePlanStatus ExchangeStatus { get; }
    public int BytesWritten { get; }
    public int RequiredBytes { get; }
    public int AvailableBytes { get; }
    public uint Flags { get; }
    public bool IsInitialized => (Flags & InitializedFlag) != 0;
    public bool Succeeded => IsInitialized && Status == FetchAppHttpTcpExchangePlanDiagnosticFormatStatus.Success && RequiredBytes > 0 && BytesWritten == RequiredBytes && RequiredBytes <= AvailableBytes;

    public static FetchAppHttpTcpExchangePlanDiagnosticFormatResult Fail(FetchAppHttpTcpExchangePlanDiagnosticFormatStatus status, FetchAppHttpTcpExchangePlanStatus exchangeStatus, int requiredBytes, int availableBytes)
    {
        return new FetchAppHttpTcpExchangePlanDiagnosticFormatResult(status, exchangeStatus, bytesWritten: 0, requiredBytes, availableBytes);
    }
}
