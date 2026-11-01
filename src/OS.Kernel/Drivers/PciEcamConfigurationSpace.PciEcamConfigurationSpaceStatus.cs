namespace OS.Drivers;
public enum PciEcamConfigurationSpaceStatus
{
    Success = 0,
    InvalidBusRange = 1,
    BusOutsideSegment = 2,
    InvalidDevice = 3,
    InvalidFunction = 4,
    InvalidRegister = 5,
    InvalidAccessWidth = 6,
    InvalidRegisterAlignment = 7,
    AddressOverflow = 8
}
