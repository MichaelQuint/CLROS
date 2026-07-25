using OS.Drivers;

namespace OS.Storage;

public enum VirtioBlockQueueMmioSetupRegisterWindowPlanStatus
{
    Success = 0,
    SetupPlanFailed = 1,
    CommandSourceTooSmall = 2,
    AccessPlanDestinationTooSmall = 3,
    UnsupportedCommandWidth = 4,
    RegisterWindowPlanFailed = 5
}

public enum VirtioBlockQueueMmioSetupRegisterWindowExecutionStatus
{
    Success = 0,
    SetupPlanFailed = 1,
    CommandSourceTooSmall = 2,
    UnsupportedCommandWidth = 3,
    RegisterWindowPlanFailed = 4,
    AccessExecutionFailed = 5,
    RegisterReadMismatch = 6
}

public enum VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus
{
    Success = 0,
    SetupPlanFailed = 1,
    CommandSourceTooSmall = 2,
    UnsupportedCommandWidth = 3,
    RegisterWindowPlanFailed = 4,
    MemoryBarrierNotReady = 5,
    AccessExecutionFailed = 6,
    RegisterReadMismatch = 7
}

public readonly struct VirtioBlockQueueMmioSetupBarrierReadiness
{
    public VirtioBlockQueueMmioSetupBarrierReadiness(
        bool queueSelectVisible,
        bool queueSizeVisible,
        bool queueAddressWritesVisible,
        bool orderedBeforeQueueEnable,
        uint barrierSequence)
    {
        QueueSelectVisible = queueSelectVisible;
        QueueSizeVisible = queueSizeVisible;
        QueueAddressWritesVisible = queueAddressWritesVisible;
        OrderedBeforeQueueEnable = orderedBeforeQueueEnable;
        BarrierSequence = barrierSequence;
    }

    public bool QueueSelectVisible { get; }

    public bool QueueSizeVisible { get; }

    public bool QueueAddressWritesVisible { get; }

    public bool OrderedBeforeQueueEnable { get; }

    public uint BarrierSequence { get; }

    public bool Succeeded =>
        QueueSelectVisible &&
        QueueSizeVisible &&
        QueueAddressWritesVisible &&
        OrderedBeforeQueueEnable;

    public static VirtioBlockQueueMmioSetupBarrierReadiness Ready(uint barrierSequence)
    {
        return new VirtioBlockQueueMmioSetupBarrierReadiness(
            queueSelectVisible: true,
            queueSizeVisible: true,
            queueAddressWritesVisible: true,
            orderedBeforeQueueEnable: true,
            barrierSequence);
    }
}

