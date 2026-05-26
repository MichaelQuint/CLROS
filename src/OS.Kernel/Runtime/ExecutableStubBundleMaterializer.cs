using OS.Kernel.Diagnostics;
using OS.Std.Memory;

namespace OS.Kernel.Runtime;

public enum ExecutableStubBundleMaterializerStatus
{
    Success = 0,
    InvalidBundlePlan = 1,
    InvalidMemoryPlan = 2,
    InconsistentMemoryPlan = 3,
    DestinationTooSmall = 4,
    MissingSlot = 5,
    SourceTooSmall = 6,
    SlotOutOfBounds = 7
}

public readonly struct ExecutableStubBundleMaterializerResult
{
    private ExecutableStubBundleMaterializerResult(
        bool isInitialized,
        ExecutableStubBundleMaterializerStatus status,
        ExecutableStubBundlePlanStatus bundlePlanStatus,
        ExecutableStubMemoryPlanStatus memoryPlanStatus,
        nuint bytesCopied,
        int com1Out8BytesCopied,
        int com1In8BytesCopied,
        int installIdtBytesCopied,
        int installGdtBytesCopied,
        int loadTaskRegisterBytesCopied,
        int invalidOpcodeProbeBytesCopied,
        int nullWriteProbeBytesCopied)
    {
        IsInitialized = isInitialized;
        Status = status;
        BundlePlanStatus = bundlePlanStatus;
        MemoryPlanStatus = memoryPlanStatus;
        BytesCopied = bytesCopied;
        Com1Out8BytesCopied = com1Out8BytesCopied;
        Com1In8BytesCopied = com1In8BytesCopied;
        InstallIdtBytesCopied = installIdtBytesCopied;
        InstallGdtBytesCopied = installGdtBytesCopied;
        LoadTaskRegisterBytesCopied = loadTaskRegisterBytesCopied;
        InvalidOpcodeProbeBytesCopied = invalidOpcodeProbeBytesCopied;
        NullWriteProbeBytesCopied = nullWriteProbeBytesCopied;
    }

    public bool IsInitialized { get; }

    public ExecutableStubBundleMaterializerStatus Status { get; }

    public ExecutableStubBundlePlanStatus BundlePlanStatus { get; }

    public ExecutableStubMemoryPlanStatus MemoryPlanStatus { get; }

    public bool Succeeded => IsInitialized && Status == ExecutableStubBundleMaterializerStatus.Success;

    public nuint BytesCopied { get; }

    public int Com1Out8BytesCopied { get; }

    public int Com1In8BytesCopied { get; }

    public int InstallIdtBytesCopied { get; }

    public int InstallGdtBytesCopied { get; }

    public int LoadTaskRegisterBytesCopied { get; }

    public int InvalidOpcodeProbeBytesCopied { get; }

    public int NullWriteProbeBytesCopied { get; }

    public static ExecutableStubBundleMaterializerResult Success()
    {
        const int com1Out8BytesCopied = PortIoStubPlan.Out8RequiredByteLength;
        const int com1In8BytesCopied = PortIoStubPlan.In8RequiredByteLength;
        const int installIdtBytesCopied = DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength;
        const int installGdtBytesCopied = DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength;
        const int loadTaskRegisterBytesCopied = DescriptorTableInstallStubPlan.LtrStubRequiredByteLength;
        const int invalidOpcodeProbeBytesCopied = FaultProbeStubPlan.InvalidOpcodeRequiredByteLength;
        const int nullWriteProbeBytesCopied = FaultProbeStubPlan.NullWriteRequiredByteLength;

        const int totalBytesCopied =
            com1Out8BytesCopied +
            com1In8BytesCopied +
            installIdtBytesCopied +
            installGdtBytesCopied +
            loadTaskRegisterBytesCopied +
            invalidOpcodeProbeBytesCopied +
            nullWriteProbeBytesCopied;

        return new ExecutableStubBundleMaterializerResult(
            true,
            ExecutableStubBundleMaterializerStatus.Success,
            ExecutableStubBundlePlanStatus.Success,
            ExecutableStubMemoryPlanStatus.Success,
            (nuint)totalBytesCopied,
            com1Out8BytesCopied,
            com1In8BytesCopied,
            installIdtBytesCopied,
            installGdtBytesCopied,
            loadTaskRegisterBytesCopied,
            invalidOpcodeProbeBytesCopied,
            nullWriteProbeBytesCopied);
    }

    public static ExecutableStubBundleMaterializerResult Fail(
        ExecutableStubBundleMaterializerStatus status,
        ExecutableStubBundlePlanStatus bundlePlanStatus,
        ExecutableStubMemoryPlanStatus memoryPlanStatus)
    {
        return new ExecutableStubBundleMaterializerResult(
            true,
            status,
            bundlePlanStatus,
            memoryPlanStatus,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0);
    }
}

