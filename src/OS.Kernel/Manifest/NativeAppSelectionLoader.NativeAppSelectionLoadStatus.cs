using OS.Boot;
using OS.Storage;

namespace OS.Manifest;

public enum NativeAppSelectionLoadStatus
{
    Success = 0,
    ManifestLoadFailed = 1,
    PackageLoadFailed = 2,
    PackageParseFailed = 3,
    LaunchPlanFailed = 4,
    LaunchDescriptorFailed = 5
}
