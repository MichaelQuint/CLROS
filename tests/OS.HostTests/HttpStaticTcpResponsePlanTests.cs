using System.Runtime.CompilerServices;
using System.Text;
using OS.Net;

internal static class HttpStaticTcpResponsePlanTests
{
    private const int TcpSegmentOffset = 32;
    private const ushort LocalPort = 8080;
    private const ushort RemotePort = 49152;
    private const uint LocalNextSequence = 0x10203040;
    private const uint RemoteNextSequence = 0x50607080;

    [HostTest("net.http.static.tcp serves accepted get", HostLane.Fast, 0)]
    private static bool TestServesAcceptedGet()
    {
        byte[] request = Encoding.ASCII.GetBytes(
            "GET /healthz HTTP/1.1\r\nHost: os.test\r\nConnection: close\r\n\r\n");
        byte[] receive = CreateReceiveBuffer(request);
        TcpReceiveDispatchResult dispatch = CreatePayloadDispatch(request.Length);
        byte[] response = new byte[256];

        HttpStaticTcpResponsePlanStatus status = HttpStaticTcpResponsePlan.TryHandle(
            receive,
            dispatch,
            "/healthz"u8,
            "OS_MINIMAL_API_OK"u8,
            "missing"u8,
            "bad method"u8,
            "text/plain"u8,
            response,
            out HttpStaticTcpResponsePlanResult result);

        string text = Encoding.ASCII.GetString(response, 0, result.ResponseBytesWritten);

        return HostCheck.Result(status == HttpStaticTcpResponsePlanStatus.Success
            && result.Succeeded
            && result.TcpDispatchStatus == TcpReceiveDispatchStatus.Success
            && result.TcpAction == TcpReceiveDispatchAction.PayloadAccepted
            && result.HttpStatus == HttpStaticResponseTransactionStatus.Success
            && result.RequestStatus == HttpRequestParseStatus.Success
            && result.ResponseStatus == HttpResponseWriterStatus.Success
            && result.Method == HttpRequestMethod.Get
            && result.HttpStatusCode == 200
            && result.RequestPayloadOffset == TcpSegmentOffset + TcpHeader.MinHeaderLength
            && result.RequestPayloadLength == request.Length
            && result.ResponseBodyLength == "OS_MINIMAL_API_OK".Length
            && text.EndsWith("OS_MINIMAL_API_OK", StringComparison.Ordinal));
    }

    [HostTest("net.http.static.tcp maps route miss to not found", HostLane.Fast, 1)]
    private static bool TestMapsRouteMissToNotFound()
    {
        byte[] request = Encoding.ASCII.GetBytes("GET /missing HTTP/1.1\r\nHost: os.test\r\n\r\n");
        byte[] receive = CreateReceiveBuffer(request);
        TcpReceiveDispatchResult dispatch = CreatePayloadDispatch(request.Length);
        byte[] response = new byte[256];

        HttpStaticTcpResponsePlanStatus status = HttpStaticTcpResponsePlan.TryHandle(
            receive,
            dispatch,
            "/healthz"u8,
            "OK"u8,
            "not found"u8,
            "bad method"u8,
            "text/plain"u8,
            response,
            out HttpStaticTcpResponsePlanResult result);

        string text = Encoding.ASCII.GetString(response, 0, result.ResponseBytesWritten);

        return HostCheck.Result(status == HttpStaticTcpResponsePlanStatus.Success
            && result.Succeeded
            && result.HttpStatusCode == 404
            && result.Method == HttpRequestMethod.Get
            && result.ResponseBodyLength == "not found".Length
            && text.Contains("HTTP/1.1 404 Not Found\r\n", StringComparison.Ordinal)
            && text.EndsWith("not found", StringComparison.Ordinal));
    }

