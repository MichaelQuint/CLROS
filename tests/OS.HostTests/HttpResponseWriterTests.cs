using System.Runtime.CompilerServices;
using System.Text;
using OS.Net;

internal static class HttpResponseWriterTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("net.http.response writes healthz text response", TestWritesHealthzTextResponse, ref failures);
        Run("net.http.response writes empty keepalive response", TestWritesEmptyKeepAliveResponse, ref failures);
        Run("net.http.response rejects invalid metadata", TestRejectsInvalidMetadata, ref failures);
        Run("net.http.response rejects short destination without write", TestRejectsShortDestinationWithoutWrite, ref failures);
        Run("net.http.response default result is not success", TestDefaultResultIsNotSuccess, ref failures);
        Run("net.http.response writer uses shared buffer operations", TestUsesSharedBufferOperations, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("HTTP response writer host tests failed: " + failures);
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

    private static bool TestWritesHealthzTextResponse()
    {
        byte[] reason = Encoding.ASCII.GetBytes("OK");
        byte[] contentType = Encoding.ASCII.GetBytes("text/plain");
        byte[] body = Encoding.ASCII.GetBytes("OS_MINIMAL_API_OK");
        byte[] destination = new byte[256];

        HttpResponseWriterStatus status = HttpResponseWriter.TryWrite(
            200,
            reason,
            contentType,
            body,
            connectionClose: true,
            destination,
            out HttpResponseWriterResult result);

        string response = Encoding.ASCII.GetString(destination, 0, result.BytesWritten);
        string expected =
            "HTTP/1.1 200 OK\r\n" +
            "Content-Length: 17\r\n" +
            "Content-Type: text/plain\r\n" +
            "Connection: close\r\n" +
            "\r\n" +
            "OS_MINIMAL_API_OK";

        return status == HttpResponseWriterStatus.Success
            && result.Succeeded
            && result.StatusCode == 200
            && result.BytesWritten == expected.Length
            && result.BytesRequired == expected.Length
            && result.HeaderLength == expected.Length - body.Length
            && result.BodyOffset == result.HeaderLength
            && result.BodyLength == body.Length
            && response == expected;
    }

    private static bool TestWritesEmptyKeepAliveResponse()
    {
        byte[] reason = Encoding.ASCII.GetBytes("No Content");
        byte[] contentType = Encoding.ASCII.GetBytes("text/plain");
        ReadOnlySpan<byte> body = ReadOnlySpan<byte>.Empty;
        byte[] destination = new byte[128];

        HttpResponseWriterStatus status = HttpResponseWriter.TryWrite(
            204,
            reason,
            contentType,
            body,
            connectionClose: false,
            destination,
            out HttpResponseWriterResult result);

        string response = Encoding.ASCII.GetString(destination, 0, result.BytesWritten);
        string expected =
            "HTTP/1.1 204 No Content\r\n" +
            "Content-Length: 0\r\n" +
            "Content-Type: text/plain\r\n" +
            "Connection: keep-alive\r\n" +
            "\r\n";

        return status == HttpResponseWriterStatus.Success
            && result.Succeeded
            && result.BodyLength == 0
            && result.BodyOffset == expected.Length
            && response == expected;
    }

    private static bool TestRejectsInvalidMetadata()
    {
        byte[] reason = Encoding.ASCII.GetBytes("OK");
        byte[] badReason = Encoding.ASCII.GetBytes("OK\r\nInjected");
        byte[] contentType = Encoding.ASCII.GetBytes("text/plain");
        byte[] badContentType = Encoding.ASCII.GetBytes("text/plain\r\nX: y");
        byte[] body = Encoding.ASCII.GetBytes("body");
        byte[] destination = new byte[128];

        HttpResponseWriterStatus statusCodeStatus = HttpResponseWriter.TryWrite(
            99,
            reason,
            contentType,
            body,
            connectionClose: true,
            destination,
            out HttpResponseWriterResult statusCodeResult);
        HttpResponseWriterStatus reasonStatus = HttpResponseWriter.TryWrite(
            200,
            badReason,
            contentType,
            body,
            connectionClose: true,
            destination,
            out HttpResponseWriterResult reasonResult);
        HttpResponseWriterStatus contentTypeStatus = HttpResponseWriter.TryWrite(
            200,
            reason,
            badContentType,
            body,
            connectionClose: true,
            destination,
            out HttpResponseWriterResult contentTypeResult);
        HttpResponseWriterStatus emptyContentTypeStatus = HttpResponseWriter.TryWrite(
            200,
            reason,
            ReadOnlySpan<byte>.Empty,
            body,
            connectionClose: true,
            destination,
            out HttpResponseWriterResult emptyContentTypeResult);

        return statusCodeStatus == HttpResponseWriterStatus.InvalidStatusCode
            && !statusCodeResult.Succeeded
            && reasonStatus == HttpResponseWriterStatus.InvalidReasonPhrase
            && !reasonResult.Succeeded
            && contentTypeStatus == HttpResponseWriterStatus.InvalidContentType
            && !contentTypeResult.Succeeded
            && emptyContentTypeStatus == HttpResponseWriterStatus.EmptyContentType
            && !emptyContentTypeResult.Succeeded;
    }

    private static bool TestRejectsShortDestinationWithoutWrite()
    {
        byte[] reason = Encoding.ASCII.GetBytes("OK");
        byte[] contentType = Encoding.ASCII.GetBytes("text/plain");
        byte[] body = Encoding.ASCII.GetBytes("OS_MINIMAL_API_OK");
        Span<byte> destination = stackalloc byte[16];
        destination.Fill(0xcc);

        HttpResponseWriterStatus status = HttpResponseWriter.TryWrite(
            200,
            reason,
            contentType,
            body,
            connectionClose: true,
            destination,
            out HttpResponseWriterResult result);

        bool destinationUnchanged = true;
        for (int index = 0; index < destination.Length; index++)
        {
            destinationUnchanged &= destination[index] == 0xcc;
        }

        return status == HttpResponseWriterStatus.DestinationTooSmall
            && !result.Succeeded
            && result.BytesRequired > destination.Length
            && result.BytesWritten == 0
            && result.BodyLength == 0
            && destinationUnchanged;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        HttpResponseWriterResult defaultResult = default;
        HttpResponseWriterResult failedResult = HttpResponseWriterResult.Fail(
            HttpResponseWriterStatus.DestinationTooSmall,
            statusCode: 200,
            bytesRequired: 64);

        return !defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == HttpResponseWriterStatus.Success
            && defaultResult.StatusCode == 0
            && defaultResult.BytesWritten == 0
            && defaultResult.BytesRequired == 0
            && failedResult.IsInitialized
            && !failedResult.Succeeded
            && failedResult.Status == HttpResponseWriterStatus.DestinationTooSmall
            && failedResult.StatusCode == 200
            && failedResult.BytesRequired == 64
            && !RuntimeHelpers.IsReferenceOrContainsReferences<HttpResponseWriterResult>();
    }

    private static bool TestUsesSharedBufferOperations()
    {
        string source = File.ReadAllText(Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "src",
            "OS.Kernel",
            "Net",
            "HttpResponseWriter.cs"));

        return source.Contains("using OS.Std.Memory;", StringComparison.Ordinal)
            && source.Contains("BufferOperations.MemoryCopy(reasonPhrase, destination.Slice(offset, reasonPhrase.Length))", StringComparison.Ordinal)
            && source.Contains("BufferOperations.MemoryCopy(contentType, destination.Slice(offset, contentType.Length))", StringComparison.Ordinal)
            && source.Contains("BufferOperations.MemoryCopy(body, destination.Slice(offset, body.Length))", StringComparison.Ordinal)
            && !source.Contains("reasonPhrase.CopyTo(destination.Slice(offset, reasonPhrase.Length))", StringComparison.Ordinal)
            && !source.Contains("contentType.CopyTo(destination.Slice(offset, contentType.Length))", StringComparison.Ordinal)
            && !source.Contains("body.CopyTo(destination.Slice(offset, body.Length))", StringComparison.Ordinal);
    }
}
