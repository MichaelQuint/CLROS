namespace OS.Drivers;

public enum PciBusScanStatus
{
    Success = 0,
    MissingReader = 1,
    InvalidBusRange = 2,
    InvalidDeviceRange = 3,
    InvalidFunctionLimit = 4,
    DestinationTooSmall = 5,
    ReaderFailed = 6,
    HeaderParseFailed = 7
}
