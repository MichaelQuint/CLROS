namespace OS.Net;
using OS.Std.Binary;
using OS.Std.Memory;

public readonly struct TcpEstablishedStreamIpv4FramePlannerInfo
{
    public TcpEstablishedStreamIpv4FramePlannerInfo(MacAddress localMac, MacAddress remoteMac, Ipv4Address localAddress, Ipv4Address remoteAddress, int maximumPayloadLength, ushort windowSize = 0x4000, byte timeToLive = 64, ushort identification = 0)
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
