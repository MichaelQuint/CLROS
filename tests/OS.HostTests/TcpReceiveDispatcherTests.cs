using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;

internal static class TcpReceiveDispatcherTests
{
    private const ushort LocalPort = 49190;
    private const ushort RemotePort = 9443;
    private const uint LocalNextSequence = 0x01020304;
    private const uint RemoteNextSequence = 0x10203040;

    [HostTest("net.tcp.rx dispatch accepts established payload", HostLane.Fast, 0)]
    private static bool TestAcceptsEstablishedPayload()
    {
        Span<byte> receiveStorage = stackalloc byte[16];
        TcpEstablishedStream stream = CreateStream(receiveStorage);
        TcpConnectionClose close = CreateClose();
        byte[] payload = Encoding.ASCII.GetBytes("data");
        byte[] buffer = CreateTcpReceiveBuffer(
            RemoteNextSequence,
            LocalNextSequence,
            TcpEstablishedStream.FlagPsh | TcpEstablishedStream.FlagAck,
            payload,
            corruptChecksum: false);

        NetworkReceiveDispatchResult network = NetworkReceiveDispatcher.Dispatch(buffer);
        TcpReceiveDispatchStatus status = TcpReceiveDispatcher.Dispatch(
            buffer,
            network,
            ref stream,
            ref close,
            Span<byte>.Empty,
            out TcpReceiveDispatchResult result);
        if (status != TcpReceiveDispatchStatus.Success
            || !result.Succeeded
            || result.Action != TcpReceiveDispatchAction.PayloadAccepted
            || result.NetworkStatus != NetworkReceiveDispatchStatus.Success
            || result.PayloadKind != EthernetPayloadKind.Ipv4
            || result.TcpPacketStatus != PacketParseStatus.Success
            || result.StreamStatus != TcpEstablishedStreamStatus.Success
            || result.CloseStatus != TcpConnectionCloseStatus.Success
            || result.CloseState != TcpConnectionCloseState.Established
            || result.TcpSegmentOffset != ExpectedTcpSegmentOffset
            || result.TcpSegmentLength != TcpHeader.MinHeaderLength + payload.Length
            || result.PayloadBytes != payload.Length
            || result.AckBytesWritten != 0
            || result.LocalNextSequenceNumber != LocalNextSequence
            || result.RemoteNextSequenceNumber != RemoteNextSequence + (uint)payload.Length
            || stream.UnreadBytes != payload.Length
            || stream.AcknowledgementNumber != RemoteNextSequence + (uint)payload.Length
            || close.State != TcpConnectionCloseState.Established)
        {
            return HostCheck.Result(false);
        }

        Span<byte> read = stackalloc byte[4];
        return HostCheck.Result(stream.Read(read, out TcpEstablishedStreamReadResult readResult)
                == TcpEstablishedStreamStatus.Success
            && readResult.BytesRead == payload.Length
            && BytesEqual(read, payload));
    }

