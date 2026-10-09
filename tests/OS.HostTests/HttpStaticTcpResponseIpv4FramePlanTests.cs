using System.Runtime.CompilerServices;
using System.Text;
using OS.Net;

internal static class HttpStaticTcpResponseIpv4FramePlanTests
{
    private const int TcpSegmentOffset = 32;
    private const ushort LocalPort = 8080;
    private const ushort RemotePort = 49152;
    private const uint LocalNextSequence = 0x10203040;
    private const uint RemoteNextSequence = 0x50607080;

    [HostTest("net.http.static.tcp.ipv4 writes 200 response frame", HostLane.Fast, 0)]
    private static bool TestWrites200ResponseFrame()
    {
        byte[] request = Encoding.ASCII.GetBytes(
            "GET /healthz HTTP/1.1\r\nHost: os.test\r\nConnection: close\r\n\r\n");
        byte[] receive = CreateReceiveBuffer(request);
        TcpReceiveDispatchResult dispatch = CreatePayloadDispatch(request.Length);
        byte[] responseScratch = new byte[256];
        Span<byte> streamScratch = stackalloc byte[16];
        byte[] frame = new byte[TcpEstablishedStreamIpv4FramePlanner.HeaderBytes + 256];

        HttpStaticTcpResponseIpv4FramePlanStatus status =
            HttpStaticTcpResponseIpv4FramePlan.TryWriteResponseFrame(
                receive,
                dispatch,
                CreateFrameInfo(maximumPayloadLength: 256),
                "/healthz"u8,
                "OS_MINIMAL_API_OK"u8,
                "missing"u8,
                "bad method"u8,
                "text/plain"u8,
                responseScratch,
                streamScratch,
                frame,
                out HttpStaticTcpResponseIpv4FramePlanResult result);

        if (status != HttpStaticTcpResponseIpv4FramePlanStatus.Success ||
            !result.Succeeded ||
            result.Response.Status != HttpStaticTcpResponsePlanStatus.Success ||
            result.Response.HttpStatusCode != 200 ||
            result.Frame.Status != TcpEstablishedStreamIpv4FramePlanStatus.Success ||
            result.PayloadBytes != result.ResponseBytesWritten ||
            result.FrameBytesWritten != TcpEstablishedStreamIpv4FramePlanner.HeaderBytes + result.ResponseBytesWritten ||
            result.LocalNextSequenceNumber != LocalNextSequence + (uint)result.ResponseBytesWritten ||
            result.RemoteNextSequenceNumber != RemoteNextSequence + (uint)request.Length)
        {
            return HostCheck.Result(false);
        }

        return HostCheck.Result(ValidateResponseFrame(
            frame.AsSpan(0, result.FrameBytesWritten),
            result.ResponseBytesWritten,
            LocalNextSequence,
            RemoteNextSequence + (uint)request.Length,
            "HTTP/1.1 200 OK\r\n",
            "OS_MINIMAL_API_OK"));
    }

