namespace OS.Kernel.Runtime;

public enum ExecutableStubPageTransitionPlanStatus
{
    Success = 0,
    InvalidMemoryPlan = 1,
    UnmaterializedPayload = 2,
    UnsafeProtectionState = 3,
    InvalidRange = 4,
    RangeOverflow = 5
}
