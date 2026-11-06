namespace OS.Net;

using OS.Std.Binary;
using OS.Std.Memory;

public readonly struct TcpPassiveIpv4FramePlannerInfo
{
    public TcpPassiveIpv4FramePlannerInfo(MacAddress localMac, MacAddress remoteMac, Ipv4Address localAddress, Ipv4Address remoteAddress, ushort localPort, ushort remotePort, uint initialSequenceNumber, ushort windowSize = 0x4000, byte timeToLive = 64, ushort identification = 0, ushort tcpChecksum = 0, ushort maximumSegmentSize = 0)
    {
        LocalMac = localMac;
        RemoteMac = remoteMac;
        LocalAddress = localAddress;
        RemoteAddress = remoteAddress;
        LocalPort = localPort;
        RemotePort = remotePort;
        InitialSequenceNumber = initialSequenceNumber;
        WindowSize = windowSize;
        TimeToLive = timeToLive;
        Identification = identification;
        TcpChecksum = tcpChecksum;
        MaximumSegmentSize = maximumSegmentSize;
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
    public uint InitialSequenceNumber
    {
        get;
    }
    public ushort WindowSize
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
    public ushort TcpChecksum
    {
        get;
    }
    /// <summary>Local receive MSS advertised in SYN-ACK; zero keeps the legacy
    /// no-option reply. This does not describe the peer's negotiated send limit.</summary>

    public ushort MaximumSegmentSize
    {
        get;
    }
}
