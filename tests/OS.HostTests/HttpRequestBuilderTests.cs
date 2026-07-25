using System.Runtime.CompilerServices;
using System.Text;
using OS.Net;

internal static class HttpRequestBuilderTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("net.http writes get request", TestWritesGetRequest, ref failures);
        Run("net.http rejects unsafe request path", TestRejectsUnsafeRequestPath, ref failures);
        Run("net.http rejects request buffer too small", TestRejectsRequestBufferTooSmall, ref failures);
        Run("net.http request default result is not success", TestDefaultResultIsNotSuccess, ref failures);
        Run("net.http.request builder uses shared buffer operations", TestUsesSharedBufferOperations, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("HTTP request builder host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestWritesGetRequest()
    {
        byte[] host = Encoding.ASCII.GetBytes("example.test");
        byte[] path = Encoding.ASCII.GetBytes("/healthz");
        byte[] destination = new byte[256];

        HttpRequestBuilderStatus status = HttpRequestBuilder.TryWriteGet(
            host,
            path,
            destination,
            out HttpRequestBuilderResult result);

        string request = Encoding.ASCII.GetString(destination, 0, result.BytesWritten);
        string expected =
            "GET /healthz HTTP/1.1\r\n" +
            "Host: example.test\r\n" +
            "User-Agent: OS/0.1\r\n" +
            "Accept: */*\r\n" +
            "Connection: close\r\n" +
            "\r\n";

        return status == HttpRequestBuilderStatus.Success
            && result.Succeeded
            && result.BytesWritten == expected.Length
            && result.BytesRequired == expected.Length
            && request == expected;
    }

    private static bool TestRejectsUnsafeRequestPath()
    {
        byte[] host = Encoding.ASCII.GetBytes("example.test");
        byte[] noSlashPath = Encoding.ASCII.GetBytes("healthz");
        byte[] crlfPath = Encoding.ASCII.GetBytes("/healthz\r\nHost:evil");
        byte[] nonAsciiPath = [(byte)'/', 0xff];
        byte[] destination = new byte[256];

        HttpRequestBuilderStatus noSlashStatus = HttpRequestBuilder.TryWriteGet(
            host,
            noSlashPath,
            destination,
            out HttpRequestBuilderResult noSlashResult);
        HttpRequestBuilderStatus crlfStatus = HttpRequestBuilder.TryWriteGet(
            host,
            crlfPath,
            destination,
            out HttpRequestBuilderResult crlfResult);
        HttpRequestBuilderStatus nonAsciiStatus = HttpRequestBuilder.TryWriteGet(
            host,
            nonAsciiPath,
            destination,
            out HttpRequestBuilderResult nonAsciiResult);

        return noSlashStatus == HttpRequestBuilderStatus.MissingLeadingSlash
            && !noSlashResult.Succeeded
            && crlfStatus == HttpRequestBuilderStatus.InvalidPath
            && !crlfResult.Succeeded
            && nonAsciiStatus == HttpRequestBuilderStatus.InvalidPath
            && !nonAsciiResult.Succeeded;
    }

    private static bool TestRejectsRequestBufferTooSmall()
    {
        byte[] host = Encoding.ASCII.GetBytes("example.test");
        byte[] invalidHost = Encoding.ASCII.GetBytes("bad host");
        byte[] path = Encoding.ASCII.GetBytes("/healthz");
        Span<byte> destination = stackalloc byte[16];
        destination.Fill(0xcc);

        HttpRequestBuilderStatus smallStatus = HttpRequestBuilder.TryWriteGet(
            host,
            path,
            destination,
            out HttpRequestBuilderResult smallResult);
        HttpRequestBuilderStatus hostStatus = HttpRequestBuilder.TryWriteGet(
            invalidHost,
            path,
            destination,
            out HttpRequestBuilderResult hostResult);

        bool destinationUnchanged = true;
        for (int i = 0; i < destination.Length; i++)
        {
            destinationUnchanged &= destination[i] == 0xcc;
        }

        return smallStatus == HttpRequestBuilderStatus.DestinationTooSmall
            && smallResult.BytesRequired > destination.Length
            && smallResult.BytesWritten == 0
            && !smallResult.Succeeded
            && destinationUnchanged
            && hostStatus == HttpRequestBuilderStatus.InvalidHost
            && !hostResult.Succeeded;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        HttpRequestBuilderResult result = default;
        HttpRequestBuilderResult failed = HttpRequestBuilderResult.Fail(
            HttpRequestBuilderStatus.DestinationTooSmall,
            bytesRequired: 64);

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == HttpRequestBuilderStatus.Success
            && result.BytesWritten == 0
            && result.BytesRequired == 0
            && failed.IsInitialized
            && !failed.Succeeded
            && failed.Status == HttpRequestBuilderStatus.DestinationTooSmall
            && failed.BytesWritten == 0
            && failed.BytesRequired == 64
            && !RuntimeHelpers.IsReferenceOrContainsReferences<HttpRequestBuilderResult>();
    }

    private static bool TestUsesSharedBufferOperations()
    {
        string source = File.ReadAllText(Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "src",
            "OS.Kernel",
            "Net",
            "HttpRequestBuilder.cs"));

        return source.Contains("using OS.Std.Memory;", StringComparison.Ordinal)
            && source.Contains("BufferOperations.MemoryCopy(path, destination.Slice(offset, path.Length))", StringComparison.Ordinal)
            && source.Contains("BufferOperations.MemoryCopy(host, destination.Slice(offset, host.Length))", StringComparison.Ordinal)
            && !source.Contains("path.CopyTo(destination.Slice(offset, path.Length))", StringComparison.Ordinal)
            && !source.Contains("host.CopyTo(destination.Slice(offset, host.Length))", StringComparison.Ordinal);
    }
}
