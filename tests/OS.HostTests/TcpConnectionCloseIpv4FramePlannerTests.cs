using System.Runtime.CompilerServices;
using System.Text;
using OS.Net;

internal static class TcpConnectionCloseIpv4FramePlannerTests
{
    private const ushort LocalPort = 49181;
    private const ushort RemotePort = 8443;
    private const uint LocalNextSequence = 0x01020304;
    private const uint RemoteNextSequence = 0x10203040;

    [HostTest("net.tcp.close.ipv4 writes active fin checksum", HostLane.Fast, 0)]
    private static bool TestWritesActiveFinChecksum()
    {
        TcpConnectionCloseIpv4FramePlanner planner = CreatePlanner();
        byte[] frame = new byte[TcpConnectionCloseIpv4FramePlanner.HeaderBytes];

        TcpConnectionCloseIpv4FramePlanStatus status = planner.TryWriteActiveFinAckFrame(
            frame,
            out TcpConnectionCloseIpv4FramePlanResult result);

        if (status != TcpConnectionCloseIpv4FramePlanStatus.Success
            || !result.Succeeded
            || result.State != TcpConnectionCloseState.FinWait1
            || planner.State != TcpConnectionCloseState.FinWait1
            || result.TcpStatus != TcpConnectionCloseStatus.Success
            || result.PacketStatus != PacketParseStatus.Success
            || result.BytesWritten != TcpConnectionCloseIpv4FramePlanner.HeaderBytes
            || result.TcpSegmentOffset != EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength
            || result.TcpSegmentLength != TcpHeader.MinHeaderLength
            || result.LocalNextSequenceNumber != LocalNextSequence + 1
            || result.RemoteNextSequenceNumber != RemoteNextSequence
            || planner.LocalNextSequenceNumber != LocalNextSequence + 1
            || planner.RemoteNextSequenceNumber != RemoteNextSequence)
        {
            return HostCheck.Result(false);
        }

        if (!TryParseTcpFrame(
                frame.AsSpan(0, result.BytesWritten),
                out EthernetHeader ethernet,
                out Ipv4Header ipv4,
                out TcpHeader tcp,
                out ReadOnlySpan<byte> tcpSegment))
        {
            return HostCheck.Result(false);
        }

        Ipv4TransportChecksumStatus checksumStatus = Ipv4TransportChecksum.Validate(
            ipv4.Source,
            ipv4.Destination,
            TcpConnectionCloseIpv4FramePlanner.ProtocolTcp,
            tcpSegment,
            out Ipv4TransportChecksumResult checksumResult);

        return HostCheck.Result(ethernet.Source == LocalMac()
            && ethernet.Destination == RemoteMac()
            && ethernet.EtherType == EthernetHeader.EtherTypeIpv4
            && ipv4.Source == LocalAddress()
            && ipv4.Destination == RemoteAddress()
            && ipv4.Protocol == TcpConnectionCloseIpv4FramePlanner.ProtocolTcp
            && ipv4.TimeToLive == 42
            && ipv4.Identification == 0x398
            && tcp.SourcePort == LocalPort
            && tcp.DestinationPort == RemotePort
            && tcp.SequenceNumber == LocalNextSequence
            && tcp.AcknowledgementNumber == RemoteNextSequence
            && tcp.Flags == (TcpConnectionClose.FlagFin | TcpConnectionClose.FlagAck)
            && tcp.WindowSize == 0x3000
            && tcp.Checksum != 0
            && result.TcpHeader.Checksum == tcp.Checksum
            && checksumStatus == Ipv4TransportChecksumStatus.Success
            && checksumResult.IsValid
            && checksumResult.TransportLength == TcpHeader.MinHeaderLength);
    }

