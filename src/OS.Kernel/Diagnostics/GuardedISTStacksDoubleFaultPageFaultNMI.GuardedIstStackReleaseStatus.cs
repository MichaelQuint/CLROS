namespace OS.Kernel.Diagnostics;

public enum GuardedIstStackReleaseStatus : byte
{
    Success = 0,
    InvalidAllocatorState = 1,
    InvalidLease = 2,
    OwnerMismatch = 3,
    AlreadyReleased = 4,
    StaleOwnership = 5
}
