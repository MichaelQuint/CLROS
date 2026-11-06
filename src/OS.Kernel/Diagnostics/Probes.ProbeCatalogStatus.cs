namespace OS.Kernel.Diagnostics;
#if !NOSTDLIB_EFI
public enum ProbeCatalogStatus
{
    Success = 0,
    DestinationTooSmall = 1,
    IndexOutOfRange = 2
}
#endif
