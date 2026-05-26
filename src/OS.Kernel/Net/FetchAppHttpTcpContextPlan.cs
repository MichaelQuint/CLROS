namespace OS.Net;

public enum FetchAppHttpTcpContextPlanStatus
{
    Success = 0,
    EndpointFailed = 1,
    DnsFailed = 2,
    DnsHostMismatch = 3,
    RouteNotCached = 4,
    InvalidLocalIdentity = 5,
    InvalidRemoteIdentity = 6
}

public readonly struct FetchAppHttpTcpContextPlanInfo
{
    public FetchAppHttpTcpContextPlanInfo(
        MacAddress localMac,
        Ipv4Address localAddress,
        ushort localPort,
        uint initialSequenceNumber,
        uint expectedRemoteInitialSequenceNumber = 0,
        bool requiresRemoteInitialSequenceNumber = false,
        ushort windowSize = 0x4000,
        byte timeToLive = 64,
        ushort identification = 0,
        int maximumRequestPayloadBytes = HttpTcpClientGetPlannerInfo.DefaultMaximumRequestPayloadBytes)
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

    public MacAddress LocalMac { get; }

    public Ipv4Address LocalAddress { get; }

    public ushort LocalPort { get; }

    public uint InitialSequenceNumber { get; }

    public uint ExpectedRemoteInitialSequenceNumber { get; }

    public bool RequiresRemoteInitialSequenceNumber { get; }

    public ushort WindowSize { get; }

    public byte TimeToLive { get; }

    public ushort Identification { get; }

    public int MaximumRequestPayloadBytes { get; }
}

public readonly struct FetchAppHttpTcpContextPlanResult
{
    private const uint InitializedFlag = 1;

    private FetchAppHttpTcpContextPlanResult(
        FetchAppHttpTcpContextPlanStatus status,
        FetchAppHttpRequestEndpointStatus endpointStatus,
        DnsResolverTransactionPlanStatus dnsStatus,
        Ipv4NeighborRoutePlanStatus routeStatus,
        Ipv4NeighborRoutePlanAction routeAction,
        int hostBytes,
        int pathBytes,
        ushort remotePort,
        uint hostFingerprint,
        uint dnsQueryNameFingerprint,
        int dnsQueryNameLength,
        Ipv4Address resolvedAddress,
        MacAddress routeMac,
        HttpTcpClientGetPlannerInfo plannerInfo,
        uint flags)
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

    public bool Succeeded =>
        IsInitialized &&
        Status == FetchAppHttpTcpContextPlanStatus.Success &&
        EndpointStatus == FetchAppHttpRequestEndpointStatus.Success &&
        DnsStatus == DnsResolverTransactionPlanStatus.Success &&
        RouteStatus == Ipv4NeighborRoutePlanStatus.Success &&
        RouteAction == Ipv4NeighborRoutePlanAction.UseCachedMac &&
        HostBytes > 0 &&
        PathBytes > 0 &&
        RemotePort != 0 &&
        HostFingerprint != 0 &&
        HostFingerprint == DnsQueryNameFingerprint &&
        HostBytes == DnsQueryNameLength &&
        ResolvedAddress != default &&
        !RouteMac.IsZero &&
        !RouteMac.IsBroadcast;

    public static FetchAppHttpTcpContextPlanResult Success(
        in FetchAppHttpRequestEndpointResult endpoint,
        in DnsResolverTransactionPlanResult dns,
        in Ipv4NeighborRoutePlanResult route,
        uint hostFingerprint,
        HttpTcpClientGetPlannerInfo plannerInfo)
    {
        return new FetchAppHttpTcpContextPlanResult(
            FetchAppHttpTcpContextPlanStatus.Success,
            endpoint.Status,
            dns.Status,
            route.Status,
            route.Action,
            endpoint.HostBytesWritten,
            endpoint.PathBytesWritten,
            endpoint.Port,
            hostFingerprint,
            dns.QueryNameFingerprint,
            dns.QueryNameLength,
            dns.AnswerAddress,
            route.TargetMac,
            plannerInfo,
            InitializedFlag);
    }

    public static FetchAppHttpTcpContextPlanResult Fail(
        FetchAppHttpTcpContextPlanStatus status,
        in FetchAppHttpRequestEndpointResult endpoint,
        in DnsResolverTransactionPlanResult dns,
        in Ipv4NeighborRoutePlanResult route,
        uint hostFingerprint)
    {
        return new FetchAppHttpTcpContextPlanResult(
            status,
            endpoint.Status,
            dns.Status,
            route.Status,
            route.Action,
            endpoint.HostBytesWritten,
            endpoint.PathBytesWritten,
            endpoint.Port,
            hostFingerprint,
            dns.QueryNameFingerprint,
            dns.QueryNameLength,
            dns.AnswerAddress,
            route.TargetMac,
            default,
            InitializedFlag);
    }
}