    [HostTest("net.tcp.close.ipv4 rejects identity and short output", HostLane.Fast, 1)]
    private static bool TestRejectsIdentityAndShortOutput()
    {
        TcpConnectionCloseIpv4FramePlanner invalidLocal = TcpConnectionCloseIpv4FramePlanner.Create(
            new TcpConnectionCloseIpv4FramePlannerInfo(
                default,
                RemoteMac(),
                LocalAddress(),
                RemoteAddress(),
                LocalPort,
                RemotePort,
                LocalNextSequence,
                RemoteNextSequence));
        byte[] frame = new byte[TcpConnectionCloseIpv4FramePlanner.HeaderBytes];
        TcpConnectionCloseIpv4FramePlanStatus invalidLocalStatus = invalidLocal.TryWriteActiveFinAckFrame(
            frame,
            out TcpConnectionCloseIpv4FramePlanResult invalidLocalResult);

        TcpConnectionCloseIpv4FramePlanner invalidRemote = TcpConnectionCloseIpv4FramePlanner.Create(
            new TcpConnectionCloseIpv4FramePlannerInfo(
                LocalMac(),
                default,
                LocalAddress(),
                RemoteAddress(),
                LocalPort,
                RemotePort,
                LocalNextSequence,
                RemoteNextSequence));
        TcpConnectionCloseIpv4FramePlanStatus invalidRemoteStatus = invalidRemote.TryWriteActiveFinAckFrame(
            frame,
            out TcpConnectionCloseIpv4FramePlanResult invalidRemoteResult);

        TcpConnectionCloseIpv4FramePlanner shortOutput = CreatePlanner();
        TcpConnectionCloseIpv4FramePlanStatus shortOutputStatus = shortOutput.TryWriteActiveFinAckFrame(
            frame.AsSpan(0, TcpConnectionCloseIpv4FramePlanner.HeaderBytes - 1),
            out TcpConnectionCloseIpv4FramePlanResult shortOutputResult);

        return HostCheck.Result(invalidLocalStatus == TcpConnectionCloseIpv4FramePlanStatus.InvalidLocalIdentity
            && !invalidLocalResult.Succeeded
            && invalidLocal.State == TcpConnectionCloseState.Established
            && invalidLocal.LocalNextSequenceNumber == LocalNextSequence
            && invalidRemoteStatus == TcpConnectionCloseIpv4FramePlanStatus.InvalidRemoteIdentity
            && !invalidRemoteResult.Succeeded
            && invalidRemote.State == TcpConnectionCloseState.Established
            && invalidRemote.RemoteNextSequenceNumber == RemoteNextSequence
            && shortOutputStatus == TcpConnectionCloseIpv4FramePlanStatus.FrameBufferTooSmall
            && !shortOutputResult.Succeeded
            && shortOutputResult.TcpStatus == TcpConnectionCloseStatus.BufferTooSmall
            && shortOutputResult.PacketStatus == PacketParseStatus.BufferTooSmall
            && shortOutput.State == TcpConnectionCloseState.Established
            && shortOutput.LocalNextSequenceNumber == LocalNextSequence
            && shortOutput.RemoteNextSequenceNumber == RemoteNextSequence);
    }

    [HostTest("net.tcp.close.ipv4 default result is not success", HostLane.Fast, 2)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        TcpConnectionCloseIpv4FramePlannerInfo info = new(
            LocalMac(),
            RemoteMac(),
            LocalAddress(),
            RemoteAddress(),
            LocalPort,
            RemotePort,
            LocalNextSequence,
            RemoteNextSequence,
            windowSize: 0x3000,
            timeToLive: 42,
            identification: 0x398);
        TcpConnectionCloseIpv4FramePlanResult defaultResult = default;
        TcpConnectionCloseIpv4FramePlanResult success = TcpConnectionCloseIpv4FramePlanResult.Success(
            TcpConnectionCloseState.FinWait1,
            TcpConnectionCloseStatus.Success,
            PacketParseStatus.Success,
            default,
            default,
            default,
            TcpConnectionCloseIpv4FramePlanner.HeaderBytes,
            EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength,
            TcpHeader.MinHeaderLength,
            info,
            LocalNextSequence + 1,
            RemoteNextSequence);
        TcpConnectionCloseIpv4FramePlanResult failure = TcpConnectionCloseIpv4FramePlanResult.Fail(
            TcpConnectionCloseIpv4FramePlanStatus.FrameBufferTooSmall,
            TcpConnectionCloseState.Established,
            TcpConnectionCloseStatus.BufferTooSmall,
            PacketParseStatus.BufferTooSmall,
            info,
            LocalNextSequence,
            RemoteNextSequence);

        return HostCheck.Result(!defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == TcpConnectionCloseIpv4FramePlanStatus.Success
            && success.IsInitialized
            && success.Succeeded
            && success.State == TcpConnectionCloseState.FinWait1
            && success.TcpStatus == TcpConnectionCloseStatus.Success
            && success.PacketStatus == PacketParseStatus.Success
            && success.BytesWritten == TcpConnectionCloseIpv4FramePlanner.HeaderBytes
            && success.TcpSegmentOffset == EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength
            && success.TcpSegmentLength == TcpHeader.MinHeaderLength
            && success.LocalMac == LocalMac()
            && success.RemoteMac == RemoteMac()
            && success.LocalAddress == LocalAddress()
            && success.RemoteAddress == RemoteAddress()
            && success.LocalPort == LocalPort
            && success.RemotePort == RemotePort
            && success.TimeToLive == 42
            && success.Identification == 0x398
            && success.LocalNextSequenceNumber == LocalNextSequence + 1
            && success.RemoteNextSequenceNumber == RemoteNextSequence
            && failure.IsInitialized
            && !failure.Succeeded
            && failure.Status == TcpConnectionCloseIpv4FramePlanStatus.FrameBufferTooSmall
            && failure.State == TcpConnectionCloseState.Established
            && failure.TcpStatus == TcpConnectionCloseStatus.BufferTooSmall
            && failure.PacketStatus == PacketParseStatus.BufferTooSmall
            && failure.BytesWritten == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<TcpConnectionCloseIpv4FramePlanResult>());
    }

