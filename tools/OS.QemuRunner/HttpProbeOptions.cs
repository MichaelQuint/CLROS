using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Kernel.Build;
using OS.Tools.QemuRunner;

readonly struct HttpProbeOptions
{
    public HttpProbeOptions(string url, int expectedStatusCode, string expectedBody, bool expectedBodyConfigured, int timeoutSeconds, bool post = false, bool vectorBody = false)
    {
        Url = url;
        ExpectedStatusCode = expectedStatusCode;
        ExpectedBody = expectedBody;
        ExpectedBodyConfigured = expectedBodyConfigured;
        TimeoutSeconds = timeoutSeconds;
        IsPost = post;
        VectorBody = vectorBody;
    }

    public string Url
    {
        get;
    }
    public bool IsPost
    {
        get;
    }
    public bool VectorBody
    {
        get;
    }
    public int ExpectedStatusCode
    {
        get;
    }
    public string ExpectedBody
    {
        get;
    }
    public bool ExpectedBodyConfigured
    {
        get;
    }

    public int ExpectedBodyByteLength
    {
        get
        {
            return VectorBody ? WebVectorResponse.Length : Encoding.UTF8.GetByteCount(ExpectedBody);
        }
    }

    public string ExpectedBodySha256 => VectorBody ? WebVectorResponse.Sha256 : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ExpectedBody))).ToLowerInvariant();
    public int TimeoutSeconds
    {
        get;
    }
}
