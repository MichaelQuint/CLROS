using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockQueueMmioSetupRegisterWindowExecutionResult
{
    private VirtioBlockQueueMmioSetupRegisterWindowExecutionResult(
        bool isInitialized,
        VirtioBlockQueueMmioSetupRegisterWindowExecutionStatus status,
        VirtioBlockQueueMmioSetupPlanStatus setupPlanStatus,
        VirtioPciBarRegisterWindowPlanStatus registerWindowStatus,
        VirtioPciBarRegisterWindowAccessExecutionStatus accessExecutionStatus,
        VirtioBlockQueueMmioSetupCommandKind commandKind,
        int commandIndex,
        int commandsExecuted,
        ushort queueIndex,
        uint relativeOffset,
        uint widthBytes,
        ulong expectedValue,
        ulong observedValue)
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

    public bool IsInitialized
    {
        get;
    }

    public VirtioBlockQueueMmioSetupRegisterWindowExecutionStatus Status
    {
        get;
    }

    public VirtioBlockQueueMmioSetupPlanStatus SetupPlanStatus
    {
        get;
    }

    public VirtioPciBarRegisterWindowPlanStatus RegisterWindowStatus
    {
        get;
    }

    public VirtioPciBarRegisterWindowAccessExecutionStatus AccessExecutionStatus
    {
        get;
    }

    public VirtioBlockQueueMmioSetupCommandKind CommandKind
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

    public bool Succeeded =>
        IsInitialized &&
        Status == VirtioBlockQueueMmioSetupRegisterWindowExecutionStatus.Success;

    internal static VirtioBlockQueueMmioSetupRegisterWindowExecutionResult Success(
        VirtioBlockQueueMmioSetupPlanResult setupPlan,
        VirtioBlockQueueMmioSetupCommand command,
        int commandsExecuted)
    {
        return new VirtioBlockQueueMmioSetupRegisterWindowExecutionResult(
            isInitialized: true,
            VirtioBlockQueueMmioSetupRegisterWindowExecutionStatus.Success,
            setupPlan.Status,
            VirtioPciBarRegisterWindowPlanStatus.Success,
            VirtioPciBarRegisterWindowAccessExecutionStatus.Success,
            command.Kind,
            commandIndex: commandsExecuted - 1,
            commandsExecuted,
            command.QueueIndex,
            command.RelativeOffset,
            command.WidthBytes,
            command.Value,
            command.Value);
    }

    internal static VirtioBlockQueueMmioSetupRegisterWindowExecutionResult Fail(
        VirtioBlockQueueMmioSetupRegisterWindowExecutionStatus status,
        VirtioBlockQueueMmioSetupPlanStatus setupPlanStatus,
        VirtioPciBarRegisterWindowPlanStatus registerWindowStatus,
        VirtioPciBarRegisterWindowAccessExecutionStatus accessExecutionStatus,
        VirtioBlockQueueMmioSetupCommand command,
        int commandIndex,
        int commandsExecuted,
        ulong observedValue = 0)
    {
        return new VirtioBlockQueueMmioSetupRegisterWindowExecutionResult(
            isInitialized: true,
            status,
            setupPlanStatus,
            registerWindowStatus,
            accessExecutionStatus,
            command.Kind,
            commandIndex,
            commandsExecuted,
            command.QueueIndex,
            command.RelativeOffset,
            command.WidthBytes,
            command.Value,
            observedValue);
    }
}
