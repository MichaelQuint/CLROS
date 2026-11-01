namespace OS.Kernel.Diagnostics;
public enum OwnedX64GdtVerificationStatus : byte
{
    Success = 0,
    InvalidSource = 1,
    SourceTooSmall = 2,
    NullTssBaseAddress = 3,
    NonCanonicalTssBaseAddress = 4,
    InvalidTssLimit = 5,
    ByteMismatch = 6
}
#if !NOSTDLIB_EFI
#endif
