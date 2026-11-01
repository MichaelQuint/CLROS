namespace OS.Kernel.Diagnostics;
public enum Com1OwnedPortIoOperationStatus : byte
{
    Success = 0,
    BindingNotReady = 1,
    PortOutsideConfiguredRange = 2,
    InvocationRejected = 3
}
#if !NOSTDLIB_EFI
#endif