    [HostTest("net.http.static.tcp maps non get to method not allowed", HostLane.Fast, 2)]
    private static bool TestMapsNonGetToMethodNotAllowed()
    {
        byte[] request = Encoding.ASCII.GetBytes(
            "POST /healthz HTTP/1.1\r\nHost: os.test\r\nContent-Length: 0\r\n\r\n");
        byte[] receive = CreateReceiveBuffer(request);
        TcpReceiveDispatchResult dispatch = CreatePayloadDispatch(request.Length);
        byte[] response = new byte[256];

        HttpStaticTcpResponsePlanStatus status = HttpStaticTcpResponsePlan.TryHandle(
            receive,
            dispatch,
            "/healthz"u8,
            "OK"u8,
            "missing"u8,
            "bad method"u8,
            "text/plain"u8,
            response,
            out HttpStaticTcpResponsePlanResult result);

        string text = Encoding.ASCII.GetString(response, 0, result.ResponseBytesWritten);

        return HostCheck.Result(status == HttpStaticTcpResponsePlanStatus.Success
            && result.Succeeded
            && result.HttpStatusCode == 405
            && result.Method == HttpRequestMethod.Post
            && result.ResponseBodyLength == "bad method".Length
            && text.Contains("HTTP/1.1 405 Method Not Allowed\r\n", StringComparison.Ordinal)
            && text.EndsWith("bad method", StringComparison.Ordinal));
    }

    [HostTest("net.http.static.tcp rejects default tcp dispatch", HostLane.Fast, 3)]
    private static bool TestRejectsDefaultTcpDispatch()
    {
        Span<byte> response = stackalloc byte[64];
        response.Fill(0xCC);

        HttpStaticTcpResponsePlanStatus status = HttpStaticTcpResponsePlan.TryHandle(
            ReadOnlySpan<byte>.Empty,
            default,
            "/healthz"u8,
            "OK"u8,
            "missing"u8,
            "bad method"u8,
            "text/plain"u8,
            response,
            out HttpStaticTcpResponsePlanResult result);

        return HostCheck.Result(status == HttpStaticTcpResponsePlanStatus.TcpDispatchNotInitialized
            && !result.Succeeded
            && result.TcpDispatchStatus == TcpReceiveDispatchStatus.Success
            && result.RequestPayloadLength == 0
            && result.ResponseBytesWritten == 0
            && response[0] == 0xCC
            && response[63] == 0xCC);
    }

    [HostTest("net.http.static.tcp rejects non payload dispatch", HostLane.Fast, 4)]
    private static bool TestRejectsNonPayloadDispatch()
    {
        Span<byte> response = stackalloc byte[64];
        response.Fill(0xCC);
        TcpReceiveDispatchResult dispatch = TcpReceiveDispatchResult.Success(
            TcpReceiveDispatchAction.None,
            default,
            CreateTcpHeader(),
            TcpSegmentOffset,
            TcpHeader.MinHeaderLength,
            payloadBytes: 0,
            ackBytesWritten: 0,
            TcpConnectionCloseState.Established,
            LocalNextSequence,
            RemoteNextSequence);

        HttpStaticTcpResponsePlanStatus status = HttpStaticTcpResponsePlan.TryHandle(
            CreateReceiveBuffer(ReadOnlySpan<byte>.Empty),
            dispatch,
            "/healthz"u8,
            "OK"u8,
            "missing"u8,
            "bad method"u8,
            "text/plain"u8,
            response,
            out HttpStaticTcpResponsePlanResult result);

        return HostCheck.Result(status == HttpStaticTcpResponsePlanStatus.NonPayloadDispatch
            && !result.Succeeded
            && result.TcpAction == TcpReceiveDispatchAction.None
            && result.RequestPayloadLength == 0
            && response[0] == 0xCC
            && response[63] == 0xCC);
    }

