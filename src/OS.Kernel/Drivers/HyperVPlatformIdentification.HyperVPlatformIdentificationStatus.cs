namespace OS.Drivers;

public enum HyperVPlatformIdentificationStatus
{
    Success = 0,
    NoHypervisorPresentBit = 1,
    UnsupportedVendor = 2,
    InsufficientMaxLeaf = 3,
    MissingFeatureIdentificationLeaf = 4
}
