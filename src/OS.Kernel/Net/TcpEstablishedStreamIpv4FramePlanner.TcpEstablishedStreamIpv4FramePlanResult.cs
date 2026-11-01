namespace OS.Net;
using OS.Std.Binary;
using OS.Std.Memory;

public readonly struct TcpEstablishedStreamIpv4FramePlanResult
{
    private TcpEstablishedStreamIpv4FramePlanResult(TcpEstablishedStreamIpv4FramePlanStatus status, TcpEstablishedStreamStatus streamStatus, PacketParseStatus packetStatus, EthernetHeader ethernetHeader, Ipv4Header ipv4Header, TcpHeader tcpHeader, int bytesWritten, int tcpSegmentOffset, int tcpSegmentLength, int payloadBytes, MacAddress localMac, MacAddress remoteMac, Ipv4Address localAddress, Ipv4Address remoteAddress, ushort localPort, ushort remotePort, byte timeToLive, ushort identification, uint localNextSequenceNumber, uint remoteNextSequenceNumber)
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

    public static TcpEstablishedStreamIpv4FramePlanResult Success(TcpEstablishedStreamStatus streamStatus, PacketParseStatus packetStatus, EthernetHeader ethernetHeader, Ipv4Header ipv4Header, TcpHeader tcpHeader, int bytesWritten, int tcpSegmentOffset, int tcpSegmentLength, int payloadBytes, in TcpEstablishedStreamIpv4FramePlannerInfo info, ushort localPort, ushort remotePort, uint localNextSequenceNumber, uint remoteNextSequenceNumber)
    {
        return new TcpEstablishedStreamIpv4FramePlanResult(TcpEstablishedStreamIpv4FramePlanStatus.Success, streamStatus, packetStatus, ethernetHeader, ipv4Header, tcpHeader, bytesWritten, tcpSegmentOffset, tcpSegmentLength, payloadBytes, info.LocalMac, info.RemoteMac, info.LocalAddress, info.RemoteAddress, localPort, remotePort, EffectiveTimeToLive(info), info.Identification, localNextSequenceNumber, remoteNextSequenceNumber);
    }

    public static TcpEstablishedStreamIpv4FramePlanResult Fail(TcpEstablishedStreamIpv4FramePlanStatus status, TcpEstablishedStreamStatus streamStatus, PacketParseStatus packetStatus, EthernetHeader ethernetHeader, Ipv4Header ipv4Header, TcpHeader tcpHeader, int tcpSegmentOffset, int tcpSegmentLength, in TcpEstablishedStreamIpv4FramePlannerInfo info, ushort localPort, ushort remotePort, uint localNextSequenceNumber, uint remoteNextSequenceNumber)
    {
        return new TcpEstablishedStreamIpv4FramePlanResult(status, streamStatus, packetStatus, ethernetHeader, ipv4Header, tcpHeader, bytesWritten: 0, tcpSegmentOffset, tcpSegmentLength, payloadBytes: 0, info.LocalMac, info.RemoteMac, info.LocalAddress, info.RemoteAddress, localPort, remotePort, EffectiveTimeToLive(info), info.Identification, localNextSequenceNumber, remoteNextSequenceNumber);
    }

    private static byte EffectiveTimeToLive(in TcpEstablishedStreamIpv4FramePlannerInfo info)
    {
        return info.TimeToLive == 0 ? (byte)64 : info.TimeToLive;
    }
}