    [HostTest("net.http.static.tcp rejects invalid payload range", HostLane.Fast, 5)]
    private static bool TestRejectsInvalidPayloadRange()
    {
        byte[] request = Encoding.ASCII.GetBytes("GET /healthz HTTP/1.1\r\n\r\n");
        byte[] receive = CreateReceiveBuffer(request);
        Span<byte> response = stackalloc byte[64];
        response.Fill(0xCC);
        TcpReceiveDispatchResult dispatch = TcpReceiveDispatchResult.Success(
            TcpReceiveDispatchAction.PayloadAccepted,
            default,
            CreateTcpHeader(),
            receive.Length - 4,
            TcpHeader.MinHeaderLength + request.Length,
            request.Length,
            ackBytesWritten: 0,
            TcpConnectionCloseState.Established,
            LocalNextSequence,
            RemoteNextSequence + (uint)request.Length);

        HttpStaticTcpResponsePlanStatus status = HttpStaticTcpResponsePlan.TryHandle(
            receive,
            dispatch,
            "/healthz"u8,
            "OK"u8,
            "missing"u8,
            "bad method"u8,
            "text/plain"u8,
            response,
            out HttpStaticTcpResponsePlanResult result);

        return HostCheck.Result(status == HttpStaticTcpResponsePlanStatus.InvalidPayloadRange
            && !result.Succeeded
            && result.RequestPayloadLength == request.Length
            && result.ResponseBytesWritten == 0
            && response[0] == 0xCC
            && response[63] == 0xCC);
    }

    [HostTest("net.http.static.tcp reports short response buffer", HostLane.Fast, 6)]
    private static bool TestReportsShortResponseBuffer()
    {
        byte[] request = Encoding.ASCII.GetBytes("GET /healthz HTTP/1.1\r\nHost: os.test\r\n\r\n");
        byte[] receive = CreateReceiveBuffer(request);
        TcpReceiveDispatchResult dispatch = CreatePayloadDispatch(request.Length);
        Span<byte> response = stackalloc byte[16];
        response.Fill(0xCC);

        HttpStaticTcpResponsePlanStatus status = HttpStaticTcpResponsePlan.TryHandle(
            receive,
            dispatch,
            "/healthz"u8,
            "OS_MINIMAL_API_OK"u8,
            "missing"u8,
            "bad method"u8,
            "text/plain"u8,
            response,
            out HttpStaticTcpResponsePlanResult result);

        return HostCheck.Result(status == HttpStaticTcpResponsePlanStatus.HttpTransactionFailed
            && !result.Succeeded
            && result.HttpStatus == HttpStaticResponseTransactionStatus.ResponseWriteFailed
            && result.ResponseStatus == HttpResponseWriterStatus.DestinationTooSmall
            && result.HttpStatusCode == 200
            && result.ResponseBytesWritten == 0
            && result.ResponseBytesRequired > response.Length
            && response[0] == 0xCC
            && response[15] == 0xCC);
    }

    [HostTest("net.http.static.tcp default result is not success", HostLane.Fast, 7)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        HttpStaticTcpResponsePlanResult defaultResult = default;
        HttpStaticTcpResponsePlanResult failure = HttpStaticTcpResponsePlanResult.Fail(
            HttpStaticTcpResponsePlanStatus.TcpDispatchFailed,
            CreateFailedDispatch(),
            default,
            requestPayloadOffset: 0,
            requestPayloadLength: 0);

        return HostCheck.Result(!defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == HttpStaticTcpResponsePlanStatus.Success
            && defaultResult.HttpStatus == HttpStaticResponseTransactionStatus.Success
            && failure.IsInitialized
            && !failure.Succeeded
            && failure.Status == HttpStaticTcpResponsePlanStatus.TcpDispatchFailed
            && failure.TcpDispatchStatus == TcpReceiveDispatchStatus.BadTcpChecksum
            && HttpStaticTcpResponsePlan.ConsumesTcpReceiveDispatchMetadata
            && HttpStaticTcpResponsePlan.UsesCallerOwnedBuffers
            && !HttpStaticTcpResponsePlan.AllocatesBuffers
            && !HttpStaticTcpResponsePlan.OpensSockets
            && !HttpStaticTcpResponsePlan.WritesTcpFrames
            && !HttpStaticTcpResponsePlan.PerformsNicIo
            && !HttpStaticTcpResponsePlan.StartsHostedDotNet
            && !HttpStaticTcpResponsePlan.StartsKestrel
            && !RuntimeHelpers.IsReferenceOrContainsReferences<HttpStaticTcpResponsePlanResult>());
    }

