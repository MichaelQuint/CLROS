using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Std.Binary;

internal static class PciBarSizingPlanTests
{
    private const byte CommonOffset = 0x40;
    private const byte NotifyOffset = 0x60;
    private const byte IsrOffset = 0x80;
    private const byte DeviceOffset = 0xA0;

    [HostTest("drivers.pci.bar.size decodes 32bit mmio aperture", HostLane.Fast, 0)]
    private static bool TestDecodes32BitMmioAperture()
    {
        byte[] headerBytes = CreateBaseHeader();
        WriteUInt32(headerBytes, 0x10, 0xFE000008);
        uint[] readbacks = CreateReadbacks();
        readbacks[0] = 0xFFFFF008;

        PciBarSizingPlanResult result = Plan(headerBytes, readbacks);
        Span<ulong> sizes = stackalloc ulong[PciBarDecoder.BarCount];

        return HostCheck.Result(result.Succeeded
            && result.IsInitialized
            && result.Status == PciBarSizingPlanStatus.Success
            && result.FailingBarIndex == PciBarDecoder.NoBarIndex
            && result.Bars.Bar0.Index == 0
            && result.Bars.Bar0.IsInitialized
            && result.Bars.Bar0.Kind == PciBarKind.Memory32
            && result.Bars.Bar0.Status == PciBarSizeDescriptorStatus.Success
            && result.Bars.Bar0.Succeeded
            && result.Bars.Bar0.RawReadback == 0xFFFFF008
            && result.Bars.Bar0.RawUpperReadback == 0
            && result.Bars.Bar0.BaseAddressBits == 0xFE000000
            && result.Bars.Bar0.SizeBytes == 0x1000
            && result.Bars.Bar0.IsPrefetchable
            && !result.Bars.Bar0.ConsumesNextSlot
            && result.TryCopySizesTo(sizes)
            && sizes[0] == 0x1000
            && sizes[1] == 0);
    }

    [HostTest("drivers.pci.bar.size decodes 64bit mmio aperture", HostLane.Fast, 1)]
    private static bool TestDecodes64BitMmioAperture()
    {
        byte[] headerBytes = CreateBaseHeader();
        WriteUInt32(headerBytes, 0x18, 0x8000000C);
        WriteUInt32(headerBytes, 0x1C, 0x00000001);
        uint[] readbacks = CreateReadbacks();
        readbacks[2] = 0xFFFFE00C;
        readbacks[3] = 0xFFFFFFFF;

        PciBarSizingPlanResult result = Plan(headerBytes, readbacks);
        Span<ulong> sizes = stackalloc ulong[PciBarDecoder.BarCount];

        return HostCheck.Result(result.Succeeded
            && result.IsInitialized
            && result.Bars.Bar2.Index == 2
            && result.Bars.Bar2.IsInitialized
            && result.Bars.Bar2.Kind == PciBarKind.Memory64
            && result.Bars.Bar2.Status == PciBarSizeDescriptorStatus.Success
            && result.Bars.Bar2.Succeeded
            && result.Bars.Bar2.RawReadback == 0xFFFFE00C
            && result.Bars.Bar2.RawUpperReadback == 0xFFFFFFFF
            && result.Bars.Bar2.BaseAddressBits == 0x0000000180000000UL
            && result.Bars.Bar2.SizeBytes == 0x2000
            && result.Bars.Bar2.ConsumesNextSlot
            && result.Bars.Bar2.PairBarIndex == 3
            && result.Bars.Bar3.Kind == PciBarKind.ConsumedByPrevious64BitBar
            && result.Bars.Bar3.Status == PciBarSizeDescriptorStatus.ConsumedByPrevious64BitBar
            && result.TryCopySizesTo(sizes)
            && sizes[2] == 0x2000
            && sizes[3] == 0);
    }

