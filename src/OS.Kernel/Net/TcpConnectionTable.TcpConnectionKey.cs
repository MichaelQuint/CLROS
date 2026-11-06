namespace OS.Net;

public readonly struct TcpConnectionKey
{
    public TcpConnectionKey(Ipv4Address localAddress, ushort localPort, Ipv4Address remoteAddress, ushort remotePort)
    {
        LocalAddress = localAddress;
        LocalPort = localPort;
        RemoteAddress = remoteAddress;
        RemotePort = remotePort;
    }

    public Ipv4Address LocalAddress
    {
        get;
    }
    public ushort LocalPort
    {
        get;
    }
    public Ipv4Address RemoteAddress
    {
        get;
    }
    public ushort RemotePort
    {
        get;
    }
    public bool HasValidEndpoint => LocalAddress != default && RemoteAddress != default && LocalPort != 0 && RemotePort != 0;

    public bool HasSameEndpoint(in TcpConnectionKey other)
    {
        return LocalAddress == other.LocalAddress && LocalPort == other.LocalPort && RemoteAddress == other.RemoteAddress && RemotePort == other.RemotePort;
    }
}
