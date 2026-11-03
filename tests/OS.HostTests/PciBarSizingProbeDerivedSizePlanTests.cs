using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Std.Binary;

internal static partial class PciBarSizingProbeDerivedSizePlanTests
{
    [HostTest("drivers.pci.bar.sizeprobe.derive derives 32bit aperture from fake readback", HostLane.Fast, 0)]
    private static bool TestDerives32BitApertureFromFakeReadback()
    {
        PciBarDescriptorSet bars = DecodeBars(CreateHeader(bar0: 0xFE000008, bar2: 0, bar3: 0));
        FakePciConfigDwordAccessor accessor = new FakePciConfigDwordAccessor();
        accessor.SetReadback(registerOffset: 0x10, value: 0xFFFFF008);
        PciBarSizingProbeDerivedSizeResult result = Plan(bars, accessor);
        Span<ulong> sizes = stackalloc ulong[PciBarDecoder.BarCount];
        return HostCheck.Result(result.Succeeded && result.IsInitialized && result.Status == PciBarSizingProbeDerivedSizeStatus.Success && result.AddressPlanStatus == PciBarSizingProbeAddressPlanStatus.Success && result.ExecutionStatus == PciBarSizingProbeExecutionStatus.Success && result.SizingStatus == PciBarSizingPlanStatus.Success && result.Execution.ReadbackCount == 1 && result.Sizing.Bars.Bar0.SizeBytes == 0x1000 && result.Sizing.Bars.Bar0.RawReadback == 0xFFFFF008 && result.TryCopySizesTo(sizes) && sizes[0] == 0x1000 && sizes[1] == 0 && accessor.OperationCount == 3);
    }

    [HostTest("drivers.pci.bar.sizeprobe.derive derives 64bit aperture from fake readbacks", HostLane.Fast, 1)]
    private static bool TestDerives64BitApertureFromFakeReadbacks()
    {
        PciBarDescriptorSet bars = DecodeBars(CreateHeader(bar0: 0, bar2: 0x8000000C, bar3: 0x00000001));
        FakePciConfigDwordAccessor accessor = new FakePciConfigDwordAccessor();
        accessor.SetReadback(registerOffset: 0x18, value: 0xFFFFE00C);
        accessor.SetReadback(registerOffset: 0x1C, value: 0xFFFFFFFF);
        PciBarSizingProbeDerivedSizeResult result = Plan(bars, accessor);
        Span<ulong> sizes = stackalloc ulong[PciBarDecoder.BarCount];
        return HostCheck.Result(result.Succeeded && result.IsInitialized && result.Execution.ReadbackCount == 2 && result.Sizing.Bars.Bar2.SizeBytes == 0x2000 && result.Sizing.Bars.Bar2.RawReadback == 0xFFFFE00C && result.Sizing.Bars.Bar2.RawUpperReadback == 0xFFFFFFFF && result.Sizing.Bars.Bar3.Status == PciBarSizeDescriptorStatus.ConsumedByPrevious64BitBar && result.TryCopySizesTo(sizes) && sizes[2] == 0x2000 && sizes[3] == 0 && accessor.OperationCount == 6 && IsOperation(accessor.Operations[4], PciBarSizingProbeCommandKind.RestoreOriginal, registerOffset: 0x1C, value: 0x00000001) && IsOperation(accessor.Operations[5], PciBarSizingProbeCommandKind.RestoreOriginal, registerOffset: 0x18, value: 0x8000000C));
    }

    [HostTest("drivers.pci.bar.sizeprobe.derive propagates execution failure", HostLane.Fast, 2)]
    private static bool TestPropagatesExecutionFailure()
    {
        PciBarDescriptorSet bars = DecodeBars(CreateHeader(bar0: 0xFE000008, bar2: 0, bar3: 0));
        FakePciConfigDwordAccessor accessor = new FakePciConfigDwordAccessor();
        accessor.FailReadAtOperation = 1;
        PciBarSizingProbeDerivedSizeResult result = Plan(bars, accessor);
        return HostCheck.Result(!result.Succeeded && result.IsInitialized && result.Status == PciBarSizingProbeDerivedSizeStatus.ExecutionFailed && result.AddressPlanStatus == PciBarSizingProbeAddressPlanStatus.Success && result.ExecutionStatus == PciBarSizingProbeExecutionStatus.ReadFailed && result.SizingStatus == default && result.FailingBarIndex == 0 && result.Execution.FailingCommandIndex == 1 && result.Execution.ReadbackCount == 0 && accessor.OperationCount == 2);
    }

