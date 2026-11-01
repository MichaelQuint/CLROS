namespace OS.Net;
public enum DhcpClientState
{
    Initial = 0,
    DiscoverSent = 1,
    OfferReceived = 2,
    RequestSent = 3,
    Bound = 4,
    Failed = 5
}
