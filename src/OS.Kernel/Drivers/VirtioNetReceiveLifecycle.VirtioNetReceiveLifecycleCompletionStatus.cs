namespace OS.Drivers;
using OS.Net;
using OS.Std.Binary;

public enum VirtioNetReceiveLifecycleCompletionStatus
{
    Success = 0,
    PublishFailed = 1,
    QueueMemoryTooSmall = 2,
    NoCompletion = 3,
    UnexpectedUsedIndex = 4,
    UnexpectedDescriptorHead = 5,
    UnexpectedByteCount = 6,
    ReceiveBufferTooSmall = 7,
    FrameClassificationFailed = 8
}