    [HostTest("net.http.static.tcp.ipv4 writes 404 and 405 frames", HostLane.Fast, 1)]
    private static bool TestWrites404And405Frames()
    {
        byte[] missingRequest = Encoding.ASCII.GetBytes("GET /missing HTTP/1.1\r\nHost: os.test\r\n\r\n");
        byte[] missingReceive = CreateReceiveBuffer(missingRequest);
        byte[] missingScratch = new byte[256];
        byte[] missingFrame = new byte[TcpEstablishedStreamIpv4FramePlanner.HeaderBytes + 256];
        Span<byte> missingStreamScratch = stackalloc byte[16];
        HttpStaticTcpResponseIpv4FramePlanStatus missingStatus =
            HttpStaticTcpResponseIpv4FramePlan.TryWriteResponseFrame(
                missingReceive,
                CreatePayloadDispatch(missingRequest.Length),
                CreateFrameInfo(maximumPayloadLength: 256),
                "/healthz"u8,
                "OK"u8,
                "not found"u8,
                "bad method"u8,
                "text/plain"u8,
                missingScratch,
                missingStreamScratch,
                missingFrame,
                out HttpStaticTcpResponseIpv4FramePlanResult missing);

        byte[] postRequest = Encoding.ASCII.GetBytes(
            "POST /healthz HTTP/1.1\r\nHost: os.test\r\nContent-Length: 0\r\n\r\n");
        byte[] postReceive = CreateReceiveBuffer(postRequest);
        byte[] postScratch = new byte[256];
        byte[] postFrame = new byte[TcpEstablishedStreamIpv4FramePlanner.HeaderBytes + 256];
        Span<byte> postStreamScratch = stackalloc byte[16];
        HttpStaticTcpResponseIpv4FramePlanStatus postStatus =
            HttpStaticTcpResponseIpv4FramePlan.TryWriteResponseFrame(
                postReceive,
                CreatePayloadDispatch(postRequest.Length),
                CreateFrameInfo(maximumPayloadLength: 256),
                "/healthz"u8,
                "OK"u8,
                "missing"u8,
                "bad method"u8,
                "text/plain"u8,
                postScratch,
                postStreamScratch,
                postFrame,
                out HttpStaticTcpResponseIpv4FramePlanResult post);

        return HostCheck.Result(missingStatus == HttpStaticTcpResponseIpv4FramePlanStatus.Success
            && postStatus == HttpStaticTcpResponseIpv4FramePlanStatus.Success
            && missing.Response.HttpStatusCode == 404
            && post.Response.HttpStatusCode == 405
            && ValidateResponseFrame(
                missingFrame.AsSpan(0, missing.FrameBytesWritten),
                missing.ResponseBytesWritten,
                LocalNextSequence,
                RemoteNextSequence + (uint)missingRequest.Length,
                "HTTP/1.1 404 Not Found\r\n",
                "not found")
            && ValidateResponseFrame(
                postFrame.AsSpan(0, post.FrameBytesWritten),
                post.ResponseBytesWritten,
                LocalNextSequence,
                RemoteNextSequence + (uint)postRequest.Length,
                "HTTP/1.1 405 Method Not Allowed\r\n",
                "bad method"));
    }

    [HostTest("net.http.static.tcp.ipv4 rejects response buffer failure", HostLane.Fast, 2)]
    private static bool TestRejectsResponseBufferFailure()
    {
        byte[] request = Encoding.ASCII.GetBytes("GET /healthz HTTP/1.1\r\nHost: os.test\r\n\r\n");
        byte[] receive = CreateReceiveBuffer(request);
        Span<byte> responseScratch = stackalloc byte[16];
        responseScratch.Fill(0xcc);
        Span<byte> streamScratch = stackalloc byte[16];
        Span<byte> frame = stackalloc byte[128];
        frame.Fill(0xcc);

        HttpStaticTcpResponseIpv4FramePlanStatus status =
            HttpStaticTcpResponseIpv4FramePlan.TryWriteResponseFrame(
                receive,
                CreatePayloadDispatch(request.Length),
                CreateFrameInfo(maximumPayloadLength: 256),
                "/healthz"u8,
                "OS_MINIMAL_API_OK"u8,
                "missing"u8,
                "bad method"u8,
                "text/plain"u8,
                responseScratch,
                streamScratch,
                frame,
                out HttpStaticTcpResponseIpv4FramePlanResult result);

        return HostCheck.Result(status == HttpStaticTcpResponseIpv4FramePlanStatus.ResponsePlanFailed
            && !result.Succeeded
            && result.Response.Status == HttpStaticTcpResponsePlanStatus.HttpTransactionFailed
            && result.Response.HttpStatus == HttpStaticResponseTransactionStatus.ResponseWriteFailed
            && !result.Frame.IsInitialized
            && AllBytesEqual(responseScratch, 0xcc)
            && AllBytesEqual(frame, 0xcc));
    }

