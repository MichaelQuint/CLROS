using System.Runtime.CompilerServices;
using System.Text;
using OS.Net;

internal static class HttpTcpClientGetPlannerTests
{
    private const ushort LocalPort = 49170;
    private const ushort RemotePort = 80;
    private const uint ClientInitialSequence = 0x2100_4000;
    private const uint ServerInitialSequence = 0x5000_7000;
    private const ushort TcpChecksum = 0x1234;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("net.http.tcp.get completes request response", TestCompletesRequestResponse, ref failures);
        Run("net.http.tcp.get writes request checksum", TestWritesRequestChecksum, ref failures);
        Run("net.http.tcp.get default result is not success", TestDefaultResultIsNotSuccess, ref failures);
        Run("net.http.tcp.get preserves tcp handshake failures", TestPreservesTcpHandshakeFailures, ref failures);
        Run("net.http.tcp.get rejects bad synack checksum", TestRejectsBadSynAckChecksum, ref failures);
        Run("net.http.tcp.get rejects request failures", TestRejectsRequestFailures, ref failures);
        Run("net.http.tcp.get rejects bad response checksum", TestRejectsBadResponseChecksum, ref failures);
        Run("net.http.tcp.get rejects response frame failures", TestRejectsResponseFrameFailures, ref failures);
        Run("net.http.tcp.get preserves http response failures", TestPreservesHttpResponseFailures, ref failures);
        Run("net.http.tcp.get rejects response buffer too small", TestRejectsResponseBufferTooSmall, ref failures);
        Run("net.http.tcp.get formatter formats success marker", TestFormatterFormatsSuccessMarker, ref failures);
        Run("net.http.tcp.get formatter names duplicate content length", TestFormatterNamesDuplicateContentLength, ref failures);
        Run("net.http.tcp.get formatter reports short destination", TestFormatterReportsShortDestination, ref failures);
        Run("net.http.tcp.get formatter default result is not success", TestFormatterDefaultResultIsNotSuccess, ref failures);
        Run(
            "net.http.tcp.get formatter uses shared ascii text builder",
            TestFormatterUsesSharedAsciiTextBuilder,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("HTTP TCP GET planner host tests failed: " + failures);
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

    private static bool TestCompletesRequestResponse()
    {
        byte[] receiveBuffer = new byte[512];
        HttpTcpClientGetPlanner planner = CreatePlanner(receiveBuffer);

        if (!Establish(ref planner))
        {
            return false;
        }

        byte[] host = Encoding.ASCII.GetBytes("example.test");
        byte[] path = Encoding.ASCII.GetBytes("/healthz");
        byte[] requestScratch = new byte[256];
        byte[] requestFrame = new byte[HttpTcpClientGetPlanner.HeaderBytes + requestScratch.Length];

        HttpTcpClientGetPlanStatus requestStatus = planner.TryWriteRequestFrame(
            host,
            path,
            requestScratch,
            requestFrame,
            out HttpTcpClientGetPlanResult requestResult);
        if (requestStatus != HttpTcpClientGetPlanStatus.Success
            || !requestResult.Succeeded
            || requestResult.State != HttpTcpClientGetPlannerState.RequestWritten
            || planner.State != HttpTcpClientGetPlannerState.RequestWritten
            || planner.HttpState != HttpClientGetTransactionState.RequestWritten
            || requestResult.RequestBytesWritten <= 0
            || requestResult.BytesWritten != HttpTcpClientGetPlanner.HeaderBytes + requestResult.RequestBytesWritten
            || requestResult.LocalNextSequenceNumber != ClientInitialSequence + 1 + (uint)requestResult.RequestBytesWritten
            || requestResult.RemoteNextSequenceNumber != ServerInitialSequence + 1)
        {
            return false;
        }

        if (!TryParseTcpFrame(
                requestFrame.AsSpan(0, requestResult.BytesWritten),
                out EthernetHeader requestEthernet,
                out Ipv4Header requestIpv4,
                out TcpHeader requestTcp,
                out ReadOnlySpan<byte> requestPayload))
        {
            return false;
        }

        string requestText = Encoding.ASCII.GetString(requestPayload);
        if (requestEthernet.Source != LocalMac()
            || requestEthernet.Destination != RemoteMac()
            || requestIpv4.Source != LocalAddress()
            || requestIpv4.Destination != RemoteAddress()
            || requestTcp.SourcePort != LocalPort
            || requestTcp.DestinationPort != RemotePort
            || requestTcp.SequenceNumber != ClientInitialSequence + 1
            || requestTcp.AcknowledgementNumber != ServerInitialSequence + 1
            || requestTcp.Flags != (TcpEstablishedStream.FlagPsh | TcpEstablishedStream.FlagAck)
            || requestTcp.Checksum == 0
            || requestText != "GET /healthz HTTP/1.1\r\n" +
                "Host: example.test\r\n" +
                "User-Agent: OS/0.1\r\n" +
                "Accept: */*\r\n" +
                "Connection: close\r\n" +
                "\r\n")
        {
            return false;
        }

        byte[] responsePayload = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Length: 5\r\nConnection: close\r\n\r\nhello");
        byte[] responseFrame = CreateServerPayloadFrame(
            RemoteMac(),
            LocalMac(),
            RemoteAddress(),
            LocalAddress(),
            RemotePort,
            LocalPort,
            ServerInitialSequence + 1,
            planner.LocalNextSequenceNumber,
            responsePayload);
        byte[] responseDestination = new byte[responsePayload.Length];

        HttpTcpClientGetPlanStatus responseStatus = planner.ReceiveResponseFrame(
            responseFrame,
            responseDestination,
            out HttpTcpClientGetPlanResult responseResult);

        return responseStatus == HttpTcpClientGetPlanStatus.Success
            && responseResult.Succeeded
            && responseResult.State == HttpTcpClientGetPlannerState.ResponseReceived
            && responseResult.StreamStatus == TcpEstablishedStreamStatus.Success
            && responseResult.HttpStatus == HttpClientGetTransactionStatus.Success
            && responseResult.ResponseStatus == HttpResponseParseStatus.Success
            && responseResult.HttpStatusCode == 200
            && responseResult.HttpBodyLength == 5
            && responseDestination[responseResult.HttpBodyOffset] == (byte)'h'
            && responseDestination[responseResult.HttpBodyOffset + 4] == (byte)'o'
            && planner.State == HttpTcpClientGetPlannerState.ResponseReceived
            && planner.HttpStatusCode == 200
            && planner.HttpBodyLength == 5
            && planner.RemoteNextSequenceNumber == ServerInitialSequence + 1 + (uint)responsePayload.Length;
    }

    private static bool TestWritesRequestChecksum()
    {
        byte[] receiveBuffer = new byte[512];
        HttpTcpClientGetPlanner planner = CreatePlanner(receiveBuffer);

        if (!Establish(ref planner))
        {
            return false;
        }

        byte[] host = Encoding.ASCII.GetBytes("example.test");
        byte[] path = Encoding.ASCII.GetBytes("/healthz");
        byte[] requestScratch = new byte[256];
        byte[] requestFrame = new byte[HttpTcpClientGetPlanner.HeaderBytes + requestScratch.Length];

        HttpTcpClientGetPlanStatus status = planner.TryWriteRequestFrame(
            host,
            path,
            requestScratch,
            requestFrame,
            out HttpTcpClientGetPlanResult result);
        if (status != HttpTcpClientGetPlanStatus.Success || !result.Succeeded)
        {
            return false;
        }

        if (!TryParseTcpFrame(
                requestFrame.AsSpan(0, result.BytesWritten),
                out EthernetHeader _,
                out Ipv4Header ipv4,
                out TcpHeader tcp,
                out ReadOnlySpan<byte> tcpSegment,
                out ReadOnlySpan<byte> payload))
        {
            return false;
        }

        Ipv4TransportChecksumStatus checksumStatus = Ipv4TransportChecksum.Validate(
            ipv4.Source,
            ipv4.Destination,
            TcpIpv4FramePlanner.ProtocolTcp,
            tcpSegment,
            out Ipv4TransportChecksumResult checksumResult);

        return result.TcpHeader.Checksum == tcp.Checksum
            && tcp.Checksum != 0
            && tcp.Checksum != TcpChecksum
            && checksumStatus == Ipv4TransportChecksumStatus.Success
            && checksumResult.IsValid
            && checksumResult.TransportLength == TcpHeader.MinHeaderLength + payload.Length;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        HttpTcpClientGetPlanResult defaultResult = default;

        byte[] successReceiveBuffer = new byte[128];
        HttpTcpClientGetPlanner successPlanner = CreatePlanner(successReceiveBuffer);
        byte[] synFrame = new byte[HttpTcpClientGetPlanner.HeaderBytes];
        HttpTcpClientGetPlanStatus successStatus = successPlanner.TryWriteSynFrame(
            synFrame,
            out HttpTcpClientGetPlanResult success);

        byte[] failureReceiveBuffer = new byte[128];
        HttpTcpClientGetPlanner failurePlanner = CreatePlanner(failureReceiveBuffer);
        byte[] host = Encoding.ASCII.GetBytes("example.test");
        byte[] path = Encoding.ASCII.GetBytes("/healthz");
        byte[] requestScratch = new byte[64];
        byte[] requestFrame = new byte[HttpTcpClientGetPlanner.HeaderBytes + requestScratch.Length];
        HttpTcpClientGetPlanStatus failureStatus = failurePlanner.TryWriteRequestFrame(
            host,
            path,
            requestScratch,
            requestFrame,
            out HttpTcpClientGetPlanResult failure);

        return !defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == HttpTcpClientGetPlanStatus.Success
            && defaultResult.State == HttpTcpClientGetPlannerState.Initial
            && defaultResult.TcpFrameStatus == TcpIpv4FramePlanStatus.Success
            && defaultResult.TcpStatus == TcpActiveConnectStatus.Success
            && defaultResult.StreamStatus == TcpEstablishedStreamStatus.Success
            && defaultResult.HttpStatus == HttpClientGetTransactionStatus.Success
            && defaultResult.RequestStatus == HttpRequestBuilderStatus.Success
            && defaultResult.ResponseStatus == HttpResponseParseStatus.Success
            && defaultResult.PacketStatus == PacketParseStatus.Success
            && defaultResult.BytesWritten == 0
            && defaultResult.TcpSegmentOffset == 0
            && defaultResult.TcpSegmentLength == 0
            && defaultResult.RequestBytesWritten == 0
            && defaultResult.ResponseBytesRead == 0
            && defaultResult.HttpStatusCode == 0
            && defaultResult.HttpBodyOffset == 0
            && defaultResult.HttpBodyLength == 0
            && defaultResult.LocalNextSequenceNumber == 0
            && defaultResult.RemoteNextSequenceNumber == 0
            && successStatus == HttpTcpClientGetPlanStatus.Success
            && success.IsInitialized
            && success.Succeeded
            && success.State == HttpTcpClientGetPlannerState.SynSent
            && success.TcpFrameStatus == TcpIpv4FramePlanStatus.Success
            && success.TcpStatus == TcpActiveConnectStatus.Success
            && success.PacketStatus == PacketParseStatus.Success
            && success.BytesWritten == HttpTcpClientGetPlanner.HeaderBytes
            && success.TcpSegmentOffset == HttpTcpClientGetPlanner.TcpSegmentOffset
            && success.TcpSegmentLength == TcpHeader.MinHeaderLength
            && success.LocalNextSequenceNumber == ClientInitialSequence + 1
            && success.RemoteNextSequenceNumber == 0
            && successPlanner.State == HttpTcpClientGetPlannerState.SynSent
            && failureStatus == HttpTcpClientGetPlanStatus.InvalidState
            && failure.IsInitialized
            && !failure.Succeeded
            && failure.Status == HttpTcpClientGetPlanStatus.InvalidState
            && failure.State == HttpTcpClientGetPlannerState.Initial
            && failure.PacketStatus == PacketParseStatus.Success
            && failure.RequestBytesWritten == 0
            && failurePlanner.State == HttpTcpClientGetPlannerState.Initial
            && !RuntimeHelpers.IsReferenceOrContainsReferences<HttpTcpClientGetPlanResult>();
    }

    private static bool TestPreservesTcpHandshakeFailures()
    {
        byte[] receiveBuffer = new byte[128];
        HttpTcpClientGetPlanner planner = CreatePlanner(receiveBuffer);
        byte[] synFrame = new byte[HttpTcpClientGetPlanner.HeaderBytes];
        if (planner.TryWriteSynFrame(synFrame, out HttpTcpClientGetPlanResult _) !=
            HttpTcpClientGetPlanStatus.Success)
        {
            return false;
        }

        byte[] wrongAck = CreateServerSynAckFrame(
            RemoteMac(),
            LocalMac(),
            RemoteAddress(),
            LocalAddress(),
            RemotePort,
            LocalPort,
            ServerInitialSequence,
            ClientInitialSequence + 2,
            TcpActiveConnectTransaction.FlagSyn | TcpActiveConnectTransaction.FlagAck);
        HttpTcpClientGetPlanStatus status = planner.ReceiveSynAckFrame(
            wrongAck,
            out HttpTcpClientGetPlanResult result);

        return status == HttpTcpClientGetPlanStatus.WrongAcknowledgementNumber
            && !result.Succeeded
            && result.TcpFrameStatus == TcpIpv4FramePlanStatus.WrongAcknowledgementNumber
            && result.TcpStatus == TcpActiveConnectStatus.WrongAcknowledgementNumber
            && result.TcpHeader.AcknowledgementNumber == ClientInitialSequence + 2
            && planner.State == HttpTcpClientGetPlannerState.Failed;
    }

    private static bool TestRejectsBadSynAckChecksum()
    {
        byte[] receiveBuffer = new byte[128];
        HttpTcpClientGetPlanner planner = CreatePlanner(receiveBuffer);
        byte[] synFrame = new byte[HttpTcpClientGetPlanner.HeaderBytes];
        if (planner.TryWriteSynFrame(synFrame, out HttpTcpClientGetPlanResult _) !=
            HttpTcpClientGetPlanStatus.Success)
        {
            return false;
        }

        byte[] badChecksum = CreateServerSynAckFrame(
            RemoteMac(),
            LocalMac(),
            RemoteAddress(),
            LocalAddress(),
            RemotePort,
            LocalPort,
            ServerInitialSequence,
            ClientInitialSequence + 1,
            TcpActiveConnectTransaction.FlagSyn | TcpActiveConnectTransaction.FlagAck);
        badChecksum[HttpTcpClientGetPlanner.TcpSegmentOffset + TcpChecksumOffset] ^= 0x01;

        HttpTcpClientGetPlanStatus status = planner.ReceiveSynAckFrame(
            badChecksum,
            out HttpTcpClientGetPlanResult result);

        return status == HttpTcpClientGetPlanStatus.TcpFrameFailed
            && !result.Succeeded
            && result.TcpFrameStatus == TcpIpv4FramePlanStatus.BadTcpChecksum
            && result.TcpStatus == TcpActiveConnectStatus.Success
            && result.PacketStatus == PacketParseStatus.BadChecksum
            && result.TcpHeader.SourcePort == RemotePort
            && result.TcpHeader.DestinationPort == LocalPort
            && result.State == HttpTcpClientGetPlannerState.SynSent
            && planner.State == HttpTcpClientGetPlannerState.SynSent;
    }

    private static bool TestRejectsRequestFailures()
    {
        byte[] receiveBuffer = new byte[128];
        HttpTcpClientGetPlanner planner = CreatePlanner(receiveBuffer);
        byte[] host = Encoding.ASCII.GetBytes("example.test");
        byte[] invalidHost = Encoding.ASCII.GetBytes("bad host");
        byte[] path = Encoding.ASCII.GetBytes("/healthz");
        byte[] requestScratch = new byte[16];
        byte[] frame = new byte[HttpTcpClientGetPlanner.HeaderBytes + 64];

        HttpTcpClientGetPlanStatus earlyStatus = planner.TryWriteRequestFrame(
            host,
            path,
            requestScratch,
            frame,
            out HttpTcpClientGetPlanResult earlyResult);
        if (earlyStatus != HttpTcpClientGetPlanStatus.InvalidState
            || earlyResult.State != HttpTcpClientGetPlannerState.Initial)
        {
            return false;
        }

        if (!Establish(ref planner))
        {
            return false;
        }

        HttpTcpClientGetPlanStatus invalidHostStatus = planner.TryWriteRequestFrame(
            invalidHost,
            path,
            requestScratch,
            frame,
            out HttpTcpClientGetPlanResult invalidHostResult);
        if (invalidHostStatus != HttpTcpClientGetPlanStatus.HttpRequestFailed
            || invalidHostResult.HttpStatus != HttpClientGetTransactionStatus.InvalidHost
            || invalidHostResult.RequestStatus != HttpRequestBuilderStatus.InvalidHost
            || planner.State != HttpTcpClientGetPlannerState.Established)
        {
            return false;
        }

        HttpTcpClientGetPlanStatus smallStatus = planner.TryWriteRequestFrame(
            host,
            path,
            requestScratch,
            frame,
            out HttpTcpClientGetPlanResult smallResult);

        return smallStatus == HttpTcpClientGetPlanStatus.RequestBufferTooSmall
            && smallResult.HttpStatus == HttpClientGetTransactionStatus.RequestBufferTooSmall
            && smallResult.RequestStatus == HttpRequestBuilderStatus.DestinationTooSmall
            && planner.State == HttpTcpClientGetPlannerState.Established;
    }

    private static bool TestRejectsBadResponseChecksum()
    {
        byte[] receiveBuffer = new byte[512];
        HttpTcpClientGetPlanner planner = PreparedRequestPlanner(receiveBuffer);
        byte[] responsePayload = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n");
        uint localNextSequenceNumber = planner.LocalNextSequenceNumber;
        uint remoteNextSequenceNumber = planner.RemoteNextSequenceNumber;

        byte[] badChecksum = CreateServerPayloadFrame(
            RemoteMac(),
            LocalMac(),
            RemoteAddress(),
            LocalAddress(),
            RemotePort,
            LocalPort,
            ServerInitialSequence + 1,
            localNextSequenceNumber,
            responsePayload);
        badChecksum[HttpTcpClientGetPlanner.TcpSegmentOffset + TcpChecksumOffset] ^= 0x01;

        HttpTcpClientGetPlanStatus status = planner.ReceiveResponseFrame(
            badChecksum,
            new byte[responsePayload.Length],
            out HttpTcpClientGetPlanResult result);

        return status == HttpTcpClientGetPlanStatus.TcpFrameFailed
            && !result.Succeeded
            && result.TcpFrameStatus == TcpIpv4FramePlanStatus.BadTcpChecksum
            && result.TcpStatus == TcpActiveConnectStatus.Success
            && result.StreamStatus == TcpEstablishedStreamStatus.Success
            && result.PacketStatus == PacketParseStatus.BadChecksum
            && result.TcpHeader.SourcePort == RemotePort
            && result.TcpHeader.DestinationPort == LocalPort
            && result.TcpSegmentLength == badChecksum.Length - HttpTcpClientGetPlanner.TcpSegmentOffset
            && result.State == HttpTcpClientGetPlannerState.RequestWritten
            && result.LocalNextSequenceNumber == localNextSequenceNumber
            && result.RemoteNextSequenceNumber == remoteNextSequenceNumber
            && planner.State == HttpTcpClientGetPlannerState.RequestWritten
            && planner.LocalNextSequenceNumber == localNextSequenceNumber
            && planner.RemoteNextSequenceNumber == remoteNextSequenceNumber;
    }

    private static bool TestRejectsResponseFrameFailures()
    {
        byte[] receiveBuffer = new byte[512];
        HttpTcpClientGetPlanner wrongPeerPlanner = PreparedRequestPlanner(receiveBuffer);
        byte[] responsePayload = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n");

        byte[] wrongPeer = CreateServerPayloadFrame(
            RemoteMac(),
            LocalMac(),
            new Ipv4Address(203, 0, 113, 8),
            LocalAddress(),
            RemotePort,
            LocalPort,
            ServerInitialSequence + 1,
            wrongPeerPlanner.LocalNextSequenceNumber,
            responsePayload);
        HttpTcpClientGetPlanStatus wrongPeerStatus = wrongPeerPlanner.ReceiveResponseFrame(
            wrongPeer,
            new byte[responsePayload.Length],
            out HttpTcpClientGetPlanResult wrongPeerResult);

        byte[] receiveBuffer2 = new byte[512];
        HttpTcpClientGetPlanner wrongAckPlanner = PreparedRequestPlanner(receiveBuffer2);
        byte[] wrongAck = CreateServerPayloadFrame(
            RemoteMac(),
            LocalMac(),
            RemoteAddress(),
            LocalAddress(),
            RemotePort,
            LocalPort,
            ServerInitialSequence + 1,
            wrongAckPlanner.LocalNextSequenceNumber + 1,
            responsePayload);
        HttpTcpClientGetPlanStatus wrongAckStatus = wrongAckPlanner.ReceiveResponseFrame(
            wrongAck,
            new byte[responsePayload.Length],
            out HttpTcpClientGetPlanResult wrongAckResult);

        return wrongPeerStatus == HttpTcpClientGetPlanStatus.WrongRemoteAddress
            && wrongPeerResult.PacketStatus == PacketParseStatus.Success
            && wrongPeerPlanner.State == HttpTcpClientGetPlannerState.RequestWritten
            && wrongAckStatus == HttpTcpClientGetPlanStatus.WrongAcknowledgementNumber
            && wrongAckResult.TcpHeader.AcknowledgementNumber == wrongAckPlanner.LocalNextSequenceNumber + 1
            && wrongAckPlanner.State == HttpTcpClientGetPlannerState.RequestWritten;
    }

    private static bool TestPreservesHttpResponseFailures()
    {
        byte[] receiveBuffer = new byte[512];
        HttpTcpClientGetPlanner planner = PreparedRequestPlanner(receiveBuffer);
        byte[] chunkedPayload = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n0\r\n\r\n");
        byte[] frame = CreateServerPayloadFrame(
            RemoteMac(),
            LocalMac(),
            RemoteAddress(),
            LocalAddress(),
            RemotePort,
            LocalPort,
            ServerInitialSequence + 1,
            planner.LocalNextSequenceNumber,
            chunkedPayload);
        byte[] responseDestination = new byte[chunkedPayload.Length];

        HttpTcpClientGetPlanStatus status = planner.ReceiveResponseFrame(
            frame,
            responseDestination,
            out HttpTcpClientGetPlanResult result);

        return status == HttpTcpClientGetPlanStatus.HttpResponseFailed
            && !result.Succeeded
            && result.StreamStatus == TcpEstablishedStreamStatus.Success
            && result.HttpStatus == HttpClientGetTransactionStatus.UnsupportedTransferEncoding
            && result.ResponseStatus == HttpResponseParseStatus.UnsupportedTransferEncoding
            && result.ResponseBytesRead == chunkedPayload.Length
            && planner.State == HttpTcpClientGetPlannerState.Failed;
    }

    private static bool TestRejectsResponseBufferTooSmall()
    {
        byte[] receiveBuffer = new byte[512];
        HttpTcpClientGetPlanner planner = PreparedRequestPlanner(receiveBuffer);
        byte[] responsePayload = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello");
        byte[] responseFrame = CreateServerPayloadFrame(
            RemoteMac(),
            LocalMac(),
            RemoteAddress(),
            LocalAddress(),
            RemotePort,
            LocalPort,
            ServerInitialSequence + 1,
            planner.LocalNextSequenceNumber,
            responsePayload);
        byte[] responseDestination = new byte[responsePayload.Length - 1];

        HttpTcpClientGetPlanStatus status = planner.ReceiveResponseFrame(
            responseFrame,
            responseDestination,
            out HttpTcpClientGetPlanResult result);

        return status == HttpTcpClientGetPlanStatus.ResponseBufferTooSmall
            && !result.Succeeded
            && result.StreamStatus == TcpEstablishedStreamStatus.Success
            && result.PacketStatus == PacketParseStatus.Success
            && planner.State == HttpTcpClientGetPlannerState.RequestWritten;
    }

    private static bool TestFormatterFormatsSuccessMarker()
    {
        byte[] receiveBuffer = new byte[512];
        HttpTcpClientGetPlanner planner = PreparedRequestPlanner(receiveBuffer);
        byte[] responsePayload = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Length: 5\r\nConnection: close\r\n\r\nhello");
        byte[] responseFrame = CreateServerPayloadFrame(
            RemoteMac(),
            LocalMac(),
            RemoteAddress(),
            LocalAddress(),
            RemotePort,
            LocalPort,
            ServerInitialSequence + 1,
            planner.LocalNextSequenceNumber,
            responsePayload);
        byte[] responseDestination = new byte[responsePayload.Length];

        HttpTcpClientGetPlanStatus status = planner.ReceiveResponseFrame(
            responseFrame,
            responseDestination,
            out HttpTcpClientGetPlanResult result);
        if (status != HttpTcpClientGetPlanStatus.Success || !result.Succeeded)
        {
            return false;
        }

        Span<byte> destination = stackalloc byte[512];
        destination.Fill(0);
        HttpTcpClientGetPlanDiagnosticFormatResult query =
            HttpTcpClientGetPlanDiagnosticFormatter.QueryRequiredBytes(result);
        HttpTcpClientGetPlanDiagnosticFormatResult format =
            HttpTcpClientGetPlanDiagnosticFormatter.FormatResult(result, destination);
        string text = Encoding.ASCII.GetString(destination[..format.BytesWritten]);
        string expected = "HTTP_TCP_GET_PLAN status=Success state=ResponseReceived tcpFrame=Success " +
            "tcp=Success stream=Success http=Success request=Success response=Success packet=Success " +
            "bytes=0 tcpSegmentOffset=" + result.TcpSegmentOffset +
            " tcpSegmentBytes=" + result.TcpSegmentLength +
            " requestBytes=0 responseBytes=" + result.ResponseBytesRead +
            " httpStatus=200 bodyOffset=" + result.HttpBodyOffset +
            " bodyBytes=5 localSeq=" + result.LocalNextSequenceNumber +
            " remoteSeq=" + result.RemoteNextSequenceNumber;

        return query.IsInitialized
            && !query.Succeeded
            && query.Status == HttpTcpClientGetPlanDiagnosticFormatStatus.Success
            && query.PlanStatus == HttpTcpClientGetPlanStatus.Success
            && query.BytesWritten == 0
            && query.RequiredBytes == expected.Length
            && query.AvailableBytes == 0
            && format.Succeeded
            && format.PlanStatus == HttpTcpClientGetPlanStatus.Success
            && format.BytesWritten == expected.Length
            && format.RequiredBytes == expected.Length
            && format.AvailableBytes == destination.Length
            && text == expected;
    }

    private static bool TestFormatterNamesDuplicateContentLength()
    {
        byte[] receiveBuffer = new byte[512];
        HttpTcpClientGetPlanner planner = PreparedRequestPlanner(receiveBuffer);
        byte[] responsePayload = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Length: 5\r\nContent-Length: 5\r\n\r\nhello");
        byte[] responseFrame = CreateServerPayloadFrame(
            RemoteMac(),
            LocalMac(),
            RemoteAddress(),
            LocalAddress(),
            RemotePort,
            LocalPort,
            ServerInitialSequence + 1,
            planner.LocalNextSequenceNumber,
            responsePayload);
        byte[] responseDestination = new byte[responsePayload.Length];

        HttpTcpClientGetPlanStatus status = planner.ReceiveResponseFrame(
            responseFrame,
            responseDestination,
            out HttpTcpClientGetPlanResult result);
        if (status != HttpTcpClientGetPlanStatus.HttpResponseFailed ||
            result.HttpStatus != HttpClientGetTransactionStatus.MalformedResponse ||
            result.ResponseStatus != HttpResponseParseStatus.DuplicateContentLength)
        {
            return false;
        }

        Span<byte> destination = stackalloc byte[512];
        destination.Fill(0);
        HttpTcpClientGetPlanDiagnosticFormatResult query =
            HttpTcpClientGetPlanDiagnosticFormatter.QueryRequiredBytes(result);
        HttpTcpClientGetPlanDiagnosticFormatResult format =
            HttpTcpClientGetPlanDiagnosticFormatter.FormatResult(result, destination);
        string text = Encoding.ASCII.GetString(destination[..format.BytesWritten]);

        return query.IsInitialized
            && !query.Succeeded
            && query.Status == HttpTcpClientGetPlanDiagnosticFormatStatus.Success
            && query.PlanStatus == HttpTcpClientGetPlanStatus.HttpResponseFailed
            && format.Succeeded
            && format.PlanStatus == HttpTcpClientGetPlanStatus.HttpResponseFailed
            && text.Contains("status=HttpResponseFailed", StringComparison.Ordinal)
            && text.Contains("http=MalformedResponse", StringComparison.Ordinal)
            && text.Contains("response=DuplicateContentLength", StringComparison.Ordinal)
            && !text.Contains("response=Unknown", StringComparison.Ordinal);
    }

    private static bool TestFormatterReportsShortDestination()
    {
        byte[] receiveBuffer = new byte[512];
        HttpTcpClientGetPlanner planner = PreparedRequestPlanner(receiveBuffer);
        byte[] responsePayload = Encoding.ASCII.GetBytes(
            "HTTP/1.1 204 No Content\r\nContent-Length: 0\r\n\r\n");
        byte[] responseFrame = CreateServerPayloadFrame(
            RemoteMac(),
            LocalMac(),
            RemoteAddress(),
            LocalAddress(),
            RemotePort,
            LocalPort,
            ServerInitialSequence + 1,
            planner.LocalNextSequenceNumber,
            responsePayload);
        byte[] responseDestination = new byte[responsePayload.Length];

        HttpTcpClientGetPlanStatus status = planner.ReceiveResponseFrame(
            responseFrame,
            responseDestination,
            out HttpTcpClientGetPlanResult result);
        if (status != HttpTcpClientGetPlanStatus.Success || !result.Succeeded)
        {
            return false;
        }

        HttpTcpClientGetPlanDiagnosticFormatResult query =
            HttpTcpClientGetPlanDiagnosticFormatter.QueryRequiredBytes(result);

        Span<byte> destination = stackalloc byte[12];
        destination.Fill(0xA5);
        HttpTcpClientGetPlanDiagnosticFormatResult shortResult =
            HttpTcpClientGetPlanDiagnosticFormatter.FormatResult(result, destination);

        return shortResult.IsInitialized
            && !shortResult.Succeeded
            && shortResult.Status == HttpTcpClientGetPlanDiagnosticFormatStatus.DestinationTooSmall
            && shortResult.PlanStatus == HttpTcpClientGetPlanStatus.Success
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

        HttpTcpClientGetPlanResult defaultPlanResult = default;
        HttpTcpClientGetPlanDiagnosticFormatResult invalidFormat =
            HttpTcpClientGetPlanDiagnosticFormatter.FormatResult(defaultPlanResult, destination);
        HttpTcpClientGetPlanDiagnosticFormatResult invalidQuery =
            HttpTcpClientGetPlanDiagnosticFormatter.QueryRequiredBytes(defaultPlanResult);
        HttpTcpClientGetPlanDiagnosticFormatResult defaultFormat = default;

        return invalidFormat.IsInitialized
            && !invalidFormat.Succeeded
            && invalidFormat.Status == HttpTcpClientGetPlanDiagnosticFormatStatus.InvalidResult
            && invalidFormat.PlanStatus == HttpTcpClientGetPlanStatus.Success
            && invalidFormat.BytesWritten == 0
            && invalidFormat.RequiredBytes == 0
            && invalidFormat.AvailableBytes == destination.Length
            && invalidQuery.IsInitialized
            && !invalidQuery.Succeeded
            && invalidQuery.Status == HttpTcpClientGetPlanDiagnosticFormatStatus.InvalidResult
            && invalidQuery.RequiredBytes == 0
            && !defaultFormat.IsInitialized
            && !defaultFormat.Succeeded
            && defaultFormat.Status == HttpTcpClientGetPlanDiagnosticFormatStatus.Success
            && defaultFormat.PlanStatus == HttpTcpClientGetPlanStatus.Success
            && defaultFormat.RequiredBytes == 0
            && defaultFormat.AvailableBytes == 0
            && destination[0] == 0xCC
            && destination[63] == 0xCC
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                HttpTcpClientGetPlanDiagnosticFormatResult>();
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
                "HttpTcpClientGetPlanDiagnosticFormatter.cs"));

