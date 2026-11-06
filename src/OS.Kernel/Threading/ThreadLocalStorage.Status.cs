using OS.Pal;

namespace OS.Kernel.Threading;

public unsafe partial struct ThreadLocalStorage
{
    public enum Status : byte
    {
        Ok,
        Uninitialized,
        AlreadyInitialized,
        InvalidOwner,
        CopiedOwner,
        InterruptsEnabled,
        InvalidStorage,
        InvalidCapacity,
        InvalidRegion,
        InvalidBase,
        Overlap,
        InvalidBinding,
        StaleBinding,
        AlreadyBound,
        Full,
        WrongState,
        InvalidContext,
        ThreadFailure,
        Busy
    }
}
