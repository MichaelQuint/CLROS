using OS.Drivers;

namespace OS.Storage;

public enum VirtioBlockQueueMmioSetupPlanStatus
{
    Success = 0,
    DescriptorNotReady = 1,
    NotBlockDevice = 2,
    QueuePlanFailed = 3,
    QueueUnavailable = 4,
    QueueSizeRejected = 5,
    InvalidPhysicalAddress = 6,
    PhysicalAddressOverflow = 7,
    MissingNotifyOffset = 8,
    CommandDestinationTooSmall = 9
}

public enum VirtioBlockQueueMmioSetupCommandKind
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

public enum VirtioBlockQueueMmioSetupAddressKind
{
    None = 0,
    DescriptorTable = 1,
    DriverRing = 2,
    DeviceRing = 3
}

public readonly struct VirtioBlockQueueMmioSetupQueueAddresses
{
    public VirtioBlockQueueMmioSetupQueueAddresses(
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

public readonly struct VirtioBlockQueueMmioSetupQueueDeviceMetadata
{
    public VirtioBlockQueueMmioSetupQueueDeviceMetadata(
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

public readonly struct VirtioBlockQueueMmioSetupCommand
{
    internal VirtioBlockQueueMmioSetupCommand(
        VirtioBlockQueueMmioSetupCommandKind kind,
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

    public VirtioBlockQueueMmioSetupCommandKind Kind { get; }

    public ushort QueueIndex { get; }

    public uint RelativeOffset { get; }

    public uint WidthBytes { get; }

    public ulong Value { get; }

    public bool ReadsRegister =>
        Kind == VirtioBlockQueueMmioSetupCommandKind.ReadQueueSize ||
        Kind == VirtioBlockQueueMmioSetupCommandKind.ReadQueueNotifyOffset;

    public bool WritesRegister => !ReadsRegister;
}

public readonly struct VirtioBlockQueueMmioSetupPlan
{
    internal VirtioBlockQueueMmioSetupPlan(
        ushort queueIndex,
        ushort plannedQueueSize,
        ushort deviceMaximumQueueSize,
        ushort notifyOffset,
        VirtioBlockQueueMmioSetupQueueAddresses addresses,
        uint descriptorTableBytes,
        uint driverRingBytes,
        uint deviceRingBytes,
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
        CommandCount = commandCount;
    }

    public ushort QueueIndex { get; }

    public ushort PlannedQueueSize { get; }

    public ushort DeviceMaximumQueueSize { get; }

    public ushort NotifyOffset { get; }

    public VirtioBlockQueueMmioSetupQueueAddresses Addresses { get; }

    public uint DescriptorTableBytes { get; }

    public uint DriverRingBytes { get; }

    public uint DeviceRingBytes { get; }

    public int CommandCount { get; }
}

public readonly struct VirtioBlockQueueMmioSetupPlanResult
{
    private VirtioBlockQueueMmioSetupPlanResult(
        bool isInitialized,
        VirtioBlockQueueMmioSetupPlanStatus status,
        VirtioPciDeviceKind deviceKind,
        VirtioPciBindingPlanStatus bindingStatus,
        VirtioBlockDeviceConfigStatus blockConfigurationStatus,
        VirtioDeviceInitializationProfileStatus initializationProfileStatus,
        VirtqueueSplitRingPlanStatus queuePlanStatus,
        VirtioBlockQueueMmioSetupAddressKind failingAddressKind,
        ulong failingPhysicalAddress,
        ushort plannedQueueSize,
        ushort deviceMaximumQueueSize,
        int requiredCommandCount,
        int availableCommandCount,
        int commandCount,
        VirtioBlockQueueMmioSetupPlan plan)
    {
        IsInitialized = isInitialized;
        Status = status;
        DeviceKind = deviceKind;
        BindingStatus = bindingStatus;
        BlockConfigurationStatus = blockConfigurationStatus;
        InitializationProfileStatus = initializationProfileStatus;
        QueuePlanStatus = queuePlanStatus;
        FailingAddressKind = failingAddressKind;
        FailingPhysicalAddress = failingPhysicalAddress;
        PlannedQueueSize = plannedQueueSize;
        DeviceMaximumQueueSize = deviceMaximumQueueSize;
        RequiredCommandCount = requiredCommandCount;
        AvailableCommandCount = availableCommandCount;
        CommandCount = commandCount;
        Plan = plan;
    }

    public bool IsInitialized { get; }

    public VirtioBlockQueueMmioSetupPlanStatus Status { get; }

    public VirtioPciDeviceKind DeviceKind { get; }

    public VirtioPciBindingPlanStatus BindingStatus { get; }

    public VirtioBlockDeviceConfigStatus BlockConfigurationStatus { get; }

    public VirtioDeviceInitializationProfileStatus InitializationProfileStatus { get; }

    public VirtqueueSplitRingPlanStatus QueuePlanStatus { get; }

    public VirtioBlockQueueMmioSetupAddressKind FailingAddressKind { get; }

    public ulong FailingPhysicalAddress { get; }

    public ushort PlannedQueueSize { get; }

    public ushort DeviceMaximumQueueSize { get; }

    public int RequiredCommandCount { get; }

    public int AvailableCommandCount { get; }

    public int CommandCount { get; }

    public VirtioBlockQueueMmioSetupPlan Plan { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == VirtioBlockQueueMmioSetupPlanStatus.Success;

    internal static VirtioBlockQueueMmioSetupPlanResult Success(
        VirtioDeviceDescriptor descriptor,
        VirtioBlockQueueMmioSetupPlan plan,
        int availableCommandCount)
    {
        return new VirtioBlockQueueMmioSetupPlanResult(
            isInitialized: true,
            VirtioBlockQueueMmioSetupPlanStatus.Success,
            descriptor.DeviceKind,
            descriptor.BindingPlan.Status,
            descriptor.BlockConfiguration.Status,
            descriptor.InitializationProfile.Status,
            VirtqueueSplitRingPlanStatus.Success,
            VirtioBlockQueueMmioSetupAddressKind.None,
            failingPhysicalAddress: 0,
            plannedQueueSize: plan.PlannedQueueSize,
            deviceMaximumQueueSize: plan.DeviceMaximumQueueSize,
            VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount,
            availableCommandCount,
            plan.CommandCount,
            plan);
    }

    internal static VirtioBlockQueueMmioSetupPlanResult Fail(
        VirtioBlockQueueMmioSetupPlanStatus status,
        VirtioDeviceDescriptor descriptor,
        VirtqueueSplitRingPlanStatus queuePlanStatus = VirtqueueSplitRingPlanStatus.Success,
        VirtioBlockQueueMmioSetupAddressKind failingAddressKind = VirtioBlockQueueMmioSetupAddressKind.None,
        ulong failingPhysicalAddress = 0,
        ushort plannedQueueSize = 0,
        ushort deviceMaximumQueueSize = 0,
        int availableCommandCount = 0)
    {
        return new VirtioBlockQueueMmioSetupPlanResult(
            isInitialized: true,
            status,
            descriptor.DeviceKind,
            descriptor.BindingPlan.Status,
            descriptor.BlockConfiguration.Status,
            descriptor.InitializationProfile.Status,
            queuePlanStatus,
            failingAddressKind,
            failingPhysicalAddress,
            plannedQueueSize,
            deviceMaximumQueueSize,
            VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount,
            availableCommandCount,
            commandCount: 0,
            default);
    }
}

public static class VirtioBlockQueueMmioSetupPlanner
{
    public const ushort QueueIndex = 0;
    public const int CommandsPerQueue = 8;
    public const int RequiredCommandCount = CommandsPerQueue;

    public const bool ConsumesVirtioDeviceDescriptor = true;
    public const bool PlansQueueZero = true;
    public const bool InvokesRegisterAccessor = false;
    public const bool WritesMmioRegisters = false;
    public const bool ReadsLiveDeviceRegisters = false;
    public const bool WritesLiveDeviceRegisters = false;
    public const bool PerformsLiveMmio = false;
    public const bool MapsBars = false;
    public const bool PublishesDescriptors = false;
    public const bool ConfiguresLiveQueues = false;
    public const bool NotifiesDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsDmaMapping = false;
    public const bool PerformsBlockIo = false;
    public const bool PerformsDiskRead = false;
    public const bool PerformsDiskWrite = false;

    public static VirtioBlockQueueMmioSetupPlanResult Create(
        VirtioDeviceDescriptor descriptor,
        ushort queueSize,
        VirtioBlockQueueMmioSetupQueueAddresses addresses,
        VirtioBlockQueueMmioSetupQueueDeviceMetadata deviceMetadata,
        Span<VirtioBlockQueueMmioSetupCommand> commands)
    {
        VirtioBlockQueueMmioSetupPlanStatus descriptorStatus = ValidateDescriptor(descriptor);
        if (descriptorStatus != VirtioBlockQueueMmioSetupPlanStatus.Success)
        {
            return VirtioBlockQueueMmioSetupPlanResult.Fail(
                descriptorStatus,
                descriptor,
                availableCommandCount: commands.Length);
        }

        if (commands.Length < RequiredCommandCount)
        {
            return VirtioBlockQueueMmioSetupPlanResult.Fail(
                VirtioBlockQueueMmioSetupPlanStatus.CommandDestinationTooSmall,
                descriptor,
                plannedQueueSize: queueSize,
                deviceMaximumQueueSize: deviceMetadata.DeviceMaximumQueueSize,
                availableCommandCount: commands.Length);
        }

        VirtqueueSplitRingPlanResult queuePlan = VirtqueueSplitRingPlan.Create(queueSize);
        if (!queuePlan.Succeeded)
        {
            return VirtioBlockQueueMmioSetupPlanResult.Fail(
                VirtioBlockQueueMmioSetupPlanStatus.QueuePlanFailed,
                descriptor,
                queuePlan.Status,
                plannedQueueSize: queueSize,
                deviceMaximumQueueSize: deviceMetadata.DeviceMaximumQueueSize,
                availableCommandCount: commands.Length);
        }

        VirtioBlockQueueMmioSetupPlanStatus queueStatus = ValidateQueue(
            queueSize,
            deviceMetadata,
            addresses,
            queuePlan.Plan,
            out VirtioBlockQueueMmioSetupAddressKind failingAddressKind,
            out ulong failingAddress);
        if (queueStatus != VirtioBlockQueueMmioSetupPlanStatus.Success)
        {
            return VirtioBlockQueueMmioSetupPlanResult.Fail(
                queueStatus,
                descriptor,
                VirtqueueSplitRingPlanStatus.Success,
                failingAddressKind,
                failingAddress,
                queueSize,
                deviceMetadata.DeviceMaximumQueueSize,
                commands.Length);
        }

        VirtioBlockQueueMmioSetupPlan plan = new VirtioBlockQueueMmioSetupPlan(
            QueueIndex,
            queueSize,
            deviceMetadata.DeviceMaximumQueueSize,
            deviceMetadata.NotifyOffset,
            addresses,
            queuePlan.Plan.DescriptorTable.Length,
            queuePlan.Plan.AvailableRing.Length,
            queuePlan.Plan.UsedRing.Length,
            RequiredCommandCount);

        WriteQueueCommands(plan, commands);
        return VirtioBlockQueueMmioSetupPlanResult.Success(descriptor, plan, commands.Length);
    }

    private static VirtioBlockQueueMmioSetupPlanStatus ValidateDescriptor(
        VirtioDeviceDescriptor descriptor)
    {
        if (!descriptor.BindingPlan.Succeeded ||
            !descriptor.InitializationProfile.Succeeded)
        {
            return VirtioBlockQueueMmioSetupPlanStatus.DescriptorNotReady;
        }

        if (descriptor.DeviceKind != VirtioPciDeviceKind.Block)
        {
            return VirtioBlockQueueMmioSetupPlanStatus.NotBlockDevice;
        }

        if (!descriptor.BlockConfiguration.Succeeded)
        {
            return VirtioBlockQueueMmioSetupPlanStatus.DescriptorNotReady;
        }

        return VirtioBlockQueueMmioSetupPlanStatus.Success;
    }

    private static VirtioBlockQueueMmioSetupPlanStatus ValidateQueue(
        ushort queueSize,
        VirtioBlockQueueMmioSetupQueueDeviceMetadata deviceMetadata,
        VirtioBlockQueueMmioSetupQueueAddresses addresses,
        VirtqueueSplitRingPlan ringPlan,
        out VirtioBlockQueueMmioSetupAddressKind failingAddressKind,
        out ulong failingAddress)
    {
        failingAddressKind = VirtioBlockQueueMmioSetupAddressKind.None;
        failingAddress = 0;

        if (deviceMetadata.DeviceMaximumQueueSize == 0)
        {
            return VirtioBlockQueueMmioSetupPlanStatus.QueueUnavailable;
        }

        if (queueSize < VirtioBlockRequestPlan.RequiredDescriptorCount ||
            queueSize > deviceMetadata.DeviceMaximumQueueSize)
        {
            return VirtioBlockQueueMmioSetupPlanStatus.QueueSizeRejected;
        }

        VirtioBlockQueueMmioSetupPlanStatus descriptorStatus = ValidateAddress(
            addresses.DescriptorTablePhysicalAddress,
            ringPlan.DescriptorTable.Length,
            ringPlan.DescriptorTable.Alignment);
        if (descriptorStatus != VirtioBlockQueueMmioSetupPlanStatus.Success)
        {
            failingAddressKind = VirtioBlockQueueMmioSetupAddressKind.DescriptorTable;
            failingAddress = addresses.DescriptorTablePhysicalAddress;
            return descriptorStatus;
        }

        VirtioBlockQueueMmioSetupPlanStatus driverStatus = ValidateAddress(
            addresses.DriverRingPhysicalAddress,
            ringPlan.AvailableRing.Length,
            ringPlan.AvailableRing.Alignment);
        if (driverStatus != VirtioBlockQueueMmioSetupPlanStatus.Success)
        {
            failingAddressKind = VirtioBlockQueueMmioSetupAddressKind.DriverRing;
            failingAddress = addresses.DriverRingPhysicalAddress;
            return driverStatus;
        }

        VirtioBlockQueueMmioSetupPlanStatus deviceStatus = ValidateAddress(
            addresses.DeviceRingPhysicalAddress,
            ringPlan.UsedRing.Length,
            ringPlan.UsedRing.Alignment);
        if (deviceStatus != VirtioBlockQueueMmioSetupPlanStatus.Success)
        {
            failingAddressKind = VirtioBlockQueueMmioSetupAddressKind.DeviceRing;
            failingAddress = addresses.DeviceRingPhysicalAddress;
            return deviceStatus;
        }

        if (!deviceMetadata.HasNotifyOffset)
        {
            return VirtioBlockQueueMmioSetupPlanStatus.MissingNotifyOffset;
        }

        return VirtioBlockQueueMmioSetupPlanStatus.Success;
    }

    private static VirtioBlockQueueMmioSetupPlanStatus ValidateAddress(
        ulong physicalAddress,
        uint byteLength,
        uint alignment)
    {
        if (physicalAddress == 0)
        {
            return VirtioBlockQueueMmioSetupPlanStatus.InvalidPhysicalAddress;
        }

        if (alignment != 0 && (physicalAddress % alignment) != 0)
        {
            return VirtioBlockQueueMmioSetupPlanStatus.InvalidPhysicalAddress;
        }

        if (byteLength != 0 &&
            physicalAddress > ulong.MaxValue - ((ulong)byteLength - 1UL))
        {
            return VirtioBlockQueueMmioSetupPlanStatus.PhysicalAddressOverflow;
        }

        return VirtioBlockQueueMmioSetupPlanStatus.Success;
    }

    private static void WriteQueueCommands(
        VirtioBlockQueueMmioSetupPlan plan,
        Span<VirtioBlockQueueMmioSetupCommand> commands)
    {
        commands[0] = CreateWrite(
            VirtioBlockQueueMmioSetupCommandKind.WriteQueueSelect,
            plan.QueueIndex,
            VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectOffset,
            VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectWidth,
            plan.QueueIndex);
        commands[1] = CreateRead(
            VirtioBlockQueueMmioSetupCommandKind.ReadQueueSize,
            plan.QueueIndex,
            VirtioPciModernRegisterMap.CommonConfiguration.QueueSizeOffset,
            VirtioPciModernRegisterMap.CommonConfiguration.QueueSizeWidth,
            plan.DeviceMaximumQueueSize);
        commands[2] = CreateWrite(
            VirtioBlockQueueMmioSetupCommandKind.WriteQueueSize,
            plan.QueueIndex,
            VirtioPciModernRegisterMap.CommonConfiguration.QueueSizeOffset,
            VirtioPciModernRegisterMap.CommonConfiguration.QueueSizeWidth,
            plan.PlannedQueueSize);
        commands[3] = CreateWrite(
            VirtioBlockQueueMmioSetupCommandKind.WriteQueueDescriptor,
            plan.QueueIndex,
            VirtioPciModernRegisterMap.CommonConfiguration.QueueDescOffset,
            VirtioPciModernRegisterMap.CommonConfiguration.QueueDescWidth,
            plan.Addresses.DescriptorTablePhysicalAddress);
        commands[4] = CreateWrite(
            VirtioBlockQueueMmioSetupCommandKind.WriteQueueDriver,
            plan.QueueIndex,
            VirtioPciModernRegisterMap.CommonConfiguration.QueueDriverOffset,
            VirtioPciModernRegisterMap.CommonConfiguration.QueueDriverWidth,
            plan.Addresses.DriverRingPhysicalAddress);
        commands[5] = CreateWrite(
            VirtioBlockQueueMmioSetupCommandKind.WriteQueueDevice,
            plan.QueueIndex,
            VirtioPciModernRegisterMap.CommonConfiguration.QueueDeviceOffset,
            VirtioPciModernRegisterMap.CommonConfiguration.QueueDeviceWidth,
            plan.Addresses.DeviceRingPhysicalAddress);
        commands[6] = CreateRead(
            VirtioBlockQueueMmioSetupCommandKind.ReadQueueNotifyOffset,
            plan.QueueIndex,
            VirtioPciModernRegisterMap.CommonConfiguration.QueueNotifyOffOffset,
            VirtioPciModernRegisterMap.CommonConfiguration.QueueNotifyOffWidth,
            plan.NotifyOffset);
        commands[7] = CreateWrite(
            VirtioBlockQueueMmioSetupCommandKind.WriteQueueEnable,
            plan.QueueIndex,
            VirtioPciModernRegisterMap.CommonConfiguration.QueueEnableOffset,
            VirtioPciModernRegisterMap.CommonConfiguration.QueueEnableWidth,
            value: 1);
    }

    private static VirtioBlockQueueMmioSetupCommand CreateRead(
        VirtioBlockQueueMmioSetupCommandKind kind,
        ushort queueIndex,
        uint relativeOffset,
        uint widthBytes,
        ulong value)
    {
        return new VirtioBlockQueueMmioSetupCommand(
            kind,
            queueIndex,
            relativeOffset,
            widthBytes,
            value);
    }

    private static VirtioBlockQueueMmioSetupCommand CreateWrite(
        VirtioBlockQueueMmioSetupCommandKind kind,
        ushort queueIndex,
        uint relativeOffset,
        uint widthBytes,
        ulong value)
    {
        return new VirtioBlockQueueMmioSetupCommand(
            kind,
            queueIndex,
            relativeOffset,
            widthBytes,
            value);
    }
}
