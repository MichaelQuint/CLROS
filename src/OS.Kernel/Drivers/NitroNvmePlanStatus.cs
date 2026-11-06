using OS.Std.Binary;

namespace OS.Drivers;

public enum NitroNvmePlanStatus
{
    Success = 0,
    UnsupportedHeaderType = 1,
    UnsupportedClassCode = 2,
    UnsupportedSubclass = 3,
    UnsupportedProgrammingInterface = 4,
    InvalidRegisterBarIndex = 5,
    RegisterBarUnavailable = 6,
    RegisterBarIsIo = 7,
    RegisterBarIsMalformed = 8,
    RegisterBarBaseAddressZero = 9,
    RegisterBarApertureMissing = 10,
    RegisterBarApertureSizeNotPowerOfTwo = 11,
    RegisterBarApertureMisaligned = 12,
    RegisterBarApertureTooSmall = 13,
    ControllerIdentifyBufferTooSmall = 14,
    ControllerIdentifyBufferTooLarge = 15,
    NamespaceIdentifyBufferTooSmall = 16,
    NamespaceIdentifyBufferTooLarge = 17,
    NamespaceCountZero = 18,
    NamespaceIdZero = 19,
    NamespaceIdOutOfRange = 20,
    MalformedSerialNumber = 21,
    MalformedModelNumber = 22,
    SelectedLbaFormatOutOfRange = 23,
    ImpossibleLogicalBlockSize = 24,
    ZeroCapacity = 25,
    CapacityOverflow = 26
}
