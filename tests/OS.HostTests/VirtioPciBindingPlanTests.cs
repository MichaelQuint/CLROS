using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Std.Binary;

internal static class VirtioPciBindingPlanTests
{
    private const byte CommonOffset = 0x40;
    private const byte NotifyOffset = 0x60;
    private const byte IsrOffset = 0x80;
    private const byte DeviceOffset = 0xA0;

    [HostTest("drivers.virtio.pci.binding creates block binding plan", HostLane.Fast, 0)]
    private static bool TestCreatesBlockBindingPlan()
    {
        ushort deviceId = VirtioPciDeviceDiscovery.ModernDeviceIdStart +
            VirtioPciDeviceDiscovery.BlockDeviceId;
        byte[] config = CreateConfig(deviceId, VirtioPciDeviceDiscovery.BlockDeviceConfigurationMinimumLength);
        VirtioPciBindingPlanResult result = VirtioPciBindingPlan.Create(
            CreateCandidate(deviceId),
            config,
            CreateBarSizes());

        return HostCheck.Result(result.Succeeded
            && result.Status == VirtioPciBindingPlanStatus.Success
            && result.HeaderStatus == PciConfigurationHeaderStatus.Success
            && result.CapabilityParse.Status == VirtioPciCapabilityListParseStatus.Success
            && result.Discovery.Status == VirtioPciDeviceDiscoveryStatus.Success
            && result.BarDecode.Status == PciBarDecodeStatus.Success
            && result.ApertureValidation.Status == VirtioPciBarApertureValidationStatus.Success
            && result.Binding.Identity.VendorId == VirtioPciDeviceDiscovery.VirtioVendorId
            && result.Binding.Identity.DeviceId == deviceId
            && result.Binding.DeviceKind == VirtioPciDeviceKind.Block
            && result.Binding.VirtioDeviceId == VirtioPciDeviceDiscovery.BlockDeviceId
            && result.Binding.SegmentIndex == 1
            && result.Binding.EcamBasePhysicalAddress == 0xE0000000UL
            && result.Binding.SegmentGroup == 7
            && result.Binding.Bus == 2
            && result.Binding.Device == 4
            && result.Binding.Function == 1
            && result.Binding.CapabilityPointer == CommonOffset
            && result.Binding.Apertures.CommonConfiguration.BarIndex == 0
            && result.Binding.Apertures.NotifyConfiguration.NotifyOffsetMultiplier == 4
            && result.Binding.Apertures.DeviceConfiguration.Length
                == VirtioPciDeviceDiscovery.BlockDeviceConfigurationMinimumLength);
    }

    [HostTest("drivers.virtio.pci.binding creates network binding plan", HostLane.Fast, 1)]
    private static bool TestCreatesNetworkBindingPlan()
    {
        ushort deviceId = VirtioPciDeviceDiscovery.ModernDeviceIdStart +
            VirtioPciDeviceDiscovery.NetworkDeviceId;
        byte[] config = CreateConfig(deviceId, VirtioPciDeviceDiscovery.NetworkDeviceConfigurationMinimumLength);
        VirtioPciBindingPlanResult result = VirtioPciBindingPlan.Create(
            CreateCandidate(deviceId),
            config,
            CreateBarSizes());

        return HostCheck.Result(result.Succeeded
            && result.Binding.DeviceKind == VirtioPciDeviceKind.Network
            && result.Binding.VirtioDeviceId == VirtioPciDeviceDiscovery.NetworkDeviceId
            && result.Discovery.DeviceKind == VirtioPciDeviceKind.Network
            && result.Binding.Apertures.DeviceConfiguration.Length
                == VirtioPciDeviceDiscovery.NetworkDeviceConfigurationMinimumLength);
    }

