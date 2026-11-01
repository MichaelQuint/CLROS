using OS.Std.Text;

namespace OS.Net;
public readonly struct HttpRequestParseResult
{
    private HttpRequestParseResult(HttpRequestParseStatus status, HttpRequestMethod method, int targetOffset, int targetLength, byte protocolMajor, byte protocolMinor, int headerLength, bool hasHost, int hostOffset, int hostLength, bool hasContentLength, int contentLength, bool connectionClose, int bodyOffset, int bodyLength)
    {
        IsInitialized = true;
        Status = status;
        Method = method;
        TargetOffset = targetOffset;
        TargetLength = targetLength;
        ProtocolMajor = protocolMajor;
        ProtocolMinor = protocolMinor;
        HeaderLength = headerLength;
        HasHost = hasHost;
        HostOffset = hostOffset;
        HostLength = hostLength;
        HasContentLength = hasContentLength;
        ContentLength = contentLength;
        ConnectionClose = connectionClose;
        BodyOffset = bodyOffset;
        BodyLength = bodyLength;
    }

    public HttpRequestParseStatus Status { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == HttpRequestParseStatus.Success;
    public HttpRequestMethod Method { get; }
    public int TargetOffset { get; }
    public int TargetLength { get; }
    public byte ProtocolMajor { get; }
    public byte ProtocolMinor { get; }
    public int HeaderLength { get; }
    public bool HasHost { get; }
    public int HostOffset { get; }
    public int HostLength { get; }
    public bool HasContentLength { get; }
    public int ContentLength { get; }
    public bool ConnectionClose { get; }
    public int BodyOffset { get; }
    public int BodyLength { get; }

    public static HttpRequestParseResult Success(HttpRequestMethod method, int targetOffset, int targetLength, byte protocolMajor, byte protocolMinor, int headerLength, bool hasHost, int hostOffset, int hostLength, bool hasContentLength, int contentLength, bool connectionClose, int bodyOffset, int bodyLength)
    {
        return new HttpRequestParseResult(HttpRequestParseStatus.Success, method, targetOffset, targetLength, protocolMajor, protocolMinor, headerLength, hasHost, hostOffset, hostLength, hasContentLength, contentLength, connectionClose, bodyOffset, bodyLength);
    }

    public static HttpRequestParseResult Fail(HttpRequestParseStatus status)
    {
        return new HttpRequestParseResult(status, HttpRequestMethod.Unknown, 0, 0, 0, 0, 0, false, 0, 0, false, 0, false, 0, 0);
    }
}
