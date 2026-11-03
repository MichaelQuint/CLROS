using OS.Kernel.Runtime;

namespace OS.Kernel.Diagnostics;

public static class FaultProbeReadiness
{
    public const string OperationName = "fault.probe.ready";
    public const int RequiredPayloadBytes = FaultProbeStubPlan.InvalidOpcodeRequiredByteLength + FaultProbeStubPlan.NullWriteRequiredByteLength;
    public static FaultProbeReadinessResult RequireReady(Span<byte> invalidOpcodeStubBuffer, Span<byte> nullWriteStubBuffer, ExecutableStubMemoryPlanResult memoryPlanResult)
    {
        return RequireReady(invalidOpcodeEnabledByDefault: Probes.GetInvalidOpcodePanicProbe().EnabledByDefault, nullWriteEnabledByDefault: Probes.GetNullPointerPanicProbe().EnabledByDefault, invalidOpcodeStubBuffer, nullWriteStubBuffer, memoryPlanResult);
    }

    public static FaultProbeReadinessResult RequireReady(bool invalidOpcodeEnabledByDefault, bool nullWriteEnabledByDefault, Span<byte> invalidOpcodeStubBuffer, Span<byte> nullWriteStubBuffer, ExecutableStubMemoryPlanResult memoryPlanResult)
    {
        if (invalidOpcodeEnabledByDefault || nullWriteEnabledByDefault)
        {
            return FaultProbeReadinessResult.Fail(FaultProbeReadinessStatus.ProbeEnabledByDefault);
        }

        FaultProbeStubPlanResult invalidOpcodeStubResult = FaultProbeStubPlan.InvalidOpcode.Emit(invalidOpcodeStubBuffer);
        if (!invalidOpcodeStubResult.Succeeded)
        {
            return FaultProbeReadinessResult.Fail(FaultProbeReadinessStatus.InvalidOpcodeStubFailure, invalidOpcodeStubResult.Status);
        }

        FaultProbeStubPlanResult nullWriteStubResult = FaultProbeStubPlan.NullWrite.Emit(nullWriteStubBuffer);
        if (!nullWriteStubResult.Succeeded)
        {
            return FaultProbeReadinessResult.Fail(FaultProbeReadinessStatus.NullWriteStubFailure, invalidOpcodeStubResult.Status, nullWriteStubResult.Status, invalidOpcodeBytesWritten: invalidOpcodeStubResult.BytesWritten);
        }

        if (!memoryPlanResult.Succeeded)
        {
            return FaultProbeReadinessResult.Fail(FaultProbeReadinessStatus.MemoryPlanUnavailable, invalidOpcodeStubResult.Status, nullWriteStubResult.Status, memoryPlanResult.Status, invalidOpcodeStubResult.BytesWritten, nullWriteStubResult.BytesWritten, memoryPlanResult.RequestedPayloadLength);
        }

        if (memoryPlanResult.RequestedPayloadLength < (nuint)RequiredPayloadBytes)
        {
            return FaultProbeReadinessResult.Fail(FaultProbeReadinessStatus.MemoryPlanTooSmall, invalidOpcodeStubResult.Status, nullWriteStubResult.Status, memoryPlanResult.Status, invalidOpcodeStubResult.BytesWritten, nullWriteStubResult.BytesWritten, memoryPlanResult.RequestedPayloadLength);
        }

        return FaultProbeReadinessResult.Pass(invalidOpcodeStubResult, nullWriteStubResult, memoryPlanResult);
    }
}
