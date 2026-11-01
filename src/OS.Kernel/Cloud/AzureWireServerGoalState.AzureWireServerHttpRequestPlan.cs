using System.Text;

namespace OS.Cloud;
public readonly struct AzureWireServerHttpRequestPlan
{
    public AzureWireServerHttpRequestPlan(string method, string host, ushort port, string pathAndQuery, string contentType, string body)
    {
        Method = method;
        Host = host;
        Port = port;
        PathAndQuery = pathAndQuery;
        ContentType = contentType;
        Body = body;
    }

    public string Method { get; }
    public string Host { get; }
    public ushort Port { get; }
    public string PathAndQuery { get; }
    public string ContentType { get; }
    public string Body { get; }
    public bool HasBody => Body.Length != 0;
    public int BodyByteLength => Encoding.UTF8.GetByteCount(Body);
}
