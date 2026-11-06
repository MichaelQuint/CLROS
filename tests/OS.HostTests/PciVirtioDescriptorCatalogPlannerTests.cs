using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;
using OS.Storage;

internal static partial class PciVirtioDescriptorCatalogPlannerTests
{
    private const byte CommonOffset = 0x40;
    private const byte NotifyOffset = 0x60;
    private const byte IsrOffset = 0x80;
    private const byte DeviceOffset = 0xA0;
    private const ulong UnsupportedDeviceFeature = 1UL << 42;
    [HostTest("drivers.pci virtio catalog composes block", HostLane.Fast, 0)]
    private static bool TestComposesBlock()
    {
        FakeConfigSpace configSpace = new FakeConfigSpace();
        configSpace.SetHeader(bus: 0, device: 4, function: 0, CreateHeader(ModernDeviceId(VirtioPciDeviceDiscovery.BlockDeviceId)));
        PciVirtioDescriptorCatalogPlanResult result = Plan(configSpace, new PciEcamSegment(0xE0000000UL, segmentGroup: 0, startBus: 0, endBus: 0), [CreateConfigurationSource(0, VirtioPciDeviceDiscovery.BlockDeviceId)], [new VirtioDeviceConfigurationSource(0, CreateBlockConfiguration(capacitySectors: 256))], [new PciVirtioDescriptorFeatureSource(0, VirtioDeviceInitializationProfile.BlockOptionalFeatureMask | UnsupportedDeviceFeature, availableQueueCount: 1, featuresOkAccepted: true)], CreateSuccessfulAccessor(), scannedCapacity: 2, candidateCapacity: 1, bindingCapacity: 1, descriptorSourceCapacity: 1, descriptorCapacity: 1, out VirtioDeviceDescriptor[] descriptors);
        VirtioDeviceDescriptor descriptor = descriptors[0];
        return HostCheck.Result(result.Succeeded && result.Status == PciVirtioDescriptorCatalogPlanStatus.Success && result.EcamScan.DeviceCount == 1 && result.CandidateSelection.CandidateCount == 1 && result.BindingCatalog.BindingCount == 1 && result.DescriptorSourceCount == 1 && result.DescriptorCatalog.DescriptorCount == 1 && descriptor.SourceIndex == 0 && descriptor.DeviceKind == VirtioPciDeviceKind.Block && descriptor.Binding.DeviceKind == VirtioPciDeviceKind.Block && descriptor.Binding.Bus == 0 && descriptor.Binding.Device == 4 && descriptor.HasBlockDeviceInfo && descriptor.BlockDeviceInfo.BlockCount == 256 && descriptor.BlockDeviceInfo.CapacityBytes == 256UL * VirtioBlockDeviceConfig.SectorSizeBytes && !descriptor.HasNetworkConfiguration && descriptor.InitializationProfile.Succeeded && descriptor.InitializationProfile.InitializationPlan.SelectedFeatures == VirtioDeviceInitializationProfile.BlockOptionalFeatureMask);
    }

