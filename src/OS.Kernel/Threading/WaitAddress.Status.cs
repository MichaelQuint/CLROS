using OS.Pal;

namespace OS.Kernel.Threading;

public unsafe partial struct WaitAddress
{
    public enum Status : byte
    {
        Ok,
        Uninitialized,
        AlreadyInitialized,
        WrongOwner,
        CopiedOwner,
        InterruptsEnabled,
        InvalidStorage,
        InvalidCapacity,
        Overlap,
        InvalidAddress,
        InvalidBinding,
        StaleBinding,
        AlreadyBound,
        Full,
        Busy,
        ValueChanged,
        TimedOut,
        IdentityExhausted,
        ThreadFailure,
        InvalidBitset
    }
}