    [HostTest("drivers.virtio.pci.binding reports capability parse failure", HostLane.Fast, 2)]
    private static bool TestReportsCapabilityParseFailure()
    {
        ushort deviceId = VirtioPciDeviceDiscovery.ModernDeviceIdStart +
            VirtioPciDeviceDiscovery.BlockDeviceId;
        byte[] config = CreateConfig(deviceId, VirtioPciDeviceDiscovery.BlockDeviceConfigurationMinimumLength);
        config[CommonOffset + 2] = 8;

        VirtioPciBindingPlanResult result = VirtioPciBindingPlan.Create(
            CreateCandidate(deviceId),
            config,
            CreateBarSizes());

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioPciBindingPlanStatus.CapabilityParseFailed
            && result.HeaderStatus == PciConfigurationHeaderStatus.Success
            && result.CapabilityParse.Status == VirtioPciCapabilityListParseStatus.VendorCapabilityTooShort
            && result.CapabilityParse.FailingOffset == CommonOffset
            && result.Discovery.Status == default
            && result.BarDecode.Status == default
            && result.ApertureValidation.Status == default);
    }

    [HostTest("drivers.virtio.pci.binding reports discovery failure", HostLane.Fast, 3)]
    private static bool TestReportsDiscoveryFailure()
    {
        ushort unsupportedDeviceId = VirtioPciDeviceDiscovery.ModernDeviceIdStart + 0x05;
        byte[] config = CreateConfig(
            unsupportedDeviceId,
            VirtioPciDeviceDiscovery.BlockDeviceConfigurationMinimumLength);
        VirtioPciBindingPlanResult result = VirtioPciBindingPlan.Create(
            CreateCandidate(unsupportedDeviceId),
            config,
            CreateBarSizes());

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioPciBindingPlanStatus.DiscoveryFailed
            && result.CapabilityParse.Status == VirtioPciCapabilityListParseStatus.Success
            && result.Discovery.Status == VirtioPciDeviceDiscoveryStatus.UnsupportedDeviceKind
            && result.Discovery.Identity.DeviceId == unsupportedDeviceId
            && result.BarDecode.Status == default
            && result.ApertureValidation.Status == default);
    }

    [HostTest("drivers.virtio.pci.binding reports bar decode failure", HostLane.Fast, 4)]
    private static bool TestReportsBarDecodeFailure()
    {
        ushort deviceId = VirtioPciDeviceDiscovery.ModernDeviceIdStart +
            VirtioPciDeviceDiscovery.BlockDeviceId;
        byte[] config = CreateConfig(
            deviceId,
            VirtioPciDeviceDiscovery.BlockDeviceConfigurationMinimumLength,
            bar0: 0x80000002);
        VirtioPciBindingPlanResult result = VirtioPciBindingPlan.Create(
            CreateCandidate(deviceId),
            config,
            CreateBarSizes());

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioPciBindingPlanStatus.BarDecodeFailed
            && result.CapabilityParse.Status == VirtioPciCapabilityListParseStatus.Success
            && result.Discovery.Status == VirtioPciDeviceDiscoveryStatus.Success
            && result.BarDecode.Status == PciBarDecodeStatus.ReservedMemoryType
            && result.BarDecode.FailingBarIndex == 0
            && result.ApertureValidation.Status == default);
    }

    [HostTest("drivers.virtio.pci.binding reports aperture validation failure", HostLane.Fast, 5)]
    private static bool TestReportsApertureValidationFailure()
    {
        ushort deviceId = VirtioPciDeviceDiscovery.ModernDeviceIdStart +
            VirtioPciDeviceDiscovery.BlockDeviceId;
        byte[] config = CreateConfig(
            deviceId,
            VirtioPciDeviceDiscovery.BlockDeviceConfigurationMinimumLength,
            bar0: 0x0000C001);
        VirtioPciBindingPlanResult result = VirtioPciBindingPlan.Create(
            CreateCandidate(deviceId),
            config,
            CreateBarSizes());

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioPciBindingPlanStatus.ApertureValidationFailed
            && result.CapabilityParse.Status == VirtioPciCapabilityListParseStatus.Success
            && result.Discovery.Status == VirtioPciDeviceDiscoveryStatus.Success
            && result.BarDecode.Status == PciBarDecodeStatus.Success
            && result.ApertureValidation.Status == VirtioPciBarApertureValidationStatus.BarIsIo
            && result.ApertureValidation.FailingCapabilityType == VirtioPciCapabilityType.CommonConfiguration
            && result.ApertureValidation.FailingBarIndex == 0);
    }

    [HostTest("drivers.virtio.pci.binding default result is not success", HostLane.Fast, 6)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        VirtioPciBindingPlanResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioPciBindingPlanStatus.Success
            && result.HeaderStatus == PciConfigurationHeaderStatus.Success
            && result.CapabilityParse.Status == default
            && result.Discovery.Status == default
            && result.BarDecode.Status == default
            && result.ApertureValidation.Status == default
            && result.Binding.Identity.VendorId == 0
            && result.Binding.DeviceKind == default
            && result.Binding.CapabilityPointer == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioPciBindingPlanResult>());
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

    private static ulong[] CreateBarSizes()
    {
        return new ulong[]
        {
            0x1000,
            0x1000,
            0x1000,
            0x1000,
            0,
            0
        };
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