    [HostTest("drivers.pci virtio catalog composes network", HostLane.Fast, 1)]
    private static bool TestComposesNetwork()
    {
        MacAddress macAddress = new MacAddress(0x02, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE);
        FakeConfigSpace configSpace = new FakeConfigSpace();
        configSpace.SetHeader(bus: 0, device: 5, function: 0, CreateHeader(ModernDeviceId(VirtioPciDeviceDiscovery.NetworkDeviceId)));
        FakePciConfigDwordAccessor accessor = CreateSuccessfulAccessor();
        accessor.SetReadback(device: 5, registerOffset: 0x10, value: 0xFFFFE000);
        PciVirtioDescriptorCatalogPlanResult result = Plan(configSpace, new PciEcamSegment(0xE0000000UL, segmentGroup: 0, startBus: 0, endBus: 0), [CreateConfigurationSource(0, VirtioPciDeviceDiscovery.NetworkDeviceId)], [new VirtioDeviceConfigurationSource(0, CreateNetworkConfiguration(macAddress, VirtioNetDeviceConfig.StatusLinkUp | VirtioNetDeviceConfig.StatusAnnounce))], [new PciVirtioDescriptorFeatureSource(0, VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask | UnsupportedDeviceFeature, availableQueueCount: 2, featuresOkAccepted: true)], accessor, scannedCapacity: 2, candidateCapacity: 1, bindingCapacity: 1, descriptorSourceCapacity: 1, descriptorCapacity: 1, out VirtioDeviceDescriptor[] descriptors);
        VirtioDeviceDescriptor descriptor = descriptors[0];
        return HostCheck.Result(result.Succeeded && descriptor.SourceIndex == 0 && descriptor.DeviceKind == VirtioPciDeviceKind.Network && descriptor.Binding.DeviceKind == VirtioPciDeviceKind.Network && descriptor.Binding.Device == 5 && descriptor.Binding.Apertures.CommonConfiguration.BarSize == 0x2000 && descriptor.HasNetworkConfiguration && descriptor.NetworkDeviceConfiguration.MacAddress == macAddress && descriptor.NetworkDeviceConfiguration.IsLinkUp && descriptor.NetworkDeviceConfiguration.HasAnnounce && !descriptor.HasBlockDeviceInfo && descriptor.InitializationProfile.Succeeded && descriptor.InitializationProfile.InitializationPlan.SelectedFeatures == VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask);
    }

    [HostTest("drivers.pci virtio catalog reports capacity", HostLane.Fast, 2)]
    private static bool TestReportsDescriptorSourceCapacity()
    {
        FakeConfigSpace configSpace = new FakeConfigSpace();
        configSpace.SetHeader(bus: 0, device: 4, function: 0, CreateHeader(ModernDeviceId(VirtioPciDeviceDiscovery.BlockDeviceId)));
        configSpace.SetHeader(bus: 0, device: 5, function: 0, CreateHeader(ModernDeviceId(VirtioPciDeviceDiscovery.NetworkDeviceId)));
        PciVirtioDescriptorCatalogPlanResult result = Plan(configSpace, new PciEcamSegment(0xE0000000UL, segmentGroup: 0, startBus: 0, endBus: 0), [CreateConfigurationSource(0, VirtioPciDeviceDiscovery.BlockDeviceId), CreateConfigurationSource(1, VirtioPciDeviceDiscovery.NetworkDeviceId)], [new VirtioDeviceConfigurationSource(0, CreateBlockConfiguration(capacitySectors: 32)), new VirtioDeviceConfigurationSource(1, CreateNetworkConfiguration(new MacAddress(0x02, 0x10, 0x20, 0x30, 0x40, 0x50), VirtioNetDeviceConfig.StatusLinkUp))], [new PciVirtioDescriptorFeatureSource(0, VirtioDeviceInitializationProfile.BlockOptionalFeatureMask, availableQueueCount: 1, featuresOkAccepted: true), new PciVirtioDescriptorFeatureSource(1, VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask, availableQueueCount: 2, featuresOkAccepted: true)], CreateSuccessfulAccessor(), scannedCapacity: 4, candidateCapacity: 2, bindingCapacity: 2, descriptorSourceCapacity: 1, descriptorCapacity: 2, out _);
        return HostCheck.Result(!result.Succeeded && result.Status == PciVirtioDescriptorCatalogPlanStatus.DescriptorSourceDestinationTooSmall && result.EcamScan.DeviceCount == 2 && result.CandidateSelection.CandidateCount == 2 && result.BindingCatalog.BindingCount == 2 && result.RequiredDescriptorSourceCapacity == 2 && result.AvailableDescriptorSourceCapacity == 1 && result.DescriptorSourceCount == 0 && result.DescriptorCatalog.Status == default);
    }