    [HostTest("net.tcp.close.ipv4.formatter formats active close marker", HostLane.Fast, 3)]
    private static bool TestFormatterFormatsActiveCloseMarker()
    {
        TcpConnectionCloseIpv4FramePlanResult result = CreateSuccessfulFramePlan();

        Span<byte> destination = stackalloc byte[384];
        destination.Fill(0);
        TcpConnectionCloseIpv4FramePlanDiagnosticFormatResult query =
            TcpConnectionCloseIpv4FramePlanDiagnosticFormatter.QueryRequiredBytes(result);
        TcpConnectionCloseIpv4FramePlanDiagnosticFormatResult format =
            TcpConnectionCloseIpv4FramePlanDiagnosticFormatter.FormatResult(result, destination);
        string text = Encoding.ASCII.GetString(destination[..format.BytesWritten]);
        string expected =
            "TCP_CLOSE_IPV4_FRAME status=Success state=FinWait1 tcp=Success packet=Success " +
            "bytes=" + result.BytesWritten +
            " tcpOffset=" + result.TcpSegmentOffset +
            " tcpBytes=" + result.TcpSegmentLength +
            " localPort=" + result.LocalPort +
            " remotePort=" + result.RemotePort +
            " ttl=" + result.TimeToLive +
            " identification=" + result.Identification +
            " localSeq=" + result.LocalNextSequenceNumber +
            " remoteSeq=" + result.RemoteNextSequenceNumber;

        return HostCheck.Result(query.IsInitialized
            && !query.Succeeded
            && query.Status == TcpConnectionCloseIpv4FramePlanDiagnosticFormatStatus.Success
            && query.FrameStatus == TcpConnectionCloseIpv4FramePlanStatus.Success
            && query.BytesWritten == 0
            && query.RequiredBytes == expected.Length
            && query.AvailableBytes == 0
            && format.Succeeded
            && format.FrameStatus == TcpConnectionCloseIpv4FramePlanStatus.Success
            && format.BytesWritten == expected.Length
            && format.RequiredBytes == expected.Length
            && format.AvailableBytes == destination.Length
            && text == expected
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                TcpConnectionCloseIpv4FramePlanDiagnosticFormatResult>());
    }

    [HostTest("net.tcp.close.ipv4.formatter reports short destination", HostLane.Fast, 4)]
    private static bool TestFormatterReportsShortDestination()
    {
        TcpConnectionCloseIpv4FramePlanResult result = CreateSuccessfulFramePlan();
        TcpConnectionCloseIpv4FramePlanDiagnosticFormatResult query =
            TcpConnectionCloseIpv4FramePlanDiagnosticFormatter.QueryRequiredBytes(result);

        Span<byte> destination = stackalloc byte[31];
        destination.Fill(0xCC);
        TcpConnectionCloseIpv4FramePlanDiagnosticFormatResult shortResult =
            TcpConnectionCloseIpv4FramePlanDiagnosticFormatter.FormatResult(result, destination);

        return HostCheck.Result(shortResult.IsInitialized
            && !shortResult.Succeeded
            && shortResult.Status == TcpConnectionCloseIpv4FramePlanDiagnosticFormatStatus.DestinationTooSmall
            && shortResult.FrameStatus == TcpConnectionCloseIpv4FramePlanStatus.Success
            && shortResult.BytesWritten == 0
            && shortResult.RequiredBytes == query.RequiredBytes
            && shortResult.AvailableBytes == destination.Length
            && query.RequiredBytes > destination.Length
            && destination[0] == 0xCC
            && destination[30] == 0xCC);
    }

