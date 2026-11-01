namespace OS.Net;
public enum HttpClientGetTransactionStatus
{
    Success = 0,
    InvalidState = 1,
    RequestBufferTooSmall = 2,
    EmptyHost = 3,
    InvalidHost = 4,
    EmptyPath = 5,
    MissingLeadingSlash = 6,
    InvalidPath = 7,
    RequestWriteFailed = 8,
    MalformedResponse = 9,
    UnsupportedTransferEncoding = 10,
    TruncatedBody = 11
}