    [HostTest("drivers.pci.bar.sizeprobe.derive propagates sizing failure", HostLane.Fast, 3)]
    private static bool TestPropagatesSizingFailure()
    {
        PciBarDescriptorSet bars = DecodeBars(CreateHeader(bar0: 0xFE000008, bar2: 0, bar3: 0));
        FakePciConfigDwordAccessor accessor = new FakePciConfigDwordAccessor();
        accessor.SetReadback(registerOffset: 0x10, value: 0xFFFFF000);
        PciBarSizingProbeDerivedSizeResult result = Plan(bars, accessor);
        return HostCheck.Result(!result.Succeeded && result.IsInitialized && result.Status == PciBarSizingProbeDerivedSizeStatus.SizingFailed && result.AddressPlanStatus == PciBarSizingProbeAddressPlanStatus.Success && result.ExecutionStatus == PciBarSizingProbeExecutionStatus.Success && result.SizingStatus == PciBarSizingPlanStatus.MalformedReadback && result.FailingBarIndex == 0 && result.FailingDescriptorStatus == PciBarSizeDescriptorStatus.MalformedReadback && result.Execution.ReadbackCount == 1 && result.Sizing.Bars.Bar0.RawReadback == 0xFFFFF000);
    }

    [HostTest("drivers.pci.bar.sizeprobe.derive feeds virtio aperture validation", HostLane.Fast, 4)]
    private static bool TestFeedsVirtioApertureValidation()
    {
        ushort deviceId = (ushort)(VirtioPciDeviceDiscovery.ModernDeviceIdStart + VirtioPciDeviceDiscovery.NetworkDeviceId);
        byte[] config = CreateVirtioNetworkConfiguration(deviceId);
        PciBarDescriptorSet bars = DecodeBars(config);
        FakePciConfigDwordAccessor accessor = new FakePciConfigDwordAccessor();
        accessor.SetReadback(registerOffset: 0x10, value: 0xFFFFF000);
        accessor.SetReadback(registerOffset: 0x14, value: 0xFFFFF000);
        accessor.SetReadback(registerOffset: 0x18, value: 0xFFFFF000);
        accessor.SetReadback(registerOffset: 0x1C, value: 0xFFFFF000);
        PciBarSizingProbeDerivedSizeResult result = Plan(bars, accessor);
        ulong[] sizes = new ulong[PciBarDecoder.BarCount];
        bool copied = result.TryCopySizesTo(sizes);
        VirtioPciBindingPlanResult binding = VirtioPciBindingPlan.Create(CreateScannedDevice(deviceId), config, sizes);
        return HostCheck.Result(result.Succeeded && result.IsInitialized && copied && sizes[0] == 0x1000 && sizes[1] == 0x1000 && sizes[2] == 0x1000 && sizes[3] == 0x1000 && binding.Succeeded && binding.ApertureValidation.Succeeded && binding.Binding.Apertures.CommonConfiguration.BarSize == 0x1000 && binding.Binding.Apertures.NotifyConfiguration.BarSize == 0x1000 && binding.Binding.Apertures.IsrStatus.BarSize == 0x1000 && binding.Binding.Apertures.DeviceConfiguration.BarSize == 0x1000);
    }

