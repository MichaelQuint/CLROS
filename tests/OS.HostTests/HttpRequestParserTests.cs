using System.Runtime.CompilerServices;
using System.Text;
using OS.Net;

internal static class HttpRequestParserTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("net.http.request parses get healthz", TestParsesGetHealthz, ref failures);
        Run("net.http.request preserves body metadata", TestPreservesBodyMetadata, ref failures);
        Run("net.http.request accepts single content length", TestAcceptsSingleContentLength, ref failures);
        Run("net.http.request trims header value OWS", TestTrimsHeaderValueOws, ref failures);
        Run("net.http.request rejects duplicate content length", TestRejectsDuplicateContentLength, ref failures);
        Run("net.http.request rejects malformed request line", TestRejectsMalformedRequestLine, ref failures);
        Run("net.http.request rejects invalid headers and body", TestRejectsInvalidHeadersAndBody, ref failures);
        Run("net.http.request default result is not success", TestDefaultResultIsNotSuccess, ref failures);
        Run("net.http.request uses shared memory index helper", TestUsesSharedMemoryIndexHelper, ref failures);
        Run("net.http.request uses shared ascii equality helper", TestUsesSharedAsciiEqualityHelper, ref failures);
        Run("net.http.request uses shared memory protocol prefix helper", TestUsesSharedMemoryProtocolPrefixHelper, ref failures);
        Run("net.http.request uses shared ascii token helper", TestUsesSharedAsciiTokenHelper, ref failures);
        Run("net.http.request uses shared ascii space-or-tab helper", TestUsesSharedAsciiSpaceOrTabHelper, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("HTTP request parser host tests failed: " + failures);
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

    private static bool TestParsesGetHealthz()
    {
        byte[] request = Encoding.ASCII.GetBytes(
            "GET /healthz HTTP/1.1\r\nHost: example.test\r\nConnection: close\r\n\r\n");

        HttpRequestParseStatus status = HttpRequestParser.TryParse(
            request,
            out HttpRequestParseResult result);

        string target = Encoding.ASCII.GetString(request, result.TargetOffset, result.TargetLength);
        string host = Encoding.ASCII.GetString(request, result.HostOffset, result.HostLength);

        return status == HttpRequestParseStatus.Success
            && result.Succeeded
            && result.Method == HttpRequestMethod.Get
            && target == "/healthz"
            && result.ProtocolMajor == 1
            && result.ProtocolMinor == 1
            && result.HeaderLength == request.Length
            && result.HasHost
            && host == "example.test"
            && !result.HasContentLength
            && result.ContentLength == 0
            && result.ConnectionClose
            && result.BodyOffset == request.Length
            && result.BodyLength == 0;
    }

    private static bool TestPreservesBodyMetadata()
    {
        byte[] request = Encoding.ASCII.GetBytes(
            "POST /submit HTTP/1.0\r\nHost: example.test\r\nContent-Length: 4\r\n\r\nbodyextra");

        HttpRequestParseStatus status = HttpRequestParser.TryParse(
            request,
            out HttpRequestParseResult result);

        return status == HttpRequestParseStatus.Success
            && result.Succeeded
            && result.Method == HttpRequestMethod.Post
            && result.ProtocolMajor == 1
            && result.ProtocolMinor == 0
            && result.HasContentLength
            && result.ContentLength == 4
            && result.BodyLength == 4
            && request[result.BodyOffset] == (byte)'b'
            && request[result.BodyOffset + 3] == (byte)'y';
    }

    private static bool TestAcceptsSingleContentLength()
    {
        byte[] request = Encoding.ASCII.GetBytes(
            "POST /submit HTTP/1.1\r\nHost: example.test\r\nContent-Length: 4\r\n\r\nbody");

        HttpRequestParseStatus status = HttpRequestParser.TryParse(
            request,
            out HttpRequestParseResult result);

        return status == HttpRequestParseStatus.Success
            && result.Succeeded
            && result.HasContentLength
            && result.ContentLength == 4
            && result.HeaderLength == result.BodyOffset
            && result.BodyLength == 4
            && request[result.BodyOffset] == (byte)'b'
            && request[result.BodyOffset + 3] == (byte)'y';
    }

    private static bool TestTrimsHeaderValueOws()
    {
        byte[] request = Encoding.ASCII.GetBytes(
            "POST /submit HTTP/1.1\r\n" +
            "Host: example.test \t\r\n" +
            "Connection: close \t\r\n" +
            "Transfer-Encoding: identity \r\n" +
            "Content-Length: 4 \t\r\n" +
            "\r\n" +
            "body");
        byte[] invalidControl = Encoding.ASCII.GetBytes(
            "GET / HTTP/1.1\r\nConnection: close\u001f \t\r\n\r\n");

        HttpRequestParseStatus status = HttpRequestParser.TryParse(
            request,
            out HttpRequestParseResult result);
        HttpRequestParseStatus invalidStatus = HttpRequestParser.TryParse(
            invalidControl,
            out HttpRequestParseResult invalidResult);

        string host = Encoding.ASCII.GetString(request, result.HostOffset, result.HostLength);

        return status == HttpRequestParseStatus.Success
            && result.Succeeded
            && result.HasHost
            && host == "example.test \t"
            && result.ConnectionClose
            && result.HasContentLength
            && result.ContentLength == 4
            && result.BodyLength == 4
            && request[result.BodyOffset] == (byte)'b'
            && request[result.BodyOffset + 3] == (byte)'y'
            && invalidStatus == HttpRequestParseStatus.InvalidHeader
            && !invalidResult.Succeeded;
    }

    private static bool TestRejectsDuplicateContentLength()
    {
        byte[] duplicateSameCase = Encoding.ASCII.GetBytes(
            "POST /submit HTTP/1.1\r\nContent-Length: 4\r\nContent-Length: 4\r\n\r\nbody");
        byte[] duplicateMixedCase = Encoding.ASCII.GetBytes(
            "POST /submit HTTP/1.1\r\nContent-Length: 4\r\ncontent-length: 4\r\n\r\nbody");

        HttpRequestParseStatus sameCaseStatus = HttpRequestParser.TryParse(
            duplicateSameCase,
            out HttpRequestParseResult sameCaseResult);
        HttpRequestParseStatus mixedCaseStatus = HttpRequestParser.TryParse(
            duplicateMixedCase,
            out HttpRequestParseResult mixedCaseResult);

        return sameCaseStatus == HttpRequestParseStatus.DuplicateContentLength
            && !sameCaseResult.Succeeded
            && mixedCaseStatus == HttpRequestParseStatus.DuplicateContentLength
            && !mixedCaseResult.Succeeded;
    }

    private static bool TestRejectsMalformedRequestLine()
    {
        byte[] unsupportedMethod = Encoding.ASCII.GetBytes("PUT /healthz HTTP/1.1\r\n\r\n");
        byte[] invalidTarget = Encoding.ASCII.GetBytes("GET healthz HTTP/1.1\r\n\r\n");
        byte[] invalidProtocol = Encoding.ASCII.GetBytes("GET /healthz HTP/1.1\r\n\r\n");
        byte[] missingLineTerminator = Encoding.ASCII.GetBytes("GET /healthz HTTP/1.1");

        HttpRequestParseStatus methodStatus = HttpRequestParser.TryParse(
            unsupportedMethod,
            out HttpRequestParseResult methodResult);
        HttpRequestParseStatus targetStatus = HttpRequestParser.TryParse(
            invalidTarget,
            out HttpRequestParseResult targetResult);
        HttpRequestParseStatus protocolStatus = HttpRequestParser.TryParse(
            invalidProtocol,
            out HttpRequestParseResult protocolResult);
        HttpRequestParseStatus terminatorStatus = HttpRequestParser.TryParse(
            missingLineTerminator,
            out HttpRequestParseResult terminatorResult);

        return methodStatus == HttpRequestParseStatus.UnsupportedMethod
            && !methodResult.Succeeded
            && targetStatus == HttpRequestParseStatus.InvalidRequestTarget
            && !targetResult.Succeeded
            && protocolStatus == HttpRequestParseStatus.InvalidProtocol
            && !protocolResult.Succeeded
            && terminatorStatus == HttpRequestParseStatus.MissingRequestLineTerminator
            && !terminatorResult.Succeeded;
    }

    private static bool TestRejectsInvalidHeadersAndBody()
    {
        byte[] invalidHeader = Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nBad Header: x\r\n\r\n");
        byte[] invalidLength = Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nContent-Length: nope\r\n\r\n");
        byte[] truncatedBody = Encoding.ASCII.GetBytes("POST / HTTP/1.1\r\nContent-Length: 8\r\n\r\nbody");
        byte[] chunked = Encoding.ASCII.GetBytes("POST / HTTP/1.1\r\nTransfer-Encoding: chunked\r\n\r\n0\r\n\r\n");

        HttpRequestParseStatus invalidHeaderStatus = HttpRequestParser.TryParse(
            invalidHeader,
            out HttpRequestParseResult invalidHeaderResult);
        HttpRequestParseStatus invalidLengthStatus = HttpRequestParser.TryParse(
            invalidLength,
            out HttpRequestParseResult invalidLengthResult);
        HttpRequestParseStatus truncatedStatus = HttpRequestParser.TryParse(
            truncatedBody,
            out HttpRequestParseResult truncatedResult);
        HttpRequestParseStatus chunkedStatus = HttpRequestParser.TryParse(
            chunked,
            out HttpRequestParseResult chunkedResult);

        return invalidHeaderStatus == HttpRequestParseStatus.InvalidHeader
            && !invalidHeaderResult.Succeeded
            && invalidLengthStatus == HttpRequestParseStatus.InvalidContentLength
            && !invalidLengthResult.Succeeded
            && truncatedStatus == HttpRequestParseStatus.BodyTooShort
            && !truncatedResult.Succeeded
            && chunkedStatus == HttpRequestParseStatus.UnsupportedTransferEncoding
            && !chunkedResult.Succeeded;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        HttpRequestParseResult defaultResult = default;
        HttpRequestParseResult failedResult =
            HttpRequestParseResult.Fail(HttpRequestParseStatus.BufferTooSmall);

        return !defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == HttpRequestParseStatus.Success
            && defaultResult.Method == HttpRequestMethod.Unknown
            && failedResult.IsInitialized
            && !failedResult.Succeeded
            && failedResult.Status == HttpRequestParseStatus.BufferTooSmall
            && !RuntimeHelpers.IsReferenceOrContainsReferences<HttpRequestParseResult>();
    }

    private static bool TestUsesSharedMemoryIndexHelper()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string parserSource = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "OS.Kernel", "Net", "HttpRequestParser.cs"));

        return parserSource.Contains(
                "OS.Std.MemoryExtensions.IndexOf(bytes.Slice(start), \"\\r\\n\\r\\n\"u8)",
                StringComparison.Ordinal)
            && parserSource.Contains(
                "OS.Std.MemoryExtensions.IndexOf(bytes.Slice(start), \"\\r\\n\"u8)",
                StringComparison.Ordinal)
            && parserSource.Contains(
                "OS.Std.MemoryExtensions.IndexOf(bytes.Slice(start, endExclusive - start), value)",
                StringComparison.Ordinal)
            && !parserSource.Contains(
                "for (int index = start; index <= bytes.Length - 4; index++)",
                StringComparison.Ordinal)
            && !parserSource.Contains(
                "for (int index = start; index <= bytes.Length - 2; index++)",
                StringComparison.Ordinal)
            && !parserSource.Contains(
                "for (int index = start; index < endExclusive; index++)",
                StringComparison.Ordinal);
    }

    private static bool TestUsesSharedAsciiEqualityHelper()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string parserSource = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "OS.Kernel", "Net", "HttpRequestParser.cs"));

        return parserSource.Contains("using OS.Std.Text;", StringComparison.Ordinal)
            && parserSource.Contains("AsciiString.Equals(method, \"GET\"u8)", StringComparison.Ordinal)
            && parserSource.Contains("AsciiString.Equals(method, \"HEAD\"u8)", StringComparison.Ordinal)
            && parserSource.Contains("AsciiString.Equals(method, \"POST\"u8)", StringComparison.Ordinal)
            && parserSource.Contains("AsciiString.EqualsIgnoreCase(name, \"Host\"u8)", StringComparison.Ordinal)
            && parserSource.Contains("AsciiString.EqualsIgnoreCase(name, \"Content-Length\"u8)", StringComparison.Ordinal)
            && parserSource.Contains("AsciiString.EqualsIgnoreCase(name, \"Connection\"u8)", StringComparison.Ordinal)
            && parserSource.Contains("AsciiString.TrimSpaceOrTab(value)", StringComparison.Ordinal)
            && parserSource.Contains("AsciiString.EqualsIgnoreCase(semanticValue, \"close\"u8)", StringComparison.Ordinal)
            && parserSource.Contains("AsciiString.EqualsIgnoreCase(name, \"Transfer-Encoding\"u8)", StringComparison.Ordinal)
            && parserSource.Contains("AsciiString.EqualsIgnoreCase(semanticValue, \"identity\"u8)", StringComparison.Ordinal)
            && !parserSource.Contains("private static bool AsciiEquals(", StringComparison.Ordinal)
            && !parserSource.Contains("private static bool AsciiEqualsIgnoreCase(", StringComparison.Ordinal)
            && !parserSource.Contains("private static byte ToLowerAscii(", StringComparison.Ordinal);
    }

    private static bool TestUsesSharedMemoryProtocolPrefixHelper()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string parserSource = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "OS.Kernel", "Net", "HttpRequestParser.cs"));

        return parserSource.Contains("OS.Std.MemoryExtensions.StartsWith(protocol, \"HTTP/\"u8)", StringComparison.Ordinal)
            && parserSource.Contains("protocol.Length != 8", StringComparison.Ordinal)
            && parserSource.Contains("!OS.Std.Ascii.IsDigit(protocol[5])", StringComparison.Ordinal)
            && parserSource.Contains("protocol[6] != (byte)'.'", StringComparison.Ordinal)
            && parserSource.Contains("!OS.Std.Ascii.IsDigit(protocol[7])", StringComparison.Ordinal)
            && !parserSource.Contains("request[secondSpace + 1] != (byte)'H'", StringComparison.Ordinal)
            && !parserSource.Contains("request[secondSpace + 2] != (byte)'T'", StringComparison.Ordinal)
            && !parserSource.Contains("request[secondSpace + 3] != (byte)'T'", StringComparison.Ordinal)
            && !parserSource.Contains("request[secondSpace + 4] != (byte)'P'", StringComparison.Ordinal)
            && !parserSource.Contains("request[secondSpace + 5] != (byte)'/'", StringComparison.Ordinal);
    }

    private static bool TestUsesSharedAsciiTokenHelper()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string parserSource = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "OS.Kernel", "Net", "HttpRequestParser.cs"));

        return parserSource.Contains("OS.Std.Ascii.IsLetterOrDigit(value)", StringComparison.Ordinal)
            && parserSource.Contains("OS.Std.Ascii.IsDigit(c)", StringComparison.Ordinal)
            && !parserSource.Contains("private static bool IsDigit(", StringComparison.Ordinal)
            && !parserSource.Contains("(value >= (byte)'a' && value <= (byte)'z')", StringComparison.Ordinal)
            && !parserSource.Contains("(value >= (byte)'A' && value <= (byte)'Z')", StringComparison.Ordinal)
            && !parserSource.Contains("(value >= (byte)'0' && value <= (byte)'9')", StringComparison.Ordinal);
    }

    private static bool TestUsesSharedAsciiSpaceOrTabHelper()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string parserSource = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "OS.Kernel", "Net", "HttpRequestParser.cs"));

        return parserSource.Contains(
                "OS.Std.Ascii.IsSpaceOrTab(request[valueOffset])",
                StringComparison.Ordinal)
            && !parserSource.Contains(
                "request[valueOffset] == (byte)' ' || request[valueOffset] == (byte)'\\t'",
                StringComparison.Ordinal);
    }
}
