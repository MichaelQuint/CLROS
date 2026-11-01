using OS.Std.Text;

namespace OS.Kernel.Runtime;
public enum NativeAotClassConstructorRunnerPreflightStatus
{
    Ready = 0,
    NotInitialized = 1,
    InvalidTypeCount = 2,
    TypeCountExceedsCapacity = 3,
    TypeCountMismatch = 4,
    MissingTargetMetadata = 5,
    TargetMetadataCountMismatch = 6,
    PendingOrReentrantConstructor = 7,
    FailedConstructor = 8,
    InvalidState = 9
}
