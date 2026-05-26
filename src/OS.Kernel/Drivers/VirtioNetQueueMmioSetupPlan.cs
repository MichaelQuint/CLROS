using OS.Std.Binary;

namespace OS.Drivers;

public enum VirtioNetQueueMmioSetupPlanStatus
{
    Success = 0,
    BringUpPlanFailed = 1,
    QueueUnavailable = 2,
    QueueSizeRejected = 3,
    InvalidPhysicalAddress = 4,
    PhysicalAddressOverflow = 5,
    MissingNotifyOffset = 6,
    CommandDestinationTooSmall = 7,
    UnsupportedNotificationDataFeature = 8
}

public enum VirtioNetQueueMmioSetupCommandKind
{
    WriteQueueSelect = 0,
    ReadQueueSize = 1,
    WriteQueueSize = 2,
    WriteQueueDescriptor = 3,
    WriteQueueDriver = 4,
    WriteQueueDevice = 5,
    ReadQueueNotifyOffset = 6,
    WriteQueueEnable = 7
}

public enum VirtioNetQueueMmioSetupAddressKind
{
    None = 0,
    DescriptorTable = 1,
    DriverRing = 2,
    DeviceRing = 3
}

public enum VirtioNetQueueMmioSetupExecutionStatus
{
    Success = 0,
    SetupPlanFailed = 1,
    CommandSourceTooSmall = 2,
    AccessorNotReady = 3,
    UnsupportedCommandWidth = 4,
    RegisterReadFailed = 5,
    RegisterReadMismatch = 6,
    RegisterWriteFailed = 7
}

public enum VirtioNetQueueMmioSetupRegisterWindowPlanStatus
{
    Success = 0,
    SetupPlanFailed = 1,
    CommandSourceTooSmall = 2,
    AccessPlanDestinationTooSmall = 3,
    UnsupportedCommandWidth = 4,
    RegisterWindowPlanFailed = 5
}

public enum VirtioNetQueueMmioSetupRegisterWindowExecutionStatus
{
    Success = 0,
    SetupPlanFailed = 1,
    CommandSourceTooSmall = 2,
    UnsupportedCommandWidth = 3,
    RegisterWindowPlanFailed = 4,
    AccessExecutionFailed = 5,
    RegisterReadMismatch = 6
}

public readonly struct VirtioNetQueueMmioSetupQueueAddresses
{
    public VirtioNetQueueMmioSetupQueueAddresses(
        ulong descriptorTablePhysicalAddress,
        ulong driverRingPhysicalAddress,
        ulong deviceRingPhysicalAddress)
    {
        DescriptorTablePhysicalAddress = descriptorTablePhysicalAddress;
        DriverRingPhysicalAddress = driverRingPhysicalAddress;
        DeviceRingPhysicalAddress = deviceRingPhysicalAddress;
    }

    public ulong DescriptorTablePhysicalAddress { get; }

    public ulong DriverRingPhysicalAddress { get; }

    public ulong DeviceRingPhysicalAddress { get; }
}

public readonly struct VirtioNetQueueMmioSetupQueueDeviceMetadata
{
    public VirtioNetQueueMmioSetupQueueDeviceMetadata(
        ushort deviceMaximumQueueSize,
        bool hasNotifyOffset,
        ushort notifyOffset)
    {
        DeviceMaximumQueueSize = deviceMaximumQueueSize;
        HasNotifyOffset = hasNotifyOffset;
        NotifyOffset = notifyOffset;
    }

    public ushort DeviceMaximumQueueSize { get; }

    public bool HasNotifyOffset { get; }

    public ushort NotifyOffset { get; }
}

public readonly struct VirtioNetQueueMmioSetupCommand
{
    internal VirtioNetQueueMmioSetupCommand(
        VirtioNetQueueMmioSetupCommandKind kind,
        ushort queueIndex,
        uint relativeOffset,
        uint widthBytes,
        ulong value)
    {
        Kind = kind;
        QueueIndex = queueIndex;
        RelativeOffset = relativeOffset;
        WidthBytes = widthBytes;
        Value = value;
    }

    public VirtioNetQueueMmioSetupCommandKind Kind { get; }

    public ushort QueueIndex { get; }

    public uint RelativeOffset { get; }

    public uint WidthBytes { get; }

    public ulong Value { get; }

    public bool ReadsRegister =>
        Kind == VirtioNetQueueMmioSetupCommandKind.ReadQueueSize ||
        Kind == VirtioNetQueueMmioSetupCommandKind.ReadQueueNotifyOffset;

    public bool WritesRegister => !ReadsRegister;
}

public readonly struct VirtioNetQueueMmioSetupQueuePlan
{
    internal VirtioNetQueueMmioSetupQueuePlan(
        ushort queueIndex,
        ushort plannedQueueSize,
        ushort deviceMaximumQueueSize,
        ushort notifyOffset,
        VirtioNetQueueMmioSetupQueueAddresses addresses,
        uint descriptorTableBytes,
        uint driverRingBytes,
        uint deviceRingBytes,
        int commandStartIndex,
        int commandCount)
    {
        QueueIndex = queueIndex;
        PlannedQueueSize = plannedQueueSize;
        DeviceMaximumQueueSize = deviceMaximumQueueSize;
        NotifyOffset = notifyOffset;
        Addresses = addresses;
        DescriptorTableBytes = descriptorTableBytes;
        DriverRingBytes = driverRingBytes;
        DeviceRingBytes = deviceRingBytes;
        CommandStartIndex = commandStartIndex;
        CommandCount = commandCount;
    }

    public ushort QueueIndex { get; }

    public ushort PlannedQueueSize { get; }

    public ushort DeviceMaximumQueueSize { get; }

    public ushort NotifyOffset { get; }

    public VirtioNetQueueMmioSetupQueueAddresses Addresses { get; }

    public uint DescriptorTableBytes { get; }

    public uint DriverRingBytes { get; }

    public uint DeviceRingBytes { get; }

    public int CommandStartIndex { get; }

    public int CommandCount { get; }
}

