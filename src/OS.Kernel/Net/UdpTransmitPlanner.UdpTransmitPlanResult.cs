namespace OS.Net;

public readonly struct UdpTransmitPlanResult
{
    private UdpTransmitPlanResult(UdpTransmitPlanStatus status, PacketParseStatus packetStatus, int bytesWritten, MacAddress localMac, Ipv4Address localAddress, MacAddress destinationMac, Ipv4Address destinationAddress, ushort sourcePort, ushort destinationPort, byte timeToLive, ushort identification, int payloadLength)
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

    public UdpTransmitPlanStatus Status
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == UdpTransmitPlanStatus.Success;
    public PacketParseStatus PacketStatus
    {
        get;
    }
    public int BytesWritten
    {
        get;
    }
    public MacAddress LocalMac
    {
        get;
    }
    public Ipv4Address LocalAddress
    {
        get;
    }
    public MacAddress DestinationMac
    {
        get;
    }
    public Ipv4Address DestinationAddress
    {
        get;
    }
    public ushort SourcePort
    {
        get;
    }
    public ushort DestinationPort
    {
        get;
    }
    public byte TimeToLive
    {
        get;
    }
    public ushort Identification
    {
        get;
    }
    public int PayloadLength
    {
        get;
    }

    public static UdpTransmitPlanResult Success(PacketParseStatus packetStatus, int bytesWritten, MacAddress localMac, Ipv4Address localAddress, MacAddress destinationMac, Ipv4Address destinationAddress, ushort sourcePort, ushort destinationPort, byte timeToLive, ushort identification, int payloadLength)
    {
        return new UdpTransmitPlanResult(UdpTransmitPlanStatus.Success, packetStatus, bytesWritten, localMac, localAddress, destinationMac, destinationAddress, sourcePort, destinationPort, timeToLive, identification, payloadLength);
    }

    public static UdpTransmitPlanResult Fail(UdpTransmitPlanStatus status, PacketParseStatus packetStatus, MacAddress localMac, Ipv4Address localAddress, MacAddress destinationMac, Ipv4Address destinationAddress, ushort sourcePort, ushort destinationPort, byte timeToLive, ushort identification, int payloadLength)
    {
        return new UdpTransmitPlanResult(status, packetStatus, bytesWritten: 0, localMac, localAddress, destinationMac, destinationAddress, sourcePort, destinationPort, timeToLive, identification, payloadLength);
    }
}
