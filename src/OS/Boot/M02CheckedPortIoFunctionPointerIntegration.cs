namespace OS.Boot;

public enum M02CheckedPortIoFunctionPointerIntegrationStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    PageFinalizationNotReady = 2,
    SafeExecutionAuditNotReady = 3,
    PortIoAlreadyExecuted = 4,
    PrivilegedStubAlreadyTargeted = 5,
    InvalidPageAddress = 6,
    InvalidPageByteCount = 7,
    InvalidOut8Offset = 8,
    InvalidIn8Offset = 9,
    AddressOverflow = 10,
    BindingRejected = 11
}

public readonly struct M02CheckedPortIoFunctionPointerIntegrationResult
{
    private readonly byte _isInitialized;
    private readonly byte _guestInvocation;
    private readonly byte _portIoExecuted;
    private readonly byte _reserved0;
    private readonly OS.Kernel.Diagnostics
        .CheckedPortIoFunctionPointerBindingResult _binding;

    private M02CheckedPortIoFunctionPointerIntegrationResult(
        M02CheckedPortIoFunctionPointerIntegrationStatus status,
        OS.Kernel.Diagnostics.CheckedPortIoFunctionPointerBindingStatus
            bindingStatus,
        OS.Kernel.Diagnostics.CheckedPortIoFunctionPointerBindingResult
            binding,
        ulong pageAddress,
        ulong out8Address,
        ulong in8Address)
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

    public M02CheckedPortIoFunctionPointerIntegrationStatus Status { get; }

    public OS.Kernel.Diagnostics.CheckedPortIoFunctionPointerBindingStatus
        BindingStatus { get; }

    public ulong PageAddress { get; }

    public ulong Out8Address { get; }

    public ulong In8Address { get; }

    public bool GuestInvocation => _guestInvocation != 0;

    public bool PortIoExecuted => _portIoExecuted != 0;

    public OS.Kernel.Diagnostics.CheckedPortIoFunctionPointerBindingResult
        Binding => _binding;

    public bool Succeeded =>
        IsInitialized &&
        Status == M02CheckedPortIoFunctionPointerIntegrationStatus.Success &&
        BindingStatus ==
            OS.Kernel.Diagnostics.CheckedPortIoFunctionPointerBindingStatus.Ready &&
        _binding.Succeeded &&
        M02SafeStubPageIntegration.IsValidPageAddress(PageAddress) &&
        _binding.PayloadBaseAddress == (nuint)PageAddress &&
        Out8Address == PageAddress + M02SafeStubPageIntegration.Out8Offset &&
        In8Address == PageAddress + M02SafeStubPageIntegration.In8Offset &&
        _binding.Out8StubAddress == (nuint)Out8Address &&
        _binding.In8StubAddress == (nuint)In8Address &&
        !GuestInvocation &&
        !PortIoExecuted;

    internal static M02CheckedPortIoFunctionPointerIntegrationResult Pass(
        OS.Kernel.Diagnostics.CheckedPortIoFunctionPointerBindingResult
            binding,
        ulong pageAddress,
        ulong out8Address,
        ulong in8Address)
    {
        return new M02CheckedPortIoFunctionPointerIntegrationResult(
            M02CheckedPortIoFunctionPointerIntegrationStatus.Success,
            binding.Status,
            binding,
            pageAddress,
            out8Address,
            in8Address);
    }

    internal static M02CheckedPortIoFunctionPointerIntegrationResult Fail(
        M02CheckedPortIoFunctionPointerIntegrationStatus status,
        OS.Kernel.Diagnostics.CheckedPortIoFunctionPointerBindingStatus
            bindingStatus =
                OS.Kernel.Diagnostics
                    .CheckedPortIoFunctionPointerBindingStatus
                    .BindingPlanNotReady)
    {
        return new M02CheckedPortIoFunctionPointerIntegrationResult(
            status,
            bindingStatus,
            default,
            pageAddress: 0,
            out8Address: 0,
            in8Address: 0);
    }
}

public static class M02CheckedPortIoFunctionPointerIntegration
{
    public const bool GuestInvocation = false;
    public const bool PortIoExecuted = false;

