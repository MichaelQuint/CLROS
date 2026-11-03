using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Kernel.Build;
using OS.Tools.QemuRunner;

readonly struct HttpProbeResult
{
    public HttpProbeResult(bool passed, string reason, int? statusCode, int bodyByteCount, string bodySha256, long elapsedMilliseconds, string errorType)
    {
        Passed = passed;
        Reason = reason;
        StatusCode = statusCode;
        BodyByteCount = bodyByteCount;
        BodySha256 = bodySha256;
        ElapsedMilliseconds = elapsedMilliseconds;
        ErrorType = errorType;
    }

    public bool Passed
    {
        get;
    }
    public string Reason
    {
        get;
    }
    public int? StatusCode
    {
        get;
    }
    public string StatusCodeText => StatusCode.HasValue ? StatusCode.Value.ToString() : "none";
    public int BodyByteCount
    {
        get;
    }
    public string BodySha256
    {
        get;
    }
    public long ElapsedMilliseconds
    {
        get;
    }
    public string ErrorType
    {
        get;
    }

    public static HttpProbeResult Failed(string reason, long elapsedMilliseconds, string errorType)
    {
        return new HttpProbeResult(false, reason, null, 0, "none", elapsedMilliseconds, errorType);
    }
}
