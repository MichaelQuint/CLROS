using OS.Drivers;

namespace OS.Storage;

public enum VirtioBlockReadQueuePublishStatus
{
    Success = 0,
    InvalidBlockGeometry = 1,
    InvalidSectorRange = 2,
    CursorPublishFailed = 3
}