public readonly ref struct ExecutableStubBundleMaterializerSources
{
    public ExecutableStubBundleMaterializerSources(
        ReadOnlySpan<byte> com1Out8,
        ReadOnlySpan<byte> com1In8,
        ReadOnlySpan<byte> installIdt,
        ReadOnlySpan<byte> installGdt,
        ReadOnlySpan<byte> loadTaskRegister,
        ReadOnlySpan<byte> invalidOpcodeProbe,
        ReadOnlySpan<byte> nullWriteProbe)
    {
        Com1Out8 = com1Out8;
        Com1In8 = com1In8;
        InstallIdt = installIdt;
        InstallGdt = installGdt;
        LoadTaskRegister = loadTaskRegister;
        InvalidOpcodeProbe = invalidOpcodeProbe;
        NullWriteProbe = nullWriteProbe;
    }

    public ReadOnlySpan<byte> Com1Out8 { get; }

    public ReadOnlySpan<byte> Com1In8 { get; }

    public ReadOnlySpan<byte> InstallIdt { get; }

    public ReadOnlySpan<byte> InstallGdt { get; }

    public ReadOnlySpan<byte> LoadTaskRegister { get; }

    public ReadOnlySpan<byte> InvalidOpcodeProbe { get; }

    public ReadOnlySpan<byte> NullWriteProbe { get; }
}

public static class ExecutableStubBundleMaterializer
{
    public static ExecutableStubBundleMaterializerResult MaterializeToMemoryRegion(
        ExecutableStubMemoryPlanResult memoryPlan,
        ExecutableStubBundlePlanResult bundlePlan,
        ExecutableStubBundleMaterializerSources sources,
        Span<byte> destination)
    {
        if (!bundlePlan.Succeeded)
        {
            return ExecutableStubBundleMaterializerResult.Fail(
                ExecutableStubBundleMaterializerStatus.InvalidBundlePlan,
                bundlePlan.Status,
                memoryPlan.Status);
        }

        if (!memoryPlan.Succeeded)
        {
            return ExecutableStubBundleMaterializerResult.Fail(
                ExecutableStubBundleMaterializerStatus.InvalidMemoryPlan,
                bundlePlan.Status,
                memoryPlan.Status);
        }

        if (memoryPlan.PayloadOffset > memoryPlan.RequiredByteLength ||
            bundlePlan.RequiredMemoryBytes > memoryPlan.RequiredByteLength)
        {
            return ExecutableStubBundleMaterializerResult.Fail(
                ExecutableStubBundleMaterializerStatus.InconsistentMemoryPlan,
                bundlePlan.Status,
                memoryPlan.Status);
        }

        if ((nuint)destination.Length < memoryPlan.RequiredByteLength)
        {
            return ExecutableStubBundleMaterializerResult.Fail(
                ExecutableStubBundleMaterializerStatus.DestinationTooSmall,
                bundlePlan.Status,
                memoryPlan.Status);
        }

        return MaterializeCore(
            bundlePlan,
            sources,
            destination,
            memoryPlan.PayloadOffset,
            memoryPlan.Status);
    }

    public static ExecutableStubBundleMaterializerResult MaterializeToPayload(
        ExecutableStubBundlePlanResult bundlePlan,
        ExecutableStubBundleMaterializerSources sources,
        Span<byte> payloadDestination)
    {
        if (!bundlePlan.Succeeded)
        {
            return ExecutableStubBundleMaterializerResult.Fail(
                ExecutableStubBundleMaterializerStatus.InvalidBundlePlan,
                bundlePlan.Status,
                ExecutableStubMemoryPlanStatus.Success);
        }

        if ((nuint)payloadDestination.Length < bundlePlan.TotalPayloadLength)
        {
            return ExecutableStubBundleMaterializerResult.Fail(
                ExecutableStubBundleMaterializerStatus.DestinationTooSmall,
                bundlePlan.Status,
                ExecutableStubMemoryPlanStatus.Success);
        }

        return MaterializeCore(
            bundlePlan,
            sources,
            payloadDestination,
            0,
            ExecutableStubMemoryPlanStatus.Success);
    }