    [HostTest("net.tcp.rx dispatch acknowledges fin", HostLane.Fast, 1)]
    private static bool TestAcknowledgesFin()
    {
        Span<byte> receiveStorage = stackalloc byte[16];
        TcpEstablishedStream stream = CreateStream(receiveStorage);
        TcpConnectionClose close = CreateClose();
        byte[] payload = Encoding.ASCII.GetBytes("hi");
        byte[] payloadBuffer = CreateTcpReceiveBuffer(
            RemoteNextSequence,
            LocalNextSequence,
            TcpEstablishedStream.FlagPsh | TcpEstablishedStream.FlagAck,
            payload,
            corruptChecksum: false);

        if (TcpReceiveDispatcher.Dispatch(
                payloadBuffer,
                NetworkReceiveDispatcher.Dispatch(payloadBuffer),
                ref stream,
                ref close,
                Span<byte>.Empty,
                out TcpReceiveDispatchResult payloadResult) != TcpReceiveDispatchStatus.Success
            || payloadResult.Action != TcpReceiveDispatchAction.PayloadAccepted)
        {
            return HostCheck.Result(false);
        }

        byte[] finBuffer = CreateTcpReceiveBuffer(
            RemoteNextSequence + (uint)payload.Length,
            LocalNextSequence,
            TcpConnectionClose.FlagFin | TcpConnectionClose.FlagAck,
            ReadOnlySpan<byte>.Empty,
            corruptChecksum: false);
        Span<byte> ackSegment = stackalloc byte[TcpHeader.MinHeaderLength];
        TcpReceiveDispatchStatus finStatus = TcpReceiveDispatcher.Dispatch(
            finBuffer,
            NetworkReceiveDispatcher.Dispatch(finBuffer),
            ref stream,
            ref close,
            ackSegment,
            out TcpReceiveDispatchResult finResult);

        if (finStatus != TcpReceiveDispatchStatus.Success
            || !finResult.Succeeded
            || finResult.Action != TcpReceiveDispatchAction.FinAcknowledged
            || finResult.AckBytesWritten != TcpHeader.MinHeaderLength
            || finResult.CloseState != TcpConnectionCloseState.CloseWait
            || finResult.LocalNextSequenceNumber != LocalNextSequence
            || finResult.RemoteNextSequenceNumber != RemoteNextSequence + (uint)payload.Length + 1
            || close.State != TcpConnectionCloseState.CloseWait
            || close.RemoteNextSequenceNumber != RemoteNextSequence + (uint)payload.Length + 1)
        {
            return HostCheck.Result(false);
        }

        ReadOnlySpan<byte> writtenAck = ackSegment.Slice(0, finResult.AckBytesWritten);
        if (TcpHeader.TryParse(
                writtenAck,
                out TcpHeader ackHeader,
                out ReadOnlySpan<byte> _,
                out ReadOnlySpan<byte> ackPayload) != PacketParseStatus.Success)
        {
            return HostCheck.Result(false);
        }

        Ipv4TransportChecksumStatus checksumStatus = Ipv4TransportChecksum.Validate(
            LocalAddress(),
            RemoteAddress(),
            TcpReceiveDispatcher.ProtocolTcp,
            writtenAck,
            out Ipv4TransportChecksumResult checksum);

        return HostCheck.Result(ackHeader.SourcePort == LocalPort
            && ackHeader.DestinationPort == RemotePort
            && ackHeader.SequenceNumber == LocalNextSequence
            && ackHeader.AcknowledgementNumber == RemoteNextSequence + (uint)payload.Length + 1
            && ackHeader.Flags == TcpConnectionClose.FlagAck
            && ackHeader.Checksum != 0
            && ackPayload.Length == 0
            && checksumStatus == Ipv4TransportChecksumStatus.Success
            && checksum.IsValid);
    }

    [HostTest("net.tcp.rx dispatch handles reset", HostLane.Fast, 2)]
    private static bool TestHandlesReset()
    {
        Span<byte> receiveStorage = stackalloc byte[16];
        TcpEstablishedStream stream = CreateStream(receiveStorage);
        TcpConnectionClose close = CreateClose();
        byte[] buffer = CreateTcpReceiveBuffer(
            RemoteNextSequence,
            LocalNextSequence,
            TcpConnectionClose.FlagRst | TcpConnectionClose.FlagAck,
            ReadOnlySpan<byte>.Empty,
            corruptChecksum: false);

        TcpReceiveDispatchStatus status = TcpReceiveDispatcher.Dispatch(
            buffer,
            NetworkReceiveDispatcher.Dispatch(buffer),
            ref stream,
            ref close,
            Span<byte>.Empty,
            out TcpReceiveDispatchResult result);

        return HostCheck.Result(status == TcpReceiveDispatchStatus.Success
            && result.Succeeded
            && result.Action == TcpReceiveDispatchAction.ResetReceived
            && result.CloseState == TcpConnectionCloseState.Reset
            && close.State == TcpConnectionCloseState.Reset
            && stream.UnreadBytes == 0
            && result.PayloadBytes == 0
            && result.AckBytesWritten == 0);
    }

    [HostTest("net.tcp.rx dispatch accepts empty ack", HostLane.Fast, 3)]
    private static bool TestAcceptsEmptyAck()
    {
        Span<byte> receiveStorage = stackalloc byte[16];
        TcpEstablishedStream stream = CreateStream(receiveStorage);
        TcpConnectionClose close = CreateClose();
        byte[] buffer = CreateTcpReceiveBuffer(
            RemoteNextSequence,
            LocalNextSequence,
            TcpConnectionClose.FlagAck,
            ReadOnlySpan<byte>.Empty,
            corruptChecksum: false);

        TcpReceiveDispatchStatus status = TcpReceiveDispatcher.Dispatch(
            buffer,
            NetworkReceiveDispatcher.Dispatch(buffer),
            ref stream,
            ref close,
            Span<byte>.Empty,
            out TcpReceiveDispatchResult result);

        return HostCheck.Result(status == TcpReceiveDispatchStatus.Success
            && result.Succeeded
            && result.Action == TcpReceiveDispatchAction.None
            && result.TcpPacketStatus == PacketParseStatus.Success
            && result.CloseStatus == TcpConnectionCloseStatus.Success
            && result.StreamStatus == TcpEstablishedStreamStatus.Success
            && result.TcpHeader.SourcePort == RemotePort
            && result.TcpHeader.DestinationPort == LocalPort
            && result.TcpHeader.SequenceNumber == RemoteNextSequence
            && result.TcpHeader.AcknowledgementNumber == LocalNextSequence
            && result.TcpSegmentOffset == ExpectedTcpSegmentOffset
            && result.TcpSegmentLength == TcpHeader.MinHeaderLength
            && result.PayloadBytes == 0
            && result.AckBytesWritten == 0
            && result.LocalNextSequenceNumber == LocalNextSequence
            && result.RemoteNextSequenceNumber == RemoteNextSequence
            && stream.LocalNextSequenceNumber == LocalNextSequence
            && stream.RemoteNextSequenceNumber == RemoteNextSequence
            && stream.UnreadBytes == 0
            && close.State == TcpConnectionCloseState.Established);
    }

