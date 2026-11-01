namespace OS.Kernel.Runtime;
public enum ExecutableStubBundlePlanStatus
{
    Success = 0,
    InvalidMemoryPlan = 1,
    InsufficientMemoryPlan = 2,
    DuplicateSlot = 3,
    ZeroLengthSlot = 4,
    UnknownSlotKind = 5,
    PayloadLengthOverflow = 6,
    RequiredMemoryLengthOverflow = 7
}
