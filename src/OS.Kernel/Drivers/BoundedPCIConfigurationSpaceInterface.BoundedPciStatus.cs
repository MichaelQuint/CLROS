namespace OS.Drivers;

public enum BoundedPciStatus : byte
{
    Uninitialized,
    Success,
    InvalidWindow,
    InvalidAccess,
    OutsideWindow,
    InvalidTuple,
    InvalidRegister,
    AbsentDevice,
    UnsupportedHeader,
    InvalidBridge,
    Capacity,
    ReadBudget
}
