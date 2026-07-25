using System.Runtime.CompilerServices;
using System.Text;
using OS.Net;

internal static class HttpStaticResponseTransactionTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("net.http.static serves matched get", TestServesMatchedGet, ref failures);
        Run("net.http.static writes not found response", TestWritesNotFoundResponse, ref failures);
        Run("net.http.static writes method not allowed response", TestWritesMethodNotAllowedResponse, ref failures);
        Run("net.http.static rejects malformed request and route", TestRejectsMalformedRequestAndRoute, ref failures);
        Run("net.http.static reports short response buffer", TestReportsShortResponseBuffer, ref failures);
        Run("net.http.static default result is not success", TestDefaultResultIsNotSuccess, ref failures);
        Run("net.http.static.formatter formats 200 marker", TestFormatterFormats200Marker, ref failures);
        Run("net.http.static.formatter formats 404 and 405 metadata", TestFormatterFormats404And405Metadata, ref failures);
        Run("net.http.static.formatter reports destination capacity", TestFormatterReportsDestinationCapacity, ref failures);
        Run("net.http.static.formatter rejects default and invalid results", TestFormatterRejectsDefaultAndInvalidResults, ref failures);
        Run("net.http.static.formatter uses shared ascii text builder", TestFormatterUsesSharedAsciiTextBuilder, ref failures);
        Run("net.http.static uses shared memory equality helper", TestUsesSharedMemoryEqualityHelper, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("HTTP static response transaction host tests failed: " + failures);
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

    private static bool TestServesMatchedGet()
    {
        byte[] request = Encoding.ASCII.GetBytes(
            "GET /healthz HTTP/1.1\r\nHost: os.test\r\nConnection: close\r\n\r\n");
        byte[] response = new byte[256];

        HttpStaticResponseTransactionStatus status = HttpStaticResponseTransaction.TryHandle(
            request,
            Encoding.ASCII.GetBytes("/healthz"),
            Encoding.ASCII.GetBytes("OS_MINIMAL_API_OK"),
            Encoding.ASCII.GetBytes("missing"),
            Encoding.ASCII.GetBytes("nope"),
            Encoding.ASCII.GetBytes("text/plain"),
            response,
            out HttpStaticResponseTransactionResult result);

        string text = Encoding.ASCII.GetString(response, 0, result.BytesWritten);

        return status == HttpStaticResponseTransactionStatus.Success
            && result.Succeeded
            && result.RequestStatus == HttpRequestParseStatus.Success
            && result.ResponseStatus == HttpResponseWriterStatus.Success
            && result.Method == HttpRequestMethod.Get
            && result.StatusCode == 200
            && result.TargetMatched
            && result.MethodAllowed
            && result.ConnectionClose
            && result.BodyLength == "OS_MINIMAL_API_OK".Length
            && text ==
                "HTTP/1.1 200 OK\r\n" +
                "Content-Length: 17\r\n" +
                "Content-Type: text/plain\r\n" +
                "Connection: close\r\n" +
                "\r\n" +
                "OS_MINIMAL_API_OK";
    }

    private static bool TestWritesNotFoundResponse()
    {
        byte[] request = Encoding.ASCII.GetBytes(
            "GET /missing HTTP/1.1\r\nHost: os.test\r\n\r\n");
        byte[] response = new byte[256];

        HttpStaticResponseTransactionStatus status = HttpStaticResponseTransaction.TryHandle(
            request,
            Encoding.ASCII.GetBytes("/healthz"),
            Encoding.ASCII.GetBytes("OK"),
            Encoding.ASCII.GetBytes("not found"),
            Encoding.ASCII.GetBytes("bad method"),
            Encoding.ASCII.GetBytes("text/plain"),
            response,
            out HttpStaticResponseTransactionResult result);

        string text = Encoding.ASCII.GetString(response, 0, result.BytesWritten);

        return status == HttpStaticResponseTransactionStatus.Success
            && result.Succeeded
            && result.StatusCode == 404
            && !result.TargetMatched
            && result.MethodAllowed
            && result.ConnectionClose
            && result.BodyLength == "not found".Length
            && text.Contains("HTTP/1.1 404 Not Found\r\n", StringComparison.Ordinal)
            && text.EndsWith("not found", StringComparison.Ordinal);
    }

    private static bool TestWritesMethodNotAllowedResponse()
    {
        byte[] request = Encoding.ASCII.GetBytes(
            "POST /healthz HTTP/1.1\r\nHost: os.test\r\nContent-Length: 0\r\n\r\n");
        byte[] response = new byte[256];

        HttpStaticResponseTransactionStatus status = HttpStaticResponseTransaction.TryHandle(
            request,
            Encoding.ASCII.GetBytes("/healthz"),
            Encoding.ASCII.GetBytes("OK"),
            Encoding.ASCII.GetBytes("missing"),
            Encoding.ASCII.GetBytes("bad method"),
            Encoding.ASCII.GetBytes("text/plain"),
            response,
            out HttpStaticResponseTransactionResult result);

        string text = Encoding.ASCII.GetString(response, 0, result.BytesWritten);

        return status == HttpStaticResponseTransactionStatus.Success
            && result.Succeeded
            && result.Method == HttpRequestMethod.Post
            && result.StatusCode == 405
            && !result.TargetMatched
            && !result.MethodAllowed
            && result.ConnectionClose
            && result.BodyLength == "bad method".Length
            && text.Contains("HTTP/1.1 405 Method Not Allowed\r\n", StringComparison.Ordinal)
            && text.EndsWith("bad method", StringComparison.Ordinal);
    }

    private static bool TestRejectsMalformedRequestAndRoute()
    {
        byte[] malformedRequest = Encoding.ASCII.GetBytes("GET /healthz HTTP/1.1");
        Span<byte> response = stackalloc byte[128];
        response.Fill(0xcc);

        HttpStaticResponseTransactionStatus requestStatus = HttpStaticResponseTransaction.TryHandle(
            malformedRequest,
            Encoding.ASCII.GetBytes("/healthz"),
            Encoding.ASCII.GetBytes("OK"),
            Encoding.ASCII.GetBytes("missing"),
            Encoding.ASCII.GetBytes("bad method"),
            Encoding.ASCII.GetBytes("text/plain"),
            response,
            out HttpStaticResponseTransactionResult requestResult);

        bool responseUnchanged = AllBytesEqual(response, 0xcc);

        HttpStaticResponseTransactionStatus routeStatus = HttpStaticResponseTransaction.TryHandle(
            Encoding.ASCII.GetBytes("GET /healthz HTTP/1.1\r\n\r\n"),
            Encoding.ASCII.GetBytes("healthz"),
            Encoding.ASCII.GetBytes("OK"),
            Encoding.ASCII.GetBytes("missing"),
            Encoding.ASCII.GetBytes("bad method"),
            Encoding.ASCII.GetBytes("text/plain"),
            response,
            out HttpStaticResponseTransactionResult routeResult);

        return requestStatus == HttpStaticResponseTransactionStatus.RequestParseFailed
            && !requestResult.Succeeded
            && requestResult.RequestStatus == HttpRequestParseStatus.MissingRequestLineTerminator
            && requestResult.BytesWritten == 0
            && responseUnchanged
            && routeStatus == HttpStaticResponseTransactionStatus.InvalidRoute
            && !routeResult.Succeeded
            && routeResult.BytesWritten == 0
            && routeResult.StatusCode == 0;
    }

    private static bool TestReportsShortResponseBuffer()
    {
        byte[] request = Encoding.ASCII.GetBytes(
            "GET /healthz HTTP/1.1\r\nHost: os.test\r\n\r\n");
        Span<byte> response = stackalloc byte[16];
        response.Fill(0xcc);

        HttpStaticResponseTransactionStatus status = HttpStaticResponseTransaction.TryHandle(
            request,
            Encoding.ASCII.GetBytes("/healthz"),
            Encoding.ASCII.GetBytes("OS_MINIMAL_API_OK"),
            Encoding.ASCII.GetBytes("missing"),
            Encoding.ASCII.GetBytes("bad method"),
            Encoding.ASCII.GetBytes("text/plain"),
            response,
            out HttpStaticResponseTransactionResult result);

        return status == HttpStaticResponseTransactionStatus.ResponseWriteFailed
            && !result.Succeeded
            && result.RequestStatus == HttpRequestParseStatus.Success
            && result.ResponseStatus == HttpResponseWriterStatus.DestinationTooSmall
            && result.StatusCode == 200
            && result.TargetMatched
            && result.MethodAllowed
            && result.BytesWritten == 0
            && result.BytesRequired > response.Length
            && AllBytesEqual(response, 0xcc);
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        HttpStaticResponseTransactionResult defaultResult = default;
        HttpStaticResponseTransactionResult failedResult =
            HttpStaticResponseTransactionResult.Fail(
                HttpStaticResponseTransactionStatus.ResponseWriteFailed,
                HttpRequestParseStatus.Success,
                HttpResponseWriterStatus.DestinationTooSmall,
                HttpRequestMethod.Get,
                200,
                targetMatched: true,
                methodAllowed: true,
                connectionClose: true,
                requestTargetOffset: 4,
                requestTargetLength: 8,
                bytesRequired: 64);

        return !defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == HttpStaticResponseTransactionStatus.Success
            && defaultResult.StatusCode == 0
            && failedResult.IsInitialized
            && !failedResult.Succeeded
            && failedResult.Status == HttpStaticResponseTransactionStatus.ResponseWriteFailed
            && failedResult.ResponseStatus == HttpResponseWriterStatus.DestinationTooSmall
            && failedResult.BytesRequired == 64
            && !RuntimeHelpers.IsReferenceOrContainsReferences<HttpStaticResponseTransactionResult>();
    }

    private static bool TestFormatterFormats200Marker()
    {
        HttpStaticResponseTransactionResult transaction = CreateTransaction(
            "GET /healthz HTTP/1.1\r\nHost: os.test\r\nConnection: close\r\n\r\n",
            "OS_MINIMAL_API_OK",
            "missing",
            "bad method",
            out _);

        HttpStaticResponseTransactionDiagnosticFormatResult query =
            HttpStaticResponseTransactionDiagnosticFormatter.QueryRequiredBytes(transaction);
        byte[] destination = new byte[query.RequiredBytes];

        HttpStaticResponseTransactionDiagnosticFormatResult format =
            HttpStaticResponseTransactionDiagnosticFormatter.FormatResult(transaction, destination);
        string marker = Encoding.ASCII.GetString(destination, 0, format.BytesWritten);

        string expected =
            "HTTP_STATIC_RESPONSE status=Success" +
            " request=Success" +
            " response=Success" +
            " method=Get" +
            " httpStatus=200" +
            " targetMatched=true" +
            " methodAllowed=true" +
            " connectionClose=true" +
            " targetBytes=8" +
            " bytesWritten=" + transaction.BytesWritten +
            " bytesRequired=" + transaction.BytesRequired +
            " bodyOffset=" + transaction.BodyOffset +
            " bodyBytes=17" +
            " liveSocket=false" +
            " startsKestrel=false";

        return query.Status == HttpStaticResponseTransactionDiagnosticFormatStatus.Success
            && query.BytesWritten == 0
            && query.RequiredBytes > 0
            && format.Succeeded
            && format.BytesWritten == query.RequiredBytes
            && format.RequiredBytes == query.RequiredBytes
            && format.TransactionStatus == HttpStaticResponseTransactionStatus.Success
            && marker == expected
            && !marker.Contains("OS_MINIMAL_API_OK", StringComparison.Ordinal)
            && !RuntimeHelpers.IsReferenceOrContainsReferences<HttpStaticResponseTransactionDiagnosticFormatResult>();
    }

    private static bool TestFormatterFormats404And405Metadata()
    {
        HttpStaticResponseTransactionResult notFound = CreateTransaction(
            "GET /missing HTTP/1.1\r\nHost: os.test\r\n\r\n",
            "OK",
            "not found",
            "bad method",
            out _);
        string notFoundMarker = FormatTransactionMarker(notFound);

        HttpStaticResponseTransactionResult methodNotAllowed = CreateTransaction(
            "POST /healthz HTTP/1.1\r\nHost: os.test\r\nContent-Length: 0\r\n\r\n",
            "OK",
            "missing",
            "bad method",
            out _);
        string methodMarker = FormatTransactionMarker(methodNotAllowed);

        return notFound.StatusCode == 404
            && methodNotAllowed.StatusCode == 405
            && notFoundMarker.Contains("httpStatus=404", StringComparison.Ordinal)
            && notFoundMarker.Contains("method=Get", StringComparison.Ordinal)
            && notFoundMarker.Contains("targetMatched=false", StringComparison.Ordinal)
            && notFoundMarker.Contains("methodAllowed=true", StringComparison.Ordinal)
            && notFoundMarker.Contains("connectionClose=true", StringComparison.Ordinal)
            && notFoundMarker.Contains("bodyBytes=9", StringComparison.Ordinal)
            && methodMarker.Contains("httpStatus=405", StringComparison.Ordinal)
            && methodMarker.Contains("method=Post", StringComparison.Ordinal)
            && methodMarker.Contains("targetMatched=false", StringComparison.Ordinal)
            && methodMarker.Contains("methodAllowed=false", StringComparison.Ordinal)
            && methodMarker.Contains("connectionClose=true", StringComparison.Ordinal)
            && methodMarker.Contains("bodyBytes=10", StringComparison.Ordinal)
            && methodMarker.Contains("liveSocket=false", StringComparison.Ordinal)
            && methodMarker.Contains("startsKestrel=false", StringComparison.Ordinal)
            && !notFoundMarker.Contains("not found", StringComparison.Ordinal)
            && !methodMarker.Contains("bad method", StringComparison.Ordinal);
    }

    private static bool TestFormatterReportsDestinationCapacity()
    {
        HttpStaticResponseTransactionResult transaction = CreateTransaction(
            "GET /healthz HTTP/1.1\r\nHost: os.test\r\n\r\n",
            "OK",
            "missing",
            "bad method",
            out _);
        HttpStaticResponseTransactionDiagnosticFormatResult query =
            HttpStaticResponseTransactionDiagnosticFormatter.QueryRequiredBytes(transaction);

        Span<byte> destination = stackalloc byte[query.RequiredBytes - 1];
        destination.Fill(0xcc);
        HttpStaticResponseTransactionDiagnosticFormatResult format =
            HttpStaticResponseTransactionDiagnosticFormatter.FormatResult(transaction, destination);

        return query.Status == HttpStaticResponseTransactionDiagnosticFormatStatus.Success
            && query.RequiredBytes > 0
            && format.Status == HttpStaticResponseTransactionDiagnosticFormatStatus.DestinationTooSmall
            && !format.Succeeded
            && format.BytesWritten == 0
            && format.RequiredBytes == query.RequiredBytes
            && format.AvailableBytes == destination.Length
            && AllBytesEqual(destination, 0xcc);
    }

    private static bool TestFormatterRejectsDefaultAndInvalidResults()
    {
        HttpStaticResponseTransactionResult defaultResult = default;
        HttpStaticResponseTransactionDiagnosticFormatResult defaultFormat =
            HttpStaticResponseTransactionDiagnosticFormatter.FormatResult(defaultResult, stackalloc byte[128]);
        HttpStaticResponseTransactionDiagnosticFormatResult defaultQuery =
            HttpStaticResponseTransactionDiagnosticFormatter.QueryRequiredBytes(defaultResult);

        HttpStaticResponseTransactionResult invalidStatus =
            HttpStaticResponseTransactionResult.Fail(
                (HttpStaticResponseTransactionStatus)99,
                HttpRequestParseStatus.Success,
                HttpResponseWriterStatus.Success,
                HttpRequestMethod.Get,
                200,
                targetMatched: true,
                methodAllowed: true,
                connectionClose: true,
                requestTargetOffset: 4,
                requestTargetLength: 8,
                bytesRequired: 64);
        HttpStaticResponseTransactionDiagnosticFormatResult invalidFormat =
            HttpStaticResponseTransactionDiagnosticFormatter.FormatResult(invalidStatus, stackalloc byte[128]);

        HttpStaticResponseTransactionResult invalidNested =
            HttpStaticResponseTransactionResult.Fail(
                HttpStaticResponseTransactionStatus.ResponseWriteFailed,
                (HttpRequestParseStatus)99,
                HttpResponseWriterStatus.DestinationTooSmall,
                HttpRequestMethod.Get,
                200,
                targetMatched: true,
                methodAllowed: true,
                connectionClose: true,
                requestTargetOffset: 4,
                requestTargetLength: 8,
                bytesRequired: 64);
        HttpStaticResponseTransactionDiagnosticFormatResult invalidNestedFormat =
            HttpStaticResponseTransactionDiagnosticFormatter.FormatResult(invalidNested, stackalloc byte[128]);

        return defaultFormat.Status == HttpStaticResponseTransactionDiagnosticFormatStatus.InvalidResult
            && defaultFormat.RequiredBytes == 0
            && defaultQuery.Status == HttpStaticResponseTransactionDiagnosticFormatStatus.InvalidResult
            && invalidFormat.Status == HttpStaticResponseTransactionDiagnosticFormatStatus.InvalidResult
            && invalidFormat.TransactionStatus == (HttpStaticResponseTransactionStatus)99
            && invalidNestedFormat.Status == HttpStaticResponseTransactionDiagnosticFormatStatus.InvalidResult
            && !defaultFormat.Succeeded
            && !invalidFormat.Succeeded
            && !invalidNestedFormat.Succeeded;
    }

    private static bool TestFormatterUsesSharedAsciiTextBuilder()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "OS.Kernel",
                "Net",
                "HttpStaticResponseTransactionDiagnosticFormatter.cs"));

        return source.Contains("AsciiTextBuilder.AppendBytes(", StringComparison.Ordinal)
            && source.Contains("AsciiTextBuilder.AppendInt64Decimal(", StringComparison.Ordinal)
            && source.Contains("AsciiTextBuilder.AppendUInt64Decimal(", StringComparison.Ordinal)
            && source.Contains("AsciiDecimalFormatter.CountInt32Digits(", StringComparison.Ordinal)
            && source.Contains("AsciiDecimalFormatter.CountUInt32Digits(", StringComparison.Ordinal)
            && !source.Contains("private static int GetInt64DecimalByteCount", StringComparison.Ordinal)
            && !source.Contains("private static int GetUInt64DecimalByteCount", StringComparison.Ordinal)
            && !source.Contains("while (value >= 10", StringComparison.Ordinal)
            && !source.Contains("value /= 10", StringComparison.Ordinal)
            && !source.Contains("value % 10", StringComparison.Ordinal);
    }

    private static bool TestUsesSharedMemoryEqualityHelper()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "OS.Kernel", "Net", "HttpStaticResponseTransaction.cs"));

        return source.Contains(
                "OS.Std.MemoryExtensions.SequenceEqual(",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static bool SequenceEqual(",
                StringComparison.Ordinal);
    }

    private static HttpStaticResponseTransactionResult CreateTransaction(
        string requestText,
        string successBody,
        string notFoundBody,
        string methodNotAllowedBody,
        out byte[] response)
    {
        response = new byte[256];
        HttpStaticResponseTransaction.TryHandle(
            Encoding.ASCII.GetBytes(requestText),
            Encoding.ASCII.GetBytes("/healthz"),
            Encoding.ASCII.GetBytes(successBody),
            Encoding.ASCII.GetBytes(notFoundBody),
            Encoding.ASCII.GetBytes(methodNotAllowedBody),
            Encoding.ASCII.GetBytes("text/plain"),
            response,
            out HttpStaticResponseTransactionResult result);

        return result;
    }

    private static string FormatTransactionMarker(
        in HttpStaticResponseTransactionResult transaction)
    {
        HttpStaticResponseTransactionDiagnosticFormatResult query =
            HttpStaticResponseTransactionDiagnosticFormatter.QueryRequiredBytes(transaction);
        byte[] destination = new byte[query.RequiredBytes];
        HttpStaticResponseTransactionDiagnosticFormatResult format =
            HttpStaticResponseTransactionDiagnosticFormatter.FormatResult(transaction, destination);

        return Encoding.ASCII.GetString(destination, 0, format.BytesWritten);
    }

    private static bool AllBytesEqual(ReadOnlySpan<byte> value, byte expected)
    {
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] != expected)
            {
                return false;
            }
        }

        return true;
    }
}
