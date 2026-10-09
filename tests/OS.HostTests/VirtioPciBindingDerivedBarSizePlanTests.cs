using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Std.Binary;

internal static class VirtioPciBindingDerivedBarSizePlanTests
{
    private const byte CommonOffset = 0x40;
    private const byte NotifyOffset = 0x60;
    private const byte IsrOffset = 0x80;
    private const byte DeviceOffset = 0xA0;

    [HostTest("drivers.virtio.pci.binding.derive creates network binding from fake bar probe", HostLane.Fast, 0)]
    private static bool TestCreatesNetworkBindingFromFakeBarProbe()
    {
        ushort deviceId = VirtioPciDeviceDiscovery.ModernDeviceIdStart +
            VirtioPciDeviceDiscovery.NetworkDeviceId;
        byte[] config = CreateConfig(
            deviceId,
            VirtioPciDeviceDiscovery.NetworkDeviceConfigurationMinimumLength);
        FakePciConfigDwordAccessor accessor = CreateSuccessfulAccessor();

        VirtioPciBindingDerivedBarSizePlanResult result =
            Plan(CreateCandidate(deviceId), config, accessor, out ulong[] sizes);

        return HostCheck.Result(result.Succeeded
            && result.IsInitialized
            && result.Status == VirtioPciBindingDerivedBarSizePlanStatus.Success
            && result.HeaderStatus == PciConfigurationHeaderStatus.Success
            && result.BarDecode.Status == PciBarDecodeStatus.Success
            && result.DerivedSizeStatus == PciBarSizingProbeDerivedSizeStatus.Success
            && result.ExecutionStatus == PciBarSizingProbeExecutionStatus.Success
            && result.SizingStatus == PciBarSizingPlanStatus.Success
            && result.BindingStatus == VirtioPciBindingPlanStatus.Success
            && result.Binding.DeviceKind == VirtioPciDeviceKind.Network
            && result.Binding.VirtioDeviceId == VirtioPciDeviceDiscovery.NetworkDeviceId
            && result.Binding.Apertures.CommonConfiguration.BarSize == 0x1000
            && result.Binding.Apertures.NotifyConfiguration.BarSize == 0x1000
            && result.Binding.Apertures.IsrStatus.BarSize == 0x1000
            && result.Binding.Apertures.DeviceConfiguration.BarSize == 0x1000
            && sizes[0] == 0x1000
            && sizes[1] == 0x1000
            && sizes[2] == 0x1000
            && sizes[3] == 0x1000
            && accessor.OperationCount == 12);
    }

    [HostTest("drivers.virtio.pci.binding.derive creates block binding from fake bar probe", HostLane.Fast, 1)]
    private static bool TestCreatesBlockBindingFromFakeBarProbe()
    {
        ushort deviceId = VirtioPciDeviceDiscovery.ModernDeviceIdStart +
            VirtioPciDeviceDiscovery.BlockDeviceId;
        byte[] config = CreateConfig(
            deviceId,
            VirtioPciDeviceDiscovery.BlockDeviceConfigurationMinimumLength);
        FakePciConfigDwordAccessor accessor = CreateSuccessfulAccessor();

        VirtioPciBindingDerivedBarSizePlanResult result =
            Plan(CreateCandidate(deviceId), config, accessor, out _);

        return HostCheck.Result(result.Succeeded
            && result.IsInitialized
            && result.Binding.DeviceKind == VirtioPciDeviceKind.Block
            && result.Binding.VirtioDeviceId == VirtioPciDeviceDiscovery.BlockDeviceId
            && result.Binding.Apertures.DeviceConfiguration.Length
                == VirtioPciDeviceDiscovery.BlockDeviceConfigurationMinimumLength);
    }

