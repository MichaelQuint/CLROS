namespace OS.Net;

public readonly struct TcpListenerBacklogConnection
{
    public TcpListenerBacklogConnection(Ipv4Address localAddress, ushort localPort, Ipv4Address remoteAddress, ushort remotePort, uint localNextSequenceNumber, uint remoteNextSequenceNumber)
    {
        LocalAddress = localAddress;
        LocalPort = localPort;
        RemoteAddress = remoteAddress;
        RemotePort = remotePort;
        LocalNextSequenceNumber = localNextSequenceNumber;
        RemoteNextSequenceNumber = remoteNextSequenceNumber;
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
    public uint LocalNextSequenceNumber
    {
        get;
    }
    public uint RemoteNextSequenceNumber
    {
        get;
    }
    public bool HasValidEndpoint => LocalAddress != default && RemoteAddress != default && LocalPort != 0 && RemotePort != 0;

    public bool HasSameEndpoint(in TcpListenerBacklogConnection other)
    {
        return LocalAddress == other.LocalAddress && LocalPort == other.LocalPort && RemoteAddress == other.RemoteAddress && RemotePort == other.RemotePort;
    }
}
