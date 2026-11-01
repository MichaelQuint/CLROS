namespace OS.Boot;
public enum BootSequenceHandoffStatus
{
    Success = 0,
    InvalidBootMode = 1,
    UnknownCapability = 2,
    MissingRequiredCapability = 3,
    InvalidFirmwareServicesMode = 4,
    MissingMemoryMap = 5,
    MemoryMapNotOwned = 6,
    MissingSourceMemoryMap = 7,
    SourceMatchesOwnedBuffer = 8,
    InvalidMapSize = 9,
    InvalidDescriptorSize = 10,
    InvalidDescriptorVersion = 11,
    InvalidDescriptorCount = 12,
    DescriptorSizeOverflow = 13,
    InvalidAcpiRsdp = 14
}
