namespace OS.Storage;

public enum VirtioBlockReadTransactionPlanStatus
{
    Success = 0,
    InvalidSectorGeometry = 1,
    InvalidSectorRange = 2,
    DataByteCountOverflow = 3,
    DestinationTooSmall = 4,
    QueueTooSmall = 5,
    RequestHeaderDestinationTooSmall = 6,
    StatusDestinationTooSmall = 7,
    RequestPlanFailed = 8
}
