using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Std.Binary;

internal static class VirtioPciBindingCatalogTests
{
    private const byte CommonOffset = 0x40;
    private const byte NotifyOffset = 0x60;
    private const byte IsrOffset = 0x80;
    private const byte DeviceOffset = 0xA0;

    [HostTest("drivers.virtio.pci.catalog binds multiple candidates", HostLane.Fast, 0)]
    private static bool TestBindsMultipleCandidates()
    {
        VirtioPciCandidate[] candidates =
        [
            CreateCandidate(10, VirtioPciDeviceDiscovery.BlockDeviceId, bus: 2, device: 4),
            CreateCandidate(12, VirtioPciDeviceDiscovery.NetworkDeviceId, bus: 3, device: 5)
        ];
        VirtioPciConfigurationSource[] configurationSources =
        [
            CreateConfigurationSource(12, VirtioPciDeviceDiscovery.NetworkDeviceId),
            CreateConfigurationSource(10, VirtioPciDeviceDiscovery.BlockDeviceId)
        ];
        VirtioPciBarSizeSource[] barSizeSources =
        [
            CreateBarSizeSource(10),
            CreateBarSizeSource(12)
        ];
        VirtioPciBindingCatalogEntry[] destination = new VirtioPciBindingCatalogEntry[2];

        VirtioPciBindingCatalogResult result = VirtioPciBindingCatalog.Build(
            candidates,
            configurationSources,
            barSizeSources,
            destination);

        return HostCheck.Result(result.Succeeded
            && result.Status == VirtioPciBindingCatalogStatus.Success
            && result.InspectedCandidateCount == 2
            && result.BindingCount == 2
            && result.RequiredBindingCapacity == 2
            && result.FailingCandidateIndex == VirtioPciBindingCatalogResult.NoFailingCandidateIndex
            && result.FailingSourceIndex == VirtioPciBindingCatalogResult.NoFailingSourceIndex
            && destination[0].SourceIndex == 10
            && destination[0].Plan.Succeeded
            && destination[0].Binding.DeviceKind == VirtioPciDeviceKind.Block
            && destination[0].Bus == 2
            && destination[0].Device == 4
            && destination[1].SourceIndex == 12
            && destination[1].Plan.Succeeded
            && destination[1].Binding.DeviceKind == VirtioPciDeviceKind.Network
            && destination[1].Bus == 3
            && destination[1].Device == 5);
    }

    [HostTest("drivers.virtio.pci.catalog preserves source indexes", HostLane.Fast, 1)]
    private static bool TestPreservesSourceIndexes()
    {
        VirtioPciCandidate[] candidates =
        [
            CreateCandidate(
                sourceIndex: 44,
                virtioDeviceId: VirtioPciDeviceDiscovery.NetworkDeviceId,
                segmentIndex: 6,
                segmentGroup: 0x1234,
                ecamBasePhysicalAddress: 0x00000008_80000000UL,
                segmentStartBus: 0x20,
                segmentEndBus: 0x2F,
                bus: 0x22,
                device: 0x05,
                function: 0x06)
        ];
        VirtioPciConfigurationSource[] configurationSources =
        [
            CreateConfigurationSource(44, VirtioPciDeviceDiscovery.NetworkDeviceId)
        ];
        VirtioPciBarSizeSource[] barSizeSources =
        [
            CreateBarSizeSource(44)
        ];
        VirtioPciBindingCatalogEntry[] destination = new VirtioPciBindingCatalogEntry[1];

        VirtioPciBindingCatalogResult result = VirtioPciBindingCatalog.Build(
            candidates,
            configurationSources,
            barSizeSources,
            destination);
        VirtioPciBindingCatalogEntry entry = destination[0];

        return HostCheck.Result(result.Succeeded
            && result.BindingCount == 1
            && entry.SourceIndex == 44
            && entry.Candidate.SourceIndex == 44
            && entry.Plan.Succeeded
            && entry.SegmentIndex == 6
            && entry.SegmentGroup == 0x1234
            && entry.EcamBasePhysicalAddress == 0x00000008_80000000UL
            && entry.SegmentStartBus == 0x20
            && entry.SegmentEndBus == 0x2F
            && entry.Bus == 0x22
            && entry.Device == 0x05
            && entry.Function == 0x06
            && entry.Binding.Identity.DeviceId == 0x1041
            && entry.Binding.DeviceKind == VirtioPciDeviceKind.Network);
    }

