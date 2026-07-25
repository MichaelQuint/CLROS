using System.Runtime.CompilerServices;
using System.Text;
using OS.Net;

internal static class HttpResponseParserTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("net.http parses fixed-length 200 response", TestParsesFixedLength200Response, ref failures);
        Run("net.http parses zero-length response body", TestParsesZeroLengthResponseBody, ref failures);
        Run("net.http rejects malformed status line", TestRejectsMalformedStatusLine, ref failures);
        Run("net.http rejects incomplete headers", TestRejectsIncompleteHeaders, ref failures);
        Run("net.http rejects invalid content length", TestRejectsInvalidContentLength, ref failures);
        Run("net.http rejects duplicate response content length", TestRejectsDuplicateContentLength, ref failures);
        Run("net.http.response trims header value OWS", TestTrimsHeaderValueOws, ref failures);
        Run("net.http rejects missing content length", TestRejectsMissingContentLength, ref failures);
        Run("net.http rejects short body", TestRejectsShortBody, ref failures);
        Run("net.http rejects unsupported transfer encoding", TestRejectsUnsupportedTransferEncoding, ref failures);
        Run("net.http response parse default result is not success", TestDefaultResultIsNotSuccess, ref failures);
        Run("net.http.response uses shared memory index helper", TestUsesSharedMemoryIndexHelper, ref failures);
        Run("net.http.response uses shared ascii equality helper", TestUsesSharedAsciiEqualityHelper, ref failures);
        Run("net.http.response uses shared memory protocol prefix helper", TestUsesSharedMemoryProtocolPrefixHelper, ref failures);
        Run("net.http.response uses shared ascii digit helper", TestUsesSharedAsciiDigitHelper, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("HTTP response parser host tests failed: " + failures);
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

    private static bool TestParsesFixedLength200Response()
    {
        byte[] response = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Length: 5\r\nConnection: close\r\n\r\nhelloextra");

        HttpResponseParseStatus status = HttpResponseParser.TryParse(response, out HttpResponseParseResult result);

        return status == HttpResponseParseStatus.Success
            && result.Succeeded
            && result.ProtocolMajor == 1
            && result.ProtocolMinor == 1
            && result.StatusCode == 200
            && result.ReasonOffset == 13
            && result.ReasonLength == 2
            && result.HasContentLength
            && result.ContentLength == 5
            && result.ConnectionClose
            && result.BodyLength == 5
            && response[result.BodyOffset] == (byte)'h'
            && response[result.BodyOffset + 4] == (byte)'o';
    }

    private static bool TestParsesZeroLengthResponseBody()
    {
        byte[] response = Encoding.ASCII.GetBytes(
            "HTTP/1.0 204 No Content\r\nContent-Length: 0\r\nConnection: keep-alive\r\n\r\n");

        HttpResponseParseStatus status = HttpResponseParser.TryParse(response, out HttpResponseParseResult result);

        return status == HttpResponseParseStatus.Success
            && result.ProtocolMajor == 1
            && result.ProtocolMinor == 0
            && result.StatusCode == 204
            && result.HasContentLength
            && result.ContentLength == 0
            && !result.ConnectionClose
            && result.BodyLength == 0
            && result.BodyOffset == response.Length;
    }

    private static bool TestRejectsMalformedStatusLine()
    {
        byte[] invalidProtocol = Encoding.ASCII.GetBytes("HTP/1.1 200 OK\r\n\r\n");
        byte[] invalidStatus = Encoding.ASCII.GetBytes("HTTP/1.1 20X OK\r\n\r\n");
        byte[] missingStatusLineTerminator = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK");

        HttpResponseParseStatus protocolStatus = HttpResponseParser.TryParse(
            invalidProtocol,
            out HttpResponseParseResult protocolResult);
        HttpResponseParseStatus statusCodeStatus = HttpResponseParser.TryParse(
            invalidStatus,
            out HttpResponseParseResult statusCodeResult);
        HttpResponseParseStatus terminatorStatus = HttpResponseParser.TryParse(
            missingStatusLineTerminator,
            out HttpResponseParseResult terminatorResult);

        return protocolStatus == HttpResponseParseStatus.InvalidProtocol
            && !protocolResult.Succeeded
            && statusCodeStatus == HttpResponseParseStatus.InvalidStatusCode
            && !statusCodeResult.Succeeded
            && terminatorStatus == HttpResponseParseStatus.MissingStatusLineTerminator
            && !terminatorResult.Succeeded;
    }

    private static bool TestRejectsIncompleteHeaders()
    {
        byte[] incomplete = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n");

        HttpResponseParseStatus status = HttpResponseParser.TryParse(
            incomplete,
            out HttpResponseParseResult result);

        return status == HttpResponseParseStatus.MissingHeaderTerminator
            && !result.Succeeded;
    }

    private static bool TestRejectsInvalidContentLength()
    {
        byte[] invalidContentLength = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Length: nope\r\n\r\n");

        HttpResponseParseStatus status = HttpResponseParser.TryParse(
            invalidContentLength,
            out HttpResponseParseResult result);

        return status == HttpResponseParseStatus.InvalidContentLength
            && !result.Succeeded;
    }

    private static bool TestRejectsDuplicateContentLength()
    {
        byte[] duplicateSameCase = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Length: 5\r\nContent-Length: 5\r\n\r\nhello");
        byte[] duplicateMixedCase = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Length: 5\r\ncontent-length: 5\r\n\r\nhello");

        HttpResponseParseStatus sameCaseStatus = HttpResponseParser.TryParse(
            duplicateSameCase,
            out HttpResponseParseResult sameCaseResult);
        HttpResponseParseStatus mixedCaseStatus = HttpResponseParser.TryParse(
            duplicateMixedCase,
            out HttpResponseParseResult mixedCaseResult);

        return sameCaseStatus == HttpResponseParseStatus.DuplicateContentLength
            && !sameCaseResult.Succeeded
            && mixedCaseStatus == HttpResponseParseStatus.DuplicateContentLength
            && !mixedCaseResult.Succeeded;
    }

    private static bool TestTrimsHeaderValueOws()
    {
        byte[] response = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Length: 5 \t\r\nConnection: close\t\r\nTransfer-Encoding: identity \r\n\r\nhello");

        HttpResponseParseStatus status = HttpResponseParser.TryParse(
            response,
            out HttpResponseParseResult result);

        return status == HttpResponseParseStatus.Success
            && result.Succeeded
            && result.HasContentLength
            && result.ContentLength == 5
            && result.ConnectionClose
            && result.BodyLength == 5
            && response[result.BodyOffset] == (byte)'h'
            && response[result.BodyOffset + 4] == (byte)'o';
    }

    private static bool TestRejectsMissingContentLength()
    {
        byte[] missingContentLength = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nConnection: close\r\n\r\nhello");

        HttpResponseParseStatus status = HttpResponseParser.TryParse(
            missingContentLength,
            out HttpResponseParseResult result);

        return status == HttpResponseParseStatus.MissingContentLength
            && !result.Succeeded;
    }

    private static bool TestRejectsShortBody()
    {
        byte[] truncated = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Length: 8\r\n\r\nhello");

        HttpResponseParseStatus status = HttpResponseParser.TryParse(
            truncated,
            out HttpResponseParseResult result);

        return status == HttpResponseParseStatus.BodyTooShort
            && !result.Succeeded;
    }

    private static bool TestRejectsUnsupportedTransferEncoding()
    {
        byte[] chunked = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n0\r\n\r\n");

        HttpResponseParseStatus status = HttpResponseParser.TryParse(
            chunked,
            out HttpResponseParseResult result);

        return status == HttpResponseParseStatus.UnsupportedTransferEncoding
            && !result.Succeeded;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        HttpResponseParseResult defaultResult = default;
        HttpResponseParseResult failedResult =
            HttpResponseParseResult.Fail(HttpResponseParseStatus.BufferTooSmall);

        return !defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == HttpResponseParseStatus.Success
            && failedResult.IsInitialized
            && !failedResult.Succeeded
            && failedResult.Status == HttpResponseParseStatus.BufferTooSmall
            && !RuntimeHelpers.IsReferenceOrContainsReferences<HttpResponseParseResult>();
    }

    private static bool TestUsesSharedMemoryIndexHelper()
    {
        string root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(root, "src", "OS.Kernel", "Net", "HttpResponseParser.cs"));

        return CountOccurrences(source, "OS.Std.MemoryExtensions.IndexOf") >= 2
            && source.Contains(
                "OS.Std.MemoryExtensions.IndexOf(bytes.Slice(start, endExclusive - start), value)",
                StringComparison.Ordinal)
            && source.Contains(
                "OS.Std.MemoryExtensions.IndexOf(bytes.Slice(start), value)",
                StringComparison.Ordinal)
            && !source.Contains("for (int index = start; index <= bytes.Length - 4", StringComparison.Ordinal)
            && !source.Contains("for (int index = start; index <= bytes.Length - 2", StringComparison.Ordinal)
            && !source.Contains("for (int index = start; index < endExclusive", StringComparison.Ordinal);
    }

    private static bool TestUsesSharedAsciiEqualityHelper()
    {
        string root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(root, "src", "OS.Kernel", "Net", "HttpResponseParser.cs"));

        return CountOccurrences(source, "AsciiString.EqualsIgnoreCase") >= 5
            && source.Contains("using OS.Std.Text;", StringComparison.Ordinal)
            && source.Contains("AsciiString.EqualsIgnoreCase(name, \"Content-Length\"u8)", StringComparison.Ordinal)
            && source.Contains("AsciiString.EqualsIgnoreCase(name, \"Connection\"u8)", StringComparison.Ordinal)
            && source.Contains("AsciiString.EqualsIgnoreCase(value, \"close\"u8)", StringComparison.Ordinal)
            && source.Contains("AsciiString.EqualsIgnoreCase(name, \"Transfer-Encoding\"u8)", StringComparison.Ordinal)
            && source.Contains("AsciiString.EqualsIgnoreCase(value, \"identity\"u8)", StringComparison.Ordinal)
            && !source.Contains("private static bool AsciiEqualsIgnoreCase", StringComparison.Ordinal)
            && !source.Contains("private static byte ToLowerAscii", StringComparison.Ordinal);
    }

    private static bool TestUsesSharedMemoryProtocolPrefixHelper()
    {
        string root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(root, "src", "OS.Kernel", "Net", "HttpResponseParser.cs"));

        return source.Contains(
                "OS.Std.MemoryExtensions.StartsWith(response, \"HTTP/\"u8)",
                StringComparison.Ordinal)
            && !source.Contains("response[0] != (byte)'H'", StringComparison.Ordinal)
            && !source.Contains("response[1] != (byte)'T'", StringComparison.Ordinal)
            && !source.Contains("response[2] != (byte)'T'", StringComparison.Ordinal)
            && !source.Contains("response[3] != (byte)'P'", StringComparison.Ordinal)
            && !source.Contains("response[4] != (byte)'/'", StringComparison.Ordinal);
    }

    private static bool TestUsesSharedAsciiDigitHelper()
    {
        string root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(root, "src", "OS.Kernel", "Net", "HttpResponseParser.cs"));

        return source.Contains("OS.Std.Ascii.IsDigit(c)", StringComparison.Ordinal)
            && !source.Contains("private static bool IsDigit(byte value)", StringComparison.Ordinal);
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int offset = 0;
        while (offset < source.Length)
        {
            int index = source.IndexOf(value, offset, StringComparison.Ordinal);
            if (index < 0)
            {
                return count;
            }

            count++;
            offset = index + value.Length;
        }

        return count;
    }

    private static string FindRepositoryRoot()
    {
        string? current = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "OS.slnx")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
