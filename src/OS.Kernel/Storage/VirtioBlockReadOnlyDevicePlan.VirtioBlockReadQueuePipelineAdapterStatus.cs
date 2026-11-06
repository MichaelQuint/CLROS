using OS.Drivers;

namespace OS.Storage;

public enum VirtioBlockReadQueuePipelineAdapterStatus
{
    Success = 0,
    RequestHeaderTooSmall = 1,
    DestinationTooSmall = 2,
    StatusBufferTooSmall = 3,
    QueuePlanFailed = 4,
    QueueMemoryTooSmall = 5,
    DescriptorStateTableTooSmall = 6,
    NotifyWindowTooSmall = 7,
    PipelineFailed = 8
}
