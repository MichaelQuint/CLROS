using OS.Boot;
using OS.Manifest;

namespace OS.Kernel.Runtime;

public enum NativeAppInvocationPlanStatus
{
    Success = 0,
    ExecutableMemoryPlanFailed = 1,
    LaunchPlanFailed = 2,
    InvalidExecutableMemoryMetadata = 3,
    InvalidEntryOffset = 4,
    InvalidStackSize = 5,
    MetadataOutOfBounds = 6,
    RangeOverflow = 7
}
