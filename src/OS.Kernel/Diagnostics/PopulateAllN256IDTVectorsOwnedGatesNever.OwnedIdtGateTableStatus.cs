namespace OS.Kernel.Diagnostics;
public enum OwnedIdtGateTableStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    TableCapacityTooSmall = 2,
    MissingTable = 3,
    MisalignedTablePointer = 4,
    NonCanonicalTablePointer = 5,
    TablePointerSpanOverflow = 6,
    NonCanonicalTablePointerSpan = 7,
    NullTableAddress = 8,
    MisalignedTableAddress = 9,
    NonCanonicalTableAddress = 10,
    TableAddressSpanOverflow = 11,
    NonCanonicalTableAddressSpan = 12,
    NullStubPageAddress = 13,
    MisalignedStubPageAddress = 14,
    NonCanonicalStubPageAddress = 15,
    StubPageAddressSpanOverflow = 16,
    NonCanonicalStubPageAddressSpan = 17,
    TableOverlapsStubPage = 18,
    TableByteMismatch = 19
}
#if !NOSTDLIB_EFI
#endif
