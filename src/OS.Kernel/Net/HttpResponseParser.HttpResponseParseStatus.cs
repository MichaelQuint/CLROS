using OS.Std.Text;

namespace OS.Net;
public enum HttpResponseParseStatus
{
    Success = 0,
    BufferTooSmall = 1,
    MissingStatusLineTerminator = 2,
    InvalidProtocol = 3,
    InvalidStatusCode = 4,
    MissingHeaderTerminator = 5,
    InvalidHeader = 6,
    InvalidContentLength = 7,
    BodyTooShort = 8,
    UnsupportedTransferEncoding = 9,
    MissingContentLength = 10,
    DuplicateContentLength = 11
}
