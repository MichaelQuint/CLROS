namespace OS.Drivers;

public enum VirtioRegionStatus : byte
{
    Uninitialized,
    Success,
    InvalidStorage,
    MissingCapability,
    InvalidCapability,
    InvalidBar,
    OutsideBar,
    InvalidAddress,
    Overlap
}