    private static ExecutableStubBundleMaterializerResult MaterializeCore(
        ExecutableStubBundlePlanResult bundlePlan,
        ExecutableStubBundleMaterializerSources sources,
        Span<byte> destination,
        nuint destinationPayloadBaseOffset,
        ExecutableStubMemoryPlanStatus memoryPlanStatus)
    {
        ExecutableStubBundleMaterializerStatus validationStatus = ValidateStableDiagnosticBundle(
            bundlePlan,
            sources,
            destination,
            destinationPayloadBaseOffset);
        if (validationStatus != ExecutableStubBundleMaterializerStatus.Success)
        {
            return ExecutableStubBundleMaterializerResult.Fail(
                validationStatus,
                bundlePlan.Status,
                memoryPlanStatus);
        }

        ExecutableStubBundleMaterializerStatus copyStatus = CopySlot(
            sources.Com1Out8,
            PortIoStubPlan.Out8RequiredByteLength,
            bundlePlan.Com1Out8Offset,
            destination,
            destinationPayloadBaseOffset);
        if (copyStatus != ExecutableStubBundleMaterializerStatus.Success)
        {
            return ExecutableStubBundleMaterializerResult.Fail(
                copyStatus,
                bundlePlan.Status,
                memoryPlanStatus);
        }

        copyStatus = CopySlot(
            sources.Com1In8,
            PortIoStubPlan.In8RequiredByteLength,
            bundlePlan.Com1In8Offset,
            destination,
            destinationPayloadBaseOffset);
        if (copyStatus != ExecutableStubBundleMaterializerStatus.Success)
        {
            return ExecutableStubBundleMaterializerResult.Fail(
                copyStatus,
                bundlePlan.Status,
                memoryPlanStatus);
        }

        copyStatus = CopySlot(
            sources.InstallIdt,
            DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength,
            bundlePlan.InstallIdtOffset,
            destination,
            destinationPayloadBaseOffset);
        if (copyStatus != ExecutableStubBundleMaterializerStatus.Success)
        {
            return ExecutableStubBundleMaterializerResult.Fail(
                copyStatus,
                bundlePlan.Status,
                memoryPlanStatus);
        }

        copyStatus = CopySlot(
            sources.InstallGdt,
            DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength,
            bundlePlan.InstallGdtOffset,
            destination,
            destinationPayloadBaseOffset);
        if (copyStatus != ExecutableStubBundleMaterializerStatus.Success)
        {
            return ExecutableStubBundleMaterializerResult.Fail(
                copyStatus,
                bundlePlan.Status,
                memoryPlanStatus);
        }

        copyStatus = CopySlot(
            sources.LoadTaskRegister,
            DescriptorTableInstallStubPlan.LtrStubRequiredByteLength,
            bundlePlan.LoadTaskRegisterOffset,
            destination,
            destinationPayloadBaseOffset);
        if (copyStatus != ExecutableStubBundleMaterializerStatus.Success)
        {
            return ExecutableStubBundleMaterializerResult.Fail(
                copyStatus,
                bundlePlan.Status,
                memoryPlanStatus);
        }

        copyStatus = CopySlot(
            sources.InvalidOpcodeProbe,
            FaultProbeStubPlan.InvalidOpcodeRequiredByteLength,
            bundlePlan.InvalidOpcodeProbeOffset,
            destination,
            destinationPayloadBaseOffset);
        if (copyStatus != ExecutableStubBundleMaterializerStatus.Success)
        {
            return ExecutableStubBundleMaterializerResult.Fail(
                copyStatus,
                bundlePlan.Status,
                memoryPlanStatus);
        }

        copyStatus = CopySlot(
            sources.NullWriteProbe,
            FaultProbeStubPlan.NullWriteRequiredByteLength,
            bundlePlan.NullWriteProbeOffset,
            destination,
            destinationPayloadBaseOffset);
        if (copyStatus != ExecutableStubBundleMaterializerStatus.Success)
        {
            return ExecutableStubBundleMaterializerResult.Fail(
                copyStatus,
                bundlePlan.Status,
                memoryPlanStatus);
        }

        return ExecutableStubBundleMaterializerResult.Success();
    }

