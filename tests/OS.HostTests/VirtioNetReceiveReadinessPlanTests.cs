using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;

internal static class VirtioNetReceiveReadinessPlanTests
{
    private const byte CommonOffset = 0x40;
    private const byte NotifyOffset = 0x60;
    private const byte IsrOffset = 0x80;
    private const byte DeviceOffset = 0xA0;
    private const ushort RxQueueSize = 8;
    private const ushort TxQueueSize = 4;
    private const ushort RxBufferCount = 3;
    private const ushort RxNotifyOffset = 3;
    private const ushort TxNotifyOffset = 5;
    private const ushort RxDeviceMaximumQueueSize = 16;
    private const ushort TxDeviceMaximumQueueSize = 8;
    private const uint NotifyOffsetMultiplier = 4;
    private const uint NotifyApertureBarOffset = 0x80;
    private const ulong NotifyBarPhysicalBase = 0x80001000UL;
    private const ulong NotifyBarVirtualBase = 0xFFFF800000001000UL;
    private const uint FrameBufferBytes = VirtioNetReceiveLifecycle.MaximumReceiveBufferBytes;
    private const ulong RxBufferPhysicalAddress = 0x0040_0000UL;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run(
            "drivers.virtionet.rx.readiness composes setup publish notify windowexec",
            TestComposesSetupPublishNotifyWindowExecution,
            ref failures);
        Run(
            "drivers.virtionet.rx.readiness rejects failed setup window execution",
            TestRejectsFailedSetupWindowExecution,
            ref failures);
        Run(
            "drivers.virtionet.rx.readiness rejects failed publish",
            TestRejectsFailedPublish,
            ref failures);
        Run(
            "drivers.virtionet.rx.readiness rejects missing barrier",
            TestRejectsMissingBarrier,
            ref failures);
        Run(
            "drivers.virtionet.rx.readiness propagates notification plan failure",
            TestPropagatesNotificationPlanFailure,
            ref failures);
        Run(
            "drivers.virtionet.rx.readiness propagates notification window failure",
            TestPropagatesNotificationWindowFailure,
            ref failures);
        Run(
            "drivers.virtionet.rx.readiness default result is not success",
            TestDefaultResultIsNotSuccess,
            ref failures);
        Run(
            "drivers.virtionet.rx.readiness preserves no-live-mmio boundary",
            TestPreservesNoLiveMmioBoundary,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException(
                "Virtio-net RX readiness host tests failed: " + failures);
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

    private static bool TestComposesSetupPublishNotifyWindowExecution()
    {
        TestContext context = CreateContext();
        byte[] commonBytes = CreateCommonWindowBytes();
        VirtioNetQueueMmioSetupRegisterWindowExecutionResult setupExecution =
            ExecuteQueueSetupWindow(context, commonBytes);
        byte[] queueMemory = CreateRxQueueMemory(context);
        byte[] notifyBytes = CreateNotifyWindowBytes();

        VirtioNetReceiveReadinessResult result = ComposeSuccessfulShape(
            context,
            setupExecution,
            queueMemory,
            notifyBytes,
            VirtioNetQueueNotificationBarrierReadiness.Ready(41));

        return setupExecution.Succeeded
            && result.Succeeded
            && result.Status == VirtioNetReceiveReadinessStatus.Success
            && result.QueueSetupPlanStatus == VirtioNetQueueMmioSetupPlanStatus.Success
            && result.QueueSetupWindowExecutionStatus ==
                VirtioNetQueueMmioSetupRegisterWindowExecutionStatus.Success
            && result.QueueSetupRegisterWindowStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.QueueSetupAccessExecutionStatus ==
                VirtioPciBarRegisterWindowAccessExecutionStatus.Success
            && result.ReceivePublishStatus == VirtioNetReceiveLifecyclePublishStatus.Success
            && result.NotificationPlanStatus == VirtioNetQueueNotificationPlanStatus.Success
            && result.PciNotificationStatus == VirtioPciQueueNotificationPlanStatus.Success
            && result.NotificationWindowExecutionStatus ==
                VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.Success
            && result.NotificationRegisterWindowStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.NotificationAccessExecutionStatus ==
                VirtioPciBarRegisterWindowAccessExecutionStatus.Success
            && result.ReceivePublishAttempted
            && result.NotificationPlanAttempted
            && result.NotificationWindowExecutionAttempted
            && result.BarrierSucceeded
            && result.BarrierSequence == 41
            && result.FirstDescriptorIndex == 1
            && result.PublishedDescriptorCount == 2
            && result.PreviousAvailableRingIndex == 6
            && result.NextAvailableRingIndex == 8
            && result.PreviousUsedIndex == 4
            && result.ExpectedNextUsedIndex == 5
            && result.QueueIndex == VirtioNetQueuePlan.ReceiveQueueIndex
            && result.QueueNotifyOffset == RxNotifyOffset
            && result.NotifyRelativeOffset == RxNotifyOffset * NotifyOffsetMultiplier
            && result.WriteValue == VirtioNetQueuePlan.ReceiveQueueIndex
            && result.WriteWidthBytes == sizeof(ushort)
            && result.CapabilityType == VirtioPciCapabilityType.NotifyConfiguration
            && result.BarIndex == 1
            && result.AccessWidth == VirtioPciRegisterAccessWidth.Word
            && result.AccessWidthBytes == sizeof(ushort)
            && result.BarRelativeOffset == NotifyApertureBarOffset + result.NotifyRelativeOffset
            && result.WrittenValue == VirtioNetQueuePlan.ReceiveQueueIndex
            && ReadUInt16AtBarOffset(notifyBytes, result.BarRelativeOffset) ==
                VirtioNetQueuePlan.ReceiveQueueIndex
            && DescriptorMatches(
                queueMemory,
                context.QueuePlan.Plan.RxQueue,
                descriptorIndex: 1,
                expectedAddress: RxBufferPhysicalAddress,
                expectedLength: FrameBufferBytes)
            && DescriptorMatches(
                queueMemory,
                context.QueuePlan.Plan.RxQueue,
                descriptorIndex: 2,
                expectedAddress: RxBufferPhysicalAddress + FrameBufferBytes,
                expectedLength: FrameBufferBytes)
            && ReadAvailableEntry(queueMemory, context.QueuePlan.Plan.RxQueue, slot: 6) == 1
            && ReadAvailableEntry(queueMemory, context.QueuePlan.Plan.RxQueue, slot: 7) == 2
            && ReadAvailableIndex(queueMemory, context.QueuePlan.Plan.RxQueue) == 8;
    }

    private static bool TestRejectsFailedSetupWindowExecution()
    {
        TestContext context = CreateContext();
        byte[] commonBytes = CreateCommonWindowBytes(length: 1);
        VirtioNetQueueMmioSetupRegisterWindowExecutionResult setupExecution =
            ExecuteQueueSetupWindow(context, commonBytes);
        byte[] queueMemory = CreateRxQueueMemory(context);
        byte[] notifyBytes = CreateNotifyWindowBytes();

        VirtioNetReceiveReadinessResult result = ComposeSuccessfulShape(
            context,
            setupExecution,
            queueMemory,
            notifyBytes,
            VirtioNetQueueNotificationBarrierReadiness.Ready(42));

        return !setupExecution.Succeeded
            && !result.Succeeded
            && result.Status == VirtioNetReceiveReadinessStatus.QueueSetupWindowExecutionFailed
            && result.QueueSetupWindowExecutionStatus ==
                VirtioNetQueueMmioSetupRegisterWindowExecutionStatus.AccessExecutionFailed
            && result.QueueSetupAccessExecutionStatus ==
                VirtioPciBarRegisterWindowAccessExecutionStatus.WindowBufferTooSmall
            && !result.ReceivePublishAttempted
            && !result.NotificationPlanAttempted
            && !result.NotificationWindowExecutionAttempted
            && BytesAreZero(queueMemory)
            && AllBytesEqual(notifyBytes, 0xCC);
    }

    private static bool TestRejectsFailedPublish()
    {
        TestContext context = CreateContext();
        byte[] commonBytes = CreateCommonWindowBytes();
        VirtioNetQueueMmioSetupRegisterWindowExecutionResult setupExecution =
            ExecuteQueueSetupWindow(context, commonBytes);
        byte[] shortQueueMemory = [0xCC];
        byte[] notifyBytes = CreateNotifyWindowBytes();

        VirtioNetReceiveReadinessResult result = ComposeSuccessfulShape(
            context,
            setupExecution,
            shortQueueMemory,
            notifyBytes,
            VirtioNetQueueNotificationBarrierReadiness.Ready(43));

        return setupExecution.Succeeded
            && !result.Succeeded
            && result.Status == VirtioNetReceiveReadinessStatus.ReceivePublishFailed
            && result.ReceivePublishStatus == VirtioNetReceiveLifecyclePublishStatus.QueueMemoryTooSmall
            && result.ReceivePublishAttempted
            && !result.NotificationPlanAttempted
            && !result.NotificationWindowExecutionAttempted
            && AllBytesEqual(shortQueueMemory, 0xCC)
            && AllBytesEqual(notifyBytes, 0xCC);
    }

    private static bool TestRejectsMissingBarrier()
    {
        TestContext context = CreateContext();
        byte[] commonBytes = CreateCommonWindowBytes();
        VirtioNetQueueMmioSetupRegisterWindowExecutionResult setupExecution =
            ExecuteQueueSetupWindow(context, commonBytes);
        byte[] queueMemory = CreateRxQueueMemory(context);
        byte[] notifyBytes = CreateNotifyWindowBytes();
        VirtioNetQueueNotificationBarrierReadiness missingBarrier =
            new VirtioNetQueueNotificationBarrierReadiness(
                descriptorWritesVisible: true,
                availableRingWritesVisible: false,
                orderedBeforeNotify: true,
                barrierSequence: 44);

        VirtioNetReceiveReadinessResult result = ComposeSuccessfulShape(
            context,
            setupExecution,
            queueMemory,
            notifyBytes,
            missingBarrier);

        return !result.Succeeded
            && result.Status == VirtioNetReceiveReadinessStatus.BarrierNotReady
            && result.NotificationWindowExecutionStatus ==
                VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.MemoryBarrierNotReady
            && result.NotificationRegisterWindowStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.ReceivePublishAttempted
            && result.NotificationPlanAttempted
            && result.NotificationWindowExecutionAttempted
            && !result.BarrierSucceeded
            && result.BarrierSequence == 44
            && ReadAvailableIndex(queueMemory, context.QueuePlan.Plan.RxQueue) == 8
            && AllBytesEqual(notifyBytes, 0xCC);
    }

    private static bool TestPropagatesNotificationPlanFailure()
    {
        TestContext context = CreateContext();
        byte[] commonBytes = CreateCommonWindowBytes();
        VirtioNetQueueMmioSetupRegisterWindowExecutionResult setupExecution =
            ExecuteQueueSetupWindow(context, commonBytes);
        byte[] queueMemory = CreateRxQueueMemory(context);
        byte[] notifyBytes = CreateNotifyWindowBytes();

        VirtioNetReceiveReadinessResult result =
            VirtioNetReceiveReadinessPlanner.ComposeReceive(
                context.QueueSetup,
                setupExecution,
                firstDescriptorIndex: 1,
                availableDescriptorCount: RxBufferCount,
                previousAvailableRingIndex: 6,
                previousUsedIndex: 4,
                RxBufferPhysicalAddress,
                receiveBufferStrideBytes: FrameBufferBytes,
                receiveBufferCount: 2,
                frameBufferBytes: FrameBufferBytes,
                queueMemory,
                VirtioNetQueueNotificationBarrierReadiness.Ready(45),
                context.Apertures.NotifyConfiguration,
                context.RegisterWindows.Bindings.NotifyConfiguration,
                notifyBytes,
                negotiatedFeatures: 0,
                queueEnabled: false);

        return !result.Succeeded
            && result.Status == VirtioNetReceiveReadinessStatus.NotificationPlanFailed
            && result.NotificationPlanStatus == VirtioNetQueueNotificationPlanStatus.NotificationPlanFailed
            && result.PciNotificationStatus == VirtioPciQueueNotificationPlanStatus.QueueNotEnabled
            && result.ReceivePublishAttempted
            && result.NotificationPlanAttempted
            && !result.NotificationWindowExecutionAttempted
            && ReadAvailableIndex(queueMemory, context.QueuePlan.Plan.RxQueue) == 8
            && AllBytesEqual(notifyBytes, 0xCC);
    }

    private static bool TestPropagatesNotificationWindowFailure()
    {
        TestContext context = CreateContext();
        byte[] commonBytes = CreateCommonWindowBytes();
        VirtioNetQueueMmioSetupRegisterWindowExecutionResult setupExecution =
            ExecuteQueueSetupWindow(context, commonBytes);
        byte[] queueMemory = CreateRxQueueMemory(context);
        byte[] notifyBytes = CreateNotifyWindowBytes(
            checked((int)(NotifyApertureBarOffset + RxNotifyOffset * NotifyOffsetMultiplier + 1)));

        VirtioNetReceiveReadinessResult result = ComposeSuccessfulShape(
            context,
            setupExecution,
            queueMemory,
            notifyBytes,
            VirtioNetQueueNotificationBarrierReadiness.Ready(46));

        return !result.Succeeded
            && result.Status == VirtioNetReceiveReadinessStatus.NotificationWindowExecutionFailed
            && result.NotificationWindowExecutionStatus ==
                VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.AccessExecutionFailed
            && result.NotificationRegisterWindowStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.NotificationAccessExecutionStatus ==
                VirtioPciBarRegisterWindowAccessExecutionStatus.WindowBufferTooSmall
            && result.NotificationWindowExecutionAttempted
            && result.BarrierSucceeded
            && result.BarrierSequence == 46
            && AllBytesEqual(notifyBytes, 0xCC);
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        VirtioNetReceiveReadinessResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioNetReceiveReadinessStatus.Success
            && !result.ReceivePublishAttempted
            && !result.NotificationPlanAttempted
            && !result.NotificationWindowExecutionAttempted
            && !result.BarrierSucceeded
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioNetReceiveReadinessResult>();
    }

    private static bool TestPreservesNoLiveMmioBoundary()
    {
        TestContext context = CreateContext();
        byte[] commonBytes = CreateCommonWindowBytes();
        VirtioNetQueueMmioSetupRegisterWindowExecutionResult setupExecution =
            ExecuteQueueSetupWindow(context, commonBytes);
        byte[] queueMemory = CreateRxQueueMemory(context);
        byte[] notifyBytes = CreateNotifyWindowBytes();

        VirtioNetReceiveReadinessResult result = ComposeSuccessfulShape(
            context,
            setupExecution,
            queueMemory,
            notifyBytes,
            VirtioNetQueueNotificationBarrierReadiness.Ready(47));

        return result.Succeeded
            && VirtioNetReceiveReadinessPlanner.RequiresQueueSetupRegisterWindowExecution
            && VirtioNetReceiveReadinessPlanner.RequiresRegisterWindowBinding
            && VirtioNetReceiveReadinessPlanner.RequiresBarrierReadiness
            && VirtioNetReceiveReadinessPlanner.UsesCallerOwnedQueueMemory
            && VirtioNetReceiveReadinessPlanner.UsesCallerOwnedNotifyWindowBuffer
            && VirtioNetReceiveReadinessPlanner.PublishesCallerOwnedReceiveDescriptors
            && VirtioNetReceiveReadinessPlanner.WritesQueueMemory
            && VirtioNetReceiveReadinessPlanner.InvokesRegisterWindowAccessExecutor
            && VirtioNetReceiveReadinessPlanner.WritesCallerOwnedNotifyWindowBuffer
            && !VirtioNetReceiveReadinessPlanner.PerformsLiveMmio
            && !VirtioNetReceiveReadinessPlanner.MapsBars
            && !VirtioNetReceiveReadinessPlanner.ContainsVolatileAccess
            && !VirtioNetReceiveReadinessPlanner.AccessesPhysicalAddresses
            && !VirtioNetReceiveReadinessPlanner.ExecutesMemoryBarriers
            && !VirtioNetReceiveReadinessPlanner.ConfiguresLiveQueues
            && !VirtioNetReceiveReadinessPlanner.NotifiesLiveDevice
            && !VirtioNetReceiveReadinessPlanner.PollsDevice
            && !VirtioNetReceiveReadinessPlanner.HandlesInterrupts
            && !VirtioNetReceiveReadinessPlanner.PerformsDmaMapping
            && !VirtioNetReceiveReadinessPlanner.AllocatesDmaMemory
            && !VirtioNetReceiveReadinessPlanner.PerformsPacketIo
            && !VirtioNetReceiveReadinessPlanner.PerformsPacketReceive
            && !VirtioNetReceiveReadinessPlanner.ReadsQueueMemory
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioNetReceiveReadinessResult>();
    }

    private static VirtioNetReceiveReadinessResult ComposeSuccessfulShape(
        TestContext context,
        VirtioNetQueueMmioSetupRegisterWindowExecutionResult setupExecution,
        Span<byte> queueMemory,
        Span<byte> notifyBytes,
        VirtioNetQueueNotificationBarrierReadiness barrier)
    {
        return VirtioNetReceiveReadinessPlanner.ComposeReceive(
            context.QueueSetup,
            setupExecution,
            firstDescriptorIndex: 1,
            availableDescriptorCount: RxBufferCount,
            previousAvailableRingIndex: 6,
            previousUsedIndex: 4,
            RxBufferPhysicalAddress,
            receiveBufferStrideBytes: FrameBufferBytes,
            receiveBufferCount: 2,
            frameBufferBytes: FrameBufferBytes,
            queueMemory,
            barrier,
            context.Apertures.NotifyConfiguration,
            context.RegisterWindows.Bindings.NotifyConfiguration,
            notifyBytes,
            negotiatedFeatures: 0);
    }

    private static VirtioNetQueueMmioSetupRegisterWindowExecutionResult ExecuteQueueSetupWindow(
        TestContext context,
        Span<byte> commonBytes)
    {
        return VirtioNetQueueMmioSetupRegisterWindowExecutor.Execute(
            context.QueueSetup,
            context.QueueSetupCommands,
            context.RegisterWindows.Bindings.CommonConfiguration,
            commonBytes);
    }

    private static TestContext CreateContext()
    {
        VirtioPciBindingPlanResult bindingPlan = CreateBindingPlan();
        VirtioPciBarRegisterWindowBindingResult registerWindows =
            VirtioPciBarRegisterWindowPlanner.BindRequiredApertures(
                bindingPlan,
                CreateRegisterWindows());
        VirtioDeviceDescriptor descriptor = CreateNetworkDescriptor(bindingPlan);
        VirtioNetQueuePlanResult queuePlan = VirtioNetQueuePlan.Create(
            VirtioNetQueuePlan.MinimumQueueCount,
            RxQueueSize,
            TxQueueSize,
            RxBufferCount,
            FrameBufferBytes);
        if (!queuePlan.Succeeded)
        {
            throw new InvalidOperationException("Failed to create deterministic queue plan.");
        }

        VirtioNetDriverQueueBringUpPlanResult bringUp = VirtioNetDriverQueueBringUpPlan.Create(
            descriptor,
            RxQueueSize,
            TxQueueSize,
            RxBufferCount,
            FrameBufferBytes,
            queuePlan.Plan.RxQueue.RequiredByteLength,
            queuePlan.Plan.TxQueue.RequiredByteLength,
            queuePlan.Plan.RxBuffers.RequiredBufferBytes,
            queuePlan.Plan.TxFrame.RequiredBufferBytes);
        if (!bringUp.Succeeded)
        {
            throw new InvalidOperationException("Failed to create deterministic bring-up plan.");
        }

        VirtioNetQueueMmioSetupCommand[] commands =
            new VirtioNetQueueMmioSetupCommand[VirtioNetQueueMmioSetupPlanner.RequiredCommandCount];
        VirtioNetQueueMmioSetupPlanResult queueSetup = VirtioNetQueueMmioSetupPlanner.Create(
            bringUp,
            CreateRxAddresses(),
            CreateTxAddresses(),
            CreateRxMetadata(),
            CreateTxMetadata(),
            commands);
        if (!queueSetup.Succeeded ||
            !registerWindows.Succeeded)
        {
            throw new InvalidOperationException("Failed to create deterministic register-window setup.");
        }

        return new TestContext(
            bindingPlan.Binding.Apertures,
            registerWindows,
            queueSetup,
            commands,
            queuePlan);
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

    private static VirtioDeviceDescriptor CreateNetworkDescriptor(
        VirtioPciBindingPlanResult bindingPlan)
    {
        VirtioDeviceDescriptorSource[] sources =
        [
            new VirtioDeviceDescriptorSource(
                sourceIndex: 44,
                bindingPlan,
                VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask,
                VirtioDeviceInitializationProfile.NetworkRequiredQueueCount,
                featuresOkAccepted: true)
        ];
        VirtioDeviceConfigurationSource[] configurations =
        [
            new VirtioDeviceConfigurationSource(
                sourceIndex: 44,
                CreateNetworkConfiguration(
                    new MacAddress(0x02, 0x33, 0x44, 0x55, 0x66, 0xD0),
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
            offsetValue: 0x20,
            length: VirtioPciDeviceDiscovery.CommonConfigurationMinimumLength,
            notifyOffsetMultiplier: 0,
            capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);
        WriteVirtioCapability(
            config,
            NotifyOffset,
            IsrOffset,
            VirtioPciCapabilityType.NotifyConfiguration,
            bar: 1,
            offsetValue: NotifyApertureBarOffset,
            length: 0x100,
            notifyOffsetMultiplier: NotifyOffsetMultiplier,
            capabilityLength: VirtioPciCapabilityListParser.ModernVirtioNotifyCapabilityLength);
        WriteVirtioCapability(
            config,
            IsrOffset,
            DeviceOffset,
            VirtioPciCapabilityType.IsrStatus,
            bar: 2,
            offsetValue: 0,
            length: 1,
            notifyOffsetMultiplier: 0,
            capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);
        WriteVirtioCapability(
            config,
            DeviceOffset,
            nextOffset: 0,
            VirtioPciCapabilityType.DeviceConfiguration,
            bar: 3,
            offsetValue: 0,
            length: 0x10,
            notifyOffsetMultiplier: 0,
            capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);

        return config;
    }

    private static ulong[] CreateBarSizes()
    {
        return [0x1000, 0x1000, 0x1000, 0x1000, 0, 0];
    }

    private static VirtioPciMappedBarWindow[] CreateRegisterWindows()
    {
        return
        [
            CreateRegisterWindow(0, 0x80000000UL, 0xFFFF800000000000UL),
            CreateRegisterWindow(1, NotifyBarPhysicalBase, NotifyBarVirtualBase),
            CreateRegisterWindow(2, 0x80002000UL, 0xFFFF800000002000UL),
            CreateRegisterWindow(3, 0x80003000UL, 0xFFFF800000003000UL),
            default,
            default
        ];
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

    private static byte[] CreateNetworkConfiguration(MacAddress mac, ushort status)
    {
        byte[] configuration = new byte[8];
        configuration[0] = mac.Octet0;
        configuration[1] = mac.Octet1;
        configuration[2] = mac.Octet2;
        configuration[3] = mac.Octet3;
        configuration[4] = mac.Octet4;
        configuration[5] = mac.Octet5;
        EndianPrimitives.WriteUInt16LittleEndian(configuration.AsSpan(6, sizeof(ushort)), status);
        return configuration;
    }

    private static void WriteVirtioCapability(
        Span<byte> config,
        byte offset,
        byte nextOffset,
        VirtioPciCapabilityType type,
        byte bar,
        uint offsetValue,
        uint length,
        uint notifyOffsetMultiplier,
        byte capabilityLength)
    {
        config[offset] = VirtioPciCapabilityListParser.VendorSpecificCapabilityId;
        config[offset + 1] = nextOffset;
        config[offset + 2] = capabilityLength;
        config[offset + 3] = (byte)type;
        config[offset + 4] = bar;
        WriteUInt32(config, offset + 8, offsetValue);
        WriteUInt32(config, offset + 12, length);
        if (type == VirtioPciCapabilityType.NotifyConfiguration)
        {
            WriteUInt32(config, offset + 16, notifyOffsetMultiplier);
        }
    }

    private static void WriteUInt32(Span<byte> destination, int offset, uint value)
    {
        EndianPrimitives.WriteUInt32LittleEndian(destination.Slice(offset, sizeof(uint)), value);
    }

    private static byte[] CreateRxQueueMemory(TestContext context)
    {
        return new byte[(int)context.QueuePlan.Plan.RxQueue.EndOffset];
    }

    private static byte[] CreateCommonWindowBytes(int length = 0x1000)
    {
        byte[] bytes = new byte[length];
        Fill(bytes, 0xCC);
        return bytes;
    }

    private static byte[] CreateNotifyWindowBytes(int length = 0x1000)
    {
        byte[] notifyBytes = new byte[length];
        Fill(notifyBytes, 0xCC);
        return notifyBytes;
    }

    private static ushort ReadAvailableIndex(
        ReadOnlySpan<byte> queueMemory,
        VirtqueueSplitRingPlan queue)
    {
        return EndianPrimitives.ReadUInt16LittleEndian(
            queueMemory.Slice((int)queue.AvailableRing.Offset + 2, sizeof(ushort)));
    }

    private static ushort ReadAvailableEntry(
        ReadOnlySpan<byte> queueMemory,
        VirtqueueSplitRingPlan queue,
        ushort slot)
    {
        uint offset = queue.AvailableRing.Offset +
            VirtqueueSplitRingPlan.AvailableRingHeaderLength +
            ((uint)slot * VirtqueueSplitRingPlan.AvailableRingEntryLength);
        return EndianPrimitives.ReadUInt16LittleEndian(
            queueMemory.Slice((int)offset, sizeof(ushort)));
    }

    private static ushort ReadUInt16AtBarOffset(ReadOnlySpan<byte> source, ulong barRelativeOffset)
    {
        return EndianPrimitives.ReadUInt16LittleEndian(
            source.Slice(checked((int)barRelativeOffset), sizeof(ushort)));
    }

    private static bool DescriptorMatches(
        ReadOnlySpan<byte> queueMemory,
        VirtqueueSplitRingPlan queue,
        ushort descriptorIndex,
        ulong expectedAddress,
        uint expectedLength)
    {
        uint descriptorOffset = queue.DescriptorTable.Offset +
            ((uint)descriptorIndex * VirtqueueSplitRingPlan.DescriptorLength);
        ReadOnlySpan<byte> descriptor =
            queueMemory.Slice((int)descriptorOffset, (int)VirtqueueSplitRingPlan.DescriptorLength);
        return EndianPrimitives.ReadUInt64LittleEndian(descriptor.Slice(0, sizeof(ulong))) == expectedAddress
            && EndianPrimitives.ReadUInt32LittleEndian(descriptor.Slice(8, sizeof(uint))) == expectedLength
            && EndianPrimitives.ReadUInt16LittleEndian(descriptor.Slice(12, sizeof(ushort))) == 0x0002
            && EndianPrimitives.ReadUInt16LittleEndian(descriptor.Slice(14, sizeof(ushort))) == 0;
    }

    private static bool BytesAreZero(ReadOnlySpan<byte> bytes)
    {
        for (int index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool AllBytesEqual(ReadOnlySpan<byte> bytes, byte expected)
    {
        for (int index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] != expected)
            {
                return false;
            }
        }

        return true;
    }

    private static void Fill(Span<byte> bytes, byte value)
    {
        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = value;
        }
    }

    private readonly struct TestContext
    {
        public TestContext(
            VirtioPciBarApertureSet apertures,
            VirtioPciBarRegisterWindowBindingResult registerWindows,
            VirtioNetQueueMmioSetupPlanResult queueSetup,
            VirtioNetQueueMmioSetupCommand[] queueSetupCommands,
            VirtioNetQueuePlanResult queuePlan)
        {
            Apertures = apertures;
            RegisterWindows = registerWindows;
            QueueSetup = queueSetup;
            QueueSetupCommands = queueSetupCommands;
            QueuePlan = queuePlan;
        }

        public VirtioPciBarApertureSet Apertures { get; }

        public VirtioPciBarRegisterWindowBindingResult RegisterWindows { get; }

        public VirtioNetQueueMmioSetupPlanResult QueueSetup { get; }

        public VirtioNetQueueMmioSetupCommand[] QueueSetupCommands { get; }

        public VirtioNetQueuePlanResult QueuePlan { get; }
    }
}
