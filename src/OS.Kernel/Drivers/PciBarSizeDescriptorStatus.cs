namespace OS.Drivers;
public enum PciBarSizeDescriptorStatus
{
    Success = 0,
    Absent = 1,
    IoBarNotSized = 2,
    ConsumedByPrevious64BitBar = 3,
    DescriptorMalformed = 4,
    SourceTooShort = 5,
    MalformedReadback = 6,
    BaseAlignmentMismatch = 7
}