    [HostTest("net.http.static.tcp.ipv4 rejects response larger than maximum payload", HostLane.Fast, 3)]
    private static bool TestRejectsResponseLargerThanMaximumPayload()
    {
        byte[] request = Encoding.ASCII.GetBytes("GET /healthz HTTP/1.1\r\nHost: os.test\r\n\r\n");
        byte[] receive = CreateReceiveBuffer(request);
        byte[] responseScratch = new byte[256];
        Span<byte> streamScratch = stackalloc byte[16];
        Span<byte> frame = stackalloc byte[128];
        frame.Fill(0xcc);

        HttpStaticTcpResponseIpv4FramePlanStatus status =
            HttpStaticTcpResponseIpv4FramePlan.TryWriteResponseFrame(
                receive,
                CreatePayloadDispatch(request.Length),
                CreateFrameInfo(maximumPayloadLength: 8),
                "/healthz"u8,
                "OS_MINIMAL_API_OK"u8,
                "missing"u8,
                "bad method"u8,
                "text/plain"u8,
                responseScratch,
                streamScratch,
                frame,
                out HttpStaticTcpResponseIpv4FramePlanResult result);

        return HostCheck.Result(status == HttpStaticTcpResponseIpv4FramePlanStatus.ResponseExceedsMaximumPayload
            && !result.Succeeded
            && result.Response.Succeeded
            && result.Response.ResponseBytesWritten > 8
            && !result.Frame.IsInitialized
            && AllBytesEqual(frame, 0xcc));
    }

    [HostTest("net.http.static.tcp.ipv4 rejects frame write failure", HostLane.Fast, 4)]
    private static bool TestRejectsFrameWriteFailure()
    {
        byte[] request = Encoding.ASCII.GetBytes("GET /healthz HTTP/1.1\r\nHost: os.test\r\n\r\n");
        byte[] receive = CreateReceiveBuffer(request);
        byte[] responseScratch = new byte[256];
        Span<byte> streamScratch = stackalloc byte[16];
        Span<byte> frame = stackalloc byte[TcpEstablishedStreamIpv4FramePlanner.HeaderBytes - 1];
        frame.Fill(0xcc);

        HttpStaticTcpResponseIpv4FramePlanStatus status =
            HttpStaticTcpResponseIpv4FramePlan.TryWriteResponseFrame(
                receive,
                CreatePayloadDispatch(request.Length),
                CreateFrameInfo(maximumPayloadLength: 256),
                "/healthz"u8,
                "OK"u8,
                "missing"u8,
                "bad method"u8,
                "text/plain"u8,
                responseScratch,
                streamScratch,
                frame,
                out HttpStaticTcpResponseIpv4FramePlanResult result);

        return HostCheck.Result(status == HttpStaticTcpResponseIpv4FramePlanStatus.FramePlanFailed
            && !result.Succeeded
            && result.Response.Succeeded
            && result.Frame.Status == TcpEstablishedStreamIpv4FramePlanStatus.FrameBufferTooSmall
            && result.Frame.StreamStatus == TcpEstablishedStreamStatus.DestinationTooSmall
            && AllBytesEqual(frame, 0xcc));
    }

    [HostTest("net.http.static.tcp.ipv4 default result is not success", HostLane.Fast, 5)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        HttpStaticTcpResponseIpv4FramePlanResult defaultResult = default;
        HttpStaticTcpResponseIpv4FramePlanResult failure =
            HttpStaticTcpResponseIpv4FramePlanResult.Create(
                HttpStaticTcpResponseIpv4FramePlanStatus.ResponsePlanFailed,
                default,
                default);

        return HostCheck.Result(!defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == HttpStaticTcpResponseIpv4FramePlanStatus.Success
            && failure.IsInitialized
            && !failure.Succeeded
            && failure.Status == HttpStaticTcpResponseIpv4FramePlanStatus.ResponsePlanFailed
            && HttpStaticTcpResponseIpv4FramePlan.ConsumesStaticTcpResponsePlan
            && HttpStaticTcpResponseIpv4FramePlan.UsesCallerOwnedBuffers
            && !HttpStaticTcpResponseIpv4FramePlan.AllocatesBuffers
            && !HttpStaticTcpResponseIpv4FramePlan.OpensSockets
            && HttpStaticTcpResponseIpv4FramePlan.WritesTcpFrame
            && !HttpStaticTcpResponseIpv4FramePlan.PerformsNicIo
            && !HttpStaticTcpResponseIpv4FramePlan.OwnsTimers
            && !HttpStaticTcpResponseIpv4FramePlan.PerformsRetransmission
            && !HttpStaticTcpResponseIpv4FramePlan.PerformsTls
            && !HttpStaticTcpResponseIpv4FramePlan.StartsHostedDotNet
            && !HttpStaticTcpResponseIpv4FramePlan.StartsKestrel
            && !RuntimeHelpers.IsReferenceOrContainsReferences<HttpStaticTcpResponseIpv4FramePlanResult>());
    }

