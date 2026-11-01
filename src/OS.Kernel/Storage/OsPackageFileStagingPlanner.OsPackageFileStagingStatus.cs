using OS.Std.Memory;

namespace OS.Storage;

public enum OsPackageFileStagingStatus
{
    Success = 0,
    OpenFileFailed = 1,
    EmptyFile = 2,
    PackageRangeInvalid = 3,
    DestinationTooSmall = 4
}