    public static M02CheckedPortIoFunctionPointerIntegrationResult Bind(
        ulong pageAddress,
        ulong pageByteCount,
        ulong out8Offset,
        ulong in8Offset,
        bool pageFinalizationAudited,
        bool safeExecutionAudited,
        bool portIoExecuted,
        bool privilegedStubTargeted)
    {
        if (!pageFinalizationAudited)
        {
            return M02CheckedPortIoFunctionPointerIntegrationResult.Fail(
                M02CheckedPortIoFunctionPointerIntegrationStatus
                    .PageFinalizationNotReady);
        }

        if (!safeExecutionAudited)
        {
            return M02CheckedPortIoFunctionPointerIntegrationResult.Fail(
                M02CheckedPortIoFunctionPointerIntegrationStatus
                    .SafeExecutionAuditNotReady);
        }

        if (portIoExecuted)
        {
            return M02CheckedPortIoFunctionPointerIntegrationResult.Fail(
                M02CheckedPortIoFunctionPointerIntegrationStatus
                    .PortIoAlreadyExecuted);
        }

        if (privilegedStubTargeted)
        {
            return M02CheckedPortIoFunctionPointerIntegrationResult.Fail(
                M02CheckedPortIoFunctionPointerIntegrationStatus
                    .PrivilegedStubAlreadyTargeted);
        }

        if (!M02SafeStubPageIntegration.IsValidPageAddress(pageAddress))
        {
            return M02CheckedPortIoFunctionPointerIntegrationResult.Fail(
                M02CheckedPortIoFunctionPointerIntegrationStatus
                    .InvalidPageAddress);
        }

        if (pageByteCount != M02SafeStubPageIntegration.PageSize)
        {
            return M02CheckedPortIoFunctionPointerIntegrationResult.Fail(
                M02CheckedPortIoFunctionPointerIntegrationStatus
                    .InvalidPageByteCount);
        }

        if (out8Offset != M02SafeStubPageIntegration.Out8Offset)
        {
            return M02CheckedPortIoFunctionPointerIntegrationResult.Fail(
                M02CheckedPortIoFunctionPointerIntegrationStatus
                    .InvalidOut8Offset);
        }

        if (in8Offset != M02SafeStubPageIntegration.In8Offset)
        {
            return M02CheckedPortIoFunctionPointerIntegrationResult.Fail(
                M02CheckedPortIoFunctionPointerIntegrationStatus
                    .InvalidIn8Offset);
        }

        if (!TryAdd(pageAddress, pageByteCount, out ulong pageEndAddress) ||
            !TryAdd(pageAddress, out8Offset, out ulong out8Address) ||
            !TryAdd(pageAddress, in8Offset, out ulong in8Address))
        {
            return M02CheckedPortIoFunctionPointerIntegrationResult.Fail(
                M02CheckedPortIoFunctionPointerIntegrationStatus
                    .AddressOverflow);
        }

        OS.Kernel.Diagnostics.CheckedPortIoFunctionPointerBindingRequest request =
            OS.Kernel.Diagnostics
                .CheckedPortIoFunctionPointerBindingRequest
                .Create(
                    sourcePlanSucceeded: true,
                    OS.Kernel.Diagnostics
                        .InvokeIn8Out8CheckedUnmanagedFunctionPointers
                        .RequiredCom1BasePort,
                    (nuint)pageAddress,
                    (nuint)pageEndAddress,
                    (nuint)pageByteCount,
                    (nuint)out8Offset,
                    (nuint)in8Offset,
                    (nuint)out8Address,
                    (nuint)in8Address,
                    OS.Kernel.Diagnostics.PortIoStubPlan.Out8RequiredByteLength,
                    OS.Kernel.Diagnostics.PortIoStubPlan.In8RequiredByteLength,
                    OS.Kernel.Diagnostics.PortIoStubPlan.Out8RequiredByteLength,
                    OS.Kernel.Diagnostics.PortIoStubPlan.In8RequiredByteLength);
        OS.Kernel.Diagnostics.CheckedPortIoFunctionPointerBindingResult binding =
            OS.Kernel.Diagnostics
                .InvokeIn8Out8CheckedUnmanagedFunctionPointers
                .Bind(request);

        if (!binding.Succeeded)
        {
            return M02CheckedPortIoFunctionPointerIntegrationResult.Fail(
                M02CheckedPortIoFunctionPointerIntegrationStatus
                    .BindingRejected,
                binding.Status);
        }

        return M02CheckedPortIoFunctionPointerIntegrationResult.Pass(
            binding,
            pageAddress,
            (ulong)binding.Out8StubAddress,
            (ulong)binding.In8StubAddress);
    }

    private static bool TryAdd(ulong value, ulong addend, out ulong result)
    {
        result = value + addend;
        return result >= value;
    }
}
