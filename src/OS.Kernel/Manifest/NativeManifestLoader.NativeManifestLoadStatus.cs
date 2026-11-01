using OS.Storage;

namespace OS.Manifest;
public enum NativeManifestLoadStatus
{
    Success = 0,
    GptFailed = 1,
    PartitionDeviceFailed = 2,
    Fat32ReadFailed = 3,
    ManifestParseFailed = 4
}
