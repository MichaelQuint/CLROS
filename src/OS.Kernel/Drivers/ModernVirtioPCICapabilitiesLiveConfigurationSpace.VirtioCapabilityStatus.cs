namespace OS.Drivers;
public enum VirtioCapabilityStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidSnapshot = 2,
    InvalidChain = 3,
    AbsentDevice = 4,
    UnsupportedDevice = 5,
    UnsupportedHeader = 6,
    InvalidCapability = 7,
    OverlappingCapabilities = 8,
    MissingRequired = 9,
    ReadFailed = 10
}
