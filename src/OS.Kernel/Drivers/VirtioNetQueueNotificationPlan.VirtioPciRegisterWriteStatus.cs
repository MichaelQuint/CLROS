namespace OS.Drivers;
public enum VirtioPciRegisterWriteStatus
{
    Success = 0,
    NotReady = 1,
    RangeOutsideAperture = 2,
    UnsupportedWidth = 3,
    WriteFailed = 4
}
