using OS.Std.Binary;

namespace OS.Boot;

public enum UefiMemoryDescriptorReaderStatus
{
    Success = 0,
    MissingSource = 1,
    InvalidDescriptorSize = 2,
    InvalidDescriptorVersion = 3,
    InvalidDescriptorCount = 4,
    SizeOverflow = 5,
    SourceTooSmall = 6,
    DestinationTooSmall = 7
}
