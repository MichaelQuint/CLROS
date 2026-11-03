using OS.Std.Text;

namespace OS.Net;

public enum HttpClientGetTransactionDiagnosticFormatStatus
{
    Success = 0,
    InvalidResult = 1,
    InvalidRequestBytes = 2,
    DestinationTooSmall = 3
}
