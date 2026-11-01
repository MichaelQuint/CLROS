namespace OS.Kernel.Diagnostics;
public enum BootEventRecordFormatterStatus
{
    Success = 0,
    NoRecords = 1,
    InvalidRecord = 2,
    DestinationTooSmall = 3,
    BufferUnavailable = 4,
    BufferWriteFailed = 5
}
