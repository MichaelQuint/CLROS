using OS.Std.Binary;
using OS.Std.Hash;
using OS.Std.Memory;

namespace OS.Storage;

public enum GptPartitionTableStatus
{
    Success = 0,
    UnsupportedSectorSize = 1,
    InvalidBlockCount = 2,
    HeaderSectorTooSmall = 3,
    InvalidHeaderSignature = 4,
    UnsupportedRevision = 5,
    InvalidHeaderSize = 6,
    InvalidCurrentLba = 7,
    InvalidBackupLba = 8,
    InvalidUsableLbaRange = 9,
    InvalidPartitionEntryCount = 10,
    InvalidPartitionEntrySize = 11,
    InvalidPartitionEntryArrayRange = 12,
    PartitionEntryArrayTooLarge = 13,
    PartitionEntryBufferTooSmall = 14,
    DestinationTooSmall = 15,
    InvalidPartitionEntryRange = 16,
    HeaderCrcMismatch = 17,
    PartitionEntryArrayCrcMismatch = 18
}
