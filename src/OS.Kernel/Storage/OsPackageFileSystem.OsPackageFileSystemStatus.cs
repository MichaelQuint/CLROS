using OS.Std.Memory;

namespace OS.Storage;

public enum OsPackageFileSystemStatus
{
    Success = 0,
    PackageParseFailed = 1,
    InvalidPath = 2,
    MissingFile = 3,
    DestinationTooSmall = 4,
    InvalidReadRange = 5,
    FileIndexOutOfRange = 6
}