    private static ExecutableStubBundleMaterializerStatus ValidateStableDiagnosticBundle(
        ExecutableStubBundlePlanResult bundlePlan,
        ExecutableStubBundleMaterializerSources sources,
        Span<byte> destination,
        nuint destinationPayloadBaseOffset)
    {
        if (bundlePlan.SlotCount != ExecutableStubBundlePlan.StableSlotCount)
        {
            return ExecutableStubBundleMaterializerStatus.MissingSlot;
        }

        ExecutableStubBundleMaterializerStatus status = ValidateSlot(
            bundlePlan,
            ExecutableStubSlotKind.Com1Out8,
            sources.Com1Out8,
            PortIoStubPlan.Out8RequiredByteLength,
            destination,
            destinationPayloadBaseOffset);
        if (status != ExecutableStubBundleMaterializerStatus.Success)
        {
            return status;
        }

        status = ValidateSlot(
            bundlePlan,
            ExecutableStubSlotKind.Com1In8,
            sources.Com1In8,
            PortIoStubPlan.In8RequiredByteLength,
            destination,
            destinationPayloadBaseOffset);
        if (status != ExecutableStubBundleMaterializerStatus.Success)
        {
            return status;
        }

        status = ValidateSlot(
            bundlePlan,
            ExecutableStubSlotKind.InstallIdt,
            sources.InstallIdt,
            DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength,
            destination,
            destinationPayloadBaseOffset);
        if (status != ExecutableStubBundleMaterializerStatus.Success)
        {
            return status;
        }

        status = ValidateSlot(
            bundlePlan,
            ExecutableStubSlotKind.InstallGdt,
            sources.InstallGdt,
            DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength,
            destination,
            destinationPayloadBaseOffset);
        if (status != ExecutableStubBundleMaterializerStatus.Success)
        {
            return status;
        }

        status = ValidateSlot(
            bundlePlan,
            ExecutableStubSlotKind.LoadTaskRegister,
            sources.LoadTaskRegister,
            DescriptorTableInstallStubPlan.LtrStubRequiredByteLength,
            destination,
            destinationPayloadBaseOffset);
        if (status != ExecutableStubBundleMaterializerStatus.Success)
        {
            return status;
        }

        status = ValidateSlot(
            bundlePlan,
            ExecutableStubSlotKind.InvalidOpcodeProbe,
            sources.InvalidOpcodeProbe,
            FaultProbeStubPlan.InvalidOpcodeRequiredByteLength,
            destination,
            destinationPayloadBaseOffset);
        if (status != ExecutableStubBundleMaterializerStatus.Success)
        {
            return status;
        }

        return ValidateSlot(
            bundlePlan,
            ExecutableStubSlotKind.NullWriteProbe,
            sources.NullWriteProbe,
            FaultProbeStubPlan.NullWriteRequiredByteLength,
            destination,
            destinationPayloadBaseOffset);
    }

    private static ExecutableStubBundleMaterializerStatus ValidateSlot(
        ExecutableStubBundlePlanResult bundlePlan,
        ExecutableStubSlotKind kind,
        ReadOnlySpan<byte> source,
        int requiredBytes,
        Span<byte> destination,
        nuint destinationPayloadBaseOffset)
    {
        if (!bundlePlan.TryGetSlotOffset(kind, out nuint payloadOffset))
        {
            return ExecutableStubBundleMaterializerStatus.MissingSlot;
        }

        if (source.Length < requiredBytes)
        {
            return ExecutableStubBundleMaterializerStatus.SourceTooSmall;
        }

        nuint payloadEnd = payloadOffset + (nuint)requiredBytes;
        if (payloadEnd < payloadOffset || payloadEnd > bundlePlan.TotalPayloadLength)
        {
            return ExecutableStubBundleMaterializerStatus.SlotOutOfBounds;
        }

        nuint destinationStart = destinationPayloadBaseOffset + payloadOffset;
        if (destinationStart < destinationPayloadBaseOffset)
        {
            return ExecutableStubBundleMaterializerStatus.SlotOutOfBounds;
        }

        nuint destinationEnd = destinationStart + (nuint)requiredBytes;
        if (destinationEnd < destinationStart || destinationEnd > (nuint)destination.Length)
        {
            return ExecutableStubBundleMaterializerStatus.SlotOutOfBounds;
        }

        return ExecutableStubBundleMaterializerStatus.Success;
    }

    private static ExecutableStubBundleMaterializerStatus CopySlot(
        ReadOnlySpan<byte> source,
        int requiredBytes,
        nuint payloadOffset,
        Span<byte> destination,
        nuint destinationPayloadBaseOffset)
    {
        int destinationStart = (int)(destinationPayloadBaseOffset + payloadOffset);
        BufferOperationResult copy = BufferOperations.MemoryCopy(
            source.Slice(0, requiredBytes),
            destination.Slice(destinationStart, requiredBytes));
        return copy.Succeeded
            ? ExecutableStubBundleMaterializerStatus.Success
            : ExecutableStubBundleMaterializerStatus.DestinationTooSmall;
    }
}
