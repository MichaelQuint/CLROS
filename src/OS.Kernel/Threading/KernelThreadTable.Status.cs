using OS.Pal;

namespace OS.Kernel.Threading;

public unsafe partial struct KernelThreadTable
{
    public enum Status : byte
    {
        Ok,
        Pending,
        Uninitialized,
        AlreadyInitialized,
        WrongOwner,
        CopiedOwner,
        InterruptsEnabled,
        InvalidStorage,
        InvalidCapacity,
        InvalidContext,
        InvalidEntry,
        Overlap,
        Full,
        ReadyFull,
        DeadlineFull,
        InvalidIdentity,
        StaleIdentity,
        WrongState,
        NotCurrent,
        Busy,
        Empty,
        ClockRegression,
        DeadlineOverflow,
        IdentityExhausted,
        StaleWait,
        AlreadyCompleted,
        SelfJoin,
        JoinCycle,
        JoinBusy,
        Cancelled,
        Pinned,
        NotSupervisor,
        QueueFailure,
        NotEmpty,
        TimerFailure
    }
}
