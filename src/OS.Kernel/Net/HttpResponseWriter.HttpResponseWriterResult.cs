namespace OS.Net;
using OS.Std.Memory;

public readonly struct HttpResponseWriterResult
{
    private HttpResponseWriterResult(HttpResponseWriterStatus status, ushort statusCode, int bytesWritten, int bytesRequired, int headerLength, int bodyOffset, int bodyLength)
    {
        IsInitialized = true;
        Status = status;
        StatusCode = statusCode;
        BytesWritten = bytesWritten;
        BytesRequired = bytesRequired;
        HeaderLength = headerLength;
        BodyOffset = bodyOffset;
        BodyLength = bodyLength;
    }

    public bool IsInitialized { get; }
    public HttpResponseWriterStatus Status { get; }
    public bool Succeeded => IsInitialized && Status == HttpResponseWriterStatus.Success;
    public ushort StatusCode { get; }
    public int BytesWritten { get; }
    public int BytesRequired { get; }
    public int HeaderLength { get; }
    public int BodyOffset { get; }
    public int BodyLength { get; }

    public static HttpResponseWriterResult Success(ushort statusCode, int bytesWritten, int headerLength, int bodyOffset, int bodyLength)
    {
        return new HttpResponseWriterResult(HttpResponseWriterStatus.Success, statusCode, bytesWritten, bytesWritten, headerLength, bodyOffset, bodyLength);
    }

    public static HttpResponseWriterResult Fail(HttpResponseWriterStatus status, ushort statusCode, int bytesRequired)
    {
        return new HttpResponseWriterResult(status, statusCode, 0, bytesRequired, 0, 0, 0);
    }
}
