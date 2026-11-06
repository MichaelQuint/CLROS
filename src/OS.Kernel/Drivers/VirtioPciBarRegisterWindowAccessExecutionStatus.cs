using OS.Std.Binary;

namespace OS.Drivers;

public enum VirtioPciBarRegisterWindowAccessExecutionStatus
{
    Success = 0,
    MissingAccessPlan = 1,
    WindowBufferTooSmall = 2,
    UnsupportedAccessWidth = 3
}
