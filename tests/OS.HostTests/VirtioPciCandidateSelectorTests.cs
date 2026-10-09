using System.Runtime.CompilerServices;
using OS.Drivers;

internal static class VirtioPciCandidateSelectorTests
{

    [HostTest("drivers.virtio.pci.candidate selects block and network devices", HostLane.Fast, 0)]
    private static bool TestSelectsBlockAndNetworkDevices()
    {
        PciEcamScannedDevice[] devices =
        [
            CreateDevice(0, 0, 1, 0, 0x1234, 0x1042, hasCapabilitiesList: true, capabilityPointer: 0x40),
            CreateDevice(0, 0, 2, 0, VirtioPciDeviceDiscovery.VirtioVendorId, 0x1042, hasCapabilitiesList: true, capabilityPointer: 0x40),
            CreateDevice(0, 0, 3, 0, VirtioPciDeviceDiscovery.VirtioVendorId, 0x1041, hasCapabilitiesList: true, capabilityPointer: 0x44),
            CreateDevice(0, 0, 4, 0, VirtioPciDeviceDiscovery.VirtioVendorId, 0x1045, hasCapabilitiesList: true, capabilityPointer: 0x48)
        ];
        VirtioPciCandidate[] candidates = new VirtioPciCandidate[2];

        VirtioPciCandidateSelectionResult result = VirtioPciCandidateSelector.Select(devices, candidates);

        return HostCheck.Result(result.Succeeded
            && result.IsInitialized
            && result.Status == VirtioPciCandidateSelectionStatus.Success
            && result.InspectedDeviceCount == devices.Length
            && result.CandidateCount == 2
            && result.RequiredCandidateCapacity == 2
            && result.FailingSourceIndex == VirtioPciCandidateSelectionResult.NoFailingSourceIndex
            && candidates[0].DeviceKind == VirtioPciDeviceKind.Block
            && candidates[0].VirtioDeviceId == VirtioPciDeviceDiscovery.BlockDeviceId
            && candidates[0].SourceIndex == 1
            && candidates[1].DeviceKind == VirtioPciDeviceKind.Network
            && candidates[1].VirtioDeviceId == VirtioPciDeviceDiscovery.NetworkDeviceId
            && candidates[1].SourceIndex == 2);
    }

    [HostTest("drivers.virtio.pci.candidate preserves pci location", HostLane.Fast, 1)]
    private static bool TestPreservesPciLocation()
    {
        PciEcamScannedDevice[] devices =
        [
            CreateDevice(
                segmentIndex: 3,
                segmentGroup: 0x1234,
                bus: 0x22,
                device: 0x05,
                function: 0x06,
                vendorId: VirtioPciDeviceDiscovery.VirtioVendorId,
                deviceId: 0x1041,
                hasCapabilitiesList: true,
                capabilityPointer: 0x80,
                ecamBasePhysicalAddress: 0x00000008_80000000,
                segmentStartBus: 0x20,
                segmentEndBus: 0x2F)
        ];
        VirtioPciCandidate[] candidates = new VirtioPciCandidate[1];

        VirtioPciCandidateSelectionResult result = VirtioPciCandidateSelector.Select(devices, candidates);
        VirtioPciCandidate candidate = candidates[0];

        return HostCheck.Result(result.Succeeded
            && result.CandidateCount == 1
            && candidate.SourceIndex == 0
            && candidate.SegmentIndex == 3
            && candidate.SegmentGroup == 0x1234
            && candidate.EcamBasePhysicalAddress == 0x00000008_80000000UL
            && candidate.SegmentStartBus == 0x20
            && candidate.SegmentEndBus == 0x2F
            && candidate.Bus == 0x22
            && candidate.Device == 0x05
            && candidate.Function == 0x06
            && candidate.VendorId == VirtioPciDeviceDiscovery.VirtioVendorId
            && candidate.DeviceId == 0x1041
            && candidate.DeviceKind == VirtioPciDeviceKind.Network
            && candidate.CapabilityPointer == 0x80);
    }

    [HostTest("drivers.virtio.pci.candidate skips unsupported vendor", HostLane.Fast, 2)]
    private static bool TestSkipsUnsupportedVendor()
    {
        PciEcamScannedDevice[] devices =
        [
            CreateDevice(0, 0, 1, 0, 0x1234, 0x1042, hasCapabilitiesList: true, capabilityPointer: 0x40)
        ];
        VirtioPciCandidate[] candidates = new VirtioPciCandidate[1];

        VirtioPciCandidateSelectionResult result = VirtioPciCandidateSelector.Select(devices, candidates);

        return HostCheck.Result(result.Succeeded
            && result.CandidateCount == 0
            && result.RequiredCandidateCapacity == 0);
    }