public static class FetchAppHttpTcpContextPlan
{
    private const uint FingerprintOffset = 2166136261;
    private const uint FingerprintPrime = 16777619;

    public static FetchAppHttpTcpContextPlanStatus Compose(
        in FetchAppHttpRequestEndpointResult endpoint,
        ReadOnlySpan<byte> hostBytes,
        in DnsResolverTransactionPlanResult dns,
        in Ipv4NeighborRoutePlanResult route,
        in FetchAppHttpTcpContextPlanInfo info,
        out FetchAppHttpTcpContextPlanResult result)
    {
        if (!endpoint.Succeeded)
        {
            result = FetchAppHttpTcpContextPlanResult.Fail(
                FetchAppHttpTcpContextPlanStatus.EndpointFailed,
                in endpoint,
                in dns,
                in route,
                hostFingerprint: 0);
            return result.Status;
        }

        if (!dns.Succeeded)
        {
            result = FetchAppHttpTcpContextPlanResult.Fail(
                FetchAppHttpTcpContextPlanStatus.DnsFailed,
                in endpoint,
                in dns,
                in route,
                hostFingerprint: 0);
            return result.Status;
        }

        if (hostBytes.Length < endpoint.HostBytesWritten)
        {
            result = FetchAppHttpTcpContextPlanResult.Fail(
                FetchAppHttpTcpContextPlanStatus.DnsHostMismatch,
                in endpoint,
                in dns,
                in route,
                hostFingerprint: 0);
            return result.Status;
        }

        uint hostFingerprint = ComputeHostFingerprint(hostBytes.Slice(0, endpoint.HostBytesWritten));
        if (dns.QueryNameLength != endpoint.HostBytesWritten ||
            dns.QueryNameFingerprint != hostFingerprint)
        {
            result = FetchAppHttpTcpContextPlanResult.Fail(
                FetchAppHttpTcpContextPlanStatus.DnsHostMismatch,
                in endpoint,
                in dns,
                in route,
                hostFingerprint);
            return result.Status;
        }

        if (info.LocalMac.IsZero ||
            info.LocalMac.IsBroadcast ||
            info.LocalAddress == default ||
            info.LocalPort == 0)
        {
            result = FetchAppHttpTcpContextPlanResult.Fail(
                FetchAppHttpTcpContextPlanStatus.InvalidLocalIdentity,
                in endpoint,
                in dns,
                in route,
                hostFingerprint);
            return result.Status;
        }

        if (!route.Succeeded || route.Action != Ipv4NeighborRoutePlanAction.UseCachedMac)
        {
            result = FetchAppHttpTcpContextPlanResult.Fail(
                FetchAppHttpTcpContextPlanStatus.RouteNotCached,
                in endpoint,
                in dns,
                in route,
                hostFingerprint);
            return result.Status;
        }

        if (dns.AnswerAddress == default || route.TargetMac.IsZero || route.TargetMac.IsBroadcast)
        {
            result = FetchAppHttpTcpContextPlanResult.Fail(
                FetchAppHttpTcpContextPlanStatus.InvalidRemoteIdentity,
                in endpoint,
                in dns,
                in route,
                hostFingerprint);
            return result.Status;
        }

        TcpIpv4FramePlannerInfo tcpInfo = new TcpIpv4FramePlannerInfo(
            info.LocalMac,
            route.TargetMac,
            info.LocalAddress,
            dns.AnswerAddress,
            info.LocalPort,
            endpoint.Port,
            info.InitialSequenceNumber,
            info.ExpectedRemoteInitialSequenceNumber,
            info.RequiresRemoteInitialSequenceNumber,
            info.WindowSize,
            info.TimeToLive,
            info.Identification);

        HttpTcpClientGetPlannerInfo httpInfo = new HttpTcpClientGetPlannerInfo(
            tcpInfo,
            info.MaximumRequestPayloadBytes);

        result = FetchAppHttpTcpContextPlanResult.Success(
            in endpoint,
            in dns,
            in route,
            hostFingerprint,
            httpInfo);
        return result.Status;
    }

    public static uint ComputeHostFingerprint(ReadOnlySpan<byte> hostBytes)
    {
        uint fingerprint = FingerprintOffset;
        for (int index = 0; index < hostBytes.Length; index++)
        {
            fingerprint ^= hostBytes[index];
            fingerprint *= FingerprintPrime;
        }

        return fingerprint;
    }
}
