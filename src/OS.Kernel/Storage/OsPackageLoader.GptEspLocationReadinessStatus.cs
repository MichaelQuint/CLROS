namespace OS.Storage;

public enum GptEspLocationReadinessStatus
{
    Success = 0,
    InvalidBlockDevice = 1,
    HeaderBufferTooSmall = 2,
    PrimaryHeaderReadFailed = 3,
    PrimaryHeaderShortRead = 4,
    ParserPlanningFailed = 5,
    PartitionEntryBufferTooSmall = 6,
    PartitionEntryReadFailed = 7,
    PartitionEntryShortRead = 8,
    ParserFailed = 9,
    EfiSystemPartitionNotFound = 10
}
