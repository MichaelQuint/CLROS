namespace OS.Kernel.Diagnostics;

public enum DisabledDefaultUd2VectorStatePreTerminalStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    ExistingState = 2,
    MissingBootServices = 3,
    MissingAllocatePages = 4,
    MissingFreePages = 5,
    CodeAllocationFailed = 6,
    InvalidCodeRange = 7,
    DataAllocationFailed = 8,
    InvalidDataRange = 9,
    OverlappingRanges = 10,
    MaterializationFailed = 11,
    ReadBackFailed = 12,
    MarkerTransportFailed = 13,
    PublicationFailed = 14
}
#if !NOSTDLIB_EFI
#endif