    [HostTest("net.tcp.rx dispatch rejects out-of-window control", HostLane.Fast, 4)]
    private static bool TestRejectsOutOfWindowControl()
    {
        return HostCheck.Result(RejectsControlFrame(
                TcpConnectionClose.FlagAck,
                RemoteNextSequence,
                LocalNextSequence + 1,
                ReadOnlySpan<byte>.Empty,
                TcpConnectionCloseStatus.WrongAcknowledgementNumber)
            && RejectsControlFrame(
                TcpConnectionClose.FlagFin | TcpConnectionClose.FlagAck,
                RemoteNextSequence + 1,
                LocalNextSequence,
                ReadOnlySpan<byte>.Empty,
                TcpConnectionCloseStatus.WrongSequenceNumber)
            && RejectsControlFrame(
                TcpConnectionClose.FlagRst | TcpConnectionClose.FlagAck,
                RemoteNextSequence,
                LocalNextSequence,
                new byte[] { 0x21 },
                TcpConnectionCloseStatus.UnexpectedPayload));
    }

    [HostTest("net.tcp.rx dispatch rejects bad checksum", HostLane.Fast, 5)]
    private static bool TestRejectsBadChecksum()
    {
        Span<byte> receiveStorage = stackalloc byte[16];
        TcpEstablishedStream stream = CreateStream(receiveStorage);
        TcpConnectionClose close = CreateClose();
        byte[] buffer = CreateTcpReceiveBuffer(
            RemoteNextSequence,
            LocalNextSequence,
            TcpEstablishedStream.FlagPsh | TcpEstablishedStream.FlagAck,
            Encoding.ASCII.GetBytes("bad"),
            corruptChecksum: true);

        TcpReceiveDispatchStatus status = TcpReceiveDispatcher.Dispatch(
            buffer,
            NetworkReceiveDispatcher.Dispatch(buffer),
            ref stream,
            ref close,
            Span<byte>.Empty,
            out TcpReceiveDispatchResult result);

        return HostCheck.Result(status == TcpReceiveDispatchStatus.BadTcpChecksum
            && !result.Succeeded
            && result.TcpPacketStatus == PacketParseStatus.BadChecksum
            && result.ChecksumStatus == Ipv4TransportChecksumStatus.Success
            && result.TcpHeader.SourcePort == RemotePort
            && result.TcpHeader.DestinationPort == LocalPort
            && result.TcpSegmentOffset == ExpectedTcpSegmentOffset
            && stream.RemoteNextSequenceNumber == RemoteNextSequence
            && stream.UnreadBytes == 0
            && close.State == TcpConnectionCloseState.Established);
    }

    [HostTest("net.tcp.rx dispatch reports stream failure", HostLane.Fast, 6)]
    private static bool TestReportsStreamFailure()
    {
        Span<byte> receiveStorage = stackalloc byte[16];
        TcpEstablishedStream stream = CreateStream(receiveStorage);
        TcpConnectionClose close = CreateClose();
        byte[] buffer = CreateTcpReceiveBuffer(
            RemoteNextSequence + 5,
            LocalNextSequence,
            TcpEstablishedStream.FlagPsh | TcpEstablishedStream.FlagAck,
            Encoding.ASCII.GetBytes("skip"),
            corruptChecksum: false);

        TcpReceiveDispatchStatus status = TcpReceiveDispatcher.Dispatch(
            buffer,
            NetworkReceiveDispatcher.Dispatch(buffer),
            ref stream,
            ref close,
            Span<byte>.Empty,
            out TcpReceiveDispatchResult result);

        return HostCheck.Result(status == TcpReceiveDispatchStatus.TcpStreamFailed
            && !result.Succeeded
            && result.StreamStatus == TcpEstablishedStreamStatus.OutOfOrderSegment
            && result.TcpPacketStatus == PacketParseStatus.Success
            && result.PayloadBytes == 0
            && stream.RemoteNextSequenceNumber == RemoteNextSequence
            && stream.UnreadBytes == 0
            && close.State == TcpConnectionCloseState.Established);
    }

