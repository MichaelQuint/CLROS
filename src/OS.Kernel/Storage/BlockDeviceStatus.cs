namespace OS.Storage;

public enum BlockDeviceStatus
{
    Success = 0,
    InvalidBlockSize = 1,
    UnalignedBlockSize = 2,
    InvalidBlockCount = 3,
    InvalidBlockRange = 4,
    DestinationTooSmall = 5,
    Overflow = 6,
    IoError = 7
}