    [HostTest("drivers.pci virtio catalog preserves fake provenance", HostLane.Fast, 3)]
    private static bool TestPreservesFakeProvenance()
    {
        FakeConfigSpace configSpace = new FakeConfigSpace();
        configSpace.SetHeader(bus: 0x20, device: 5, function: 0, CreateHeader(ModernDeviceId(VirtioPciDeviceDiscovery.NetworkDeviceId)));
        PciVirtioDescriptorCatalogPlanResult result = Plan(configSpace, new PciEcamSegment(basePhysicalAddress: 0x00000008_80000000UL, segmentGroup: 0x1234, startBus: 0x20, endBus: 0x20), [CreateConfigurationSource(0, VirtioPciDeviceDiscovery.NetworkDeviceId)], [new VirtioDeviceConfigurationSource(0, CreateNetworkConfiguration(new MacAddress(0x02, 0x21, 0x22, 0x23, 0x24, 0x25), VirtioNetDeviceConfig.StatusLinkUp))], [new PciVirtioDescriptorFeatureSource(0, VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask, availableQueueCount: 2, featuresOkAccepted: true)], CreateSuccessfulAccessor(), scannedCapacity: 1, candidateCapacity: 1, bindingCapacity: 1, descriptorSourceCapacity: 1, descriptorCapacity: 1, out VirtioDeviceDescriptor[] descriptors);
        VirtioDeviceDescriptor descriptor = descriptors[0];
        return HostCheck.Result(result.Succeeded && descriptor.Binding.SegmentIndex == 0 && descriptor.Binding.SegmentGroup == 0x1234 && descriptor.Binding.EcamBasePhysicalAddress == 0x00000008_80000000UL && descriptor.Binding.SegmentStartBus == 0x20 && descriptor.Binding.SegmentEndBus == 0x20 && descriptor.Binding.Bus == 0x20 && descriptor.Binding.Device == 5 && descriptor.Binding.Function == 0 && descriptor.Binding.CapabilityPointer == CommonOffset && PciVirtioDescriptorCatalogPlanner.UsesInjectedEcamReader && PciVirtioDescriptorCatalogPlanner.UsesCandidateSelector && PciVirtioDescriptorCatalogPlanner.UsesFakeDerivedBarSizing && PciVirtioDescriptorCatalogPlanner.UsesDescriptorCatalog && !PciVirtioDescriptorCatalogPlanner.PerformsLivePciEnumeration && !PciVirtioDescriptorCatalogPlanner.PerformsLiveConfigReads && !PciVirtioDescriptorCatalogPlanner.PerformsLiveConfigWrites && !PciVirtioDescriptorCatalogPlanner.MapsEcam && !PciVirtioDescriptorCatalogPlanner.MapsBars && !PciVirtioDescriptorCatalogPlanner.PerformsMmio && !PciVirtioDescriptorCatalogPlanner.AllocatesDmaMemory && !PciVirtioDescriptorCatalogPlanner.PerformsDmaMapping && !PciVirtioDescriptorCatalogPlanner.ConfiguresQueues && !PciVirtioDescriptorCatalogPlanner.BindsLiveDriver && !PciVirtioDescriptorCatalogPlanner.PerformsPacketIo && !PciVirtioDescriptorCatalogPlanner.PerformsBlockIo && !RuntimeHelpers.IsReferenceOrContainsReferences<PciVirtioDescriptorCatalogPlanResult>() && !RuntimeHelpers.IsReferenceOrContainsReferences<PciVirtioDescriptorFeatureSource>());
    }

    [HostTest("drivers.pci virtio catalog skips unsupported candidate", HostLane.Fast, 4)]
    private static bool TestSkipsUnsupportedCandidate()
    {
        FakeConfigSpace configSpace = new FakeConfigSpace();
        configSpace.SetHeader(bus: 0, device: 1, function: 0, CreateHeader(vendorId: 0x1234, deviceId: 0x0001));
        configSpace.SetHeader(bus: 0, device: 4, function: 0, CreateHeader(ModernDeviceId(VirtioPciDeviceDiscovery.BlockDeviceId)));
        PciVirtioDescriptorCatalogPlanResult result = Plan(configSpace, new PciEcamSegment(0xE0000000UL, segmentGroup: 0, startBus: 0, endBus: 0), [CreateConfigurationSource(1, VirtioPciDeviceDiscovery.BlockDeviceId)], [new VirtioDeviceConfigurationSource(1, CreateBlockConfiguration(capacitySectors: 8))], [new PciVirtioDescriptorFeatureSource(1, VirtioDeviceInitializationProfile.BlockOptionalFeatureMask, availableQueueCount: 1, featuresOkAccepted: true)], CreateSuccessfulAccessor(), scannedCapacity: 4, candidateCapacity: 1, bindingCapacity: 1, descriptorSourceCapacity: 1, descriptorCapacity: 1, out VirtioDeviceDescriptor[] descriptors);
        return HostCheck.Result(result.Succeeded && result.EcamScan.DeviceCount == 2 && result.CandidateSelection.InspectedDeviceCount == 2 && result.CandidateSelection.CandidateCount == 1 && descriptors[0].SourceIndex == 1 && descriptors[0].DeviceKind == VirtioPciDeviceKind.Block);
    }

