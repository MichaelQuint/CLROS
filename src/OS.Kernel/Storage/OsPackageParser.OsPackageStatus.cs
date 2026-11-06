using OS.Std.Binary;
using OS.Std.Memory;

namespace OS.Storage;

public enum OsPackageStatus
{
    Success = 0,
    PackageTooSmall = 1,
    InvalidMagic = 2,
    UnsupportedVersion = 3,
    InvalidHeaderSize = 4,
    InvalidFileCount = 5,
    InvalidFileTableEntrySize = 6,
    InvalidFileTableRange = 7,
    InvalidEntryRange = 8,
    EntryOverlapsMetadata = 9,
    EntryOverlapsEntry = 10,
    InvalidPath = 11,
    DuplicatePath = 12,
    MissingFile = 13,
    FileIndexOutOfRange = 14,
    DestinationTooSmall = 15
}
