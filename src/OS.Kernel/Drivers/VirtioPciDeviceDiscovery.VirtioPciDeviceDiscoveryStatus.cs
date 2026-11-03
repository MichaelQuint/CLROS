namespace OS.Drivers;

public enum VirtioPciDeviceDiscoveryStatus
{
    Success = 0,
    UnsupportedVendor = 1,
    UnsupportedDeviceId = 2,
    UnsupportedDeviceKind = 3,
    MissingCommonConfigurationCapability = 4,
    MissingNotifyCapability = 5,
    MissingIsrStatusCapability = 6,
    MissingDeviceConfigurationCapability = 7,
    InvalidCommonConfigurationCapability = 8,
    InvalidNotifyCapability = 9,
    InvalidIsrStatusCapability = 10,
    InvalidDeviceConfigurationCapability = 11
}
