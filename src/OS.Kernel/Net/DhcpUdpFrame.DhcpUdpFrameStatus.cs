namespace OS.Net;

public enum DhcpUdpFrameStatus
{
    Success = 0,
    PacketFailed = 1,
    DhcpMessageFailed = 2,
    UdpReceiveFailed = 3,
    WrongUdpPorts = 4
}
