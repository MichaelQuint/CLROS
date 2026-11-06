using OS.Kernel.Platform;

namespace OS.Boot;

public enum BoundedExitRetryStatus
{
    NotStarted,
    Completed,
    WindowFailed,
    ExitFailed,
    RetryExhausted,
    ReentryRejected,
    AlreadyConsumed,
    InvalidAttemptLimit,
    InvalidOwnedBuffer
}
