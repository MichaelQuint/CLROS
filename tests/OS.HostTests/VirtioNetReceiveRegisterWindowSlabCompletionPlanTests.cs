using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;

internal static partial class VirtioNetReceiveRegisterWindowSlabCompletionPlanTests
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
    [HostTest("drivers.virtionet.rx.completion.slab.windowexec selects completed buffer", HostLane.Fast, 0)]
    private static bool TestSelectsCompletedBuffer()
    {
        TestContext context = CreateContext();
        byte[] queueMemory = CreateRxQueueMemory(context);
        byte[] notifyBytes = CreateNotifyWindowBytes();
        VirtioNetReceiveReadinessResult readiness = CreateSuccessfulReadiness(context, queueMemory, notifyBytes, barrierSequence: 81);
        byte[] receiveBuffer = CreateReceiveBuffer(EthernetHeader.EtherTypeIpv4, payloadBytes: 20);
        byte[] slab = CreateReceiveSlab();
        Copy(receiveBuffer, slab.AsSpan((int)FrameBufferBytes));
        WriteUsedCompletion(readiness.ReceivePublish.QueuePlan, queueMemory, readiness.PreviousUsedIndex, readiness.ExpectedNextUsedIndex, descriptorHead: 2, usedByteCount: (uint)receiveBuffer.Length);
        VirtioNetReceiveRegisterWindowSlabCompletionResult result = VirtioNetReceiveRegisterWindowSlabCompletionPlanner.CompleteReceiveFromSlab(readiness, queueMemory, slab);
        return HostCheck.Result(readiness.Succeeded && result.Succeeded && result.IsInitialized && result.Status == VirtioNetReceiveRegisterWindowSlabCompletionStatus.Success && result.ReadinessStatus == VirtioNetReceiveReadinessStatus.Success && result.SlabSelectionStatus == VirtioNetReceiveBufferSlabSelectionStatus.Success && result.CompletionPlanStatus == VirtioNetReceiveRegisterWindowCompletionStatus.Success && result.CompletionStatus == VirtioNetReceiveLifecycleCompletionStatus.Success && result.ClassifierStatus == VirtioNetReceiveFrameClassifierStatus.Success && result.PacketStatus == PacketParseStatus.Success && result.SlabSelectionAttempted && result.CompletionAttempted && result.DescriptorHead == 2 && result.BufferIndex == 1 && result.ReceiveBufferSlabLength == slab.Length && result.ReceiveBufferStrideBytes == FrameBufferBytes && result.SelectedBufferOffset == FrameBufferBytes && result.SelectedBufferLength == FrameBufferBytes && result.PreviousUsedIndex == 4 && result.CurrentUsedIndex == 5 && result.NextUsedIndex == 5 && result.UsedRingSlot == 4 && result.UsedRingElementOffset == GetUsedEntryOffset(readiness.ReceivePublish.QueuePlan, 4) && result.UsedByteCount == receiveBuffer.Length && result.PayloadKind == EthernetPayloadKind.Ipv4 && result.PayloadOffset == VirtioNetHeader.HeaderLengthWithoutMergeableBuffers + EthernetHeader.HeaderLength && result.PayloadLength == 20 && result.Completion.BufferIndex == 1 && result.Completion.PayloadKind == EthernetPayloadKind.Ipv4);
    }

    [HostTest("drivers.virtionet.rx.completion.slab.windowexec rejects failed readiness", HostLane.Fast, 1)]
    private static bool TestRejectsFailedReadiness()
    {
        TestContext context = CreateContext();
        byte[] commonBytes = CreateCommonWindowBytes(length: 1);
        VirtioNetQueueMmioSetupRegisterWindowExecutionResult setupExecution = ExecuteQueueSetupWindow(context, commonBytes);
        byte[] queueMemory = CreateRxQueueMemory(context);
        byte[] notifyBytes = CreateNotifyWindowBytes();
        VirtioNetReceiveReadinessResult readiness = ComposeReceive(context, setupExecution, queueMemory, notifyBytes, VirtioNetQueueNotificationBarrierReadiness.Ready(82));
        VirtioNetReceiveRegisterWindowSlabCompletionResult result = VirtioNetReceiveRegisterWindowSlabCompletionPlanner.CompleteReceiveFromSlab(readiness, queueMemory, CreateReceiveSlab());
        return HostCheck.Result(!setupExecution.Succeeded && !readiness.Succeeded && !result.Succeeded && result.Status == VirtioNetReceiveRegisterWindowSlabCompletionStatus.ReadinessFailed && result.ReadinessStatus == VirtioNetReceiveReadinessStatus.QueueSetupWindowExecutionFailed && result.SlabSelectionStatus == VirtioNetReceiveBufferSlabSelectionStatus.ReadinessFailed && result.CompletionPlanStatus == VirtioNetReceiveRegisterWindowCompletionStatus.ReadinessFailed && !result.SlabSelectionAttempted && !result.CompletionAttempted && BytesAreZero(queueMemory) && AllBytesEqual(notifyBytes, 0xCC));
    }

    [HostTest("drivers.virtionet.rx.completion.slab.windowexec rejects no completion", HostLane.Fast, 2)]
    private static bool TestRejectsNoCompletion()
    {
        TestContext context = CreateContext();
        byte[] queueMemory = CreateRxQueueMemory(context);
        byte[] notifyBytes = CreateNotifyWindowBytes();
        VirtioNetReceiveReadinessResult readiness = CreateSuccessfulReadiness(context, queueMemory, notifyBytes, barrierSequence: 83);
        WriteUsedIndex(readiness.ReceivePublish.QueuePlan, queueMemory, readiness.PreviousUsedIndex);
        VirtioNetReceiveRegisterWindowSlabCompletionResult result = VirtioNetReceiveRegisterWindowSlabCompletionPlanner.CompleteReceiveFromSlab(readiness, queueMemory, CreateReceiveSlab());
        return HostCheck.Result(readiness.Succeeded && !result.Succeeded && result.Status == VirtioNetReceiveRegisterWindowSlabCompletionStatus.NoCompletion && result.SlabSelectionStatus == VirtioNetReceiveBufferSlabSelectionStatus.NoCompletion && result.CompletionStatus == VirtioNetReceiveLifecycleCompletionStatus.NoCompletion && result.SlabSelectionAttempted && !result.CompletionAttempted && result.PreviousUsedIndex == readiness.PreviousUsedIndex && result.CurrentUsedIndex == readiness.PreviousUsedIndex && result.UsedRingSlot == 4 && result.UsedByteCount == 0);
    }

    [HostTest("drivers.virtionet.rx.completion.slab.windowexec rejects short slab", HostLane.Fast, 3)]
    private static bool TestRejectsShortSlab()
    {
        TestContext context = CreateContext();
        byte[] queueMemory = CreateRxQueueMemory(context);
        byte[] notifyBytes = CreateNotifyWindowBytes();
        VirtioNetReceiveReadinessResult readiness = CreateSuccessfulReadiness(context, queueMemory, notifyBytes, barrierSequence: 84);
        byte[] shortSlab = new byte[(int)FrameBufferBytes + 16];
        WriteUsedCompletion(readiness.ReceivePublish.QueuePlan, queueMemory, readiness.PreviousUsedIndex, readiness.ExpectedNextUsedIndex, descriptorHead: 2, usedByteCount: 16);
        VirtioNetReceiveRegisterWindowSlabCompletionResult result = VirtioNetReceiveRegisterWindowSlabCompletionPlanner.CompleteReceiveFromSlab(readiness, queueMemory, shortSlab);
        return HostCheck.Result(readiness.Succeeded && !result.Succeeded && result.Status == VirtioNetReceiveRegisterWindowSlabCompletionStatus.ReceiveSlabTooSmall && result.SlabSelectionStatus == VirtioNetReceiveBufferSlabSelectionStatus.ReceiveBufferSlabTooSmall && result.CompletionStatus == VirtioNetReceiveLifecycleCompletionStatus.ReceiveBufferTooSmall && result.SlabSelectionAttempted && !result.CompletionAttempted && result.DescriptorHead == 2 && result.BufferIndex == 1 && result.SelectedBufferOffset == FrameBufferBytes && result.SelectedBufferLength == FrameBufferBytes && result.UsedByteCount == 16);
    }

    [HostTest("drivers.virtionet.rx.completion.slab.windowexec propagates classifier failure", HostLane.Fast, 4)]
    private static bool TestPropagatesClassifierFailure()
    {
        TestContext context = CreateContext();
        byte[] queueMemory = CreateRxQueueMemory(context);
        byte[] notifyBytes = CreateNotifyWindowBytes();
        VirtioNetReceiveReadinessResult readiness = CreateSuccessfulReadiness(context, queueMemory, notifyBytes, barrierSequence: 85);
        byte[] slab = CreateReceiveSlab();
        uint shortUsedByteCount = (uint)(VirtioNetHeader.HeaderLengthWithoutMergeableBuffers - 1);
        WriteUsedCompletion(readiness.ReceivePublish.QueuePlan, queueMemory, readiness.PreviousUsedIndex, readiness.ExpectedNextUsedIndex, readiness.FirstDescriptorIndex, shortUsedByteCount);
        VirtioNetReceiveRegisterWindowSlabCompletionResult result = VirtioNetReceiveRegisterWindowSlabCompletionPlanner.CompleteReceiveFromSlab(readiness, queueMemory, slab);
        return HostCheck.Result(readiness.Succeeded && !result.Succeeded && result.Status == VirtioNetReceiveRegisterWindowSlabCompletionStatus.ClassificationFailed && result.SlabSelectionStatus == VirtioNetReceiveBufferSlabSelectionStatus.Success && result.CompletionPlanStatus == VirtioNetReceiveRegisterWindowCompletionStatus.ClassificationFailed && result.CompletionStatus == VirtioNetReceiveLifecycleCompletionStatus.FrameClassificationFailed && result.ClassifierStatus == VirtioNetReceiveFrameClassifierStatus.BufferTooSmall && result.PacketStatus == PacketParseStatus.BufferTooSmall && result.SlabSelectionAttempted && result.CompletionAttempted && result.DescriptorHead == readiness.FirstDescriptorIndex && result.BufferIndex == 0 && result.SelectedBufferOffset == 0 && result.SelectedBufferLength == FrameBufferBytes && result.UsedByteCount == shortUsedByteCount);
    }

    [HostTest("drivers.virtionet.rx.completion.slab.windowexec preserves no-live-mmio boundary", HostLane.Fast, 5)]
    private static bool TestPreservesNoLiveMmioBoundary()
    {
        TestContext context = CreateContext();
        byte[] queueMemory = CreateRxQueueMemory(context);
        byte[] notifyBytes = CreateNotifyWindowBytes();
        VirtioNetReceiveReadinessResult readiness = CreateSuccessfulReadiness(context, queueMemory, notifyBytes, barrierSequence: 86);
        byte[] receiveBuffer = CreateReceiveBuffer(EthernetHeader.EtherTypeArp, payloadBytes: 28);
        byte[] slab = CreateReceiveSlab();
        Copy(receiveBuffer, slab.AsSpan(0));
        WriteUsedCompletion(readiness.ReceivePublish.QueuePlan, queueMemory, readiness.PreviousUsedIndex, readiness.ExpectedNextUsedIndex, readiness.FirstDescriptorIndex, (uint)receiveBuffer.Length);
        VirtioNetReceiveRegisterWindowSlabCompletionResult result = VirtioNetReceiveRegisterWindowSlabCompletionPlanner.CompleteReceiveFromSlab(readiness, queueMemory, slab);
        return HostCheck.Result(result.Succeeded && VirtioNetReceiveRegisterWindowSlabCompletionPlanner.RequiresSuccessfulRegisterWindowReadiness && VirtioNetReceiveRegisterWindowSlabCompletionPlanner.SelectsCallerOwnedReceiveBufferFromSlab && VirtioNetReceiveRegisterWindowSlabCompletionPlanner.ConsumesRegisterWindowNotificationProvenance && VirtioNetReceiveRegisterWindowSlabCompletionPlanner.RequiresCallerOwnedQueueMemory && VirtioNetReceiveRegisterWindowSlabCompletionPlanner.RequiresCallerOwnedReceiveBufferSlab && VirtioNetReceiveRegisterWindowSlabCompletionPlanner.ReadsQueueMemory && VirtioNetReceiveRegisterWindowSlabCompletionPlanner.ReadsCallerOwnedReceiveBuffer && VirtioNetReceiveRegisterWindowSlabCompletionPlanner.ClassifiesCallerOwnedFrame && !VirtioNetReceiveRegisterWindowSlabCompletionPlanner.PerformsLiveMmio && !VirtioNetReceiveRegisterWindowSlabCompletionPlanner.MapsBars && !VirtioNetReceiveRegisterWindowSlabCompletionPlanner.ContainsVolatileAccess && !VirtioNetReceiveRegisterWindowSlabCompletionPlanner.AccessesPhysicalAddresses && !VirtioNetReceiveRegisterWindowSlabCompletionPlanner.ExecutesMemoryBarriers && !VirtioNetReceiveRegisterWindowSlabCompletionPlanner.ConfiguresLiveQueues && !VirtioNetReceiveRegisterWindowSlabCompletionPlanner.NotifiesLiveDevice && !VirtioNetReceiveRegisterWindowSlabCompletionPlanner.PollsDevice && !VirtioNetReceiveRegisterWindowSlabCompletionPlanner.HandlesInterrupts && !VirtioNetReceiveRegisterWindowSlabCompletionPlanner.PerformsDmaMapping && !VirtioNetReceiveRegisterWindowSlabCompletionPlanner.AllocatesDmaMemory && !VirtioNetReceiveRegisterWindowSlabCompletionPlanner.PerformsPacketIo && !VirtioNetReceiveRegisterWindowSlabCompletionPlanner.PerformsPacketReceive && !VirtioNetReceiveRegisterWindowSlabCompletionPlanner.CopiesPayload && !VirtioNetReceiveRegisterWindowSlabCompletionPlanner.WritesQueueMemory && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioNetReceiveRegisterWindowSlabCompletionResult>());
    }

    [HostTest("drivers.virtionet.rx.completion.slab.windowexec default result is not success", HostLane.Fast, 6)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        VirtioNetReceiveRegisterWindowSlabCompletionResult result = default;
        return HostCheck.Result(!result.IsInitialized && !result.Succeeded && result.Status == VirtioNetReceiveRegisterWindowSlabCompletionStatus.Success && result.ReadinessStatus == VirtioNetReceiveReadinessStatus.Success && result.SlabSelectionStatus == VirtioNetReceiveBufferSlabSelectionStatus.Success && result.CompletionPlanStatus == VirtioNetReceiveRegisterWindowCompletionStatus.Success && !result.SlabSelectionAttempted && !result.CompletionAttempted && result.ReceiveBufferSlabLength == 0 && result.SelectedBufferLength == 0 && result.PayloadLength == 0 && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioNetReceiveRegisterWindowSlabCompletionResult>());
    }

    private static VirtioNetReceiveReadinessResult CreateSuccessfulReadiness(TestContext context, Span<byte> queueMemory, Span<byte> notifyBytes, uint barrierSequence)
    {
        byte[] commonBytes = CreateCommonWindowBytes();
        VirtioNetQueueMmioSetupRegisterWindowExecutionResult setupExecution = ExecuteQueueSetupWindow(context, commonBytes);
        return ComposeReceive(context, setupExecution, queueMemory, notifyBytes, VirtioNetQueueNotificationBarrierReadiness.Ready(barrierSequence));
    }

    private static VirtioNetReceiveReadinessResult ComposeReceive(TestContext context, VirtioNetQueueMmioSetupRegisterWindowExecutionResult setupExecution, Span<byte> queueMemory, Span<byte> notifyBytes, VirtioNetQueueNotificationBarrierReadiness barrier)
    {
        return VirtioNetReceiveReadinessPlanner.ComposeReceive(context.QueueSetup, setupExecution, firstDescriptorIndex: 1, availableDescriptorCount: RxBufferCount, previousAvailableRingIndex: 6, previousUsedIndex: 4, RxBufferPhysicalAddress, receiveBufferStrideBytes: FrameBufferBytes, receiveBufferCount: 2, frameBufferBytes: FrameBufferBytes, queueMemory, barrier, context.Apertures.NotifyConfiguration, context.RegisterWindows.Bindings.NotifyConfiguration, notifyBytes, negotiatedFeatures: 0);
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
        VirtioDeviceDescriptorSource[] sources = [new VirtioDeviceDescriptorSource(sourceIndex: 48, bindingPlan, VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask, VirtioDeviceInitializationProfile.NetworkRequiredQueueCount, featuresOkAccepted: true)];
        VirtioDeviceConfigurationSource[] configurations = [new VirtioDeviceConfigurationSource(sourceIndex: 48, CreateNetworkConfiguration(new MacAddress(0x02, 0x33, 0x44, 0x55, 0x66, 0xD4), VirtioNetDeviceConfig.StatusLinkUp))];
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
        return[0x1000, 0x1000, 0x1000, 0x1000, 0, 0];
    }

    private static VirtioPciMappedBarWindow[] CreateRegisterWindows()
    {
        return[CreateRegisterWindow(0, 0x80000000UL, 0xFFFF800000000000UL), CreateRegisterWindow(1, NotifyBarPhysicalBase, NotifyBarVirtualBase), CreateRegisterWindow(2, 0x80002000UL, 0xFFFF800000002000UL), CreateRegisterWindow(3, 0x80003000UL, 0xFFFF800000003000UL), default, default ];
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

    private static byte[] CreateRxQueueMemory(TestContext context)
    {
        return new byte[(int)context.QueuePlan.Plan.RxQueue.EndOffset];
    }

    private static byte[] CreateReceiveSlab()
    {
        return new byte[checked((int)FrameBufferBytes * 2)];
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

    private static byte[] CreateReceiveBuffer(ushort etherType, int payloadBytes)
    {
        byte[] buffer = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers + EthernetHeader.HeaderLength + payloadBytes];
        EthernetHeader ethernetHeader = new EthernetHeader(new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x22), new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x11), etherType);
        PacketParseStatus status = EthernetHeader.TryWrite(buffer.AsSpan(VirtioNetHeader.HeaderLengthWithoutMergeableBuffers), ethernetHeader);
        if (status != PacketParseStatus.Success)
        {
            throw new InvalidOperationException("Failed to build deterministic Ethernet frame.");
        }

        int payloadOffset = VirtioNetHeader.HeaderLengthWithoutMergeableBuffers + EthernetHeader.HeaderLength;
        for (int index = 0; index < payloadBytes; index++)
        {
            buffer[payloadOffset + index] = (byte)(0x80 + index);
        }

        return buffer;
    }

    private static void WriteUsedIndex(VirtqueueSplitRingPlan queue, byte[] queueMemory, ushort currentUsedIndex)
    {
        EndianPrimitives.WriteUInt16LittleEndian(queueMemory.AsSpan((int)queue.UsedRing.Offset + 2, sizeof(ushort)), currentUsedIndex);
    }

    private static void WriteUsedCompletion(VirtqueueSplitRingPlan queue, byte[] queueMemory, ushort previousUsedIndex, ushort currentUsedIndex, uint descriptorHead, uint usedByteCount)
    {
        WriteUsedIndex(queue, queueMemory, currentUsedIndex);
        ushort slot = (ushort)(previousUsedIndex & (queue.QueueSize - 1));
        int elementOffset = GetUsedEntryOffset(queue, slot);
        EndianPrimitives.WriteUInt32LittleEndian(queueMemory.AsSpan(elementOffset, sizeof(uint)), descriptorHead);
        EndianPrimitives.WriteUInt32LittleEndian(queueMemory.AsSpan(elementOffset + sizeof(uint), sizeof(uint)), usedByteCount);
    }

    private static int GetUsedEntryOffset(VirtqueueSplitRingPlan queue, int slot)
    {
        return (int)queue.UsedRing.Offset + (int)VirtqueueSplitRingPlan.UsedRingHeaderLength + (slot * (int)VirtqueueSplitRingPlan.UsedRingElementLength);
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

    private static void Copy(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        for (int index = 0; index < source.Length; index++)
        {
            destination[index] = source[index];
        }
    }

    private static void Fill(Span<byte> bytes, byte value)
    {
        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = value;
        }
    }
}
