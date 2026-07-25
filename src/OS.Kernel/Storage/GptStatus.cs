namespace OS.Storage;

public enum GptStatus
{
    Success = 0,
    InvalidBlockDevice = 1,
    UnsupportedGeometry = 2,
    PrimaryHeaderReadFailed = 3,
    PrimaryHeaderShortRead = 4,
    InvalidHeaderSignature = 5,
    InvalidHeaderSize = 6,
    InvalidHeaderLba = 7,
    InvalidUsableLbaRange = 8,
    InvalidPartitionEntryCount = 9,
    InvalidPartitionEntrySize = 10,
    InvalidPartitionEntryArrayRange = 11,
    PartitionEntryReadFailed = 12,
    PartitionEntryShortRead = 13,
    MissingPartitionEntry = 14,
    InvalidPartitionEntryRange = 15,
    EfiSystemPartitionNotFound = 16
}
