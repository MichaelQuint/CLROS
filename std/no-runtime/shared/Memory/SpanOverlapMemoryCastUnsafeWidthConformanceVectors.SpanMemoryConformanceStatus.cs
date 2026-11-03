namespace OS.Std.Memory;

public enum SpanMemoryConformanceStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    DestinationTooSmall = 2,
    VectorPointerNull = 3,
    VectorCountMismatch = 4,
    VectorUninitialized = 5,
    VectorKeyOutOfRange = 6,
    OperationOutOfRange = 7,
    VectorMalformed = 8,
    DuplicateVectorKey = 9,
    VectorOrderMismatch = 10,
    CatalogFingerprintMismatch = 11
}
#if !NOSTDLIB_EFI
#else

#endif
