using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;

internal static partial class VirtioNetTransmitRegisterWindowReclaimPlanTests
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
    [HostTest("drivers.virtionet.tx.reclaim.windowexec default result is not success", HostLane.Fast, 1)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        VirtioNetTransmitRegisterWindowReclaimResult result = default;
        return HostCheck.Result(!result.IsInitialized && !result.Succeeded && result.Status == VirtioNetTransmitRegisterWindowReclaimStatus.Success && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioNetTransmitRegisterWindowReclaimResult>());
    }

    [HostTest("drivers.virtionet.tx.reclaim.windowexec composes readiness completion ownership", HostLane.Fast, 0)]
    private static bool TestComposesReadinessCompletionOwnership()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitDescriptorOwnershipState[] states = new VirtioNetTransmitDescriptorOwnershipState[TxQueueSize];
        byte[] txHeader = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers];
        byte[] queueMemory = CreateTxQueueMemory(context);
        byte[] notifyBytes = CreateNotifyWindowBytes();
        VirtioNetTransmitRegisterWindowReadinessResult readiness = CreateSuccessfulReadiness(context, states, txHeader, queueMemory, notifyBytes, barrierSequence: 61);
        const ushort previousUsedIndex = 4;
        WriteUsedCompletion(readiness.TransmitPublish.TransmitPublish.Plan.TxQueue, queueMemory, previousUsedIndex, unchecked((ushort)(previousUsedIndex + 1)), readiness.TransmitPublish.TransmitPublish.HeadDescriptorIndex, usedByteCount: 0);
        VirtioNetTransmitRegisterWindowReclaimResult result = VirtioNetTransmitRegisterWindowReclaimPlanner.ApplyCompletedDescriptor(readiness, states, previousUsedIndex, queueMemory);
        return HostCheck.Result(readiness.Succeeded && result.Succeeded && result.Status == VirtioNetTransmitRegisterWindowReclaimStatus.Success && result.ReadinessStatus == VirtioNetTransmitRegisterWindowReadinessStatus.Success && result.CompletionPlanStatus == VirtioNetTransmitCompletionPlanStatus.Success && result.OwnershipStatus == VirtioNetTransmitDescriptorOwnershipStatus.Success && result.CompletionPlanAttempted && result.OwnershipReclaimAttempted && result.UsesRegisterWindowNotification && result.NotificationWindowExecutionStatus == VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.Success && result.NotificationRegisterWindowStatus == VirtioPciBarRegisterWindowPlanStatus.Success && result.NotificationWindowAccessExecutionStatus == VirtioPciBarRegisterWindowAccessExecutionStatus.Success && result.NotificationBarrierSucceeded && result.NotificationBarrierSequence == 61 && result.QueueIndex == VirtioNetQueuePlan.TransmitQueueIndex && result.QueueNotifyOffset == TxNotifyOffset && result.PublishedDescriptorCount == VirtioNetTransmitLifecycle.RequiredDescriptorCount && result.PreviousAvailableRingIndex == 11 && result.NextAvailableRingIndex == 12 && result.NotifyRelativeOffset == TxNotifyOffset * NotifyOffsetMultiplier && result.WriteValue == VirtioNetQueuePlan.TransmitQueueIndex && result.WriteWidthBytes == sizeof(ushort) && result.CapabilityType == VirtioPciCapabilityType.NotifyConfiguration && result.BarIndex == 1 && result.AccessWidth == VirtioPciRegisterAccessWidth.Word && result.AccessWidthBytes == sizeof(ushort) && result.BarRelativeOffset == NotifyApertureBarOffset + result.NotifyRelativeOffset && result.WrittenValue == VirtioNetQueuePlan.TransmitQueueIndex && result.PreviousUsedIndex == previousUsedIndex && result.CurrentUsedIndex == previousUsedIndex + 1 && result.NextUsedIndex == result.CurrentUsedIndex && result.UsedRingSlot == 0 && result.DescriptorHead == readiness.TransmitPublish.TransmitPublish.HeadDescriptorIndex && result.UsedByteCount == VirtioNetTransmitCompletionPlanner.ExpectedTransmitUsedByteCount && result.ReclaimedDescriptorCount == VirtioNetTransmitLifecycle.RequiredDescriptorCount && result.HeaderDescriptorIndex == readiness.HeaderDescriptorIndex && result.FrameDescriptorIndex == readiness.FrameDescriptorIndex && result.StateTableLength == states.Length && result.RequiredStateTableLength == VirtioNetTransmitLifecycle.RequiredDescriptorCount && result.DescriptorsFreed == VirtioNetTransmitLifecycle.RequiredDescriptorCount && ReadUInt16AtBarOffset(notifyBytes, result.BarRelativeOffset) == VirtioNetQueuePlan.TransmitQueueIndex && states[0] == VirtioNetTransmitDescriptorOwnershipState.Free && states[1] == VirtioNetTransmitDescriptorOwnershipState.Free && states[2] == VirtioNetTransmitDescriptorOwnershipState.Free && states[3] == VirtioNetTransmitDescriptorOwnershipState.Free);
    }

    [HostTest("drivers.virtionet.tx.reclaim.windowexec rejects failed readiness", HostLane.Fast, 2)]
    private static bool TestRejectsFailedReadiness()
    {
        TestContext context = CreateContext();
        byte[] commonBytes = CreateCommonWindowBytes(length: 1);
        VirtioNetQueueMmioSetupRegisterWindowExecutionResult setupExecution = ExecuteQueueSetupWindow(context, commonBytes);
        VirtioNetTransmitDescriptorOwnershipState[] states = new VirtioNetTransmitDescriptorOwnershipState[TxQueueSize];
        byte[] txHeader = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers];
        byte[] queueMemory = CreateTxQueueMemory(context);
        byte[] notifyBytes = CreateNotifyWindowBytes();
        VirtioNetTransmitRegisterWindowReadinessResult readiness = ComposeTransmit(context, setupExecution, states, txHeader, queueMemory, notifyBytes, VirtioNetQueueNotificationBarrierReadiness.Ready(62));
        VirtioNetTransmitRegisterWindowReclaimResult result = VirtioNetTransmitRegisterWindowReclaimPlanner.ApplyCompletedDescriptor(readiness, states, previousUsedIndex: 2, queueMemory);
        return HostCheck.Result(!setupExecution.Succeeded && !readiness.Succeeded && !result.Succeeded && result.Status == VirtioNetTransmitRegisterWindowReclaimStatus.ReadinessFailed && result.ReadinessStatus == VirtioNetTransmitRegisterWindowReadinessStatus.QueueSetupWindowExecutionFailed && !result.CompletionPlanAttempted && !result.OwnershipReclaimAttempted && result.PreviousUsedIndex == 2 && result.CurrentUsedIndex == 2 && AllStatesAre(states, VirtioNetTransmitDescriptorOwnershipState.Free) && BytesAreZero(txHeader) && BytesAreZero(queueMemory) && AllBytesEqual(notifyBytes, 0xCC));
    }

    [HostTest("drivers.virtionet.tx.reclaim.windowexec rejects no completion", HostLane.Fast, 3)]
    private static bool TestRejectsNoCompletion()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitDescriptorOwnershipState[] states = new VirtioNetTransmitDescriptorOwnershipState[TxQueueSize];
        byte[] txHeader = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers];
        byte[] queueMemory = CreateTxQueueMemory(context);
        byte[] notifyBytes = CreateNotifyWindowBytes();
        VirtioNetTransmitRegisterWindowReadinessResult readiness = CreateSuccessfulReadiness(context, states, txHeader, queueMemory, notifyBytes, barrierSequence: 63);
        const ushort previousUsedIndex = 0;
        VirtioNetTransmitRegisterWindowReclaimResult result = VirtioNetTransmitRegisterWindowReclaimPlanner.ApplyCompletedDescriptor(readiness, states, previousUsedIndex, queueMemory);
        return HostCheck.Result(readiness.Succeeded && !result.Succeeded && result.Status == VirtioNetTransmitRegisterWindowReclaimStatus.CompletionPlanFailed && result.ReadinessStatus == VirtioNetTransmitRegisterWindowReadinessStatus.Success && result.CompletionPlanStatus == VirtioNetTransmitCompletionPlanStatus.NoCompletion && result.OwnershipStatus == VirtioNetTransmitDescriptorOwnershipStatus.Success && result.CompletionPlanAttempted && !result.OwnershipReclaimAttempted && result.UsesRegisterWindowNotification && result.NotificationBarrierSucceeded && result.NotificationBarrierSequence == 63 && result.PreviousUsedIndex == previousUsedIndex && result.CurrentUsedIndex == previousUsedIndex && result.ReclaimedDescriptorCount == 0 && states[0] == VirtioNetTransmitDescriptorOwnershipState.InFlight && states[1] == VirtioNetTransmitDescriptorOwnershipState.InFlight && states[2] == VirtioNetTransmitDescriptorOwnershipState.Free && states[3] == VirtioNetTransmitDescriptorOwnershipState.Free);
    }

    [HostTest("drivers.virtionet.tx.reclaim.windowexec rejects ownership mismatch", HostLane.Fast, 4)]
    private static bool TestRejectsOwnershipMismatch()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitDescriptorOwnershipState[] states = new VirtioNetTransmitDescriptorOwnershipState[TxQueueSize];
        byte[] txHeader = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers];
        byte[] queueMemory = CreateTxQueueMemory(context);
        byte[] notifyBytes = CreateNotifyWindowBytes();
        VirtioNetTransmitRegisterWindowReadinessResult readiness = CreateSuccessfulReadiness(context, states, txHeader, queueMemory, notifyBytes, barrierSequence: 64);
        states[readiness.HeaderDescriptorIndex] = VirtioNetTransmitDescriptorOwnershipState.Free;
        const ushort previousUsedIndex = 1;
        WriteUsedCompletion(readiness.TransmitPublish.TransmitPublish.Plan.TxQueue, queueMemory, previousUsedIndex, unchecked((ushort)(previousUsedIndex + 1)), readiness.TransmitPublish.TransmitPublish.HeadDescriptorIndex, usedByteCount: 0);
        VirtioNetTransmitRegisterWindowReclaimResult result = VirtioNetTransmitRegisterWindowReclaimPlanner.ApplyCompletedDescriptor(readiness, states, previousUsedIndex, queueMemory);
        return HostCheck.Result(readiness.Succeeded && !result.Succeeded && result.Status == VirtioNetTransmitRegisterWindowReclaimStatus.OwnershipReclaimFailed && result.CompletionPlanStatus == VirtioNetTransmitCompletionPlanStatus.Success && result.OwnershipStatus == VirtioNetTransmitDescriptorOwnershipStatus.DescriptorNotInFlight && result.CompletionPlanAttempted && result.OwnershipReclaimAttempted && result.ReclaimedDescriptorCount == VirtioNetTransmitLifecycle.RequiredDescriptorCount && result.DescriptorsFreed == 0 && states[readiness.HeaderDescriptorIndex] == VirtioNetTransmitDescriptorOwnershipState.Free && states[readiness.FrameDescriptorIndex] == VirtioNetTransmitDescriptorOwnershipState.InFlight);
    }

    [HostTest("drivers.virtionet.tx.reclaim.windowexec preserves no-live-mmio boundary", HostLane.Fast, 5)]
    private static bool TestPreservesNoLiveMmioBoundary()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitDescriptorOwnershipState[] states = new VirtioNetTransmitDescriptorOwnershipState[TxQueueSize];
        byte[] txHeader = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers];
        byte[] queueMemory = CreateTxQueueMemory(context);
        byte[] notifyBytes = CreateNotifyWindowBytes();
        VirtioNetTransmitRegisterWindowReadinessResult readiness = CreateSuccessfulReadiness(context, states, txHeader, queueMemory, notifyBytes, barrierSequence: 65);
        const ushort previousUsedIndex = 3;
        WriteUsedCompletion(readiness.TransmitPublish.TransmitPublish.Plan.TxQueue, queueMemory, previousUsedIndex, unchecked((ushort)(previousUsedIndex + 1)), readiness.TransmitPublish.TransmitPublish.HeadDescriptorIndex, usedByteCount: 0);
        VirtioNetTransmitRegisterWindowReclaimResult result = VirtioNetTransmitRegisterWindowReclaimPlanner.ApplyCompletedDescriptor(readiness, states, previousUsedIndex, queueMemory);
        return HostCheck.Result(result.Succeeded && VirtioNetTransmitRegisterWindowReclaimPlanner.RequiresSuccessfulRegisterWindowReadiness && VirtioNetTransmitRegisterWindowReclaimPlanner.ConsumesRegisterWindowNotificationProvenance && VirtioNetTransmitRegisterWindowReclaimPlanner.RequiresCallerOwnedQueueMemory && VirtioNetTransmitRegisterWindowReclaimPlanner.RequiresCallerOwnedDescriptorStateTable && VirtioNetTransmitRegisterWindowReclaimPlanner.ReadsQueueMemory && VirtioNetTransmitRegisterWindowReclaimPlanner.MutatesCallerOwnedStateTable && !VirtioNetTransmitRegisterWindowReclaimPlanner.PerformsLiveMmio && !VirtioNetTransmitRegisterWindowReclaimPlanner.MapsBars && !VirtioNetTransmitRegisterWindowReclaimPlanner.ContainsVolatileAccess && !VirtioNetTransmitRegisterWindowReclaimPlanner.AccessesPhysicalAddresses && !VirtioNetTransmitRegisterWindowReclaimPlanner.ExecutesMemoryBarriers && !VirtioNetTransmitRegisterWindowReclaimPlanner.ConfiguresLiveQueues && !VirtioNetTransmitRegisterWindowReclaimPlanner.NotifiesLiveDevice && !VirtioNetTransmitRegisterWindowReclaimPlanner.PollsDevice && !VirtioNetTransmitRegisterWindowReclaimPlanner.HandlesInterrupts && !VirtioNetTransmitRegisterWindowReclaimPlanner.PerformsDmaMapping && !VirtioNetTransmitRegisterWindowReclaimPlanner.AllocatesDmaMemory && !VirtioNetTransmitRegisterWindowReclaimPlanner.PerformsPacketIo && !VirtioNetTransmitRegisterWindowReclaimPlanner.PerformsPacketTransmit && !VirtioNetTransmitRegisterWindowReclaimPlanner.WaitsForUsedRingCompletion && !VirtioNetTransmitRegisterWindowReclaimPlanner.WritesQueueMemory && !VirtioNetTransmitRegisterWindowReclaimPlanner.WritesTransmitHeader && !VirtioNetTransmitRegisterWindowReclaimPlanner.InvokesRegisterWindowAccessExecutor && !VirtioNetTransmitRegisterWindowReclaimPlanner.WritesCallerOwnedNotifyWindowBuffer && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioNetTransmitRegisterWindowReclaimResult>());
    }

    private static VirtioNetTransmitRegisterWindowReadinessResult CreateSuccessfulReadiness(TestContext context, Span<VirtioNetTransmitDescriptorOwnershipState> states, Span<byte> txHeader, Span<byte> queueMemory, Span<byte> notifyBytes, uint barrierSequence)
    {
        byte[] commonBytes = CreateCommonWindowBytes();
        VirtioNetQueueMmioSetupRegisterWindowExecutionResult setupExecution = ExecuteQueueSetupWindow(context, commonBytes);
        return ComposeTransmit(context, setupExecution, states, txHeader, queueMemory, notifyBytes, VirtioNetQueueNotificationBarrierReadiness.Ready(barrierSequence));
    }

    private static VirtioNetTransmitRegisterWindowReadinessResult ComposeTransmit(TestContext context, VirtioNetQueueMmioSetupRegisterWindowExecutionResult setupExecution, Span<VirtioNetTransmitDescriptorOwnershipState> states, Span<byte> txHeader, Span<byte> queueMemory, Span<byte> notifyBytes, VirtioNetQueueNotificationBarrierReadiness barrier)
    {
        return VirtioNetTransmitRegisterWindowReadinessPlanner.ComposeTransmit(context.QueueSetup, setupExecution, context.QueuePlan, states, availableRingIndex: 11, TxHeaderPhysicalAddress, TxFramePhysicalAddress, CreateEthernetFrame(), txHeader, queueMemory, barrier, context.Apertures.NotifyConfiguration, context.RegisterWindows.Bindings.NotifyConfiguration, notifyBytes, negotiatedFeatures: 0);
    }

    private static VirtioNetQueueMmioSetupRegisterWindowExecutionResult ExecuteQueueSetupWindow(TestContext context, Span<byte> commonBytes)
    {
        return VirtioNetQueueMmioSetupRegisterWindowExecutor.Execute(context.QueueSetup, context.QueueSetupCommands, context.RegisterWindows.Bindings.CommonConfiguration, commonBytes);
    }

    private static TestContext CreateContext()
    {
        VirtioPciBindingPlanResult bindingPlan = CreateBindingPlan();
        VirtioPciBarRegisterWindowBindingResult registerWindows = VirtioPciBarRegisterWindowPlanner.BindRequiredApertures(bindingPlan, CreateRegisterWindows());
        VirtioDeviceDescriptor descriptor = CreateNetworkDescriptor(bindingPlan);
        VirtioNetQueuePlanResult queuePlan = VirtioNetQueuePlan.Create(VirtioNetQueuePlan.MinimumQueueCount, RxQueueSize, TxQueueSize, RxBufferCount, FrameBufferBytes);
        if (!queuePlan.Succeeded)
        {
            throw new InvalidOperationException("Failed to create deterministic queue plan.");
        }

        VirtioNetDriverQueueBringUpPlanResult bringUp = VirtioNetDriverQueueBringUpPlan.Create(descriptor, RxQueueSize, TxQueueSize, RxBufferCount, FrameBufferBytes, queuePlan.Plan.RxQueue.RequiredByteLength, queuePlan.Plan.TxQueue.RequiredByteLength, queuePlan.Plan.RxBuffers.RequiredBufferBytes, queuePlan.Plan.TxFrame.RequiredBufferBytes);
        if (!bringUp.Succeeded)
        {
            throw new InvalidOperationException("Failed to create deterministic bring-up plan.");
        }

        VirtioNetQueueMmioSetupCommand[] commands = new VirtioNetQueueMmioSetupCommand[VirtioNetQueueMmioSetupPlanner.RequiredCommandCount];
        VirtioNetQueueMmioSetupPlanResult queueSetup = VirtioNetQueueMmioSetupPlanner.Create(bringUp, CreateRxAddresses(), CreateTxAddresses(), CreateRxMetadata(), CreateTxMetadata(), commands);
        if (!queueSetup.Succeeded || !registerWindows.Succeeded)
        {
            throw new InvalidOperationException("Failed to create deterministic register-window setup.");
        }

        return new TestContext(bindingPlan.Binding.Apertures, registerWindows, queueSetup, commands, queuePlan);
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
        return new VirtioNetQueueMmioSetupQueueAddresses(descriptorTablePhysicalAddress: 0x100000, driverRingPhysicalAddress: 0x101000, deviceRingPhysicalAddress: 0x102000);
    }

    private static VirtioNetQueueMmioSetupQueueAddresses CreateTxAddresses()
    {
        return new VirtioNetQueueMmioSetupQueueAddresses(descriptorTablePhysicalAddress: 0x110000, driverRingPhysicalAddress: 0x111000, deviceRingPhysicalAddress: 0x112000);
    }

    private static VirtioNetQueueMmioSetupQueueDeviceMetadata CreateRxMetadata()
    {
        return new VirtioNetQueueMmioSetupQueueDeviceMetadata(RxDeviceMaximumQueueSize, hasNotifyOffset: true, RxNotifyOffset);
    }

    private static VirtioNetQueueMmioSetupQueueDeviceMetadata CreateTxMetadata()
    {
        return new VirtioNetQueueMmioSetupQueueDeviceMetadata(TxDeviceMaximumQueueSize, hasNotifyOffset: true, TxNotifyOffset);
    }

    private static VirtioDeviceDescriptor CreateNetworkDescriptor(VirtioPciBindingPlanResult bindingPlan)
    {
        VirtioDeviceDescriptorSource[] sources = [new VirtioDeviceDescriptorSource(sourceIndex: 46, bindingPlan, VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask, VirtioDeviceInitializationProfile.NetworkRequiredQueueCount, featuresOkAccepted: true)];
        VirtioDeviceConfigurationSource[] configurations = [new VirtioDeviceConfigurationSource(sourceIndex: 46, CreateNetworkConfiguration(new MacAddress(0x02, 0x33, 0x44, 0x55, 0x66, 0xD2), VirtioNetDeviceConfig.StatusLinkUp))];
        VirtioDeviceDescriptor[] destination = new VirtioDeviceDescriptor[1];
        VirtioDeviceDescriptorCatalogResult result = VirtioDeviceDescriptorCatalog.Build(sources, configurations, destination);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Failed to create deterministic virtio-net descriptor.");
        }

        return destination[0];
    }

    private static VirtioPciBindingPlanResult CreateBindingPlan()
    {
        ushort deviceId = (ushort)(VirtioPciDeviceDiscovery.ModernDeviceIdStart + VirtioPciDeviceDiscovery.NetworkDeviceId);
        return VirtioPciBindingPlan.Create(CreateScannedDevice(deviceId), CreatePciConfiguration(deviceId), CreateBarSizes());
    }

    private static PciEcamScannedDevice CreateScannedDevice(ushort deviceId)
    {
        return new PciEcamScannedDevice(segmentIndex: 2, new PciEcamSegment(basePhysicalAddress: 0xE0000000UL, segmentGroup: 1, startBus: 0, endBus: 0x7F), new PciScannedDevice(bus: 3, device: 5, function: 0, vendorId: VirtioPciDeviceDiscovery.VirtioVendorId, deviceId: deviceId, revisionId: 0, classCode: 0, subclass: 0, programmingInterface: 0, headerType: PciConfigurationHeader.Type0HeaderType, isMultifunction: false, hasCapabilitiesList: true, capabilityPointer: CommonOffset));
    }

    private static byte[] CreatePciConfiguration(ushort deviceId)
    {
        byte[] config = new byte[256];
        EndianPrimitives.WriteUInt16LittleEndian(config.AsSpan(0x00, sizeof(ushort)), VirtioPciDeviceDiscovery.VirtioVendorId);
        EndianPrimitives.WriteUInt16LittleEndian(config.AsSpan(0x02, sizeof(ushort)), deviceId);
        EndianPrimitives.WriteUInt16LittleEndian(config.AsSpan(0x06, sizeof(ushort)), PciConfigurationHeader.StatusCapabilitiesListBit);
        config[0x0E] = PciConfigurationHeader.Type0HeaderType;
        WriteUInt32(config, 0x10, 0x80000000);
        WriteUInt32(config, 0x14, 0x80001000);
        WriteUInt32(config, 0x18, 0x80002000);
        WriteUInt32(config, 0x1C, 0x80003000);
        config[0x34] = CommonOffset;
        WriteVirtioCapability(config, CommonOffset, NotifyOffset, VirtioPciCapabilityType.CommonConfiguration, bar: 0, offsetValue: 0x20, length: VirtioPciDeviceDiscovery.CommonConfigurationMinimumLength, notifyOffsetMultiplier: 0, capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);
        WriteVirtioCapability(config, NotifyOffset, IsrOffset, VirtioPciCapabilityType.NotifyConfiguration, bar: 1, offsetValue: NotifyApertureBarOffset, length: 0x100, notifyOffsetMultiplier: NotifyOffsetMultiplier, capabilityLength: VirtioPciCapabilityListParser.ModernVirtioNotifyCapabilityLength);
        WriteVirtioCapability(config, IsrOffset, DeviceOffset, VirtioPciCapabilityType.IsrStatus, bar: 2, offsetValue: 0, length: 1, notifyOffsetMultiplier: 0, capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);
        WriteVirtioCapability(config, DeviceOffset, nextOffset: 0, VirtioPciCapabilityType.DeviceConfiguration, bar: 3, offsetValue: 0, length: 0x10, notifyOffsetMultiplier: 0, capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);
        return config;
    }

    private static ulong[] CreateBarSizes()
    {
        return [0x1000, 0x1000, 0x1000, 0x1000, 0, 0];
    }

    private static VirtioPciMappedBarWindow[] CreateRegisterWindows()
    {
        return [CreateRegisterWindow(0, 0x80000000UL, 0xFFFF800000000000UL), CreateRegisterWindow(1, NotifyBarPhysicalBase, NotifyBarVirtualBase), CreateRegisterWindow(2, 0x80002000UL, 0xFFFF800000002000UL), CreateRegisterWindow(3, 0x80003000UL, 0xFFFF800000003000UL), default, default];
    }

    private static VirtioPciMappedBarWindow CreateRegisterWindow(byte barIndex, ulong physicalBaseAddress, ulong virtualBaseAddress)
    {
        return new VirtioPciMappedBarWindow(barIndex, physicalBaseAddress, virtualBaseAddress, length: 0x1000, isMapped: true, isMmio: true, isDeviceMemory: true, isUncached: true);
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

    private static void WriteVirtioCapability(Span<byte> config, byte offset, byte nextOffset, VirtioPciCapabilityType type, byte bar, uint offsetValue, uint length, uint notifyOffsetMultiplier, byte capabilityLength)
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

    private static void WriteUsedCompletion(VirtqueueSplitRingPlan queue, byte[] queueMemory, ushort previousUsedIndex, ushort currentUsedIndex, uint descriptorHead, uint usedByteCount)
    {
        EndianPrimitives.WriteUInt16LittleEndian(queueMemory.AsSpan((int)queue.UsedRing.Offset + 2, sizeof(ushort)), currentUsedIndex);
        ushort slot = (ushort)(previousUsedIndex & (queue.QueueSize - 1));
        int elementOffset = (int)queue.UsedRing.Offset + (int)VirtqueueSplitRingPlan.UsedRingHeaderLength + (slot * (int)VirtqueueSplitRingPlan.UsedRingElementLength);
        EndianPrimitives.WriteUInt32LittleEndian(queueMemory.AsSpan(elementOffset, sizeof(uint)), descriptorHead);
        EndianPrimitives.WriteUInt32LittleEndian(queueMemory.AsSpan(elementOffset + sizeof(uint), sizeof(uint)), usedByteCount);
    }

    private static ushort ReadUInt16AtBarOffset(ReadOnlySpan<byte> source, ulong barRelativeOffset)
    {
        return EndianPrimitives.ReadUInt16LittleEndian(source.Slice(checked((int)barRelativeOffset), sizeof(ushort)));
    }

    private static bool AllStatesAre(ReadOnlySpan<VirtioNetTransmitDescriptorOwnershipState> states, VirtioNetTransmitDescriptorOwnershipState expected)
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
}
