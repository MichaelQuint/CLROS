namespace OS.Drivers;
using OS.Net;

public enum VirtioNetReceiveFrameClassifierStatus
{
    Success = 0,
    BufferTooSmall = 1,
    MissingEthernetHeader = 2,
    UnsupportedChecksumOffload = 3,
    UnsupportedOffloadFlags = 4,
    UnsupportedGenericSegmentationOffload = 5,
    UnsupportedPayload = 6,
    UnsupportedReceiveBufferCount = 7
}
