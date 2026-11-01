namespace OS.Net;
public readonly struct FetchAppHttpTcpContextPlanResult
{
    private const uint InitializedFlag = 1;
    private FetchAppHttpTcpContextPlanResult(FetchAppHttpTcpContextPlanStatus status, FetchAppHttpRequestEndpointStatus endpointStatus, DnsResolverTransactionPlanStatus dnsStatus, Ipv4NeighborRoutePlanStatus routeStatus, Ipv4NeighborRoutePlanAction routeAction, int hostBytes, int pathBytes, ushort remotePort, uint hostFingerprint, uint dnsQueryNameFingerprint, int dnsQueryNameLength, Ipv4Address resolvedAddress, MacAddress routeMac, HttpTcpClientGetPlannerInfo plannerInfo, uint flags)
    {
        Status = status;
        EndpointStatus = endpointStatus;
        DnsStatus = dnsStatus;
        RouteStatus = routeStatus;
        RouteAction = routeAction;
        HostBytes = hostBytes;
        PathBytes = pathBytes;
        RemotePort = remotePort;
        HostFingerprint = hostFingerprint;
        DnsQueryNameFingerprint = dnsQueryNameFingerprint;
        DnsQueryNameLength = dnsQueryNameLength;
        ResolvedAddress = resolvedAddress;
        RouteMac = routeMac;
        PlannerInfo = plannerInfo;
        Flags = flags;
    }

    public FetchAppHttpTcpContextPlanStatus Status { get; }
    public FetchAppHttpRequestEndpointStatus EndpointStatus { get; }
    public DnsResolverTransactionPlanStatus DnsStatus { get; }
    public Ipv4NeighborRoutePlanStatus RouteStatus { get; }
    public Ipv4NeighborRoutePlanAction RouteAction { get; }
    public int HostBytes { get; }
    public int PathBytes { get; }
    public ushort RemotePort { get; }
    public uint HostFingerprint { get; }
    public uint DnsQueryNameFingerprint { get; }
    public int DnsQueryNameLength { get; }
    public Ipv4Address ResolvedAddress { get; }
    public MacAddress RouteMac { get; }
    public HttpTcpClientGetPlannerInfo PlannerInfo { get; }
    public uint Flags { get; }
    public bool IsInitialized => (Flags & InitializedFlag) != 0;
    public bool Succeeded => IsInitialized && Status == FetchAppHttpTcpContextPlanStatus.Success && EndpointStatus == FetchAppHttpRequestEndpointStatus.Success && DnsStatus == DnsResolverTransactionPlanStatus.Success && RouteStatus == Ipv4NeighborRoutePlanStatus.Success && RouteAction == Ipv4NeighborRoutePlanAction.UseCachedMac && HostBytes > 0 && PathBytes > 0 && RemotePort != 0 && HostFingerprint != 0 && HostFingerprint == DnsQueryNameFingerprint && HostBytes == DnsQueryNameLength && ResolvedAddress != default && !RouteMac.IsZero && !RouteMac.IsBroadcast;

    public static FetchAppHttpTcpContextPlanResult Success(in FetchAppHttpRequestEndpointResult endpoint, in DnsResolverTransactionPlanResult dns, in Ipv4NeighborRoutePlanResult route, uint hostFingerprint, HttpTcpClientGetPlannerInfo plannerInfo)
    {
        return new FetchAppHttpTcpContextPlanResult(FetchAppHttpTcpContextPlanStatus.Success, endpoint.Status, dns.Status, route.Status, route.Action, endpoint.HostBytesWritten, endpoint.PathBytesWritten, endpoint.Port, hostFingerprint, dns.QueryNameFingerprint, dns.QueryNameLength, dns.AnswerAddress, route.TargetMac, plannerInfo, InitializedFlag);
    }

    public static FetchAppHttpTcpContextPlanResult Fail(FetchAppHttpTcpContextPlanStatus status, in FetchAppHttpRequestEndpointResult endpoint, in DnsResolverTransactionPlanResult dns, in Ipv4NeighborRoutePlanResult route, uint hostFingerprint)
    {
        return new FetchAppHttpTcpContextPlanResult(status, endpoint.Status, dns.Status, route.Status, route.Action, endpoint.HostBytesWritten, endpoint.PathBytesWritten, endpoint.Port, hostFingerprint, dns.QueryNameFingerprint, dns.QueryNameLength, dns.AnswerAddress, route.TargetMac, default, InitializedFlag);
    }
}
