namespace OS.Drivers;
public enum PciBarSizingPlanStatus
{
    Success = 0,
    SourceTooShort = 1,
    DescriptorMalformed = 2,
    MalformedReadback = 3,
    BaseAlignmentMismatch = 4
}
