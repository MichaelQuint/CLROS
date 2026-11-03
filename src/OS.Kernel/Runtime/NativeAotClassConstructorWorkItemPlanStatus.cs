namespace OS.Kernel.Runtime;

public enum NativeAotClassConstructorWorkItemPlanStatus
{
    Ready = 0,
    NotInitialized = 1,
    PreflightNotReady = 2,
    InvalidTypeCount = 3,
    TargetMetadataCountMismatch = 4,
    TypeCountMismatch = 5,
    MissingTargetMetadata = 6,
    MissingTargetToken = 7,
    DestinationTooSmall = 8,
    PendingOrReentrantConstructor = 9,
    FailedConstructor = 10,
    InvalidState = 11
}
