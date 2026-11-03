using OS.Manifest;

namespace OS.Kernel.Runtime;

public enum NativeAppPayloadMappingPlanStatus
{
    Success = 0,
    DescriptorFailed = 1,
    PayloadTooSmall = 2,
    ZeroExecutableContent = 3,
    InvalidEntryOffset = 4,
    RangeOverflow = 5
}