    [HostTest("net.http.static.tcp.formatter formats 200 marker", HostLane.Fast, 8)]
    private static bool TestFormatterFormats200Marker()
    {
        HttpStaticTcpResponsePlanResult plan = CreatePlan(
            "GET /healthz HTTP/1.1\r\nHost: os.test\r\nConnection: close\r\n\r\n",
            "OS_MINIMAL_API_OK",
            "missing",
            "bad method",
            out _);

        HttpStaticTcpResponsePlanDiagnosticFormatResult query =
            HttpStaticTcpResponsePlanDiagnosticFormatter.QueryRequiredBytes(plan);
        byte[] destination = new byte[query.RequiredBytes];
        HttpStaticTcpResponsePlanDiagnosticFormatResult format =
            HttpStaticTcpResponsePlanDiagnosticFormatter.FormatResult(plan, destination);
        string marker = Encoding.ASCII.GetString(destination, 0, format.BytesWritten);

        string expected =
            "HTTP_STATIC_TCP_RESPONSE status=Success" +
            " tcp=Success" +
            " action=PayloadAccepted" +
            " http=Success" +
            " request=Success" +
            " response=Success" +
            " method=Get" +
            " httpStatus=200" +
            " requestPayloadOffset=" + plan.RequestPayloadOffset +
            " requestPayloadBytes=" + plan.RequestPayloadLength +
            " responseBytesWritten=" + plan.ResponseBytesWritten +
            " responseBytesRequired=" + plan.ResponseBytesRequired +
            " responseBodyOffset=" + plan.ResponseBodyOffset +
            " responseBodyBytes=" + plan.ResponseBodyLength +
            " opensSocket=false" +
            " writesTcpFrame=false" +
            " performsNicIo=false" +
            " startsKestrel=false";

        return HostCheck.Result(query.IsInitialized
            && !query.Succeeded
            && query.Status == HttpStaticTcpResponsePlanDiagnosticFormatStatus.Success
            && query.PlanStatus == HttpStaticTcpResponsePlanStatus.Success
            && query.BytesWritten == 0
            && query.RequiredBytes == expected.Length
            && format.Succeeded
            && format.PlanStatus == HttpStaticTcpResponsePlanStatus.Success
            && format.BytesWritten == expected.Length
            && format.RequiredBytes == expected.Length
            && format.AvailableBytes == destination.Length
            && marker == expected
            && !marker.Contains("GET /healthz", StringComparison.Ordinal)
            && !marker.Contains("OS_MINIMAL_API_OK", StringComparison.Ordinal)
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                HttpStaticTcpResponsePlanDiagnosticFormatResult>());
    }

    [HostTest("net.http.static.tcp.formatter formats 404 and 405 metadata", HostLane.Fast, 9)]
    private static bool TestFormatterFormats404And405Metadata()
    {
        HttpStaticTcpResponsePlanResult notFound = CreatePlan(
            "GET /missing HTTP/1.1\r\nHost: os.test\r\n\r\n",
            "OK",
            "not found",
            "bad method",
            out _);
        string notFoundMarker = FormatPlanMarker(notFound);

        HttpStaticTcpResponsePlanResult methodNotAllowed = CreatePlan(
            "POST /healthz HTTP/1.1\r\nHost: os.test\r\nContent-Length: 0\r\n\r\n",
            "OK",
            "missing",
            "bad method",
            out _);
        string methodMarker = FormatPlanMarker(methodNotAllowed);

        return HostCheck.Result(notFound.HttpStatusCode == 404
            && methodNotAllowed.HttpStatusCode == 405
            && notFoundMarker.Contains("httpStatus=404", StringComparison.Ordinal)
            && notFoundMarker.Contains("method=Get", StringComparison.Ordinal)
            && notFoundMarker.Contains("requestPayloadOffset=" + notFound.RequestPayloadOffset, StringComparison.Ordinal)
            && notFoundMarker.Contains("requestPayloadBytes=" + notFound.RequestPayloadLength, StringComparison.Ordinal)
            && notFoundMarker.Contains("responseBodyBytes=9", StringComparison.Ordinal)
            && methodMarker.Contains("httpStatus=405", StringComparison.Ordinal)
            && methodMarker.Contains("method=Post", StringComparison.Ordinal)
            && methodMarker.Contains("requestPayloadOffset=" + methodNotAllowed.RequestPayloadOffset, StringComparison.Ordinal)
            && methodMarker.Contains("requestPayloadBytes=" + methodNotAllowed.RequestPayloadLength, StringComparison.Ordinal)
            && methodMarker.Contains("responseBodyBytes=10", StringComparison.Ordinal)
            && methodMarker.Contains("opensSocket=false", StringComparison.Ordinal)
            && methodMarker.Contains("writesTcpFrame=false", StringComparison.Ordinal)
            && methodMarker.Contains("performsNicIo=false", StringComparison.Ordinal)
            && methodMarker.Contains("startsKestrel=false", StringComparison.Ordinal)
            && !notFoundMarker.Contains("not found", StringComparison.Ordinal)
            && !methodMarker.Contains("bad method", StringComparison.Ordinal));
    }

