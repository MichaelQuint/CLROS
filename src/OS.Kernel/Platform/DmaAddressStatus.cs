namespace OS.Kernel.Platform;

public enum DmaAddressStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidAddress = 2,
    InvalidLength = 3,
    InvalidAlignment = 4,
    AddressOverflow = 5,
    InvalidDeviceWidth = 6,
    DeviceAddressLimit = 7,
    OutOfRange = 8
}
