using OS.Std.Binary;

namespace OS.Drivers;
public readonly struct VirtioNetQueueMmioSetupRegisterWindowExecutionResult
{
    private VirtioNetQueueMmioSetupRegisterWindowExecutionResult(bool isInitialized, VirtioNetQueueMmioSetupRegisterWindowExecutionStatus status, VirtioNetQueueMmioSetupPlanStatus setupPlanStatus, VirtioPciBarRegisterWindowPlanStatus registerWindowStatus, VirtioPciBarRegisterWindowAccessExecutionStatus accessExecutionStatus, VirtioNetQueueMmioSetupCommandKind commandKind, int commandIndex, int commandsExecuted, ushort queueIndex, uint relativeOffset, uint widthBytes, ulong expectedValue, ulong observedValue)
    {
        IsInitialized = isInitialized;
        Status = status;
        SetupPlanStatus = setupPlanStatus;
        RegisterWindowStatus = registerWindowStatus;
        AccessExecutionStatus = accessExecutionStatus;
        CommandKind = commandKind;
        CommandIndex = commandIndex;
        CommandsExecuted = commandsExecuted;
        QueueIndex = queueIndex;
        RelativeOffset = relativeOffset;
        WidthBytes = widthBytes;
        ExpectedValue = expectedValue;
        ObservedValue = observedValue;
    }

    public VirtioNetQueueMmioSetupRegisterWindowExecutionStatus Status { get; }
    public VirtioNetQueueMmioSetupPlanStatus SetupPlanStatus { get; }
    public VirtioPciBarRegisterWindowPlanStatus RegisterWindowStatus { get; }
    public VirtioPciBarRegisterWindowAccessExecutionStatus AccessExecutionStatus { get; }
    public VirtioNetQueueMmioSetupCommandKind CommandKind { get; }
    public int CommandIndex { get; }
    public int CommandsExecuted { get; }
    public ushort QueueIndex { get; }
    public uint RelativeOffset { get; }
    public uint WidthBytes { get; }
    public ulong ExpectedValue { get; }
    public ulong ObservedValue { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == VirtioNetQueueMmioSetupRegisterWindowExecutionStatus.Success;

    internal static VirtioNetQueueMmioSetupRegisterWindowExecutionResult Success(VirtioNetQueueMmioSetupPlanResult setupPlan, int commandsExecuted)
    {
        return new VirtioNetQueueMmioSetupRegisterWindowExecutionResult(isInitialized: true, VirtioNetQueueMmioSetupRegisterWindowExecutionStatus.Success, setupPlan.Status, VirtioPciBarRegisterWindowPlanStatus.Success, VirtioPciBarRegisterWindowAccessExecutionStatus.Success, VirtioNetQueueMmioSetupCommandKind.WriteQueueEnable, commandIndex: commandsExecuted - 1, commandsExecuted, queueIndex: 0, relativeOffset: 0, widthBytes: 0, expectedValue: 0, observedValue: 0);
    }

    internal static VirtioNetQueueMmioSetupRegisterWindowExecutionResult Fail(VirtioNetQueueMmioSetupRegisterWindowExecutionStatus status, VirtioNetQueueMmioSetupPlanStatus setupPlanStatus, VirtioPciBarRegisterWindowPlanStatus registerWindowStatus, VirtioPciBarRegisterWindowAccessExecutionStatus accessExecutionStatus, VirtioNetQueueMmioSetupCommand command, int commandIndex, int commandsExecuted, ulong observedValue = 0)
    {
        return new VirtioNetQueueMmioSetupRegisterWindowExecutionResult(isInitialized: true, status, setupPlanStatus, registerWindowStatus, accessExecutionStatus, command.Kind, commandIndex, commandsExecuted, command.QueueIndex, command.RelativeOffset, command.WidthBytes, command.Value, observedValue);
    }
}
