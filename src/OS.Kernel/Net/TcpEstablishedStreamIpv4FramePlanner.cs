namespace OS.Net;

using OS.Std.Binary;
using OS.Std.Memory;

public enum TcpEstablishedStreamIpv4FramePlanStatus
{
    Success = 0,
    InvalidLocalIdentity = 1,
    InvalidRemoteIdentity = 2,
    InvalidMaximumPayloadLength = 3,
    FrameBufferTooSmall = 4,
    TcpStreamFailed = 5,
    TcpChecksumFailed = 6,
    FrameWriteFailed = 7,
    FrameParseFailed = 8,
    UnsupportedEtherType = 9,
    UnsupportedIpProtocol = 10,
    WrongLocalMac = 11,
    WrongRemoteMac = 12,
    WrongLocalAddress = 13,
    WrongRemoteAddress = 14,
    TcpSegmentParseFailed = 15,
    BadTcpChecksum = 16
}

public readonly struct TcpEstablishedStreamIpv4FramePlannerInfo
{
    public TcpEstablishedStreamIpv4FramePlannerInfo(
        MacAddress localMac,
        MacAddress remoteMac,
        Ipv4Address localAddress,
        Ipv4Address remoteAddress,
        int maximumPayloadLength,
        ushort windowSize = 0x4000,
        byte timeToLive = 64,
        ushort identification = 0)
    {
        LocalMac = localMac;
        RemoteMac = remoteMac;
        LocalAddress = localAddress;
        RemoteAddress = remoteAddress;
        MaximumPayloadLength = maximumPayloadLength;
        WindowSize = windowSize;
        TimeToLive = timeToLive;
        Identification = identification;
    }

    public MacAddress LocalMac { get; }

    public MacAddress RemoteMac { get; }

    public Ipv4Address LocalAddress { get; }

    public Ipv4Address RemoteAddress { get; }

    public int MaximumPayloadLength { get; }

    public ushort WindowSize { get; }

    public byte TimeToLive { get; }

    public ushort Identification { get; }
}

