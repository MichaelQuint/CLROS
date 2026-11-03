namespace OS.Net;

public enum UdpTransmitPlanStatus
{
    Success = 0,
    InvalidLocalIdentity = 1,
    InvalidDestinationIdentity = 2,
    PayloadTooLarge = 3,
    FrameBufferTooSmall = 4,
    PacketWriteFailed = 5
}
