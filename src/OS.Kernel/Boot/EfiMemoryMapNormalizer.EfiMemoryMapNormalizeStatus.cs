namespace OS.Boot;

public enum EfiMemoryMapNormalizeStatus
{
    Success = 0,
    MissingSource = 1,
    InvalidDescriptorSize = 2,
    InvalidDescriptorVersion = 3,
    InvalidDescriptorCount = 4,
    SizeOverflow = 5,
    SourceTooSmall = 6,
    InvalidPageCount = 7,
    PageByteCountOverflow = 8,
    AddressRangeOverflow = 9,
    TotalPageCountOverflow = 10,
    TotalByteCountOverflow = 11
}
