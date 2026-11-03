namespace OS.Net;

public readonly struct DhcpLeaseConfiguration
{
    private DhcpLeaseConfiguration(Ipv4Address address, Ipv4Address serverIdentifier, uint leaseTimeSeconds, bool hasSubnetMask, Ipv4Address subnetMask, bool hasRouter, Ipv4Address router, bool hasDnsServer, Ipv4Address dnsServer)
    {
        HasAddress = true;
        Address = address;
        HasServerIdentifier = true;
        ServerIdentifier = serverIdentifier;
        HasLeaseTime = true;
        LeaseTimeSeconds = leaseTimeSeconds;
        HasSubnetMask = hasSubnetMask;
        SubnetMask = subnetMask;
        HasRouter = hasRouter;
        Router = router;
        HasDnsServer = hasDnsServer;
        DnsServer = dnsServer;
    }

    public bool HasAddress
    {
        get;
    }
    public Ipv4Address Address
    {
        get;
    }
    public bool HasSubnetMask
    {
        get;
    }
    public Ipv4Address SubnetMask
    {
        get;
    }
    public bool HasRouter
    {
        get;
    }
    public Ipv4Address Router
    {
        get;
    }
    public bool HasDnsServer
    {
        get;
    }
    public Ipv4Address DnsServer
    {
        get;
    }
    public bool HasServerIdentifier
    {
        get;
    }
    public Ipv4Address ServerIdentifier
    {
        get;
    }
    public bool HasLeaseTime
    {
        get;
    }
    public uint LeaseTimeSeconds
    {
        get;
    }

    public static DhcpLeaseConfigurationStatus TryCreate(in DhcpClientLease lease, out DhcpLeaseConfiguration configuration)
    {
        if (lease.Address == default)
        {
            configuration = default;
            return DhcpLeaseConfigurationStatus.MissingAddress;
        }

        if (lease.ServerIdentifier == default)
        {
            configuration = default;
            return DhcpLeaseConfigurationStatus.MissingServerIdentifier;
        }

        if (lease.LeaseTimeSeconds == 0)
        {
            configuration = default;
            return DhcpLeaseConfigurationStatus.ZeroLeaseTime;
        }

        if (lease.HasSubnetMask && !IsValidSubnetMask(lease.SubnetMask))
        {
            configuration = default;
            return DhcpLeaseConfigurationStatus.InvalidSubnetMask;
        }

        configuration = new DhcpLeaseConfiguration(lease.Address, lease.ServerIdentifier, lease.LeaseTimeSeconds, lease.HasSubnetMask, lease.SubnetMask, lease.HasRouter, lease.Router, lease.HasDnsServer, lease.DnsServer);
        return DhcpLeaseConfigurationStatus.Success;
    }

    private static bool IsValidSubnetMask(Ipv4Address subnetMask)
    {
        uint value = ((uint)subnetMask.Octet0 << 24) | ((uint)subnetMask.Octet1 << 16) | ((uint)subnetMask.Octet2 << 8) | subnetMask.Octet3;
        if (value == 0)
        {
            return false;
        }

        uint hostBits = ~value;
        return (hostBits & (hostBits + 1)) == 0;
    }
}
