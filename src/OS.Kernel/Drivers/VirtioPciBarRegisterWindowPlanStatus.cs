using OS.Std.Binary;

namespace OS.Drivers;

public enum VirtioPciBarRegisterWindowPlanStatus
{
    Success = 0,
    BindingPlanFailed = 1,
    ApertureValidationFailed = 2,
    WindowSourceTooSmall = 3,
    MissingWindow = 4,
    WindowNotMapped = 5,
    WindowNotMmio = 6,
    WindowBaseAddressZero = 7,
    PhysicalBaseMismatch = 8,
    WindowLengthTooSmall = 9,
    ApertureAddressOverflow = 10,
    AccessRangeOverflow = 11,
    AccessRangeOutsideAperture = 12,
    AccessUnaligned = 13,
    UnsupportedAccessWidth = 14
}
