namespace OS.Net;

using OS.Std.Binary;
using OS.Std.Memory;

public readonly struct TcpIpv4FramePlanResult
{
    private TcpIpv4FramePlanResult(TcpIpv4FramePlanStatus status, TcpActiveConnectState state, TcpActiveConnectStatus tcpStatus, PacketParseStatus packetStatus, EthernetHeader ethernetHeader, Ipv4Header ipv4Header, TcpHeader tcpHeader, int bytesWritten, int tcpSegmentOffset, int tcpSegmentLength, MacAddress localMac, MacAddress remoteMac, Ipv4Address localAddress, Ipv4Address remoteAddress, ushort localPort, ushort remotePort, byte timeToLive, ushort identification, uint localNextSequenceNumber, uint remoteInitialSequenceNumber, uint remoteNextSequenceNumber)
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
        RemoteInitialSequenceNumber = remoteInitialSequenceNumber;
        RemoteNextSequenceNumber = remoteNextSequenceNumber;
        IsInitialized = true;
    }

    public TcpIpv4FramePlanStatus Status
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == TcpIpv4FramePlanStatus.Success;
    public TcpActiveConnectState State
    {
        get;
    }
    public TcpActiveConnectStatus TcpStatus
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
    public uint RemoteInitialSequenceNumber
    {
        get;
    }
    public uint RemoteNextSequenceNumber
    {
        get;
    }

    public static TcpIpv4FramePlanResult Success(TcpActiveConnectState state, TcpActiveConnectStatus tcpStatus, PacketParseStatus packetStatus, EthernetHeader ethernetHeader, Ipv4Header ipv4Header, TcpHeader tcpHeader, int bytesWritten, int tcpSegmentOffset, int tcpSegmentLength, in TcpIpv4FramePlannerInfo info, uint localNextSequenceNumber, uint remoteInitialSequenceNumber, uint remoteNextSequenceNumber)
    {
        return new TcpIpv4FramePlanResult(TcpIpv4FramePlanStatus.Success, state, tcpStatus, packetStatus, ethernetHeader, ipv4Header, tcpHeader, bytesWritten, tcpSegmentOffset, tcpSegmentLength, info.LocalMac, info.RemoteMac, info.LocalAddress, info.RemoteAddress, info.LocalPort, info.RemotePort, EffectiveTimeToLive(info), info.Identification, localNextSequenceNumber, remoteInitialSequenceNumber, remoteNextSequenceNumber);
    }

    public static TcpIpv4FramePlanResult Fail(TcpIpv4FramePlanStatus status, TcpActiveConnectState state, TcpActiveConnectStatus tcpStatus, PacketParseStatus packetStatus, EthernetHeader ethernetHeader, Ipv4Header ipv4Header, TcpHeader tcpHeader, int tcpSegmentOffset, int tcpSegmentLength, in TcpIpv4FramePlannerInfo info, uint localNextSequenceNumber, uint remoteInitialSequenceNumber, uint remoteNextSequenceNumber)
    {
        return new TcpIpv4FramePlanResult(status, state, tcpStatus, packetStatus, ethernetHeader, ipv4Header, tcpHeader, bytesWritten: 0, tcpSegmentOffset, tcpSegmentLength, info.LocalMac, info.RemoteMac, info.LocalAddress, info.RemoteAddress, info.LocalPort, info.RemotePort, EffectiveTimeToLive(info), info.Identification, localNextSequenceNumber, remoteInitialSequenceNumber, remoteNextSequenceNumber);
    }

    private static byte EffectiveTimeToLive(in TcpIpv4FramePlannerInfo info)
    {
        return info.TimeToLive == 0 ? (byte)64 : info.TimeToLive;
    }
}
