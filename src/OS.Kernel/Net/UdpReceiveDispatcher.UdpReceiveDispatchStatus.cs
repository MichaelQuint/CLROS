namespace OS.Net;

using OS.Drivers;

public enum UdpReceiveDispatchStatus
{
    Success = 0,
    ClassifierFailed = 1,
    Ipv4ParseFailed = 2,
    NonIpv4Payload = 3,
    UnsupportedIpProtocol = 4,
    UdpParseFailed = 5,
    BadUdpChecksum = 6
}
