using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;
using OS.Storage;

internal static class VirtioNetBarLiveAccessorBoundaryTests
{
    private const byte CommonOffset = 0x40;
    private const byte NotifyOffset = 0x60;
    private const byte IsrOffset = 0x80;
    private const byte DeviceOffset = 0xA0;
    private const ushort RxQueueSize = 8;
    private const ushort TxQueueSize = 4;
    private const ushort RxBufferCount = 3;
    private const ushort RxDeviceMaximumQueueSize = 16;
    private const ushort TxDeviceMaximumQueueSize = 8;
    private const ushort RxNotifyOffset = 3;
    private const ushort TxNotifyOffset = 5;
    private const uint FrameBufferBytes = VirtioNetQueuePlan.MaximumFrameBufferBytes;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run(
            "drivers.virtionet.bar.liveaccessor reads planned queue-size access",
            TestReadsPlannedQueueSizeAccess,
            ref failures);
        Run(
            "drivers.virtionet.bar.liveaccessor writes planned descriptor access",
            TestWritesPlannedDescriptorAccess,
            ref failures);
        Run(
            "drivers.virtionet.bar.liveaccessor rejects missing accessor and plan",
            TestRejectsMissingAccessorAndPlan,
            ref failures);
        Run(
            "drivers.virtionet.bar.liveaccessor rejects unsupported width",
            TestRejectsUnsupportedWidth,
            ref failures);
        Run(
            "drivers.virtionet.bar.liveaccessor propagates accessor status",
            TestPropagatesAccessorStatus,
            ref failures);
        Run(
            "drivers.virtionet.bar.liveaccessor preserves boundary contract",
            TestPreservesBoundaryContract,
            ref failures);
        Run(
            "drivers.virtionet.bar.liveaccessor default result is not success",
            TestDefaultResultIsNotSuccess,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException(
                "Virtio-net BAR live-accessor boundary host tests failed: " + failures);
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

    private static bool TestReadsPlannedQueueSizeAccess()
    {
        VirtioPciBarRegisterWindowAccessPlan[] accessPlans = CreateQueueSetupAccessPlans();
        FakeRegisterAccessor accessor = new FakeRegisterAccessor
        {
            Read16Value = RxDeviceMaximumQueueSize
        };

        VirtioPciBarRegisterWindowLiveAccessorResult result =
            VirtioPciBarRegisterWindowLiveAccessorBoundary.Read16(
                accessPlans[1],
                accessor);

        return result.Succeeded
            && result.Status == VirtioPciBarRegisterWindowLiveAccessorStatus.Success
            && result.Operation == VirtioPciBarRegisterWindowLiveAccessorOperation.Read16
            && result.ReadStatus == VirtioPciRegisterReadStatus.Success
            && result.WriteStatus == VirtioPciRegisterWriteStatus.Success
            && result.CapabilityType == VirtioPciCapabilityType.CommonConfiguration
            && result.BarIndex == 0
            && result.BarKind == PciBarKind.Memory32
            && result.RelativeOffset == VirtioPciModernRegisterMap.CommonConfiguration.QueueSizeOffset
            && result.Width == VirtioPciRegisterAccessWidth.Word
            && result.WidthBytes == sizeof(ushort)
            && result.BarRelativeOffset ==
                0x20UL + VirtioPciModernRegisterMap.CommonConfiguration.QueueSizeOffset
            && result.PhysicalAddress ==
                0x80000000UL + 0x20UL + VirtioPciModernRegisterMap.CommonConfiguration.QueueSizeOffset
            && result.VirtualAddress ==
                0xFFFF800000000000UL + 0x20UL + VirtioPciModernRegisterMap.CommonConfiguration.QueueSizeOffset
            && result.Value == RxDeviceMaximumQueueSize
            && accessor.OperationCount == 1
            && accessor.IsRead[0]
            && accessor.RelativeOffsets[0] == VirtioPciModernRegisterMap.CommonConfiguration.QueueSizeOffset
            && accessor.Values[0] == RxDeviceMaximumQueueSize
            && accessor.CapabilityTypes[0] == VirtioPciCapabilityType.CommonConfiguration
            && accessor.BarIndices[0] == 0;
    }

    private static bool TestWritesPlannedDescriptorAccess()
    {
        VirtioPciBarRegisterWindowAccessPlan[] accessPlans = CreateQueueSetupAccessPlans();
        FakeRegisterAccessor accessor = new FakeRegisterAccessor();
        ulong descriptorAddress = CreateRxAddresses().DescriptorTablePhysicalAddress;

        VirtioPciBarRegisterWindowLiveAccessorResult result =
            VirtioPciBarRegisterWindowLiveAccessorBoundary.Write64(
                accessPlans[3],
                descriptorAddress,
                accessor);

        return result.Succeeded
            && result.Status == VirtioPciBarRegisterWindowLiveAccessorStatus.Success
            && result.Operation == VirtioPciBarRegisterWindowLiveAccessorOperation.Write64
            && result.WriteStatus == VirtioPciRegisterWriteStatus.Success
            && result.RelativeOffset == VirtioPciModernRegisterMap.CommonConfiguration.QueueDescOffset
            && result.Width == VirtioPciRegisterAccessWidth.QWord
            && result.WidthBytes == sizeof(ulong)
            && result.Value == descriptorAddress
            && accessor.OperationCount == 1
            && !accessor.IsRead[0]
            && accessor.RelativeOffsets[0] == VirtioPciModernRegisterMap.CommonConfiguration.QueueDescOffset
            && accessor.Values[0] == descriptorAddress
            && accessor.Widths[0] == sizeof(ulong);
    }

    private static bool TestRejectsMissingAccessorAndPlan()
    {
        VirtioPciBarRegisterWindowAccessPlan[] accessPlans = CreateQueueSetupAccessPlans();
        FakeRegisterAccessor accessor = new FakeRegisterAccessor();

        VirtioPciBarRegisterWindowLiveAccessorResult missingAccessor =
            VirtioPciBarRegisterWindowLiveAccessorBoundary.Write16(
                accessPlans[0],
                VirtioNetQueuePlan.ReceiveQueueIndex,
                accessor: null);
        VirtioPciBarRegisterWindowLiveAccessorResult missingPlan =
            VirtioPciBarRegisterWindowLiveAccessorBoundary.Read16(
                default,
                accessor);

        return !missingAccessor.Succeeded
            && missingAccessor.Status == VirtioPciBarRegisterWindowLiveAccessorStatus.AccessorNotReady
            && missingAccessor.Operation == VirtioPciBarRegisterWindowLiveAccessorOperation.Write16
            && missingAccessor.WriteStatus == VirtioPciRegisterWriteStatus.NotReady
            && !missingPlan.Succeeded
            && missingPlan.Status == VirtioPciBarRegisterWindowLiveAccessorStatus.MissingAccessPlan
            && missingPlan.Operation == VirtioPciBarRegisterWindowLiveAccessorOperation.Read16
            && missingPlan.WidthBytes == 0
            && accessor.OperationCount == 0;
    }

    private static bool TestRejectsUnsupportedWidth()
    {
        VirtioPciBarRegisterWindowAccessPlan[] accessPlans = CreateQueueSetupAccessPlans();
        FakeRegisterAccessor accessor = new FakeRegisterAccessor();

        VirtioPciBarRegisterWindowLiveAccessorResult readQword =
            VirtioPciBarRegisterWindowLiveAccessorBoundary.Read16(
                accessPlans[3],
                accessor);
        VirtioPciBarRegisterWindowLiveAccessorResult writeWordAsQword =
            VirtioPciBarRegisterWindowLiveAccessorBoundary.Write64(
                accessPlans[0],
                value: 1,
                accessor);

        return !readQword.Succeeded
            && readQword.Status == VirtioPciBarRegisterWindowLiveAccessorStatus.UnsupportedReadWidth
            && readQword.Width == VirtioPciRegisterAccessWidth.QWord
            && !writeWordAsQword.Succeeded
            && writeWordAsQword.Status ==
                VirtioPciBarRegisterWindowLiveAccessorStatus.UnsupportedWriteWidth
            && writeWordAsQword.Width == VirtioPciRegisterAccessWidth.Word
            && accessor.OperationCount == 0;
    }

    private static bool TestPropagatesAccessorStatus()
    {
        VirtioPciBarRegisterWindowAccessPlan[] accessPlans = CreateQueueSetupAccessPlans();
        FakeRegisterAccessor readFailure = new FakeRegisterAccessor
        {
            ReadFailureStatus = VirtioPciRegisterReadStatus.ReadFailed
        };
        FakeRegisterAccessor writeFailure = new FakeRegisterAccessor
        {
            WriteFailureStatus = VirtioPciRegisterWriteStatus.RangeOutsideAperture
        };
        FakeRegisterAccessor notReady = new FakeRegisterAccessor
        {
            ReadFailureStatus = VirtioPciRegisterReadStatus.NotReady
        };

        VirtioPciBarRegisterWindowLiveAccessorResult readResult =
            VirtioPciBarRegisterWindowLiveAccessorBoundary.Read16(
                accessPlans[1],
                readFailure);
        VirtioPciBarRegisterWindowLiveAccessorResult writeResult =
            VirtioPciBarRegisterWindowLiveAccessorBoundary.Write16(
                accessPlans[0],
                VirtioNetQueuePlan.ReceiveQueueIndex,
                writeFailure);
        VirtioPciBarRegisterWindowLiveAccessorResult notReadyResult =
            VirtioPciBarRegisterWindowLiveAccessorBoundary.Read16(
                accessPlans[1],
                notReady);

        return !readResult.Succeeded
            && readResult.Status == VirtioPciBarRegisterWindowLiveAccessorStatus.RegisterReadFailed
            && readResult.ReadStatus == VirtioPciRegisterReadStatus.ReadFailed
            && readFailure.OperationCount == 1
            && !writeResult.Succeeded
            && writeResult.Status == VirtioPciBarRegisterWindowLiveAccessorStatus.RegisterWriteFailed
            && writeResult.WriteStatus == VirtioPciRegisterWriteStatus.RangeOutsideAperture
            && writeFailure.OperationCount == 1
            && !notReadyResult.Succeeded
            && notReadyResult.Status == VirtioPciBarRegisterWindowLiveAccessorStatus.AccessorNotReady
            && notReadyResult.ReadStatus == VirtioPciRegisterReadStatus.NotReady
            && notReady.OperationCount == 1;
    }

    private static bool TestPreservesBoundaryContract()
    {
        VirtioPciBarRegisterWindowAccessPlan[] accessPlans = CreateQueueSetupAccessPlans();
        FakeRegisterAccessor accessor = new FakeRegisterAccessor();

        VirtioPciBarRegisterWindowLiveAccessorResult result =
            VirtioPciBarRegisterWindowLiveAccessorBoundary.Write16(
                accessPlans[0],
                VirtioNetQueuePlan.ReceiveQueueIndex,
                accessor);

        return result.Succeeded
            && VirtioPciBarRegisterWindowLiveAccessorBoundary.RequiresAccessPlan
            && VirtioPciBarRegisterWindowLiveAccessorBoundary.RequiresInjectedAccessor
            && VirtioPciBarRegisterWindowLiveAccessorBoundary.ConsumesMappedRegisterWindowAccessPlan
            && VirtioPciBarRegisterWindowLiveAccessorBoundary.DelegatesOnlyPlannedAccessorOperations
            && !VirtioPciBarRegisterWindowLiveAccessorBoundary.ContainsLiveMmioImplementation
            && !VirtioPciBarRegisterWindowLiveAccessorBoundary.ContainsPortIoImplementation
            && !VirtioPciBarRegisterWindowLiveAccessorBoundary.MapsBars
            && !VirtioPciBarRegisterWindowLiveAccessorBoundary.AllocatesDmaMemory
            && !VirtioPciBarRegisterWindowLiveAccessorBoundary.PerformsDmaMapping
            && !VirtioPciBarRegisterWindowLiveAccessorBoundary.PerformsPacketIo
            && !VirtioPciBarRegisterWindowLiveAccessorBoundary.ConfiguresQueuesByItself
            && !VirtioPciBarRegisterWindowLiveAccessorBoundary.NotifiesDeviceByItself
            && !VirtioPciBarRegisterWindowLiveAccessorBoundary.PollsDevice
            && !VirtioPciBarRegisterWindowLiveAccessorBoundary.HandlesInterrupts
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioPciBarRegisterWindowLiveAccessorResult>();
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        VirtioPciBarRegisterWindowLiveAccessorResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioPciBarRegisterWindowLiveAccessorStatus.Success
            && result.Operation == VirtioPciBarRegisterWindowLiveAccessorOperation.None
            && result.ReadStatus == VirtioPciRegisterReadStatus.Success
            && result.WriteStatus == VirtioPciRegisterWriteStatus.Success
            && result.WidthBytes == 0
            && result.Value == 0;
    }

    private static VirtioPciBarRegisterWindowAccessPlan[] CreateQueueSetupAccessPlans()
    {
        VirtioNetQueueMmioSetupPlanResult setup = CreateSetupPlan(
            out VirtioNetQueueMmioSetupCommand[] commands);
        VirtioPciBarRegisterWindowBindingResult registerWindows = CreateRegisterWindows();
        VirtioPciBarRegisterWindowAccessPlan[] accessPlans =
            new VirtioPciBarRegisterWindowAccessPlan[
                VirtioNetQueueMmioSetupPlanner.RequiredCommandCount];

        VirtioNetQueueMmioSetupRegisterWindowPlanResult result =
            VirtioNetQueueMmioSetupRegisterWindowPlanner.PlanAccesses(
                setup,
                commands,
                registerWindows.Bindings.CommonConfiguration,
                accessPlans);
        if (!setup.Succeeded || !registerWindows.Succeeded || !result.Succeeded)
        {
            throw new InvalidOperationException("Failed to create virtio-net queue setup access plans.");
        }

        return accessPlans;
    }

    private static VirtioNetQueueMmioSetupPlanResult CreateSetupPlan(
        out VirtioNetQueueMmioSetupCommand[] commands)
    {
        commands =
            new VirtioNetQueueMmioSetupCommand[VirtioNetQueueMmioSetupPlanner.RequiredCommandCount];

        return VirtioNetQueueMmioSetupPlanner.Create(
            CreateBringUpPlan(),
            CreateRxAddresses(),
            CreateTxAddresses(),
            CreateRxMetadata(),
            CreateTxMetadata(),
            commands);
    }

    private static VirtioNetDriverQueueBringUpPlanResult CreateBringUpPlan()
    {
        VirtioNetQueuePlan queuePlan = VirtioNetQueuePlan.Create(
            VirtioNetQueuePlan.MinimumQueueCount,
            RxQueueSize,
            TxQueueSize,
            RxBufferCount,
            FrameBufferBytes).Plan;

        return VirtioNetDriverQueueBringUpPlan.Create(
            CreateNetworkDescriptor(),
            RxQueueSize,
            TxQueueSize,
            RxBufferCount,
            FrameBufferBytes,
            queuePlan.RxQueue.RequiredByteLength,
            queuePlan.TxQueue.RequiredByteLength,
            queuePlan.RxBuffers.RequiredBufferBytes,
            queuePlan.TxFrame.RequiredBufferBytes);
    }

    private static VirtioPciBarRegisterWindowBindingResult CreateRegisterWindows()
    {
        return VirtioPciBarRegisterWindowPlanner.BindRequiredApertures(
            CreateBindingPlan(),
            [
                CreateRegisterWindow(0, 0x80000000UL, 0xFFFF800000000000UL),
                CreateRegisterWindow(1, 0x80001000UL, 0xFFFF800000001000UL),
                CreateRegisterWindow(2, 0x80002000UL, 0xFFFF800000002000UL),
                CreateRegisterWindow(3, 0x80003000UL, 0xFFFF800000003000UL),
                default,
                default
            ]);
    }

    private static VirtioPciMappedBarWindow CreateRegisterWindow(
        byte barIndex,
        ulong physicalBaseAddress,
        ulong virtualBaseAddress)
    {
        return new VirtioPciMappedBarWindow(
            barIndex,
            physicalBaseAddress,
            virtualBaseAddress,
            length: 0x1000,
            isMapped: true,
            isMmio: true,
            isDeviceMemory: true,
            isUncached: true);
    }

    private static VirtioNetQueueMmioSetupQueueAddresses CreateRxAddresses()
    {
        return new VirtioNetQueueMmioSetupQueueAddresses(
            descriptorTablePhysicalAddress: 0x100000,
            driverRingPhysicalAddress: 0x101000,
            deviceRingPhysicalAddress: 0x102000);
    }

    private static VirtioNetQueueMmioSetupQueueAddresses CreateTxAddresses()
    {
        return new VirtioNetQueueMmioSetupQueueAddresses(
            descriptorTablePhysicalAddress: 0x110000,
            driverRingPhysicalAddress: 0x111000,
            deviceRingPhysicalAddress: 0x112000);
    }

    private static VirtioNetQueueMmioSetupQueueDeviceMetadata CreateRxMetadata()
    {
        return new VirtioNetQueueMmioSetupQueueDeviceMetadata(
            RxDeviceMaximumQueueSize,
            hasNotifyOffset: true,
            RxNotifyOffset);
    }

    private static VirtioNetQueueMmioSetupQueueDeviceMetadata CreateTxMetadata()
    {
        return new VirtioNetQueueMmioSetupQueueDeviceMetadata(
            TxDeviceMaximumQueueSize,
            hasNotifyOffset: true,
            TxNotifyOffset);
    }

    private static VirtioDeviceDescriptor CreateNetworkDescriptor()
    {
        VirtioDeviceDescriptorSource[] sources =
        [
            new VirtioDeviceDescriptorSource(
                sourceIndex: 41,
                CreateBindingPlan(),
                VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask,
                VirtioDeviceInitializationProfile.NetworkRequiredQueueCount,
                featuresOkAccepted: true)
        ];
        VirtioDeviceConfigurationSource[] configurations =
        [
            new VirtioDeviceConfigurationSource(
                sourceIndex: 41,
                CreateNetworkConfiguration(
                    new MacAddress(0x02, 0x33, 0x44, 0x55, 0x66, 0xCD),
                    VirtioNetDeviceConfig.StatusLinkUp))
        ];
        VirtioDeviceDescriptor[] destination = new VirtioDeviceDescriptor[1];

        VirtioDeviceDescriptorCatalogResult result = VirtioDeviceDescriptorCatalog.Build(
            sources,
            configurations,
            destination);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Failed to create deterministic virtio-net descriptor.");
        }

        return destination[0];
    }

