using OS.Pal;

namespace OS.Kernel.Threading;

public unsafe partial struct ReadyQueue
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
        NotRunnable,
        ContextNotReady,
        Duplicate,
        Empty,
        Full,
        NotFound,
        NotEmpty
    }
}
