namespace OS.Drivers;

using OS.Net;
using OS.Std.Binary;

public enum VirtioNetDeviceConfigRegisterWindowReadStatus
{
    Success = 0,
    ScratchBufferTooSmall = 1,
    RegisterWindowPlanFailed = 2,
    AccessExecutionFailed = 3,
    ConfigParseFailed = 4
}
