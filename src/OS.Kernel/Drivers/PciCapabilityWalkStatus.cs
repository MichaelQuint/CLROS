namespace OS.Drivers;
public enum PciCapabilityWalkStatus : byte
{
    Uninitialized = 0,
    CompleteEmpty = 1,
    CompletePresent = 2,
    InvalidStorage = 3,
    Truncated = 4,
    AbsentDevice = 5,
    UnsupportedHeader = 6,
    OffsetOutOfRange = 7,
    OffsetUnaligned = 8,
    LoopDetected = 9
}
