using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;
using OS.Storage;

internal static class StorageVirtioBlockQueueMmioSetupPlanTests
{
    private const byte CommonOffset = 0x40;
    private const byte NotifyOffset = 0x60;
    private const byte IsrOffset = 0x80;
    private const byte DeviceOffset = 0xA0;
    private const ushort QueueSize = 8;
    private const ushort DeviceMaximumQueueSize = 16;
    private const ushort QueueNotifyOffset = 7;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run(
            "storage.virtioblk.queuesetup plans queue zero common config commands",
            TestPlansQueueZeroCommonConfigCommands,
            ref failures);
        Run(
            "storage.virtioblk.queuesetup rejects failed block descriptor",
            TestRejectsFailedBlockDescriptor,
            ref failures);
        Run(
            "storage.virtioblk.queuesetup rejects queue metadata",
            TestRejectsQueueMetadata,
            ref failures);
        Run(
            "storage.virtioblk.queuesetup rejects physical addresses",
            TestRejectsPhysicalAddresses,
            ref failures);
        Run(
            "storage.virtioblk.queuesetup reports command destination capacity",
            TestReportsCommandDestinationCapacity,
            ref failures);
        Run(
            "storage.virtioblk.queuesetup default result is not success",
            TestDefaultResultIsNotSuccess,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException(
                "Virtio-blk queue MMIO setup plan host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestPlansQueueZeroCommonConfigCommands()
    {
        VirtioBlockQueueMmioSetupCommand[] commands =
            new VirtioBlockQueueMmioSetupCommand[VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount];

        VirtioBlockQueueMmioSetupPlanResult result = VirtioBlockQueueMmioSetupPlanner.Create(
            CreateBlockDescriptor(sourceIndex: 10),
            QueueSize,
            CreateAddresses(),
            CreateMetadata(),
            commands);
        VirtioBlockQueueMmioSetupPlan plan = result.Plan;
        VirtqueueSplitRingPlan queue = VirtqueueSplitRingPlan.Create(QueueSize).Plan;

        return result.Succeeded
            && result.Status == VirtioBlockQueueMmioSetupPlanStatus.Success
            && result.DeviceKind == VirtioPciDeviceKind.Block
            && result.BindingStatus == VirtioPciBindingPlanStatus.Success
            && result.BlockConfigurationStatus == VirtioBlockDeviceConfigStatus.Success
            && result.InitializationProfileStatus == VirtioDeviceInitializationProfileStatus.Success
            && result.QueuePlanStatus == VirtqueueSplitRingPlanStatus.Success
            && result.RequiredCommandCount == VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount
            && result.AvailableCommandCount == VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount
            && result.CommandCount == VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount
            && plan.QueueIndex == VirtioBlockQueueMmioSetupPlanner.QueueIndex
            && plan.PlannedQueueSize == QueueSize
            && plan.DeviceMaximumQueueSize == DeviceMaximumQueueSize
            && plan.NotifyOffset == QueueNotifyOffset
            && plan.DescriptorTableBytes == queue.DescriptorTable.Length
            && plan.DriverRingBytes == queue.AvailableRing.Length
            && plan.DeviceRingBytes == queue.UsedRing.Length
            && plan.CommandCount == VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount
            && CommandsMatchPlan(commands, plan)
            && VirtioBlockQueueMmioSetupPlanner.ConsumesVirtioDeviceDescriptor
            && VirtioBlockQueueMmioSetupPlanner.PlansQueueZero
            && !VirtioBlockQueueMmioSetupPlanner.InvokesRegisterAccessor
            && !VirtioBlockQueueMmioSetupPlanner.WritesMmioRegisters
            && !VirtioBlockQueueMmioSetupPlanner.ReadsLiveDeviceRegisters
            && !VirtioBlockQueueMmioSetupPlanner.WritesLiveDeviceRegisters
            && !VirtioBlockQueueMmioSetupPlanner.PerformsLiveMmio
            && !VirtioBlockQueueMmioSetupPlanner.MapsBars
            && !VirtioBlockQueueMmioSetupPlanner.PublishesDescriptors
            && !VirtioBlockQueueMmioSetupPlanner.ConfiguresLiveQueues
            && !VirtioBlockQueueMmioSetupPlanner.NotifiesDevice
            && !VirtioBlockQueueMmioSetupPlanner.HandlesInterrupts
            && !VirtioBlockQueueMmioSetupPlanner.AllocatesDmaMemory
            && !VirtioBlockQueueMmioSetupPlanner.PerformsDmaMapping
            && !VirtioBlockQueueMmioSetupPlanner.PerformsBlockIo
            && !VirtioBlockQueueMmioSetupPlanner.PerformsDiskRead
            && !VirtioBlockQueueMmioSetupPlanner.PerformsDiskWrite
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockQueueMmioSetupCommand>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockQueueMmioSetupPlanResult>();
    }

    private static bool TestRejectsFailedBlockDescriptor()
    {
        VirtioBlockQueueMmioSetupCommand[] commands =
            new VirtioBlockQueueMmioSetupCommand[VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount];

        VirtioBlockQueueMmioSetupPlanResult defaultDescriptor =
            VirtioBlockQueueMmioSetupPlanner.Create(
                default,
                QueueSize,
                CreateAddresses(),
                CreateMetadata(),
                commands);
        VirtioBlockQueueMmioSetupPlanResult networkDescriptor =
            VirtioBlockQueueMmioSetupPlanner.Create(
                CreateNetworkDescriptor(sourceIndex: 11),
                QueueSize,
                CreateAddresses(),
                CreateMetadata(),
                commands);

        return !defaultDescriptor.Succeeded
            && defaultDescriptor.Status == VirtioBlockQueueMmioSetupPlanStatus.DescriptorNotReady
            && defaultDescriptor.DeviceKind == VirtioPciDeviceKind.Unknown
            && defaultDescriptor.RequiredCommandCount == VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount
            && defaultDescriptor.AvailableCommandCount == VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount
            && !networkDescriptor.Succeeded
            && networkDescriptor.Status == VirtioBlockQueueMmioSetupPlanStatus.NotBlockDevice
            && networkDescriptor.DeviceKind == VirtioPciDeviceKind.Network
            && networkDescriptor.BlockConfigurationStatus == default;
    }

    private static bool TestRejectsQueueMetadata()
    {
        VirtioBlockQueueMmioSetupCommand[] commands =
            new VirtioBlockQueueMmioSetupCommand[VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount];
        VirtioDeviceDescriptor descriptor = CreateBlockDescriptor(sourceIndex: 12);

        VirtioBlockQueueMmioSetupPlanResult invalidQueuePlan =
            VirtioBlockQueueMmioSetupPlanner.Create(
                descriptor,
                queueSize: 3,
                CreateAddresses(),
                CreateMetadata(),
                commands);
        VirtioBlockQueueMmioSetupPlanResult unavailable =
            VirtioBlockQueueMmioSetupPlanner.Create(
                descriptor,
                QueueSize,
                CreateAddresses(),
                new VirtioBlockQueueMmioSetupQueueDeviceMetadata(0, true, QueueNotifyOffset),
                commands);
        VirtioBlockQueueMmioSetupPlanResult rejectedSize =
            VirtioBlockQueueMmioSetupPlanner.Create(
                descriptor,
                queueSize: 2,
                CreateAddresses(),
                CreateMetadata(),
                commands);
        VirtioBlockQueueMmioSetupPlanResult aboveMaximum =
            VirtioBlockQueueMmioSetupPlanner.Create(
                descriptor,
                QueueSize,
                CreateAddresses(),
                new VirtioBlockQueueMmioSetupQueueDeviceMetadata(QueueSize - 1, true, QueueNotifyOffset),
                commands);
        VirtioBlockQueueMmioSetupPlanResult missingNotify =
            VirtioBlockQueueMmioSetupPlanner.Create(
                descriptor,
                QueueSize,
                CreateAddresses(),
                new VirtioBlockQueueMmioSetupQueueDeviceMetadata(DeviceMaximumQueueSize, false, 0),
                commands);

        return !invalidQueuePlan.Succeeded
            && invalidQueuePlan.Status == VirtioBlockQueueMmioSetupPlanStatus.QueuePlanFailed
            && invalidQueuePlan.QueuePlanStatus == VirtqueueSplitRingPlanStatus.QueueSizeNotPowerOfTwo
            && !unavailable.Succeeded
            && unavailable.Status == VirtioBlockQueueMmioSetupPlanStatus.QueueUnavailable
            && unavailable.DeviceMaximumQueueSize == 0
            && !rejectedSize.Succeeded
            && rejectedSize.Status == VirtioBlockQueueMmioSetupPlanStatus.QueueSizeRejected
            && rejectedSize.PlannedQueueSize == 2
            && !aboveMaximum.Succeeded
            && aboveMaximum.Status == VirtioBlockQueueMmioSetupPlanStatus.QueueSizeRejected
            && aboveMaximum.DeviceMaximumQueueSize == QueueSize - 1
            && !missingNotify.Succeeded
            && missingNotify.Status == VirtioBlockQueueMmioSetupPlanStatus.MissingNotifyOffset;
    }

    private static bool TestRejectsPhysicalAddresses()
    {
        VirtioBlockQueueMmioSetupCommand[] commands =
            new VirtioBlockQueueMmioSetupCommand[VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount];
        VirtioDeviceDescriptor descriptor = CreateBlockDescriptor(sourceIndex: 13);

        VirtioBlockQueueMmioSetupPlanResult badDescriptor =
            VirtioBlockQueueMmioSetupPlanner.Create(
                descriptor,
                QueueSize,
                new VirtioBlockQueueMmioSetupQueueAddresses(
                    descriptorTablePhysicalAddress: 0x100003,
                    driverRingPhysicalAddress: 0x101000,
                    deviceRingPhysicalAddress: 0x102000),
                CreateMetadata(),
                commands);
        VirtioBlockQueueMmioSetupPlanResult badDriver =
            VirtioBlockQueueMmioSetupPlanner.Create(
                descriptor,
                QueueSize,
                new VirtioBlockQueueMmioSetupQueueAddresses(
                    descriptorTablePhysicalAddress: 0x100000,
                    driverRingPhysicalAddress: 0,
                    deviceRingPhysicalAddress: 0x102000),
                CreateMetadata(),
                commands);
        VirtioBlockQueueMmioSetupPlanResult deviceOverflow =
            VirtioBlockQueueMmioSetupPlanner.Create(
                descriptor,
                QueueSize,
                new VirtioBlockQueueMmioSetupQueueAddresses(
                    descriptorTablePhysicalAddress: 0x100000,
                    driverRingPhysicalAddress: 0x101000,
                    deviceRingPhysicalAddress: ulong.MaxValue - 3UL),
                CreateMetadata(),
                commands);

        return !badDescriptor.Succeeded
            && badDescriptor.Status == VirtioBlockQueueMmioSetupPlanStatus.InvalidPhysicalAddress
            && badDescriptor.FailingAddressKind == VirtioBlockQueueMmioSetupAddressKind.DescriptorTable
            && badDescriptor.FailingPhysicalAddress == 0x100003
            && !badDriver.Succeeded
            && badDriver.Status == VirtioBlockQueueMmioSetupPlanStatus.InvalidPhysicalAddress
            && badDriver.FailingAddressKind == VirtioBlockQueueMmioSetupAddressKind.DriverRing
            && badDriver.FailingPhysicalAddress == 0
            && !deviceOverflow.Succeeded
            && deviceOverflow.Status == VirtioBlockQueueMmioSetupPlanStatus.PhysicalAddressOverflow
            && deviceOverflow.FailingAddressKind == VirtioBlockQueueMmioSetupAddressKind.DeviceRing;
    }

    private static bool TestReportsCommandDestinationCapacity()
    {
        VirtioBlockQueueMmioSetupCommand[] commands =
            new VirtioBlockQueueMmioSetupCommand[VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount - 1];

        VirtioBlockQueueMmioSetupPlanResult result = VirtioBlockQueueMmioSetupPlanner.Create(
            CreateBlockDescriptor(sourceIndex: 14),
            QueueSize,
            CreateAddresses(),
            CreateMetadata(),
            commands);

        return !result.Succeeded
            && result.Status == VirtioBlockQueueMmioSetupPlanStatus.CommandDestinationTooSmall
            && result.RequiredCommandCount == VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount
            && result.AvailableCommandCount == VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount - 1
            && result.PlannedQueueSize == QueueSize
            && result.DeviceMaximumQueueSize == DeviceMaximumQueueSize
            && result.CommandCount == 0;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        VirtioBlockQueueMmioSetupPlanResult result = default;
        VirtioBlockQueueMmioSetupCommand command = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.CommandCount == 0
            && result.RequiredCommandCount == 0
            && command.Kind == VirtioBlockQueueMmioSetupCommandKind.WriteQueueSelect
            && !command.ReadsRegister
            && command.WritesRegister;
    }

    private static bool CommandsMatchPlan(
        ReadOnlySpan<VirtioBlockQueueMmioSetupCommand> commands,
        VirtioBlockQueueMmioSetupPlan plan)
    {
        return commands.Length == VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount
            && Matches(
                commands[0],
                VirtioBlockQueueMmioSetupCommandKind.WriteQueueSelect,
                plan.QueueIndex,
                VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectOffset,
                VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectWidth,
                plan.QueueIndex,
                reads: false)
            && Matches(
                commands[1],
                VirtioBlockQueueMmioSetupCommandKind.ReadQueueSize,
                plan.QueueIndex,
                VirtioPciModernRegisterMap.CommonConfiguration.QueueSizeOffset,
                VirtioPciModernRegisterMap.CommonConfiguration.QueueSizeWidth,
                plan.DeviceMaximumQueueSize,
                reads: true)
            && Matches(
                commands[2],
                VirtioBlockQueueMmioSetupCommandKind.WriteQueueSize,
                plan.QueueIndex,
                VirtioPciModernRegisterMap.CommonConfiguration.QueueSizeOffset,
                VirtioPciModernRegisterMap.CommonConfiguration.QueueSizeWidth,
                plan.PlannedQueueSize,
                reads: false)
            && Matches(
                commands[3],
                VirtioBlockQueueMmioSetupCommandKind.WriteQueueDescriptor,
                plan.QueueIndex,
                VirtioPciModernRegisterMap.CommonConfiguration.QueueDescOffset,
                VirtioPciModernRegisterMap.CommonConfiguration.QueueDescWidth,
                plan.Addresses.DescriptorTablePhysicalAddress,
                reads: false)
            && Matches(
                commands[4],
                VirtioBlockQueueMmioSetupCommandKind.WriteQueueDriver,
                plan.QueueIndex,
                VirtioPciModernRegisterMap.CommonConfiguration.QueueDriverOffset,
                VirtioPciModernRegisterMap.CommonConfiguration.QueueDriverWidth,
                plan.Addresses.DriverRingPhysicalAddress,
                reads: false)
            && Matches(
                commands[5],
                VirtioBlockQueueMmioSetupCommandKind.WriteQueueDevice,
                plan.QueueIndex,
                VirtioPciModernRegisterMap.CommonConfiguration.QueueDeviceOffset,
                VirtioPciModernRegisterMap.CommonConfiguration.QueueDeviceWidth,
                plan.Addresses.DeviceRingPhysicalAddress,
                reads: false)
            && Matches(
                commands[6],
                VirtioBlockQueueMmioSetupCommandKind.ReadQueueNotifyOffset,
                plan.QueueIndex,
                VirtioPciModernRegisterMap.CommonConfiguration.QueueNotifyOffOffset,
                VirtioPciModernRegisterMap.CommonConfiguration.QueueNotifyOffWidth,
                plan.NotifyOffset,
                reads: true)
            && Matches(
                commands[7],
                VirtioBlockQueueMmioSetupCommandKind.WriteQueueEnable,
                plan.QueueIndex,
                VirtioPciModernRegisterMap.CommonConfiguration.QueueEnableOffset,
                VirtioPciModernRegisterMap.CommonConfiguration.QueueEnableWidth,
                value: 1,
                reads: false);
    }

    private static bool Matches(
        VirtioBlockQueueMmioSetupCommand command,
        VirtioBlockQueueMmioSetupCommandKind kind,
        ushort queueIndex,
        uint relativeOffset,
        uint widthBytes,
        ulong value,
        bool reads)
    {
        return command.Kind == kind
            && command.QueueIndex == queueIndex
            && command.RelativeOffset == relativeOffset
            && command.WidthBytes == widthBytes
            && command.Value == value
            && command.ReadsRegister == reads
            && command.WritesRegister != reads;
    }

    private static VirtioBlockQueueMmioSetupQueueAddresses CreateAddresses()
    {
        return new VirtioBlockQueueMmioSetupQueueAddresses(
            descriptorTablePhysicalAddress: 0x100000,
            driverRingPhysicalAddress: 0x101000,
            deviceRingPhysicalAddress: 0x102000);
    }

    private static VirtioBlockQueueMmioSetupQueueDeviceMetadata CreateMetadata()
    {
        return new VirtioBlockQueueMmioSetupQueueDeviceMetadata(
            DeviceMaximumQueueSize,
            hasNotifyOffset: true,
            QueueNotifyOffset);
    }

    private static VirtioDeviceDescriptor CreateBlockDescriptor(int sourceIndex)
    {
        return CreateDescriptor(
            sourceIndex,
            VirtioPciDeviceDiscovery.BlockDeviceId,
            VirtioDeviceInitializationProfile.BlockOptionalFeatureMask,
            VirtioDeviceInitializationProfile.BlockRequiredQueueCount,
            CreateBlockConfiguration(capacitySectors: 64));
    }

    private static VirtioDeviceDescriptor CreateNetworkDescriptor(int sourceIndex)
    {
        return CreateDescriptor(
            sourceIndex,
            VirtioPciDeviceDiscovery.NetworkDeviceId,
            VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask,
            VirtioDeviceInitializationProfile.NetworkRequiredQueueCount,
            CreateNetworkConfiguration(
                new MacAddress(0x02, 0x33, 0x44, 0x55, 0x66, 0xDD),
                VirtioNetDeviceConfig.StatusLinkUp));
    }

    private static VirtioDeviceDescriptor CreateDescriptor(
        int sourceIndex,
        byte virtioDeviceId,
        ulong features,
        ushort availableQueueCount,
        byte[] configuration)
    {
        VirtioDeviceDescriptorSource[] sources =
        [
            new VirtioDeviceDescriptorSource(
                sourceIndex,
                CreateBindingPlan(virtioDeviceId),
                features,
                availableQueueCount,
                featuresOkAccepted: true)
        ];
        VirtioDeviceConfigurationSource[] configurations =
        [
            new VirtioDeviceConfigurationSource(sourceIndex, configuration)
        ];
        VirtioDeviceDescriptor[] destination = new VirtioDeviceDescriptor[1];

        VirtioDeviceDescriptorCatalogResult result = VirtioDeviceDescriptorCatalog.Build(
            sources,
            configurations,
            destination);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Failed to create deterministic descriptor.");
        }

        return destination[0];
    }

    private static VirtioPciBindingPlanResult CreateBindingPlan(byte virtioDeviceId)
    {
        ushort deviceId = (ushort)(VirtioPciDeviceDiscovery.ModernDeviceIdStart + virtioDeviceId);
        uint deviceConfigurationLength = virtioDeviceId == VirtioPciDeviceDiscovery.NetworkDeviceId
            ? VirtioPciDeviceDiscovery.NetworkDeviceConfigurationMinimumLength
            : VirtioPciDeviceDiscovery.BlockDeviceConfigurationMinimumLength;

        return VirtioPciBindingPlan.Create(
            CreateScannedDevice(deviceId),
            CreatePciConfiguration(deviceId, deviceConfigurationLength),
            CreateBarSizes());
    }

    private static PciEcamScannedDevice CreateScannedDevice(ushort deviceId)
    {
        return new PciEcamScannedDevice(
            segmentIndex: 2,
            new PciEcamSegment(
                basePhysicalAddress: 0xE0000000UL,
                segmentGroup: 1,
                startBus: 0,
                endBus: 0x7F),
            new PciScannedDevice(
                bus: 3,
                device: 5,
                function: 0,
                vendorId: VirtioPciDeviceDiscovery.VirtioVendorId,
                deviceId: deviceId,
                revisionId: 0,
                classCode: 0,
                subclass: 0,
                programmingInterface: 0,
                headerType: PciConfigurationHeader.Type0HeaderType,
                isMultifunction: false,
                hasCapabilitiesList: true,
                capabilityPointer: CommonOffset));
    }

    private static byte[] CreatePciConfiguration(
        ushort deviceId,
        uint deviceConfigurationLength)
    {
        byte[] config = new byte[256];
        EndianPrimitives.WriteUInt16LittleEndian(
            config.AsSpan(0x00, sizeof(ushort)),
            VirtioPciDeviceDiscovery.VirtioVendorId);
        EndianPrimitives.WriteUInt16LittleEndian(config.AsSpan(0x02, sizeof(ushort)), deviceId);
        EndianPrimitives.WriteUInt16LittleEndian(
            config.AsSpan(0x06, sizeof(ushort)),
            PciConfigurationHeader.StatusCapabilitiesListBit);
        config[0x0E] = PciConfigurationHeader.Type0HeaderType;
        WriteUInt32(config, 0x10, 0x80000000);
        WriteUInt32(config, 0x14, 0x80001000);
        WriteUInt32(config, 0x18, 0x80002000);
        WriteUInt32(config, 0x1C, 0x80003000);
        config[0x34] = CommonOffset;

        WriteVirtioCapability(
            config,
            CommonOffset,
            NotifyOffset,
            VirtioPciCapabilityType.CommonConfiguration,
            bar: 0,
            offset: 0x20,
            VirtioPciDeviceDiscovery.CommonConfigurationMinimumLength,
            notifyOffsetMultiplier: 0,
            capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);
        WriteVirtioCapability(
            config,
            NotifyOffset,
            IsrOffset,
            VirtioPciCapabilityType.NotifyConfiguration,
            bar: 1,
            offset: 0x80,
            length: 0x100,
            notifyOffsetMultiplier: 4,
            capabilityLength: VirtioPciCapabilityListParser.ModernVirtioNotifyCapabilityLength);
        WriteVirtioCapability(
            config,
            IsrOffset,
            DeviceOffset,
            VirtioPciCapabilityType.IsrStatus,
            bar: 2,
            offset: 0,
            VirtioPciDeviceDiscovery.IsrStatusMinimumLength,
            notifyOffsetMultiplier: 0,
            capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);
        WriteVirtioCapability(
            config,
            DeviceOffset,
            nextOffset: 0,
            VirtioPciCapabilityType.DeviceConfiguration,
            bar: 3,
            offset: 0x40,
            deviceConfigurationLength,
            notifyOffsetMultiplier: 0,
            capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);

        return config;
    }

    private static ulong[] CreateBarSizes()
    {
        return
        [
            0x1000,
            0x1000,
            0x1000,
            0x1000,
            0,
            0
        ];
    }

    private static byte[] CreateNetworkConfiguration(
        MacAddress macAddress,
        ushort statusBits)
    {
        byte[] config = new byte[VirtioNetDeviceConfig.StatusConfigLength];
        macAddress.TryWrite(config, VirtioNetDeviceConfig.MacAddressOffset);
        EndianPrimitives.WriteUInt16LittleEndian(
            config.AsSpan(VirtioNetDeviceConfig.StatusOffset, VirtioNetDeviceConfig.StatusLength),
            statusBits);
        return config;
    }

    private static byte[] CreateBlockConfiguration(ulong capacitySectors)
    {
        byte[] config = new byte[VirtioBlockDeviceConfig.CapacityLength];
        EndianPrimitives.WriteUInt64LittleEndian(config, capacitySectors);
        return config;
    }

    private static void WriteVirtioCapability(
        Span<byte> config,
        byte capabilityOffset,
        byte nextOffset,
        VirtioPciCapabilityType type,
        byte bar,
        uint offset,
        uint length,
        uint notifyOffsetMultiplier,
        byte capabilityLength)
    {
        config[capabilityOffset] = VirtioPciCapabilityListParser.VendorSpecificCapabilityId;
        config[capabilityOffset + 1] = nextOffset;
        config[capabilityOffset + 2] = capabilityLength;
        config[capabilityOffset + 3] = (byte)type;
        config[capabilityOffset + 4] = bar;
        WriteUInt32(config, capabilityOffset + 8, offset);
        WriteUInt32(config, capabilityOffset + 12, length);

        if (capabilityLength >= VirtioPciCapabilityListParser.ModernVirtioNotifyCapabilityLength)
        {
            WriteUInt32(config, capabilityOffset + 16, notifyOffsetMultiplier);
        }
    }

    private static void WriteUInt32(Span<byte> destination, int offset, uint value)
    {
        EndianPrimitives.WriteUInt32LittleEndian(destination.Slice(offset, sizeof(uint)), value);
    }
}
