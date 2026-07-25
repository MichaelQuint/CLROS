namespace OS.Boot;

public enum UefiMemoryMapStatus
{
    Unknown = 0,
    Success = 1,
    BufferTooSmall = 2,
    FirmwareError = 3,
    InvalidDescriptorSize = 4,
    InvalidDescriptorVersion = 5,
    InvalidDescriptorEntries = 6
}