    private static bool ValidateResponseFrame(
        ReadOnlySpan<byte> frame,
        int expectedPayloadBytes,
        uint expectedSequence,
        uint expectedAcknowledgement,
        string expectedStatusLine,
        string expectedBodySuffix)
    {
        if (EthernetHeader.TryParse(frame, out EthernetHeader ethernet, out ReadOnlySpan<byte> ethernetPayload)
            != PacketParseStatus.Success)
        {
            return false;
        }

        if (Ipv4Header.TryParse(ethernetPayload, out Ipv4Header ipv4, out ReadOnlySpan<byte> ipv4Payload)
            != PacketParseStatus.Success)
        {
            return false;
        }

        if (TcpHeader.TryParse(
                ipv4Payload,
                out TcpHeader tcp,
                out ReadOnlySpan<byte> options,
                out ReadOnlySpan<byte> payload) != PacketParseStatus.Success)
        {
            return false;
        }

        Ipv4TransportChecksumStatus checksumStatus = Ipv4TransportChecksum.Validate(
            ipv4.Source,
            ipv4.Destination,
            TcpEstablishedStreamIpv4FramePlanner.ProtocolTcp,
            ipv4Payload,
            out Ipv4TransportChecksumResult checksum);
        string payloadText = Encoding.ASCII.GetString(payload);

        return ethernet.Source == LocalMac()
            && ethernet.Destination == RemoteMac()
            && ipv4.Source == LocalAddress()
            && ipv4.Destination == RemoteAddress()
            && ipv4.Protocol == TcpEstablishedStreamIpv4FramePlanner.ProtocolTcp
            && ipv4.TimeToLive == 42
            && ipv4.Identification == 0x775
            && tcp.SourcePort == LocalPort
            && tcp.DestinationPort == RemotePort
            && tcp.SequenceNumber == expectedSequence
            && tcp.AcknowledgementNumber == expectedAcknowledgement
            && tcp.Flags == (TcpEstablishedStream.FlagPsh | TcpEstablishedStream.FlagAck)
            && tcp.Checksum != 0
            && options.Length == 0
            && payload.Length == expectedPayloadBytes
            && payloadText.StartsWith(expectedStatusLine, StringComparison.Ordinal)
            && payloadText.EndsWith(expectedBodySuffix, StringComparison.Ordinal)
            && checksumStatus == Ipv4TransportChecksumStatus.Success
            && checksum.IsValid;
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

    private static TcpEstablishedStreamIpv4FramePlannerInfo CreateFrameInfo(int maximumPayloadLength)
    {
        return new TcpEstablishedStreamIpv4FramePlannerInfo(
            LocalMac(),
            RemoteMac(),
            LocalAddress(),
            RemoteAddress(),
            maximumPayloadLength,
            windowSize: 0x3000,
            timeToLive: 42,
            identification: 0x775);
    }

    private static bool AllBytesEqual(ReadOnlySpan<byte> value, byte expected)
    {
        for (int index = 0; index < value.Length; index++)
        {
            if (value[index] != expected)
            {
                return false;
            }
        }

        return true;
    }

    private static MacAddress LocalMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x77, 0x50);
    }

    private static MacAddress RemoteMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x77, 0x51);
    }

    private static Ipv4Address LocalAddress()
    {
        return new Ipv4Address(192, 0, 2, 77);
    }

    private static Ipv4Address RemoteAddress()
    {
        return new Ipv4Address(198, 51, 100, 77);
    }
}
