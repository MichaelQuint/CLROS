namespace OS.Net;
using OS.Std.Binary;

public readonly struct DnsResolverTransactionPlannerInfo
{
    public DnsResolverTransactionPlannerInfo(MacAddress localMac, MacAddress nextHopMac, Ipv4Address localAddress, Ipv4Address dnsServerAddress, ushort clientPort, ushort transactionId, ushort identification = 0)
    {
        LocalMac = localMac;
        NextHopMac = nextHopMac;
        LocalAddress = localAddress;
        DnsServerAddress = dnsServerAddress;
        ClientPort = clientPort;
        TransactionId = transactionId;
        Identification = identification;
    }

    public MacAddress LocalMac { get; }
    public MacAddress NextHopMac { get; }
    public Ipv4Address LocalAddress { get; }
    public Ipv4Address DnsServerAddress { get; }
    public ushort ClientPort { get; }
    public ushort TransactionId { get; }
    public ushort Identification { get; }

    public DnsUdpQueryFrameInfo ToQueryFrameInfo()
    {
        return new DnsUdpQueryFrameInfo(LocalMac, NextHopMac, LocalAddress, DnsServerAddress, ClientPort, Identification);
    }
}
