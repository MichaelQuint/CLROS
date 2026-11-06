namespace OS.Storage;

public enum GptPartitionSelectorStatus
{
    Success = 0,
    ParseResultNotInitialized = 1,
    ParseFailed = 2,
    DescriptorSpanEmpty = 3,
    DescriptorCountMismatch = 4,
    MissingGuid = 5,
    NotFound = 6,
    InvalidDescriptorMetadata = 7
}
