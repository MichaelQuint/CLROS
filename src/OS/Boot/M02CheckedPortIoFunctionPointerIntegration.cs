namespace OS.Boot;

public static class M02CheckedPortIoFunctionPointerIntegration
{
    public const bool GuestInvocation = false;
    public const bool PortIoExecuted = false;
    public static M02CheckedPortIoFunctionPointerIntegrationResult Bind(ulong pageAddress, ulong pageByteCount, ulong out8Offset, ulong in8Offset, bool pageFinalizationAudited, bool safeExecutionAudited, bool portIoExecuted, bool privilegedStubTargeted)
    {
        if (!pageFinalizationAudited)
        {
            return M02CheckedPortIoFunctionPointerIntegrationResult.Fail(M02CheckedPortIoFunctionPointerIntegrationStatus.PageFinalizationNotReady);
        }

        if (!safeExecutionAudited)
        {
            return M02CheckedPortIoFunctionPointerIntegrationResult.Fail(M02CheckedPortIoFunctionPointerIntegrationStatus.SafeExecutionAuditNotReady);
        }

        if (portIoExecuted)
        {
            return M02CheckedPortIoFunctionPointerIntegrationResult.Fail(M02CheckedPortIoFunctionPointerIntegrationStatus.PortIoAlreadyExecuted);
        }

        if (privilegedStubTargeted)
        {
            return M02CheckedPortIoFunctionPointerIntegrationResult.Fail(M02CheckedPortIoFunctionPointerIntegrationStatus.PrivilegedStubAlreadyTargeted);
        }

        if (!M02SafeStubPageIntegration.IsValidPageAddress(pageAddress))
        {
            return M02CheckedPortIoFunctionPointerIntegrationResult.Fail(M02CheckedPortIoFunctionPointerIntegrationStatus.InvalidPageAddress);
        }

        if (pageByteCount != M02SafeStubPageIntegration.PageSize)
        {
            return M02CheckedPortIoFunctionPointerIntegrationResult.Fail(M02CheckedPortIoFunctionPointerIntegrationStatus.InvalidPageByteCount);
        }

        if (out8Offset != M02SafeStubPageIntegration.Out8Offset)
        {
            return M02CheckedPortIoFunctionPointerIntegrationResult.Fail(M02CheckedPortIoFunctionPointerIntegrationStatus.InvalidOut8Offset);
        }

        if (in8Offset != M02SafeStubPageIntegration.In8Offset)
        {
            return M02CheckedPortIoFunctionPointerIntegrationResult.Fail(M02CheckedPortIoFunctionPointerIntegrationStatus.InvalidIn8Offset);
        }

        if (!TryAdd(pageAddress, pageByteCount, out ulong pageEndAddress) || !TryAdd(pageAddress, out8Offset, out ulong out8Address) || !TryAdd(pageAddress, in8Offset, out ulong in8Address))
        {
            return M02CheckedPortIoFunctionPointerIntegrationResult.Fail(M02CheckedPortIoFunctionPointerIntegrationStatus.AddressOverflow);
        }

        OS.Kernel.Diagnostics.CheckedPortIoFunctionPointerBindingRequest request = OS.Kernel.Diagnostics.CheckedPortIoFunctionPointerBindingRequest.Create(sourcePlanSucceeded: true, OS.Kernel.Diagnostics.InvokeIn8Out8CheckedUnmanagedFunctionPointers.RequiredCom1BasePort, (nuint)pageAddress, (nuint)pageEndAddress, (nuint)pageByteCount, (nuint)out8Offset, (nuint)in8Offset, (nuint)out8Address, (nuint)in8Address, OS.Kernel.Diagnostics.PortIoStubPlan.Out8RequiredByteLength, OS.Kernel.Diagnostics.PortIoStubPlan.In8RequiredByteLength, OS.Kernel.Diagnostics.PortIoStubPlan.Out8RequiredByteLength, OS.Kernel.Diagnostics.PortIoStubPlan.In8RequiredByteLength);
        OS.Kernel.Diagnostics.CheckedPortIoFunctionPointerBindingResult binding = OS.Kernel.Diagnostics.InvokeIn8Out8CheckedUnmanagedFunctionPointers.Bind(request);
        if (!binding.Succeeded)
        {
            return M02CheckedPortIoFunctionPointerIntegrationResult.Fail(M02CheckedPortIoFunctionPointerIntegrationStatus.BindingRejected, binding.Status);
        }

        return M02CheckedPortIoFunctionPointerIntegrationResult.Pass(binding, pageAddress, (ulong)binding.Out8StubAddress, (ulong)binding.In8StubAddress);
    }

    private static bool TryAdd(ulong value, ulong addend, out ulong result)
    {
        result = value + addend;
        return result >= value;
    }
}
