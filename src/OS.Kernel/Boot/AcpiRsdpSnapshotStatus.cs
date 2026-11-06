using OS.Kernel.Platform;
using OS.Std.Binary;

namespace OS.Boot;

public enum AcpiRsdpSnapshotStatus
{
    Success = 0,
    InvalidPhysicalAddress = 1,
    ScratchTooSmall = 2,
    PhysicalReadFailed = 3,
    BaseValidationFailed = 4,
    ExtendedReadFailed = 5,
    ExtendedValidationFailed = 6,
    PhysicalMemoryUnavailable = 7
}
