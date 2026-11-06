using OS.Std.Binary;

namespace OS.Drivers;

public static class VirtioNetQueueMmioSetupPlanner
{
    public const int CommandsPerQueue = 8;
    public const int RequiredCommandCount = CommandsPerQueue * 2;
    public const int NotificationDataFeatureBit = 38;
    public const int NotificationConfigDataFeatureBit = 39;
    public const ulong NotificationDataFeatureMask = 1UL << NotificationDataFeatureBit;
    public const ulong NotificationConfigDataFeatureMask = 1UL << NotificationConfigDataFeatureBit;
    public const ulong UnsupportedNotificationFeatureMask = NotificationDataFeatureMask | NotificationConfigDataFeatureMask;
    public const bool InvokesRegisterAccessor = false;
    public const bool WritesMmioRegisters = false;
    public const bool PublishesDescriptors = false;
    public const bool NotifiesDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool SendsOrReceivesPackets = false;
    public const bool SupportsNotificationData = false;
    public static VirtioNetQueueMmioSetupPlanResult Create(VirtioNetDriverQueueBringUpPlanResult bringUpPlan, VirtioNetQueueMmioSetupQueueAddresses rxAddresses, VirtioNetQueueMmioSetupQueueAddresses txAddresses, VirtioNetQueueMmioSetupQueueDeviceMetadata rxDeviceMetadata, VirtioNetQueueMmioSetupQueueDeviceMetadata txDeviceMetadata, Span<VirtioNetQueueMmioSetupCommand> commands)
    {
        if (!bringUpPlan.Succeeded)
        {
            return VirtioNetQueueMmioSetupPlanResult.Fail(VirtioNetQueueMmioSetupPlanStatus.BringUpPlanFailed, bringUpPlan.Status, availableCommandCount: commands.Length);
        }

        VirtioNetDriverQueueBringUpMetadata bringUp = bringUpPlan.Plan;
        ulong unsupportedNotificationFeatures = bringUp.SelectedFeatures & UnsupportedNotificationFeatureMask;
        if (unsupportedNotificationFeatures != 0)
        {
            return VirtioNetQueueMmioSetupPlanResult.Fail(VirtioNetQueueMmioSetupPlanStatus.UnsupportedNotificationDataFeature, bringUpPlan.Status, unsupportedFeatureMask: unsupportedNotificationFeatures, availableCommandCount: commands.Length);
        }

        if (commands.Length < RequiredCommandCount)
        {
            return VirtioNetQueueMmioSetupPlanResult.Fail(VirtioNetQueueMmioSetupPlanStatus.CommandDestinationTooSmall, bringUpPlan.Status, availableCommandCount: commands.Length);
        }

        VirtioNetQueueMmioSetupPlanStatus rxStatus = ValidateQueue(bringUp.RxQueueIndex, bringUp.RxQueueSize, rxDeviceMetadata, rxAddresses, bringUp.QueuePlan.RxQueue, out VirtioNetQueueMmioSetupAddressKind failingAddressKind, out ulong failingAddress);
        if (rxStatus != VirtioNetQueueMmioSetupPlanStatus.Success)
        {
            return VirtioNetQueueMmioSetupPlanResult.Fail(rxStatus, bringUpPlan.Status, bringUp.RxQueueIndex, failingAddressKind, failingAddress, bringUp.RxQueueSize, rxDeviceMetadata.DeviceMaximumQueueSize, availableCommandCount: commands.Length);
        }

        VirtioNetQueueMmioSetupPlanStatus txStatus = ValidateQueue(bringUp.TxQueueIndex, bringUp.TxQueueSize, txDeviceMetadata, txAddresses, bringUp.QueuePlan.TxQueue, out failingAddressKind, out failingAddress);
        if (txStatus != VirtioNetQueueMmioSetupPlanStatus.Success)
        {
            return VirtioNetQueueMmioSetupPlanResult.Fail(txStatus, bringUpPlan.Status, bringUp.TxQueueIndex, failingAddressKind, failingAddress, bringUp.TxQueueSize, txDeviceMetadata.DeviceMaximumQueueSize, availableCommandCount: commands.Length);
        }

        VirtioNetQueueMmioSetupQueuePlan rxQueuePlan = CreateQueuePlan(bringUp.RxQueueIndex, bringUp.RxQueueSize, rxDeviceMetadata, rxAddresses, bringUp.QueuePlan.RxQueue, commandStartIndex: 0);
        VirtioNetQueueMmioSetupQueuePlan txQueuePlan = CreateQueuePlan(bringUp.TxQueueIndex, bringUp.TxQueueSize, txDeviceMetadata, txAddresses, bringUp.QueuePlan.TxQueue, commandStartIndex: CommandsPerQueue);
        WriteQueueCommands(rxQueuePlan, commands);
        WriteQueueCommands(txQueuePlan, commands);
        VirtioNetQueueMmioSetupPlan plan = new VirtioNetQueueMmioSetupPlan(rxQueuePlan, txQueuePlan, RequiredCommandCount);
        return VirtioNetQueueMmioSetupPlanResult.Success(plan, bringUpPlan.Status, commands.Length);
    }

    private static VirtioNetQueueMmioSetupPlanStatus ValidateQueue(ushort queueIndex, ushort plannedQueueSize, VirtioNetQueueMmioSetupQueueDeviceMetadata deviceMetadata, VirtioNetQueueMmioSetupQueueAddresses addresses, VirtqueueSplitRingPlan ringPlan, out VirtioNetQueueMmioSetupAddressKind failingAddressKind, out ulong failingAddress)
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