    [HostTest("net.http.static.tcp.formatter reports destination capacity", HostLane.Fast, 10)]
    private static bool TestFormatterReportsDestinationCapacity()
    {
        HttpStaticTcpResponsePlanResult plan = CreatePlan(
            "GET /healthz HTTP/1.1\r\nHost: os.test\r\n\r\n",
            "OK",
            "missing",
            "bad method",
            out _);
        HttpStaticTcpResponsePlanDiagnosticFormatResult query =
            HttpStaticTcpResponsePlanDiagnosticFormatter.QueryRequiredBytes(plan);

        Span<byte> destination = stackalloc byte[query.RequiredBytes - 1];
        destination.Fill(0xCC);
        HttpStaticTcpResponsePlanDiagnosticFormatResult format =
            HttpStaticTcpResponsePlanDiagnosticFormatter.FormatResult(plan, destination);

        return HostCheck.Result(query.Status == HttpStaticTcpResponsePlanDiagnosticFormatStatus.Success
            && query.RequiredBytes > 0
            && format.Status == HttpStaticTcpResponsePlanDiagnosticFormatStatus.DestinationTooSmall
            && !format.Succeeded
            && format.BytesWritten == 0
            && format.RequiredBytes == query.RequiredBytes
            && format.AvailableBytes == destination.Length
            && AllBytesEqual(destination, 0xCC));
    }

    [HostTest("net.http.static.tcp.formatter rejects default and invalid results", HostLane.Fast, 11)]
    private static bool TestFormatterRejectsDefaultAndInvalidResults()
    {
        Span<byte> destination = stackalloc byte[512];
        destination.Fill(0xCC);

        HttpStaticTcpResponsePlanResult defaultResult = default;
        HttpStaticTcpResponsePlanDiagnosticFormatResult defaultFormat =
            HttpStaticTcpResponsePlanDiagnosticFormatter.FormatResult(defaultResult, destination);
        HttpStaticTcpResponsePlanDiagnosticFormatResult defaultQuery =
            HttpStaticTcpResponsePlanDiagnosticFormatter.QueryRequiredBytes(defaultResult);

        HttpStaticTcpResponsePlanResult invalidPlanStatus =
            HttpStaticTcpResponsePlanResult.Fail(
                (HttpStaticTcpResponsePlanStatus)99,
                CreatePayloadDispatch(requestLength: 8),
                default,
                requestPayloadOffset: TcpSegmentOffset + TcpHeader.MinHeaderLength,
                requestPayloadLength: 8);
        HttpStaticTcpResponsePlanDiagnosticFormatResult invalidPlanFormat =
            HttpStaticTcpResponsePlanDiagnosticFormatter.FormatResult(
                invalidPlanStatus,
                stackalloc byte[512]);

        HttpStaticResponseTransactionResult invalidHttp =
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
                bytesRequired: 128);
        HttpStaticTcpResponsePlanResult invalidNested =
            HttpStaticTcpResponsePlanResult.Fail(
                HttpStaticTcpResponsePlanStatus.HttpTransactionFailed,
                CreatePayloadDispatch(requestLength: 8),
                invalidHttp,
                requestPayloadOffset: TcpSegmentOffset + TcpHeader.MinHeaderLength,
                requestPayloadLength: 8);
        HttpStaticTcpResponsePlanDiagnosticFormatResult invalidNestedFormat =
            HttpStaticTcpResponsePlanDiagnosticFormatter.FormatResult(
                invalidNested,
                stackalloc byte[512]);

