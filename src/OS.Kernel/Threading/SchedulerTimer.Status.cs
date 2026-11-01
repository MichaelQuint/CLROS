using OS.Drivers;

namespace OS.Kernel.Threading;
public unsafe partial struct SchedulerTimer
{
    public enum Status : byte
    {
        Ok,
        Uninitialized,
        AlreadyPrepared,
        InvalidOwner,
        WrongOwner,
        InvalidConfiguration,
        InvalidAccess,
        InterruptsEnabled,
        PathsNotReady,
        NotArmed,
        AlreadyArmed,
        DirtyState,
        ReadFailed,
        WriteFailed,
        ReadBackFailed,
        BaselineMismatch,
        PendingInterrupt,
        NotTimerInterrupt,
        InvalidInterruptedFlags,
        InvalidPhase,
        InvalidDeadlineQueue,
        ClockMismatch,
        TickOverflow,
        DeferralLimit
    }
}
