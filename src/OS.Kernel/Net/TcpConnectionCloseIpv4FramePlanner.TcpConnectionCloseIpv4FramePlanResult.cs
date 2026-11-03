namespace OS.Net;

using OS.Std.Binary;
using OS.Std.Memory;

public readonly struct TcpConnectionCloseIpv4FramePlanResult
{
    private TcpConnectionCloseIpv4FramePlanResult(TcpConnectionCloseIpv4FramePlanStatus status, TcpConnectionCloseState state, TcpConnectionCloseStatus tcpStatus, PacketParseStatus packetStatus, EthernetHeader ethernetHeader, Ipv4Header ipv4Header, TcpHeader tcpHeader, int bytesWritten, int tcpSegmentOffset, int tcpSegmentLength, MacAddress localMac, MacAddress remoteMac, Ipv4Address localAddress, Ipv4Address remoteAddress, ushort localPort, ushort remotePort, byte timeToLive, ushort identification, uint localNextSequenceNumber, uint remoteNextSequenceNumber)
    {
        Status = status;
        State = state;
        TcpStatus = tcpStatus;
        PacketStatus = packetStatus;
        EthernetHeader = ethernetHeader;
        Ipv4Header = ipv4Header;
        TcpHeader = tcpHeader;
        BytesWritten = bytesWritten;
        TcpSegmentOffset = tcpSegmentOffset;
        TcpSegmentLength = tcpSegmentLength;
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

    public TcpConnectionCloseIpv4FramePlanStatus Status
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == TcpConnectionCloseIpv4FramePlanStatus.Success;
    public TcpConnectionCloseState State
    {
        get;
    }
    public TcpConnectionCloseStatus TcpStatus
    {
        get;
    }
    public PacketParseStatus PacketStatus
    {
        get;
    }
    public EthernetHeader EthernetHeader
    {
        get;
    }
    public Ipv4Header Ipv4Header
    {
        get;
    }
    public TcpHeader TcpHeader
    {
        get;
    }
    public int BytesWritten
    {
        get;
    }
    public int TcpSegmentOffset
    {
        get;
    }
    public int TcpSegmentLength
    {
        get;
    }
    public MacAddress LocalMac
    {
        get;
    }
    public MacAddress RemoteMac
    {
        get;
    }
    public Ipv4Address LocalAddress
    {
        get;
    }
    public Ipv4Address RemoteAddress
    {
        get;
    }
    public ushort LocalPort
    {
        get;
    }
    public ushort RemotePort
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
    public uint LocalNextSequenceNumber
    {
        get;
    }
    public uint RemoteNextSequenceNumber
    {
        get;
    }

    public static TcpConnectionCloseIpv4FramePlanResult Success(TcpConnectionCloseState state, TcpConnectionCloseStatus tcpStatus, PacketParseStatus packetStatus, EthernetHeader ethernetHeader, Ipv4Header ipv4Header, TcpHeader tcpHeader, int bytesWritten, int tcpSegmentOffset, int tcpSegmentLength, in TcpConnectionCloseIpv4FramePlannerInfo info, uint localNextSequenceNumber, uint remoteNextSequenceNumber)
    {
        return new TcpConnectionCloseIpv4FramePlanResult(TcpConnectionCloseIpv4FramePlanStatus.Success, state, tcpStatus, packetStatus, ethernetHeader, ipv4Header, tcpHeader, bytesWritten, tcpSegmentOffset, tcpSegmentLength, info.LocalMac, info.RemoteMac, info.LocalAddress, info.RemoteAddress, info.LocalPort, info.RemotePort, EffectiveTimeToLive(info), info.Identification, localNextSequenceNumber, remoteNextSequenceNumber);
    }

    public static TcpConnectionCloseIpv4FramePlanResult Fail(TcpConnectionCloseIpv4FramePlanStatus status, TcpConnectionCloseState state, TcpConnectionCloseStatus tcpStatus, PacketParseStatus packetStatus, in TcpConnectionCloseIpv4FramePlannerInfo info, uint localNextSequenceNumber, uint remoteNextSequenceNumber)
    {
        return new TcpConnectionCloseIpv4FramePlanResult(status, state, tcpStatus, packetStatus, default, default, default, bytesWritten: 0, tcpSegmentOffset: 0, tcpSegmentLength: 0, info.LocalMac, info.RemoteMac, info.LocalAddress, info.RemoteAddress, info.LocalPort, info.RemotePort, EffectiveTimeToLive(info), info.Identification, localNextSequenceNumber, remoteNextSequenceNumber);
    }

    private static byte EffectiveTimeToLive(in TcpConnectionCloseIpv4FramePlannerInfo info)
    {
        return info.TimeToLive == 0 ? (byte)64 : info.TimeToLive;
    }
}
