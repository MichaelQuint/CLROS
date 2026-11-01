using OS.Std.Binary;

namespace OS.Drivers;
public enum VirtioPciCapabilityListParseStatus
{
    Success = 0,
    CapabilityOffsetUnaligned = 1,
    CapabilityHeaderTruncated = 2,
    CapabilityLoopDetected = 3,
    VendorCapabilityTooShort = 4,
    VendorCapabilityRecordOutOfRange = 5,
    UnsupportedVendorCapability = 6,
    InvalidBar = 7,
    CapabilityRegionOverflow = 8,
    MissingCommonConfigurationCapability = 9,
    MissingNotifyCapability = 10,
    MissingIsrStatusCapability = 11,
    MissingDeviceConfigurationCapability = 12,
    NotifyCapabilityTooShort = 13
}