        return source.Contains("using OS.Std.Text;", StringComparison.Ordinal)
            && source.Contains(
                "AsciiDecimalFormatter.CountInt64Digits(result.BytesWritten)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiDecimalFormatter.CountInt64Digits(result.TcpSegmentOffset)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiDecimalFormatter.CountUInt64Digits(result.HttpStatusCode)",
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

    private static HttpTcpClientGetPlanner PreparedRequestPlanner(byte[] receiveBuffer)
    {
        HttpTcpClientGetPlanner planner = CreatePlanner(receiveBuffer);
        if (!Establish(ref planner))
        {
            throw new InvalidOperationException("Failed to establish HTTP TCP planner.");
        }

        byte[] host = Encoding.ASCII.GetBytes("example.test");
        byte[] path = Encoding.ASCII.GetBytes("/healthz");
        byte[] requestScratch = new byte[256];
        byte[] frame = new byte[HttpTcpClientGetPlanner.HeaderBytes + requestScratch.Length];
        if (planner.TryWriteRequestFrame(
                host,
                path,
                requestScratch,
                frame,
                out HttpTcpClientGetPlanResult result) != HttpTcpClientGetPlanStatus.Success
            || !result.Succeeded)
        {
            throw new InvalidOperationException("Failed to write HTTP GET request frame.");
        }

        return planner;
    }

    private static bool Establish(ref HttpTcpClientGetPlanner planner)
    {
        byte[] synFrame = new byte[HttpTcpClientGetPlanner.HeaderBytes];
        if (planner.TryWriteSynFrame(
                synFrame,
                out HttpTcpClientGetPlanResult synResult) != HttpTcpClientGetPlanStatus.Success
            || !synResult.Succeeded)
        {
            return false;
        }

        byte[] synAckFrame = CreateServerSynAckFrame(
            RemoteMac(),
            LocalMac(),
            RemoteAddress(),
            LocalAddress(),
            RemotePort,
            LocalPort,
            ServerInitialSequence,
            ClientInitialSequence + 1,
            TcpActiveConnectTransaction.FlagSyn | TcpActiveConnectTransaction.FlagAck);
        if (planner.ReceiveSynAckFrame(
                synAckFrame,
                out HttpTcpClientGetPlanResult synAckResult) != HttpTcpClientGetPlanStatus.Success
            || !synAckResult.Succeeded)
        {
            return false;
        }

        byte[] finalAckFrame = new byte[HttpTcpClientGetPlanner.HeaderBytes];
        return planner.TryWriteFinalAckFrame(
                finalAckFrame,
                out HttpTcpClientGetPlanResult finalAckResult) == HttpTcpClientGetPlanStatus.Success
            && finalAckResult.Succeeded
            && finalAckResult.State == HttpTcpClientGetPlannerState.Established
            && planner.State == HttpTcpClientGetPlannerState.Established;
    }

    private static HttpTcpClientGetPlanner CreatePlanner(byte[] receiveBuffer)
    {
        return HttpTcpClientGetPlanner.Create(
            new HttpTcpClientGetPlannerInfo(CreateTcpInfo()),
            receiveBuffer);
    }

    private static TcpIpv4FramePlannerInfo CreateTcpInfo()
    {
        return new TcpIpv4FramePlannerInfo(
            LocalMac(),
            RemoteMac(),
            LocalAddress(),
            RemoteAddress(),
            LocalPort,
            RemotePort,
            ClientInitialSequence,
            ServerInitialSequence,
            requiresRemoteInitialSequenceNumber: true,
            windowSize: 0x3000,
            timeToLive: 42,
            identification: 0x228,
            tcpChecksum: TcpChecksum);
    }

    private static byte[] CreateServerSynAckFrame(
        MacAddress sourceMac,
        MacAddress destinationMac,
        Ipv4Address sourceAddress,
        Ipv4Address destinationAddress,
        ushort sourcePort,
        ushort destinationPort,
        uint sequenceNumber,
        uint acknowledgementNumber,
        byte flags)
    {
        return CreateServerPayloadFrame(
            sourceMac,
            destinationMac,
            sourceAddress,
            destinationAddress,
            sourcePort,
            destinationPort,
            sequenceNumber,
            acknowledgementNumber,
            ReadOnlySpan<byte>.Empty,
            flags);
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
        return TryParseTcpFrame(
            frame,
            out ethernet,
            out ipv4,
            out tcp,
            out ReadOnlySpan<byte> _,
            out payload);
    }

    private static bool TryParseTcpFrame(
        ReadOnlySpan<byte> frame,
        out EthernetHeader ethernet,
        out Ipv4Header ipv4,
        out TcpHeader tcp,
        out ReadOnlySpan<byte> tcpSegment,
        out ReadOnlySpan<byte> payload)
    {
        tcp = default;
        ipv4 = default;
        tcpSegment = default;
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

        tcpSegment = ipv4Payload;
        return TcpHeader.TryParse(
                tcpSegment,
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

    private const int TcpChecksumOffset = 16;

    private static MacAddress LocalMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x22, 0x01);
    }

    private static MacAddress RemoteMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x22, 0x80);
    }

    private static Ipv4Address LocalAddress()
    {
        return new Ipv4Address(192, 0, 2, 170);
    }

    private static Ipv4Address RemoteAddress()
    {
        return new Ipv4Address(198, 51, 100, 80);
    }
}
