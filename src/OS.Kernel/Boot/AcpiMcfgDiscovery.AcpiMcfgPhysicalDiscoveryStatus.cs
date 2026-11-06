using OS.Drivers;
using OS.Kernel.Platform;
using OS.Std.Binary;

namespace OS.Boot;

public enum AcpiMcfgPhysicalDiscoveryStatus
{
    Success = 0,
    InvalidRootSdtSnapshot = 1,
    PhysicalMemoryUnavailable = 2,
    HeaderScratchTooSmall = 3,
    RootPointerListFailed = 4,
    ChildHeaderReadFailed = 5,
    ChildTableLengthInvalid = 6,
    ChildTableDestinationTooSmall = 7,
    ChildFullReadFailed = 8,
    ChildTableChecksumInvalid = 9,
    SignatureNotFound = 10,
    McfgParseFailed = 11
}
