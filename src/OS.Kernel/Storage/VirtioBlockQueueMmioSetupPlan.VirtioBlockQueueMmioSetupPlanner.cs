using OS.Drivers;

namespace OS.Storage;

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