    [HostTest("net.tcp.close.ipv4.formatter rejects default result", HostLane.Fast, 5)]
    private static bool TestFormatterRejectsDefaultResult()
    {
        Span<byte> destination = stackalloc byte[64];
        destination.Fill(0xAA);

        TcpConnectionCloseIpv4FramePlanResult defaultResult = default;
        TcpConnectionCloseIpv4FramePlanDiagnosticFormatResult format =
            TcpConnectionCloseIpv4FramePlanDiagnosticFormatter.FormatResult(defaultResult, destination);
        TcpConnectionCloseIpv4FramePlanDiagnosticFormatResult query =
            TcpConnectionCloseIpv4FramePlanDiagnosticFormatter.QueryRequiredBytes(defaultResult);
        TcpConnectionCloseIpv4FramePlanDiagnosticFormatResult defaultFormat = default;

        return HostCheck.Result(format.IsInitialized
            && !format.Succeeded
            && format.Status == TcpConnectionCloseIpv4FramePlanDiagnosticFormatStatus.InvalidResult
            && format.FrameStatus == TcpConnectionCloseIpv4FramePlanStatus.Success
            && format.BytesWritten == 0
            && format.RequiredBytes == 0
            && format.AvailableBytes == destination.Length
            && query.IsInitialized
            && !query.Succeeded
            && query.Status == TcpConnectionCloseIpv4FramePlanDiagnosticFormatStatus.InvalidResult
            && query.RequiredBytes == 0
            && !defaultFormat.IsInitialized
            && !defaultFormat.Succeeded
            && defaultFormat.Status == TcpConnectionCloseIpv4FramePlanDiagnosticFormatStatus.Success
            && defaultFormat.FrameStatus == TcpConnectionCloseIpv4FramePlanStatus.Success
            && defaultFormat.RequiredBytes == 0
            && defaultFormat.AvailableBytes == 0
            && destination[0] == 0xAA
            && destination[63] == 0xAA);
    }

    [HostTest("net.tcp.close.ipv4.formatter uses shared ascii text builder", HostLane.Fast, 6)]
    private static bool TestFormatterUsesSharedAsciiTextBuilder()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "OS.Kernel",
                "Net",
                "TcpConnectionCloseIpv4FramePlanDiagnosticFormatter.cs"));

        return HostCheck.Result(source.Contains("using OS.Std.Text;", StringComparison.Ordinal)
            && source.Contains(
                "AsciiDecimalFormatter.CountInt32Digits(result.BytesWritten)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiDecimalFormatter.CountInt32Digits(result.TcpSegmentOffset)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiDecimalFormatter.CountUInt64Digits(result.LocalPort)",
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
                "private static void AppendUInt64",
                StringComparison.Ordinal)
            && !source.Contains(
                "destination[offset + index] = (byte)('0' +",
                StringComparison.Ordinal)
            && !source.Contains("value /= 10", StringComparison.Ordinal)
            && !source.Contains("value.CopyTo(destination[offset..])", StringComparison.Ordinal));
    }

    private static TcpConnectionCloseIpv4FramePlanResult CreateSuccessfulFramePlan()
    {
        TcpConnectionCloseIpv4FramePlanner planner = CreatePlanner();
        Span<byte> frame = stackalloc byte[TcpConnectionCloseIpv4FramePlanner.HeaderBytes];
        TcpConnectionCloseIpv4FramePlanStatus status = planner.TryWriteActiveFinAckFrame(
            frame,
            out TcpConnectionCloseIpv4FramePlanResult result);

        if (status != TcpConnectionCloseIpv4FramePlanStatus.Success || !result.Succeeded)
        {
            throw new InvalidOperationException("Failed to compose TCP close frame plan fixture.");
        }

        return result;
    }

    private static TcpConnectionCloseIpv4FramePlanner CreatePlanner()
    {
        return TcpConnectionCloseIpv4FramePlanner.Create(
            new TcpConnectionCloseIpv4FramePlannerInfo(
                LocalMac(),
                RemoteMac(),
                LocalAddress(),
                RemoteAddress(),
                LocalPort,
                RemotePort,
                LocalNextSequence,
                RemoteNextSequence,
                windowSize: 0x3000,
                timeToLive: 42,
                identification: 0x398));
    }

    private static bool TryParseTcpFrame(
        ReadOnlySpan<byte> frame,
        out EthernetHeader ethernet,
        out Ipv4Header ipv4,
        out TcpHeader tcp,
        out ReadOnlySpan<byte> tcpSegment)
    {
        tcpSegment = default;
        if (EthernetHeader.TryParse(frame, out ethernet, out ReadOnlySpan<byte> ethernetPayload)
            != PacketParseStatus.Success)
        {
            ipv4 = default;
            tcp = default;
            return false;
        }

        if (Ipv4Header.TryParse(ethernetPayload, out ipv4, out ReadOnlySpan<byte> ipv4Payload)
            != PacketParseStatus.Success)
        {
            tcp = default;
            return false;
        }

        tcpSegment = ipv4Payload;
        return TcpHeader.TryParse(
                ipv4Payload,
                out tcp,
                out ReadOnlySpan<byte> options,
                out ReadOnlySpan<byte> payload) == PacketParseStatus.Success
            && options.Length == 0
            && payload.Length == 0;
    }

    private static MacAddress LocalMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x39, 0x01);
    }

    private static MacAddress RemoteMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x39, 0x80);
    }

    private static Ipv4Address LocalAddress()
    {
        return new Ipv4Address(192, 0, 2, 39);
    }

    private static Ipv4Address RemoteAddress()
    {
        return new Ipv4Address(198, 51, 100, 39);
    }
}
