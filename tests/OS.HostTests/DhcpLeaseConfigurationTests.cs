using System.Runtime.CompilerServices;
using OS.Net;

internal static class DhcpLeaseConfigurationTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("net.dhcp.lease materializes full config", TestMaterializesFullConfig, ref failures);
        Run("net.dhcp.lease materializes partial config", TestMaterializesPartialConfig, ref failures);
        Run("net.dhcp.lease rejects missing address", TestRejectsMissingAddress, ref failures);
        Run("net.dhcp.lease rejects missing server identifier", TestRejectsMissingServerIdentifier, ref failures);
        Run("net.dhcp.lease rejects zero lease time", TestRejectsZeroLeaseTime, ref failures);
        Run("net.dhcp.lease rejects invalid subnet mask", TestRejectsInvalidSubnetMask, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("DHCP lease configuration host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestMaterializesFullConfig()
    {
        DhcpClientLease lease = new DhcpClientLease(
            Address(),
            ServerIdentifier(),
            7200,
            hasSubnetMask: true,
            SubnetMask(),
            hasRouter: true,
            Router(),
            hasDnsServer: true,
            DnsServer());

        DhcpLeaseConfigurationStatus status = DhcpLeaseConfiguration.TryCreate(
            lease,
            out DhcpLeaseConfiguration configuration);

        return status == DhcpLeaseConfigurationStatus.Success
            && configuration.HasAddress
            && configuration.Address == Address()
            && configuration.HasServerIdentifier
            && configuration.ServerIdentifier == ServerIdentifier()
            && configuration.HasLeaseTime
            && configuration.LeaseTimeSeconds == 7200
            && configuration.HasSubnetMask
            && configuration.SubnetMask == SubnetMask()
            && configuration.HasRouter
            && configuration.Router == Router()
            && configuration.HasDnsServer
            && configuration.DnsServer == DnsServer();
    }

    private static bool TestMaterializesPartialConfig()
    {
        DhcpClientLease lease = new DhcpClientLease(
            Address(),
            ServerIdentifier(),
            3600,
            hasSubnetMask: false,
            default,
            hasRouter: false,
            default,
            hasDnsServer: false,
            default);

        DhcpLeaseConfigurationStatus status = DhcpLeaseConfiguration.TryCreate(
            lease,
            out DhcpLeaseConfiguration configuration);

        return status == DhcpLeaseConfigurationStatus.Success
            && configuration.HasAddress
            && configuration.Address == Address()
            && configuration.HasServerIdentifier
            && configuration.ServerIdentifier == ServerIdentifier()
            && configuration.HasLeaseTime
            && configuration.LeaseTimeSeconds == 3600
            && !configuration.HasSubnetMask
            && configuration.SubnetMask == default
            && !configuration.HasRouter
            && configuration.Router == default
            && !configuration.HasDnsServer
            && configuration.DnsServer == default;
    }

    private static bool TestRejectsMissingAddress()
    {
        DhcpClientLease lease = new DhcpClientLease(
            default,
            ServerIdentifier(),
            3600,
            hasSubnetMask: true,
            SubnetMask(),
            hasRouter: true,
            Router(),
            hasDnsServer: true,
            DnsServer());

        DhcpLeaseConfigurationStatus status = DhcpLeaseConfiguration.TryCreate(
            lease,
            out DhcpLeaseConfiguration configuration);

        return status == DhcpLeaseConfigurationStatus.MissingAddress
            && configuration.Equals(default(DhcpLeaseConfiguration));
    }

    private static bool TestRejectsMissingServerIdentifier()
    {
        DhcpClientLease lease = new DhcpClientLease(
            Address(),
            default,
            3600,
            hasSubnetMask: true,
            SubnetMask(),
            hasRouter: true,
            Router(),
            hasDnsServer: true,
            DnsServer());

        DhcpLeaseConfigurationStatus status = DhcpLeaseConfiguration.TryCreate(
            lease,
            out DhcpLeaseConfiguration configuration);

        return status == DhcpLeaseConfigurationStatus.MissingServerIdentifier
            && configuration.Equals(default(DhcpLeaseConfiguration));
    }

    private static bool TestRejectsZeroLeaseTime()
    {
        DhcpClientLease lease = new DhcpClientLease(
            Address(),
            ServerIdentifier(),
            0,
            hasSubnetMask: true,
            SubnetMask(),
            hasRouter: true,
            Router(),
            hasDnsServer: true,
            DnsServer());

        DhcpLeaseConfigurationStatus status = DhcpLeaseConfiguration.TryCreate(
            lease,
            out DhcpLeaseConfiguration configuration);

        return status == DhcpLeaseConfigurationStatus.ZeroLeaseTime
            && configuration.Equals(default(DhcpLeaseConfiguration));
    }

    private static bool TestRejectsInvalidSubnetMask()
    {
        DhcpClientLease lease = new DhcpClientLease(
            Address(),
            ServerIdentifier(),
            3600,
            hasSubnetMask: true,
            new Ipv4Address(255, 0, 255, 0),
            hasRouter: true,
            Router(),
            hasDnsServer: true,
            DnsServer());

        DhcpLeaseConfigurationStatus status = DhcpLeaseConfiguration.TryCreate(
            lease,
            out DhcpLeaseConfiguration configuration);

        return status == DhcpLeaseConfigurationStatus.InvalidSubnetMask
            && configuration.Equals(default(DhcpLeaseConfiguration));
    }

    private static Ipv4Address Address()
    {
        return new Ipv4Address(192, 0, 2, 42);
    }

    private static Ipv4Address SubnetMask()
    {
        return new Ipv4Address(255, 255, 255, 0);
    }

    private static Ipv4Address Router()
    {
        return new Ipv4Address(192, 0, 2, 1);
    }

    private static Ipv4Address DnsServer()
    {
        return new Ipv4Address(198, 51, 100, 53);
    }

    private static Ipv4Address ServerIdentifier()
    {
        return new Ipv4Address(192, 0, 2, 254);
    }
}
