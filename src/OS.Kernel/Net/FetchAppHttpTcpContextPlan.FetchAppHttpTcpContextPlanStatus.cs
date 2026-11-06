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
