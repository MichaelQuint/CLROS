namespace OS.Kernel.Memory;

public enum MemoryRegionType
{
    Unknown = 0,
    Reserved = 1,
    LoaderCode = 2,
    LoaderData = 3,
    BootServicesCode = 4,
    BootServicesData = 5,
    RuntimeServicesCode = 6,
    RuntimeServicesData = 7,
    ConventionalMemory = 8,
    UnusableMemory = 9,
    AcpiReclaimMemory = 10,
    AcpiMemoryNvs = 11,
    MemoryMappedIo = 12,
    MemoryMappedIoPortSpace = 13,
    PalCode = 14,
    PersistentMemory = 15
}
