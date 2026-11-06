namespace OS.Drivers;

public enum PciEcamSegmentScanStatus
{
    Success = 0,
    MissingReader = 1,
    InvalidSegmentRange = 2,
    ScratchTooSmall = 3,
    DestinationTooSmall = 4,
    NestedScannerFailed = 5
}