    [HostTest("drivers.pci.bar.sizeprobe.derive default result is not success", HostLane.Fast, 5)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        PciBarSizingProbeDerivedSizeResult result = default;
        Span<ulong> sizes = stackalloc ulong[PciBarDecoder.BarCount];
        return HostCheck.Result(!result.IsInitialized && !result.Succeeded && result.Status == PciBarSizingProbeDerivedSizeStatus.Success && result.AddressPlanStatus == PciBarSizingProbeAddressPlanStatus.Success && !result.AddressPlan.IsInitialized && !result.AddressPlan.Succeeded && result.ExecutionStatus == PciBarSizingProbeExecutionStatus.Success && !result.Execution.IsInitialized && !result.Execution.Succeeded && result.SizingStatus == PciBarSizingPlanStatus.Success && !result.Sizing.IsInitialized && !result.Sizing.Succeeded && !result.TryCopySizesTo(sizes) && result.FailingBarIndex == 0 && result.FailingDescriptorStatus == PciBarSizeDescriptorStatus.Success);
    }

    [HostTest("drivers.pci.bar.sizeprobe.derive preserves no-live-config-access boundary", HostLane.Fast, 6)]
    private static bool TestPreservesNoLiveConfigAccessBoundary()
    {
        PciBarDescriptorSet bars = DecodeBars(CreateHeader(bar0: 0xFE000008, bar2: 0, bar3: 0));
        FakePciConfigDwordAccessor accessor = new FakePciConfigDwordAccessor();
        accessor.SetReadback(registerOffset: 0x10, value: 0xFFFFF008);
        PciBarSizingProbeDerivedSizeResult result = Plan(bars, accessor);
        return HostCheck.Result(result.Succeeded && result.IsInitialized && PciBarSizingProbeDerivedSizePlanner.RequiresDecodedBarDescriptors && PciBarSizingProbeDerivedSizePlanner.RequiresAddressPlan && PciBarSizingProbeDerivedSizePlanner.RequiresFakeProbeExecution && PciBarSizingProbeDerivedSizePlanner.UsesCallerOwnedReadbackSnapshots && PciBarSizingProbeDerivedSizePlanner.ProducesVirtioApertureValidatorInput && !PciBarSizingProbeDerivedSizePlanner.PerformsLiveConfigWrites && !PciBarSizingProbeDerivedSizePlanner.PerformsLiveConfigReads && !PciBarSizingProbeDerivedSizePlanner.RestoresHardwareBars && !PciBarSizingProbeDerivedSizePlanner.MapsEcam && !PciBarSizingProbeDerivedSizePlanner.MapsBars && !PciBarSizingProbeDerivedSizePlanner.PerformsMmio && !PciBarSizingProbeDerivedSizePlanner.ContainsVolatileAccess && !PciBarSizingProbeDerivedSizePlanner.AllocatesDmaMemory && !PciBarSizingProbeDerivedSizePlanner.PerformsDmaMapping && !PciBarSizingProbeDerivedSizePlanner.HandlesInterrupts && !PciBarSizingProbeDerivedSizePlanner.PerformsPacketIo && !PciBarSizingProbeDerivedSizePlanner.PerformsBlockIo && !RuntimeHelpers.IsReferenceOrContainsReferences<PciBarSizingProbeDerivedSizeResult>());
    }

    private static PciBarSizingProbeDerivedSizeResult Plan(PciBarDescriptorSet bars, IPciConfigDwordAccessor accessor)
    {
        Span<PciBarSizingProbeAddressCommand> commands = stackalloc PciBarSizingProbeAddressCommand[PciBarSizingProbePlanner.MaximumCommandCount];
        Span<uint> readbacks = stackalloc uint[PciBarSizingProbeExecutor.RequiredReadbackCount];
        return PciBarSizingProbeDerivedSizePlanner.Plan(new PciEcamSegment(0xE0000000, segmentGroup: 1, startBus: 0x20, endBus: 0x2F), bus: 0x24, device: 3, function: 5, bars, accessor, commands, readbacks);
    }

    private static bool IsOperation(FakePciConfigDwordOperation operation, PciBarSizingProbeCommandKind kind, ushort registerOffset, uint value)
    {
        return operation.Kind == kind && operation.RegisterOffset == registerOffset && operation.Value == value;
    }

