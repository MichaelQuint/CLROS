namespace OS.Kernel.Diagnostics;
public enum PortIoOperationPlanStatus
{
    Success = 0,
    InvalidOperationKind = 1,
    OutputBufferTooSmall = 2,
    MissingDestination = 3
}
#if !NOSTDLIB_EFI
#endif
