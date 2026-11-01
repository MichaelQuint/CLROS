using OS.Kernel.Platform;

namespace OS.Boot;
public enum FinalMapExitWindowStatus
{
    NotStarted,
    Returned,
    ReentryRejected,
    AlreadyConsumed,
    MissingGetMemoryMap,
    InvalidOwnedBuffer,
    ExitAdmissionRejected,
    CaptureFailed,
    InvalidMapMetadata
}
