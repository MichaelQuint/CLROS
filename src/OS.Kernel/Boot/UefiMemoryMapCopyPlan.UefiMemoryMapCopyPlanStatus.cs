namespace OS.Boot;
public enum UefiMemoryMapCopyPlanStatus
{
    Success = 0,
    MissingSource = 1,
    InvalidMapSize = 2,
    InvalidDescriptorSize = 3,
    InvalidDescriptorVersion = 4,
    InvalidDescriptorCount = 5,
    MapSizeDescriptorMismatch = 6,
    DescriptorCountLimitExceeded = 7,
    SizeOverflow = 8,
    DestinationTooSmall = 9
}
