namespace OS.Kernel.Runtime;
public enum NativeAotClassConstructorStateTableStatus
{
    Success = 0,
    InvalidKey = 1,
    MissingKey = 2,
    DuplicateKey = 3,
    CapacityExceeded = 4,
    ReentryCycle = 5,
    PreviouslyFailed = 6,
    AlreadyInitialized = 7,
    NotInitializing = 8,
    FailureCodeRequired = 9,
    InvalidState = 10
}
