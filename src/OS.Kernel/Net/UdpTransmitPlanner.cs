namespace OS.Net;

public enum UdpTransmitPlanStatus
{
    Success = 0,
    InvalidLocalIdentity = 1,
    InvalidDestinationIdentity = 2,
    PayloadTooLarge = 3,
    FrameBufferTooSmall = 4,
    PacketWriteFailed = 5
}

public readonly struct UdpTransmitPlanResult
{
    private UdpTransmitPlanResult(
        UdpTransmitPlanStatus status,
        PacketParseStatus packetStatus,
        int bytesWritten,
        MacAddress localMac,
        Ipv4Address localAddress,
        MacAddress destinationMac,
        Ipv4Address destinationAddress,
        ushort sourcePort,
        ushort destinationPort,
        byte timeToLive,
        ushort identification,
        int payloadLength)
    {
        Status = status;
        PacketStatus = packetStatus;
        BytesWritten = bytesWritten;
        LocalMac = localMac;
        LocalAddress = localAddress;
        DestinationMac = destinationMac;
        DestinationAddress = destinationAddress;
        SourcePort = sourcePort;
        DestinationPort = destinationPort;
        TimeToLive = timeToLive;
        Identification = identification;
        PayloadLength = payloadLength;
        IsInitialized = true;
    }

    public UdpTransmitPlanStatus Status { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == UdpTransmitPlanStatus.Success;

    public PacketParseStatus PacketStatus { get; }

    public int BytesWritten { get; }

    public MacAddress LocalMac { get; }

    public Ipv4Address LocalAddress { get; }

    public MacAddress DestinationMac { get; }

    public Ipv4Address DestinationAddress { get; }

    public ushort SourcePort { get; }

    public ushort DestinationPort { get; }

    public byte TimeToLive { get; }

    public ushort Identification { get; }

    public int PayloadLength { get; }

    public static UdpTransmitPlanResult Success(
        PacketParseStatus packetStatus,
        int bytesWritten,
        MacAddress localMac,
        Ipv4Address localAddress,
        MacAddress destinationMac,
        Ipv4Address destinationAddress,
        ushort sourcePort,
        ushort destinationPort,
        byte timeToLive,
        ushort identification,
        int payloadLength)
    {
        return new UdpTransmitPlanResult(
            UdpTransmitPlanStatus.Success,
            packetStatus,
            bytesWritten,
            localMac,
            localAddress,
            destinationMac,
            destinationAddress,
            sourcePort,
            destinationPort,
            timeToLive,
            identification,
            payloadLength);
    }

    public static UdpTransmitPlanResult Fail(
        UdpTransmitPlanStatus status,
        PacketParseStatus packetStatus,
        MacAddress localMac,
        Ipv4Address localAddress,
        MacAddress destinationMac,
        Ipv4Address destinationAddress,
        ushort sourcePort,
        ushort destinationPort,
        byte timeToLive,
        ushort identification,
        int payloadLength)
    {
        return new UdpTransmitPlanResult(
            status,
            packetStatus,
            bytesWritten: 0,
            localMac,
            localAddress,
            destinationMac,
            destinationAddress,
            sourcePort,
            destinationPort,
            timeToLive,
            identification,
            payloadLength);
    }
}

public static class UdpTransmitPlanner
{
    public const int HeaderBytes =
        EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength + UdpHeader.HeaderLength;

    public const int MaximumPayloadBytes =
        ushort.MaxValue - Ipv4Header.MinHeaderLength - UdpHeader.HeaderLength;

    public static UdpTransmitPlanStatus TryPlan(
        MacAddress localMac,
        Ipv4Address localAddress,
        MacAddress destinationMac,
        Ipv4Address destinationAddress,
        ushort sourcePort,
        ushort destinationPort,
        byte timeToLive,
        ushort identification,
        ReadOnlySpan<byte> payload,
        Span<byte> frame,
        out UdpTransmitPlanResult result)
    {
        if (localMac.IsZero || localMac.IsBroadcast || localAddress == default || sourcePort == 0)
        {
            result = Fail(
                UdpTransmitPlanStatus.InvalidLocalIdentity,
                PacketParseStatus.Success,
                localMac,
                localAddress,
                destinationMac,
                destinationAddress,
                sourcePort,
                destinationPort,
                timeToLive,
                identification,
                payload.Length);
            return result.Status;
        }

        if (destinationMac.IsZero || destinationAddress == default || destinationPort == 0)
        {
            result = Fail(
                UdpTransmitPlanStatus.InvalidDestinationIdentity,
                PacketParseStatus.Success,
                localMac,
                localAddress,
                destinationMac,
                destinationAddress,
                sourcePort,
                destinationPort,
                timeToLive,
                identification,
                payload.Length);
            return result.Status;
        }

        if (payload.Length > MaximumPayloadBytes)
        {
            result = Fail(
                UdpTransmitPlanStatus.PayloadTooLarge,
                PacketParseStatus.InvalidLength,
                localMac,
                localAddress,
                destinationMac,
                destinationAddress,
                sourcePort,
                destinationPort,
                timeToLive,
                identification,
                payload.Length);
            return result.Status;
        }

        if (frame.Length < HeaderBytes + payload.Length)
        {
            result = Fail(
                UdpTransmitPlanStatus.FrameBufferTooSmall,
                PacketParseStatus.BufferTooSmall,
                localMac,
                localAddress,
                destinationMac,
                destinationAddress,
                sourcePort,
                destinationPort,
                timeToLive,
                identification,
                payload.Length);
            return result.Status;
        }

        byte effectiveTimeToLive = timeToLive == 0 ? (byte)64 : timeToLive;
        EthernetIpv4UdpPacketInfo info = new EthernetIpv4UdpPacketInfo(
            destinationMac,
            localMac,
            localAddress,
            destinationAddress,
            sourcePort,
            destinationPort,
            effectiveTimeToLive,
            identification);

        PacketParseStatus packetStatus = EthernetIpv4UdpPacket.TryWrite(
            frame,
            info,
            payload,
            out int bytesWritten);
        if (packetStatus != PacketParseStatus.Success)
        {
            UdpTransmitPlanStatus status = MapPacketWriteStatus(packetStatus);
            result = Fail(
                status,
                packetStatus,
                localMac,
                localAddress,
                destinationMac,
                destinationAddress,
                sourcePort,
                destinationPort,
                effectiveTimeToLive,
                identification,
                payload.Length);
            return result.Status;
        }

        result = UdpTransmitPlanResult.Success(
            packetStatus,
            bytesWritten,
            localMac,
            localAddress,
            destinationMac,
            destinationAddress,
            sourcePort,
            destinationPort,
            effectiveTimeToLive,
            identification,
            payload.Length);
        return result.Status;
    }

    private static UdpTransmitPlanResult Fail(
        UdpTransmitPlanStatus status,
        PacketParseStatus packetStatus,
        MacAddress localMac,
        Ipv4Address localAddress,
        MacAddress destinationMac,
        Ipv4Address destinationAddress,
        ushort sourcePort,
        ushort destinationPort,
        byte timeToLive,
        ushort identification,
        int payloadLength)
    {
        return UdpTransmitPlanResult.Fail(
            status,
            packetStatus,
            localMac,
            localAddress,
            destinationMac,
            destinationAddress,
            sourcePort,
            destinationPort,
            timeToLive,
            identification,
            payloadLength);
    }

    private static UdpTransmitPlanStatus MapPacketWriteStatus(PacketParseStatus status)
    {
        return status switch
        {
            PacketParseStatus.BufferTooSmall => UdpTransmitPlanStatus.FrameBufferTooSmall,
            PacketParseStatus.InvalidLength => UdpTransmitPlanStatus.PayloadTooLarge,
            _ => UdpTransmitPlanStatus.PacketWriteFailed
        };
    }
}
