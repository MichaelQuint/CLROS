using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult
{
    private VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult(
        bool isInitialized,
        VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus status,
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
        ulong observedValue,
        bool queueSelectVisible,
        bool queueSizeVisible,
        bool queueAddressWritesVisible,
        bool orderedBeforeQueueEnable,
        uint barrierSequence,
        bool queueEnableAttempted)
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
        QueueSelectVisible = queueSelectVisible;
        QueueSizeVisible = queueSizeVisible;
        QueueAddressWritesVisible = queueAddressWritesVisible;
        OrderedBeforeQueueEnable = orderedBeforeQueueEnable;
        BarrierSequence = barrierSequence;
        QueueEnableAttempted = queueEnableAttempted;
    }

    public bool IsInitialized { get; }

    public VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus Status { get; }

    public VirtioBlockQueueMmioSetupPlanStatus SetupPlanStatus { get; }

    public VirtioPciBarRegisterWindowPlanStatus RegisterWindowStatus { get; }

    public VirtioPciBarRegisterWindowAccessExecutionStatus AccessExecutionStatus { get; }

    public VirtioBlockQueueMmioSetupCommandKind CommandKind { get; }

    public int CommandIndex { get; }

    public int CommandsExecuted { get; }

    public ushort QueueIndex { get; }

    public uint RelativeOffset { get; }

    public uint WidthBytes { get; }

    public ulong ExpectedValue { get; }

    public ulong ObservedValue { get; }

    public bool QueueSelectVisible { get; }

    public bool QueueSizeVisible { get; }

    public bool QueueAddressWritesVisible { get; }

    public bool OrderedBeforeQueueEnable { get; }

    public uint BarrierSequence { get; }

    public bool BarrierSucceeded =>
        QueueSelectVisible &&
        QueueSizeVisible &&
        QueueAddressWritesVisible &&
        OrderedBeforeQueueEnable;

    public bool QueueEnableAttempted { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus.Success;

    internal static VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult Success(
        VirtioBlockQueueMmioSetupPlanResult setupPlan,
        VirtioBlockQueueMmioSetupCommand command,
        int commandsExecuted,
        VirtioBlockQueueMmioSetupBarrierReadiness barrier,
        bool queueEnableAttempted)
    {
        return new VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult(
            isInitialized: true,
            VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus.Success,
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
            command.Value,
            barrier.QueueSelectVisible,
            barrier.QueueSizeVisible,
            barrier.QueueAddressWritesVisible,
            barrier.OrderedBeforeQueueEnable,
            barrier.BarrierSequence,
            queueEnableAttempted);
    }

    internal static VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult Fail(
        VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus status,
        VirtioBlockQueueMmioSetupPlanStatus setupPlanStatus,
        VirtioPciBarRegisterWindowPlanStatus registerWindowStatus,
        VirtioPciBarRegisterWindowAccessExecutionStatus accessExecutionStatus,
        VirtioBlockQueueMmioSetupCommand command,
        int commandIndex,
        int commandsExecuted,
        VirtioBlockQueueMmioSetupBarrierReadiness barrier,
        bool queueEnableAttempted,
        ulong observedValue = 0)
    {
        return new VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult(
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
            observedValue,
            barrier.QueueSelectVisible,
            barrier.QueueSizeVisible,
            barrier.QueueAddressWritesVisible,
            barrier.OrderedBeforeQueueEnable,
            barrier.BarrierSequence,
            queueEnableAttempted);
    }
}
