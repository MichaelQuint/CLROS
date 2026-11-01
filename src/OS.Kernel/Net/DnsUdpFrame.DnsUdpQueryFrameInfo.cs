namespace OS.Net;
public readonly struct DnsUdpQueryFrameInfo
{
    public DnsUdpQueryFrameInfo(MacAddress localMac, MacAddress destinationMac, Ipv4Address localAddress, Ipv4Address dnsServerAddress, ushort sourcePort, ushort identification = 0)
    {
        LocalMac = localMac;
        DestinationMac = destinationMac;
        LocalAddress = localAddress;
        DnsServerAddress = dnsServerAddress;
        SourcePort = sourcePort;
        Identification = identification;
    }

    public MacAddress LocalMac { get; }
    public MacAddress DestinationMac { get; }
    public Ipv4Address LocalAddress { get; }
    public Ipv4Address DnsServerAddress { get; }
    public ushort SourcePort { get; }
    public ushort Identification { get; }
}
