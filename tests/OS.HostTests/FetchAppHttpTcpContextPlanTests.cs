using System.Runtime.CompilerServices;
using System.Text;
using OS.Net;

internal static class FetchAppHttpTcpContextPlanTests
{
    private const ushort LocalPort = 49192;
    private const ushort RemotePort = 8080;
    private const uint ClientInitialSequence = 0x4400_1200;
    private const uint ServerInitialSequence = 0x5500_3400;
    private const ushort WindowSize = 0x2222;
    private const byte TimeToLive = 41;
    private const ushort Identification = 0x3344;
    private const int MaximumRequestPayloadBytes = 1200;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("fetch.http tcp context composes planner info", TestComposesPlannerInfo, ref failures);
        Run("fetch.http tcp context rejects endpoint failure", TestRejectsEndpointFailure, ref failures);
        Run("fetch.http tcp context rejects dns failure", TestRejectsDnsFailure, ref failures);
        Run("fetch.http tcp context rejects dns host mismatch", TestRejectsDnsHostMismatch, ref failures);
        Run("fetch.http tcp context rejects uncached route", TestRejectsUncachedRoute, ref failures);
        Run("fetch.http tcp context rejects invalid local identity", TestRejectsInvalidLocalIdentity, ref failures);
        Run("fetch.http tcp context default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("FetchApp HTTP TCP context plan host tests failed: " + failures);
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

    private static bool TestComposesPlannerInfo()
    {
        byte[] host = HostBytes("example.com");
        FetchAppHttpRequestEndpointResult endpoint = ReadyEndpoint(host.Length);
        DnsResolverTransactionPlanResult dns = SuccessfulDns(host, RemoteAddress());
        Ipv4NeighborRoutePlanResult route = CachedRoute();
        FetchAppHttpTcpContextPlanInfo info = ReadyInfo();

        FetchAppHttpTcpContextPlanStatus status = FetchAppHttpTcpContextPlan.Compose(
            endpoint,
            host,
            dns,
            route,
            info,
            out FetchAppHttpTcpContextPlanResult result);
        TcpIpv4FramePlannerInfo tcpInfo = result.PlannerInfo.TcpInfo;

        return status == FetchAppHttpTcpContextPlanStatus.Success
            && result.Succeeded
            && result.Status == FetchAppHttpTcpContextPlanStatus.Success
            && result.EndpointStatus == FetchAppHttpRequestEndpointStatus.Success
            && result.DnsStatus == DnsResolverTransactionPlanStatus.Success
            && result.RouteStatus == Ipv4NeighborRoutePlanStatus.Success
            && result.RouteAction == Ipv4NeighborRoutePlanAction.UseCachedMac
            && result.HostBytes == host.Length
            && result.PathBytes == "/healthz".Length
            && result.RemotePort == RemotePort
            && result.HostFingerprint == FetchAppHttpTcpContextPlan.ComputeHostFingerprint(host)
            && result.DnsQueryNameFingerprint == result.HostFingerprint
            && result.DnsQueryNameLength == host.Length
            && result.ResolvedAddress == RemoteAddress()
            && result.RouteMac == RemoteMac()
            && tcpInfo.LocalMac == LocalMac()
            && tcpInfo.RemoteMac == RemoteMac()
            && tcpInfo.LocalAddress == LocalAddress()
            && tcpInfo.RemoteAddress == RemoteAddress()
            && tcpInfo.LocalPort == LocalPort
            && tcpInfo.RemotePort == RemotePort
            && tcpInfo.InitialSequenceNumber == ClientInitialSequence
            && tcpInfo.ExpectedRemoteInitialSequenceNumber == ServerInitialSequence
            && tcpInfo.RequiresRemoteInitialSequenceNumber
            && tcpInfo.WindowSize == WindowSize
            && tcpInfo.TimeToLive == TimeToLive
            && tcpInfo.Identification == Identification
            && result.PlannerInfo.MaximumRequestPayloadBytes == MaximumRequestPayloadBytes
            && !RuntimeHelpers.IsReferenceOrContainsReferences<FetchAppHttpTcpContextPlanResult>();
    }

    private static bool TestRejectsEndpointFailure()
    {
        byte[] host = HostBytes("example.com");
        FetchAppHttpRequestEndpointResult endpoint = FetchAppHttpRequestEndpointResult.Create(
            FetchAppHttpRequestEndpointStatus.InvalidPort,
            port: 0,
            hostBytesWritten: 0,
            pathBytesWritten: 0,
            requiredHostBytes: host.Length,
            requiredPathBytes: "/healthz".Length,
            availableHostBytes: FetchAppHttpRequestEndpoint.MaximumHostLength,
            availablePathBytes: FetchAppHttpRequestEndpoint.MaximumPathLength,
            hasConfiguredUrl: false,
            hasConfiguredHost: true,
            hasConfiguredPort: true,
            hasConfiguredPath: true);

        FetchAppHttpTcpContextPlanStatus status = FetchAppHttpTcpContextPlan.Compose(
            endpoint,
            host,
            SuccessfulDns(host, RemoteAddress()),
            CachedRoute(),
            ReadyInfo(),
            out FetchAppHttpTcpContextPlanResult result);

        return status == FetchAppHttpTcpContextPlanStatus.EndpointFailed
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == FetchAppHttpTcpContextPlanStatus.EndpointFailed
            && result.EndpointStatus == FetchAppHttpRequestEndpointStatus.InvalidPort
            && result.HostFingerprint == 0
            && result.PlannerInfo.TcpInfo.LocalPort == 0;
    }

    private static bool TestRejectsDnsFailure()
    {
        byte[] host = HostBytes("example.com");
        DnsResolverTransactionPlanResult failedDns = DnsResolverTransactionPlanResult.Fail(
            DnsResolverTransactionPlanStatus.Nxdomain,
            default,
            default,
            default,
            DnsResolverStatus.Success,
            DnsMessageStatus.ResponseCodeFailure,
            PacketParseStatus.Success,
            UdpReceiveDispatchStatus.Success,
            transactionId: 0x1010,
            queryNameLength: host.Length,
            queryNameFingerprint: FetchAppHttpTcpContextPlan.ComputeHostFingerprint(host),
            responseSize: 64,
            sourceServer: new Ipv4Address(198, 51, 100, 53));

        FetchAppHttpTcpContextPlanStatus status = FetchAppHttpTcpContextPlan.Compose(
            ReadyEndpoint(host.Length),
            host,
            failedDns,
            CachedRoute(),
            ReadyInfo(),
            out FetchAppHttpTcpContextPlanResult result);

        return status == FetchAppHttpTcpContextPlanStatus.DnsFailed
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == FetchAppHttpTcpContextPlanStatus.DnsFailed
            && result.DnsStatus == DnsResolverTransactionPlanStatus.Nxdomain
            && result.DnsQueryNameFingerprint == failedDns.QueryNameFingerprint
            && result.ResolvedAddress == default
            && result.HostFingerprint == 0;
    }

    private static bool TestRejectsDnsHostMismatch()
    {
        byte[] host = HostBytes("example.com");
        DnsResolverTransactionPlanResult wrongHostDns = SuccessfulDns(
            HostBytes("example.net"),
            RemoteAddress());

        FetchAppHttpTcpContextPlanStatus status = FetchAppHttpTcpContextPlan.Compose(
            ReadyEndpoint(host.Length),
            host,
            wrongHostDns,
            CachedRoute(),
            ReadyInfo(),
            out FetchAppHttpTcpContextPlanResult result);

        uint hostFingerprint = FetchAppHttpTcpContextPlan.ComputeHostFingerprint(host);
        return status == FetchAppHttpTcpContextPlanStatus.DnsHostMismatch
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == FetchAppHttpTcpContextPlanStatus.DnsHostMismatch
            && result.HostFingerprint == hostFingerprint
            && result.DnsQueryNameFingerprint == wrongHostDns.QueryNameFingerprint
            && result.DnsQueryNameLength == "example.net".Length
            && result.ResolvedAddress == RemoteAddress();
    }

    private static bool TestRejectsUncachedRoute()
    {
        byte[] host = HostBytes("example.com");
        Ipv4NeighborRoutePlanResult uncachedRoute = Ipv4NeighborRoutePlanResult.RequestWritten(
            bytesWritten: ArpNeighborResolutionTransaction.RequestFrameLength,
            ArpNeighborCacheStatus.NotFound,
            ArpNeighborResolutionStatus.Success,
            PacketParseStatus.Success,
            cacheCount: 0,
            cacheCapacity: 1);

        FetchAppHttpTcpContextPlanStatus status = FetchAppHttpTcpContextPlan.Compose(
            ReadyEndpoint(host.Length),
            host,
            SuccessfulDns(host, RemoteAddress()),
            uncachedRoute,
            ReadyInfo(),
            out FetchAppHttpTcpContextPlanResult result);

        return status == FetchAppHttpTcpContextPlanStatus.RouteNotCached
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == FetchAppHttpTcpContextPlanStatus.RouteNotCached
            && result.RouteStatus == Ipv4NeighborRoutePlanStatus.Success
            && result.RouteAction == Ipv4NeighborRoutePlanAction.SendArpRequest
            && result.RouteMac == default
            && result.PlannerInfo.TcpInfo.RemotePort == 0;
    }

    private static bool TestRejectsInvalidLocalIdentity()
    {
        byte[] host = HostBytes("example.com");
        FetchAppHttpRequestEndpointResult endpoint = ReadyEndpoint(host.Length);
        DnsResolverTransactionPlanResult dns = SuccessfulDns(host, RemoteAddress());
        Ipv4NeighborRoutePlanResult route = CachedRoute();

        FetchAppHttpTcpContextPlanInfo zeroMac = new FetchAppHttpTcpContextPlanInfo(
            default,
            LocalAddress(),
            LocalPort,
            ClientInitialSequence);
        FetchAppHttpTcpContextPlanInfo zeroAddress = new FetchAppHttpTcpContextPlanInfo(
            LocalMac(),
            default,
            LocalPort,
            ClientInitialSequence);
        FetchAppHttpTcpContextPlanInfo zeroPort = new FetchAppHttpTcpContextPlanInfo(
            LocalMac(),
            LocalAddress(),
            0,
            ClientInitialSequence);

        FetchAppHttpTcpContextPlanStatus zeroMacStatus = FetchAppHttpTcpContextPlan.Compose(
            endpoint,
            host,
            dns,
            route,
            zeroMac,
            out FetchAppHttpTcpContextPlanResult zeroMacResult);
        FetchAppHttpTcpContextPlanStatus zeroAddressStatus = FetchAppHttpTcpContextPlan.Compose(
            endpoint,
            host,
            dns,
            route,
            zeroAddress,
            out FetchAppHttpTcpContextPlanResult zeroAddressResult);
        FetchAppHttpTcpContextPlanStatus zeroPortStatus = FetchAppHttpTcpContextPlan.Compose(
            endpoint,
            host,
            dns,
            route,
            zeroPort,
            out FetchAppHttpTcpContextPlanResult zeroPortResult);

        return zeroMacStatus == FetchAppHttpTcpContextPlanStatus.InvalidLocalIdentity
            && zeroAddressStatus == FetchAppHttpTcpContextPlanStatus.InvalidLocalIdentity
            && zeroPortStatus == FetchAppHttpTcpContextPlanStatus.InvalidLocalIdentity
            && zeroMacResult.IsInitialized
            && zeroAddressResult.IsInitialized
            && zeroPortResult.IsInitialized
            && !zeroMacResult.Succeeded
            && !zeroAddressResult.Succeeded
            && !zeroPortResult.Succeeded
            && zeroMacResult.HostFingerprint == FetchAppHttpTcpContextPlan.ComputeHostFingerprint(host)
            && zeroAddressResult.RouteMac == route.TargetMac
            && zeroPortResult.PlannerInfo.TcpInfo.LocalPort == 0;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        FetchAppHttpTcpContextPlanResult defaultResult = default;
        FetchAppHttpTcpContextPlanInfo defaultInfo = default;

        return !defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == FetchAppHttpTcpContextPlanStatus.Success
            && defaultResult.EndpointStatus == FetchAppHttpRequestEndpointStatus.Success
            && defaultResult.DnsStatus == DnsResolverTransactionPlanStatus.Success
            && defaultResult.RouteStatus == Ipv4NeighborRoutePlanStatus.Success
            && defaultResult.RouteAction == Ipv4NeighborRoutePlanAction.None
            && defaultResult.HostBytes == 0
            && defaultResult.PathBytes == 0
            && defaultResult.RemotePort == 0
            && defaultResult.HostFingerprint == 0
            && defaultResult.DnsQueryNameFingerprint == 0
            && defaultResult.DnsQueryNameLength == 0
            && defaultResult.ResolvedAddress == default
            && defaultResult.RouteMac == default
            && defaultResult.PlannerInfo.TcpInfo.LocalPort == 0
            && defaultInfo.LocalMac == default
            && defaultInfo.LocalAddress == default
            && defaultInfo.LocalPort == 0
            && defaultInfo.InitialSequenceNumber == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<FetchAppHttpTcpContextPlanResult>();
    }

    private static FetchAppHttpRequestEndpointResult ReadyEndpoint(int hostLength)
    {
        return FetchAppHttpRequestEndpointResult.Create(
            FetchAppHttpRequestEndpointStatus.Success,
            RemotePort,
            hostLength,
            "/healthz".Length,
            hostLength,
            "/healthz".Length,
            FetchAppHttpRequestEndpoint.MaximumHostLength,
            FetchAppHttpRequestEndpoint.MaximumPathLength,
            hasConfiguredUrl: false,
            hasConfiguredHost: true,
            hasConfiguredPort: true,
            hasConfiguredPath: true);
    }

    private static DnsResolverTransactionPlanResult SuccessfulDns(
        ReadOnlySpan<byte> host,
        Ipv4Address answerAddress)
    {
        DnsResolverTransitionResult resolver = DnsResolverTransitionResult.Success(
            DnsResolverState.Resolved,
            bytesWritten: 4,
            answerAddress);
        DnsUdpReceiveFrameResult receive = DnsUdpReceiveFrameResult.Success(default, resolver);
        return DnsResolverTransactionPlanResult.ReceiveSuccess(
            default,
            receive,
            transactionId: 0x1010,
            queryNameLength: host.Length,
            queryNameFingerprint: FetchAppHttpTcpContextPlan.ComputeHostFingerprint(host),
            answerTtlSeconds: 60);
    }

    private static Ipv4NeighborRoutePlanResult CachedRoute()
    {
        return Ipv4NeighborRoutePlanResult.Cached(
            RemoteMac(),
            ArpNeighborCacheStatus.Success,
            cacheCount: 1,
            cacheCapacity: 1);
    }

    private static FetchAppHttpTcpContextPlanInfo ReadyInfo()
    {
        return new FetchAppHttpTcpContextPlanInfo(
            LocalMac(),
            LocalAddress(),
            LocalPort,
            ClientInitialSequence,
            ServerInitialSequence,
            true,
            WindowSize,
            TimeToLive,
            Identification,
            MaximumRequestPayloadBytes);
    }

    private static byte[] HostBytes(string host)
    {
        return Encoding.ASCII.GetBytes(host);
    }

    private static MacAddress LocalMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x71, 0x01);
    }

    private static MacAddress RemoteMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x71, 0x80);
    }

    private static Ipv4Address LocalAddress()
    {
        return new Ipv4Address(192, 0, 2, 71);
    }

    private static Ipv4Address RemoteAddress()
    {
        return new Ipv4Address(203, 0, 113, 71);
    }
}