    [HostTest("drivers.virtio.pci.catalog reports missing config source", HostLane.Fast, 2)]
    private static bool TestReportsMissingConfigSource()
    {
        VirtioPciCandidate[] candidates =
        [
            CreateCandidate(7, VirtioPciDeviceDiscovery.BlockDeviceId)
        ];
        VirtioPciBarSizeSource[] barSizeSources =
        [
            CreateBarSizeSource(7)
        ];
        VirtioPciBindingCatalogEntry[] destination = new VirtioPciBindingCatalogEntry[1];

        VirtioPciBindingCatalogResult result = VirtioPciBindingCatalog.Build(
            candidates,
            Array.Empty<VirtioPciConfigurationSource>(),
            barSizeSources,
            destination);

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioPciBindingCatalogStatus.MissingConfigurationSource
            && result.InspectedCandidateCount == 1
            && result.BindingCount == 0
            && result.RequiredBindingCapacity == 1
            && result.FailingCandidateIndex == 0
            && result.FailingSourceIndex == 7
            && result.FailingPlan.Status == default);
    }

    [HostTest("drivers.virtio.pci.catalog reports missing bar-size source", HostLane.Fast, 3)]
    private static bool TestReportsMissingBarSizeSource()
    {
        VirtioPciCandidate[] candidates =
        [
            CreateCandidate(8, VirtioPciDeviceDiscovery.BlockDeviceId)
        ];
        VirtioPciConfigurationSource[] configurationSources =
        [
            CreateConfigurationSource(8, VirtioPciDeviceDiscovery.BlockDeviceId)
        ];
        VirtioPciBindingCatalogEntry[] destination = new VirtioPciBindingCatalogEntry[1];

        VirtioPciBindingCatalogResult result = VirtioPciBindingCatalog.Build(
            candidates,
            configurationSources,
            Array.Empty<VirtioPciBarSizeSource>(),
            destination);

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioPciBindingCatalogStatus.MissingBarSizeSource
            && result.InspectedCandidateCount == 1
            && result.BindingCount == 0
            && result.RequiredBindingCapacity == 1
            && result.FailingCandidateIndex == 0
            && result.FailingSourceIndex == 8
            && result.FailingPlan.Status == default);
    }

    [HostTest("drivers.virtio.pci.catalog reports binding failure", HostLane.Fast, 4)]
    private static bool TestReportsBindingFailure()
    {
        VirtioPciCandidate[] candidates =
        [
            CreateCandidate(11, VirtioPciDeviceDiscovery.BlockDeviceId)
        ];
        VirtioPciConfigurationSource[] configurationSources =
        [
            new VirtioPciConfigurationSource(11, new byte[PciConfigurationHeader.Type0HeaderLength - 1])
        ];
        VirtioPciBarSizeSource[] barSizeSources =
        [
            CreateBarSizeSource(11)
        ];
        VirtioPciBindingCatalogEntry[] destination = new VirtioPciBindingCatalogEntry[1];

        VirtioPciBindingCatalogResult result = VirtioPciBindingCatalog.Build(
            candidates,
            configurationSources,
            barSizeSources,
            destination);

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioPciBindingCatalogStatus.BindingFailed
            && result.InspectedCandidateCount == 1
            && result.BindingCount == 0
            && result.RequiredBindingCapacity == 1
            && result.FailingCandidateIndex == 0
            && result.FailingSourceIndex == 11
            && result.FailingPlan.Status == VirtioPciBindingPlanStatus.HeaderParseFailed
            && result.FailingPlan.HeaderStatus == PciConfigurationHeaderStatus.BufferTooSmall);
    }

    [HostTest("drivers.virtio.pci.catalog reports destination exhaustion", HostLane.Fast, 5)]
    private static bool TestReportsDestinationExhaustion()
    {
        VirtioPciCandidate[] candidates =
        [
            CreateCandidate(20, VirtioPciDeviceDiscovery.BlockDeviceId, bus: 1, device: 1),
            CreateCandidate(21, VirtioPciDeviceDiscovery.NetworkDeviceId, bus: 1, device: 2)
        ];
        VirtioPciConfigurationSource[] configurationSources =
        [
            CreateConfigurationSource(20, VirtioPciDeviceDiscovery.BlockDeviceId),
            CreateConfigurationSource(21, VirtioPciDeviceDiscovery.NetworkDeviceId)
        ];
        VirtioPciBarSizeSource[] barSizeSources =
        [
            CreateBarSizeSource(20),
            CreateBarSizeSource(21)
        ];
        VirtioPciBindingCatalogEntry[] destination = new VirtioPciBindingCatalogEntry[1];

        VirtioPciBindingCatalogResult result = VirtioPciBindingCatalog.Build(
            candidates,
            configurationSources,
            barSizeSources,
            destination);

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioPciBindingCatalogStatus.DestinationTooSmall
            && result.InspectedCandidateCount == 2
            && result.BindingCount == 1
            && result.RequiredBindingCapacity == 2
            && result.FailingCandidateIndex == 1
            && result.FailingSourceIndex == 21
            && destination[0].SourceIndex == 20
            && destination[0].Plan.Succeeded
            && destination[0].Binding.DeviceKind == VirtioPciDeviceKind.Block);
    }

    [HostTest("drivers.virtio.pci.catalog default result is not success", HostLane.Fast, 6)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        VirtioPciBindingCatalogResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioPciBindingCatalogStatus.Success
            && result.BindingCount == 0
            && result.RequiredBindingCapacity == 0
            && result.FailingPlan.Status == default);
    }

    private static VirtioPciCandidate CreateCandidate(
        int sourceIndex,
        byte virtioDeviceId,
        int segmentIndex = 1,
        ushort segmentGroup = 7,
        ulong ecamBasePhysicalAddress = 0xE0000000UL,
        byte segmentStartBus = 0,
        byte segmentEndBus = 0x7F,
        byte bus = 2,
        byte device = 4,
        byte function = 1)
    {
        ushort deviceId = (ushort)(VirtioPciDeviceDiscovery.ModernDeviceIdStart + virtioDeviceId);
        VirtioPciDeviceKind deviceKind = virtioDeviceId == VirtioPciDeviceDiscovery.NetworkDeviceId
            ? VirtioPciDeviceKind.Network
            : VirtioPciDeviceKind.Block;

        return new VirtioPciCandidate(
            sourceIndex,
            segmentIndex,
            segmentGroup,
            ecamBasePhysicalAddress,
            segmentStartBus,
            segmentEndBus,
            bus,
            device,
            function,
            VirtioPciDeviceDiscovery.VirtioVendorId,
            deviceId,
            deviceKind,
            virtioDeviceId,
            CommonOffset);
    }

    private static VirtioPciConfigurationSource CreateConfigurationSource(
        int sourceIndex,
        byte virtioDeviceId)
    {
        ushort deviceId = (ushort)(VirtioPciDeviceDiscovery.ModernDeviceIdStart + virtioDeviceId);
        uint deviceConfigurationLength = virtioDeviceId == VirtioPciDeviceDiscovery.NetworkDeviceId
            ? VirtioPciDeviceDiscovery.NetworkDeviceConfigurationMinimumLength
            : VirtioPciDeviceDiscovery.BlockDeviceConfigurationMinimumLength;

        return new VirtioPciConfigurationSource(
            sourceIndex,
            CreateConfig(deviceId, deviceConfigurationLength));
    }

    private static VirtioPciBarSizeSource CreateBarSizeSource(int sourceIndex)
    {
        return new VirtioPciBarSizeSource(
            sourceIndex,
            new ulong[]
            {
                0x1000,
                0x1000,
                0x1000,
                0x1000,
                0,
                0
            });
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
}