    [HostTest("net.tcp.rx dispatch rejects non tcp", HostLane.Fast, 7)]
    private static bool TestRejectsNonTcp()
    {
        Span<byte> receiveStorage = stackalloc byte[16];
        TcpEstablishedStream stream = CreateStream(receiveStorage);
        TcpConnectionClose close = CreateClose();
        byte[] buffer = CreateIpv4ReceiveBuffer(Ipv4Header.ProtocolUdp, ipv4PayloadLength: 0);

        TcpReceiveDispatchStatus status = TcpReceiveDispatcher.Dispatch(
            buffer,
            NetworkReceiveDispatcher.Dispatch(buffer),
            ref stream,
            ref close,
            Span<byte>.Empty,
            out TcpReceiveDispatchResult result);

        return HostCheck.Result(status == TcpReceiveDispatchStatus.UnsupportedIpProtocol
            && !result.Succeeded
            && result.NetworkStatus == NetworkReceiveDispatchStatus.Success
            && result.PayloadKind == EthernetPayloadKind.Ipv4
            && result.TcpPacketStatus == PacketParseStatus.UnsupportedIpProtocol
            && result.TcpSegmentOffset == 0
            && result.TcpSegmentLength == 0
            && stream.RemoteNextSequenceNumber == RemoteNextSequence
            && close.State == TcpConnectionCloseState.Established);
    }

    [HostTest("net.tcp.rx dispatch default result is not success", HostLane.Fast, 8)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        TcpReceiveDispatchResult defaultResult = default;
        NetworkReceiveDispatchResult failedNetwork =
            NetworkReceiveDispatcher.Dispatch(new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers - 1]);
        TcpReceiveDispatchResult failure = TcpReceiveDispatchResult.Fail(
            TcpReceiveDispatchStatus.NetworkDispatchFailed,
            failedNetwork,
            PacketParseStatus.BufferTooSmall,
            TcpEstablishedStreamStatus.Success,
            TcpConnectionCloseStatus.Success,
            TcpConnectionCloseState.Established,
            Ipv4TransportChecksumStatus.Success,
            default,
            0,
            0,
            LocalNextSequence,
            RemoteNextSequence);

