namespace OS.Drivers;

public enum PciBarProbeStatus : byte
{
    Uninitialized,
    Success,
    InvalidInput,
    ReadFailed,
    WriteFailed,
    AbsentDevice,
    UnsupportedHeader,
    UnsupportedType,
    InvalidMask,
    InvalidExtent,
    DecodeNotDisabled,
    RestoreFailed,
    BusMasterEnabled
}
