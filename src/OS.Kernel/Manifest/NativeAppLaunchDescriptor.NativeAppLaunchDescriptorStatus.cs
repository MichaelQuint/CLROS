using OS.Storage;

namespace OS.Manifest;

public enum NativeAppLaunchDescriptorStatus
{
    Success = 0,
    LaunchPlanFailed = 1,
    AppPathUnavailable = 2,
    PackageFileMissing = 3,
    PackageFileInvalidPath = 4,
    PackageFileSystemFailed = 5,
    InvalidPayloadHeader = 6
}
