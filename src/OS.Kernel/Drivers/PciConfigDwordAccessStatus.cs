namespace OS.Drivers;
public enum PciConfigDwordAccessStatus
{
    Success = 0,
    NotReady = 1,
    UnsupportedAddress = 2,
    AccessFailed = 3
}
