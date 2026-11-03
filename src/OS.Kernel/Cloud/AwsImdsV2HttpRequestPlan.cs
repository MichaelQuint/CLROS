namespace OS.Cloud;

public readonly struct AwsImdsV2HttpRequestPlan
{
    public AwsImdsV2HttpRequestPlan(string method, string host, ushort port, string pathAndQuery, string headerName, string headerValue)
    {
        Method = method;
        Host = host;
        Port = port;
        PathAndQuery = pathAndQuery;
        HeaderName = headerName;
        HeaderValue = headerValue;
    }

    public string Method
    {
        get;
    }
    public string Host
    {
        get;
    }
    public ushort Port
    {
        get;
    }
    public string PathAndQuery
    {
        get;
    }
    public string HeaderName
    {
        get;
    }
    public string HeaderValue
    {
        get;
    }
    public bool HasHeader => HeaderName.Length != 0;
}
