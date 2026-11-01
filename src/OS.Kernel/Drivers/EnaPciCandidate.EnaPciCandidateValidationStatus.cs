namespace OS.Drivers;
public enum EnaPciCandidateValidationStatus
{
    Success = 0,
    UnsupportedVendor = 1,
    UnsupportedDeviceId = 2,
    UnsupportedHeaderType = 3,
    UnsupportedClassCode = 4,
    UnsupportedSubclass = 5,
    UnsupportedProgrammingInterface = 6,
    MissingCapabilitiesList = 7,
    InvalidCapabilityPointer = 8
}
