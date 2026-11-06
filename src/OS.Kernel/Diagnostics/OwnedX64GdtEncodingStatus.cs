namespace OS.Kernel.Diagnostics;

public enum OwnedX64GdtEncodingStatus : byte
{
    Success = 0,
    InvalidDestination = 1,
    DestinationTooSmall = 2,
    NullTssBaseAddress = 3,
    NonCanonicalTssBaseAddress = 4,
    InvalidTssLimit = 5,
    ReadBackMismatch = 6
}
