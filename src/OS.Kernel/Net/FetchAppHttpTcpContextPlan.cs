namespace OS.Net;

public static class FetchAppHttpTcpContextPlan
{
    private const uint FingerprintOffset = 2166136261;
    private const uint FingerprintPrime = 16777619;
    public static FetchAppHttpTcpContextPlanStatus Compose(in FetchAppHttpRequestEndpointResult endpoint, ReadOnlySpan<byte> hostBytes, in DnsResolverTransactionPlanResult dns, in Ipv4NeighborRoutePlanResult route, in FetchAppHttpTcpContextPlanInfo info, out FetchAppHttpTcpContextPlanResult result)
    {
        if (!endpoint.Succeeded)
        {
            result = FetchAppHttpTcpContextPlanResult.Fail(FetchAppHttpTcpContextPlanStatus.EndpointFailed, in endpoint, in dns, in route, hostFingerprint: 0);
            return result.Status;
        }

        if (!dns.Succeeded)
        {
            result = FetchAppHttpTcpContextPlanResult.Fail(FetchAppHttpTcpContextPlanStatus.DnsFailed, in endpoint, in dns, in route, hostFingerprint: 0);
            return result.Status;
        }

        if (hostBytes.Length < endpoint.HostBytesWritten)
        {
            result = FetchAppHttpTcpContextPlanResult.Fail(FetchAppHttpTcpContextPlanStatus.DnsHostMismatch, in endpoint, in dns, in route, hostFingerprint: 0);
            return result.Status;
        }

        uint hostFingerprint = ComputeHostFingerprint(hostBytes.Slice(0, endpoint.HostBytesWritten));
        if (dns.QueryNameLength != endpoint.HostBytesWritten || dns.QueryNameFingerprint != hostFingerprint)
        {
            result = FetchAppHttpTcpContextPlanResult.Fail(FetchAppHttpTcpContextPlanStatus.DnsHostMismatch, in endpoint, in dns, in route, hostFingerprint);
            return result.Status;
        }

        if (info.LocalMac.IsZero || info.LocalMac.IsBroadcast || info.LocalAddress == default || info.LocalPort == 0)
        {
            result = FetchAppHttpTcpContextPlanResult.Fail(FetchAppHttpTcpContextPlanStatus.InvalidLocalIdentity, in endpoint, in dns, in route, hostFingerprint);
            return result.Status;
        }

        if (!route.Succeeded || route.Action != Ipv4NeighborRoutePlanAction.UseCachedMac)
        {
            result = FetchAppHttpTcpContextPlanResult.Fail(FetchAppHttpTcpContextPlanStatus.RouteNotCached, in endpoint, in dns, in route, hostFingerprint);
            return result.Status;
        }

        if (dns.AnswerAddress == default || route.TargetMac.IsZero || route.TargetMac.IsBroadcast)
        {
            result = FetchAppHttpTcpContextPlanResult.Fail(FetchAppHttpTcpContextPlanStatus.InvalidRemoteIdentity, in endpoint, in dns, in route, hostFingerprint);
            return result.Status;
        }

        TcpIpv4FramePlannerInfo tcpInfo = new TcpIpv4FramePlannerInfo(info.LocalMac, route.TargetMac, info.LocalAddress, dns.AnswerAddress, info.LocalPort, endpoint.Port, info.InitialSequenceNumber, info.ExpectedRemoteInitialSequenceNumber, info.RequiresRemoteInitialSequenceNumber, info.WindowSize, info.TimeToLive, info.Identification);
        HttpTcpClientGetPlannerInfo httpInfo = new HttpTcpClientGetPlannerInfo(tcpInfo, info.MaximumRequestPayloadBytes);
        result = FetchAppHttpTcpContextPlanResult.Success(in endpoint, in dns, in route, hostFingerprint, httpInfo);
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
