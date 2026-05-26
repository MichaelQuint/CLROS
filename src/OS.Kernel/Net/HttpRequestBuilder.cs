namespace OS.Net;

using OS.Std.Memory;

public enum HttpRequestBuilderStatus
{
    Success = 0,
    DestinationTooSmall = 1,
    EmptyHost = 2,
    InvalidHost = 3,
    EmptyPath = 4,
    MissingLeadingSlash = 5,
    InvalidPath = 6
}

public readonly struct HttpRequestBuilderResult
{
    private HttpRequestBuilderResult(HttpRequestBuilderStatus status, int bytesWritten, int bytesRequired)
    {
        IsInitialized = true;
        Status = status;
        BytesWritten = bytesWritten;
        BytesRequired = bytesRequired;
    }

    public bool IsInitialized { get; }

    public HttpRequestBuilderStatus Status { get; }

    public bool Succeeded => IsInitialized && Status == HttpRequestBuilderStatus.Success;

    public int BytesWritten { get; }

    public int BytesRequired { get; }

    public static HttpRequestBuilderResult Success(int bytesWritten)
    {
        return new HttpRequestBuilderResult(
            HttpRequestBuilderStatus.Success,
            bytesWritten,
            bytesWritten);
    }

    public static HttpRequestBuilderResult Fail(HttpRequestBuilderStatus status, int bytesRequired)
    {
        return new HttpRequestBuilderResult(status, 0, bytesRequired);
    }
}

public static class HttpRequestBuilder
{
    private const string MethodPrefix = "GET ";
    private const string ProtocolSuffix = " HTTP/1.1\r\n";
    private const string HostHeaderPrefix = "Host: ";
    private const string UserAgentHeader = "User-Agent: OS/0.1\r\n";
    private const string AcceptHeader = "Accept: */*\r\n";
    private const string ConnectionHeader = "Connection: close\r\n";
    private const string HeaderTerminator = "\r\n";

    private const int FixedBytes =
        4 // GET
        + 11 //  HTTP/1.1\r\n
        + 6 // Host:
        + 20 // User-Agent: OS/0.1\r\n
        + 13 // Accept: */*\r\n
        + 19 // Connection: close\r\n
        + 2; // \r\n

    public static HttpRequestBuilderStatus TryWriteGet(
        ReadOnlySpan<byte> host,
        ReadOnlySpan<byte> path,
        Span<byte> destination,
        out HttpRequestBuilderResult result)
    {
        int bytesRequired = FixedBytes + host.Length + path.Length;

        if (host.Length == 0)
        {
            result = HttpRequestBuilderResult.Fail(HttpRequestBuilderStatus.EmptyHost, bytesRequired);
            return result.Status;
        }

        if (!IsSafeHeaderValue(host))
        {
            result = HttpRequestBuilderResult.Fail(HttpRequestBuilderStatus.InvalidHost, bytesRequired);
            return result.Status;
        }

        if (path.Length == 0)
        {
            result = HttpRequestBuilderResult.Fail(HttpRequestBuilderStatus.EmptyPath, bytesRequired);
            return result.Status;
        }

        if (path[0] != (byte)'/')
        {
            result = HttpRequestBuilderResult.Fail(HttpRequestBuilderStatus.MissingLeadingSlash, bytesRequired);
            return result.Status;
        }

        if (!IsSafePath(path))
        {
            result = HttpRequestBuilderResult.Fail(HttpRequestBuilderStatus.InvalidPath, bytesRequired);
            return result.Status;
        }

        if (destination.Length < bytesRequired)
        {
            result = HttpRequestBuilderResult.Fail(HttpRequestBuilderStatus.DestinationTooSmall, bytesRequired);
            return result.Status;
        }

        int offset = 0;
        WriteAscii(MethodPrefix, destination, ref offset);
        BufferOperationResult pathCopy =
            BufferOperations.MemoryCopy(path, destination.Slice(offset, path.Length));
        if (!pathCopy.Succeeded)
        {
            result = HttpRequestBuilderResult.Fail(
                HttpRequestBuilderStatus.DestinationTooSmall,
                bytesRequired);
            return result.Status;
        }

        offset += path.Length;
        WriteAscii(ProtocolSuffix, destination, ref offset);
        WriteAscii(HostHeaderPrefix, destination, ref offset);
        BufferOperationResult hostCopy =
            BufferOperations.MemoryCopy(host, destination.Slice(offset, host.Length));
        if (!hostCopy.Succeeded)
        {
            result = HttpRequestBuilderResult.Fail(
                HttpRequestBuilderStatus.DestinationTooSmall,
                bytesRequired);
            return result.Status;
        }

        offset += host.Length;
        WriteAscii("\r\n", destination, ref offset);
        WriteAscii(UserAgentHeader, destination, ref offset);
        WriteAscii(AcceptHeader, destination, ref offset);
        WriteAscii(ConnectionHeader, destination, ref offset);
        WriteAscii(HeaderTerminator, destination, ref offset);

        result = HttpRequestBuilderResult.Success(offset);
        return result.Status;
    }

    private static void WriteAscii(string value, Span<byte> destination, ref int offset)
    {
        for (int i = 0; i < value.Length; i++)
        {
            destination[offset] = (byte)value[i];
            offset++;
        }
    }

    private static bool IsSafeHeaderValue(ReadOnlySpan<byte> value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            byte b = value[i];
            if (b < 0x21 || b > 0x7e || b == (byte)'\r' || b == (byte)'\n')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSafePath(ReadOnlySpan<byte> value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            byte b = value[i];
            if (b < 0x21 || b > 0x7e || b == (byte)'\r' || b == (byte)'\n')
            {
                return false;
            }
        }

        return true;
    }
}
