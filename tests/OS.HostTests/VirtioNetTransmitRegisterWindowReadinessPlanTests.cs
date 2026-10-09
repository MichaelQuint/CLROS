using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;

internal static class VirtioNetTransmitRegisterWindowReadinessPlanTests
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
    private const ulong TxHeaderPhysicalAddress = 0x0050_0000UL;
    private const ulong TxFramePhysicalAddress = 0x0060_0000UL;

    [HostTest("drivers.virtionet.tx.readiness.windowexec default result is not success", HostLane.Fast, 1)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        VirtioNetTransmitRegisterWindowReadinessResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioNetTransmitRegisterWindowReadinessStatus.Success
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioNetTransmitRegisterWindowReadinessResult>());
    }

    [HostTest("drivers.virtionet.tx.readiness.windowexec composes setup publish notify", HostLane.Fast, 0)]
    private static bool TestComposesSetupPublishNotify()
    {
        TestContext context = CreateContext();
        byte[] commonBytes = CreateCommonWindowBytes();
        VirtioNetQueueMmioSetupRegisterWindowExecutionResult setupExecution =
            ExecuteQueueSetupWindow(context, commonBytes);
        VirtioNetTransmitDescriptorOwnershipState[] states =
            new VirtioNetTransmitDescriptorOwnershipState[TxQueueSize];
        byte[] txHeader = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers];
        byte[] queueMemory = CreateTxQueueMemory(context);
        byte[] notifyBytes = CreateNotifyWindowBytes();

        VirtioNetTransmitRegisterWindowReadinessResult result = ComposeSuccessfulShape(
            context,
            setupExecution,
            states,
            txHeader,
            queueMemory,
            notifyBytes,
            VirtioNetQueueNotificationBarrierReadiness.Ready(51));

        return HostCheck.Result(setupExecution.Succeeded
            && result.Succeeded
            && result.Status == VirtioNetTransmitRegisterWindowReadinessStatus.Success
            && result.QueueSetupPlanStatus == VirtioNetQueueMmioSetupPlanStatus.Success
            && result.QueueSetupWindowExecutionStatus ==
                VirtioNetQueueMmioSetupRegisterWindowExecutionStatus.Success
            && result.QueueSetupRegisterWindowStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.QueueSetupAccessExecutionStatus ==
                VirtioPciBarRegisterWindowAccessExecutionStatus.Success
            && result.TransmitPublishPlanStatus == VirtioNetTransmitDescriptorPublishPlanStatus.Success
            && result.TransmitPublishStatus == VirtioNetTransmitLifecycleStatus.Success
            && result.NotificationPlanStatus == VirtioNetQueueNotificationPlanStatus.Success
            && result.PciNotificationStatus == VirtioPciQueueNotificationPlanStatus.Success
            && result.NotificationWindowExecutionStatus ==
                VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.Success
            && result.NotificationRegisterWindowStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.NotificationAccessExecutionStatus ==
                VirtioPciBarRegisterWindowAccessExecutionStatus.Success
            && result.TransmitPublishAttempted
            && result.NotificationPlanAttempted
            && result.NotificationWindowExecutionAttempted
            && result.BarrierSucceeded
            && result.BarrierSequence == 51
            && result.HeaderDescriptorIndex == 0
            && result.FrameDescriptorIndex == 1
            && result.DescriptorsWritten == VirtioNetTransmitLifecycle.RequiredDescriptorCount
            && result.DescriptorsMarkedInFlight == VirtioNetTransmitLifecycle.RequiredDescriptorCount
            && result.QueueIndex == VirtioNetQueuePlan.TransmitQueueIndex
            && result.QueueNotifyOffset == TxNotifyOffset
            && result.PublishedDescriptorCount == VirtioNetTransmitLifecycle.RequiredDescriptorCount
            && result.PreviousAvailableRingIndex == 11
            && result.NextAvailableRingIndex == 12
            && result.NotifyRelativeOffset == TxNotifyOffset * NotifyOffsetMultiplier
            && result.WriteValue == VirtioNetQueuePlan.TransmitQueueIndex
            && result.WriteWidthBytes == sizeof(ushort)
            && result.CapabilityType == VirtioPciCapabilityType.NotifyConfiguration
            && result.BarIndex == 1
            && result.AccessWidth == VirtioPciRegisterAccessWidth.Word
            && result.AccessWidthBytes == sizeof(ushort)
            && result.BarRelativeOffset == NotifyApertureBarOffset + result.NotifyRelativeOffset
            && result.WrittenValue == VirtioNetQueuePlan.TransmitQueueIndex
            && ReadUInt16AtBarOffset(notifyBytes, result.BarRelativeOffset) ==
                VirtioNetQueuePlan.TransmitQueueIndex
            && states[0] == VirtioNetTransmitDescriptorOwnershipState.InFlight
            && states[1] == VirtioNetTransmitDescriptorOwnershipState.InFlight
            && BytesAreZero(txHeader)
            && DescriptorMatches(
                queueMemory,
                context.QueuePlan.Plan.TxQueue,
                descriptorIndex: 0,
                expectedAddress: TxHeaderPhysicalAddress,
                expectedLength: VirtioNetTransmitLifecycle.TxHeaderBytes,
                expectedFlags: 1,
                expectedNext: 1)
            && DescriptorMatches(
                queueMemory,
                context.QueuePlan.Plan.TxQueue,
                descriptorIndex: 1,
                expectedAddress: TxFramePhysicalAddress,
                expectedLength: (uint)CreateEthernetFrame().Length,
                expectedFlags: 0,
                expectedNext: 0)
            && ReadAvailableEntry(queueMemory, context.QueuePlan.Plan.TxQueue, slot: 3) == 0
            && ReadAvailableIndex(queueMemory, context.QueuePlan.Plan.TxQueue) == 12);
    }

    [HostTest("drivers.virtionet.tx.readiness.windowexec rejects failed setup window execution", HostLane.Fast, 2)]
    private static bool TestRejectsFailedSetupWindowExecution()
    {
        TestContext context = CreateContext();
        byte[] commonBytes = CreateCommonWindowBytes(length: 1);
        VirtioNetQueueMmioSetupRegisterWindowExecutionResult setupExecution =
            ExecuteQueueSetupWindow(context, commonBytes);
        VirtioNetTransmitDescriptorOwnershipState[] states =
            new VirtioNetTransmitDescriptorOwnershipState[TxQueueSize];
        byte[] txHeader = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers];
        byte[] queueMemory = CreateTxQueueMemory(context);
        byte[] notifyBytes = CreateNotifyWindowBytes();

        VirtioNetTransmitRegisterWindowReadinessResult result = ComposeSuccessfulShape(
            context,
            setupExecution,
            states,
            txHeader,
            queueMemory,
            notifyBytes,
            VirtioNetQueueNotificationBarrierReadiness.Ready(52));

        return HostCheck.Result(!setupExecution.Succeeded
            && !result.Succeeded
            && result.Status ==
                VirtioNetTransmitRegisterWindowReadinessStatus.QueueSetupWindowExecutionFailed
            && result.QueueSetupWindowExecutionStatus ==
                VirtioNetQueueMmioSetupRegisterWindowExecutionStatus.AccessExecutionFailed
            && result.QueueSetupAccessExecutionStatus ==
                VirtioPciBarRegisterWindowAccessExecutionStatus.WindowBufferTooSmall
            && !result.TransmitPublishAttempted
            && !result.NotificationPlanAttempted
            && !result.NotificationWindowExecutionAttempted
            && AllStatesAre(states, VirtioNetTransmitDescriptorOwnershipState.Free)
            && BytesAreZero(txHeader)
            && BytesAreZero(queueMemory)
            && AllBytesEqual(notifyBytes, 0xCC));
    }

    [HostTest("drivers.virtionet.tx.readiness.windowexec rejects failed publish", HostLane.Fast, 3)]
    private static bool TestRejectsFailedPublish()
    {
        TestContext context = CreateContext();
        byte[] commonBytes = CreateCommonWindowBytes();
        VirtioNetQueueMmioSetupRegisterWindowExecutionResult setupExecution =
            ExecuteQueueSetupWindow(context, commonBytes);
        VirtioNetTransmitDescriptorOwnershipState[] states =
            new VirtioNetTransmitDescriptorOwnershipState[TxQueueSize];
        byte[] shortTxHeader = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers - 1];
        byte[] queueMemory = CreateTxQueueMemory(context);
        byte[] notifyBytes = CreateNotifyWindowBytes();

        VirtioNetTransmitRegisterWindowReadinessResult result = ComposeSuccessfulShape(
            context,
            setupExecution,
            states,
            shortTxHeader,
            queueMemory,
            notifyBytes,
            VirtioNetQueueNotificationBarrierReadiness.Ready(53));

        return HostCheck.Result(setupExecution.Succeeded
            && !result.Succeeded
            && result.Status == VirtioNetTransmitRegisterWindowReadinessStatus.TransmitPublishFailed
            && result.TransmitPublishPlanStatus ==
                VirtioNetTransmitDescriptorPublishPlanStatus.TransmitPublishFailed
            && result.TransmitPublishStatus == VirtioNetTransmitLifecycleStatus.HeaderBufferTooSmall
            && result.TransmitPublishAttempted
            && !result.NotificationPlanAttempted
            && !result.NotificationWindowExecutionAttempted
            && AllStatesAre(states, VirtioNetTransmitDescriptorOwnershipState.Free)
            && BytesAreZero(queueMemory)
            && AllBytesEqual(notifyBytes, 0xCC));
    }

    [HostTest("drivers.virtionet.tx.readiness.windowexec rejects missing barrier", HostLane.Fast, 4)]
    private static bool TestRejectsMissingBarrier()
    {
        TestContext context = CreateContext();
        byte[] commonBytes = CreateCommonWindowBytes();
        VirtioNetQueueMmioSetupRegisterWindowExecutionResult setupExecution =
            ExecuteQueueSetupWindow(context, commonBytes);
        VirtioNetTransmitDescriptorOwnershipState[] states =
            new VirtioNetTransmitDescriptorOwnershipState[TxQueueSize];
        byte[] txHeader = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers];
        byte[] queueMemory = CreateTxQueueMemory(context);
        byte[] notifyBytes = CreateNotifyWindowBytes();
        VirtioNetQueueNotificationBarrierReadiness missingBarrier =
            new VirtioNetQueueNotificationBarrierReadiness(
                descriptorWritesVisible: true,
                availableRingWritesVisible: false,
                orderedBeforeNotify: true,
                barrierSequence: 54);

        VirtioNetTransmitRegisterWindowReadinessResult result = ComposeSuccessfulShape(
            context,
            setupExecution,
            states,
            txHeader,
            queueMemory,
            notifyBytes,
            missingBarrier);

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioNetTransmitRegisterWindowReadinessStatus.BarrierNotReady
            && result.NotificationWindowExecutionStatus ==
                VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.MemoryBarrierNotReady
            && result.NotificationRegisterWindowStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.TransmitPublishAttempted
            && result.NotificationPlanAttempted
            && result.NotificationWindowExecutionAttempted
            && !result.BarrierSucceeded
            && result.BarrierSequence == 54
            && states[0] == VirtioNetTransmitDescriptorOwnershipState.InFlight
            && states[1] == VirtioNetTransmitDescriptorOwnershipState.InFlight
            && AllBytesEqual(notifyBytes, 0xCC));
    }

    [HostTest("drivers.virtionet.tx.readiness.windowexec propagates notification plan failure", HostLane.Fast, 5)]
    private static bool TestPropagatesNotificationPlanFailure()
    {
        TestContext context = CreateContext();
        byte[] commonBytes = CreateCommonWindowBytes();
        VirtioNetQueueMmioSetupRegisterWindowExecutionResult setupExecution =
            ExecuteQueueSetupWindow(context, commonBytes);
        VirtioNetTransmitDescriptorOwnershipState[] states =
            new VirtioNetTransmitDescriptorOwnershipState[TxQueueSize];
        byte[] txHeader = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers];
        byte[] queueMemory = CreateTxQueueMemory(context);
        byte[] notifyBytes = CreateNotifyWindowBytes();

        VirtioNetTransmitRegisterWindowReadinessResult result =
            VirtioNetTransmitRegisterWindowReadinessPlanner.ComposeTransmit(
                context.QueueSetup,
                setupExecution,
                context.QueuePlan,
                states,
                availableRingIndex: 11,
                TxHeaderPhysicalAddress,
                TxFramePhysicalAddress,
                CreateEthernetFrame(),
                txHeader,
                queueMemory,
                VirtioNetQueueNotificationBarrierReadiness.Ready(55),
                context.Apertures.NotifyConfiguration,
                context.RegisterWindows.Bindings.NotifyConfiguration,
                notifyBytes,
                negotiatedFeatures: 0,
                queueEnabled: false);

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioNetTransmitRegisterWindowReadinessStatus.NotificationPlanFailed
            && result.NotificationPlanStatus == VirtioNetQueueNotificationPlanStatus.NotificationPlanFailed
            && result.PciNotificationStatus == VirtioPciQueueNotificationPlanStatus.QueueNotEnabled
            && result.TransmitPublishAttempted
            && result.NotificationPlanAttempted
            && !result.NotificationWindowExecutionAttempted
            && states[0] == VirtioNetTransmitDescriptorOwnershipState.InFlight
            && states[1] == VirtioNetTransmitDescriptorOwnershipState.InFlight
            && AllBytesEqual(notifyBytes, 0xCC));
    }

    [HostTest("drivers.virtionet.tx.readiness.windowexec propagates notification window failure", HostLane.Fast, 6)]
    private static bool TestPropagatesNotificationWindowFailure()
    {
        TestContext context = CreateContext();
        byte[] commonBytes = CreateCommonWindowBytes();
        VirtioNetQueueMmioSetupRegisterWindowExecutionResult setupExecution =
            ExecuteQueueSetupWindow(context, commonBytes);
        VirtioNetTransmitDescriptorOwnershipState[] states =
            new VirtioNetTransmitDescriptorOwnershipState[TxQueueSize];
        byte[] txHeader = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers];
        byte[] queueMemory = CreateTxQueueMemory(context);
        byte[] notifyBytes = CreateNotifyWindowBytes(
            checked((int)(NotifyApertureBarOffset + TxNotifyOffset * NotifyOffsetMultiplier + 1)));

        VirtioNetTransmitRegisterWindowReadinessResult result = ComposeSuccessfulShape(
            context,
            setupExecution,
            states,
            txHeader,
            queueMemory,
            notifyBytes,
            VirtioNetQueueNotificationBarrierReadiness.Ready(56));

        return HostCheck.Result(!result.Succeeded
            && result.Status ==
                VirtioNetTransmitRegisterWindowReadinessStatus.NotificationWindowExecutionFailed
            && result.NotificationWindowExecutionStatus ==
                VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.AccessExecutionFailed
            && result.NotificationRegisterWindowStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.NotificationAccessExecutionStatus ==
                VirtioPciBarRegisterWindowAccessExecutionStatus.WindowBufferTooSmall
            && result.NotificationWindowExecutionAttempted
            && result.BarrierSucceeded
            && result.BarrierSequence == 56
            && AllBytesEqual(notifyBytes, 0xCC));
    }

    [HostTest("drivers.virtionet.tx.readiness.windowexec preserves no-live-mmio boundary", HostLane.Fast, 7)]
    private static bool TestPreservesNoLiveMmioBoundary()
    {
        TestContext context = CreateContext();
        byte[] commonBytes = CreateCommonWindowBytes();
        VirtioNetQueueMmioSetupRegisterWindowExecutionResult setupExecution =
            ExecuteQueueSetupWindow(context, commonBytes);
        VirtioNetTransmitDescriptorOwnershipState[] states =
            new VirtioNetTransmitDescriptorOwnershipState[TxQueueSize];
        byte[] txHeader = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers];
        byte[] queueMemory = CreateTxQueueMemory(context);
        byte[] notifyBytes = CreateNotifyWindowBytes();

        VirtioNetTransmitRegisterWindowReadinessResult result = ComposeSuccessfulShape(
            context,
            setupExecution,
            states,
            txHeader,
            queueMemory,
            notifyBytes,
            VirtioNetQueueNotificationBarrierReadiness.Ready(57));

        return HostCheck.Result(result.Succeeded
            && VirtioNetTransmitRegisterWindowReadinessPlanner.RequiresQueueSetupRegisterWindowExecution
            && VirtioNetTransmitRegisterWindowReadinessPlanner.RequiresRegisterWindowBinding
            && VirtioNetTransmitRegisterWindowReadinessPlanner.RequiresBarrierReadiness
            && VirtioNetTransmitRegisterWindowReadinessPlanner.UsesCallerOwnedQueueMemory
            && VirtioNetTransmitRegisterWindowReadinessPlanner.UsesCallerOwnedNotifyWindowBuffer
            && VirtioNetTransmitRegisterWindowReadinessPlanner.PublishesCallerOwnedTransmitDescriptors
            && VirtioNetTransmitRegisterWindowReadinessPlanner.WritesQueueMemory
            && VirtioNetTransmitRegisterWindowReadinessPlanner.WritesTransmitHeader
            && VirtioNetTransmitRegisterWindowReadinessPlanner.MutatesCallerOwnedStateTable
            && VirtioNetTransmitRegisterWindowReadinessPlanner.InvokesRegisterWindowAccessExecutor
            && VirtioNetTransmitRegisterWindowReadinessPlanner.WritesCallerOwnedNotifyWindowBuffer
            && !VirtioNetTransmitRegisterWindowReadinessPlanner.PerformsLiveMmio
            && !VirtioNetTransmitRegisterWindowReadinessPlanner.MapsBars
            && !VirtioNetTransmitRegisterWindowReadinessPlanner.ContainsVolatileAccess
            && !VirtioNetTransmitRegisterWindowReadinessPlanner.AccessesPhysicalAddresses
            && !VirtioNetTransmitRegisterWindowReadinessPlanner.ExecutesMemoryBarriers
            && !VirtioNetTransmitRegisterWindowReadinessPlanner.ConfiguresLiveQueues
            && !VirtioNetTransmitRegisterWindowReadinessPlanner.NotifiesLiveDevice
            && !VirtioNetTransmitRegisterWindowReadinessPlanner.PollsDevice
            && !VirtioNetTransmitRegisterWindowReadinessPlanner.HandlesInterrupts
            && !VirtioNetTransmitRegisterWindowReadinessPlanner.PerformsDmaMapping
            && !VirtioNetTransmitRegisterWindowReadinessPlanner.AllocatesDmaMemory
            && !VirtioNetTransmitRegisterWindowReadinessPlanner.PerformsPacketIo
            && !VirtioNetTransmitRegisterWindowReadinessPlanner.PerformsPacketTransmit
            && !VirtioNetTransmitRegisterWindowReadinessPlanner.WaitsForUsedRingCompletion
            && !VirtioNetTransmitRegisterWindowReadinessPlanner.ReadsQueueMemory
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioNetTransmitRegisterWindowReadinessResult>());
    }

    private static VirtioNetTransmitRegisterWindowReadinessResult ComposeSuccessfulShape(
        TestContext context,
        VirtioNetQueueMmioSetupRegisterWindowExecutionResult setupExecution,
        Span<VirtioNetTransmitDescriptorOwnershipState> states,
        Span<byte> txHeader,
        Span<byte> queueMemory,
        Span<byte> notifyBytes,
        VirtioNetQueueNotificationBarrierReadiness barrier)
    {
        return VirtioNetTransmitRegisterWindowReadinessPlanner.ComposeTransmit(
            context.QueueSetup,
            setupExecution,
            context.QueuePlan,
            states,
            availableRingIndex: 11,
            TxHeaderPhysicalAddress,
            TxFramePhysicalAddress,
            CreateEthernetFrame(),
            txHeader,
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

    private static byte[] CreateEthernetFrame()
    {
        byte[] frame = new byte[EthernetHeader.HeaderLength + 4];
        for (int i = 0; i < frame.Length; i++)
        {
            frame[i] = (byte)(0x20 + i);
        }

        return frame;
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
                sourceIndex: 45,
                bindingPlan,
                VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask,
                VirtioDeviceInitializationProfile.NetworkRequiredQueueCount,
                featuresOkAccepted: true)
        ];
        VirtioDeviceConfigurationSource[] configurations =
        [
            new VirtioDeviceConfigurationSource(
                sourceIndex: 45,
                CreateNetworkConfiguration(
                    new MacAddress(0x02, 0x33, 0x44, 0x55, 0x66, 0xD1),
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

    private static byte[] CreateTxQueueMemory(TestContext context)
    {
        return new byte[(int)context.QueuePlan.Plan.TxQueue.EndOffset];
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
        uint expectedLength,
        ushort expectedFlags,
        ushort expectedNext)
    {
        uint descriptorOffset = queue.DescriptorTable.Offset +
            ((uint)descriptorIndex * VirtqueueSplitRingPlan.DescriptorLength);
        ReadOnlySpan<byte> descriptor =
            queueMemory.Slice((int)descriptorOffset, (int)VirtqueueSplitRingPlan.DescriptorLength);
        return EndianPrimitives.ReadUInt64LittleEndian(descriptor.Slice(0, sizeof(ulong))) == expectedAddress
            && EndianPrimitives.ReadUInt32LittleEndian(descriptor.Slice(8, sizeof(uint))) == expectedLength
            && EndianPrimitives.ReadUInt16LittleEndian(descriptor.Slice(12, sizeof(ushort))) ==
                expectedFlags
            && EndianPrimitives.ReadUInt16LittleEndian(descriptor.Slice(14, sizeof(ushort))) ==
                expectedNext;
    }

    private static bool AllStatesAre(
        ReadOnlySpan<VirtioNetTransmitDescriptorOwnershipState> states,
        VirtioNetTransmitDescriptorOwnershipState expected)
    {
        for (int index = 0; index < states.Length; index++)
        {
            if (states[index] != expected)
            {
                return false;
            }
        }

        return true;
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
