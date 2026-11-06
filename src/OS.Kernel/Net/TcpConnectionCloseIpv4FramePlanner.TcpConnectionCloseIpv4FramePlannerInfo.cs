namespace OS.Net;

using OS.Std.Binary;
using OS.Std.Memory;

public readonly struct TcpConnectionCloseIpv4FramePlannerInfo
{
    public TcpConnectionCloseIpv4FramePlannerInfo(MacAddress localMac, MacAddress remoteMac, Ipv4Address localAddress, Ipv4Address remoteAddress, ushort localPort, ushort remotePort, uint localNextSequenceNumber, uint remoteNextSequenceNumber, ushort windowSize = 0x4000, byte timeToLive = 64, ushort identification = 0)
    {
        LocalMac = localMac;
        RemoteMac = remoteMac;
        LocalAddress = localAddress;
        RemoteAddress = remoteAddress;
        LocalPort = localPort;
        RemotePort = remotePort;
        LocalNextSequenceNumber = localNextSequenceNumber;
        RemoteNextSequenceNumber = remoteNextSequenceNumber;
        WindowSize = windowSize;
        TimeToLive = timeToLive;
        Identification = identification;
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
    public uint LocalNextSequenceNumber
    {
        get;
    }
    public uint RemoteNextSequenceNumber
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
}
