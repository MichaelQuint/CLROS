namespace OS.Net;

using OS.Std.Memory;

public readonly struct HttpRequestBuilderResult
{
    private HttpRequestBuilderResult(HttpRequestBuilderStatus status, int bytesWritten, int bytesRequired)
    {
        IsInitialized = true;
        Status = status;
        BytesWritten = bytesWritten;
        BytesRequired = bytesRequired;
    }

    public bool IsInitialized
    {
        get;
    }
    public HttpRequestBuilderStatus Status
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == HttpRequestBuilderStatus.Success;
    public int BytesWritten
    {
        get;
    }
    public int BytesRequired
    {
        get;
    }

    public static HttpRequestBuilderResult Success(int bytesWritten)
    {
        return new HttpRequestBuilderResult(HttpRequestBuilderStatus.Success, bytesWritten, bytesWritten);
    }

    public static HttpRequestBuilderResult Fail(HttpRequestBuilderStatus status, int bytesRequired)
    {
        return new HttpRequestBuilderResult(status, 0, bytesRequired);
    }
}