        HttpStaticTcpResponsePlanResult invalidRange =
            HttpStaticTcpResponsePlanResult.Fail(
                HttpStaticTcpResponsePlanStatus.InvalidPayloadRange,
                CreatePayloadDispatch(requestLength: 8),
                default,
                requestPayloadOffset: -1,
                requestPayloadLength: 8);
        HttpStaticTcpResponsePlanDiagnosticFormatResult invalidRangeFormat =
            HttpStaticTcpResponsePlanDiagnosticFormatter.FormatResult(
                invalidRange,
                stackalloc byte[512]);
        HttpStaticTcpResponsePlanDiagnosticFormatResult defaultFormatResult = default;

        return HostCheck.Result(defaultFormat.Status == HttpStaticTcpResponsePlanDiagnosticFormatStatus.InvalidResult
            && !defaultFormat.Succeeded
            && defaultFormat.RequiredBytes == 0
            && defaultFormat.AvailableBytes == destination.Length
            && defaultQuery.Status == HttpStaticTcpResponsePlanDiagnosticFormatStatus.InvalidResult
            && defaultQuery.RequiredBytes == 0
            && invalidPlanFormat.Status == HttpStaticTcpResponsePlanDiagnosticFormatStatus.InvalidResult
            && invalidPlanFormat.PlanStatus == (HttpStaticTcpResponsePlanStatus)99
            && invalidNestedFormat.Status == HttpStaticTcpResponsePlanDiagnosticFormatStatus.InvalidResult
            && invalidRangeFormat.Status == HttpStaticTcpResponsePlanDiagnosticFormatStatus.InvalidResult
            && !defaultFormatResult.IsInitialized
            && !defaultFormatResult.Succeeded
            && defaultFormatResult.Status == HttpStaticTcpResponsePlanDiagnosticFormatStatus.Success
            && defaultFormatResult.PlanStatus == HttpStaticTcpResponsePlanStatus.Success
            && destination[0] == 0xCC
            && destination[511] == 0xCC);
    }

    [HostTest("net.http.static.tcp.formatter uses shared ascii text builder", HostLane.Fast, 12)]
    private static bool TestFormatterUsesSharedAsciiTextBuilder()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "OS.Kernel",
                "Net",
                "HttpStaticTcpResponsePlanDiagnosticFormatter.cs"));

        return HostCheck.Result(HttpStaticTcpResponsePlanDiagnosticFormatter.UsesSharedAsciiTextBuilder
            && HttpStaticTcpResponsePlanDiagnosticFormatter.UsesCallerProvidedDestination
            && !HttpStaticTcpResponsePlanDiagnosticFormatter.AllocatesManagedStorage
            && !HttpStaticTcpResponsePlanDiagnosticFormatter.OpensSocket
            && !HttpStaticTcpResponsePlanDiagnosticFormatter.WritesTcpFrame
            && !HttpStaticTcpResponsePlanDiagnosticFormatter.PerformsNicIo
            && !HttpStaticTcpResponsePlanDiagnosticFormatter.StartsKestrel
            && source.Contains("using OS.Std.Text;", StringComparison.Ordinal)
            && source.Contains("AsciiTextBuilder.AppendBytes(destination, offset, value)", StringComparison.Ordinal)
            && source.Contains("AsciiTextBuilder.AppendInt64Decimal(destination, offset, value)", StringComparison.Ordinal)
            && source.Contains("AsciiTextBuilder.AppendUInt64Decimal(destination, offset, value)", StringComparison.Ordinal)
            && source.Contains("AsciiDecimalFormatter.CountInt32Digits(", StringComparison.Ordinal)
            && source.Contains("AsciiDecimalFormatter.CountUInt32Digits(", StringComparison.Ordinal)
            && !source.Contains("private static int GetInt64DecimalByteCount", StringComparison.Ordinal)
            && !source.Contains("private static int GetUInt64DecimalByteCount", StringComparison.Ordinal)
            && !source.Contains("destination[offset + index] = (byte)('0' +", StringComparison.Ordinal)
            && !source.Contains("value /= 10", StringComparison.Ordinal)
            && !source.Contains("value % 10", StringComparison.Ordinal)
            && !source.Contains("value.CopyTo(destination[offset..])", StringComparison.Ordinal));
    }

    private static byte[] CreateReceiveBuffer(ReadOnlySpan<byte> request)
    {
        byte[] buffer = new byte[TcpSegmentOffset + TcpHeader.MinHeaderLength + request.Length + 8];
        request.CopyTo(buffer.AsSpan(TcpSegmentOffset + TcpHeader.MinHeaderLength, request.Length));
        return buffer;
    }

    private static TcpReceiveDispatchResult CreatePayloadDispatch(int requestLength)
    {
        return TcpReceiveDispatchResult.Success(
            TcpReceiveDispatchAction.PayloadAccepted,
            default,
            CreateTcpHeader(),
            TcpSegmentOffset,
            TcpHeader.MinHeaderLength + requestLength,
            requestLength,
            ackBytesWritten: 0,
            TcpConnectionCloseState.Established,
            LocalNextSequence,
            RemoteNextSequence + (uint)requestLength);
    }

    private static TcpReceiveDispatchResult CreateFailedDispatch()
    {
        return TcpReceiveDispatchResult.Fail(
            TcpReceiveDispatchStatus.BadTcpChecksum,
            default,
            PacketParseStatus.BadChecksum,
            TcpEstablishedStreamStatus.Success,
            TcpConnectionCloseStatus.Success,
            TcpConnectionCloseState.Established,
            Ipv4TransportChecksumStatus.Success,
            CreateTcpHeader(),
            TcpSegmentOffset,
            TcpHeader.MinHeaderLength,
            LocalNextSequence,
            RemoteNextSequence);
    }

    private static TcpHeader CreateTcpHeader()
    {
        return new TcpHeader(
            RemotePort,
            LocalPort,
            RemoteNextSequence,
            LocalNextSequence,
            TcpHeader.MinHeaderLength,
            TcpEstablishedStream.FlagPsh | TcpEstablishedStream.FlagAck,
            windowSize: 0x4000,
            checksum: 0x1234,
            urgentPointer: 0);
    }

    private static HttpStaticTcpResponsePlanResult CreatePlan(
        string requestText,
        string successBody,
        string notFoundBody,
        string methodNotAllowedBody,
        out byte[] response)
    {
        byte[] request = Encoding.ASCII.GetBytes(requestText);
        byte[] receive = CreateReceiveBuffer(request);
        response = new byte[256];

        HttpStaticTcpResponsePlanStatus status = HttpStaticTcpResponsePlan.TryHandle(
            receive,
            CreatePayloadDispatch(request.Length),
            "/healthz"u8,
            Encoding.ASCII.GetBytes(successBody),
            Encoding.ASCII.GetBytes(notFoundBody),
            Encoding.ASCII.GetBytes(methodNotAllowedBody),
            "text/plain"u8,
            response,
            out HttpStaticTcpResponsePlanResult result);

        if (status != HttpStaticTcpResponsePlanStatus.Success || !result.Succeeded)
        {
            throw new InvalidOperationException("Failed to create HTTP static TCP response plan.");
        }

        return result;
    }

    private static string FormatPlanMarker(in HttpStaticTcpResponsePlanResult plan)
    {
        HttpStaticTcpResponsePlanDiagnosticFormatResult query =
            HttpStaticTcpResponsePlanDiagnosticFormatter.QueryRequiredBytes(plan);
        byte[] destination = new byte[query.RequiredBytes];
        HttpStaticTcpResponsePlanDiagnosticFormatResult format =
            HttpStaticTcpResponsePlanDiagnosticFormatter.FormatResult(plan, destination);

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