public readonly struct TcpEstablishedStreamIpv4FramePlanResult
{
    private TcpEstablishedStreamIpv4FramePlanResult(
        TcpEstablishedStreamIpv4FramePlanStatus status,
        TcpEstablishedStreamStatus streamStatus,
        PacketParseStatus packetStatus,
        EthernetHeader ethernetHeader,
        Ipv4Header ipv4Header,
        TcpHeader tcpHeader,
        int bytesWritten,
        int tcpSegmentOffset,
        int tcpSegmentLength,
        int payloadBytes,
        MacAddress localMac,
        MacAddress remoteMac,
        Ipv4Address localAddress,
        Ipv4Address remoteAddress,
        ushort localPort,
        ushort remotePort,
        byte timeToLive,
        ushort identification,
        uint localNextSequenceNumber,
        uint remoteNextSequenceNumber)
    {
        Status = status;
        StreamStatus = streamStatus;
        PacketStatus = packetStatus;
        EthernetHeader = ethernetHeader;
        Ipv4Header = ipv4Header;
        TcpHeader = tcpHeader;
        BytesWritten = bytesWritten;
        TcpSegmentOffset = tcpSegmentOffset;
        TcpSegmentLength = tcpSegmentLength;
        PayloadBytes = payloadBytes;
        LocalMac = localMac;
        RemoteMac = remoteMac;
        LocalAddress = localAddress;
        RemoteAddress = remoteAddress;
        LocalPort = localPort;
        RemotePort = remotePort;
        TimeToLive = timeToLive;
        Identification = identification;
        LocalNextSequenceNumber = localNextSequenceNumber;
        RemoteNextSequenceNumber = remoteNextSequenceNumber;
        IsInitialized = true;
    }

    public TcpEstablishedStreamIpv4FramePlanStatus Status { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == TcpEstablishedStreamIpv4FramePlanStatus.Success;

    public TcpEstablishedStreamStatus StreamStatus { get; }

    public PacketParseStatus PacketStatus { get; }

    public EthernetHeader EthernetHeader { get; }

    public Ipv4Header Ipv4Header { get; }

    public TcpHeader TcpHeader { get; }

    public int BytesWritten { get; }

    public int TcpSegmentOffset { get; }

    public int TcpSegmentLength { get; }

    public int PayloadBytes { get; }

    public MacAddress LocalMac { get; }

    public MacAddress RemoteMac { get; }

    public Ipv4Address LocalAddress { get; }

    public Ipv4Address RemoteAddress { get; }

    public ushort LocalPort { get; }

    public ushort RemotePort { get; }

    public byte TimeToLive { get; }

    public ushort Identification { get; }

    public uint LocalNextSequenceNumber { get; }

    public uint RemoteNextSequenceNumber { get; }

    public static TcpEstablishedStreamIpv4FramePlanResult Success(
        TcpEstablishedStreamStatus streamStatus,
        PacketParseStatus packetStatus,
        EthernetHeader ethernetHeader,
        Ipv4Header ipv4Header,
        TcpHeader tcpHeader,
        int bytesWritten,
        int tcpSegmentOffset,
        int tcpSegmentLength,
        int payloadBytes,
        in TcpEstablishedStreamIpv4FramePlannerInfo info,
        ushort localPort,
        ushort remotePort,
        uint localNextSequenceNumber,
        uint remoteNextSequenceNumber)
    {
        return new TcpEstablishedStreamIpv4FramePlanResult(
            TcpEstablishedStreamIpv4FramePlanStatus.Success,
            streamStatus,
            packetStatus,
            ethernetHeader,
            ipv4Header,
            tcpHeader,
            bytesWritten,
            tcpSegmentOffset,
            tcpSegmentLength,
            payloadBytes,
            info.LocalMac,
            info.RemoteMac,
            info.LocalAddress,
            info.RemoteAddress,
            localPort,
            remotePort,
            EffectiveTimeToLive(info),
            info.Identification,
            localNextSequenceNumber,
            remoteNextSequenceNumber);
    }

    public static TcpEstablishedStreamIpv4FramePlanResult Fail(
        TcpEstablishedStreamIpv4FramePlanStatus status,
        TcpEstablishedStreamStatus streamStatus,
        PacketParseStatus packetStatus,
        EthernetHeader ethernetHeader,
        Ipv4Header ipv4Header,
        TcpHeader tcpHeader,
        int tcpSegmentOffset,
        int tcpSegmentLength,
        in TcpEstablishedStreamIpv4FramePlannerInfo info,
        ushort localPort,
        ushort remotePort,
        uint localNextSequenceNumber,
        uint remoteNextSequenceNumber)
    {
        return new TcpEstablishedStreamIpv4FramePlanResult(
            status,
            streamStatus,
            packetStatus,
            ethernetHeader,
            ipv4Header,
            tcpHeader,
            bytesWritten: 0,
            tcpSegmentOffset,
            tcpSegmentLength,
            payloadBytes: 0,
            info.LocalMac,
            info.RemoteMac,
            info.LocalAddress,
            info.RemoteAddress,
            localPort,
            remotePort,
            EffectiveTimeToLive(info),
            info.Identification,
            localNextSequenceNumber,
            remoteNextSequenceNumber);
    }

    private static byte EffectiveTimeToLive(in TcpEstablishedStreamIpv4FramePlannerInfo info)
    {
        return info.TimeToLive == 0 ? (byte)64 : info.TimeToLive;
    }
}

public static class TcpEstablishedStreamIpv4FramePlanner
{
    public const byte ProtocolTcp = 6;

    public const int HeaderBytes =
        EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength + TcpHeader.MinHeaderLength;

    public static TcpEstablishedStreamIpv4FramePlanStatus TryWritePayloadFrame(
        ref TcpEstablishedStream stream,
        in TcpEstablishedStreamIpv4FramePlannerInfo info,
        ReadOnlySpan<byte> payload,
        Span<byte> frame,
        out TcpEstablishedStreamIpv4FramePlanResult result)
    {
        TcpEstablishedStreamIpv4FramePlanStatus identityStatus = ValidateIdentity(stream, info);
        if (identityStatus != TcpEstablishedStreamIpv4FramePlanStatus.Success)
        {
            result = Fail(stream, info, identityStatus);
            return result.Status;
        }

        if (info.MaximumPayloadLength <= 0)
        {
            result = Fail(
                stream,
                info,
                TcpEstablishedStreamIpv4FramePlanStatus.InvalidMaximumPayloadLength,
                TcpEstablishedStreamStatus.InvalidMaximumPayloadLength,
                PacketParseStatus.Success);
            return result.Status;
        }

        if (frame.Length < HeaderBytes)
        {
            result = Fail(
                stream,
                info,
                TcpEstablishedStreamIpv4FramePlanStatus.FrameBufferTooSmall,
                TcpEstablishedStreamStatus.DestinationTooSmall,
                PacketParseStatus.BufferTooSmall);
            return result.Status;
        }

        TcpEstablishedStream pending = stream;
        int tcpSegmentOffset = EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength;
        Span<byte> tcpSegmentDestination = frame.Slice(tcpSegmentOffset);
        TcpEstablishedStreamStatus streamStatus = pending.TryWritePayloadSegment(
            payload,
            info.MaximumPayloadLength,
            tcpSegmentDestination,
            info.WindowSize,
            checksum: 0,
            out TcpEstablishedStreamSendResult sendResult);
        if (streamStatus != TcpEstablishedStreamStatus.Success)
        {
            result = Fail(
                stream,
                info,
                TcpEstablishedStreamIpv4FramePlanStatus.TcpStreamFailed,
                streamStatus,
                sendResult.PacketStatus);
            return result.Status;
        }

        Span<byte> tcpSegment = tcpSegmentDestination.Slice(0, sendResult.SegmentBytesWritten);
        PacketParseStatus checksumStatus = TryWriteTcpChecksum(
            tcpSegment,
            info.LocalAddress,
            info.RemoteAddress);
        if (checksumStatus != PacketParseStatus.Success)
        {
            result = Fail(
                stream,
                info,
                TcpEstablishedStreamIpv4FramePlanStatus.TcpChecksumFailed,
                streamStatus,
                checksumStatus,
                tcpSegmentOffset,
                sendResult.SegmentBytesWritten);
            return result.Status;
        }

        PacketParseStatus frameStatus = TryWriteFrame(
            info,
            frame,
            tcpSegment,
            out EthernetHeader ethernetHeader,
            out Ipv4Header ipv4Header,
            out int bytesWritten);
        if (frameStatus != PacketParseStatus.Success)
        {
            result = Fail(
                stream,
                info,
                MapFrameWriteFailure(frameStatus),
                streamStatus,
                frameStatus,
                tcpSegmentOffset,
                sendResult.SegmentBytesWritten);
            return result.Status;
        }

        PacketParseStatus tcpParseStatus = TcpHeader.TryParse(
            tcpSegment,
            out TcpHeader tcpHeader,
            out ReadOnlySpan<byte> _,
            out ReadOnlySpan<byte> _);

        stream = pending;
        result = TcpEstablishedStreamIpv4FramePlanResult.Success(
            streamStatus,
            tcpParseStatus,
            ethernetHeader,
            ipv4Header,
            tcpHeader,
            bytesWritten,
            tcpSegmentOffset,
            sendResult.SegmentBytesWritten,
            sendResult.PayloadBytesWritten,
            info,
            stream.LocalPort,
            stream.RemotePort,
            stream.LocalNextSequenceNumber,
            stream.RemoteNextSequenceNumber);
        return result.Status;
    }

    public static TcpEstablishedStreamIpv4FramePlanStatus ReceivePayloadFrame(
        ref TcpEstablishedStream stream,
        in TcpEstablishedStreamIpv4FramePlannerInfo info,
        ReadOnlySpan<byte> frame,
        out TcpEstablishedStreamIpv4FramePlanResult result)
    {
        TcpEstablishedStreamIpv4FramePlanStatus identityStatus = ValidateIdentity(stream, info);
        if (identityStatus != TcpEstablishedStreamIpv4FramePlanStatus.Success)
        {
            result = Fail(stream, info, identityStatus);
            return result.Status;
        }

        PacketParseStatus frameParseStatus = TryParseFrame(
            info,
            frame,
            out EthernetHeader ethernetHeader,
            out Ipv4Header ipv4Header,
            out ReadOnlySpan<byte> tcpSegment,
            out int tcpSegmentOffset,
            out int tcpSegmentLength,
            out TcpEstablishedStreamIpv4FramePlanStatus frameStatus);
        if (frameParseStatus != PacketParseStatus.Success || frameStatus != TcpEstablishedStreamIpv4FramePlanStatus.Success)
        {
            result = TcpEstablishedStreamIpv4FramePlanResult.Fail(
                frameStatus,
                TcpEstablishedStreamStatus.Success,
                frameParseStatus,
                ethernetHeader,
                ipv4Header,
                default,
                tcpSegmentOffset,
                tcpSegmentLength,
                info,
                stream.LocalPort,
                stream.RemotePort,
                stream.LocalNextSequenceNumber,
                stream.RemoteNextSequenceNumber);
            return result.Status;
        }

        PacketParseStatus tcpParseStatus = TcpHeader.TryParse(
            tcpSegment,
            out TcpHeader tcpHeader,
            out ReadOnlySpan<byte> _,
            out ReadOnlySpan<byte> _);
        if (tcpParseStatus != PacketParseStatus.Success)
        {
            result = TcpEstablishedStreamIpv4FramePlanResult.Fail(
                TcpEstablishedStreamIpv4FramePlanStatus.TcpSegmentParseFailed,
                TcpEstablishedStreamStatus.SegmentParseFailed,
                tcpParseStatus,
                ethernetHeader,
                ipv4Header,
                tcpHeader,
                tcpSegmentOffset,
                tcpSegmentLength,
                info,
                stream.LocalPort,
                stream.RemotePort,
                stream.LocalNextSequenceNumber,
                stream.RemoteNextSequenceNumber);
            return result.Status;
        }

        Ipv4TransportChecksumStatus checksumStatus = Ipv4TransportChecksum.Validate(
            ipv4Header.Source,
            ipv4Header.Destination,
            ProtocolTcp,
            tcpSegment,
            out Ipv4TransportChecksumResult checksumResult);
        if (checksumStatus != Ipv4TransportChecksumStatus.Success || !checksumResult.IsValid)
        {
            result = TcpEstablishedStreamIpv4FramePlanResult.Fail(
                TcpEstablishedStreamIpv4FramePlanStatus.BadTcpChecksum,
                TcpEstablishedStreamStatus.Success,
                PacketParseStatus.BadChecksum,
                ethernetHeader,
                ipv4Header,
                tcpHeader,
                tcpSegmentOffset,
                tcpSegmentLength,
                info,
                stream.LocalPort,
                stream.RemotePort,
                stream.LocalNextSequenceNumber,
                stream.RemoteNextSequenceNumber);
            return result.Status;
        }

        TcpEstablishedStream pending = stream;
        TcpEstablishedStreamStatus streamStatus = pending.ReceiveSegment(
            tcpSegment,
            out TcpEstablishedStreamReceiveResult receiveResult);
        if (streamStatus != TcpEstablishedStreamStatus.Success)
        {
            result = TcpEstablishedStreamIpv4FramePlanResult.Fail(
                TcpEstablishedStreamIpv4FramePlanStatus.TcpStreamFailed,
                streamStatus,
                receiveResult.PacketStatus,
                ethernetHeader,
                ipv4Header,
                tcpHeader,
                tcpSegmentOffset,
                tcpSegmentLength,
                info,
                stream.LocalPort,
                stream.RemotePort,
                stream.LocalNextSequenceNumber,
                stream.RemoteNextSequenceNumber);
            return result.Status;
        }

        stream = pending;
        result = TcpEstablishedStreamIpv4FramePlanResult.Success(
            streamStatus,
            tcpParseStatus,
            ethernetHeader,
            ipv4Header,
            tcpHeader,
            bytesWritten: 0,
            tcpSegmentOffset,
            tcpSegmentLength,
            receiveResult.PayloadBytesAccepted,
            info,
            stream.LocalPort,
            stream.RemotePort,
            stream.LocalNextSequenceNumber,
            stream.RemoteNextSequenceNumber);
        return result.Status;
    }

    private static PacketParseStatus TryWriteFrame(
        in TcpEstablishedStreamIpv4FramePlannerInfo info,
        Span<byte> frame,
        ReadOnlySpan<byte> tcpSegment,
        out EthernetHeader ethernetHeader,
        out Ipv4Header ipv4Header,
        out int bytesWritten)
    {
        bytesWritten = 0;
        ethernetHeader = new EthernetHeader(
            info.RemoteMac,
            info.LocalMac,
            EthernetHeader.EtherTypeIpv4);
        ipv4Header = default;

        if (tcpSegment.Length > ushort.MaxValue - Ipv4Header.MinHeaderLength)
        {
            return PacketParseStatus.InvalidLength;
        }

        int ipv4Length = Ipv4Header.MinHeaderLength + tcpSegment.Length;
        int frameLength = EthernetHeader.HeaderLength + ipv4Length;
        if (frame.Length < frameLength)
        {
            return PacketParseStatus.BufferTooSmall;
        }

        PacketParseStatus ethernetStatus = EthernetHeader.TryWrite(frame, ethernetHeader);
        if (ethernetStatus != PacketParseStatus.Success)
        {
            return ethernetStatus;
        }

        ipv4Header = Ipv4Header.Create(
            info.LocalAddress,
            info.RemoteAddress,
            ProtocolTcp,
            (ushort)tcpSegment.Length,
            info.TimeToLive == 0 ? (byte)64 : info.TimeToLive,
            info.Identification,
            flagsAndFragmentOffset: 0);
        PacketParseStatus ipv4Status = Ipv4Header.TryWrite(
            frame.Slice(EthernetHeader.HeaderLength, Ipv4Header.MinHeaderLength),
            ipv4Header,
            out int ipv4BytesWritten);
        if (ipv4Status != PacketParseStatus.Success)
        {
            return ipv4Status;
        }

        BufferOperationResult copy = BufferOperations.MemoryCopy(
            tcpSegment,
            frame.Slice(EthernetHeader.HeaderLength + ipv4BytesWritten, tcpSegment.Length));
        if (!copy.Succeeded)
        {
            return PacketParseStatus.BufferTooSmall;
        }

        bytesWritten = EthernetHeader.HeaderLength + ipv4BytesWritten + tcpSegment.Length;
        return PacketParseStatus.Success;
    }

    private static PacketParseStatus TryWriteTcpChecksum(
        Span<byte> tcpSegment,
        Ipv4Address sourceAddress,
        Ipv4Address destinationAddress)
    {
        if (tcpSegment.Length < TcpHeader.MinHeaderLength)
        {
            return PacketParseStatus.InvalidLength;
        }

        EndianPrimitives.WriteUInt16BigEndian(
            tcpSegment.Slice(16, sizeof(ushort)),
            0);
        Ipv4TransportChecksumStatus checksumStatus = Ipv4TransportChecksum.Compute(
            sourceAddress,
            destinationAddress,
            ProtocolTcp,
            tcpSegment,
            out Ipv4TransportChecksumResult checksumResult);
        if (checksumStatus != Ipv4TransportChecksumStatus.Success)
        {
            return PacketParseStatus.InvalidLength;
        }

        ushort checksum = checksumResult.Checksum == 0
            ? ushort.MaxValue
            : checksumResult.Checksum;
        EndianPrimitives.WriteUInt16BigEndian(
            tcpSegment.Slice(16, sizeof(ushort)),
            checksum);
        return PacketParseStatus.Success;
    }

    private static PacketParseStatus TryParseFrame(
        in TcpEstablishedStreamIpv4FramePlannerInfo info,
        ReadOnlySpan<byte> frame,
        out EthernetHeader ethernetHeader,
        out Ipv4Header ipv4Header,
        out ReadOnlySpan<byte> tcpSegment,
        out int tcpSegmentOffset,
        out int tcpSegmentLength,
        out TcpEstablishedStreamIpv4FramePlanStatus frameStatus)
    {
        tcpSegment = default;
        tcpSegmentOffset = 0;
        tcpSegmentLength = 0;
        ipv4Header = default;

        PacketParseStatus ethernetStatus = EthernetHeader.TryParse(
            frame,
            out ethernetHeader,
            out ReadOnlySpan<byte> ethernetPayload);
        if (ethernetStatus != PacketParseStatus.Success)
        {
            frameStatus = TcpEstablishedStreamIpv4FramePlanStatus.FrameParseFailed;
            return ethernetStatus;
        }

        if (ethernetHeader.EtherType != EthernetHeader.EtherTypeIpv4)
        {
            frameStatus = TcpEstablishedStreamIpv4FramePlanStatus.UnsupportedEtherType;
            return PacketParseStatus.UnsupportedEtherType;
        }

        if (ethernetHeader.Destination != info.LocalMac)
        {
            frameStatus = TcpEstablishedStreamIpv4FramePlanStatus.WrongLocalMac;
            return PacketParseStatus.Success;
        }

        if (ethernetHeader.Source != info.RemoteMac)
        {
            frameStatus = TcpEstablishedStreamIpv4FramePlanStatus.WrongRemoteMac;
            return PacketParseStatus.Success;
        }

        PacketParseStatus ipv4Status = Ipv4Header.TryParse(
            ethernetPayload,
            out ipv4Header,
            out ReadOnlySpan<byte> ipv4Payload);
        if (ipv4Status != PacketParseStatus.Success)
        {
            frameStatus = TcpEstablishedStreamIpv4FramePlanStatus.FrameParseFailed;
            return ipv4Status;
        }

        if (ipv4Header.Protocol != ProtocolTcp)
        {
            frameStatus = TcpEstablishedStreamIpv4FramePlanStatus.UnsupportedIpProtocol;
            return PacketParseStatus.UnsupportedIpProtocol;
        }

        if (ipv4Header.Destination != info.LocalAddress)
        {
            frameStatus = TcpEstablishedStreamIpv4FramePlanStatus.WrongLocalAddress;
            return PacketParseStatus.Success;
        }

        if (ipv4Header.Source != info.RemoteAddress)
        {
            frameStatus = TcpEstablishedStreamIpv4FramePlanStatus.WrongRemoteAddress;
            return PacketParseStatus.Success;
        }

        tcpSegment = ipv4Payload;
        tcpSegmentOffset = EthernetHeader.HeaderLength + ipv4Header.HeaderLength;
        tcpSegmentLength = ipv4Payload.Length;
        frameStatus = TcpEstablishedStreamIpv4FramePlanStatus.Success;
        return PacketParseStatus.Success;
    }

    private static TcpEstablishedStreamIpv4FramePlanStatus ValidateIdentity(
        in TcpEstablishedStream stream,
        in TcpEstablishedStreamIpv4FramePlannerInfo info)
    {
        if (info.LocalMac.IsZero
            || info.LocalMac.IsBroadcast
            || info.LocalAddress == default
            || stream.LocalPort == 0)
        {
            return TcpEstablishedStreamIpv4FramePlanStatus.InvalidLocalIdentity;
        }

        if (info.RemoteMac.IsZero
            || info.RemoteMac.IsBroadcast
            || info.RemoteAddress == default
            || stream.RemotePort == 0)
        {
            return TcpEstablishedStreamIpv4FramePlanStatus.InvalidRemoteIdentity;
        }

        return TcpEstablishedStreamIpv4FramePlanStatus.Success;
    }

    private static TcpEstablishedStreamIpv4FramePlanResult Fail(
        in TcpEstablishedStream stream,
        in TcpEstablishedStreamIpv4FramePlannerInfo info,
        TcpEstablishedStreamIpv4FramePlanStatus status)
    {
        return Fail(
            stream,
            info,
            status,
            TcpEstablishedStreamStatus.Success,
            PacketParseStatus.Success);
    }

    private static TcpEstablishedStreamIpv4FramePlanResult Fail(
        in TcpEstablishedStream stream,
        in TcpEstablishedStreamIpv4FramePlannerInfo info,
        TcpEstablishedStreamIpv4FramePlanStatus status,
        TcpEstablishedStreamStatus streamStatus,
        PacketParseStatus packetStatus,
        int tcpSegmentOffset = 0,
        int tcpSegmentLength = 0)
    {
        return TcpEstablishedStreamIpv4FramePlanResult.Fail(
            status,
            streamStatus,
            packetStatus,
            default,
            default,
            default,
            tcpSegmentOffset,
            tcpSegmentLength,
            info,
            stream.LocalPort,
            stream.RemotePort,
            stream.LocalNextSequenceNumber,
            stream.RemoteNextSequenceNumber);
    }

    private static TcpEstablishedStreamIpv4FramePlanStatus MapFrameWriteFailure(PacketParseStatus status)
    {
        return status switch
        {
            PacketParseStatus.BufferTooSmall => TcpEstablishedStreamIpv4FramePlanStatus.FrameBufferTooSmall,
            _ => TcpEstablishedStreamIpv4FramePlanStatus.FrameWriteFailed
        };
    }

}
