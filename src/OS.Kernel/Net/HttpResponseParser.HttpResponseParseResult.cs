using OS.Std.Text;

namespace OS.Net;
public readonly struct HttpResponseParseResult
{
    private HttpResponseParseResult(HttpResponseParseStatus status, byte protocolMajor, byte protocolMinor, ushort statusCode, int reasonOffset, int reasonLength, int headerLength, bool hasContentLength, int contentLength, bool connectionClose, int bodyOffset, int bodyLength)
    {
        IsInitialized = true;
        Status = status;
        ProtocolMajor = protocolMajor;
        ProtocolMinor = protocolMinor;
        StatusCode = statusCode;
        ReasonOffset = reasonOffset;
        ReasonLength = reasonLength;
        HeaderLength = headerLength;
        HasContentLength = hasContentLength;
        ContentLength = contentLength;
        ConnectionClose = connectionClose;
        BodyOffset = bodyOffset;
        BodyLength = bodyLength;
    }

    public HttpResponseParseStatus Status { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == HttpResponseParseStatus.Success;
    public byte ProtocolMajor { get; }
    public byte ProtocolMinor { get; }
    public ushort StatusCode { get; }
    public int ReasonOffset { get; }
    public int ReasonLength { get; }
    public int HeaderLength { get; }
    public bool HasContentLength { get; }
    public int ContentLength { get; }
    public bool ConnectionClose { get; }
    public int BodyOffset { get; }
    public int BodyLength { get; }

    public static HttpResponseParseResult Success(byte protocolMajor, byte protocolMinor, ushort statusCode, int reasonOffset, int reasonLength, int headerLength, bool hasContentLength, int contentLength, bool connectionClose, int bodyOffset, int bodyLength)
    {
        return new HttpResponseParseResult(HttpResponseParseStatus.Success, protocolMajor, protocolMinor, statusCode, reasonOffset, reasonLength, headerLength, hasContentLength, contentLength, connectionClose, bodyOffset, bodyLength);
    }

    public static HttpResponseParseResult Fail(HttpResponseParseStatus status)
    {
        return new HttpResponseParseResult(status, 0, 0, 0, 0, 0, 0, false, 0, false, 0, 0);
    }
}
