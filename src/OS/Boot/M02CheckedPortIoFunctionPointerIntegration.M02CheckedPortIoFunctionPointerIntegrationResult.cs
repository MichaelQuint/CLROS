namespace OS.Boot;

public readonly struct M02CheckedPortIoFunctionPointerIntegrationResult
{
    private readonly byte _isInitialized;
    private readonly byte _guestInvocation;
    private readonly byte _portIoExecuted;
    private readonly byte _reserved0;
    private readonly OS.Kernel.Diagnostics.CheckedPortIoFunctionPointerBindingResult _binding;
    private M02CheckedPortIoFunctionPointerIntegrationResult(M02CheckedPortIoFunctionPointerIntegrationStatus status, OS.Kernel.Diagnostics.CheckedPortIoFunctionPointerBindingStatus bindingStatus, OS.Kernel.Diagnostics.CheckedPortIoFunctionPointerBindingResult binding, ulong pageAddress, ulong out8Address, ulong in8Address)
    {
        _isInitialized = 1;
        _guestInvocation = 0;
        _portIoExecuted = 0;
        _reserved0 = 0;
        _binding = binding;
        Status = status;
        BindingStatus = bindingStatus;
        PageAddress = pageAddress;
        Out8Address = out8Address;
        In8Address = in8Address;
    }

    public bool IsInitialized => _isInitialized == 1 && _reserved0 == 0;
    public M02CheckedPortIoFunctionPointerIntegrationStatus Status
    {
        get;
    }
    public OS.Kernel.Diagnostics.CheckedPortIoFunctionPointerBindingStatus BindingStatus
    {
        get;
    }
    public ulong PageAddress
    {
        get;
    }
    public ulong Out8Address
    {
        get;
    }
    public ulong In8Address
    {
        get;
    }
    public bool GuestInvocation => _guestInvocation != 0;
    public bool PortIoExecuted => _portIoExecuted != 0;
    public OS.Kernel.Diagnostics.CheckedPortIoFunctionPointerBindingResult Binding => _binding;
    public bool Succeeded => IsInitialized && Status == M02CheckedPortIoFunctionPointerIntegrationStatus.Success && BindingStatus == OS.Kernel.Diagnostics.CheckedPortIoFunctionPointerBindingStatus.Ready && _binding.Succeeded && M02SafeStubPageIntegration.IsValidPageAddress(PageAddress) && _binding.PayloadBaseAddress == (nuint)PageAddress && Out8Address == PageAddress + M02SafeStubPageIntegration.Out8Offset && In8Address == PageAddress + M02SafeStubPageIntegration.In8Offset && _binding.Out8StubAddress == (nuint)Out8Address && _binding.In8StubAddress == (nuint)In8Address && !GuestInvocation && !PortIoExecuted;

    internal static M02CheckedPortIoFunctionPointerIntegrationResult Pass(OS.Kernel.Diagnostics.CheckedPortIoFunctionPointerBindingResult binding, ulong pageAddress, ulong out8Address, ulong in8Address)
    {
        return new M02CheckedPortIoFunctionPointerIntegrationResult(M02CheckedPortIoFunctionPointerIntegrationStatus.Success, binding.Status, binding, pageAddress, out8Address, in8Address);
    }

    internal static M02CheckedPortIoFunctionPointerIntegrationResult Fail(M02CheckedPortIoFunctionPointerIntegrationStatus status, OS.Kernel.Diagnostics.CheckedPortIoFunctionPointerBindingStatus bindingStatus = OS.Kernel.Diagnostics.CheckedPortIoFunctionPointerBindingStatus.BindingPlanNotReady)
    {
        return new M02CheckedPortIoFunctionPointerIntegrationResult(status, bindingStatus, default, pageAddress: 0, out8Address: 0, in8Address: 0);
    }
}
