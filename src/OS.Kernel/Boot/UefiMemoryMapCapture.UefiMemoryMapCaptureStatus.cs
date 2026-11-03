namespace OS.Boot;

public enum UefiMemoryMapCaptureStatus
{
    Success = 0,
    MissingGetMemoryMap = 1,
    MissingAllocatePool = 2,
    MissingOwnedDestination = 3,
    InitialGetMemoryMapFailed = 4,
    InvalidInitialMetadata = 5,
    AllocationSizeOverflow = 6,
    AllocatePoolFailed = 7,
    FinalGetMemoryMapFailed = 8,
    InvalidCapturedMetadata = 9,
    CopyPlanRejected = 10
}
