#if !NOSTDLIB_EFI
using OS.Kernel.Runtime;

#endif
namespace OS.Kernel.Diagnostics;

public enum CheckedPortIoFunctionPointerInvocationStatus
{
    Success = 0,
    BindingNotReady = 1,
    PortOutsideConfiguredRange = 2
}
