using OS.Std.Binary;

namespace OS.Drivers;

public enum EnaPciResourcePlanStatus
{
    Success = 0,
    CandidateValidationFailed = 1,
    InvalidRegisterBarIndex = 2,
    RegisterBarUnavailable = 3,
    RegisterBarIsIo = 4,
    RegisterBarIsMalformed = 5,
    RegisterBarBaseAddressZero = 6,
    RegisterBarApertureMissing = 7,
    RegisterBarApertureSizeNotPowerOfTwo = 8,
    RegisterBarApertureMisaligned = 9,
    RegisterBarApertureTooSmall = 10,
    MissingMsixCapability = 11,
    MsixTableBarInvalid = 12,
    MsixPbaBarInvalid = 13,
    MsixTableBarNotMemory = 14,
    MsixPbaBarNotMemory = 15,
    MsixTableApertureMissing = 16,
    MsixPbaApertureMissing = 17,
    MsixOffsetMisaligned = 18,
    MsixRegionOutOfRange = 19,
    MsixRegionsOverlap = 20
}
