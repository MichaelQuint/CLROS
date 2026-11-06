namespace OS.Net;

public enum DnsUdpFrameStatus
{
    Success = 0,
    BufferTooSmall = 1,
    ResolverWriteFailed = 2,
    PacketWriteFailed = 3,
    UdpDispatchFailed = 4,
    WrongUdpPorts = 5,
    ResolverParseFailed = 6
}
