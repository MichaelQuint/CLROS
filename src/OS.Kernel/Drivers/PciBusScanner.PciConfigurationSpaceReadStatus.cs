namespace OS.Drivers;

public enum PciConfigurationSpaceReadStatus
{
    Success = 0,
    UnsupportedTuple = 1,
    ReadFailed = 2,
    DestinationTooSmall = 3
}
