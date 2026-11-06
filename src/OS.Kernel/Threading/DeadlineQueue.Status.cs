using OS.Pal;

namespace OS.Kernel.Threading;

public unsafe partial struct DeadlineQueue
{
    public enum Status : byte
    {
        Ok,
        Uninitialized,
        AlreadyInitialized,
        InvalidOwner,
        WrongOwner,
        InvalidBuffer,
        InvalidCapacity,
        BufferTooSmall,
        InvalidIdentity,
        StaleIdentity,
        Duplicate,
        NotFound,
        Empty,
        Full,
        NotEmpty,
        ClockRegression,
        DeadlineOverflow,
        InvalidLimit
    }
}
