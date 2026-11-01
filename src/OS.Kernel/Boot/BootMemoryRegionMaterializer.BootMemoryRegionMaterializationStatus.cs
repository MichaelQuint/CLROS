namespace OS.Boot;
public enum BootMemoryRegionMaterializationStatus
{
    Success = 0,
    MissingSource = 1,
    MissingDestination = 2,
    InvalidDescriptorSize = 3,
    InvalidDescriptorVersion = 4,
    InvalidDescriptorCount = 5,
    SizeOverflow = 6,
    SourceTooSmall = 7,
    DestinationTooSmall = 8,
    InvalidPageCount = 9,
    PageByteCountOverflow = 10,
    AddressRangeOverflow = 11,
    TotalPageCountOverflow = 12,
    TotalByteCountOverflow = 13
}
#if !NOSTDLIB_EFI
#endif
