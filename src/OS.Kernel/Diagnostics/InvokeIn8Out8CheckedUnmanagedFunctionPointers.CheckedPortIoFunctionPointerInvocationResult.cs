#if !NOSTDLIB_EFI
using OS.Kernel.Runtime;

#endif
namespace OS.Kernel.Diagnostics;

public readonly struct CheckedPortIoFunctionPointerInvocationResult
{
    private CheckedPortIoFunctionPointerInvocationResult(bool isInitialized, CheckedPortIoFunctionPointerInvocationStatus status, PortIoOperationKind operationKind, ushort port, byte value, bool wasInvoked)
    {
        IsInitialized = isInitialized;
        Status = status;
        OperationKind = operationKind;
        Port = port;
        Value = value;
        WasInvoked = wasInvoked;
    }

    public bool IsInitialized
    {
        get;
    }
    public CheckedPortIoFunctionPointerInvocationStatus Status
    {
        get;
    }
    public PortIoOperationKind OperationKind
    {
        get;
    }
    public ushort Port
    {
        get;
    }
    public byte Value
    {
        get;
    }
    public bool WasInvoked
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == CheckedPortIoFunctionPointerInvocationStatus.Success && WasInvoked && (OperationKind == PortIoOperationKind.Out8 || OperationKind == PortIoOperationKind.In8);

    internal static CheckedPortIoFunctionPointerInvocationResult Pass(PortIoOperationKind operationKind, ushort port, byte value)
    {
        return new CheckedPortIoFunctionPointerInvocationResult(true, CheckedPortIoFunctionPointerInvocationStatus.Success, operationKind, port, value, true);
    }

    internal static CheckedPortIoFunctionPointerInvocationResult Fail(CheckedPortIoFunctionPointerInvocationStatus status, PortIoOperationKind operationKind, ushort port, byte value)
    {
        return new CheckedPortIoFunctionPointerInvocationResult(true, status, operationKind, port, value, false);
    }
}
