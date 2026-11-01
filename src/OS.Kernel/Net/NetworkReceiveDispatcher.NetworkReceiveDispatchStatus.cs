namespace OS.Net;
using OS.Drivers;

public enum NetworkReceiveDispatchStatus
{
    Success = 0,
    ClassifierFailed = 1,
    ArpParseFailed = 2,
    Ipv4ParseFailed = 3,
    UnsupportedPayload = 4
}
