using OS.Kernel.Diagnostics;
using OS.Std.Memory;

namespace OS.Kernel.Runtime;
public readonly struct ExecutableStubBundleMaterializerResult
{
    private ExecutableStubBundleMaterializerResult(bool isInitialized, ExecutableStubBundleMaterializerStatus status, ExecutableStubBundlePlanStatus bundlePlanStatus, ExecutableStubMemoryPlanStatus memoryPlanStatus, nuint bytesCopied, int com1Out8BytesCopied, int com1In8BytesCopied, int installIdtBytesCopied, int installGdtBytesCopied, int loadTaskRegisterBytesCopied, int invalidOpcodeProbeBytesCopied, int nullWriteProbeBytesCopied)
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
        const int totalBytesCopied = com1Out8BytesCopied + com1In8BytesCopied + installIdtBytesCopied + installGdtBytesCopied + loadTaskRegisterBytesCopied + invalidOpcodeProbeBytesCopied + nullWriteProbeBytesCopied;
        return new ExecutableStubBundleMaterializerResult(true, ExecutableStubBundleMaterializerStatus.Success, ExecutableStubBundlePlanStatus.Success, ExecutableStubMemoryPlanStatus.Success, (nuint)totalBytesCopied, com1Out8BytesCopied, com1In8BytesCopied, installIdtBytesCopied, installGdtBytesCopied, loadTaskRegisterBytesCopied, invalidOpcodeProbeBytesCopied, nullWriteProbeBytesCopied);
    }

    public static ExecutableStubBundleMaterializerResult Fail(ExecutableStubBundleMaterializerStatus status, ExecutableStubBundlePlanStatus bundlePlanStatus, ExecutableStubMemoryPlanStatus memoryPlanStatus)
    {
        return new ExecutableStubBundleMaterializerResult(true, status, bundlePlanStatus, memoryPlanStatus, 0, 0, 0, 0, 0, 0, 0, 0);
    }
}
