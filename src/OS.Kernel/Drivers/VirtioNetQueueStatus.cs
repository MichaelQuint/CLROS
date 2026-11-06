using OS.Std.Memory;

namespace OS.Drivers;

public enum VirtioNetQueueStatus : byte
{
    Uninitialized,
    Success,
    InvalidAccess,
    ReadFailed,
    WriteFailed,
    StatusRejected,
    InvalidQueue,
    InvalidNotify,
    DmaRejected,
    Overlap,
    DescriptorRejected,
    OrderingFailed,
    BusMasterFailed,
    Timeout,
    InvalidCompletion,
    ResetFailed
}
