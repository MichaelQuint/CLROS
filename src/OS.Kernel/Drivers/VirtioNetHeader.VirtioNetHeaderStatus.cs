namespace OS.Drivers;
using OS.Net;
using OS.Std.Binary;

public enum VirtioNetHeaderStatus
{
    Success = 0,
    BufferTooSmall = 1,
    MissingEthernetHeader = 2,
    UnsupportedChecksumOffload = 3,
    UnsupportedOffloadFlags = 4,
    UnsupportedGenericSegmentationOffload = 5,
    UnsupportedReceiveBufferCount = 6
}
