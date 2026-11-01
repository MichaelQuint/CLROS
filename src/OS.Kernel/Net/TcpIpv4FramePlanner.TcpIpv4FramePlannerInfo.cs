namespace OS.Net;
using OS.Std.Binary;
using OS.Std.Memory;

public readonly struct TcpIpv4FramePlannerInfo
{
    public TcpIpv4FramePlannerInfo(MacAddress localMac, MacAddress remoteMac, Ipv4Address localAddress, Ipv4Address remoteAddress, ushort localPort, ushort remotePort, uint initialSequenceNumber, uint expectedRemoteInitialSequenceNumber = 0, bool requiresRemoteInitialSequenceNumber = false, ushort windowSize = 0x4000, byte timeToLive = 64, ushort identification = 0, ushort tcpChecksum = 0)
    {
        LocalMac = localMac;
        RemoteMac = remoteMac;
        LocalAddress = localAddress;
        RemoteAddress = remoteAddress;
        LocalPort = localPort;
        RemotePort = remotePort;
        InitialSequenceNumber = initialSequenceNumber;
        ExpectedRemoteInitialSequenceNumber = expectedRemoteInitialSequenceNumber;
        RequiresRemoteInitialSequenceNumber = requiresRemoteInitialSequenceNumber;
        WindowSize = windowSize;
        TimeToLive = timeToLive;
        Identification = identification;
        TcpChecksum = tcpChecksum;
    }

    public MacAddress LocalMac { get; }
    public MacAddress RemoteMac { get; }
    public Ipv4Address LocalAddress { get; }
    public Ipv4Address RemoteAddress { get; }
    public ushort LocalPort { get; }
    public ushort RemotePort { get; }
    public uint InitialSequenceNumber { get; }
    public uint ExpectedRemoteInitialSequenceNumber { get; }
    public bool RequiresRemoteInitialSequenceNumber { get; }
    public ushort WindowSize { get; }
    public byte TimeToLive { get; }
    public ushort Identification { get; }
    public ushort TcpChecksum { get; }
}
