namespace OS.Boot;

public enum M01LivePagingCleanupStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidCompletedCount = 2,
    InvalidAllocationStorage = 3,
    InvalidAllocationRecord = 4,
    FirmwareServicesUnavailable = 5,
    OwnedRootNotRestored = 6
}
