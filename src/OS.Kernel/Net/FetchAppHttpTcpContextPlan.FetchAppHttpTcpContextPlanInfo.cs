namespace OS.Net;

public readonly struct FetchAppHttpTcpContextPlanInfo
{
    public FetchAppHttpTcpContextPlanInfo(MacAddress localMac, Ipv4Address localAddress, ushort localPort, uint initialSequenceNumber, uint expectedRemoteInitialSequenceNumber = 0, bool requiresRemoteInitialSequenceNumber = false, ushort windowSize = 0x4000, byte timeToLive = 64, ushort identification = 0, int maximumRequestPayloadBytes = HttpTcpClientGetPlannerInfo.DefaultMaximumRequestPayloadBytes)
    {
        LocalMac = localMac;
        LocalAddress = localAddress;
        LocalPort = localPort;
        InitialSequenceNumber = initialSequenceNumber;
        ExpectedRemoteInitialSequenceNumber = expectedRemoteInitialSequenceNumber;
        RequiresRemoteInitialSequenceNumber = requiresRemoteInitialSequenceNumber;
        WindowSize = windowSize;
        TimeToLive = timeToLive;
        Identification = identification;
        MaximumRequestPayloadBytes = maximumRequestPayloadBytes;
    }

    public MacAddress LocalMac
    {
        get;
    }
    public Ipv4Address LocalAddress
    {
        get;
    }
    public ushort LocalPort
    {
        get;
    }
    public uint InitialSequenceNumber
    {
        get;
    }
    public uint ExpectedRemoteInitialSequenceNumber
    {
        get;
    }
    public bool RequiresRemoteInitialSequenceNumber
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
    public int MaximumRequestPayloadBytes
    {
        get;
    }
}
