namespace OS.Net;

using OS.Drivers;

public enum IcmpReceiveDispatchStatus
{
    Success = 0,
    ClassifierFailed = 1,
    Ipv4ParseFailed = 2,
    NonIpv4Payload = 3,
    UnsupportedIpProtocol = 4,
    IcmpParseFailed = 5
}