public readonly struct VirtioNetQueueMmioSetupPlan
{
    internal VirtioNetQueueMmioSetupPlan(
        VirtioNetQueueMmioSetupQueuePlan rxQueue,
        VirtioNetQueueMmioSetupQueuePlan txQueue,
        int commandCount)
    {
        RxQueue = rxQueue;
        TxQueue = txQueue;
        CommandCount = commandCount;
    }

    public VirtioNetQueueMmioSetupQueuePlan RxQueue { get; }

    public VirtioNetQueueMmioSetupQueuePlan TxQueue { get; }

    public int CommandCount { get; }
}

public readonly struct VirtioNetQueueMmioSetupPlanResult
{
    private VirtioNetQueueMmioSetupPlanResult(
        bool isInitialized,
        VirtioNetQueueMmioSetupPlanStatus status,
        VirtioNetDriverQueueBringUpPlanStatus bringUpStatus,
        ushort failingQueueIndex,
        VirtioNetQueueMmioSetupAddressKind failingAddressKind,
        ulong failingPhysicalAddress,
        ushort plannedQueueSize,
        ushort deviceMaximumQueueSize,
        ulong unsupportedFeatureMask,
        int requiredCommandCount,
        int availableCommandCount,
        int commandCount,
        VirtioNetQueueMmioSetupPlan plan)
    {
        IsInitialized = isInitialized;
        Status = status;
        BringUpStatus = bringUpStatus;
        FailingQueueIndex = failingQueueIndex;
        FailingAddressKind = failingAddressKind;
        FailingPhysicalAddress = failingPhysicalAddress;
        PlannedQueueSize = plannedQueueSize;
        DeviceMaximumQueueSize = deviceMaximumQueueSize;
        UnsupportedFeatureMask = unsupportedFeatureMask;
        RequiredCommandCount = requiredCommandCount;
        AvailableCommandCount = availableCommandCount;
        CommandCount = commandCount;
        Plan = plan;
    }

    public VirtioNetQueueMmioSetupPlanStatus Status { get; }

    public VirtioNetDriverQueueBringUpPlanStatus BringUpStatus { get; }

    public ushort FailingQueueIndex { get; }

    public VirtioNetQueueMmioSetupAddressKind FailingAddressKind { get; }

    public ulong FailingPhysicalAddress { get; }

    public ushort PlannedQueueSize { get; }

    public ushort DeviceMaximumQueueSize { get; }

    public ulong UnsupportedFeatureMask { get; }

    public int RequiredCommandCount { get; }

    public int AvailableCommandCount { get; }

    public int CommandCount { get; }

    public VirtioNetQueueMmioSetupPlan Plan { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == VirtioNetQueueMmioSetupPlanStatus.Success;

    internal static VirtioNetQueueMmioSetupPlanResult Success(
        VirtioNetQueueMmioSetupPlan plan,
        VirtioNetDriverQueueBringUpPlanStatus bringUpStatus,
        int availableCommandCount)
    {
        return new VirtioNetQueueMmioSetupPlanResult(
            isInitialized: true,
            VirtioNetQueueMmioSetupPlanStatus.Success,
            bringUpStatus,
            failingQueueIndex: 0,
            VirtioNetQueueMmioSetupAddressKind.None,
            failingPhysicalAddress: 0,
            plannedQueueSize: 0,
            deviceMaximumQueueSize: 0,
            unsupportedFeatureMask: 0,
            VirtioNetQueueMmioSetupPlanner.RequiredCommandCount,
            availableCommandCount,
            plan.CommandCount,
            plan);
    }

    internal static VirtioNetQueueMmioSetupPlanResult Fail(
        VirtioNetQueueMmioSetupPlanStatus status,
        VirtioNetDriverQueueBringUpPlanStatus bringUpStatus,
        ushort failingQueueIndex = 0,
        VirtioNetQueueMmioSetupAddressKind failingAddressKind = VirtioNetQueueMmioSetupAddressKind.None,
        ulong failingPhysicalAddress = 0,
        ushort plannedQueueSize = 0,
        ushort deviceMaximumQueueSize = 0,
        ulong unsupportedFeatureMask = 0,
        int availableCommandCount = 0)
    {
        return new VirtioNetQueueMmioSetupPlanResult(
            isInitialized: true,
            status,
            bringUpStatus,
            failingQueueIndex,
            failingAddressKind,
            failingPhysicalAddress,
            plannedQueueSize,
            deviceMaximumQueueSize,
            unsupportedFeatureMask,
            VirtioNetQueueMmioSetupPlanner.RequiredCommandCount,
            availableCommandCount,
            commandCount: 0,
            default);
    }
}

public readonly struct VirtioNetQueueMmioSetupExecutionResult
{
    private VirtioNetQueueMmioSetupExecutionResult(
        bool isInitialized,
        VirtioNetQueueMmioSetupExecutionStatus status,
        VirtioNetQueueMmioSetupPlanStatus setupPlanStatus,
        VirtioPciRegisterReadStatus registerReadStatus,
        VirtioPciRegisterWriteStatus registerWriteStatus,
        VirtioNetQueueMmioSetupCommandKind commandKind,
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

    public VirtioNetQueueMmioSetupExecutionStatus Status { get; }

    public VirtioNetQueueMmioSetupPlanStatus SetupPlanStatus { get; }

    public VirtioPciRegisterReadStatus RegisterReadStatus { get; }

    public VirtioPciRegisterWriteStatus RegisterWriteStatus { get; }

    public VirtioNetQueueMmioSetupCommandKind CommandKind { get; }

    public int CommandIndex { get; }

    public int CommandsExecuted { get; }

    public ushort QueueIndex { get; }

    public uint RelativeOffset { get; }

    public uint WidthBytes { get; }

    public ulong ExpectedValue { get; }

    public ulong ObservedValue { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == VirtioNetQueueMmioSetupExecutionStatus.Success;

    internal static VirtioNetQueueMmioSetupExecutionResult Success(
        VirtioNetQueueMmioSetupPlanResult setupPlan,
        int commandsExecuted)
    {
        return new VirtioNetQueueMmioSetupExecutionResult(
            isInitialized: true,
            VirtioNetQueueMmioSetupExecutionStatus.Success,
            setupPlan.Status,
            VirtioPciRegisterReadStatus.Success,
            VirtioPciRegisterWriteStatus.Success,
            VirtioNetQueueMmioSetupCommandKind.WriteQueueEnable,
            commandIndex: commandsExecuted - 1,
            commandsExecuted,
            queueIndex: 0,
            relativeOffset: 0,
            widthBytes: 0,
            expectedValue: 0,
            observedValue: 0);
    }

    internal static VirtioNetQueueMmioSetupExecutionResult Fail(
        VirtioNetQueueMmioSetupExecutionStatus status,
        VirtioNetQueueMmioSetupPlanStatus setupPlanStatus,
        VirtioPciRegisterReadStatus registerReadStatus,
        VirtioPciRegisterWriteStatus registerWriteStatus,
        VirtioNetQueueMmioSetupCommand command,
        int commandIndex,
        int commandsExecuted,
        ulong observedValue = 0)
    {
        return new VirtioNetQueueMmioSetupExecutionResult(
            isInitialized: true,
            status,
            setupPlanStatus,
            registerReadStatus,
            registerWriteStatus,
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

public readonly struct VirtioNetQueueMmioSetupRegisterWindowPlanResult
{
    private VirtioNetQueueMmioSetupRegisterWindowPlanResult(
        bool isInitialized,
        VirtioNetQueueMmioSetupRegisterWindowPlanStatus status,
        VirtioNetQueueMmioSetupPlanStatus setupPlanStatus,
        VirtioPciBarRegisterWindowPlanStatus registerWindowStatus,
        VirtioNetQueueMmioSetupCommandKind commandKind,
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

    public VirtioNetQueueMmioSetupRegisterWindowPlanStatus Status { get; }

    public VirtioNetQueueMmioSetupPlanStatus SetupPlanStatus { get; }

    public VirtioPciBarRegisterWindowPlanStatus RegisterWindowStatus { get; }

    public VirtioNetQueueMmioSetupCommandKind CommandKind { get; }

    public int CommandIndex { get; }

    public int AccessPlansWritten { get; }

    public int RequiredAccessPlanCount { get; }

    public int AvailableAccessPlanCount { get; }

    public ushort QueueIndex { get; }

    public uint RelativeOffset { get; }

    public uint WidthBytes { get; }

    public bool IsInitialized { get; }

    public bool Succeeded =>
        IsInitialized && Status == VirtioNetQueueMmioSetupRegisterWindowPlanStatus.Success;

    internal static VirtioNetQueueMmioSetupRegisterWindowPlanResult Success(
        VirtioNetQueueMmioSetupPlanResult setupPlan,
        int accessPlansWritten,
        int availableAccessPlanCount)
    {
        return new VirtioNetQueueMmioSetupRegisterWindowPlanResult(
            isInitialized: true,
            VirtioNetQueueMmioSetupRegisterWindowPlanStatus.Success,
            setupPlan.Status,
            VirtioPciBarRegisterWindowPlanStatus.Success,
            VirtioNetQueueMmioSetupCommandKind.WriteQueueEnable,
            commandIndex: accessPlansWritten - 1,
            accessPlansWritten,
            setupPlan.CommandCount,
            availableAccessPlanCount,
            queueIndex: 0,
            relativeOffset: 0,
            widthBytes: 0);
    }

    internal static VirtioNetQueueMmioSetupRegisterWindowPlanResult Fail(
        VirtioNetQueueMmioSetupRegisterWindowPlanStatus status,
        VirtioNetQueueMmioSetupPlanStatus setupPlanStatus,
        VirtioPciBarRegisterWindowPlanStatus registerWindowStatus,
        VirtioNetQueueMmioSetupCommand command,
        int commandIndex,
        int accessPlansWritten,
        int requiredAccessPlanCount,
        int availableAccessPlanCount)
    {
        return new VirtioNetQueueMmioSetupRegisterWindowPlanResult(
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

public readonly struct VirtioNetQueueMmioSetupRegisterWindowExecutionResult
{
    private VirtioNetQueueMmioSetupRegisterWindowExecutionResult(
        bool isInitialized,
        VirtioNetQueueMmioSetupRegisterWindowExecutionStatus status,
        VirtioNetQueueMmioSetupPlanStatus setupPlanStatus,
        VirtioPciBarRegisterWindowPlanStatus registerWindowStatus,
        VirtioPciBarRegisterWindowAccessExecutionStatus accessExecutionStatus,
        VirtioNetQueueMmioSetupCommandKind commandKind,
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

    public bool Succeeded =>
        IsInitialized && Status == VirtioNetQueueMmioSetupRegisterWindowExecutionStatus.Success;

    internal static VirtioNetQueueMmioSetupRegisterWindowExecutionResult Success(
        VirtioNetQueueMmioSetupPlanResult setupPlan,
        int commandsExecuted)
    {
        return new VirtioNetQueueMmioSetupRegisterWindowExecutionResult(
            isInitialized: true,
            VirtioNetQueueMmioSetupRegisterWindowExecutionStatus.Success,
            setupPlan.Status,
            VirtioPciBarRegisterWindowPlanStatus.Success,
            VirtioPciBarRegisterWindowAccessExecutionStatus.Success,
            VirtioNetQueueMmioSetupCommandKind.WriteQueueEnable,
            commandIndex: commandsExecuted - 1,
            commandsExecuted,
            queueIndex: 0,
            relativeOffset: 0,
            widthBytes: 0,
            expectedValue: 0,
            observedValue: 0);
    }

    internal static VirtioNetQueueMmioSetupRegisterWindowExecutionResult Fail(
        VirtioNetQueueMmioSetupRegisterWindowExecutionStatus status,
        VirtioNetQueueMmioSetupPlanStatus setupPlanStatus,
        VirtioPciBarRegisterWindowPlanStatus registerWindowStatus,
        VirtioPciBarRegisterWindowAccessExecutionStatus accessExecutionStatus,
        VirtioNetQueueMmioSetupCommand command,
        int commandIndex,
        int commandsExecuted,
        ulong observedValue = 0)
    {
        return new VirtioNetQueueMmioSetupRegisterWindowExecutionResult(
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

public static class VirtioNetQueueMmioSetupRegisterWindowPlanner
{
    public const bool RequiresRegisterWindowBinding = true;
    public const bool WritesAccessPlansToCallerStorage = true;
    public const bool MapsBars = false;
    public const bool PerformsLiveMmio = false;
    public const bool ContainsVolatileAccess = false;
    public const bool ReadsDeviceRegisters = false;
    public const bool WritesDeviceRegisters = false;
    public const bool ConfiguresQueues = false;
    public const bool NotifiesLiveDevice = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsDmaMapping = false;
    public const bool PerformsPacketIo = false;

    public static VirtioNetQueueMmioSetupRegisterWindowPlanResult PlanAccesses(
        VirtioNetQueueMmioSetupPlanResult setupPlan,
        ReadOnlySpan<VirtioNetQueueMmioSetupCommand> commands,
        VirtioPciBarRegisterWindowBinding commonConfiguration,
        Span<VirtioPciBarRegisterWindowAccessPlan> accessPlans)
    {
        if (!setupPlan.Succeeded)
        {
            return VirtioNetQueueMmioSetupRegisterWindowPlanResult.Fail(
                VirtioNetQueueMmioSetupRegisterWindowPlanStatus.SetupPlanFailed,
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
            return VirtioNetQueueMmioSetupRegisterWindowPlanResult.Fail(
                VirtioNetQueueMmioSetupRegisterWindowPlanStatus.CommandSourceTooSmall,
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
            return VirtioNetQueueMmioSetupRegisterWindowPlanResult.Fail(
                VirtioNetQueueMmioSetupRegisterWindowPlanStatus.AccessPlanDestinationTooSmall,
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
            VirtioNetQueueMmioSetupCommand command = commands[index];
            VirtioPciRegisterAccessWidth width = GetRegisterWidth(command.WidthBytes);
            if (width == default)
            {
                return VirtioNetQueueMmioSetupRegisterWindowPlanResult.Fail(
                    VirtioNetQueueMmioSetupRegisterWindowPlanStatus.UnsupportedCommandWidth,
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
                return VirtioNetQueueMmioSetupRegisterWindowPlanResult.Fail(
                    VirtioNetQueueMmioSetupRegisterWindowPlanStatus.RegisterWindowPlanFailed,
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

        return VirtioNetQueueMmioSetupRegisterWindowPlanResult.Success(
            setupPlan,
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

public static class VirtioNetQueueMmioSetupRegisterWindowExecutor
{
    public const bool RequiresRegisterWindowBinding = true;
    public const bool UsesCallerOwnedWindowBuffer = true;
    public const bool StagesExpectedDeviceReadValues = true;
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
    public const bool PerformsPacketIo = false;

    public static VirtioNetQueueMmioSetupRegisterWindowExecutionResult Execute(
        VirtioNetQueueMmioSetupPlanResult setupPlan,
        ReadOnlySpan<VirtioNetQueueMmioSetupCommand> commands,
        VirtioPciBarRegisterWindowBinding commonConfiguration,
        Span<byte> commonWindowBytes)
    {
        if (!setupPlan.Succeeded)
        {
            return VirtioNetQueueMmioSetupRegisterWindowExecutionResult.Fail(
                VirtioNetQueueMmioSetupRegisterWindowExecutionStatus.SetupPlanFailed,
                setupPlan.Status,
                VirtioPciBarRegisterWindowPlanStatus.Success,
                VirtioPciBarRegisterWindowAccessExecutionStatus.Success,
                default,
                commandIndex: -1,
                commandsExecuted: 0);
        }

        if (commands.Length < setupPlan.CommandCount)
        {
            return VirtioNetQueueMmioSetupRegisterWindowExecutionResult.Fail(
                VirtioNetQueueMmioSetupRegisterWindowExecutionStatus.CommandSourceTooSmall,
                setupPlan.Status,
                VirtioPciBarRegisterWindowPlanStatus.Success,
                VirtioPciBarRegisterWindowAccessExecutionStatus.Success,
                default,
                commandIndex: commands.Length,
                commandsExecuted: commands.Length);
        }

        for (int index = 0; index < setupPlan.CommandCount; index++)
        {
            VirtioNetQueueMmioSetupCommand command = commands[index];
            VirtioPciRegisterAccessWidth width = GetRegisterWidth(command.WidthBytes);
            if (width == default)
            {
                return VirtioNetQueueMmioSetupRegisterWindowExecutionResult.Fail(
                    VirtioNetQueueMmioSetupRegisterWindowExecutionStatus.UnsupportedCommandWidth,
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
                return VirtioNetQueueMmioSetupRegisterWindowExecutionResult.Fail(
                    VirtioNetQueueMmioSetupRegisterWindowExecutionStatus.RegisterWindowPlanFailed,
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
                    return VirtioNetQueueMmioSetupRegisterWindowExecutionResult.Fail(
                        VirtioNetQueueMmioSetupRegisterWindowExecutionStatus.RegisterReadMismatch,
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

        return VirtioNetQueueMmioSetupRegisterWindowExecutionResult.Success(
            setupPlan,
            setupPlan.CommandCount);
    }

    private static VirtioNetQueueMmioSetupRegisterWindowExecutionResult AccessFailure(
        VirtioNetQueueMmioSetupPlanResult setupPlan,
        VirtioNetQueueMmioSetupCommand command,
        int commandIndex,
        VirtioPciBarRegisterWindowAccessExecutionResult access)
    {
        return VirtioNetQueueMmioSetupRegisterWindowExecutionResult.Fail(
            VirtioNetQueueMmioSetupRegisterWindowExecutionStatus.AccessExecutionFailed,
            setupPlan.Status,
            VirtioPciBarRegisterWindowPlanStatus.Success,
            access.Status,
            command,
            commandIndex,
            commandsExecuted: commandIndex,
            access.Value);
    }

    private static ulong GetExpectedReadValue(
        VirtioNetQueueMmioSetupPlan plan,
        VirtioNetQueueMmioSetupCommand command)
    {
        VirtioNetQueueMmioSetupQueuePlan queue =
            command.QueueIndex == plan.RxQueue.QueueIndex
                ? plan.RxQueue
                : plan.TxQueue;

        return command.Kind == VirtioNetQueueMmioSetupCommandKind.ReadQueueSize
            ? queue.DeviceMaximumQueueSize
            : command.Kind == VirtioNetQueueMmioSetupCommandKind.ReadQueueNotifyOffset
                ? queue.NotifyOffset
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

public static class VirtioNetQueueMmioSetupExecutor
{
    public const bool RequiresInjectedAccessor = true;
    public const bool PerformsLiveMmio = false;
    public const bool MapsBars = false;
    public const bool PublishesDescriptors = false;
    public const bool NotifiesDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool PerformsDmaMapping = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsPacketIo = false;
    public const bool ReadsOrWritesPacketBuffers = false;

    public static VirtioNetQueueMmioSetupExecutionResult Execute(
        VirtioNetQueueMmioSetupPlanResult setupPlan,
        ReadOnlySpan<VirtioNetQueueMmioSetupCommand> commands,
        VirtioPciBarAperture commonAperture,
        IVirtioPciRegisterAccessor? accessor)
    {
        if (!setupPlan.Succeeded)
        {
            return VirtioNetQueueMmioSetupExecutionResult.Fail(
                VirtioNetQueueMmioSetupExecutionStatus.SetupPlanFailed,
                setupPlan.Status,
                VirtioPciRegisterReadStatus.NotReady,
                VirtioPciRegisterWriteStatus.NotReady,
                default,
                commandIndex: -1,
                commandsExecuted: 0);
        }

        if (commands.Length < setupPlan.CommandCount)
        {
            return VirtioNetQueueMmioSetupExecutionResult.Fail(
                VirtioNetQueueMmioSetupExecutionStatus.CommandSourceTooSmall,
                setupPlan.Status,
                VirtioPciRegisterReadStatus.NotReady,
                VirtioPciRegisterWriteStatus.NotReady,
                default,
                commandIndex: commands.Length,
                commandsExecuted: commands.Length);
        }

        if (accessor is null)
        {
            return VirtioNetQueueMmioSetupExecutionResult.Fail(
                VirtioNetQueueMmioSetupExecutionStatus.AccessorNotReady,
                setupPlan.Status,
                VirtioPciRegisterReadStatus.NotReady,
                VirtioPciRegisterWriteStatus.NotReady,
                commands[0],
                commandIndex: 0,
                commandsExecuted: 0);
        }

        for (int index = 0; index < setupPlan.CommandCount; index++)
        {
            VirtioNetQueueMmioSetupCommand command = commands[index];
            if (command.ReadsRegister)
            {
                if (command.WidthBytes != sizeof(ushort))
                {
                    return VirtioNetQueueMmioSetupExecutionResult.Fail(
                        VirtioNetQueueMmioSetupExecutionStatus.UnsupportedCommandWidth,
                        setupPlan.Status,
                        VirtioPciRegisterReadStatus.UnsupportedWidth,
                        VirtioPciRegisterWriteStatus.Success,
                        command,
                        index,
                        commandsExecuted: index);
                }

                VirtioPciRegisterRead16Result read =
                    accessor.Read16(commonAperture, command.RelativeOffset);
                if (!read.Succeeded)
                {
                    VirtioNetQueueMmioSetupExecutionStatus status =
                        read.Status == VirtioPciRegisterReadStatus.NotReady
                            ? VirtioNetQueueMmioSetupExecutionStatus.AccessorNotReady
                            : VirtioNetQueueMmioSetupExecutionStatus.RegisterReadFailed;

                    return VirtioNetQueueMmioSetupExecutionResult.Fail(
                        status,
                        setupPlan.Status,
                        read.Status,
                        VirtioPciRegisterWriteStatus.Success,
                        command,
                        index,
                        commandsExecuted: index,
                        read.Value);
                }

                if (read.Value != command.Value)
                {
                    return VirtioNetQueueMmioSetupExecutionResult.Fail(
                        VirtioNetQueueMmioSetupExecutionStatus.RegisterReadMismatch,
                        setupPlan.Status,
                        read.Status,
                        VirtioPciRegisterWriteStatus.Success,
                        command,
                        index,
                        commandsExecuted: index,
                        read.Value);
                }

                continue;
            }

            VirtioPciRegisterWriteStatus writeStatus;
            if (command.WidthBytes == sizeof(ushort))
            {
                writeStatus = accessor.Write16(
                    commonAperture,
                    command.RelativeOffset,
                    (ushort)command.Value);
            }
            else if (command.WidthBytes == sizeof(ulong))
            {
                writeStatus = accessor.Write64(
                    commonAperture,
                    command.RelativeOffset,
                    command.Value);
            }
            else
            {
                return VirtioNetQueueMmioSetupExecutionResult.Fail(
                    VirtioNetQueueMmioSetupExecutionStatus.UnsupportedCommandWidth,
                    setupPlan.Status,
                    VirtioPciRegisterReadStatus.Success,
                    VirtioPciRegisterWriteStatus.UnsupportedWidth,
                    command,
                    index,
                    commandsExecuted: index);
            }

            if (writeStatus != VirtioPciRegisterWriteStatus.Success)
            {
                VirtioNetQueueMmioSetupExecutionStatus status =
                    writeStatus == VirtioPciRegisterWriteStatus.NotReady
                        ? VirtioNetQueueMmioSetupExecutionStatus.AccessorNotReady
                        : VirtioNetQueueMmioSetupExecutionStatus.RegisterWriteFailed;

                return VirtioNetQueueMmioSetupExecutionResult.Fail(
                    status,
                    setupPlan.Status,
                    VirtioPciRegisterReadStatus.Success,
                    writeStatus,
                    command,
                    index,
                    commandsExecuted: index);
            }
        }

        return VirtioNetQueueMmioSetupExecutionResult.Success(
            setupPlan,
            setupPlan.CommandCount);
    }
}

public static class VirtioNetQueueMmioSetupPlanner
{
    public const int CommandsPerQueue = 8;
    public const int RequiredCommandCount = CommandsPerQueue * 2;

    public const int NotificationDataFeatureBit = 38;
    public const int NotificationConfigDataFeatureBit = 39;
    public const ulong NotificationDataFeatureMask = 1UL << NotificationDataFeatureBit;
    public const ulong NotificationConfigDataFeatureMask = 1UL << NotificationConfigDataFeatureBit;
    public const ulong UnsupportedNotificationFeatureMask =
        NotificationDataFeatureMask | NotificationConfigDataFeatureMask;

    public const bool InvokesRegisterAccessor = false;
    public const bool WritesMmioRegisters = false;
    public const bool PublishesDescriptors = false;
    public const bool NotifiesDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool SendsOrReceivesPackets = false;
    public const bool SupportsNotificationData = false;

    public static VirtioNetQueueMmioSetupPlanResult Create(
        VirtioNetDriverQueueBringUpPlanResult bringUpPlan,
        VirtioNetQueueMmioSetupQueueAddresses rxAddresses,
        VirtioNetQueueMmioSetupQueueAddresses txAddresses,
        VirtioNetQueueMmioSetupQueueDeviceMetadata rxDeviceMetadata,
        VirtioNetQueueMmioSetupQueueDeviceMetadata txDeviceMetadata,
        Span<VirtioNetQueueMmioSetupCommand> commands)
    {
        if (!bringUpPlan.Succeeded)
        {
            return VirtioNetQueueMmioSetupPlanResult.Fail(
                VirtioNetQueueMmioSetupPlanStatus.BringUpPlanFailed,
                bringUpPlan.Status,
                availableCommandCount: commands.Length);
        }

        VirtioNetDriverQueueBringUpMetadata bringUp = bringUpPlan.Plan;
        ulong unsupportedNotificationFeatures =
            bringUp.SelectedFeatures & UnsupportedNotificationFeatureMask;
        if (unsupportedNotificationFeatures != 0)
        {
            return VirtioNetQueueMmioSetupPlanResult.Fail(
                VirtioNetQueueMmioSetupPlanStatus.UnsupportedNotificationDataFeature,
                bringUpPlan.Status,
                unsupportedFeatureMask: unsupportedNotificationFeatures,
                availableCommandCount: commands.Length);
        }

        if (commands.Length < RequiredCommandCount)
        {
            return VirtioNetQueueMmioSetupPlanResult.Fail(
                VirtioNetQueueMmioSetupPlanStatus.CommandDestinationTooSmall,
                bringUpPlan.Status,
                availableCommandCount: commands.Length);
        }

        VirtioNetQueueMmioSetupPlanStatus rxStatus = ValidateQueue(
            bringUp.RxQueueIndex,
            bringUp.RxQueueSize,
            rxDeviceMetadata,
            rxAddresses,
            bringUp.QueuePlan.RxQueue,
            out VirtioNetQueueMmioSetupAddressKind failingAddressKind,
            out ulong failingAddress);
        if (rxStatus != VirtioNetQueueMmioSetupPlanStatus.Success)
        {
            return VirtioNetQueueMmioSetupPlanResult.Fail(
                rxStatus,
                bringUpPlan.Status,
                bringUp.RxQueueIndex,
                failingAddressKind,
                failingAddress,
                bringUp.RxQueueSize,
                rxDeviceMetadata.DeviceMaximumQueueSize,
                availableCommandCount: commands.Length);
        }

        VirtioNetQueueMmioSetupPlanStatus txStatus = ValidateQueue(
            bringUp.TxQueueIndex,
            bringUp.TxQueueSize,
            txDeviceMetadata,
            txAddresses,
            bringUp.QueuePlan.TxQueue,
            out failingAddressKind,
            out failingAddress);
        if (txStatus != VirtioNetQueueMmioSetupPlanStatus.Success)
        {
            return VirtioNetQueueMmioSetupPlanResult.Fail(
                txStatus,
                bringUpPlan.Status,
                bringUp.TxQueueIndex,
                failingAddressKind,
                failingAddress,
                bringUp.TxQueueSize,
                txDeviceMetadata.DeviceMaximumQueueSize,
                availableCommandCount: commands.Length);
        }

        VirtioNetQueueMmioSetupQueuePlan rxQueuePlan = CreateQueuePlan(
            bringUp.RxQueueIndex,
            bringUp.RxQueueSize,
            rxDeviceMetadata,
            rxAddresses,
            bringUp.QueuePlan.RxQueue,
            commandStartIndex: 0);
        VirtioNetQueueMmioSetupQueuePlan txQueuePlan = CreateQueuePlan(
            bringUp.TxQueueIndex,
            bringUp.TxQueueSize,
            txDeviceMetadata,
            txAddresses,
            bringUp.QueuePlan.TxQueue,
            commandStartIndex: CommandsPerQueue);

        WriteQueueCommands(rxQueuePlan, commands);
        WriteQueueCommands(txQueuePlan, commands);

        VirtioNetQueueMmioSetupPlan plan = new VirtioNetQueueMmioSetupPlan(
            rxQueuePlan,
            txQueuePlan,
            RequiredCommandCount);
        return VirtioNetQueueMmioSetupPlanResult.Success(
            plan,
            bringUpPlan.Status,
            commands.Length);
    }

    private static VirtioNetQueueMmioSetupPlanStatus ValidateQueue(
        ushort queueIndex,
        ushort plannedQueueSize,
        VirtioNetQueueMmioSetupQueueDeviceMetadata deviceMetadata,
        VirtioNetQueueMmioSetupQueueAddresses addresses,
        VirtqueueSplitRingPlan ringPlan,
        out VirtioNetQueueMmioSetupAddressKind failingAddressKind,
        out ulong failingAddress)
    {
        failingAddressKind = VirtioNetQueueMmioSetupAddressKind.None;
        failingAddress = 0;

        if (deviceMetadata.DeviceMaximumQueueSize == 0)
        {
            return VirtioNetQueueMmioSetupPlanStatus.QueueUnavailable;
        }

        if (plannedQueueSize > deviceMetadata.DeviceMaximumQueueSize)
        {
            return VirtioNetQueueMmioSetupPlanStatus.QueueSizeRejected;
        }

        VirtioNetQueueMmioSetupPlanStatus descriptorStatus = ValidateAddress(
            addresses.DescriptorTablePhysicalAddress,
            ringPlan.DescriptorTable.Length,
            ringPlan.DescriptorTable.Alignment);
        if (descriptorStatus != VirtioNetQueueMmioSetupPlanStatus.Success)
        {
            failingAddressKind = VirtioNetQueueMmioSetupAddressKind.DescriptorTable;
            failingAddress = addresses.DescriptorTablePhysicalAddress;
            return descriptorStatus;
        }

        VirtioNetQueueMmioSetupPlanStatus driverStatus = ValidateAddress(
            addresses.DriverRingPhysicalAddress,
            ringPlan.AvailableRing.Length,
            ringPlan.AvailableRing.Alignment);
        if (driverStatus != VirtioNetQueueMmioSetupPlanStatus.Success)
        {
            failingAddressKind = VirtioNetQueueMmioSetupAddressKind.DriverRing;
            failingAddress = addresses.DriverRingPhysicalAddress;
            return driverStatus;
        }

        VirtioNetQueueMmioSetupPlanStatus deviceStatus = ValidateAddress(
            addresses.DeviceRingPhysicalAddress,
            ringPlan.UsedRing.Length,
            ringPlan.UsedRing.Alignment);
        if (deviceStatus != VirtioNetQueueMmioSetupPlanStatus.Success)
        {
            failingAddressKind = VirtioNetQueueMmioSetupAddressKind.DeviceRing;
            failingAddress = addresses.DeviceRingPhysicalAddress;
            return deviceStatus;
        }

        if (!deviceMetadata.HasNotifyOffset)
        {
            return VirtioNetQueueMmioSetupPlanStatus.MissingNotifyOffset;
        }

        _ = queueIndex;
        return VirtioNetQueueMmioSetupPlanStatus.Success;
    }

    private static VirtioNetQueueMmioSetupPlanStatus ValidateAddress(
        ulong physicalAddress,
        uint byteLength,
        uint alignment)
    {
        if (physicalAddress == 0)
        {
            return VirtioNetQueueMmioSetupPlanStatus.InvalidPhysicalAddress;
        }

        if (alignment != 0 && (physicalAddress % alignment) != 0)
        {
            return VirtioNetQueueMmioSetupPlanStatus.InvalidPhysicalAddress;
        }

        if (byteLength != 0 && physicalAddress > ulong.MaxValue - ((ulong)byteLength - 1UL))
        {
            return VirtioNetQueueMmioSetupPlanStatus.PhysicalAddressOverflow;
        }

        return VirtioNetQueueMmioSetupPlanStatus.Success;
    }

    private static VirtioNetQueueMmioSetupQueuePlan CreateQueuePlan(
        ushort queueIndex,
        ushort plannedQueueSize,
        VirtioNetQueueMmioSetupQueueDeviceMetadata deviceMetadata,
        VirtioNetQueueMmioSetupQueueAddresses addresses,
        VirtqueueSplitRingPlan ringPlan,
        int commandStartIndex)
    {
        return new VirtioNetQueueMmioSetupQueuePlan(
            queueIndex,
            plannedQueueSize,
            deviceMetadata.DeviceMaximumQueueSize,
            deviceMetadata.NotifyOffset,
            addresses,
            ringPlan.DescriptorTable.Length,
            ringPlan.AvailableRing.Length,
            ringPlan.UsedRing.Length,
            commandStartIndex,
            CommandsPerQueue);
    }

    private static void WriteQueueCommands(
        VirtioNetQueueMmioSetupQueuePlan queue,
        Span<VirtioNetQueueMmioSetupCommand> commands)
    {
        int index = queue.CommandStartIndex;
        commands[index] = CreateWrite(
            VirtioNetQueueMmioSetupCommandKind.WriteQueueSelect,
            queue.QueueIndex,
            VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectOffset,
            VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectWidth,
            queue.QueueIndex);
        commands[index + 1] = CreateRead(
            VirtioNetQueueMmioSetupCommandKind.ReadQueueSize,
            queue.QueueIndex,
            VirtioPciModernRegisterMap.CommonConfiguration.QueueSizeOffset,
            VirtioPciModernRegisterMap.CommonConfiguration.QueueSizeWidth,
            queue.DeviceMaximumQueueSize);
        commands[index + 2] = CreateWrite(
            VirtioNetQueueMmioSetupCommandKind.WriteQueueSize,
            queue.QueueIndex,
            VirtioPciModernRegisterMap.CommonConfiguration.QueueSizeOffset,
            VirtioPciModernRegisterMap.CommonConfiguration.QueueSizeWidth,
            queue.PlannedQueueSize);
        commands[index + 3] = CreateWrite(
            VirtioNetQueueMmioSetupCommandKind.WriteQueueDescriptor,
            queue.QueueIndex,
            VirtioPciModernRegisterMap.CommonConfiguration.QueueDescOffset,
            VirtioPciModernRegisterMap.CommonConfiguration.QueueDescWidth,
            queue.Addresses.DescriptorTablePhysicalAddress);
        commands[index + 4] = CreateWrite(
            VirtioNetQueueMmioSetupCommandKind.WriteQueueDriver,
            queue.QueueIndex,
            VirtioPciModernRegisterMap.CommonConfiguration.QueueDriverOffset,
            VirtioPciModernRegisterMap.CommonConfiguration.QueueDriverWidth,
            queue.Addresses.DriverRingPhysicalAddress);
        commands[index + 5] = CreateWrite(
            VirtioNetQueueMmioSetupCommandKind.WriteQueueDevice,
            queue.QueueIndex,
            VirtioPciModernRegisterMap.CommonConfiguration.QueueDeviceOffset,
            VirtioPciModernRegisterMap.CommonConfiguration.QueueDeviceWidth,
            queue.Addresses.DeviceRingPhysicalAddress);
        commands[index + 6] = CreateRead(
            VirtioNetQueueMmioSetupCommandKind.ReadQueueNotifyOffset,
            queue.QueueIndex,
            VirtioPciModernRegisterMap.CommonConfiguration.QueueNotifyOffOffset,
            VirtioPciModernRegisterMap.CommonConfiguration.QueueNotifyOffWidth,
            queue.NotifyOffset);
        commands[index + 7] = CreateWrite(
            VirtioNetQueueMmioSetupCommandKind.WriteQueueEnable,
            queue.QueueIndex,
            VirtioPciModernRegisterMap.CommonConfiguration.QueueEnableOffset,
            VirtioPciModernRegisterMap.CommonConfiguration.QueueEnableWidth,
            value: 1);
    }

    private static VirtioNetQueueMmioSetupCommand CreateRead(
        VirtioNetQueueMmioSetupCommandKind kind,
        ushort queueIndex,
        uint relativeOffset,
        uint widthBytes,
        ulong value)
    {
        return new VirtioNetQueueMmioSetupCommand(
            kind,
            queueIndex,
            relativeOffset,
            widthBytes,
            value);
    }

    private static VirtioNetQueueMmioSetupCommand CreateWrite(
        VirtioNetQueueMmioSetupCommandKind kind,
        ushort queueIndex,
        uint relativeOffset,
        uint widthBytes,
        ulong value)
    {
        return new VirtioNetQueueMmioSetupCommand(
            kind,
            queueIndex,
            relativeOffset,
            widthBytes,
            value);
    }
}
