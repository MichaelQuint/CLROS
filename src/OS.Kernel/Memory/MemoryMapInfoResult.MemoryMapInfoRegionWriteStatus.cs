using OS.Boot;

namespace OS.Kernel.Memory;

public enum MemoryMapInfoRegionWriteStatus
{
    Unknown = 0,
    Success = 1,
    InvalidDescriptorSize = 2,
    InvalidDescriptorVersion = 3,
    InvalidDescriptorCount = 4,
    InvalidDescriptorEntries = 5,
    DestinationTooSmall = 6,
    InvalidPageCount = 7,
    PageByteCountOverflow = 8,
    AddressRangeOverflow = 9,
    TotalPageCountOverflow = 10,
    TotalByteCountOverflow = 11
}
