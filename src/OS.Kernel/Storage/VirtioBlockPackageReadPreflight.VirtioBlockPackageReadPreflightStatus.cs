namespace OS.Storage;

public enum VirtioBlockPackageReadPreflightStatus
{
    Success = 0,
    IncompleteReadSession = 1,
    InvalidBlockDevice = 2,
    HeaderScratchTooSmall = 3,
    PartitionEntryScratchTooSmall = 4,
    PartitionDescriptorTableEmpty = 5,
    PackageBufferTooSmall = 6,
    PackageLoadFailed = 7
}