    [HostTest("drivers.virtio.pci.binding.derive reports header parse failure", HostLane.Fast, 2)]
    private static bool TestReportsHeaderParseFailure()
    {
        ushort deviceId = VirtioPciDeviceDiscovery.ModernDeviceIdStart +
            VirtioPciDeviceDiscovery.NetworkDeviceId;
        FakePciConfigDwordAccessor accessor = CreateSuccessfulAccessor();

        VirtioPciBindingDerivedBarSizePlanResult result =
            Plan(CreateCandidate(deviceId), new byte[16], accessor, out _);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == VirtioPciBindingDerivedBarSizePlanStatus.HeaderParseFailed
            && result.HeaderStatus == PciConfigurationHeaderStatus.BufferTooSmall
            && result.BarDecode.Status == default
            && result.DerivedSizeStatus == default
            && result.BindingStatus == default
            && accessor.OperationCount == 0);
    }

    [HostTest("drivers.virtio.pci.binding.derive reports bar decode failure", HostLane.Fast, 3)]
    private static bool TestReportsBarDecodeFailure()
    {
        ushort deviceId = VirtioPciDeviceDiscovery.ModernDeviceIdStart +
            VirtioPciDeviceDiscovery.NetworkDeviceId;
        byte[] config = CreateConfig(
            deviceId,
            VirtioPciDeviceDiscovery.NetworkDeviceConfigurationMinimumLength,
            bar0: 0x80000002);
        FakePciConfigDwordAccessor accessor = CreateSuccessfulAccessor();

        VirtioPciBindingDerivedBarSizePlanResult result =
            Plan(CreateCandidate(deviceId), config, accessor, out _);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == VirtioPciBindingDerivedBarSizePlanStatus.BarDecodeFailed
            && result.HeaderStatus == PciConfigurationHeaderStatus.Success
            && result.BarDecode.Status == PciBarDecodeStatus.ReservedMemoryType
            && result.BarDecode.FailingBarIndex == 0
            && result.DerivedSizeStatus == default
            && result.BindingStatus == default
            && accessor.OperationCount == 0);
    }

    [HostTest("drivers.virtio.pci.binding.derive propagates derived size failure", HostLane.Fast, 4)]
    private static bool TestPropagatesDerivedSizeFailure()
    {
        ushort deviceId = VirtioPciDeviceDiscovery.ModernDeviceIdStart +
            VirtioPciDeviceDiscovery.NetworkDeviceId;
        byte[] config = CreateConfig(
            deviceId,
            VirtioPciDeviceDiscovery.NetworkDeviceConfigurationMinimumLength);
        FakePciConfigDwordAccessor accessor = CreateSuccessfulAccessor();
        accessor.SetReadback(registerOffset: 0x10, value: 0xFFFFF008);

        VirtioPciBindingDerivedBarSizePlanResult result =
            Plan(CreateCandidate(deviceId), config, accessor, out _);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == VirtioPciBindingDerivedBarSizePlanStatus.DerivedSizeFailed
            && result.DerivedSizeStatus == PciBarSizingProbeDerivedSizeStatus.SizingFailed
            && result.SizingStatus == PciBarSizingPlanStatus.MalformedReadback
            && result.BindingStatus == default
            && result.DerivedSize.FailingBarIndex == 0
            && result.DerivedSize.FailingDescriptorStatus == PciBarSizeDescriptorStatus.MalformedReadback);
    }

    [HostTest("drivers.virtio.pci.binding.derive reports short size destination", HostLane.Fast, 5)]
    private static bool TestReportsShortSizeDestination()
    {
        ushort deviceId = VirtioPciDeviceDiscovery.ModernDeviceIdStart +
            VirtioPciDeviceDiscovery.NetworkDeviceId;
        byte[] config = CreateConfig(
            deviceId,
            VirtioPciDeviceDiscovery.NetworkDeviceConfigurationMinimumLength);
        FakePciConfigDwordAccessor accessor = CreateSuccessfulAccessor();
        ulong[] sizes = new ulong[2];

        VirtioPciBindingDerivedBarSizePlanResult result =
            PlanWithSizes(CreateCandidate(deviceId), config, accessor, sizes);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == VirtioPciBindingDerivedBarSizePlanStatus.SizeDestinationTooSmall
            && result.DerivedSizeStatus == PciBarSizingProbeDerivedSizeStatus.Success
            && result.BindingStatus == default
            && sizes[0] == 0
            && sizes[1] == 0
            && accessor.OperationCount == 12);
    }

    [HostTest("drivers.virtio.pci.binding.derive propagates binding failure", HostLane.Fast, 6)]
    private static bool TestPropagatesBindingFailure()
    {
        ushort deviceId = VirtioPciDeviceDiscovery.ModernDeviceIdStart +
            VirtioPciDeviceDiscovery.NetworkDeviceId;
        byte[] config = CreateConfig(
            deviceId,
            VirtioPciDeviceDiscovery.NetworkDeviceConfigurationMinimumLength);
        config[CommonOffset + 2] = 8;
        FakePciConfigDwordAccessor accessor = CreateSuccessfulAccessor();

        VirtioPciBindingDerivedBarSizePlanResult result =
            Plan(CreateCandidate(deviceId), config, accessor, out _);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == VirtioPciBindingDerivedBarSizePlanStatus.BindingFailed
            && result.DerivedSizeStatus == PciBarSizingProbeDerivedSizeStatus.Success
            && result.BindingStatus == VirtioPciBindingPlanStatus.CapabilityParseFailed
            && result.BindingPlan.CapabilityParse.Status == VirtioPciCapabilityListParseStatus.VendorCapabilityTooShort
            && result.BindingPlan.CapabilityParse.FailingOffset == CommonOffset);
    }

    [HostTest("drivers.virtio.pci.binding.derive default result is not success", HostLane.Fast, 7)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        VirtioPciBindingDerivedBarSizePlanResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioPciBindingDerivedBarSizePlanStatus.Success
            && result.HeaderStatus == PciConfigurationHeaderStatus.Success
            && !result.BarDecode.IsInitialized
            && !result.BarDecode.Succeeded
            && result.BarDecode.Status == PciBarDecodeStatus.Success
            && !result.DerivedSize.IsInitialized
            && !result.DerivedSize.Succeeded
            && result.DerivedSizeStatus == PciBarSizingProbeDerivedSizeStatus.Success
            && result.ExecutionStatus == PciBarSizingProbeExecutionStatus.Success
            && result.SizingStatus == PciBarSizingPlanStatus.Success
            && result.BindingStatus == VirtioPciBindingPlanStatus.Success);
    }

    [HostTest("drivers.virtio.pci.binding.derive preserves no-live-config-access boundary", HostLane.Fast, 8)]
    private static bool TestPreservesNoLiveConfigAccessBoundary()
    {
        ushort deviceId = VirtioPciDeviceDiscovery.ModernDeviceIdStart +
            VirtioPciDeviceDiscovery.NetworkDeviceId;
        byte[] config = CreateConfig(
            deviceId,
            VirtioPciDeviceDiscovery.NetworkDeviceConfigurationMinimumLength);
        FakePciConfigDwordAccessor accessor = CreateSuccessfulAccessor();

        VirtioPciBindingDerivedBarSizePlanResult result =
            Plan(CreateCandidate(deviceId), config, accessor, out _);

        return HostCheck.Result(result.Succeeded
            && result.IsInitialized
            && VirtioPciBindingDerivedBarSizePlanner.RequiresCallerProvidedPciConfigurationSpace
            && VirtioPciBindingDerivedBarSizePlanner.RequiresDecodedBarDescriptors
            && VirtioPciBindingDerivedBarSizePlanner.RequiresFakeProbeDerivedSizes
            && VirtioPciBindingDerivedBarSizePlanner.UsesCallerOwnedCommandBuffer
            && VirtioPciBindingDerivedBarSizePlanner.UsesCallerOwnedReadbackBuffer
            && VirtioPciBindingDerivedBarSizePlanner.UsesCallerOwnedSizeBuffer
            && VirtioPciBindingDerivedBarSizePlanner.InvokesVirtioPciBindingPlan
            && !VirtioPciBindingDerivedBarSizePlanner.PerformsLiveConfigWrites
            && !VirtioPciBindingDerivedBarSizePlanner.PerformsLiveConfigReads
            && !VirtioPciBindingDerivedBarSizePlanner.RestoresHardwareBars
            && !VirtioPciBindingDerivedBarSizePlanner.MapsEcam
            && !VirtioPciBindingDerivedBarSizePlanner.MapsBars
            && !VirtioPciBindingDerivedBarSizePlanner.PerformsMmio
            && !VirtioPciBindingDerivedBarSizePlanner.ContainsVolatileAccess
            && !VirtioPciBindingDerivedBarSizePlanner.AllocatesDmaMemory
            && !VirtioPciBindingDerivedBarSizePlanner.PerformsDmaMapping
            && !VirtioPciBindingDerivedBarSizePlanner.ConfiguresQueues
            && !VirtioPciBindingDerivedBarSizePlanner.BindsLiveDriver
            && !VirtioPciBindingDerivedBarSizePlanner.HandlesInterrupts
            && !VirtioPciBindingDerivedBarSizePlanner.PerformsPacketIo
            && !VirtioPciBindingDerivedBarSizePlanner.PerformsBlockIo
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioPciBindingDerivedBarSizePlanResult>());
    }

    private static VirtioPciBindingDerivedBarSizePlanResult Plan(
        PciEcamScannedDevice device,
        byte[] config,
        IPciConfigDwordAccessor accessor,
        out ulong[] sizes)
    {
        PciBarSizingProbeAddressCommand[] commands =
            new PciBarSizingProbeAddressCommand[PciBarSizingProbePlanner.MaximumCommandCount];
        uint[] readbacks = new uint[PciBarSizingProbeExecutor.RequiredReadbackCount];
        sizes = new ulong[PciBarDecoder.BarCount];
        return PlanWithBuffers(
            device,
            config,
            accessor,
            commands,
            readbacks,
            sizes);
    }

    private static VirtioPciBindingDerivedBarSizePlanResult PlanWithSizes(
        PciEcamScannedDevice device,
        byte[] config,
        IPciConfigDwordAccessor accessor,
        ulong[] sizes)
    {
        PciBarSizingProbeAddressCommand[] commands =
            new PciBarSizingProbeAddressCommand[PciBarSizingProbePlanner.MaximumCommandCount];
        uint[] readbacks = new uint[PciBarSizingProbeExecutor.RequiredReadbackCount];
        return PlanWithBuffers(
            device,
            config,
            accessor,
            commands,
            readbacks,
            sizes);
    }

    private static VirtioPciBindingDerivedBarSizePlanResult PlanWithBuffers(
        PciEcamScannedDevice device,
        byte[] config,
        IPciConfigDwordAccessor accessor,
        PciBarSizingProbeAddressCommand[] commands,
        uint[] readbacks,
        ulong[] sizes)
    {
        return VirtioPciBindingDerivedBarSizePlanner.Plan(
            device,
            config,
            accessor,
            commands,
            readbacks,
            sizes);
    }

    private static FakePciConfigDwordAccessor CreateSuccessfulAccessor()
    {
        FakePciConfigDwordAccessor accessor = new FakePciConfigDwordAccessor();
        accessor.SetReadback(registerOffset: 0x10, value: 0xFFFFF000);
        accessor.SetReadback(registerOffset: 0x14, value: 0xFFFFF000);
        accessor.SetReadback(registerOffset: 0x18, value: 0xFFFFF000);
        accessor.SetReadback(registerOffset: 0x1C, value: 0xFFFFF000);
        return accessor;
    }

    private static PciEcamScannedDevice CreateCandidate(ushort deviceId)
    {
        return new PciEcamScannedDevice(
            segmentIndex: 1,
            new PciEcamSegment(
                basePhysicalAddress: 0xE0000000UL,
                segmentGroup: 7,
                startBus: 0,
                endBus: 0x7F),
            new PciScannedDevice(
                bus: 2,
                device: 4,
                function: 1,
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

    private static byte[] CreateConfig(
        ushort deviceId,
        uint deviceConfigurationLength,
        uint bar0 = 0x80000000,
        uint bar1 = 0x80001000,
        uint bar2 = 0x80002000,
        uint bar3 = 0x80003000)
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
        WriteUInt32(config, 0x10, bar0);
        WriteUInt32(config, 0x14, bar1);
        WriteUInt32(config, 0x18, bar2);
        WriteUInt32(config, 0x1C, bar3);
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

    private sealed class FakePciConfigDwordAccessor : IPciConfigDwordAccessor
    {
        private readonly uint[] _readbacks = new uint[PciBarSizingProbeExecutor.RequiredReadbackCount];

        public int OperationCount { get; private set; }

        public void SetReadback(
            ushort registerOffset,
            uint value)
        {
            byte barIndex = (byte)((registerOffset - PciBarSizingProbePlanner.FirstBarRegisterOffset) /
                PciBarSizingProbePlanner.BarRegisterStride);
            _readbacks[barIndex] = value;
        }

        public PciConfigDwordAccessStatus Write32(
            PciEcamConfigurationAddressResult address,
            uint value)
        {
            OperationCount++;
            return PciConfigDwordAccessStatus.Success;
        }

        public PciConfigDwordReadResult Read32(
            PciEcamConfigurationAddressResult address)
        {
            byte barIndex = (byte)((address.RegisterOffset - PciBarSizingProbePlanner.FirstBarRegisterOffset) /
                PciBarSizingProbePlanner.BarRegisterStride);
            OperationCount++;
            return new PciConfigDwordReadResult(
                PciConfigDwordAccessStatus.Success,
                _readbacks[barIndex]);
        }
    }
}
