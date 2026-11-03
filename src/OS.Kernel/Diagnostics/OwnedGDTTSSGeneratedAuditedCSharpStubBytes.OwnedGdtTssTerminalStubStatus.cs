namespace OS.Kernel.Diagnostics;

public enum OwnedGdtTssTerminalStubStatus : byte
{
    Success = 0,
    MissingContext = 1,
    ContextCapacityTooSmall = 2,
    MisalignedContextPointer = 3,
    NonCanonicalContextPointer = 4,
    ContextPointerSpanOverflow = 5,
    NonCanonicalContextPointerSpan = 6,
    NullContextAddress = 7,
    MisalignedContextAddress = 8,
    NonCanonicalContextAddress = 9,
    ContextAddressSpanOverflow = 10,
    NonCanonicalContextAddressSpan = 11,
    MissingStub = 12,
    StubCapacityTooSmall = 13,
    MisalignedStubPointer = 14,
    NonCanonicalStubPointer = 15,
    StubPointerSpanOverflow = 16,
    NonCanonicalStubPointerSpan = 17,
    NullStubAddress = 18,
    MisalignedStubAddress = 19,
    NonCanonicalStubAddress = 20,
    StubAddressSpanOverflow = 21,
    NonCanonicalStubAddressSpan = 22,
    StubOverlapsContextPointer = 23,
    StubOverlapsContextAddress = 24,
    ContextByteMismatch = 25,
    StubByteMismatch = 26,
    UnexpectedGeneratedFingerprint = 27
}
#if !NOSTDLIB_EFI
#endif
