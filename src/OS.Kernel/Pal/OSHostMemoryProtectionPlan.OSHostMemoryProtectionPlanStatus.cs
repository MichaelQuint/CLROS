namespace OS.Pal;

public enum OSHostMemoryProtectionPlanStatus
{
    Success = 0,
    InvalidPageSize = 1,
    InvalidRange = 2,
    RangeOverflow = 3,
    InvalidProtection = 4,
    WritableExecutableRejected = 5,
    InvalidTransition = 6
}