    [HostTest("drivers.pci.bar.size records absent and consumed bars", HostLane.Fast, 2)]
    private static bool TestRecordsAbsentIoAndConsumedBars()
    {
        byte[] headerBytes = CreateBaseHeader();
        WriteUInt32(headerBytes, 0x14, 0x0000C001);
        WriteUInt32(headerBytes, 0x18, 0x90000004);
        WriteUInt32(headerBytes, 0x1C, 0);
        uint[] readbacks = CreateReadbacks();
        readbacks[1] = 0xFFFFFF01;
        readbacks[2] = 0xFFFFF004;
        readbacks[3] = 0xFFFFFFFF;

        PciBarSizingPlanResult result = Plan(headerBytes, readbacks);

        return HostCheck.Result(result.Succeeded
            && result.IsInitialized
            && result.Bars.Bar0.Kind == PciBarKind.Absent
            && result.Bars.Bar0.IsInitialized
            && result.Bars.Bar0.Status == PciBarSizeDescriptorStatus.Absent
            && result.Bars.Bar0.SizeBytes == 0
            && result.Bars.Bar1.Kind == PciBarKind.Io
            && result.Bars.Bar1.IsInitialized
            && result.Bars.Bar1.Status == PciBarSizeDescriptorStatus.IoBarNotSized
            && result.Bars.Bar1.RawReadback == 0xFFFFFF01
            && result.Bars.Bar1.SizeBytes == 0
            && result.Bars.Bar2.Kind == PciBarKind.Memory64
            && result.Bars.Bar2.IsInitialized
            && result.Bars.Bar2.SizeBytes == 0x1000
            && result.Bars.Bar3.Kind == PciBarKind.ConsumedByPrevious64BitBar
            && result.Bars.Bar3.IsInitialized
            && result.Bars.Bar3.Status == PciBarSizeDescriptorStatus.ConsumedByPrevious64BitBar
            && result.Bars.Bar3.PairBarIndex == 2
            && result.Bars.Bar3.SizeBytes == 0);
    }

