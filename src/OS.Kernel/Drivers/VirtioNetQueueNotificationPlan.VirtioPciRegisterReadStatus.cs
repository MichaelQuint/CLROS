namespace OS.Drivers;

public enum VirtioPciRegisterReadStatus
{
    Success = 0,
    NotReady = 1,
    RangeOutsideAperture = 2,
    UnsupportedWidth = 3,
    ReadFailed = 4
}
