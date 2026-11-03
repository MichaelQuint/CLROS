namespace OS.Kernel.Diagnostics;

public enum PanicSafeOwnedCom1FormatStatus : byte
{
    Success = 0,
    InvalidSubsystem = 1,
    InvalidReason = 2,
    InvalidVector = 3,
    DestinationTooSmall = 4,
    InvalidDestination = 5
}
#if !NOSTDLIB_EFI
#endif
