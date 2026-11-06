using OS.Drivers;

namespace OS.Storage;

public enum VirtioBlockReadOnlyDevicePlanStatus
{
    Success = 0,
    InvalidBlockGeometry = 1,
    InvalidSectorRange = 2,
    LifecyclePublishFailed = 3
}
