#if !NOSTDLIB_EFI
using OS.Kernel.Runtime;

#endif
namespace OS.Kernel.Diagnostics;
public readonly unsafe struct CheckedPortIoFunctionPointerBindingResult
{
    private readonly delegate* unmanaged<ushort, byte, void> _out8;
    private readonly delegate* unmanaged<ushort, byte> _in8;
    private CheckedPortIoFunctionPointerBindingResult(bool isInitialized, CheckedPortIoFunctionPointerBindingStatus status, ushort configuredBasePort, ushort configuredEndPort, nuint payloadBaseAddress, nuint payloadEndAddress, nuint out8StubAddress, nuint in8StubAddress, delegate* unmanaged<ushort, byte, void> out8, delegate* unmanaged<ushort, byte> in8)
    {
        IsInitialized = isInitialized;
        Status = status;
        ConfiguredBasePort = configuredBasePort;
        ConfiguredEndPort = configuredEndPort;
        PayloadBaseAddress = payloadBaseAddress;
        PayloadEndAddress = payloadEndAddress;
        Out8StubAddress = out8StubAddress;
        In8StubAddress = in8StubAddress;
        _out8 = out8;
        _in8 = in8;
    }

    public bool IsInitialized
    {
        get;
    }
    public CheckedPortIoFunctionPointerBindingStatus Status
    {
        get;
    }
    public ushort ConfiguredBasePort
    {
        get;
    }
    public ushort ConfiguredEndPort
    {
        get;
    }
    public nuint PayloadBaseAddress
    {
        get;
    }
    public nuint PayloadEndAddress
    {
        get;
    }
    public nuint Out8StubAddress
    {
        get;
    }
    public nuint In8StubAddress
    {
        get;
    }
    public bool CreatesManagedDelegate => false;
    public bool Succeeded => IsInitialized && Status == CheckedPortIoFunctionPointerBindingStatus.Ready && ConfiguredBasePort == InvokeIn8Out8CheckedUnmanagedFunctionPointers.RequiredCom1BasePort && ConfiguredEndPort >= ConfiguredBasePort && PayloadBaseAddress != 0 && PayloadEndAddress > PayloadBaseAddress && Out8StubAddress != 0 && In8StubAddress != 0 && _out8 != null && _in8 != null && !CreatesManagedDelegate;

    public CheckedPortIoFunctionPointerInvocationResult InvokeOut8(ushort port, byte value)
    {
        if (!Succeeded)
        {
            return CheckedPortIoFunctionPointerInvocationResult.Fail(CheckedPortIoFunctionPointerInvocationStatus.BindingNotReady, PortIoOperationKind.Out8, port, value);
        }

        if (!IsConfiguredPort(port))
        {
            return CheckedPortIoFunctionPointerInvocationResult.Fail(CheckedPortIoFunctionPointerInvocationStatus.PortOutsideConfiguredRange, PortIoOperationKind.Out8, port, value);
        }

        _out8(port, value);
        return CheckedPortIoFunctionPointerInvocationResult.Pass(PortIoOperationKind.Out8, port, value);
    }

    public CheckedPortIoFunctionPointerInvocationResult InvokeIn8(ushort port)
    {
        if (!Succeeded)
        {
            return CheckedPortIoFunctionPointerInvocationResult.Fail(CheckedPortIoFunctionPointerInvocationStatus.BindingNotReady, PortIoOperationKind.In8, port, 0);
        }

        if (!IsConfiguredPort(port))
        {
            return CheckedPortIoFunctionPointerInvocationResult.Fail(CheckedPortIoFunctionPointerInvocationStatus.PortOutsideConfiguredRange, PortIoOperationKind.In8, port, 0);
        }

        byte value = _in8(port);
        return CheckedPortIoFunctionPointerInvocationResult.Pass(PortIoOperationKind.In8, port, value);
    }

    internal static CheckedPortIoFunctionPointerBindingResult Ready(CheckedPortIoFunctionPointerBindingRequest request, ushort configuredEndPort, delegate* unmanaged<ushort, byte, void> out8, delegate* unmanaged<ushort, byte> in8)
    {
        return new CheckedPortIoFunctionPointerBindingResult(true, CheckedPortIoFunctionPointerBindingStatus.Ready, request.ConfiguredBasePort, configuredEndPort, request.PayloadBaseAddress, request.PayloadEndAddress, request.Out8StubAddress, request.In8StubAddress, out8, in8);
    }

    internal static CheckedPortIoFunctionPointerBindingResult Fail(CheckedPortIoFunctionPointerBindingStatus status, CheckedPortIoFunctionPointerBindingRequest request, ushort configuredEndPort)
    {
        return new CheckedPortIoFunctionPointerBindingResult(true, status, request.ConfiguredBasePort, configuredEndPort, request.PayloadBaseAddress, request.PayloadEndAddress, request.Out8StubAddress, request.In8StubAddress, null, null);
    }

    private bool IsConfiguredPort(ushort port)
    {
        return port >= ConfiguredBasePort && port <= ConfiguredEndPort;
    }
}
