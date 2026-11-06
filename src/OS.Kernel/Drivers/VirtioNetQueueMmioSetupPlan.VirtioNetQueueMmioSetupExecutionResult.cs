using OS.Std.Binary;

namespace OS.Drivers;

public readonly struct VirtioNetQueueMmioSetupExecutionResult
{
    private VirtioNetQueueMmioSetupExecutionResult(bool isInitialized, VirtioNetQueueMmioSetupExecutionStatus status, VirtioNetQueueMmioSetupPlanStatus setupPlanStatus, VirtioPciRegisterReadStatus registerReadStatus, VirtioPciRegisterWriteStatus registerWriteStatus, VirtioNetQueueMmioSetupCommandKind commandKind, int commandIndex, int commandsExecuted, ushort queueIndex, uint relativeOffset, uint widthBytes, ulong expectedValue, ulong observedValue)
    {
        IsInitialized = isInitialized;
        Status = status;
        SetupPlanStatus = setupPlanStatus;
        RegisterReadStatus = registerReadStatus;
        RegisterWriteStatus = registerWriteStatus;
        CommandKind = commandKind;
        CommandIndex = commandIndex;
        CommandsExecuted = commandsExecuted;
        QueueIndex = queueIndex;
        RelativeOffset = relativeOffset;
        WidthBytes = widthBytes;
        ExpectedValue = expectedValue;
        ObservedValue = observedValue;
    }

    public VirtioNetQueueMmioSetupExecutionStatus Status
    {
        get;
    }
    public VirtioNetQueueMmioSetupPlanStatus SetupPlanStatus
    {
        get;
    }
    public VirtioPciRegisterReadStatus RegisterReadStatus
    {
        get;
    }
    public VirtioPciRegisterWriteStatus RegisterWriteStatus
    {
        get;
    }
    public VirtioNetQueueMmioSetupCommandKind CommandKind
    {
        get;
    }
    public int CommandIndex
    {
        get;
    }
    public int CommandsExecuted
    {
        get;
    }
    public ushort QueueIndex
    {
        get;
    }
    public uint RelativeOffset
    {
        get;
    }
    public uint WidthBytes
    {
        get;
    }
    public ulong ExpectedValue
    {
        get;
    }
    public ulong ObservedValue
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == VirtioNetQueueMmioSetupExecutionStatus.Success;

    internal static VirtioNetQueueMmioSetupExecutionResult Success(VirtioNetQueueMmioSetupPlanResult setupPlan, int commandsExecuted)
    {
        return new VirtioNetQueueMmioSetupExecutionResult(isInitialized: true, VirtioNetQueueMmioSetupExecutionStatus.Success, setupPlan.Status, VirtioPciRegisterReadStatus.Success, VirtioPciRegisterWriteStatus.Success, VirtioNetQueueMmioSetupCommandKind.WriteQueueEnable, commandIndex: commandsExecuted - 1, commandsExecuted, queueIndex: 0, relativeOffset: 0, widthBytes: 0, expectedValue: 0, observedValue: 0);
    }

    internal static VirtioNetQueueMmioSetupExecutionResult Fail(VirtioNetQueueMmioSetupExecutionStatus status, VirtioNetQueueMmioSetupPlanStatus setupPlanStatus, VirtioPciRegisterReadStatus registerReadStatus, VirtioPciRegisterWriteStatus registerWriteStatus, VirtioNetQueueMmioSetupCommand command, int commandIndex, int commandsExecuted, ulong observedValue = 0)
    {
        return new VirtioNetQueueMmioSetupExecutionResult(isInitialized: true, status, setupPlanStatus, registerReadStatus, registerWriteStatus, command.Kind, commandIndex, commandsExecuted, command.QueueIndex, command.RelativeOffset, command.WidthBytes, command.Value, observedValue);
    }
}
