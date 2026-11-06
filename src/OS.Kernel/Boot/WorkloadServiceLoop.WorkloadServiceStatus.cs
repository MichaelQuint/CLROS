namespace OS.Boot;

public enum WorkloadServiceStatus : byte
{
    Uninitialized,
    Running,
    ShutdownRequested,
    Stopped,
    InvalidAdmission,
    AlreadyConsumed,
    ClockFailed,
    ClockRegressed,
    DeadlineOverflow,
    DeadlineExhausted,
    ServiceFailed,
    ShutdownMissing,
    ShutdownFailed,
    StateChanged
}
