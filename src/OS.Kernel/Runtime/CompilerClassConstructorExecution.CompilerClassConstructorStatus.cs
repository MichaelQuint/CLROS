using OS.Boot;

namespace OS.Kernel.Runtime;

public enum CompilerClassConstructorStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    StorageRejected = 2,
    ModuleRejected = 3,
    EagerTableRejected = 4,
    TargetRejected = 5,
    ContextRejected = 6,
    CapacityExceeded = 7,
    AlreadyBound = 8,
    NotBound = 9,
    StateChanged = 10,
    EagerNotStarted = 11,
    ExecutionInProgress = 12,
    InvocationRejected = 13,
    InitializationFailed = 14
}
