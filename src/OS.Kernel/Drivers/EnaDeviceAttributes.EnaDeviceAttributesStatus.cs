namespace OS.Drivers;
using OS.Net;
using OS.Std.Binary;

public enum EnaDeviceAttributesStatus
{
    Success = 0,
    BufferTooSmall = 1,
    CommandIdOutOfRange = 2,
    CommandIdMismatch = 3,
    PhaseMismatch = 4,
    CompletionStatusNotSuccess = 5,
    ZeroMacAddress = 6,
    BroadcastMacAddress = 7,
    InvalidMtu = 8,
    InvalidPhysicalAddressWidth = 9,
    InvalidVirtualAddressWidth = 10
}
