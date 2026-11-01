namespace OS.Drivers;
using OS.Net;
using OS.Std.Binary;

public enum VirtioNetDeviceConfigStatus
{
    Success = 0,
    BufferTooSmall = 1,
    ZeroMacAddress = 2,
    BroadcastMacAddress = 3
}
