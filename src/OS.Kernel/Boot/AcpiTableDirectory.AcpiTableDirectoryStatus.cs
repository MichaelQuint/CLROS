using OS.Std.Binary;

namespace OS.Boot;

public enum AcpiTableDirectoryStatus
{
    Success = 0,
    SourcePointerListFailed = 1,
    SnapshotCapacityTooSmall = 2,
    TableSourceMissing = 3,
    TableSourceTooShort = 4,
    TableLengthInvalid = 5,
    TableChecksumInvalid = 6,
    DestinationTooSmall = 7,
    SignatureNotFound = 8
}
