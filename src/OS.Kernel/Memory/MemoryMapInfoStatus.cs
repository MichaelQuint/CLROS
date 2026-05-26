namespace OS.Kernel.Memory;

public enum MemoryMapInfoStatus
{
    Unknown = 0,
    Success = 1,
    InvalidDescriptorSize = 2,
    InvalidDescriptorVersion = 3,
    InvalidDescriptorCount = 4,
    InvalidDescriptorEntries = 5,
    InvalidPageCount = 6,
    PageByteCountOverflow = 7,
    AddressRangeOverflow = 8,
    TotalPageCountOverflow = 9,
    TotalByteCountOverflow = 10
}