    [HostTest("drivers.pci virtio catalog propagates nested scan failure", HostLane.Fast, 5)]
    private static bool TestPropagatesNestedScanFailure()
    {
        FakeConfigSpace configSpace = new FakeConfigSpace();
        configSpace.FailRead(bus: 0, device: 0, function: 0, PciConfigurationSpaceReadStatus.ReadFailed);
        PciVirtioDescriptorCatalogPlanResult result = Plan(configSpace, new PciEcamSegment(0xE0000000UL, segmentGroup: 0, startBus: 0, endBus: 0), Array.Empty<VirtioPciConfigurationSource>(), Array.Empty<VirtioDeviceConfigurationSource>(), Array.Empty<PciVirtioDescriptorFeatureSource>(), CreateSuccessfulAccessor(), scannedCapacity: 1, candidateCapacity: 1, bindingCapacity: 1, descriptorSourceCapacity: 1, descriptorCapacity: 1, out _);
        return HostCheck.Result(!result.Succeeded && result.Status == PciVirtioDescriptorCatalogPlanStatus.EcamScanFailed && result.EcamScan.Status == PciEcamSegmentScanStatus.NestedScannerFailed && result.EcamScan.NestedResult.Status == PciBusScanStatus.ReaderFailed && result.EcamScan.NestedResult.ReaderStatus == PciConfigurationSpaceReadStatus.ReadFailed && result.CandidateSelection.Status == default && result.BindingCatalog.Status == default && result.DescriptorCatalog.Status == default);
    }

    [HostTest("drivers.pci virtio catalog propagates binding failure", HostLane.Fast, 6)]
    private static bool TestPropagatesBindingFailure()
    {
        FakeConfigSpace configSpace = new FakeConfigSpace();
        configSpace.SetHeader(bus: 0, device: 5, function: 0, CreateHeader(ModernDeviceId(VirtioPciDeviceDiscovery.NetworkDeviceId)));
        byte[] invalidConfiguration = CreatePciConfiguration(ModernDeviceId(VirtioPciDeviceDiscovery.NetworkDeviceId), VirtioPciDeviceDiscovery.NetworkDeviceConfigurationMinimumLength);
        invalidConfiguration[CommonOffset + 2] = 8;
        PciVirtioDescriptorCatalogPlanResult result = Plan(configSpace, new PciEcamSegment(0xE0000000UL, segmentGroup: 0, startBus: 0, endBus: 0), [new VirtioPciConfigurationSource(0, invalidConfiguration)], [new VirtioDeviceConfigurationSource(0, CreateNetworkConfiguration(new MacAddress(0x02, 0x31, 0x32, 0x33, 0x34, 0x35), VirtioNetDeviceConfig.StatusLinkUp))], [new PciVirtioDescriptorFeatureSource(0, VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask, availableQueueCount: 2, featuresOkAccepted: true)], CreateSuccessfulAccessor(), scannedCapacity: 1, candidateCapacity: 1, bindingCapacity: 1, descriptorSourceCapacity: 1, descriptorCapacity: 1, out _);
        return HostCheck.Result(!result.Succeeded && result.Status == PciVirtioDescriptorCatalogPlanStatus.DerivedBindingCatalogFailed && result.CandidateSelection.CandidateCount == 1 && result.BindingCatalog.Status == VirtioPciBindingDerivedBarSizeCatalogStatus.DerivedBindingFailed && result.BindingCatalog.FailingCandidateIndex == 0 && result.BindingCatalog.FailingSourceIndex == 0 && result.BindingCatalog.FailingPlan.Status == VirtioPciBindingDerivedBarSizePlanStatus.BindingFailed && result.BindingCatalog.FailingPlan.BindingStatus == VirtioPciBindingPlanStatus.CapabilityParseFailed && result.DescriptorCatalog.Status == default);
    }

