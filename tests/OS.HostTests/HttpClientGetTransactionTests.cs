using System.Runtime.CompilerServices;
using System.Text;
using OS.Net;

internal static class HttpClientGetTransactionTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("net.http client writes request and parses response", TestWritesRequestAndParsesResponse, ref failures);
        Run("net.http client rejects malformed response", TestRejectsMalformedResponse, ref failures);
        Run("net.http client rejects invalid state transitions", TestRejectsInvalidStateTransitions, ref failures);
        Run("net.http client rejects invalid request inputs", TestRejectsInvalidRequestInputs, ref failures);
        Run("net.http client default result is not success", TestDefaultResultIsNotSuccess, ref failures);
        Run("net.http.clientget formatter formats success marker", TestFormatterFormatsSuccessMarker, ref failures);
        Run("net.http.clientget formatter names duplicate content length", TestFormatterNamesDuplicateContentLength, ref failures);
        Run("net.http.clientget formatter reports short destination", TestFormatterReportsShortDestination, ref failures);
        Run("net.http.clientget formatter default result is not success", TestFormatterDefaultResultIsNotSuccess, ref failures);
        Run(
            "network http client transaction formatter uses shared ascii text builder",
            TestFormatterUsesSharedAsciiTextBuilder,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("HTTP client transaction host tests failed: " + failures);
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

    private static bool TestWritesRequestAndParsesResponse()
    {
        byte[] host = Encoding.ASCII.GetBytes("example.test");
        byte[] path = Encoding.ASCII.GetBytes("/healthz");
        byte[] request = new byte[256];
        byte[] response = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Length: 5\r\nConnection: close\r\n\r\nhello");
        HttpClientGetTransaction transaction = HttpClientGetTransaction.Create();

        HttpClientGetTransactionStatus requestStatus = transaction.TryWriteRequest(
            host,
            path,
            request,
            out HttpClientGetTransactionResult requestResult);
        if (requestStatus != HttpClientGetTransactionStatus.Success
            || !requestResult.Succeeded
            || requestResult.State != HttpClientGetTransactionState.RequestWritten
            || requestResult.RequestStatus != HttpRequestBuilderStatus.Success
            || requestResult.BytesWritten == 0
            || requestResult.BytesRequired != requestResult.BytesWritten
            || transaction.State != HttpClientGetTransactionState.RequestWritten
            || transaction.RequestBytesWritten != requestResult.BytesWritten)
        {
            return false;
        }

        string requestText = Encoding.ASCII.GetString(request, 0, requestResult.BytesWritten);
        if (requestText != "GET /healthz HTTP/1.1\r\n" +
            "Host: example.test\r\n" +
            "User-Agent: OS/0.1\r\n" +
            "Accept: */*\r\n" +
            "Connection: close\r\n" +
            "\r\n")
        {
            return false;
        }

        HttpClientGetTransactionStatus responseStatus = transaction.ReceiveResponse(
            response,
            out HttpClientGetTransactionResult responseResult);

        return responseStatus == HttpClientGetTransactionStatus.Success
            && responseResult.Succeeded
            && responseResult.State == HttpClientGetTransactionState.ResponseReceived
            && responseResult.ResponseStatus == HttpResponseParseStatus.Success
            && responseResult.ProtocolMajor == 1
            && responseResult.ProtocolMinor == 1
            && responseResult.StatusCode == 200
            && responseResult.HasContentLength
            && responseResult.ContentLength == 5
            && responseResult.ConnectionClose
            && responseResult.BodyLength == 5
            && response[responseResult.BodyOffset] == (byte)'h'
            && response[responseResult.BodyOffset + 4] == (byte)'o'
            && transaction.State == HttpClientGetTransactionState.ResponseReceived
            && transaction.StatusCode == 200
            && transaction.BodyOffset == responseResult.BodyOffset
            && transaction.BodyLength == responseResult.BodyLength
            && transaction.ConnectionClose;
    }

    private static bool TestRejectsMalformedResponse()
    {
        byte[] malformed = Encoding.ASCII.GetBytes("HTTP/1.1 20X OK\r\n\r\n");
        byte[] chunked = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n0\r\n\r\n");
        byte[] truncated = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Length: 8\r\n\r\nhello");

        return ReceiveFails(
                malformed,
                HttpClientGetTransactionStatus.MalformedResponse,
                HttpResponseParseStatus.InvalidStatusCode)
            && ReceiveFails(
                chunked,
                HttpClientGetTransactionStatus.UnsupportedTransferEncoding,
                HttpResponseParseStatus.UnsupportedTransferEncoding)
            && ReceiveFails(
                truncated,
                HttpClientGetTransactionStatus.TruncatedBody,
                HttpResponseParseStatus.BodyTooShort);
    }

    private static bool TestRejectsInvalidStateTransitions()
    {
        byte[] host = Encoding.ASCII.GetBytes("example.test");
        byte[] path = Encoding.ASCII.GetBytes("/healthz");
        byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n");
        byte[] request = new byte[256];
        HttpClientGetTransaction transaction = HttpClientGetTransaction.Create();

        HttpClientGetTransactionStatus earlyReceiveStatus = transaction.ReceiveResponse(
            response,
            out HttpClientGetTransactionResult earlyReceiveResult);
        if (earlyReceiveStatus != HttpClientGetTransactionStatus.InvalidState
            || earlyReceiveResult.State != HttpClientGetTransactionState.Initial
            || transaction.State != HttpClientGetTransactionState.Initial)
        {
            return false;
        }

        HttpClientGetTransactionStatus writeStatus = transaction.TryWriteRequest(
            host,
            path,
            request,
            out HttpClientGetTransactionResult _);
        if (writeStatus != HttpClientGetTransactionStatus.Success
            || transaction.State != HttpClientGetTransactionState.RequestWritten)
        {
            return false;
        }

        HttpClientGetTransactionStatus secondWriteStatus = transaction.TryWriteRequest(
            host,
            path,
            request,
            out HttpClientGetTransactionResult secondWriteResult);
        if (secondWriteStatus != HttpClientGetTransactionStatus.InvalidState
            || secondWriteResult.State != HttpClientGetTransactionState.RequestWritten
            || transaction.State != HttpClientGetTransactionState.RequestWritten)
        {
            return false;
        }

        HttpClientGetTransactionStatus responseStatus = transaction.ReceiveResponse(
            response,
            out HttpClientGetTransactionResult responseResult);
        if (responseStatus != HttpClientGetTransactionStatus.Success
            || !responseResult.Succeeded
            || transaction.State != HttpClientGetTransactionState.ResponseReceived)
        {
            return false;
        }

        HttpClientGetTransactionStatus secondReceiveStatus = transaction.ReceiveResponse(
            response,
            out HttpClientGetTransactionResult secondReceiveResult);

        return secondReceiveStatus == HttpClientGetTransactionStatus.InvalidState
            && secondReceiveResult.State == HttpClientGetTransactionState.ResponseReceived
            && transaction.State == HttpClientGetTransactionState.ResponseReceived;
    }

    private static bool TestRejectsInvalidRequestInputs()
    {
        byte[] host = Encoding.ASCII.GetBytes("example.test");
        byte[] invalidHost = Encoding.ASCII.GetBytes("bad host");
        byte[] path = Encoding.ASCII.GetBytes("/healthz");
        byte[] invalidPath = Encoding.ASCII.GetBytes("/healthz\r\nHost:evil");
        byte[] missingSlashPath = Encoding.ASCII.GetBytes("healthz");
        Span<byte> request = stackalloc byte[16];

        return WriteFails(
                invalidHost,
                path,
                request,
                HttpClientGetTransactionStatus.InvalidHost,
                HttpRequestBuilderStatus.InvalidHost)
            && WriteFails(
                host,
                invalidPath,
                request,
                HttpClientGetTransactionStatus.InvalidPath,
                HttpRequestBuilderStatus.InvalidPath)
            && WriteFails(
                host,
                missingSlashPath,
                request,
                HttpClientGetTransactionStatus.MissingLeadingSlash,
                HttpRequestBuilderStatus.MissingLeadingSlash)
            && WriteFails(
                host,
                path,
                request,
                HttpClientGetTransactionStatus.RequestBufferTooSmall,
                HttpRequestBuilderStatus.DestinationTooSmall);
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        HttpClientGetTransactionResult defaultResult = default;
        HttpClientGetTransactionResult invalidStateResult =
            HttpClientGetTransactionResult.InvalidState(HttpClientGetTransactionState.Initial);

        return !defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == HttpClientGetTransactionStatus.Success
            && invalidStateResult.IsInitialized
            && !invalidStateResult.Succeeded
            && invalidStateResult.Status == HttpClientGetTransactionStatus.InvalidState
            && invalidStateResult.State == HttpClientGetTransactionState.Initial
            && invalidStateResult.RequestStatus == HttpRequestBuilderStatus.Success
            && invalidStateResult.ResponseStatus == HttpResponseParseStatus.Success
            && invalidStateResult.BytesWritten == 0
            && invalidStateResult.BodyLength == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                HttpClientGetTransactionResult>();
    }

    private static bool TestFormatterFormatsSuccessMarker()
    {
        byte[] host = Encoding.ASCII.GetBytes("example.test");
        byte[] path = Encoding.ASCII.GetBytes("/healthz");
        byte[] request = new byte[256];
        byte[] response = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Length: 5\r\nConnection: close\r\n\r\nhello");
        Span<byte> destination = stackalloc byte[192];
        destination.Fill(0);
        HttpClientGetTransaction transaction = HttpClientGetTransaction.Create();

        if (transaction.TryWriteRequest(
                host,
                path,
                request,
                out HttpClientGetTransactionResult requestResult) != HttpClientGetTransactionStatus.Success)
        {
            return false;
        }

        if (transaction.ReceiveResponse(
                response,
                out HttpClientGetTransactionResult responseResult) != HttpClientGetTransactionStatus.Success)
        {
            return false;
        }

        HttpClientGetTransactionDiagnosticFormatResult query =
            HttpClientGetTransactionDiagnosticFormatter.QueryRequiredBytes(
                responseResult,
                transaction.RequestBytesWritten);
        HttpClientGetTransactionDiagnosticFormatResult format =
            HttpClientGetTransactionDiagnosticFormatter.FormatResult(
                responseResult,
                transaction.RequestBytesWritten,
                destination);
        string text = Encoding.ASCII.GetString(destination[..format.BytesWritten]);
        string expected = "HTTP_CLIENT_GET_TRANSACTION status=Success state=ResponseReceived request=Success response=Success requestBytes=" +
            requestResult.BytesWritten +
            " responseStatus=200 bodyBytes=5";

        return query.IsInitialized
            && !query.Succeeded
            && query.Status == HttpClientGetTransactionDiagnosticFormatStatus.Success
            && query.TransactionStatus == HttpClientGetTransactionStatus.Success
            && query.BytesWritten == 0
            && query.RequiredBytes == expected.Length
            && query.AvailableBytes == 0
            && format.Succeeded
            && format.TransactionStatus == HttpClientGetTransactionStatus.Success
            && format.BytesWritten == expected.Length
            && format.RequiredBytes == expected.Length
            && format.AvailableBytes == destination.Length
            && text == expected;
    }

    private static bool TestFormatterNamesDuplicateContentLength()
    {
        byte[] host = Encoding.ASCII.GetBytes("example.test");
        byte[] path = Encoding.ASCII.GetBytes("/healthz");
        byte[] request = new byte[256];
        byte[] response = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Length: 5\r\nContent-Length: 5\r\n\r\nhello");
        Span<byte> destination = stackalloc byte[256];
        destination.Fill(0);
        HttpClientGetTransaction transaction = HttpClientGetTransaction.Create();

        if (transaction.TryWriteRequest(
                host,
                path,
                request,
                out HttpClientGetTransactionResult requestResult) != HttpClientGetTransactionStatus.Success)
        {
            return false;
        }

        HttpClientGetTransactionStatus status = transaction.ReceiveResponse(
            response,
            out HttpClientGetTransactionResult responseResult);
        if (status != HttpClientGetTransactionStatus.MalformedResponse ||
            responseResult.ResponseStatus != HttpResponseParseStatus.DuplicateContentLength)
        {
            return false;
        }

        HttpClientGetTransactionDiagnosticFormatResult query =
            HttpClientGetTransactionDiagnosticFormatter.QueryRequiredBytes(
                responseResult,
                transaction.RequestBytesWritten);
        HttpClientGetTransactionDiagnosticFormatResult format =
            HttpClientGetTransactionDiagnosticFormatter.FormatResult(
                responseResult,
                transaction.RequestBytesWritten,
                destination);
        string text = Encoding.ASCII.GetString(destination[..format.BytesWritten]);
        string expected =
            "HTTP_CLIENT_GET_TRANSACTION status=MalformedResponse state=Failed request=Success " +
            "response=DuplicateContentLength requestBytes=" +
            requestResult.BytesWritten +
            " responseStatus=0 bodyBytes=0";

        return query.IsInitialized
            && !query.Succeeded
            && query.Status == HttpClientGetTransactionDiagnosticFormatStatus.Success
            && query.TransactionStatus == HttpClientGetTransactionStatus.MalformedResponse
            && query.RequiredBytes == expected.Length
            && format.Succeeded
            && format.TransactionStatus == HttpClientGetTransactionStatus.MalformedResponse
            && format.BytesWritten == expected.Length
            && text == expected;
    }

    private static bool TestFormatterReportsShortDestination()
    {
        byte[] host = Encoding.ASCII.GetBytes("example.test");
        byte[] path = Encoding.ASCII.GetBytes("/healthz");
        byte[] request = new byte[256];
        byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 204 No Content\r\nContent-Length: 0\r\n\r\n");
        Span<byte> destination = stackalloc byte[12];
        destination.Fill(0xA5);
        HttpClientGetTransaction transaction = HttpClientGetTransaction.Create();

        if (transaction.TryWriteRequest(
                host,
                path,
                request,
                out HttpClientGetTransactionResult _) != HttpClientGetTransactionStatus.Success)
        {
            return false;
        }

        if (transaction.ReceiveResponse(
                response,
                out HttpClientGetTransactionResult responseResult) != HttpClientGetTransactionStatus.Success)
        {
            return false;
        }

        HttpClientGetTransactionDiagnosticFormatResult query =
            HttpClientGetTransactionDiagnosticFormatter.QueryRequiredBytes(
                responseResult,
                transaction.RequestBytesWritten);
        HttpClientGetTransactionDiagnosticFormatResult shortResult =
            HttpClientGetTransactionDiagnosticFormatter.FormatResult(
                responseResult,
                transaction.RequestBytesWritten,
                destination);

        return shortResult.IsInitialized
            && !shortResult.Succeeded
            && shortResult.Status == HttpClientGetTransactionDiagnosticFormatStatus.DestinationTooSmall
            && shortResult.TransactionStatus == HttpClientGetTransactionStatus.Success
            && shortResult.BytesWritten == 0
            && shortResult.RequiredBytes == query.RequiredBytes
            && shortResult.AvailableBytes == destination.Length
            && query.RequiredBytes > destination.Length
            && destination[0] == 0xA5
            && destination[11] == 0xA5;
    }

    private static bool TestFormatterDefaultResultIsNotSuccess()
    {
        Span<byte> destination = stackalloc byte[64];
        destination.Fill(0xCC);

        HttpClientGetTransactionResult defaultTransactionResult = default;
        HttpClientGetTransactionDiagnosticFormatResult invalidFormat =
            HttpClientGetTransactionDiagnosticFormatter.FormatResult(
                defaultTransactionResult,
                requestBytesWritten: 0,
                destination);
        HttpClientGetTransactionDiagnosticFormatResult invalidQuery =
            HttpClientGetTransactionDiagnosticFormatter.QueryRequiredBytes(
                defaultTransactionResult,
                requestBytesWritten: 0);
        HttpClientGetTransactionDiagnosticFormatResult defaultFormat = default;

        return invalidFormat.IsInitialized
            && !invalidFormat.Succeeded
            && invalidFormat.Status == HttpClientGetTransactionDiagnosticFormatStatus.InvalidResult
            && invalidFormat.TransactionStatus == HttpClientGetTransactionStatus.Success
            && invalidFormat.BytesWritten == 0
            && invalidFormat.RequiredBytes == 0
            && invalidFormat.AvailableBytes == destination.Length
            && invalidQuery.IsInitialized
            && !invalidQuery.Succeeded
            && invalidQuery.Status == HttpClientGetTransactionDiagnosticFormatStatus.InvalidResult
            && invalidQuery.RequiredBytes == 0
            && !defaultFormat.IsInitialized
            && !defaultFormat.Succeeded
            && defaultFormat.Status == HttpClientGetTransactionDiagnosticFormatStatus.Success
            && defaultFormat.TransactionStatus == HttpClientGetTransactionStatus.Success
            && destination[0] == 0xCC
            && destination[63] == 0xCC
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                HttpClientGetTransactionDiagnosticFormatResult>();
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
                "HttpClientGetTransactionDiagnosticFormatter.cs"));

        return source.Contains("using OS.Std.Text;", StringComparison.Ordinal)
            && source.Contains(
                "AsciiDecimalFormatter.CountInt64Digits(requestBytesWritten)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiDecimalFormatter.CountUInt64Digits(result.StatusCode)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiDecimalFormatter.CountInt64Digits(result.BodyLength)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiTextBuilder.AppendBytes(destination, offset, value)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiTextBuilder.AppendInt64Decimal(destination, offset, value)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiTextBuilder.AppendUInt64Decimal(destination, offset, value)",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static int GetInt64DecimalByteCount",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static int GetUInt64DecimalByteCount",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static void Append(",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static void AppendInt64",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static void AppendUInt64",
                StringComparison.Ordinal)
            && !source.Contains(
                "destination[offset + index] = (byte)('0' +",
                StringComparison.Ordinal)
            && !source.Contains("value /= 10", StringComparison.Ordinal)
            && !source.Contains("value.CopyTo(destination[offset..])", StringComparison.Ordinal);
    }

    private static bool WriteFails(
        ReadOnlySpan<byte> host,
        ReadOnlySpan<byte> path,
        Span<byte> request,
        HttpClientGetTransactionStatus expectedStatus,
        HttpRequestBuilderStatus expectedRequestStatus)
    {
        HttpClientGetTransaction transaction = HttpClientGetTransaction.Create();
        request.Fill(0xcc);

        HttpClientGetTransactionStatus status = transaction.TryWriteRequest(
            host,
            path,
            request,
            out HttpClientGetTransactionResult result);

        bool requestUnchanged = true;
        for (int index = 0; index < request.Length; index++)
        {
            requestUnchanged &= request[index] == 0xcc;
        }

        return status == expectedStatus
            && !result.Succeeded
            && result.State == HttpClientGetTransactionState.Initial
            && result.RequestStatus == expectedRequestStatus
            && transaction.State == HttpClientGetTransactionState.Initial
            && requestUnchanged;
    }

    private static bool ReceiveFails(
        ReadOnlySpan<byte> response,
        HttpClientGetTransactionStatus expectedStatus,
        HttpResponseParseStatus expectedResponseStatus)
    {
        byte[] host = Encoding.ASCII.GetBytes("example.test");
        byte[] path = Encoding.ASCII.GetBytes("/healthz");
        Span<byte> request = stackalloc byte[256];
        HttpClientGetTransaction transaction = HttpClientGetTransaction.Create();

        if (transaction.TryWriteRequest(
                host,
                path,
                request,
                out HttpClientGetTransactionResult _) != HttpClientGetTransactionStatus.Success)
        {
            return false;
        }

        HttpClientGetTransactionStatus status = transaction.ReceiveResponse(
            response,
            out HttpClientGetTransactionResult result);

        return status == expectedStatus
            && !result.Succeeded
            && result.State == HttpClientGetTransactionState.Failed
            && result.ResponseStatus == expectedResponseStatus
            && transaction.State == HttpClientGetTransactionState.Failed;
    }
}
