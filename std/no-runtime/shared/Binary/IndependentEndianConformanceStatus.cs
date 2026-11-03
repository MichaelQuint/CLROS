namespace OS.Std.Binary;

public enum IndependentEndianConformanceStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    ReadDestinationTooSmall = 2,
    ReverseDestinationTooSmall = 3,
    ReadVectorCountMismatch = 4,
    ReverseVectorCountMismatch = 5,
    ReadVectorUninitialized = 6,
    ReverseVectorUninitialized = 7,
    ReadVectorKeyOutOfRange = 8,
    ReverseVectorKeyOutOfRange = 9,
    DuplicateReadVectorKey = 10,
    DuplicateReverseVectorKey = 11,
    ReadVectorExpectedValueMismatch = 12,
    ReverseVectorExpectedValueMismatch = 13,
    ReadVectorOrderMismatch = 14,
    ReverseVectorOrderMismatch = 15,
    CatalogFingerprintMismatch = 16,
    ReadVectorPointerNull = 17,
    ReverseVectorPointerNull = 18
}
#if !NOSTDLIB_EFI
#else

#endif
#if NOSTDLIB_EFI && M04_INDEPENDENT_ENDIAN_CONFORMANCE_PROBE && !M01_OWNED_PAGING_PROBE

#endif
