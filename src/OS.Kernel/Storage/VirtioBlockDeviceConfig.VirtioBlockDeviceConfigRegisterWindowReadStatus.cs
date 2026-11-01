using OS.Drivers;
using OS.Std.Binary;

namespace OS.Storage;

public enum VirtioBlockDeviceConfigRegisterWindowReadStatus
{
    Success = 0,
    ScratchBufferTooSmall = 1,
    RegisterWindowPlanFailed = 2,
    AccessExecutionFailed = 3,
    ConfigParseFailed = 4
}
