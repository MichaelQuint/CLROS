namespace OS.Net;
public readonly struct DhcpClientLease
{
    public DhcpClientLease(Ipv4Address address, Ipv4Address serverIdentifier, uint leaseTimeSeconds, bool hasSubnetMask, Ipv4Address subnetMask, bool hasRouter, Ipv4Address router, bool hasDnsServer, Ipv4Address dnsServer)
    {
        Address = address;
        ServerIdentifier = serverIdentifier;
        LeaseTimeSeconds = leaseTimeSeconds;
        HasSubnetMask = hasSubnetMask;
        SubnetMask = subnetMask;
        HasRouter = hasRouter;
        Router = router;
        HasDnsServer = hasDnsServer;
        DnsServer = dnsServer;
    }

    public Ipv4Address Address { get; }
    public Ipv4Address ServerIdentifier { get; }
    public uint LeaseTimeSeconds { get; }
    public bool HasSubnetMask { get; }
    public Ipv4Address SubnetMask { get; }
    public bool HasRouter { get; }
    public Ipv4Address Router { get; }
    public bool HasDnsServer { get; }
    public Ipv4Address DnsServer { get; }
}