    private static VirtioPciBindingPlanResult CreateBindingPlan()
    {
        ushort deviceId = (ushort)(VirtioPciDeviceDiscovery.ModernDeviceIdStart +
            VirtioPciDeviceDiscovery.NetworkDeviceId);

        return VirtioPciBindingPlan.Create(
            CreateScannedDevice(deviceId),
            CreatePciConfiguration(deviceId),
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

    private static byte[] CreatePciConfiguration(ushort deviceId)
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
            VirtioPciDeviceDiscovery.NetworkDeviceConfigurationMinimumLength,
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

    private sealed class FakeRegisterAccessor : IVirtioPciRegisterAccessor
    {
        public bool[] IsRead { get; } = new bool[4];

        public uint[] RelativeOffsets { get; } = new uint[4];

        public uint[] Widths { get; } = new uint[4];

        public ulong[] Values { get; } = new ulong[4];

        public VirtioPciCapabilityType[] CapabilityTypes { get; } = new VirtioPciCapabilityType[4];

        public byte[] BarIndices { get; } = new byte[4];

        public int OperationCount { get; private set; }

        public ushort Read16Value { get; init; }

        public VirtioPciRegisterReadStatus ReadFailureStatus { get; init; } =
            VirtioPciRegisterReadStatus.Success;

        public VirtioPciRegisterWriteStatus WriteFailureStatus { get; init; } =
            VirtioPciRegisterWriteStatus.Success;

        public VirtioPciRegisterRead16Result Read16(
            VirtioPciBarAperture aperture,
            uint relativeOffset)
        {
            int index = Record(aperture, relativeOffset, sizeof(ushort), value: 0, isRead: true);
            if (ReadFailureStatus != VirtioPciRegisterReadStatus.Success)
            {
                return new VirtioPciRegisterRead16Result(ReadFailureStatus, value: 0);
            }

            Values[index] = Read16Value;
            return new VirtioPciRegisterRead16Result(
                VirtioPciRegisterReadStatus.Success,
                Read16Value);
        }

        public VirtioPciRegisterWriteStatus Write16(
            VirtioPciBarAperture aperture,
            uint relativeOffset,
            ushort value)
        {
            Record(aperture, relativeOffset, sizeof(ushort), value, isRead: false);
            return WriteFailureStatus;
        }

        public VirtioPciRegisterWriteStatus Write64(
            VirtioPciBarAperture aperture,
            uint relativeOffset,
            ulong value)
        {
            Record(aperture, relativeOffset, sizeof(ulong), value, isRead: false);
            return WriteFailureStatus;
        }

        private int Record(
            VirtioPciBarAperture aperture,
            uint relativeOffset,
            uint widthBytes,
            ulong value,
            bool isRead)
        {
            int index = OperationCount++;
            IsRead[index] = isRead;
            RelativeOffsets[index] = relativeOffset;
            Widths[index] = widthBytes;
            Values[index] = value;
            CapabilityTypes[index] = aperture.CapabilityType;
            BarIndices[index] = aperture.BarIndex;
            return index;
        }
    }
}
