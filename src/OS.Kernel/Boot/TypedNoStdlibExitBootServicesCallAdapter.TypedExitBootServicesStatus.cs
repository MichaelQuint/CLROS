using OS.Kernel.Platform;

namespace OS.Boot;

public enum TypedExitBootServicesStatus
{
    NotStarted = 0,
    Prepared,
    Returned,
    MissingFunctionPointer,
    MissingImageHandle,
    FirmwareServicesUnavailable,
    NotPrepared,
    AlreadyPrepared,
    ReentryRejected,
    AlreadyAttempted,
    InvalidState
}
