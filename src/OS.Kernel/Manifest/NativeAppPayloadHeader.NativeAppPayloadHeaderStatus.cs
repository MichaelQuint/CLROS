using OS.Std.Binary;

namespace OS.Manifest;
public enum NativeAppPayloadHeaderStatus
{
    Success = 0,
    PayloadTooSmall = 1,
    InvalidMagic = 2,
    UnsupportedVersion = 3,
    InvalidHeaderSize = 4,
    UnsupportedFlags = 5,
    InvalidEntryOffset = 6,
    InvalidReserved = 7
}