    [HostTest("drivers.virtio.pci.candidate skips unsupported virtio device id", HostLane.Fast, 3)]
    private static bool TestSkipsUnsupportedVirtioDeviceId()
    {
        PciEcamScannedDevice[] devices =
        [
            CreateDevice(0, 0, 1, 0, VirtioPciDeviceDiscovery.VirtioVendorId, 0x1040, hasCapabilitiesList: true, capabilityPointer: 0x40),
            CreateDevice(0, 0, 2, 0, VirtioPciDeviceDiscovery.VirtioVendorId, 0x1045, hasCapabilitiesList: true, capabilityPointer: 0x44),
            CreateDevice(0, 0, 3, 0, VirtioPciDeviceDiscovery.VirtioVendorId, 0x1002, hasCapabilitiesList: true, capabilityPointer: 0x48)
        ];
        VirtioPciCandidate[] candidates = new VirtioPciCandidate[1];

        VirtioPciCandidateSelectionResult result = VirtioPciCandidateSelector.Select(devices, candidates);

        return HostCheck.Result(result.Succeeded
            && result.CandidateCount == 0
            && result.RequiredCandidateCapacity == 0);
    }

    [HostTest("drivers.virtio.pci.candidate requires capability pointer", HostLane.Fast, 4)]
    private static bool TestRequiresCapabilityPointer()
    {
        PciEcamScannedDevice[] devices =
        [
            CreateDevice(0, 0, 1, 0, VirtioPciDeviceDiscovery.VirtioVendorId, 0x1042, hasCapabilitiesList: false, capabilityPointer: 0x40),
            CreateDevice(0, 0, 2, 0, VirtioPciDeviceDiscovery.VirtioVendorId, 0x1042, hasCapabilitiesList: true, capabilityPointer: 0),
            CreateDevice(0, 0, 3, 0, VirtioPciDeviceDiscovery.VirtioVendorId, 0x1042, hasCapabilitiesList: true, capabilityPointer: 0x40)
        ];
        VirtioPciCandidate[] candidates = new VirtioPciCandidate[1];

        VirtioPciCandidateSelectionResult result = VirtioPciCandidateSelector.Select(devices, candidates);

        return HostCheck.Result(result.Succeeded
            && result.CandidateCount == 1
            && result.RequiredCandidateCapacity == 1
            && candidates[0].SourceIndex == 2
            && candidates[0].CapabilityPointer == 0x40);
    }

    [HostTest("drivers.virtio.pci.candidate reports destination exhaustion", HostLane.Fast, 5)]
    private static bool TestReportsDestinationExhaustion()
    {
        PciEcamScannedDevice[] devices =
        [
            CreateDevice(0, 0, 1, 0, VirtioPciDeviceDiscovery.VirtioVendorId, 0x1042, hasCapabilitiesList: true, capabilityPointer: 0x40),
            CreateDevice(0, 0, 2, 0, VirtioPciDeviceDiscovery.VirtioVendorId, 0x1041, hasCapabilitiesList: true, capabilityPointer: 0x44),
            CreateDevice(0, 0, 3, 0, VirtioPciDeviceDiscovery.VirtioVendorId, 0x1042, hasCapabilitiesList: true, capabilityPointer: 0x48)
        ];
        VirtioPciCandidate[] candidates = new VirtioPciCandidate[2];

        VirtioPciCandidateSelectionResult result = VirtioPciCandidateSelector.Select(devices, candidates);

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioPciCandidateSelectionStatus.DestinationTooSmall
            && result.InspectedDeviceCount == devices.Length
            && result.CandidateCount == 2
            && result.RequiredCandidateCapacity == 3
            && result.FailingSourceIndex == 2
            && candidates[0].SourceIndex == 0
            && candidates[0].DeviceKind == VirtioPciDeviceKind.Block
            && candidates[1].SourceIndex == 1
            && candidates[1].DeviceKind == VirtioPciDeviceKind.Network);
    }

    [HostTest("drivers.virtio.pci.candidate default result is not success", HostLane.Fast, 6)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        VirtioPciCandidateSelectionResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && result.Status == VirtioPciCandidateSelectionStatus.Success
            && !result.Succeeded
            && result.FailingSourceIndex == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioPciCandidateSelectionResult>());
    }

    private static PciEcamScannedDevice CreateDevice(
        int segmentIndex,
        ushort segmentGroup,
        byte bus,
        byte device,
        ushort vendorId,
        ushort deviceId,
        bool hasCapabilitiesList,
        byte capabilityPointer,
        byte function = 0,
        ulong ecamBasePhysicalAddress = 0xE0000000,
        byte segmentStartBus = 0,
        byte segmentEndBus = 0xFF)
    {
        return new PciEcamScannedDevice(
            segmentIndex,
            new PciEcamSegment(
                ecamBasePhysicalAddress,
                segmentGroup,
                segmentStartBus,
                segmentEndBus),
            new PciScannedDevice(
                bus,
                device,
                function,
                vendorId,
                deviceId,
                revisionId: 0,
                classCode: 0,
                subclass: 0,
                programmingInterface: 0,
                headerType: PciConfigurationHeader.Type0HeaderType,
                isMultifunction: false,
                hasCapabilitiesList,
                capabilityPointer));
    }
}
