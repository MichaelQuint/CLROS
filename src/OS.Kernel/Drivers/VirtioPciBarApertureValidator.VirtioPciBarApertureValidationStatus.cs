namespace OS.Drivers;

public enum VirtioPciBarApertureValidationStatus
{
    Success = 0,
    DiscoveryFailed = 1,
    MissingRequiredCapability = 2,
    BarIndexOutOfRange = 3,
    BarSizeUnavailable = 4,
    BarAbsent = 5,
    BarIsIo = 6,
    BarConsumedByPrevious64BitBar = 7,
    BarMalformed = 8,
    CapabilityRegionOverflow = 9,
    CapabilityRegionBeyondBarSize = 10
}
