using System.Runtime.CompilerServices;
using System.Text;
using OS.Net;
using OS.Std.Binary;

internal static class TcpEstablishedStreamIpv4FramePlannerTests
{
    private const ushort LocalPort = 49182;
    private const ushort RemotePort = 8080;
    private const uint LocalNextSequence = 0x01020304;
    private const uint RemoteNextSequence = 0x10203040;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("net.tcp.stream.ipv4 writes payload checksum", TestWritesPayloadChecksum, ref failures);
        Run("net.tcp.stream.ipv4 receives checksum-valid payload", TestReceivesChecksumValidPayload, ref failures);
        Run("net.tcp.stream.ipv4 rejects bad checksum", TestRejectsBadChecksum, ref failures);
        Run("net.tcp.stream.ipv4 rejects identity and short output", TestRejectsIdentityAndShortOutput, ref failures);
        Run("net.tcp.stream.ipv4 default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("TCP established stream IPv4 frame planner host tests failed: " + failures);
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

    private static bool TestWritesPayloadChecksum()
    {
        Span<byte> receiveBuffer = stackalloc byte[16];
        TcpEstablishedStream stream = CreateStream(receiveBuffer);
        TcpEstablishedStreamIpv4FramePlannerInfo info = CreateInfo(maximumPayloadLength: 4);
        byte[] payload = Encoding.ASCII.GetBytes("abcdef");
        byte[] frame = new byte[TcpEstablishedStreamIpv4FramePlanner.HeaderBytes + 4];

        TcpEstablishedStreamIpv4FramePlanStatus status =
            TcpEstablishedStreamIpv4FramePlanner.TryWritePayloadFrame(
                ref stream,
                info,
                payload,
                frame,
                out TcpEstablishedStreamIpv4FramePlanResult result);

        if (status != TcpEstablishedStreamIpv4FramePlanStatus.Success
            || !result.Succeeded
            || result.StreamStatus != TcpEstablishedStreamStatus.Success
            || result.PacketStatus != PacketParseStatus.Success
            || result.BytesWritten != TcpEstablishedStreamIpv4FramePlanner.HeaderBytes + 4
            || result.TcpSegmentOffset != EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength
            || result.TcpSegmentLength != TcpHeader.MinHeaderLength + 4
            || result.PayloadBytes != 4
            || result.LocalNextSequenceNumber != LocalNextSequence + 4
            || result.RemoteNextSequenceNumber != RemoteNextSequence
            || stream.LocalNextSequenceNumber != LocalNextSequence + 4
            || stream.RemoteNextSequenceNumber != RemoteNextSequence)
        {
            return false;
        }

        if (!TryParseTcpFrame(
                frame.AsSpan(0, result.BytesWritten),
                out EthernetHeader ethernet,
                out Ipv4Header ipv4,
                out TcpHeader tcp,
                out ReadOnlySpan<byte> tcpSegment,
                out ReadOnlySpan<byte> tcpPayload))
        {
            return false;
        }

        Ipv4TransportChecksumStatus checksumStatus = Ipv4TransportChecksum.Validate(
            ipv4.Source,
            ipv4.Destination,
            TcpEstablishedStreamIpv4FramePlanner.ProtocolTcp,
            tcpSegment,
            out Ipv4TransportChecksumResult checksumResult);

        return ethernet.Source == LocalMac()
            && ethernet.Destination == RemoteMac()
            && ipv4.Source == LocalAddress()
            && ipv4.Destination == RemoteAddress()
            && ipv4.Protocol == TcpEstablishedStreamIpv4FramePlanner.ProtocolTcp
            && ipv4.TimeToLive == 42
            && ipv4.Identification == 0x482
            && tcp.SourcePort == LocalPort
            && tcp.DestinationPort == RemotePort
            && tcp.SequenceNumber == LocalNextSequence
            && tcp.AcknowledgementNumber == RemoteNextSequence
            && tcp.Flags == (TcpEstablishedStream.FlagPsh | TcpEstablishedStream.FlagAck)
            && tcp.Checksum != 0
            && result.TcpHeader.Checksum == tcp.Checksum
            && BytesEqual(tcpPayload, payload.AsSpan(0, 4))
            && checksumStatus == Ipv4TransportChecksumStatus.Success
            && checksumResult.IsValid;
    }

    private static bool TestReceivesChecksumValidPayload()
    {
        Span<byte> receiveBuffer = stackalloc byte[16];
        TcpEstablishedStream stream = CreateStream(receiveBuffer);
        TcpEstablishedStreamIpv4FramePlannerInfo info = CreateInfo(maximumPayloadLength: 8);
        byte[] payload = Encoding.ASCII.GetBytes("pong");
        byte[] frame = WriteRemotePayloadFrame(payload, corruptChecksum: false);

        TcpEstablishedStreamIpv4FramePlanStatus status =
            TcpEstablishedStreamIpv4FramePlanner.ReceivePayloadFrame(
                ref stream,
                info,
                frame,
                out TcpEstablishedStreamIpv4FramePlanResult result);

        if (status != TcpEstablishedStreamIpv4FramePlanStatus.Success
            || !result.Succeeded
            || result.StreamStatus != TcpEstablishedStreamStatus.Success
            || result.PacketStatus != PacketParseStatus.Success
            || result.PayloadBytes != payload.Length
            || result.BytesWritten != 0
            || result.LocalNextSequenceNumber != LocalNextSequence
            || result.RemoteNextSequenceNumber != RemoteNextSequence + (uint)payload.Length
            || stream.UnreadBytes != payload.Length
            || stream.AcknowledgementNumber != RemoteNextSequence + (uint)payload.Length)
        {
            return false;
        }

        Span<byte> destination = stackalloc byte[4];
        TcpEstablishedStreamStatus readStatus = stream.Read(
            destination,
            out TcpEstablishedStreamReadResult readResult);

        return readStatus == TcpEstablishedStreamStatus.Success
            && readResult.BytesRead == payload.Length
            && readResult.UnreadBytes == 0
            && BytesEqual(destination, payload);
    }

    private static bool TestRejectsBadChecksum()
    {
        Span<byte> receiveBuffer = stackalloc byte[16];
        TcpEstablishedStream stream = CreateStream(receiveBuffer);
        TcpEstablishedStreamIpv4FramePlannerInfo info = CreateInfo(maximumPayloadLength: 8);
        byte[] frame = WriteRemotePayloadFrame(Encoding.ASCII.GetBytes("pong"), corruptChecksum: true);

        TcpEstablishedStreamIpv4FramePlanStatus status =
            TcpEstablishedStreamIpv4FramePlanner.ReceivePayloadFrame(
                ref stream,
                info,
                frame,
                out TcpEstablishedStreamIpv4FramePlanResult result);

        return status == TcpEstablishedStreamIpv4FramePlanStatus.BadTcpChecksum
            && !result.Succeeded
            && result.PacketStatus == PacketParseStatus.BadChecksum
            && result.TcpHeader.SourcePort == RemotePort
            && result.TcpHeader.DestinationPort == LocalPort
            && result.TcpSegmentLength == TcpHeader.MinHeaderLength + 4
            && stream.RemoteNextSequenceNumber == RemoteNextSequence
            && stream.LocalNextSequenceNumber == LocalNextSequence
            && stream.UnreadBytes == 0;
    }

    private static bool TestRejectsIdentityAndShortOutput()
    {
        Span<byte> receiveBuffer = stackalloc byte[16];
        TcpEstablishedStream stream = CreateStream(receiveBuffer);
        TcpEstablishedStreamIpv4FramePlannerInfo invalidLocal = new(
            default,
            RemoteMac(),
            LocalAddress(),
            RemoteAddress(),
            maximumPayloadLength: 4);
        byte[] frame = new byte[TcpEstablishedStreamIpv4FramePlanner.HeaderBytes];
        TcpEstablishedStreamIpv4FramePlanStatus invalidLocalStatus =
            TcpEstablishedStreamIpv4FramePlanner.TryWritePayloadFrame(
                ref stream,
                invalidLocal,
                Encoding.ASCII.GetBytes("data"),
                frame,
                out TcpEstablishedStreamIpv4FramePlanResult invalidLocalResult);

        TcpEstablishedStreamIpv4FramePlannerInfo info = CreateInfo(maximumPayloadLength: 4);
        TcpEstablishedStreamIpv4FramePlanStatus shortStatus =
            TcpEstablishedStreamIpv4FramePlanner.TryWritePayloadFrame(
                ref stream,
                info,
                Encoding.ASCII.GetBytes("data"),
                frame.AsSpan(0, TcpEstablishedStreamIpv4FramePlanner.HeaderBytes - 1),
                out TcpEstablishedStreamIpv4FramePlanResult shortResult);

        TcpEstablishedStreamIpv4FramePlannerInfo invalidMaximum = CreateInfo(maximumPayloadLength: 0);
        TcpEstablishedStreamIpv4FramePlanStatus maximumStatus =
            TcpEstablishedStreamIpv4FramePlanner.TryWritePayloadFrame(
                ref stream,
                invalidMaximum,
                Encoding.ASCII.GetBytes("data"),
                frame,
                out TcpEstablishedStreamIpv4FramePlanResult maximumResult);

        return invalidLocalStatus == TcpEstablishedStreamIpv4FramePlanStatus.InvalidLocalIdentity
            && !invalidLocalResult.Succeeded
            && shortStatus == TcpEstablishedStreamIpv4FramePlanStatus.FrameBufferTooSmall
            && !shortResult.Succeeded
            && shortResult.StreamStatus == TcpEstablishedStreamStatus.DestinationTooSmall
            && maximumStatus == TcpEstablishedStreamIpv4FramePlanStatus.InvalidMaximumPayloadLength
            && !maximumResult.Succeeded
            && maximumResult.StreamStatus == TcpEstablishedStreamStatus.InvalidMaximumPayloadLength
            && stream.LocalNextSequenceNumber == LocalNextSequence
            && stream.RemoteNextSequenceNumber == RemoteNextSequence
            && stream.UnreadBytes == 0;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        TcpEstablishedStreamIpv4FramePlannerInfo info = CreateInfo(maximumPayloadLength: 4);
        TcpEstablishedStreamIpv4FramePlanResult defaultResult = default;
        TcpEstablishedStreamIpv4FramePlanResult success =
            TcpEstablishedStreamIpv4FramePlanResult.Success(
                TcpEstablishedStreamStatus.Success,
                PacketParseStatus.Success,
                default,
                default,
                default,
                TcpEstablishedStreamIpv4FramePlanner.HeaderBytes,
                EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength,
                TcpHeader.MinHeaderLength,
                payloadBytes: 0,
                info,
                LocalPort,
                RemotePort,
                LocalNextSequence,
                RemoteNextSequence);
        TcpEstablishedStreamIpv4FramePlanResult failure =
            TcpEstablishedStreamIpv4FramePlanResult.Fail(
                TcpEstablishedStreamIpv4FramePlanStatus.BadTcpChecksum,
                TcpEstablishedStreamStatus.Success,
                PacketParseStatus.BadChecksum,
                default,
                default,
                default,
                EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength,
                TcpHeader.MinHeaderLength,
                info,
                LocalPort,
                RemotePort,
                LocalNextSequence,
                RemoteNextSequence);

        return !defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == TcpEstablishedStreamIpv4FramePlanStatus.Success
            && success.IsInitialized
            && success.Succeeded
            && success.BytesWritten == TcpEstablishedStreamIpv4FramePlanner.HeaderBytes
            && success.LocalMac == LocalMac()
            && success.RemoteMac == RemoteMac()
            && success.LocalAddress == LocalAddress()
            && success.RemoteAddress == RemoteAddress()
            && success.LocalPort == LocalPort
            && success.RemotePort == RemotePort
            && success.TimeToLive == 42
            && success.Identification == 0x482
            && failure.IsInitialized
            && !failure.Succeeded
            && failure.Status == TcpEstablishedStreamIpv4FramePlanStatus.BadTcpChecksum
            && failure.PacketStatus == PacketParseStatus.BadChecksum
            && !RuntimeHelpers.IsReferenceOrContainsReferences<TcpEstablishedStreamIpv4FramePlanResult>();
    }

    private static TcpEstablishedStream CreateStream(Span<byte> receiveBuffer)
    {
        return TcpEstablishedStream.Create(
            LocalPort,
            RemotePort,
            LocalNextSequence,
            RemoteNextSequence,
            receiveBuffer);
    }

    private static TcpEstablishedStreamIpv4FramePlannerInfo CreateInfo(int maximumPayloadLength)
    {
        return new TcpEstablishedStreamIpv4FramePlannerInfo(
            LocalMac(),
            RemoteMac(),
            LocalAddress(),
            RemoteAddress(),
            maximumPayloadLength,
            windowSize: 0x3000,
            timeToLive: 42,
            identification: 0x482);
    }

    private static byte[] WriteRemotePayloadFrame(ReadOnlySpan<byte> payload, bool corruptChecksum)
    {
        byte[] frame = new byte[TcpEstablishedStreamIpv4FramePlanner.HeaderBytes + payload.Length];
        Span<byte> tcpSegment = frame.AsSpan(
            EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength,
            TcpHeader.MinHeaderLength + payload.Length);
        PacketParseStatus tcpStatus = TcpHeader.TryWrite(
            tcpSegment,
            RemotePort,
            LocalPort,
            RemoteNextSequence,
            LocalNextSequence,
            TcpEstablishedStream.FlagPsh | TcpEstablishedStream.FlagAck,
            windowSize: 0x2000,
            checksum: 0,
            urgentPointer: 0,
            ReadOnlySpan<byte>.Empty,
            payload,
            out int tcpBytesWritten);
        if (tcpStatus != PacketParseStatus.Success || tcpBytesWritten != tcpSegment.Length)
        {
            throw new InvalidOperationException("failed to write remote tcp segment");
        }

        Ipv4TransportChecksumStatus checksumStatus = Ipv4TransportChecksum.Compute(
            RemoteAddress(),
            LocalAddress(),
            TcpEstablishedStreamIpv4FramePlanner.ProtocolTcp,
            tcpSegment,
            out Ipv4TransportChecksumResult checksumResult);
        if (checksumStatus != Ipv4TransportChecksumStatus.Success)
        {
            throw new InvalidOperationException("failed to compute remote tcp checksum");
        }

        ushort checksum = checksumResult.Checksum == 0 ? ushort.MaxValue : checksumResult.Checksum;
        if (corruptChecksum)
        {
            checksum ^= 0x0100;
        }

        EndianPrimitives.WriteUInt16BigEndian(tcpSegment.Slice(16, sizeof(ushort)), checksum);

        EthernetHeader ethernetHeader = new(
            LocalMac(),
            RemoteMac(),
            EthernetHeader.EtherTypeIpv4);
        if (EthernetHeader.TryWrite(frame, ethernetHeader) != PacketParseStatus.Success)
        {
            throw new InvalidOperationException("failed to write ethernet header");
        }

        Ipv4Header ipv4Header = new(
            RemoteAddress(),
            LocalAddress(),
            TcpEstablishedStreamIpv4FramePlanner.ProtocolTcp,
            (ushort)(Ipv4Header.MinHeaderLength + tcpSegment.Length),
            timeToLive: 64,
            identification: 0x900,
            flagsAndFragmentOffset: 0,
            typeOfService: 0,
            headerChecksum: 0,
            headerLength: Ipv4Header.MinHeaderLength);
        if (Ipv4Header.TryWrite(
                frame.AsSpan(EthernetHeader.HeaderLength, Ipv4Header.MinHeaderLength),
                ipv4Header,
                out int ipv4BytesWritten) != PacketParseStatus.Success
            || ipv4BytesWritten != Ipv4Header.MinHeaderLength)
        {
            throw new InvalidOperationException("failed to write ipv4 header");
        }

        return frame;
    }

    private static bool TryParseTcpFrame(
        ReadOnlySpan<byte> frame,
        out EthernetHeader ethernet,
        out Ipv4Header ipv4,
        out TcpHeader tcp,
        out ReadOnlySpan<byte> tcpSegment,
        out ReadOnlySpan<byte> tcpPayload)
    {
        tcpSegment = default;
        tcpPayload = default;
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
                out tcpPayload) == PacketParseStatus.Success
            && options.Length == 0;
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

    private static MacAddress LocalMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x48, 0x01);
    }

    private static MacAddress RemoteMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x48, 0x80);
    }

    private static Ipv4Address LocalAddress()
    {
        return new Ipv4Address(192, 0, 2, 48);
    }

    private static Ipv4Address RemoteAddress()
    {
        return new Ipv4Address(198, 51, 100, 48);
    }
}
