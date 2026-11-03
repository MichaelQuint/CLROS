namespace OS.Kernel.Diagnostics;

public enum NormalizeInterruptFramePageStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    PageCapacityTooSmall = 2,
    MissingPage = 3,
    MisalignedPagePointer = 4,
    NonCanonicalPagePointer = 5,
    PagePointerSpanOverflow = 6,
    NonCanonicalPagePointerSpan = 7,
    NullPageAddress = 8,
    MisalignedPageAddress = 9,
    NonCanonicalPageAddress = 10,
    PageAddressSpanOverflow = 11,
    NonCanonicalPageAddressSpan = 12,
    NullCommonTargetAddress = 13,
    NonCanonicalCommonTargetAddress = 14,
    CommonTargetOverlapsPage = 15,
    CommonTargetOutOfRel32Range = 16,
    PageByteMismatch = 17
}
#if !NOSTDLIB_EFI
#endif
