using OS.Std.Binary;

namespace OS.Drivers;

public enum VirtioNetQueueMmioSetupCommandKind
{
    WriteQueueSelect = 0,
    ReadQueueSize = 1,
    WriteQueueSize = 2,
    WriteQueueDescriptor = 3,
    WriteQueueDriver = 4,
    WriteQueueDevice = 5,
    ReadQueueNotifyOffset = 6,
    WriteQueueEnable = 7
}
