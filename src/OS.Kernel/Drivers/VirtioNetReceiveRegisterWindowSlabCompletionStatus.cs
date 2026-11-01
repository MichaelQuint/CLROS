namespace OS.Drivers;
using OS.Net;
using OS.Std.Binary;

public enum VirtioNetReceiveRegisterWindowSlabCompletionStatus
{
    Success = 0,
    ReadinessFailed = 1,
    QueueMemoryTooSmall = 2,
    NoCompletion = 3,
    UnexpectedUsedIndex = 4,
    UnexpectedDescriptorHead = 5,
    ReceiveSlabTooSmall = 6,
    CompletionFailed = 7,
    ClassificationFailed = 8
}
