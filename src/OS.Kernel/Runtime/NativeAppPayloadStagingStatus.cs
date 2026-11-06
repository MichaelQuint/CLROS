using OS.Manifest;
using OS.Std.Memory;

namespace OS.Kernel.Runtime;

public enum NativeAppPayloadStagingStatus
{
    Success = 0,
    MappingPlanFailed = 1,
    RangeOverflow = 2,
    PackageRangeOutOfBounds = 3,
    DestinationTooSmall = 4
}
