using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Net;

internal static class FetchAppHttpTcpExchangePlanTests
{
    private const ushort LocalPort = 49210;
    private const ushort RemotePort = 80;
    private const uint ClientInitialSequence = 0x7100_2000;
    private const uint ServerInitialSequence = 0x8100_3000;
    private const ushort WindowSize = 0x3000;
    private const byte TimeToLive = 43;
    private const ushort Identification = 0x411;
    private const int TcpChecksumOffset = 16;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("fetch.http tcp exchange completes completion and close", TestCompletesCompletionAndClose, ref failures);
        Run("fetch.http tcp exchange rejects context failure", TestRejectsContextFailure, ref failures);
        Run("fetch.http tcp exchange rejects short endpoint buffers", TestRejectsShortEndpointBuffers, ref failures);
        Run("fetch.http tcp exchange rejects bad synack checksum", TestRejectsBadSynAckChecksum, ref failures);
        Run("fetch.http tcp exchange rejects request frame too small", TestRejectsRequestFrameTooSmall, ref failures);
        Run("fetch.http tcp exchange rejects missing expected marker", TestRejectsMissingExpectedMarker, ref failures);
        Run("fetch.http tcp exchange rejects close frame too small", TestRejectsCloseFrameTooSmall, ref failures);
        Run("fetch.http tcp exchange default result is not success", TestDefaultResultIsNotSuccess, ref failures);
        Run(
            "fetch.http tcp exchange formatter formats success marker",
            TestFormatterFormatsSuccessMarker,
            ref failures);
        Run(
            "fetch.http tcp exchange formatter reports short destination",
            TestFormatterReportsShortDestination,
            ref failures);
        Run(
            "fetch.http tcp exchange formatter rejects default result",
            TestFormatterRejectsDefaultResult,
            ref failures);
        Run(
            "fetch.http tcp exchange formatter uses shared ascii text builder",
            TestFormatterUsesSharedAsciiTextBuilder,
            ref failures);
        Run("boot.sequence fetch http tcp exchange ready", TestBootSequenceReady, ref failures);
        Run(
            "boot.sequence fetch http tcp exchange rejects failed result",
            TestBootSequenceRejectsFailedResult,
            ref failures);
        Run(
            "boot.sequence fetch http tcp exchange rejects default result",
            TestBootSequenceRejectsDefaultResult,
            ref failures);
        Run(
            "boot.sequence fetch http tcp exchange preserves default markers",
            TestBootSequencePreservesDefaultMarkers,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("FetchApp HTTP TCP exchange plan host tests failed: " + failures);
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

    private static bool TestCompletesCompletionAndClose()
    {
        byte[] responsePayload = CreateHttpResponseBody("prefix FETCH_OK suffix");
        ExchangeBuffers buffers = CreateBuffers(responsePayload);

        FetchAppHttpTcpExchangePlanStatus status = FetchAppHttpTcpExchangePlan.Execute(
            ReadyContext(),
            buffers.Host,
            buffers.Path,
            buffers.SynAckFrame,
            buffers.ResponseFrame,
            buffers.StreamReceiveBuffer,
            buffers.SynFrame,
            buffers.FinalAckFrame,
            buffers.RequestScratch,
            buffers.RequestFrame,
            buffers.ResponseDestination,
            buffers.CloseFrame,
            expectedHttpStatusCode: 200,
            "FETCH_OK"u8,
            out FetchAppHttpTcpExchangePlanResult result);

        if (status != FetchAppHttpTcpExchangePlanStatus.Success ||
            !result.Succeeded ||
            result.Context.Status != FetchAppHttpTcpContextPlanStatus.Success ||
            result.SynFrameBytes != HttpTcpClientGetPlanner.HeaderBytes ||
            result.FinalAckFrameBytes != HttpTcpClientGetPlanner.HeaderBytes ||
            result.RequestFrameBytes <= HttpTcpClientGetPlanner.HeaderBytes ||
            result.ResponseBytesRead != responsePayload.Length ||
            result.Completion.Status != FetchAppHttpCompletionPlanStatus.Success ||
            result.Completion.MatchOffset < result.Response.HttpBodyOffset ||
            result.Close.Status != TcpConnectionCloseIpv4FramePlanStatus.Success ||
            result.CloseFrameBytes != TcpConnectionCloseIpv4FramePlanner.HeaderBytes ||
            result.Close.State != TcpConnectionCloseState.FinWait1 ||
            result.Close.LocalNextSequenceNumber != result.Response.LocalNextSequenceNumber + 1 ||
            result.Close.RemoteNextSequenceNumber != result.Response.RemoteNextSequenceNumber)
        {
            return false;
        }

        return TryParseTcpFrame(
                buffers.CloseFrame.AsSpan(0, result.CloseFrameBytes),
                out EthernetHeader closeEthernet,
                out Ipv4Header closeIpv4,
                out TcpHeader closeTcp,
                out ReadOnlySpan<byte> closePayload)
            && closeEthernet.Source == LocalMac()
            && closeEthernet.Destination == RemoteMac()
            && closeIpv4.Source == LocalAddress()
            && closeIpv4.Destination == RemoteAddress()
            && closeTcp.SourcePort == LocalPort
            && closeTcp.DestinationPort == RemotePort
            && closeTcp.SequenceNumber == result.Response.LocalNextSequenceNumber
            && closeTcp.AcknowledgementNumber == result.Response.RemoteNextSequenceNumber
            && closeTcp.Flags == (TcpConnectionClose.FlagFin | TcpConnectionClose.FlagAck)
            && closeTcp.Checksum != 0
            && closePayload.Length == 0
            && FetchAppHttpTcpExchangePlan.UsesCallerOwnedFrameBuffers
            && !FetchAppHttpTcpExchangePlan.PerformsLiveNetworkIo
            && !FetchAppHttpTcpExchangePlan.OpensSockets
            && !FetchAppHttpTcpExchangePlan.PerformsDnsOrArp
            && !FetchAppHttpTcpExchangePlan.PerformsTls
            && !RuntimeHelpers.IsReferenceOrContainsReferences<FetchAppHttpTcpExchangePlanResult>();
    }

    private static bool TestRejectsContextFailure()
    {
        byte[] responsePayload = CreateHttpResponseBody("prefix FETCH_OK suffix");
        ExchangeBuffers buffers = CreateBuffers(responsePayload);

        FetchAppHttpTcpExchangePlanStatus status = FetchAppHttpTcpExchangePlan.Execute(
            default,
            buffers.Host,
            buffers.Path,
            buffers.SynAckFrame,
            buffers.ResponseFrame,
            buffers.StreamReceiveBuffer,
            buffers.SynFrame,
            buffers.FinalAckFrame,
            buffers.RequestScratch,
            buffers.RequestFrame,
            buffers.ResponseDestination,
            buffers.CloseFrame,
            expectedHttpStatusCode: 200,
            "FETCH_OK"u8,
            out FetchAppHttpTcpExchangePlanResult result);

        return status == FetchAppHttpTcpExchangePlanStatus.ContextFailed
            && result.IsInitialized
            && !result.Succeeded
            && !result.Context.IsInitialized
            && !result.Syn.IsInitialized
            && result.ExpectedMarkerLength == "FETCH_OK".Length;
    }

    private static bool TestRejectsShortEndpointBuffers()
    {
        byte[] responsePayload = CreateHttpResponseBody("prefix FETCH_OK suffix");
        ExchangeBuffers buffers = CreateBuffers(responsePayload);

        FetchAppHttpTcpExchangePlanStatus status = FetchAppHttpTcpExchangePlan.Execute(
            ReadyContext(),
            buffers.Host.AsSpan(0, buffers.Host.Length - 1),
            buffers.Path,
            buffers.SynAckFrame,
            buffers.ResponseFrame,
            buffers.StreamReceiveBuffer,
            buffers.SynFrame,
            buffers.FinalAckFrame,
            buffers.RequestScratch,
            buffers.RequestFrame,
            buffers.ResponseDestination,
            buffers.CloseFrame,
            expectedHttpStatusCode: 200,
            "FETCH_OK"u8,
            out FetchAppHttpTcpExchangePlanResult result);

        return status == FetchAppHttpTcpExchangePlanStatus.EndpointBufferTooSmall
            && result.IsInitialized
            && !result.Succeeded
            && result.Context.Succeeded
            && !result.Syn.IsInitialized
            && result.HostBytes == buffers.Host.Length
            && result.PathBytes == buffers.Path.Length;
    }

    private static bool TestRejectsBadSynAckChecksum()
    {
        byte[] responsePayload = CreateHttpResponseBody("prefix FETCH_OK suffix");
        ExchangeBuffers buffers = CreateBuffers(responsePayload);
        buffers.SynAckFrame[TcpChecksumOffset + HttpTcpClientGetPlanner.TcpSegmentOffset] ^= 0x5A;

        FetchAppHttpTcpExchangePlanStatus status = FetchAppHttpTcpExchangePlan.Execute(
            ReadyContext(),
            buffers.Host,
            buffers.Path,
            buffers.SynAckFrame,
            buffers.ResponseFrame,
            buffers.StreamReceiveBuffer,
            buffers.SynFrame,
            buffers.FinalAckFrame,
            buffers.RequestScratch,
            buffers.RequestFrame,
            buffers.ResponseDestination,
            buffers.CloseFrame,
            expectedHttpStatusCode: 200,
            "FETCH_OK"u8,
            out FetchAppHttpTcpExchangePlanResult result);

        return status == FetchAppHttpTcpExchangePlanStatus.SynAckFrameFailed
            && result.IsInitialized
            && !result.Succeeded
            && result.Syn.Succeeded
            && !result.SynAck.Succeeded
            && result.SynAck.TcpFrameStatus == TcpIpv4FramePlanStatus.BadTcpChecksum
            && !result.FinalAck.IsInitialized;
    }

    private static bool TestRejectsRequestFrameTooSmall()
    {
        byte[] responsePayload = CreateHttpResponseBody("prefix FETCH_OK suffix");
        ExchangeBuffers buffers = CreateBuffers(responsePayload);
        byte[] shortRequestFrame = new byte[HttpTcpClientGetPlanner.HeaderBytes + 3];

        FetchAppHttpTcpExchangePlanStatus status = FetchAppHttpTcpExchangePlan.Execute(
            ReadyContext(),
            buffers.Host,
            buffers.Path,
            buffers.SynAckFrame,
            buffers.ResponseFrame,
            buffers.StreamReceiveBuffer,
            buffers.SynFrame,
            buffers.FinalAckFrame,
            buffers.RequestScratch,
            shortRequestFrame,
            buffers.ResponseDestination,
            buffers.CloseFrame,
            expectedHttpStatusCode: 200,
            "FETCH_OK"u8,
            out FetchAppHttpTcpExchangePlanResult result);

        return status == FetchAppHttpTcpExchangePlanStatus.RequestFrameFailed
            && result.IsInitialized
            && !result.Succeeded
            && result.FinalAck.Succeeded
            && !result.Request.Succeeded
            && result.Request.Status == HttpTcpClientGetPlanStatus.FrameBufferTooSmall
            && !result.Response.IsInitialized;
    }

    private static bool TestRejectsMissingExpectedMarker()
    {
        byte[] responsePayload = CreateHttpResponseBody("prefix NO_MATCH suffix");
        ExchangeBuffers buffers = CreateBuffers(responsePayload);

        FetchAppHttpTcpExchangePlanStatus status = FetchAppHttpTcpExchangePlan.Execute(
            ReadyContext(),
            buffers.Host,
            buffers.Path,
            buffers.SynAckFrame,
            buffers.ResponseFrame,
            buffers.StreamReceiveBuffer,
            buffers.SynFrame,
            buffers.FinalAckFrame,
            buffers.RequestScratch,
            buffers.RequestFrame,
            buffers.ResponseDestination,
            buffers.CloseFrame,
            expectedHttpStatusCode: 200,
            "FETCH_OK"u8,
            out FetchAppHttpTcpExchangePlanResult result);

        return status == FetchAppHttpTcpExchangePlanStatus.CompletionFailed
            && result.IsInitialized
            && !result.Succeeded
            && result.Response.Succeeded
            && result.Completion.Status == FetchAppHttpCompletionPlanStatus.MissingExpectedMarker
            && !result.Close.IsInitialized;
    }

    private static bool TestRejectsCloseFrameTooSmall()
    {
        byte[] responsePayload = CreateHttpResponseBody("prefix FETCH_OK suffix");
        ExchangeBuffers buffers = CreateBuffers(responsePayload);
        byte[] shortCloseFrame = new byte[TcpConnectionCloseIpv4FramePlanner.HeaderBytes - 1];

        FetchAppHttpTcpExchangePlanStatus status = FetchAppHttpTcpExchangePlan.Execute(
            ReadyContext(),
            buffers.Host,
            buffers.Path,
            buffers.SynAckFrame,
            buffers.ResponseFrame,
            buffers.StreamReceiveBuffer,
            buffers.SynFrame,
            buffers.FinalAckFrame,
            buffers.RequestScratch,
            buffers.RequestFrame,
            buffers.ResponseDestination,
            shortCloseFrame,
            expectedHttpStatusCode: 200,
            "FETCH_OK"u8,
            out FetchAppHttpTcpExchangePlanResult result);

        return status == FetchAppHttpTcpExchangePlanStatus.CloseFrameFailed
            && result.IsInitialized
            && !result.Succeeded
            && result.Completion.Succeeded
            && !result.Close.Succeeded
            && result.Close.Status == TcpConnectionCloseIpv4FramePlanStatus.FrameBufferTooSmall;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        FetchAppHttpTcpExchangePlanResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == FetchAppHttpTcpExchangePlanStatus.Success
            && result.Context.Status == FetchAppHttpTcpContextPlanStatus.Success
            && result.Syn.Status == HttpTcpClientGetPlanStatus.Success
            && result.Completion.Status == FetchAppHttpCompletionPlanStatus.Success
            && result.Close.Status == TcpConnectionCloseIpv4FramePlanStatus.Success
            && result.HostBytes == 0
            && result.PathBytes == 0
            && result.ExpectedHttpStatusCode == 0
            && result.ExpectedMarkerLength == 0;
    }

    private static bool TestFormatterFormatsSuccessMarker()
    {
        FetchAppHttpTcpExchangePlanResult result = ExecuteSuccessfulExchange(out ExchangeBuffers _);

        Span<byte> destination = stackalloc byte[768];
        destination.Fill(0);
        FetchAppHttpTcpExchangePlanDiagnosticFormatResult query =
            FetchAppHttpTcpExchangePlanDiagnosticFormatter.QueryRequiredBytes(result);
        FetchAppHttpTcpExchangePlanDiagnosticFormatResult format =
            FetchAppHttpTcpExchangePlanDiagnosticFormatter.FormatResult(result, destination);
        string text = Encoding.ASCII.GetString(destination[..format.BytesWritten]);
        string expected = "FETCH_HTTP_TCP_EXCHANGE status=Success context=Success syn=Success synAck=Success " +
            "finalAck=Success request=Success response=Success completion=Success close=Success " +
            "hostBytes=" + result.HostBytes +
            " pathBytes=" + result.PathBytes +
            " expectedHttpStatus=200 expectedMarkerBytes=8 synBytes=" + result.SynFrameBytes +
            " finalAckBytes=" + result.FinalAckFrameBytes +
            " requestBytes=" + result.RequestFrameBytes +
            " responseBytes=" + result.ResponseBytesRead +
            " closeBytes=" + result.CloseFrameBytes +
            " localSeq=" + result.Close.LocalNextSequenceNumber +
            " remoteSeq=" + result.Close.RemoteNextSequenceNumber;

        return query.IsInitialized
            && !query.Succeeded
            && query.Status == FetchAppHttpTcpExchangePlanDiagnosticFormatStatus.Success
            && query.ExchangeStatus == FetchAppHttpTcpExchangePlanStatus.Success
            && query.BytesWritten == 0
            && query.RequiredBytes == expected.Length
            && query.AvailableBytes == 0
            && format.Succeeded
            && format.ExchangeStatus == FetchAppHttpTcpExchangePlanStatus.Success
            && format.BytesWritten == expected.Length
            && format.RequiredBytes == expected.Length
            && format.AvailableBytes == destination.Length
            && text == expected
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                FetchAppHttpTcpExchangePlanDiagnosticFormatResult>();
    }

    private static bool TestFormatterReportsShortDestination()
    {
        FetchAppHttpTcpExchangePlanResult result = ExecuteSuccessfulExchange(out ExchangeBuffers _);
        FetchAppHttpTcpExchangePlanDiagnosticFormatResult query =
            FetchAppHttpTcpExchangePlanDiagnosticFormatter.QueryRequiredBytes(result);

        Span<byte> destination = stackalloc byte[24];
        destination.Fill(0xCC);
        FetchAppHttpTcpExchangePlanDiagnosticFormatResult shortResult =
            FetchAppHttpTcpExchangePlanDiagnosticFormatter.FormatResult(result, destination);

        return shortResult.IsInitialized
            && !shortResult.Succeeded
            && shortResult.Status == FetchAppHttpTcpExchangePlanDiagnosticFormatStatus.DestinationTooSmall
            && shortResult.ExchangeStatus == FetchAppHttpTcpExchangePlanStatus.Success
            && shortResult.BytesWritten == 0
            && shortResult.RequiredBytes == query.RequiredBytes
            && shortResult.AvailableBytes == destination.Length
            && query.RequiredBytes > destination.Length
            && destination[0] == 0xCC
            && destination[23] == 0xCC;
    }

    private static bool TestFormatterRejectsDefaultResult()
    {
        Span<byte> destination = stackalloc byte[64];
        destination.Fill(0xAA);

        FetchAppHttpTcpExchangePlanResult defaultResult = default;
        FetchAppHttpTcpExchangePlanDiagnosticFormatResult format =
            FetchAppHttpTcpExchangePlanDiagnosticFormatter.FormatResult(defaultResult, destination);
        FetchAppHttpTcpExchangePlanDiagnosticFormatResult query =
            FetchAppHttpTcpExchangePlanDiagnosticFormatter.QueryRequiredBytes(defaultResult);
        FetchAppHttpTcpExchangePlanDiagnosticFormatResult defaultFormat = default;

        return format.IsInitialized
            && !format.Succeeded
            && format.Status == FetchAppHttpTcpExchangePlanDiagnosticFormatStatus.InvalidResult
            && format.ExchangeStatus == FetchAppHttpTcpExchangePlanStatus.Success
            && format.BytesWritten == 0
            && format.RequiredBytes == 0
            && format.AvailableBytes == destination.Length
            && query.IsInitialized
            && !query.Succeeded
            && query.Status == FetchAppHttpTcpExchangePlanDiagnosticFormatStatus.InvalidResult
            && query.RequiredBytes == 0
            && !defaultFormat.IsInitialized
            && !defaultFormat.Succeeded
            && defaultFormat.Status == FetchAppHttpTcpExchangePlanDiagnosticFormatStatus.Success
            && defaultFormat.ExchangeStatus == FetchAppHttpTcpExchangePlanStatus.Success
            && defaultFormat.RequiredBytes == 0
            && defaultFormat.AvailableBytes == 0
            && destination[0] == 0xAA
            && destination[63] == 0xAA;
    }

    private static bool TestFormatterUsesSharedAsciiTextBuilder()
    {
        string source = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "OS.Kernel",
            "Net",
            "FetchAppHttpTcpExchangePlanDiagnosticFormatter.cs"));

