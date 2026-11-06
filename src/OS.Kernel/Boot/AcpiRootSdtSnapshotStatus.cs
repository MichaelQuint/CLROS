using OS.Kernel.Platform;
using OS.Std.Binary;

namespace OS.Boot;

public enum AcpiRootSdtSnapshotStatus
{
    Success = 0,
    InvalidRsdpSnapshot = 1,
    PhysicalMemoryUnavailable = 2,
    NoUsableRootTableAddress = 3,
    ScratchTooSmall = 4,
    DestinationTooSmall = 5,
    HeaderReadFailed = 6,
    FullReadFailed = 7,
    InvalidSignature = 8,
    InvalidTableLength = 9,
    InvalidPointerArrayLength = 10,
    InvalidChecksum = 11
}
