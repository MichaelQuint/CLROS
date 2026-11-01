using OS.Std.Text;

namespace OS.Net;
public enum HttpRequestParseStatus
{
    Success = 0,
    BufferTooSmall = 1,
    MissingRequestLineTerminator = 2,
    UnsupportedMethod = 3,
    InvalidRequestTarget = 4,
    InvalidProtocol = 5,
    MissingHeaderTerminator = 6,
    InvalidHeader = 7,
    InvalidContentLength = 8,
    BodyTooShort = 9,
    UnsupportedTransferEncoding = 10,
    DuplicateContentLength = 11
}