    private static PciBarDescriptorSet DecodeBars(byte[] headerBytes)
    {
        PciConfigurationHeaderResult header = PciConfigurationHeader.ParseType0(headerBytes);
        if (!header.Succeeded)
        {
            throw new InvalidOperationException("test header parse failed: " + header.Status);
        }

        PciBarDecodeResult decode = PciBarDecoder.Decode(header.Header);
        if (!decode.Succeeded)
        {
            throw new InvalidOperationException("test BAR decode failed: " + decode.Status);
        }

        return decode.Bars;
    }

    private static PciEcamScannedDevice CreateScannedDevice(ushort deviceId)
    {
        return new PciEcamScannedDevice(segmentIndex: 1, new PciEcamSegment(basePhysicalAddress: 0xE0000000UL, segmentGroup: 0, startBus: 0, endBus: 0x7F), new PciScannedDevice(bus: 2, device: 4, function: 0, vendorId: VirtioPciDeviceDiscovery.VirtioVendorId, deviceId, revisionId: 0, classCode: 0, subclass: 0, programmingInterface: 0, headerType: PciConfigurationHeader.Type0HeaderType, isMultifunction: false, hasCapabilitiesList: true, capabilityPointer: CommonOffset));
    }

    private static byte[] CreateVirtioNetworkConfiguration(ushort deviceId)
    {
        byte[] config = CreateHeader(bar0: 0x80000000, bar2: 0x80002000, bar3: 0x80003000);
        WriteUInt16(config, 0x02, deviceId);
        WriteUInt16(config, 0x06, PciConfigurationHeader.StatusCapabilitiesListBit);
        WriteUInt32(config, 0x14, 0x80001000);
        config[0x34] = CommonOffset;
        WriteVirtioCapability(config, CommonOffset, NotifyOffset, VirtioPciCapabilityType.CommonConfiguration, bar: 0, offsetValue: 0x20, length: VirtioPciDeviceDiscovery.CommonConfigurationMinimumLength, notifyOffsetMultiplier: 0, capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);
        WriteVirtioCapability(config, NotifyOffset, IsrOffset, VirtioPciCapabilityType.NotifyConfiguration, bar: 1, offsetValue: 0x80, length: 0x100, notifyOffsetMultiplier: 4, capabilityLength: VirtioPciCapabilityListParser.ModernVirtioNotifyCapabilityLength);
        WriteVirtioCapability(config, IsrOffset, DeviceOffset, VirtioPciCapabilityType.IsrStatus, bar: 2, offsetValue: 0, length: 1, notifyOffsetMultiplier: 0, capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);
        WriteVirtioCapability(config, DeviceOffset, nextOffset: 0, VirtioPciCapabilityType.DeviceConfiguration, bar: 3, offsetValue: 0, length: 0x10, notifyOffsetMultiplier: 0, capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);
        return config;
    }

    private static byte[] CreateHeader(uint bar0, uint bar2, uint bar3)
    {
        byte[] headerBytes = new byte[256];
        WriteUInt16(headerBytes, 0x00, VirtioPciDeviceDiscovery.VirtioVendorId);
        WriteUInt16(headerBytes, 0x02, 0x1041);
        headerBytes[0x0E] = PciConfigurationHeader.Type0HeaderType;
        WriteUInt32(headerBytes, 0x10, bar0);
        WriteUInt32(headerBytes, 0x18, bar2);
        WriteUInt32(headerBytes, 0x1C, bar3);
        return headerBytes;
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

    private static void WriteUInt16(Span<byte> destination, int offset, ushort value)
    {
        EndianPrimitives.WriteUInt16LittleEndian(destination.Slice(offset, sizeof(ushort)), value);
    }

    private static void WriteUInt32(Span<byte> destination, int offset, uint value)
    {
        EndianPrimitives.WriteUInt32LittleEndian(destination.Slice(offset, sizeof(uint)), value);
    }

    private const byte CommonOffset = 0x40;
    private const byte NotifyOffset = 0x60;
    private const byte IsrOffset = 0x80;
    private const byte DeviceOffset = 0xA0;
}
