using OS.Std.Binary;

namespace OS.Drivers;
public enum PciMsixCapabilityParseStatus
{
    Success = 0,
    BufferTooSmall = 1,
    InvalidCapabilityPointer = 2,
    TruncatedCapability = 3,
    InvalidNextCapabilityPointer = 4,
    CapabilityLoopDetected = 5,
    CapabilityNotFound = 6,
    InvalidBarIndicator = 7
}
