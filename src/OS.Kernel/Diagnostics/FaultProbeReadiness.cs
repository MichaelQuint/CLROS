using OS.Kernel.Runtime;

namespace OS.Kernel.Diagnostics;

public enum FaultProbeReadinessStatus
{
    Ready = 0,
    ProbeEnabledByDefault = 1,
    InvalidOpcodeStubFailure = 2,
    NullWriteStubFailure = 3,
    MemoryPlanUnavailable = 4,
    MemoryPlanTooSmall = 5
}

public readonly struct FaultProbeReadinessResult
{
    private FaultProbeReadinessResult(
        bool succeeded,
        FaultProbeReadinessStatus status,
        FaultProbeStubPlanStatus invalidOpcodeStubStatus,
        FaultProbeStubPlanStatus nullWriteStubStatus,
        ExecutableStubMemoryPlanStatus memoryPlanStatus,
        int invalidOpcodeBytesWritten,
        int nullWriteBytesWritten,
        int requiredPayloadBytes,
        nuint availablePayloadBytes)
    {
        Succeeded = succeeded;
        Status = status;
        Operation = FaultProbeReadiness.OperationName;
        InvalidOpcodeStubStatus = invalidOpcodeStubStatus;
        NullWriteStubStatus = nullWriteStubStatus;
        MemoryPlanStatus = memoryPlanStatus;
        InvalidOpcodeBytesWritten = invalidOpcodeBytesWritten;
        NullWriteBytesWritten = nullWriteBytesWritten;
        RequiredPayloadBytes = requiredPayloadBytes;
        AvailablePayloadBytes = availablePayloadBytes;
    }

    public bool Succeeded { get; }

    public FaultProbeReadinessStatus Status { get; }

    public string Operation { get; }

    public FaultProbeStubPlanStatus InvalidOpcodeStubStatus { get; }

    public FaultProbeStubPlanStatus NullWriteStubStatus { get; }

    public ExecutableStubMemoryPlanStatus MemoryPlanStatus { get; }

    public int InvalidOpcodeBytesWritten { get; }

    public int NullWriteBytesWritten { get; }

    public int RequiredPayloadBytes { get; }

    public nuint AvailablePayloadBytes { get; }

    public static FaultProbeReadinessResult Pass(
        FaultProbeStubPlanResult invalidOpcodeStubResult,
        FaultProbeStubPlanResult nullWriteStubResult,
        ExecutableStubMemoryPlanResult memoryPlanResult)
    {
        return new FaultProbeReadinessResult(
            true,
            FaultProbeReadinessStatus.Ready,
            invalidOpcodeStubResult.Status,
            nullWriteStubResult.Status,
            memoryPlanResult.Status,
            invalidOpcodeStubResult.BytesWritten,
            nullWriteStubResult.BytesWritten,
            FaultProbeReadiness.RequiredPayloadBytes,
            memoryPlanResult.RequestedPayloadLength);
    }

    public static FaultProbeReadinessResult Fail(
        FaultProbeReadinessStatus status,
        FaultProbeStubPlanStatus invalidOpcodeStubStatus = FaultProbeStubPlanStatus.Success,
        FaultProbeStubPlanStatus nullWriteStubStatus = FaultProbeStubPlanStatus.Success,
        ExecutableStubMemoryPlanStatus memoryPlanStatus = ExecutableStubMemoryPlanStatus.Success,
        int invalidOpcodeBytesWritten = 0,
        int nullWriteBytesWritten = 0,
        nuint availablePayloadBytes = 0)
    {
        return new FaultProbeReadinessResult(
            false,
            status,
            invalidOpcodeStubStatus,
            nullWriteStubStatus,
            memoryPlanStatus,
            invalidOpcodeBytesWritten,
            nullWriteBytesWritten,
            FaultProbeReadiness.RequiredPayloadBytes,
            availablePayloadBytes);
    }
}

public static class FaultProbeReadiness
{
    public const string OperationName = "fault.probe.ready";

    public const int RequiredPayloadBytes =
        FaultProbeStubPlan.InvalidOpcodeRequiredByteLength + FaultProbeStubPlan.NullWriteRequiredByteLength;

    public static FaultProbeReadinessResult RequireReady(
        Span<byte> invalidOpcodeStubBuffer,
        Span<byte> nullWriteStubBuffer,
        ExecutableStubMemoryPlanResult memoryPlanResult)
    {
        return RequireReady(
            invalidOpcodeEnabledByDefault: Probes.GetInvalidOpcodePanicProbe().EnabledByDefault,
            nullWriteEnabledByDefault: Probes.GetNullPointerPanicProbe().EnabledByDefault,
            invalidOpcodeStubBuffer,
            nullWriteStubBuffer,
            memoryPlanResult);
    }

    public static FaultProbeReadinessResult RequireReady(
        bool invalidOpcodeEnabledByDefault,
        bool nullWriteEnabledByDefault,
        Span<byte> invalidOpcodeStubBuffer,
        Span<byte> nullWriteStubBuffer,
        ExecutableStubMemoryPlanResult memoryPlanResult)
    {
        if (invalidOpcodeEnabledByDefault || nullWriteEnabledByDefault)
        {
            return FaultProbeReadinessResult.Fail(FaultProbeReadinessStatus.ProbeEnabledByDefault);
        }

        FaultProbeStubPlanResult invalidOpcodeStubResult =
            FaultProbeStubPlan.InvalidOpcode.Emit(invalidOpcodeStubBuffer);
        if (!invalidOpcodeStubResult.Succeeded)
        {
            return FaultProbeReadinessResult.Fail(
                FaultProbeReadinessStatus.InvalidOpcodeStubFailure,
                invalidOpcodeStubResult.Status);
        }

        FaultProbeStubPlanResult nullWriteStubResult =
            FaultProbeStubPlan.NullWrite.Emit(nullWriteStubBuffer);
        if (!nullWriteStubResult.Succeeded)
        {
            return FaultProbeReadinessResult.Fail(
                FaultProbeReadinessStatus.NullWriteStubFailure,
                invalidOpcodeStubResult.Status,
                nullWriteStubResult.Status,
                invalidOpcodeBytesWritten: invalidOpcodeStubResult.BytesWritten);
        }

        if (!memoryPlanResult.Succeeded)
        {
            return FaultProbeReadinessResult.Fail(
                FaultProbeReadinessStatus.MemoryPlanUnavailable,
                invalidOpcodeStubResult.Status,
                nullWriteStubResult.Status,
                memoryPlanResult.Status,
                invalidOpcodeStubResult.BytesWritten,
                nullWriteStubResult.BytesWritten,
                memoryPlanResult.RequestedPayloadLength);
        }

        if (memoryPlanResult.RequestedPayloadLength < (nuint)RequiredPayloadBytes)
        {
            return FaultProbeReadinessResult.Fail(
                FaultProbeReadinessStatus.MemoryPlanTooSmall,
                invalidOpcodeStubResult.Status,
                nullWriteStubResult.Status,
                memoryPlanResult.Status,
                invalidOpcodeStubResult.BytesWritten,
                nullWriteStubResult.BytesWritten,
                memoryPlanResult.RequestedPayloadLength);
        }

        return FaultProbeReadinessResult.Pass(
            invalidOpcodeStubResult,
            nullWriteStubResult,
            memoryPlanResult);
    }
}