        VirtioNetQueueMmioSetupPlanStatus descriptorStatus = ValidateAddress(addresses.DescriptorTablePhysicalAddress, ringPlan.DescriptorTable.Length, ringPlan.DescriptorTable.Alignment);
        if (descriptorStatus != VirtioNetQueueMmioSetupPlanStatus.Success)
        {
            failingAddressKind = VirtioNetQueueMmioSetupAddressKind.DescriptorTable;
            failingAddress = addresses.DescriptorTablePhysicalAddress;
            return descriptorStatus;
        }

        VirtioNetQueueMmioSetupPlanStatus driverStatus = ValidateAddress(addresses.DriverRingPhysicalAddress, ringPlan.AvailableRing.Length, ringPlan.AvailableRing.Alignment);
        if (driverStatus != VirtioNetQueueMmioSetupPlanStatus.Success)
        {
            failingAddressKind = VirtioNetQueueMmioSetupAddressKind.DriverRing;
            failingAddress = addresses.DriverRingPhysicalAddress;
            return driverStatus;
        }

        VirtioNetQueueMmioSetupPlanStatus deviceStatus = ValidateAddress(addresses.DeviceRingPhysicalAddress, ringPlan.UsedRing.Length, ringPlan.UsedRing.Alignment);
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

    private static VirtioNetQueueMmioSetupPlanStatus ValidateAddress(ulong physicalAddress, uint byteLength, uint alignment)
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

    private static VirtioNetQueueMmioSetupQueuePlan CreateQueuePlan(ushort queueIndex, ushort plannedQueueSize, VirtioNetQueueMmioSetupQueueDeviceMetadata deviceMetadata, VirtioNetQueueMmioSetupQueueAddresses addresses, VirtqueueSplitRingPlan ringPlan, int commandStartIndex)
    {
        return new VirtioNetQueueMmioSetupQueuePlan(queueIndex, plannedQueueSize, deviceMetadata.DeviceMaximumQueueSize, deviceMetadata.NotifyOffset, addresses, ringPlan.DescriptorTable.Length, ringPlan.AvailableRing.Length, ringPlan.UsedRing.Length, commandStartIndex, CommandsPerQueue);
    }

    private static void WriteQueueCommands(VirtioNetQueueMmioSetupQueuePlan queue, Span<VirtioNetQueueMmioSetupCommand> commands)
    {
        int index = queue.CommandStartIndex;
        commands[index] = CreateWrite(VirtioNetQueueMmioSetupCommandKind.WriteQueueSelect, queue.QueueIndex, VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectOffset, VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectWidth, queue.QueueIndex);
        commands[index + 1] = CreateRead(VirtioNetQueueMmioSetupCommandKind.ReadQueueSize, queue.QueueIndex, VirtioPciModernRegisterMap.CommonConfiguration.QueueSizeOffset, VirtioPciModernRegisterMap.CommonConfiguration.QueueSizeWidth, queue.DeviceMaximumQueueSize);
        commands[index + 2] = CreateWrite(VirtioNetQueueMmioSetupCommandKind.WriteQueueSize, queue.QueueIndex, VirtioPciModernRegisterMap.CommonConfiguration.QueueSizeOffset, VirtioPciModernRegisterMap.CommonConfiguration.QueueSizeWidth, queue.PlannedQueueSize);
        commands[index + 3] = CreateWrite(VirtioNetQueueMmioSetupCommandKind.WriteQueueDescriptor, queue.QueueIndex, VirtioPciModernRegisterMap.CommonConfiguration.QueueDescOffset, VirtioPciModernRegisterMap.CommonConfiguration.QueueDescWidth, queue.Addresses.DescriptorTablePhysicalAddress);
        commands[index + 4] = CreateWrite(VirtioNetQueueMmioSetupCommandKind.WriteQueueDriver, queue.QueueIndex, VirtioPciModernRegisterMap.CommonConfiguration.QueueDriverOffset, VirtioPciModernRegisterMap.CommonConfiguration.QueueDriverWidth, queue.Addresses.DriverRingPhysicalAddress);
        commands[index + 5] = CreateWrite(VirtioNetQueueMmioSetupCommandKind.WriteQueueDevice, queue.QueueIndex, VirtioPciModernRegisterMap.CommonConfiguration.QueueDeviceOffset, VirtioPciModernRegisterMap.CommonConfiguration.QueueDeviceWidth, queue.Addresses.DeviceRingPhysicalAddress);
        commands[index + 6] = CreateRead(VirtioNetQueueMmioSetupCommandKind.ReadQueueNotifyOffset, queue.QueueIndex, VirtioPciModernRegisterMap.CommonConfiguration.QueueNotifyOffOffset, VirtioPciModernRegisterMap.CommonConfiguration.QueueNotifyOffWidth, queue.NotifyOffset);
        commands[index + 7] = CreateWrite(VirtioNetQueueMmioSetupCommandKind.WriteQueueEnable, queue.QueueIndex, VirtioPciModernRegisterMap.CommonConfiguration.QueueEnableOffset, VirtioPciModernRegisterMap.CommonConfiguration.QueueEnableWidth, value: 1);
    }

    private static VirtioNetQueueMmioSetupCommand CreateRead(VirtioNetQueueMmioSetupCommandKind kind, ushort queueIndex, uint relativeOffset, uint widthBytes, ulong value)
    {
        return new VirtioNetQueueMmioSetupCommand(kind, queueIndex, relativeOffset, widthBytes, value);
    }

    private static VirtioNetQueueMmioSetupCommand CreateWrite(VirtioNetQueueMmioSetupCommandKind kind, ushort queueIndex, uint relativeOffset, uint widthBytes, ulong value)
    {
        return new VirtioNetQueueMmioSetupCommand(kind, queueIndex, relativeOffset, widthBytes, value);
    }
}
