namespace OS.Drivers;
public enum PciBarSizingProbeExecutionStatus
{
    Success = 0,
    AddressPlanFailed = 1,
    CommandSourceTooShort = 2,
    ReadbackDestinationTooSmall = 3,
    MissingAccessor = 4,
    CommandMalformed = 5,
    WriteFailed = 6,
    ReadFailed = 7
}
