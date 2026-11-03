namespace OS.Kernel.Diagnostics;

public readonly struct Com1OwnedPortIoOperationResult
{
    private Com1OwnedPortIoOperationResult(bool isInitialized, Com1OwnedPortIoOperationStatus status, CheckedPortIoFunctionPointerBindingStatus bindingStatus, CheckedPortIoFunctionPointerInvocationStatus invocationStatus, PortIoOperationKind operationKind, ushort port, byte value, bool wasInvoked)
    {
        IsInitialized = isInitialized;
        Status = status;
        BindingStatus = bindingStatus;
        InvocationStatus = invocationStatus;
        OperationKind = operationKind;
        Port = port;
        Value = value;
        WasInvoked = wasInvoked;
    }

    public bool IsInitialized
    {
        get;
    }
    public Com1OwnedPortIoOperationStatus Status
    {
        get;
    }
    public CheckedPortIoFunctionPointerBindingStatus BindingStatus
    {
        get;
    }
    public CheckedPortIoFunctionPointerInvocationStatus InvocationStatus
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
    public bool Succeeded => IsInitialized && Status == Com1OwnedPortIoOperationStatus.Success && BindingStatus == CheckedPortIoFunctionPointerBindingStatus.Ready && InvocationStatus == CheckedPortIoFunctionPointerInvocationStatus.Success && (OperationKind == PortIoOperationKind.Out8 || OperationKind == PortIoOperationKind.In8) && WasInvoked;

    internal static Com1OwnedPortIoOperationResult Pass(CheckedPortIoFunctionPointerInvocationResult invocation)
    {
        return new Com1OwnedPortIoOperationResult(true, Com1OwnedPortIoOperationStatus.Success, CheckedPortIoFunctionPointerBindingStatus.Ready, invocation.Status, invocation.OperationKind, invocation.Port, invocation.Value, invocation.WasInvoked);
    }

    internal static Com1OwnedPortIoOperationResult Fail(Com1OwnedPortIoOperationStatus status, CheckedPortIoFunctionPointerBindingStatus bindingStatus, CheckedPortIoFunctionPointerInvocationStatus invocationStatus, PortIoOperationKind operationKind, ushort port, byte value)
    {
        return new Com1OwnedPortIoOperationResult(true, status, bindingStatus, invocationStatus, operationKind, port, value, false);
    }
}
#if !NOSTDLIB_EFI
#endif
