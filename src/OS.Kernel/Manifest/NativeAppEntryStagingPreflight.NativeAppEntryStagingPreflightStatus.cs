using OS.Kernel.Runtime;
using OS.Storage;

namespace OS.Manifest;

public enum NativeAppEntryStagingPreflightStatus
{
    Success = 0,
    NotInitialized = 1,
    ManifestLoadFailed = 2,
    PackageParseFailed = 3,
    LaunchPlanFailed = 4,
    LaunchDescriptorFailed = 5,
    PackageFileStagingFailed = 6,
    PayloadMappingFailed = 7,
    PayloadStagingFailed = 8,
    ExecutableMemoryPlanFailed = 9,
    InvocationPlanFailed = 10,
    MissingPackageStagingBytes = 11,
    MissingPayloadStagingBytes = 12
}