        return source.Contains("using OS.Std.Text;", StringComparison.Ordinal)
            && source.Contains("AsciiTextBuilder.AppendBytes(destination, offset, value)", StringComparison.Ordinal)
            && source.Contains("AsciiTextBuilder.AppendInt64Decimal(destination, offset, value)", StringComparison.Ordinal)
            && source.Contains("AsciiTextBuilder.AppendUInt64Decimal(destination, offset, value)", StringComparison.Ordinal)
            && !source.Contains("value.CopyTo(destination[offset..])", StringComparison.Ordinal);
    }

    private static bool TestBootSequenceReady()
    {
        FetchAppHttpTcpExchangePlanResult exchange = ExecuteSuccessfulExchange(out ExchangeBuffers _);
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        BootResult result = BootSequence.RunFetchAppHttpTcpExchangeReadiness(
            exchange,
            serial,
            events);
        string expectedMarker = BootSequence.CreateFetchAppHttpTcpExchangeReadyMarker(exchange);

        return exchange.Succeeded
            && result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker
            && expectedMarker == BootSequence.FetchAppHttpTcpExchangeReadyPrefix +
                "FETCH_HTTP_TCP_EXCHANGE status=Success context=Success syn=Success synAck=Success " +
                "finalAck=Success request=Success response=Success completion=Success close=Success " +
                "hostBytes=" + exchange.HostBytes +
                " pathBytes=" + exchange.PathBytes +
                " expectedHttpStatus=200 expectedMarkerBytes=8 synBytes=" + exchange.SynFrameBytes +
                " finalAckBytes=" + exchange.FinalAckFrameBytes +
                " requestBytes=" + exchange.RequestFrameBytes +
                " responseBytes=" + exchange.ResponseBytesRead +
                " closeBytes=" + exchange.CloseFrameBytes +
                " localSeq=" + exchange.Close.LocalNextSequenceNumber +
                " remoteSeq=" + exchange.Close.RemoteNextSequenceNumber
            && expectedMarker.StartsWith(
                BootSequence.FetchAppHttpTcpExchangeReadyPrefix + "FETCH_HTTP_TCP_EXCHANGE ",
                StringComparison.Ordinal)
            && !expectedMarker.Contains("example.com", StringComparison.Ordinal)
            && !expectedMarker.Contains("/healthz", StringComparison.Ordinal)
            && !expectedMarker.Contains("FETCH_OK", StringComparison.Ordinal);
    }

    private static bool TestBootSequenceRejectsFailedResult()
    {
        FetchAppHttpTcpExchangePlanResult exchange = ExecuteExchangeWithMissingMarker();
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        BootResult result = BootSequence.RunFetchAppHttpTcpExchangeReadiness(
            exchange,
            serial,
            events);
        string expectedMarker = BootSequence.CreateFetchAppHttpTcpExchangeNotReadyMarker(
            FetchAppHttpTcpExchangeReadinessStatus.ExchangeFailed,
            exchange);

        return exchange.IsInitialized
            && !exchange.Succeeded
            && exchange.Status == FetchAppHttpTcpExchangePlanStatus.CompletionFailed
            && !result.Succeeded
            && result.Reason.Contains(
                nameof(FetchAppHttpTcpExchangeReadinessStatus.ExchangeFailed),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker
            && expectedMarker.StartsWith(BootSequence.FetchAppHttpTcpExchangeNotReadyPrefix, StringComparison.Ordinal)
            && expectedMarker.Contains("status=ExchangeFailed", StringComparison.Ordinal)
            && expectedMarker.Contains("exchange=CompletionFailed", StringComparison.Ordinal)
            && expectedMarker.Contains("completion=MissingExpectedMarker", StringComparison.Ordinal)
            && expectedMarker.Contains("format=Success", StringComparison.Ordinal);
    }

    private static bool TestBootSequenceRejectsDefaultResult()
    {
        FetchAppHttpTcpExchangePlanResult exchange = default;
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        BootResult result = BootSequence.RunFetchAppHttpTcpExchangeReadiness(
            exchange,
            serial,
            events);
        string expectedMarker = BootSequence.CreateFetchAppHttpTcpExchangeNotReadyMarker(
            FetchAppHttpTcpExchangeReadinessStatus.NotInitialized,
            exchange);

        return !exchange.IsInitialized
            && !exchange.Succeeded
            && !result.Succeeded
            && result.Reason.Contains(
                nameof(FetchAppHttpTcpExchangeReadinessStatus.NotInitialized),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker
            && expectedMarker ==
                "FETCH_APP_HTTP_TCP_EXCHANGE_NOT_READY status=NotInitialized initialized=false succeeded=false " +
                "exchange=Success format=InvalidResult bytesWritten=0 requiredBytes=0 availableBytes=0 " +
                "context=Success syn=Success synAck=Success finalAck=Success request=Success response=Success " +
                "completion=Success close=Success hostBytes=0 pathBytes=0 expectedHttpStatus=0 " +
                "expectedMarkerBytes=0 responseBytes=0 closeBytes=0";
    }

    private static bool TestBootSequencePreservesDefaultMarkers()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(32);

        BootResult result = BootSequence.Run(serial, events);
        if (!result.Succeeded)
        {
            return false;
        }

        return !serial.ContainsPrefix(BootSequence.FetchAppHttpTcpExchangeReadyPrefix)
            && !serial.ContainsPrefix(BootSequence.FetchAppHttpTcpExchangeNotReadyPrefix);
    }

    private static FetchAppHttpTcpExchangePlanResult ExecuteSuccessfulExchange(out ExchangeBuffers buffers)
    {
        byte[] responsePayload = CreateHttpResponseBody("prefix FETCH_OK suffix");
        buffers = CreateBuffers(responsePayload);

        FetchAppHttpTcpExchangePlanStatus status = FetchAppHttpTcpExchangePlan.Execute(
            ReadyContext(),
            buffers.Host,
            buffers.Path,
            buffers.SynAckFrame,
            buffers.ResponseFrame,
            buffers.StreamReceiveBuffer,
            buffers.SynFrame,
            buffers.FinalAckFrame,
            buffers.RequestScratch,
            buffers.RequestFrame,
            buffers.ResponseDestination,
            buffers.CloseFrame,
            expectedHttpStatusCode: 200,
            "FETCH_OK"u8,
            out FetchAppHttpTcpExchangePlanResult result);
        if (status != FetchAppHttpTcpExchangePlanStatus.Success || !result.Succeeded)
        {
            throw new InvalidOperationException("Failed to compose successful FetchApp exchange.");
        }

        return result;
    }

    private static FetchAppHttpTcpExchangePlanResult ExecuteExchangeWithMissingMarker()
    {
        byte[] responsePayload = CreateHttpResponseBody("prefix NO_MATCH suffix");
        ExchangeBuffers buffers = CreateBuffers(responsePayload);

        FetchAppHttpTcpExchangePlanStatus status = FetchAppHttpTcpExchangePlan.Execute(
            ReadyContext(),
            buffers.Host,
            buffers.Path,
            buffers.SynAckFrame,
            buffers.ResponseFrame,
            buffers.StreamReceiveBuffer,
            buffers.SynFrame,
            buffers.FinalAckFrame,
            buffers.RequestScratch,
            buffers.RequestFrame,
            buffers.ResponseDestination,
            buffers.CloseFrame,
            expectedHttpStatusCode: 200,
            "FETCH_OK"u8,
            out FetchAppHttpTcpExchangePlanResult result);
        if (status != FetchAppHttpTcpExchangePlanStatus.CompletionFailed ||
            result.Completion.Status != FetchAppHttpCompletionPlanStatus.MissingExpectedMarker)
        {
            throw new InvalidOperationException("Failed to compose failed FetchApp exchange fixture.");
        }

        return result;
    }

    private static ExchangeBuffers CreateBuffers(byte[] responsePayload)
    {
        byte[] host = Encoding.ASCII.GetBytes("example.com");
        byte[] path = Encoding.ASCII.GetBytes("/healthz");
        byte[] synAckFrame = CreateServerSynAckFrame();
        byte[] responseFrame = CreateServerPayloadFrame(
            RemoteMac(),
            LocalMac(),
            RemoteAddress(),
            LocalAddress(),
            RemotePort,
            LocalPort,
            ServerInitialSequence + 1,
            ClientInitialSequence + 1 + (uint)ExpectedRequestBytes(host, path),
            responsePayload);

        return new ExchangeBuffers(
            host,
            path,
            synAckFrame,
            responseFrame,
            new byte[512],
            new byte[HttpTcpClientGetPlanner.HeaderBytes],
            new byte[HttpTcpClientGetPlanner.HeaderBytes],
            new byte[256],
            new byte[HttpTcpClientGetPlanner.HeaderBytes + 256],
            new byte[responsePayload.Length],
            new byte[TcpConnectionCloseIpv4FramePlanner.HeaderBytes]);
    }

    private static FetchAppHttpTcpContextPlanResult ReadyContext()
    {
        byte[] host = Encoding.ASCII.GetBytes("example.com");
        FetchAppHttpTcpContextPlanStatus status = FetchAppHttpTcpContextPlan.Compose(
            ReadyEndpoint(host.Length),
            host,
            SuccessfulDns(host, RemoteAddress()),
            Ipv4NeighborRoutePlanResult.Cached(RemoteMac(), ArpNeighborCacheStatus.Success, 1, 1),
            ReadyInfo(),
            out FetchAppHttpTcpContextPlanResult result);
        if (status != FetchAppHttpTcpContextPlanStatus.Success || !result.Succeeded)
        {
            throw new InvalidOperationException("Failed to compose ready FetchApp TCP context.");
        }

        return result;
    }

    private static FetchAppHttpRequestEndpointResult ReadyEndpoint(int hostLength)
    {
        return FetchAppHttpRequestEndpointResult.Create(
            FetchAppHttpRequestEndpointStatus.Success,
            RemotePort,
            hostLength,
            "/healthz".Length,
            hostLength,
            "/healthz".Length,
            FetchAppHttpRequestEndpoint.MaximumHostLength,
            FetchAppHttpRequestEndpoint.MaximumPathLength,
            hasConfiguredUrl: false,
            hasConfiguredHost: true,
            hasConfiguredPort: true,
            hasConfiguredPath: true);
    }

    private static DnsResolverTransactionPlanResult SuccessfulDns(
        ReadOnlySpan<byte> host,
        Ipv4Address answerAddress)
    {
        DnsResolverTransitionResult resolver = DnsResolverTransitionResult.Success(
            DnsResolverState.Resolved,
            bytesWritten: 4,
            answerAddress);
        DnsUdpReceiveFrameResult receive = DnsUdpReceiveFrameResult.Success(default, resolver);
        return DnsResolverTransactionPlanResult.ReceiveSuccess(
            default,
            receive,
            transactionId: 0x1010,
            queryNameLength: host.Length,
            queryNameFingerprint: FetchAppHttpTcpContextPlan.ComputeHostFingerprint(host),
            answerTtlSeconds: 60);
    }

    private static FetchAppHttpTcpContextPlanInfo ReadyInfo()
    {
        return new FetchAppHttpTcpContextPlanInfo(
            LocalMac(),
            LocalAddress(),
            LocalPort,
            ClientInitialSequence,
            ServerInitialSequence,
            requiresRemoteInitialSequenceNumber: true,
            WindowSize,
            TimeToLive,
            Identification,
            maximumRequestPayloadBytes: 512);
    }

    private static byte[] CreateHttpResponseBody(string body)
    {
        return Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Length: " +
            body.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            "\r\nConnection: close\r\n\r\n" +
            body);
    }

    private static int ExpectedRequestBytes(ReadOnlySpan<byte> host, ReadOnlySpan<byte> path)
    {
        return 4 + path.Length + 11 + 6 + host.Length + 2 + 20 + 13 + 19 + 2;
    }

    private static byte[] CreateServerSynAckFrame()
    {
        return CreateServerPayloadFrame(
            RemoteMac(),
            LocalMac(),
            RemoteAddress(),
            LocalAddress(),
            RemotePort,
            LocalPort,
            ServerInitialSequence,
            ClientInitialSequence + 1,
            ReadOnlySpan<byte>.Empty,
            TcpActiveConnectTransaction.FlagSyn | TcpActiveConnectTransaction.FlagAck);
    }

    private static byte[] CreateServerPayloadFrame(
        MacAddress sourceMac,
        MacAddress destinationMac,
        Ipv4Address sourceAddress,
        Ipv4Address destinationAddress,
        ushort sourcePort,
        ushort destinationPort,
        uint sequenceNumber,
        uint acknowledgementNumber,
        ReadOnlySpan<byte> payload,
        byte flags = TcpEstablishedStream.FlagPsh | TcpEstablishedStream.FlagAck)
    {
        byte[] frame = new byte[HttpTcpClientGetPlanner.HeaderBytes + payload.Length];
        Span<byte> segment = frame.AsSpan(HttpTcpClientGetPlanner.TcpSegmentOffset);
        PacketParseStatus tcpStatus = TcpHeader.TryWrite(
            segment,
            sourcePort,
            destinationPort,
            sequenceNumber,
            acknowledgementNumber,
            flags,
            windowSize: 0x2000,
            checksum: 0,
            urgentPointer: 0,
            ReadOnlySpan<byte>.Empty,
            payload,
            out int tcpBytesWritten);
        if (tcpStatus != PacketParseStatus.Success)
        {
            throw new InvalidOperationException("Failed to build TCP fixture.");
        }

        Ipv4TransportChecksumStatus checksumStatus = Ipv4TransportChecksum.Compute(
            sourceAddress,
            destinationAddress,
            TcpIpv4FramePlanner.ProtocolTcp,
            segment.Slice(0, tcpBytesWritten),
            out Ipv4TransportChecksumResult checksumResult);
        if (checksumStatus != Ipv4TransportChecksumStatus.Success)
        {
            throw new InvalidOperationException("Failed to compute TCP checksum fixture.");
        }

        WriteTcpChecksum(segment, checksumResult.Checksum);

        EthernetHeader ethernet = new EthernetHeader(
            destinationMac,
            sourceMac,
            EthernetHeader.EtherTypeIpv4);
        if (EthernetHeader.TryWrite(frame, ethernet) != PacketParseStatus.Success)
        {
            throw new InvalidOperationException("Failed to build Ethernet fixture.");
        }

        Ipv4Header ipv4 = new Ipv4Header(
            sourceAddress,
            destinationAddress,
            TcpIpv4FramePlanner.ProtocolTcp,
            (ushort)(Ipv4Header.MinHeaderLength + tcpBytesWritten),
            timeToLive: 64,
            identification: 0x700,
            flagsAndFragmentOffset: 0,
            typeOfService: 0,
            headerChecksum: 0,
            headerLength: Ipv4Header.MinHeaderLength);
        if (Ipv4Header.TryWrite(
                frame.AsSpan(EthernetHeader.HeaderLength, Ipv4Header.MinHeaderLength),
                ipv4,
                out int ipv4BytesWritten) != PacketParseStatus.Success
            || ipv4BytesWritten != Ipv4Header.MinHeaderLength)
        {
            throw new InvalidOperationException("Failed to build IPv4 fixture.");
        }

        return frame;
    }

    private static bool TryParseTcpFrame(
        ReadOnlySpan<byte> frame,
        out EthernetHeader ethernet,
        out Ipv4Header ipv4,
        out TcpHeader tcp,
        out ReadOnlySpan<byte> payload)
    {
        tcp = default;
        ipv4 = default;
        payload = default;

        if (EthernetHeader.TryParse(
                frame,
                out ethernet,
                out ReadOnlySpan<byte> ethernetPayload) != PacketParseStatus.Success)
        {
            return false;
        }

        if (Ipv4Header.TryParse(
                ethernetPayload,
                out ipv4,
                out ReadOnlySpan<byte> ipv4Payload) != PacketParseStatus.Success)
        {
            return false;
        }

        return TcpHeader.TryParse(
                ipv4Payload,
                out tcp,
                out ReadOnlySpan<byte> options,
                out payload) == PacketParseStatus.Success
            && options.Length == 0;
    }

    private static void WriteTcpChecksum(Span<byte> segment, ushort checksum)
    {
        segment[TcpChecksumOffset] = (byte)(checksum >> 8);
        segment[TcpChecksumOffset + 1] = (byte)checksum;
    }

    private static string RepositoryRoot()
    {
        return HostToolProcess.FindRepositoryRoot();
    }

    private static MacAddress LocalMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x74, 0x01);
    }

    private static MacAddress RemoteMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x74, 0x80);
    }

    private static Ipv4Address LocalAddress()
    {
        return new Ipv4Address(192, 0, 2, 74);
    }

    private static Ipv4Address RemoteAddress()
    {
        return new Ipv4Address(198, 51, 100, 74);
    }

    private sealed class ExchangeBuffers
    {
        public ExchangeBuffers(
            byte[] host,
            byte[] path,
            byte[] synAckFrame,
            byte[] responseFrame,
            byte[] streamReceiveBuffer,
            byte[] synFrame,
            byte[] finalAckFrame,
            byte[] requestScratch,
            byte[] requestFrame,
            byte[] responseDestination,
            byte[] closeFrame)
        {
            Host = host;
            Path = path;
            SynAckFrame = synAckFrame;
            ResponseFrame = responseFrame;
            StreamReceiveBuffer = streamReceiveBuffer;
            SynFrame = synFrame;
            FinalAckFrame = finalAckFrame;
            RequestScratch = requestScratch;
            RequestFrame = requestFrame;
            ResponseDestination = responseDestination;
            CloseFrame = closeFrame;
        }

        public byte[] Host { get; }

        public byte[] Path { get; }

        public byte[] SynAckFrame { get; }

        public byte[] ResponseFrame { get; }

        public byte[] StreamReceiveBuffer { get; }

        public byte[] SynFrame { get; }

        public byte[] FinalAckFrame { get; }

        public byte[] RequestScratch { get; }

        public byte[] RequestFrame { get; }

        public byte[] ResponseDestination { get; }

        public byte[] CloseFrame { get; }
    }

    private sealed class SequenceTestSerialSink : ISerialSink
    {
        private readonly List<string> _lines = new List<string>();

        public int Count => _lines.Count;

        public void WriteLine(string line)
        {
            _lines.Add(line);
        }

        public string LineAt(int index)
        {
            return _lines[index];
        }

        public bool ContainsPrefix(string value)
        {
            for (int index = 0; index < _lines.Count; index++)
            {
                if (_lines[index].StartsWith(value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