    [HostTest("drivers.pci.bar.size rejects malformed readback", HostLane.Fast, 3)]
    private static bool TestRejectsMalformedReadback()
    {
        byte[] headerBytes = CreateBaseHeader();
        WriteUInt32(headerBytes, 0x10, 0xFE000000);
        uint[] readbacks = CreateReadbacks();
        readbacks[0] = 0xFFFFF004;

        PciBarSizingPlanResult result = Plan(headerBytes, readbacks);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == PciBarSizingPlanStatus.MalformedReadback
            && result.FailingBarIndex == 0
            && result.FailingDescriptorStatus == PciBarSizeDescriptorStatus.MalformedReadback
            && result.Bars.Bar0.Kind == PciBarKind.Memory32
            && result.Bars.Bar0.IsInitialized
            && result.Bars.Bar0.Status == PciBarSizeDescriptorStatus.MalformedReadback
            && result.Bars.Bar0.RawReadback == 0xFFFFF004
            && result.Bars.Bar0.SizeBytes == 0);
    }

    [HostTest("drivers.pci.bar.size rejects base alignment mismatch", HostLane.Fast, 4)]
    private static bool TestRejectsBaseAlignmentMismatch()
    {
        byte[] headerBytes = CreateBaseHeader();
        WriteUInt32(headerBytes, 0x10, 0xFE000800);
        uint[] readbacks = CreateReadbacks();
        readbacks[0] = 0xFFFFF000;

        PciBarSizingPlanResult result = Plan(headerBytes, readbacks);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == PciBarSizingPlanStatus.BaseAlignmentMismatch
            && result.FailingBarIndex == 0
            && result.FailingDescriptorStatus == PciBarSizeDescriptorStatus.BaseAlignmentMismatch
            && result.Bars.Bar0.IsInitialized
            && result.Bars.Bar0.BaseAddressBits == 0xFE000800
            && result.Bars.Bar0.SizeBytes == 0x1000);
    }

    [HostTest("drivers.pci.bar.size default result is not success", HostLane.Fast, 5)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        PciBarSizingPlanResult result = default;
        PciBarSizeDescriptor descriptor = default;
        Span<ulong> sizes = stackalloc ulong[PciBarDecoder.BarCount];

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == PciBarSizingPlanStatus.Success
            && result.FailingBarIndex == 0
            && result.FailingDescriptorStatus == PciBarSizeDescriptorStatus.Success
            && !result.TryCopySizesTo(sizes)
            && !result.Bars.Bar0.IsInitialized
            && !result.Bars.Bar0.Succeeded
            && result.Bars.Bar0.Status == PciBarSizeDescriptorStatus.Success
            && !descriptor.IsInitialized
            && !descriptor.Succeeded
            && descriptor.Status == PciBarSizeDescriptorStatus.Success
            && descriptor.Index == 0
            && descriptor.Kind == PciBarKind.Absent
            && descriptor.SizeBytes == 0);
    }

    [HostTest("drivers.pci.bar.size feeds virtio aperture validation", HostLane.Fast, 6)]
    private static bool TestFeedsVirtioApertureValidation()
    {
        ushort deviceId = (ushort)(VirtioPciDeviceDiscovery.ModernDeviceIdStart +
            VirtioPciDeviceDiscovery.NetworkDeviceId);
        byte[] config = CreateVirtioNetworkConfiguration(deviceId);
        uint[] readbacks = CreateReadbacks();
        readbacks[0] = 0xFFFFF000;
        readbacks[1] = 0xFFFFF000;
        readbacks[2] = 0xFFFFF000;
        readbacks[3] = 0xFFFFF000;

        PciBarSizingPlanResult sizing = Plan(config, readbacks);
        ulong[] sizes = new ulong[PciBarDecoder.BarCount];
        bool copied = sizing.TryCopySizesTo(sizes);
        VirtioPciBindingPlanResult binding = VirtioPciBindingPlan.Create(
            CreateScannedDevice(deviceId),
            config,
            sizes);

        return HostCheck.Result(sizing.Succeeded
            && sizing.IsInitialized
            && copied
            && sizes[0] == 0x1000
            && sizes[1] == 0x1000
            && sizes[2] == 0x1000
            && sizes[3] == 0x1000
            && binding.Succeeded
            && binding.ApertureValidation.Succeeded
            && binding.Binding.Apertures.CommonConfiguration.BarSize == 0x1000
            && binding.Binding.Apertures.NotifyConfiguration.BarSize == 0x1000
            && binding.Binding.Apertures.IsrStatus.BarSize == 0x1000
            && binding.Binding.Apertures.DeviceConfiguration.BarSize == 0x1000);
    }

    [HostTest("drivers.pci.bar.size preserves no-live-config-write boundary", HostLane.Fast, 7)]
    private static bool TestPreservesNoLiveConfigWriteBoundary()
    {
        byte[] headerBytes = CreateBaseHeader();
        WriteUInt32(headerBytes, 0x10, 0xFE000000);
        uint[] readbacks = CreateReadbacks();
        readbacks[0] = 0xFFFFF000;

        PciBarSizingPlanResult result = Plan(headerBytes, readbacks);

        return HostCheck.Result(result.Succeeded
            && result.IsInitialized
            && PciBarSizingPlanner.RequiresCallerOwnedOriginalBarSnapshot
            && PciBarSizingPlanner.RequiresCallerOwnedSizingReadbacks
            && PciBarSizingPlanner.ProducesVirtioApertureValidatorInput
            && !PciBarSizingPlanner.PerformsLiveConfigWrites
            && !PciBarSizingPlanner.PerformsLiveConfigReads
            && !PciBarSizingPlanner.RestoresBars
            && !PciBarSizingPlanner.MapsBars
            && !PciBarSizingPlanner.PerformsMmio
            && !PciBarSizingPlanner.PerformsDmaMapping
            && !PciBarSizingPlanner.AllocatesDmaMemory
            && !PciBarSizingPlanner.HandlesInterrupts
            && !PciBarSizingPlanner.PerformsPacketIo
            && !PciBarSizingPlanner.PerformsBlockIo
            && !RuntimeHelpers.IsReferenceOrContainsReferences<PciBarSizingPlanResult>());
    }

    private static PciBarSizingPlanResult Plan(
        byte[] headerBytes,
        uint[] readbacks)
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

        return PciBarSizingPlanner.Plan(decode.Bars, readbacks);
    }

    private static uint[] CreateReadbacks()
    {
        return new uint[PciBarDecoder.BarCount];
    }

    private static PciEcamScannedDevice CreateScannedDevice(ushort deviceId)
    {
        return new PciEcamScannedDevice(
            segmentIndex: 1,
            new PciEcamSegment(
                basePhysicalAddress: 0xE0000000UL,
                segmentGroup: 0,
                startBus: 0,
                endBus: 0x7F),
            new PciScannedDevice(
                bus: 2,
                device: 4,
                function: 0,
                vendorId: VirtioPciDeviceDiscovery.VirtioVendorId,
                deviceId,
                revisionId: 0,
                classCode: 0,
                subclass: 0,
                programmingInterface: 0,
                headerType: PciConfigurationHeader.Type0HeaderType,
                isMultifunction: false,
                hasCapabilitiesList: true,
                capabilityPointer: CommonOffset));
    }

    private static byte[] CreateVirtioNetworkConfiguration(ushort deviceId)
    {
        byte[] config = CreateBaseHeader();
        WriteUInt16(config, 0x02, deviceId);
        WriteUInt16(config, 0x06, PciConfigurationHeader.StatusCapabilitiesListBit);
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
            offsetValue: 0x80,
            length: 0x100,
            notifyOffsetMultiplier: 4,
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

    private static byte[] CreateBaseHeader()
    {
        byte[] headerBytes = new byte[256];
        WriteUInt16(headerBytes, 0x00, VirtioPciDeviceDiscovery.VirtioVendorId);
        WriteUInt16(headerBytes, 0x02, 0x1041);
        headerBytes[0x0E] = PciConfigurationHeader.Type0HeaderType;
        return headerBytes;
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

    private static void WriteUInt16(Span<byte> destination, int offset, ushort value)
    {
        EndianPrimitives.WriteUInt16LittleEndian(destination.Slice(offset, sizeof(ushort)), value);
    }

    private static void WriteUInt32(Span<byte> destination, int offset, uint value)
    {
        EndianPrimitives.WriteUInt32LittleEndian(destination.Slice(offset, sizeof(uint)), value);
    }
}
