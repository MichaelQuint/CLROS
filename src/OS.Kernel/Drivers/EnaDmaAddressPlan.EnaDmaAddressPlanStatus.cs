namespace OS.Drivers;

public enum EnaDmaAddressPlanStatus
{
    Success = 0,
    InvalidAddressWidth = 1,
    ZeroLength = 2,
    InvalidAlignment = 3,
    UnalignedBaseAddress = 4,
    AddressOverflow = 5,
    AddressOutOfRange = 6,
    QueueLayoutRejected = 7,
    QueueMemoryTooSmall = 8
}
