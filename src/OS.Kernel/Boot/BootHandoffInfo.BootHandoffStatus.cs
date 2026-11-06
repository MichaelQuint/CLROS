using OS.Kernel.Platform;

namespace OS.Boot;

public enum BootHandoffStatus
{
    Unknown = 0,
    Success = 1,
    MissingMemoryMap = 2,
    InvalidMapSize = 3,
    InvalidDescriptorSize = 4,
    InvalidDescriptorVersion = 5,
    InvalidDescriptorCount = 6,
    InvalidAcpiRsdp = 7
}
