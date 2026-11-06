namespace OS.Storage;

public enum OsPackageLoadStatus
{
    Success = 0,
    GptFailed = 1,
    PartitionDeviceFailed = 2,
    Fat32ReadFailed = 3,
    PackageParseFailed = 4
}
