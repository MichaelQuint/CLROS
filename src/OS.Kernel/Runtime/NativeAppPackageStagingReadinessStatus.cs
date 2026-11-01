using OS.Manifest;
using OS.Storage;

namespace OS.Kernel.Runtime;
public enum NativeAppPackageStagingReadinessStatus
{
    Success = 0,
    ManifestLoadFailed = 1,
    PackageParseFailed = 2,
    LaunchPlanFailed = 3,
    LaunchDescriptorFailed = 4,
    PackageFileStagingFailed = 5,
    PayloadMappingFailed = 6,
    PayloadStagingFailed = 7,
    ExecutableMemoryPlanFailed = 8,
    InvocationPlanFailed = 9,
    PackageStagingByteMismatch = 10,
    PayloadStagingByteMismatch = 11,
    WritableStagingByteMismatch = 12,
    EntryOffsetMismatch = 13
}