    [HostTest("drivers.pci virtio catalog default result is not success", HostLane.Fast, 7)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        PciVirtioDescriptorCatalogPlanResult result = default;
        return HostCheck.Result(!result.IsInitialized && !result.Succeeded && result.Status == PciVirtioDescriptorCatalogPlanStatus.Success && result.EcamScan.Status == default && result.CandidateSelection.Status == default && result.BindingCatalog.Status == default && result.DescriptorCatalog.Status == default && result.DescriptorSourceCount == 0 && result.RequiredDescriptorSourceCapacity == 0 && result.AvailableDescriptorSourceCapacity == 0 && result.FailingSourceIndex == 0 && result.FailingInputIndex == 0 && !RuntimeHelpers.IsReferenceOrContainsReferences<PciVirtioDescriptorCatalogPlanResult>());
    }

    private static PciVirtioDescriptorCatalogPlanResult Plan(FakeConfigSpace configSpace, PciEcamSegment segment, VirtioPciConfigurationSource[] pciConfigurationSources, VirtioDeviceConfigurationSource[] deviceConfigurationSources, PciVirtioDescriptorFeatureSource[] featureSources, IPciConfigDwordAccessor accessor, int scannedCapacity, int candidateCapacity, int bindingCapacity, int descriptorSourceCapacity, int descriptorCapacity, out VirtioDeviceDescriptor[] descriptors)
    {
        PciEcamSegment[] segments = [segment];
        PciEcamScannedDevice[] scannedDevices = new PciEcamScannedDevice[scannedCapacity];
        PciScannedDevice[] scanScratch = new PciScannedDevice[scannedDevices.Length];
        VirtioPciCandidate[] candidates = new VirtioPciCandidate[candidateCapacity];
        PciBarSizingProbeAddressCommand[] commands = new PciBarSizingProbeAddressCommand[PciBarSizingProbePlanner.MaximumCommandCount];
        uint[] readbacks = new uint[PciBarSizingProbeExecutor.RequiredReadbackCount];
        ulong[] barSizes = new ulong[PciBarDecoder.BarCount];
        VirtioPciBindingDerivedBarSizeCatalogEntry[] bindings = new VirtioPciBindingDerivedBarSizeCatalogEntry[bindingCapacity];
        VirtioDeviceDescriptorDerivedBindingSource[] descriptorSources = new VirtioDeviceDescriptorDerivedBindingSource[descriptorSourceCapacity];
        descriptors = new VirtioDeviceDescriptor[descriptorCapacity];
        return PciVirtioDescriptorCatalogPlanner.Plan(segments, configSpace.Read, pciConfigurationSources, deviceConfigurationSources, featureSources, accessor, scannedDevices, scanScratch, candidates, commands, readbacks, barSizes, bindings, descriptorSources, descriptors);
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

    private static VirtioPciConfigurationSource CreateConfigurationSource(int sourceIndex, byte virtioDeviceId)
    {
        uint deviceConfigurationLength = virtioDeviceId == VirtioPciDeviceDiscovery.NetworkDeviceId ? VirtioPciDeviceDiscovery.NetworkDeviceConfigurationMinimumLength : VirtioPciDeviceDiscovery.BlockDeviceConfigurationMinimumLength;
        return new VirtioPciConfigurationSource(sourceIndex, CreatePciConfiguration(ModernDeviceId(virtioDeviceId), deviceConfigurationLength));
    }

    private static byte[] CreateHeader(ushort deviceId)
    {
        return CreateHeader(VirtioPciDeviceDiscovery.VirtioVendorId, deviceId);
    }

    private static byte[] CreateHeader(ushort vendorId, ushort deviceId)
    {
        byte[] headerBytes = new byte[PciConfigurationHeader.Type0HeaderLength];
        EndianPrimitives.WriteUInt16LittleEndian(headerBytes.AsSpan(0x00, sizeof(ushort)), vendorId);
        EndianPrimitives.WriteUInt16LittleEndian(headerBytes.AsSpan(0x02, sizeof(ushort)), deviceId);
        EndianPrimitives.WriteUInt16LittleEndian(headerBytes.AsSpan(0x06, sizeof(ushort)), PciConfigurationHeader.StatusCapabilitiesListBit);
        headerBytes[0x0E] = PciConfigurationHeader.Type0HeaderType;
        headerBytes[0x34] = CommonOffset;
        return headerBytes;
    }

    private static ushort ModernDeviceId(byte virtioDeviceId)
    {
        return (ushort)(VirtioPciDeviceDiscovery.ModernDeviceIdStart + virtioDeviceId);
    }

    private static byte[] CreatePciConfiguration(ushort deviceId, uint deviceConfigurationLength, uint bar0 = 0x80000000, uint bar1 = 0x80001000, uint bar2 = 0x80002000, uint bar3 = 0x80003000)
    {
        byte[] config = new byte[256];
        EndianPrimitives.WriteUInt16LittleEndian(config.AsSpan(0x00, sizeof(ushort)), VirtioPciDeviceDiscovery.VirtioVendorId);
        EndianPrimitives.WriteUInt16LittleEndian(config.AsSpan(0x02, sizeof(ushort)), deviceId);
        EndianPrimitives.WriteUInt16LittleEndian(config.AsSpan(0x06, sizeof(ushort)), PciConfigurationHeader.StatusCapabilitiesListBit);
        config[0x0E] = PciConfigurationHeader.Type0HeaderType;
        WriteUInt32(config, 0x10, bar0);
        WriteUInt32(config, 0x14, bar1);
        WriteUInt32(config, 0x18, bar2);
        WriteUInt32(config, 0x1C, bar3);
        config[0x34] = CommonOffset;
        WriteVirtioCapability(config, CommonOffset, NotifyOffset, VirtioPciCapabilityType.CommonConfiguration, bar: 0, offset: 0x20, VirtioPciDeviceDiscovery.CommonConfigurationMinimumLength, notifyOffsetMultiplier: 0, capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);
        WriteVirtioCapability(config, NotifyOffset, IsrOffset, VirtioPciCapabilityType.NotifyConfiguration, bar: 1, offset: 0x80, length: 0x100, notifyOffsetMultiplier: 4, capabilityLength: VirtioPciCapabilityListParser.ModernVirtioNotifyCapabilityLength);
        WriteVirtioCapability(config, IsrOffset, DeviceOffset, VirtioPciCapabilityType.IsrStatus, bar: 2, offset: 0, VirtioPciDeviceDiscovery.IsrStatusMinimumLength, notifyOffsetMultiplier: 0, capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);
        WriteVirtioCapability(config, DeviceOffset, nextOffset: 0, VirtioPciCapabilityType.DeviceConfiguration, bar: 3, offset: 0x40, deviceConfigurationLength, notifyOffsetMultiplier: 0, capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);
        return config;
    }

    private static byte[] CreateBlockConfiguration(ulong capacitySectors)
    {
        byte[] config = new byte[VirtioBlockDeviceConfig.CapacityLength];
        EndianPrimitives.WriteUInt64LittleEndian(config, capacitySectors);
        return config;
    }

    private static byte[] CreateNetworkConfiguration(MacAddress macAddress, ushort statusBits)
    {
        byte[] config = new byte[VirtioNetDeviceConfig.StatusConfigLength];
        macAddress.TryWrite(config, VirtioNetDeviceConfig.MacAddressOffset);
        EndianPrimitives.WriteUInt16LittleEndian(config.AsSpan(VirtioNetDeviceConfig.StatusOffset, VirtioNetDeviceConfig.StatusLength), statusBits);
        return config;
    }

    private static void WriteVirtioCapability(Span<byte> config, byte capabilityOffset, byte nextOffset, VirtioPciCapabilityType type, byte bar, uint offset, uint length, uint notifyOffsetMultiplier, byte capabilityLength)
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
