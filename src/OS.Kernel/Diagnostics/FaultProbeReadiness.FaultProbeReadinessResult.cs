using OS.Kernel.Runtime;

namespace OS.Kernel.Diagnostics;
public readonly struct FaultProbeReadinessResult
{
    private FaultProbeReadinessResult(bool succeeded, FaultProbeReadinessStatus status, FaultProbeStubPlanStatus invalidOpcodeStubStatus, FaultProbeStubPlanStatus nullWriteStubStatus, ExecutableStubMemoryPlanStatus memoryPlanStatus, int invalidOpcodeBytesWritten, int nullWriteBytesWritten, int requiredPayloadBytes, nuint availablePayloadBytes)
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

    public static FaultProbeReadinessResult Pass(FaultProbeStubPlanResult invalidOpcodeStubResult, FaultProbeStubPlanResult nullWriteStubResult, ExecutableStubMemoryPlanResult memoryPlanResult)
    {
        return new FaultProbeReadinessResult(true, FaultProbeReadinessStatus.Ready, invalidOpcodeStubResult.Status, nullWriteStubResult.Status, memoryPlanResult.Status, invalidOpcodeStubResult.BytesWritten, nullWriteStubResult.BytesWritten, FaultProbeReadiness.RequiredPayloadBytes, memoryPlanResult.RequestedPayloadLength);
    }

    public static FaultProbeReadinessResult Fail(FaultProbeReadinessStatus status, FaultProbeStubPlanStatus invalidOpcodeStubStatus = FaultProbeStubPlanStatus.Success, FaultProbeStubPlanStatus nullWriteStubStatus = FaultProbeStubPlanStatus.Success, ExecutableStubMemoryPlanStatus memoryPlanStatus = ExecutableStubMemoryPlanStatus.Success, int invalidOpcodeBytesWritten = 0, int nullWriteBytesWritten = 0, nuint availablePayloadBytes = 0)
    {
        return new FaultProbeReadinessResult(false, status, invalidOpcodeStubStatus, nullWriteStubStatus, memoryPlanStatus, invalidOpcodeBytesWritten, nullWriteBytesWritten, FaultProbeReadiness.RequiredPayloadBytes, availablePayloadBytes);
    }
}