public readonly struct VirtioBlockQueueMmioSetupRegisterWindowPlanResult
{
    private VirtioBlockQueueMmioSetupRegisterWindowPlanResult(
        bool isInitialized,
        VirtioBlockQueueMmioSetupRegisterWindowPlanStatus status,
        VirtioBlockQueueMmioSetupPlanStatus setupPlanStatus,
        VirtioPciBarRegisterWindowPlanStatus registerWindowStatus,
        VirtioBlockQueueMmioSetupCommandKind commandKind,
        int commandIndex,
        int accessPlansWritten,
        int requiredAccessPlanCount,
        int availableAccessPlanCount,
        ushort queueIndex,
        uint relativeOffset,
        uint widthBytes)
    {
        IsInitialized = isInitialized;
        Status = status;
        SetupPlanStatus = setupPlanStatus;
        RegisterWindowStatus = registerWindowStatus;
        CommandKind = commandKind;
        CommandIndex = commandIndex;
        AccessPlansWritten = accessPlansWritten;
        RequiredAccessPlanCount = requiredAccessPlanCount;
        AvailableAccessPlanCount = availableAccessPlanCount;
        QueueIndex = queueIndex;
        RelativeOffset = relativeOffset;
        WidthBytes = widthBytes;
    }

    public bool IsInitialized { get; }

    public VirtioBlockQueueMmioSetupRegisterWindowPlanStatus Status { get; }

    public VirtioBlockQueueMmioSetupPlanStatus SetupPlanStatus { get; }

    public VirtioPciBarRegisterWindowPlanStatus RegisterWindowStatus { get; }

    public VirtioBlockQueueMmioSetupCommandKind CommandKind { get; }

    public int CommandIndex { get; }

    public int AccessPlansWritten { get; }

    public int RequiredAccessPlanCount { get; }

    public int AvailableAccessPlanCount { get; }

    public ushort QueueIndex { get; }

    public uint RelativeOffset { get; }

    public uint WidthBytes { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == VirtioBlockQueueMmioSetupRegisterWindowPlanStatus.Success;

    internal static VirtioBlockQueueMmioSetupRegisterWindowPlanResult Success(
        VirtioBlockQueueMmioSetupPlanResult setupPlan,
        VirtioBlockQueueMmioSetupCommand command,
        int accessPlansWritten,
        int availableAccessPlanCount)
    {
        return new VirtioBlockQueueMmioSetupRegisterWindowPlanResult(
            isInitialized: true,
            VirtioBlockQueueMmioSetupRegisterWindowPlanStatus.Success,
            setupPlan.Status,
            VirtioPciBarRegisterWindowPlanStatus.Success,
            command.Kind,
            commandIndex: accessPlansWritten - 1,
            accessPlansWritten,
            setupPlan.CommandCount,
            availableAccessPlanCount,
            command.QueueIndex,
            command.RelativeOffset,
            command.WidthBytes);
    }

    internal static VirtioBlockQueueMmioSetupRegisterWindowPlanResult Fail(
        VirtioBlockQueueMmioSetupRegisterWindowPlanStatus status,
        VirtioBlockQueueMmioSetupPlanStatus setupPlanStatus,
        VirtioPciBarRegisterWindowPlanStatus registerWindowStatus,
        VirtioBlockQueueMmioSetupCommand command,
        int commandIndex,
        int accessPlansWritten,
        int requiredAccessPlanCount,
        int availableAccessPlanCount)
    {
        return new VirtioBlockQueueMmioSetupRegisterWindowPlanResult(
            isInitialized: true,
            status,
            setupPlanStatus,
            registerWindowStatus,
            command.Kind,
            commandIndex,
            accessPlansWritten,
            requiredAccessPlanCount,
            availableAccessPlanCount,
            command.QueueIndex,
            command.RelativeOffset,
            command.WidthBytes);
    }
}

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

    public bool IsInitialized { get; }

    public VirtioBlockQueueMmioSetupRegisterWindowExecutionStatus Status { get; }

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

public static class VirtioBlockQueueMmioSetupRegisterWindowPlanner
{
    public const bool RequiresRegisterWindowBinding = true;
    public const bool WritesAccessPlansToCallerStorage = true;
    public const bool PlansCommonConfigurationAccesses = true;
    public const bool MapsBars = false;
    public const bool PerformsLiveMmio = false;
    public const bool ContainsVolatileAccess = false;
    public const bool ReadsDeviceRegisters = false;
    public const bool WritesDeviceRegisters = false;
    public const bool ConfiguresLiveQueues = false;
    public const bool NotifiesLiveDevice = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsDmaMapping = false;
    public const bool PerformsBlockIo = false;
    public const bool PerformsDiskRead = false;
    public const bool PerformsDiskWrite = false;

    public static VirtioBlockQueueMmioSetupRegisterWindowPlanResult PlanAccesses(
        VirtioBlockQueueMmioSetupPlanResult setupPlan,
        ReadOnlySpan<VirtioBlockQueueMmioSetupCommand> commands,
        VirtioPciBarRegisterWindowBinding commonConfiguration,
        Span<VirtioPciBarRegisterWindowAccessPlan> accessPlans)
    {
        if (!setupPlan.Succeeded)
        {
            return VirtioBlockQueueMmioSetupRegisterWindowPlanResult.Fail(
                VirtioBlockQueueMmioSetupRegisterWindowPlanStatus.SetupPlanFailed,
                setupPlan.Status,
                VirtioPciBarRegisterWindowPlanStatus.Success,
                default,
                commandIndex: -1,
                accessPlansWritten: 0,
                requiredAccessPlanCount: setupPlan.CommandCount,
                availableAccessPlanCount: accessPlans.Length);
        }

        if (commands.Length < setupPlan.CommandCount)
        {
            return VirtioBlockQueueMmioSetupRegisterWindowPlanResult.Fail(
                VirtioBlockQueueMmioSetupRegisterWindowPlanStatus.CommandSourceTooSmall,
                setupPlan.Status,
                VirtioPciBarRegisterWindowPlanStatus.Success,
                default,
                commandIndex: commands.Length,
                accessPlansWritten: 0,
                requiredAccessPlanCount: setupPlan.CommandCount,
                availableAccessPlanCount: accessPlans.Length);
        }

        if (accessPlans.Length < setupPlan.CommandCount)
        {
            return VirtioBlockQueueMmioSetupRegisterWindowPlanResult.Fail(
                VirtioBlockQueueMmioSetupRegisterWindowPlanStatus.AccessPlanDestinationTooSmall,
                setupPlan.Status,
                VirtioPciBarRegisterWindowPlanStatus.Success,
                default,
                commandIndex: accessPlans.Length,
                accessPlansWritten: 0,
                requiredAccessPlanCount: setupPlan.CommandCount,
                availableAccessPlanCount: accessPlans.Length);
        }

        for (int index = 0; index < setupPlan.CommandCount; index++)
        {
            VirtioBlockQueueMmioSetupCommand command = commands[index];
            VirtioPciRegisterAccessWidth width = GetRegisterWidth(command.WidthBytes);
            if (width == default)
            {
                return VirtioBlockQueueMmioSetupRegisterWindowPlanResult.Fail(
                    VirtioBlockQueueMmioSetupRegisterWindowPlanStatus.UnsupportedCommandWidth,
                    setupPlan.Status,
                    VirtioPciBarRegisterWindowPlanStatus.UnsupportedAccessWidth,
                    command,
                    index,
                    accessPlansWritten: index,
                    requiredAccessPlanCount: setupPlan.CommandCount,
                    availableAccessPlanCount: accessPlans.Length);
            }

            VirtioPciBarRegisterWindowAccessPlanResult accessPlan =
                VirtioPciBarRegisterWindowPlanner.PlanAccess(
                    commonConfiguration,
                    command.RelativeOffset,
                    width);
            if (!accessPlan.Succeeded)
            {
                return VirtioBlockQueueMmioSetupRegisterWindowPlanResult.Fail(
                    VirtioBlockQueueMmioSetupRegisterWindowPlanStatus.RegisterWindowPlanFailed,
                    setupPlan.Status,
                    accessPlan.Status,
                    command,
                    index,
                    accessPlansWritten: index,
                    requiredAccessPlanCount: setupPlan.CommandCount,
                    availableAccessPlanCount: accessPlans.Length);
            }

            accessPlans[index] = accessPlan.Plan;
        }

        return VirtioBlockQueueMmioSetupRegisterWindowPlanResult.Success(
            setupPlan,
            commands[setupPlan.CommandCount - 1],
            setupPlan.CommandCount,
            accessPlans.Length);
    }

    private static VirtioPciRegisterAccessWidth GetRegisterWidth(uint widthBytes)
    {
        return widthBytes == sizeof(byte)
            ? VirtioPciRegisterAccessWidth.Byte
            : widthBytes == sizeof(ushort)
                ? VirtioPciRegisterAccessWidth.Word
                : widthBytes == sizeof(uint)
                    ? VirtioPciRegisterAccessWidth.DWord
                    : widthBytes == sizeof(ulong)
                        ? VirtioPciRegisterAccessWidth.QWord
                        : default;
    }
}

public static class VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutor
{
    public const bool RequiresRegisterWindowBinding = true;
    public const bool UsesCallerOwnedWindowBuffer = true;
    public const bool StagesExpectedDeviceReadValues = true;
    public const bool InvokesRegisterWindowAccessExecutor = true;
    public const bool RequiresBarrierReadiness = true;
    public const bool GatesQueueEnableOnBarrier = true;
    public const bool MapsBars = false;
    public const bool PerformsLiveMmio = false;
    public const bool ContainsVolatileAccess = false;
    public const bool AccessesPhysicalAddresses = false;
    public const bool ExecutesMemoryBarriers = false;
    public const bool ConfiguresLiveQueues = false;
    public const bool NotifiesLiveDevice = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsDmaMapping = false;
    public const bool PerformsBlockIo = false;
    public const bool PerformsDiskRead = false;
    public const bool PerformsDiskWrite = false;

    public static VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult Execute(
        VirtioBlockQueueMmioSetupPlanResult setupPlan,
        ReadOnlySpan<VirtioBlockQueueMmioSetupCommand> commands,
        VirtioPciBarRegisterWindowBinding commonConfiguration,
        Span<byte> commonWindowBytes,
        VirtioBlockQueueMmioSetupBarrierReadiness barrier)
    {
        if (!setupPlan.Succeeded)
        {
            return VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult.Fail(
                VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus.SetupPlanFailed,
                setupPlan.Status,
                VirtioPciBarRegisterWindowPlanStatus.Success,
                VirtioPciBarRegisterWindowAccessExecutionStatus.Success,
                default,
                commandIndex: -1,
                commandsExecuted: 0,
                barrier,
                queueEnableAttempted: false);
        }

        if (commands.Length < setupPlan.CommandCount)
        {
            return VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult.Fail(
                VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus.CommandSourceTooSmall,
                setupPlan.Status,
                VirtioPciBarRegisterWindowPlanStatus.Success,
                VirtioPciBarRegisterWindowAccessExecutionStatus.Success,
                default,
                commandIndex: commands.Length,
                commandsExecuted: 0,
                barrier,
                queueEnableAttempted: false);
        }

        bool queueEnableAttempted = false;

        for (int index = 0; index < setupPlan.CommandCount; index++)
        {
            VirtioBlockQueueMmioSetupCommand command = commands[index];
            VirtioPciRegisterAccessWidth width = GetRegisterWidth(command.WidthBytes);
            if (width == default)
            {
                return VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult.Fail(
                    VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus.UnsupportedCommandWidth,
                    setupPlan.Status,
                    VirtioPciBarRegisterWindowPlanStatus.UnsupportedAccessWidth,
                    VirtioPciBarRegisterWindowAccessExecutionStatus.UnsupportedAccessWidth,
                    command,
                    index,
                    commandsExecuted: index,
                    barrier,
                    queueEnableAttempted);
            }

            VirtioPciBarRegisterWindowAccessPlanResult accessPlan =
                VirtioPciBarRegisterWindowPlanner.PlanAccess(
                    commonConfiguration,
                    command.RelativeOffset,
                    width);
            if (!accessPlan.Succeeded)
            {
                return VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult.Fail(
                    VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus.RegisterWindowPlanFailed,
                    setupPlan.Status,
                    accessPlan.Status,
                    VirtioPciBarRegisterWindowAccessExecutionStatus.Success,
                    command,
                    index,
                    commandsExecuted: index,
                    barrier,
                    queueEnableAttempted);
            }

            if (command.ReadsRegister)
            {
                ulong stagedValue = GetExpectedReadValue(setupPlan.Plan, command);
                VirtioPciBarRegisterWindowAccessExecutionResult stage =
                    VirtioPciBarRegisterWindowAccessExecutor.Write(
                        accessPlan.Plan,
                        stagedValue,
                        commonWindowBytes);
                if (!stage.Succeeded)
                {
                    return AccessFailure(setupPlan, command, index, stage, barrier, queueEnableAttempted);
                }

                VirtioPciBarRegisterWindowAccessExecutionResult read =
                    VirtioPciBarRegisterWindowAccessExecutor.Read(
                        accessPlan.Plan,
                        commonWindowBytes);
                if (!read.Succeeded)
                {
                    return AccessFailure(setupPlan, command, index, read, barrier, queueEnableAttempted);
                }

                if (read.Value != command.Value)
                {
                    return VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult.Fail(
                        VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus.RegisterReadMismatch,
                        setupPlan.Status,
                        VirtioPciBarRegisterWindowPlanStatus.Success,
                        read.Status,
                        command,
                        index,
                        commandsExecuted: index,
                        barrier,
                        queueEnableAttempted,
                        read.Value);
                }

                continue;
            }

            if (command.Kind == VirtioBlockQueueMmioSetupCommandKind.WriteQueueEnable)
            {
                if (!barrier.Succeeded)
                {
                    return VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult.Fail(
                        VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus.MemoryBarrierNotReady,
                        setupPlan.Status,
                        VirtioPciBarRegisterWindowPlanStatus.Success,
                        VirtioPciBarRegisterWindowAccessExecutionStatus.Success,
                        command,
                        index,
                        commandsExecuted: index,
                        barrier,
                        queueEnableAttempted: false);
                }

                queueEnableAttempted = true;
            }

            VirtioPciBarRegisterWindowAccessExecutionResult write =
                VirtioPciBarRegisterWindowAccessExecutor.Write(
                    accessPlan.Plan,
                    command.Value,
                    commonWindowBytes);
            if (!write.Succeeded)
            {
                return AccessFailure(setupPlan, command, index, write, barrier, queueEnableAttempted);
            }
        }

        return VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult.Success(
            setupPlan,
            commands[setupPlan.CommandCount - 1],
            setupPlan.CommandCount,
            barrier,
            queueEnableAttempted);
    }

    private static VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult AccessFailure(
        VirtioBlockQueueMmioSetupPlanResult setupPlan,
        VirtioBlockQueueMmioSetupCommand command,
        int commandIndex,
        VirtioPciBarRegisterWindowAccessExecutionResult access,
        VirtioBlockQueueMmioSetupBarrierReadiness barrier,
        bool queueEnableAttempted)
    {
        return VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult.Fail(
            VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus.AccessExecutionFailed,
            setupPlan.Status,
            VirtioPciBarRegisterWindowPlanStatus.Success,
            access.Status,
            command,
            commandIndex,
            commandsExecuted: commandIndex,
            barrier,
            queueEnableAttempted,
            access.Value);
    }

    private static ulong GetExpectedReadValue(
        VirtioBlockQueueMmioSetupPlan plan,
        VirtioBlockQueueMmioSetupCommand command)
    {
        return command.Kind == VirtioBlockQueueMmioSetupCommandKind.ReadQueueSize
            ? plan.DeviceMaximumQueueSize
            : command.Kind == VirtioBlockQueueMmioSetupCommandKind.ReadQueueNotifyOffset
                ? plan.NotifyOffset
                : command.Value;
    }

    private static VirtioPciRegisterAccessWidth GetRegisterWidth(uint widthBytes)
    {
        return widthBytes == sizeof(byte)
            ? VirtioPciRegisterAccessWidth.Byte
            : widthBytes == sizeof(ushort)
                ? VirtioPciRegisterAccessWidth.Word
                : widthBytes == sizeof(uint)
                    ? VirtioPciRegisterAccessWidth.DWord
                    : widthBytes == sizeof(ulong)
                        ? VirtioPciRegisterAccessWidth.QWord
                        : default;
    }
}

public static class VirtioBlockQueueMmioSetupRegisterWindowExecutor
{
    public const bool RequiresRegisterWindowBinding = true;
    public const bool UsesCallerOwnedWindowBuffer = true;
    public const bool StagesExpectedDeviceReadValues = true;
    public const bool InvokesRegisterWindowAccessExecutor = true;
    public const bool MapsBars = false;
    public const bool PerformsLiveMmio = false;
    public const bool ContainsVolatileAccess = false;
    public const bool AccessesPhysicalAddresses = false;
    public const bool ExecutesMemoryBarriers = false;
    public const bool ConfiguresLiveQueues = false;
    public const bool NotifiesLiveDevice = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsDmaMapping = false;
    public const bool PerformsBlockIo = false;
    public const bool PerformsDiskRead = false;
    public const bool PerformsDiskWrite = false;

    public static VirtioBlockQueueMmioSetupRegisterWindowExecutionResult Execute(
        VirtioBlockQueueMmioSetupPlanResult setupPlan,
        ReadOnlySpan<VirtioBlockQueueMmioSetupCommand> commands,
        VirtioPciBarRegisterWindowBinding commonConfiguration,
        Span<byte> commonWindowBytes)
    {
        if (!setupPlan.Succeeded)
        {
            return VirtioBlockQueueMmioSetupRegisterWindowExecutionResult.Fail(
                VirtioBlockQueueMmioSetupRegisterWindowExecutionStatus.SetupPlanFailed,
                setupPlan.Status,
                VirtioPciBarRegisterWindowPlanStatus.Success,
                VirtioPciBarRegisterWindowAccessExecutionStatus.Success,
                default,
                commandIndex: -1,
                commandsExecuted: 0);
        }

        if (commands.Length < setupPlan.CommandCount)
        {
            return VirtioBlockQueueMmioSetupRegisterWindowExecutionResult.Fail(
                VirtioBlockQueueMmioSetupRegisterWindowExecutionStatus.CommandSourceTooSmall,
                setupPlan.Status,
                VirtioPciBarRegisterWindowPlanStatus.Success,
                VirtioPciBarRegisterWindowAccessExecutionStatus.Success,
                default,
                commandIndex: commands.Length,
                commandsExecuted: commands.Length);
        }

        for (int index = 0; index < setupPlan.CommandCount; index++)
        {
            VirtioBlockQueueMmioSetupCommand command = commands[index];
            VirtioPciRegisterAccessWidth width = GetRegisterWidth(command.WidthBytes);
            if (width == default)
            {
                return VirtioBlockQueueMmioSetupRegisterWindowExecutionResult.Fail(
                    VirtioBlockQueueMmioSetupRegisterWindowExecutionStatus.UnsupportedCommandWidth,
                    setupPlan.Status,
                    VirtioPciBarRegisterWindowPlanStatus.UnsupportedAccessWidth,
                    VirtioPciBarRegisterWindowAccessExecutionStatus.UnsupportedAccessWidth,
                    command,
                    index,
                    commandsExecuted: index);
            }

            VirtioPciBarRegisterWindowAccessPlanResult accessPlan =
                VirtioPciBarRegisterWindowPlanner.PlanAccess(
                    commonConfiguration,
                    command.RelativeOffset,
                    width);
            if (!accessPlan.Succeeded)
            {
                return VirtioBlockQueueMmioSetupRegisterWindowExecutionResult.Fail(
                    VirtioBlockQueueMmioSetupRegisterWindowExecutionStatus.RegisterWindowPlanFailed,
                    setupPlan.Status,
                    accessPlan.Status,
                    VirtioPciBarRegisterWindowAccessExecutionStatus.Success,
                    command,
                    index,
                    commandsExecuted: index);
            }

            if (command.ReadsRegister)
            {
                ulong stagedValue = GetExpectedReadValue(setupPlan.Plan, command);
                VirtioPciBarRegisterWindowAccessExecutionResult stage =
                    VirtioPciBarRegisterWindowAccessExecutor.Write(
                        accessPlan.Plan,
                        stagedValue,
                        commonWindowBytes);
                if (!stage.Succeeded)
                {
                    return AccessFailure(setupPlan, command, index, stage);
                }

                VirtioPciBarRegisterWindowAccessExecutionResult read =
                    VirtioPciBarRegisterWindowAccessExecutor.Read(
                        accessPlan.Plan,
                        commonWindowBytes);
                if (!read.Succeeded)
                {
                    return AccessFailure(setupPlan, command, index, read);
                }

                if (read.Value != command.Value)
                {
                    return VirtioBlockQueueMmioSetupRegisterWindowExecutionResult.Fail(
                        VirtioBlockQueueMmioSetupRegisterWindowExecutionStatus.RegisterReadMismatch,
                        setupPlan.Status,
                        VirtioPciBarRegisterWindowPlanStatus.Success,
                        read.Status,
                        command,
                        index,
                        commandsExecuted: index,
                        read.Value);
                }

                continue;
            }

            VirtioPciBarRegisterWindowAccessExecutionResult write =
                VirtioPciBarRegisterWindowAccessExecutor.Write(
                    accessPlan.Plan,
                    command.Value,
                    commonWindowBytes);
            if (!write.Succeeded)
            {
                return AccessFailure(setupPlan, command, index, write);
            }
        }

        return VirtioBlockQueueMmioSetupRegisterWindowExecutionResult.Success(
            setupPlan,
            commands[setupPlan.CommandCount - 1],
            setupPlan.CommandCount);
    }

    private static VirtioBlockQueueMmioSetupRegisterWindowExecutionResult AccessFailure(
        VirtioBlockQueueMmioSetupPlanResult setupPlan,
        VirtioBlockQueueMmioSetupCommand command,
        int commandIndex,
        VirtioPciBarRegisterWindowAccessExecutionResult access)
    {
        return VirtioBlockQueueMmioSetupRegisterWindowExecutionResult.Fail(
            VirtioBlockQueueMmioSetupRegisterWindowExecutionStatus.AccessExecutionFailed,
            setupPlan.Status,
            VirtioPciBarRegisterWindowPlanStatus.Success,
            access.Status,
            command,
            commandIndex,
            commandsExecuted: commandIndex,
            access.Value);
    }

    private static ulong GetExpectedReadValue(
        VirtioBlockQueueMmioSetupPlan plan,
        VirtioBlockQueueMmioSetupCommand command)
    {
        return command.Kind == VirtioBlockQueueMmioSetupCommandKind.ReadQueueSize
            ? plan.DeviceMaximumQueueSize
            : command.Kind == VirtioBlockQueueMmioSetupCommandKind.ReadQueueNotifyOffset
                ? plan.NotifyOffset
                : command.Value;
    }

    private static VirtioPciRegisterAccessWidth GetRegisterWidth(uint widthBytes)
    {
        return widthBytes == sizeof(byte)
            ? VirtioPciRegisterAccessWidth.Byte
            : widthBytes == sizeof(ushort)
                ? VirtioPciRegisterAccessWidth.Word
                : widthBytes == sizeof(uint)
                    ? VirtioPciRegisterAccessWidth.DWord
                    : widthBytes == sizeof(ulong)
                        ? VirtioPciRegisterAccessWidth.QWord
                        : default;
    }
}
