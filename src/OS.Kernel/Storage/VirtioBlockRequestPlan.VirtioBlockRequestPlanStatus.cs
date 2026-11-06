using OS.Std.Binary;

namespace OS.Storage;

public enum VirtioBlockRequestPlanStatus
{
    Success = 0,
    UnsupportedRequestType = 1,
    InvalidSectorCount = 2,
    DataByteCountOverflow = 3,
    SectorRangeOverflow = 4,
    HeaderDestinationTooSmall = 5,
    WriteRejectedReadOnlyMode = 6
}
