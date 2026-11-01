using OS.Std.Memory;

namespace OS.Drivers;
public enum VirtqueuePollingStatus : byte
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
    DescriptorRejected,
    OrderingFailed,
    BusMasterFailed,
    Timeout,
    InvalidCompletion,
    ResetFailed,
    InterruptFailed
}