        return HostCheck.Result(!defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == TcpReceiveDispatchStatus.Success
            && defaultResult.Action == TcpReceiveDispatchAction.None
            && defaultResult.NetworkStatus == NetworkReceiveDispatchStatus.Success
            && defaultResult.TcpPacketStatus == PacketParseStatus.Success
            && defaultResult.StreamStatus == TcpEstablishedStreamStatus.Success
            && defaultResult.CloseStatus == TcpConnectionCloseStatus.Success
            && defaultResult.ChecksumStatus == Ipv4TransportChecksumStatus.Success
            && defaultResult.TcpSegmentOffset == 0
            && defaultResult.TcpSegmentLength == 0
            && defaultResult.PayloadBytes == 0
            && defaultResult.AckBytesWritten == 0
            && failure.IsInitialized
            && !failure.Succeeded
            && failure.Status == TcpReceiveDispatchStatus.NetworkDispatchFailed
            && failure.TcpPacketStatus == PacketParseStatus.BufferTooSmall
            && failure.LocalNextSequenceNumber == LocalNextSequence
            && failure.RemoteNextSequenceNumber == RemoteNextSequence
            && TcpReceiveDispatcher.ConsumesParsedNetworkMetadata
            && TcpReceiveDispatcher.UsesCallerOwnedBuffers
            && !TcpReceiveDispatcher.AllocatesBuffers
            && !TcpReceiveDispatcher.PerformsNicIo
            && !TcpReceiveDispatcher.DispatchesSockets
            && !TcpReceiveDispatcher.ImplementsTls
            && !TcpReceiveDispatcher.ImplementsFullTcp
            && !TcpReceiveDispatcher.StartsKestrel
            && !RuntimeHelpers.IsReferenceOrContainsReferences<TcpReceiveDispatchResult>());
    }

    [HostTest("net.tcp.rx.dispatch diagnostic formats payload result", HostLane.Fast, 9)]
    private static bool TestDiagnosticFormatsPayloadResult()
    {
        Span<byte> receiveStorage = stackalloc byte[16];
        TcpEstablishedStream stream = CreateStream(receiveStorage);
        TcpConnectionClose close = CreateClose();
        byte[] payload = Encoding.ASCII.GetBytes("data");
        byte[] buffer = CreateTcpReceiveBuffer(
            RemoteNextSequence,
            LocalNextSequence,
            TcpEstablishedStream.FlagPsh | TcpEstablishedStream.FlagAck,
            payload,
            corruptChecksum: false);
        Span<byte> destination = stackalloc byte[256];
        destination.Fill(0);

        if (TcpReceiveDispatcher.Dispatch(
                buffer,
                NetworkReceiveDispatcher.Dispatch(buffer),
                ref stream,
                ref close,
                Span<byte>.Empty,
                out TcpReceiveDispatchResult dispatch) != TcpReceiveDispatchStatus.Success)
        {
            return HostCheck.Result(false);
        }

        TcpReceiveDispatchDiagnosticFormatResult query =
            TcpReceiveDispatchDiagnosticFormatter.QueryRequiredBytes(dispatch);
        TcpReceiveDispatchDiagnosticFormatResult format =
            TcpReceiveDispatchDiagnosticFormatter.FormatResult(dispatch, destination);
        string text = Encoding.ASCII.GetString(destination[..format.BytesWritten]);
        string expected =
            "TCP_RX_DISPATCH status=Success action=PayloadAccepted network=Success packet=Success " +
            "stream=Success close=Success closeState=Established checksum=Success payloadBytes=" +
            payload.Length +
            " ackBytes=0 tcpSegmentOffset=" +
            ExpectedTcpSegmentOffset +
            " tcpSegmentBytes=" +
            (TcpHeader.MinHeaderLength + payload.Length) +
            " localSeq=" +
            LocalNextSequence +
            " remoteSeq=" +
            (RemoteNextSequence + (uint)payload.Length);

        return HostCheck.Result(query.IsInitialized
            && !query.Succeeded
            && query.Status == TcpReceiveDispatchDiagnosticFormatStatus.Success
            && query.DispatchStatus == TcpReceiveDispatchStatus.Success
            && query.BytesWritten == 0
            && query.RequiredBytes == expected.Length
            && query.AvailableBytes == 0
            && format.Succeeded
            && format.DispatchStatus == TcpReceiveDispatchStatus.Success
            && format.BytesWritten == expected.Length
            && format.RequiredBytes == expected.Length
            && format.AvailableBytes == destination.Length
            && text == expected);
    }

    [HostTest("net.tcp.rx.dispatch diagnostic formats failure result", HostLane.Fast, 10)]
    private static bool TestDiagnosticFormatsFailureResult()
    {
        Span<byte> receiveStorage = stackalloc byte[16];
        TcpEstablishedStream stream = CreateStream(receiveStorage);
        TcpConnectionClose close = CreateClose();
        byte[] payload = Encoding.ASCII.GetBytes("bad!");
        byte[] buffer = CreateTcpReceiveBuffer(
            RemoteNextSequence,
            LocalNextSequence,
            TcpEstablishedStream.FlagPsh | TcpEstablishedStream.FlagAck,
            payload,
            corruptChecksum: true);
        Span<byte> destination = stackalloc byte[256];
        destination.Fill(0);

        TcpReceiveDispatchStatus status = TcpReceiveDispatcher.Dispatch(
            buffer,
            NetworkReceiveDispatcher.Dispatch(buffer),
            ref stream,
            ref close,
            Span<byte>.Empty,
            out TcpReceiveDispatchResult dispatch);
        if (status != TcpReceiveDispatchStatus.BadTcpChecksum)
        {
            return HostCheck.Result(false);
        }

        TcpReceiveDispatchDiagnosticFormatResult query =
            TcpReceiveDispatchDiagnosticFormatter.QueryRequiredBytes(dispatch);
        TcpReceiveDispatchDiagnosticFormatResult format =
            TcpReceiveDispatchDiagnosticFormatter.FormatResult(dispatch, destination);
        string text = Encoding.ASCII.GetString(destination[..format.BytesWritten]);
        string expected =
            "TCP_RX_DISPATCH status=BadTcpChecksum action=None network=Success packet=BadChecksum " +
            "stream=Success close=Success closeState=Established checksum=Success payloadBytes=0" +
            " ackBytes=0 tcpSegmentOffset=" +
            ExpectedTcpSegmentOffset +
            " tcpSegmentBytes=" +
            (TcpHeader.MinHeaderLength + payload.Length) +
            " localSeq=" +
            LocalNextSequence +
            " remoteSeq=" +
            RemoteNextSequence;

        return HostCheck.Result(query.IsInitialized
            && !query.Succeeded
            && query.Status == TcpReceiveDispatchDiagnosticFormatStatus.Success
            && query.DispatchStatus == TcpReceiveDispatchStatus.BadTcpChecksum
            && query.RequiredBytes == expected.Length
            && format.Succeeded
            && format.DispatchStatus == TcpReceiveDispatchStatus.BadTcpChecksum
            && format.BytesWritten == expected.Length
            && text == expected);
    }

    [HostTest("net.tcp.rx.dispatch diagnostic reports short destination", HostLane.Fast, 11)]
    private static bool TestDiagnosticReportsShortDestination()
    {
        Span<byte> receiveStorage = stackalloc byte[16];
        TcpEstablishedStream stream = CreateStream(receiveStorage);
        TcpConnectionClose close = CreateClose();
        byte[] payload = Encoding.ASCII.GetBytes("data");
        byte[] buffer = CreateTcpReceiveBuffer(
            RemoteNextSequence,
            LocalNextSequence,
            TcpEstablishedStream.FlagPsh | TcpEstablishedStream.FlagAck,
            payload,
            corruptChecksum: false);
        Span<byte> destination = stackalloc byte[12];
        destination.Fill(0xA5);

        if (TcpReceiveDispatcher.Dispatch(
                buffer,
                NetworkReceiveDispatcher.Dispatch(buffer),
                ref stream,
                ref close,
                Span<byte>.Empty,
                out TcpReceiveDispatchResult dispatch) != TcpReceiveDispatchStatus.Success)
        {
            return HostCheck.Result(false);
        }

        TcpReceiveDispatchDiagnosticFormatResult query =
            TcpReceiveDispatchDiagnosticFormatter.QueryRequiredBytes(dispatch);
        TcpReceiveDispatchDiagnosticFormatResult shortResult =
            TcpReceiveDispatchDiagnosticFormatter.FormatResult(dispatch, destination);

        return HostCheck.Result(shortResult.IsInitialized
            && !shortResult.Succeeded
            && shortResult.Status == TcpReceiveDispatchDiagnosticFormatStatus.DestinationTooSmall
            && shortResult.DispatchStatus == TcpReceiveDispatchStatus.Success
            && shortResult.BytesWritten == 0
            && shortResult.RequiredBytes == query.RequiredBytes
            && shortResult.AvailableBytes == destination.Length
            && query.RequiredBytes > destination.Length
            && destination[0] == 0xA5
            && destination[11] == 0xA5);
    }

    [HostTest("net.tcp.rx.dispatch diagnostic rejects default result", HostLane.Fast, 12)]
    private static bool TestDiagnosticRejectsDefaultResult()
    {
        Span<byte> destination = stackalloc byte[64];
        destination.Fill(0xCC);

        TcpReceiveDispatchResult defaultDispatch = default;
        TcpReceiveDispatchDiagnosticFormatResult invalidFormat =
            TcpReceiveDispatchDiagnosticFormatter.FormatResult(defaultDispatch, destination);
        TcpReceiveDispatchDiagnosticFormatResult invalidQuery =
            TcpReceiveDispatchDiagnosticFormatter.QueryRequiredBytes(defaultDispatch);
        TcpReceiveDispatchDiagnosticFormatResult defaultFormat = default;

        return HostCheck.Result(invalidFormat.IsInitialized
            && !invalidFormat.Succeeded
            && invalidFormat.Status == TcpReceiveDispatchDiagnosticFormatStatus.InvalidResult
            && invalidFormat.DispatchStatus == TcpReceiveDispatchStatus.Success
            && invalidFormat.BytesWritten == 0
            && invalidFormat.RequiredBytes == 0
            && invalidFormat.AvailableBytes == destination.Length
            && invalidQuery.IsInitialized
            && !invalidQuery.Succeeded
            && invalidQuery.Status == TcpReceiveDispatchDiagnosticFormatStatus.InvalidResult
            && invalidQuery.RequiredBytes == 0
            && !defaultFormat.IsInitialized
            && !defaultFormat.Succeeded
            && defaultFormat.Status == TcpReceiveDispatchDiagnosticFormatStatus.Success
            && defaultFormat.DispatchStatus == TcpReceiveDispatchStatus.Success
            && destination[0] == 0xCC
            && destination[63] == 0xCC
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                TcpReceiveDispatchDiagnosticFormatResult>());
    }

    [HostTest("net.tcp.rx.dispatch diagnostic formatter uses shared ascii text builder", HostLane.Fast, 13)]
    private static bool TestDiagnosticFormatterUsesSharedAsciiTextBuilder()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "OS.Kernel",
            "Net",
            "TcpReceiveDispatchDiagnosticFormatter.cs"));

        return HostCheck.Result(source.Contains("AsciiTextBuilder.AppendBytes(destination, offset, value)", StringComparison.Ordinal)
            && source.Contains("AsciiTextBuilder.AppendInt64Decimal(destination, offset, value)", StringComparison.Ordinal)
            && source.Contains("AsciiTextBuilder.AppendUInt64Decimal(destination, offset, value)", StringComparison.Ordinal)
            && source.Contains("AsciiDecimalFormatter.CountInt32Digits", StringComparison.Ordinal)
            && source.Contains("AsciiDecimalFormatter.CountUInt32Digits", StringComparison.Ordinal)
            && !source.Contains("value.CopyTo(destination[offset..])", StringComparison.Ordinal)
            && !source.Contains("AsciiDecimalFormatter.FormatInt32(destination[offset..], value)", StringComparison.Ordinal)
            && !source.Contains("AsciiDecimalFormatter.FormatUInt32(destination[offset..], value)", StringComparison.Ordinal)
            && !source.Contains("private static void AppendInt32", StringComparison.Ordinal)
            && !source.Contains("private static void AppendUInt32", StringComparison.Ordinal)
            && !source.Contains("private static void Append(ReadOnlySpan<byte>", StringComparison.Ordinal));
    }

    private static bool RejectsControlFrame(
        byte flags,
        uint sequenceNumber,
        uint acknowledgementNumber,
        ReadOnlySpan<byte> payload,
        TcpConnectionCloseStatus expectedCloseStatus)
    {
        Span<byte> receiveStorage = stackalloc byte[16];
        TcpEstablishedStream stream = CreateStream(receiveStorage);
        TcpConnectionClose close = CreateClose();
        byte[] buffer = CreateTcpReceiveBuffer(
            sequenceNumber,
            acknowledgementNumber,
            flags,
            payload,
            corruptChecksum: false);
        Span<byte> ackSegment = stackalloc byte[TcpHeader.MinHeaderLength];
        ackSegment.Fill(0xCC);

        TcpReceiveDispatchStatus status = TcpReceiveDispatcher.Dispatch(
            buffer,
            NetworkReceiveDispatcher.Dispatch(buffer),
            ref stream,
            ref close,
            ackSegment,
            out TcpReceiveDispatchResult result);

        return status == TcpReceiveDispatchStatus.ControlSegmentRejected
            && !result.Succeeded
            && result.Action == TcpReceiveDispatchAction.None
            && result.TcpPacketStatus == PacketParseStatus.Success
            && result.CloseStatus == expectedCloseStatus
            && result.StreamStatus == TcpEstablishedStreamStatus.Success
            && result.ChecksumStatus == Ipv4TransportChecksumStatus.Success
            && result.TcpHeader.Flags == flags
            && result.TcpSegmentOffset == ExpectedTcpSegmentOffset
            && result.TcpSegmentLength == TcpHeader.MinHeaderLength + payload.Length
            && result.PayloadBytes == 0
            && result.AckBytesWritten == 0
            && result.LocalNextSequenceNumber == LocalNextSequence
            && result.RemoteNextSequenceNumber == RemoteNextSequence
            && stream.LocalNextSequenceNumber == LocalNextSequence
            && stream.RemoteNextSequenceNumber == RemoteNextSequence
            && stream.UnreadBytes == 0
            && close.State == TcpConnectionCloseState.Established
            && AllBytesEqual(ackSegment, 0xCC);
    }

    private static TcpEstablishedStream CreateStream(Span<byte> receiveStorage)
    {
        return TcpEstablishedStream.Create(
            LocalPort,
            RemotePort,
            LocalNextSequence,
            RemoteNextSequence,
            receiveStorage);
    }

    private static TcpConnectionClose CreateClose()
    {
        return TcpConnectionClose.CreateEstablished(
            LocalPort,
            RemotePort,
            LocalNextSequence,
            RemoteNextSequence);
    }

    private static byte[] CreateTcpReceiveBuffer(
        uint sequenceNumber,
        uint acknowledgementNumber,
        byte flags,
        ReadOnlySpan<byte> payload,
        bool corruptChecksum)
    {
        byte[] buffer = new byte[
            VirtioNetHeader.HeaderLengthWithoutMergeableBuffers +
            EthernetHeader.HeaderLength +
            Ipv4Header.MinHeaderLength +
            TcpHeader.MinHeaderLength +
            payload.Length];
        Span<byte> tcpSegment = buffer.AsSpan(
            ExpectedTcpSegmentOffset,
            TcpHeader.MinHeaderLength + payload.Length);

        PacketParseStatus tcpStatus = TcpHeader.TryWrite(
            tcpSegment,
            RemotePort,
            LocalPort,
            sequenceNumber,
            acknowledgementNumber,
            flags,
            windowSize: 0x3000,
            checksum: 0,
            urgentPointer: 0,
            ReadOnlySpan<byte>.Empty,
            payload,
            out int tcpBytesWritten);
        if (tcpStatus != PacketParseStatus.Success || tcpBytesWritten != tcpSegment.Length)
        {
            throw new InvalidOperationException("Failed to build deterministic TCP segment.");
        }

        Ipv4TransportChecksumStatus checksumStatus = Ipv4TransportChecksum.Compute(
            RemoteAddress(),
            LocalAddress(),
            TcpReceiveDispatcher.ProtocolTcp,
            tcpSegment,
            out Ipv4TransportChecksumResult checksumResult);
        if (checksumStatus != Ipv4TransportChecksumStatus.Success)
        {
            throw new InvalidOperationException("Failed to compute deterministic TCP checksum.");
        }

        ushort checksum = checksumResult.Checksum == 0
            ? ushort.MaxValue
            : checksumResult.Checksum;
        if (corruptChecksum)
        {
            checksum ^= 0x0100;
        }

        EndianPrimitives.WriteUInt16BigEndian(
            tcpSegment.Slice(16, sizeof(ushort)),
            checksum);
        WriteEthernetIpv4Headers(buffer, TcpReceiveDispatcher.ProtocolTcp, tcpSegment.Length);
        return buffer;
    }

    private static byte[] CreateIpv4ReceiveBuffer(byte protocol, int ipv4PayloadLength)
    {
        byte[] buffer = new byte[
            VirtioNetHeader.HeaderLengthWithoutMergeableBuffers +
            EthernetHeader.HeaderLength +
            Ipv4Header.MinHeaderLength +
            ipv4PayloadLength];
        WriteEthernetIpv4Headers(buffer, protocol, ipv4PayloadLength);
        return buffer;
    }

    private static void WriteEthernetIpv4Headers(
        Span<byte> buffer,
        byte protocol,
        int ipv4PayloadLength)
    {
        EthernetHeader ethernet = new EthernetHeader(
            LocalMac(),
            RemoteMac(),
            EthernetHeader.EtherTypeIpv4);
        PacketParseStatus ethernetStatus = EthernetHeader.TryWrite(
            buffer.Slice(
                VirtioNetHeader.HeaderLengthWithoutMergeableBuffers,
                EthernetHeader.HeaderLength),
            ethernet);
        if (ethernetStatus != PacketParseStatus.Success)
        {
            throw new InvalidOperationException("Failed to build deterministic Ethernet header.");
        }

        Ipv4Header ipv4 = new Ipv4Header(
            RemoteAddress(),
            LocalAddress(),
            protocol,
            (ushort)(Ipv4Header.MinHeaderLength + ipv4PayloadLength),
            timeToLive: 64,
            identification: 0x9190,
            flagsAndFragmentOffset: 0,
            typeOfService: 0,
            headerChecksum: 0,
            headerLength: Ipv4Header.MinHeaderLength);
        PacketParseStatus ipv4Status = Ipv4Header.TryWrite(
            buffer.Slice(ExpectedIpv4HeaderOffset, Ipv4Header.MinHeaderLength),
            ipv4,
            out int ipv4BytesWritten);
        if (ipv4Status != PacketParseStatus.Success || ipv4BytesWritten != Ipv4Header.MinHeaderLength)
        {
            throw new InvalidOperationException("Failed to build deterministic IPv4 header.");
        }
    }

    private static bool BytesEqual(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (int index = 0; index < left.Length; index++)
        {
            if (left[index] != right[index])
            {
                return false;
            }
        }

        return true;
    }

    private static bool AllBytesEqual(ReadOnlySpan<byte> buffer, byte expected)
    {
        for (int index = 0; index < buffer.Length; index++)
        {
            if (buffer[index] != expected)
            {
                return false;
            }
        }

        return true;
    }

    private static int ExpectedIpv4HeaderOffset =>
        VirtioNetHeader.HeaderLengthWithoutMergeableBuffers + EthernetHeader.HeaderLength;

    private static int ExpectedTcpSegmentOffset =>
        ExpectedIpv4HeaderOffset + Ipv4Header.MinHeaderLength;

    private static MacAddress LocalMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x90);
    }

    private static MacAddress RemoteMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x91);
    }

    private static Ipv4Address LocalAddress()
    {
        return new Ipv4Address(192, 0, 2, 90);
    }

    private static Ipv4Address RemoteAddress()
    {
        return new Ipv4Address(198, 51, 100, 91);
    }
}
