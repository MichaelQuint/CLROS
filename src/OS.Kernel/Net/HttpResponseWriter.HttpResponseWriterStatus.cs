namespace OS.Net;
using OS.Std.Memory;

public enum HttpResponseWriterStatus
{
    Success = 0,
    DestinationTooSmall = 1,
    InvalidStatusCode = 2,
    EmptyReasonPhrase = 3,
    InvalidReasonPhrase = 4,
    EmptyContentType = 5,
    InvalidContentType = 6
}
