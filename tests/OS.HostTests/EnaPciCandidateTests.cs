using System.Runtime.CompilerServices;
using OS.Drivers;

internal static class EnaPciCandidateTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("drivers.ena.pci.candidate recognizes ena vf devices", TestRecognizesEnaVfDevices, ref failures);
        Run("drivers.ena.pci.candidate rejects unsupported vendor device", TestRejectsUnsupportedVendorDevice, ref failures);
        Run("drivers.ena.pci.candidate rejects invalid pci shape", TestRejectsInvalidPciShape, ref failures);
        Run("drivers.ena.pci.candidate preserves pci source metadata", TestPreservesPciSourceMetadata, ref failures);
        Run("drivers.ena.pci.candidate reports destination exhaustion", TestReportsDestinationExhaustion, ref failures);
        Run("drivers.ena.pci.candidate rejects default results", TestRejectsDefaultResults, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("ENA PCI candidate host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestRecognizesEnaVfDevices()
    {
        PciEcamScannedDevice[] devices =
        [
            CreateDevice(
                vendorId: EnaPciCandidateSelector.AmazonVendorId,
                deviceId: EnaPciCandidateSelector.VirtualFunctionDeviceId,
                bus: 0,
                device: 1,
                function: 0,
                capabilityPointer: 0x40),
            CreateDevice(
                vendorId: EnaPciCandidateSelector.AmazonVendorId,
                deviceId: EnaPciCandidateSelector.LowLatencyQueueVirtualFunctionDeviceId,
                bus: 0,
                device: 2,
                function: 0,
                capabilityPointer: 0x44),
            CreateDevice(
                vendorId: EnaPciCandidateSelector.AmazonVendorId,
                deviceId: EnaPciCandidateSelector.PhysicalFunctionDeviceId,
                bus: 0,
                device: 3,
                function: 0,
                capabilityPointer: 0x48)
        ];
        EnaPciCandidate[] candidates = new EnaPciCandidate[2];

        EnaPciCandidateValidationResult vfValidation =
            EnaPciCandidateSelector.Validate(devices[0]);
        EnaPciCandidateValidationResult llqVfValidation =
            EnaPciCandidateSelector.Validate(devices[1]);
        EnaPciCandidateValidationResult pfValidation =
            EnaPciCandidateSelector.Validate(devices[2]);
        EnaPciCandidateSelectionResult selection = EnaPciCandidateSelector.Select(devices, candidates);

        return EnaPciCandidateSelector.AmazonVendorId == 0x1D0F
            && EnaPciCandidateSelector.VirtualFunctionDeviceId == 0xEC20
            && EnaPciCandidateSelector.LowLatencyQueueVirtualFunctionDeviceId == 0xEC21
            && EnaPciCandidateSelector.NetworkControllerClassCode == 0x02
            && EnaPciCandidateSelector.EthernetControllerSubclass == 0x00
            && EnaPciCandidateSelector.EthernetProgrammingInterface == 0x00
            && vfValidation.Succeeded
            && vfValidation.DeviceKind == EnaPciDeviceKind.VirtualFunction
            && llqVfValidation.Succeeded
            && llqVfValidation.DeviceKind == EnaPciDeviceKind.LowLatencyQueueVirtualFunction
            && !pfValidation.Succeeded
            && pfValidation.Status == EnaPciCandidateValidationStatus.UnsupportedDeviceId
            && selection.Succeeded
            && selection.Status == EnaPciCandidateSelectionStatus.Success
            && selection.InspectedDeviceCount == devices.Length
            && selection.CandidateCount == 2
            && selection.RequiredCandidateCapacity == 2
            && selection.FailingSourceIndex == EnaPciCandidateSelectionResult.NoFailingSourceIndex
            && candidates[0].DeviceKind == EnaPciDeviceKind.VirtualFunction
            && candidates[0].SourceIndex == 0
            && candidates[1].DeviceKind == EnaPciDeviceKind.LowLatencyQueueVirtualFunction
            && candidates[1].SourceIndex == 1;
    }

    private static bool TestRejectsDefaultResults()
    {
        EnaPciCandidateValidationResult validation = default;
        EnaPciCandidateSelectionResult selection = default;

        return !validation.IsInitialized
            && !validation.Succeeded
            && validation.Status == EnaPciCandidateValidationStatus.Success
            && !selection.IsInitialized
            && !selection.Succeeded
            && selection.Status == EnaPciCandidateSelectionStatus.Success
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                EnaPciCandidateValidationResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                EnaPciCandidateSelectionResult>();
    }

    private static bool TestRejectsUnsupportedVendorDevice()
    {
        PciEcamScannedDevice nonAmazon = CreateDevice(
            vendorId: 0x1AF4,
            deviceId: EnaPciCandidateSelector.VirtualFunctionDeviceId);
        PciEcamScannedDevice unknownAmazonDevice = CreateDevice(
            vendorId: EnaPciCandidateSelector.AmazonVendorId,
            deviceId: 0x1234);
        PciEcamScannedDevice reservedAmazonDevice = CreateDevice(
            vendorId: EnaPciCandidateSelector.AmazonVendorId,
            deviceId: EnaPciCandidateSelector.ReservedDeviceId);
        PciEcamScannedDevice[] devices = [nonAmazon, unknownAmazonDevice, reservedAmazonDevice];
        EnaPciCandidate[] candidates = new EnaPciCandidate[1];

        EnaPciCandidateValidationResult nonAmazonResult =
            EnaPciCandidateSelector.Validate(nonAmazon);
        EnaPciCandidateValidationResult unknownDeviceResult =
            EnaPciCandidateSelector.Validate(unknownAmazonDevice);
        EnaPciCandidateValidationResult reservedDeviceResult =
            EnaPciCandidateSelector.Validate(reservedAmazonDevice);
        EnaPciCandidateSelectionResult selection = EnaPciCandidateSelector.Select(devices, candidates);

        return !nonAmazonResult.Succeeded
            && nonAmazonResult.Status == EnaPciCandidateValidationStatus.UnsupportedVendor
            && !unknownDeviceResult.Succeeded
            && unknownDeviceResult.Status == EnaPciCandidateValidationStatus.UnsupportedDeviceId
            && !reservedDeviceResult.Succeeded
            && reservedDeviceResult.Status == EnaPciCandidateValidationStatus.UnsupportedDeviceId
            && selection.Succeeded
            && selection.CandidateCount == 0
            && selection.RequiredCandidateCapacity == 0;
    }

    private static bool TestRejectsInvalidPciShape()
    {
        PciEcamScannedDevice badHeader = CreateDevice(
            headerType: 0x01);
        PciEcamScannedDevice badClass = CreateDevice(
            classCode: 0x01);
        PciEcamScannedDevice badSubclass = CreateDevice(
            subclass: 0x80);
        PciEcamScannedDevice badProgrammingInterface = CreateDevice(
            programmingInterface: 0x01);
        PciEcamScannedDevice missingCapabilities = CreateDevice(
            hasCapabilitiesList: false,
            capabilityPointer: 0x40);
        PciEcamScannedDevice invalidCapabilityPointer = CreateDevice(
            capabilityPointer: 0x41);

        EnaPciCandidateValidationResult badHeaderResult =
            EnaPciCandidateSelector.Validate(badHeader);
        EnaPciCandidateValidationResult badClassResult =
            EnaPciCandidateSelector.Validate(badClass);
        EnaPciCandidateValidationResult badSubclassResult =
            EnaPciCandidateSelector.Validate(badSubclass);
        EnaPciCandidateValidationResult badProgrammingInterfaceResult =
            EnaPciCandidateSelector.Validate(badProgrammingInterface);
        EnaPciCandidateValidationResult missingCapabilitiesResult =
            EnaPciCandidateSelector.Validate(missingCapabilities);
        EnaPciCandidateValidationResult invalidCapabilityPointerResult =
            EnaPciCandidateSelector.Validate(invalidCapabilityPointer);

        PciEcamScannedDevice[] devices =
        [
            badHeader,
            badClass,
            badSubclass,
            badProgrammingInterface,
            missingCapabilities,
            invalidCapabilityPointer
        ];
        EnaPciCandidate[] candidates = new EnaPciCandidate[1];
        EnaPciCandidateSelectionResult selection = EnaPciCandidateSelector.Select(devices, candidates);

        return !badHeaderResult.Succeeded
            && badHeaderResult.Status == EnaPciCandidateValidationStatus.UnsupportedHeaderType
            && !badClassResult.Succeeded
            && badClassResult.Status == EnaPciCandidateValidationStatus.UnsupportedClassCode
            && !badSubclassResult.Succeeded
            && badSubclassResult.Status == EnaPciCandidateValidationStatus.UnsupportedSubclass
            && !badProgrammingInterfaceResult.Succeeded
            && badProgrammingInterfaceResult.Status ==
                EnaPciCandidateValidationStatus.UnsupportedProgrammingInterface
            && !missingCapabilitiesResult.Succeeded
            && missingCapabilitiesResult.Status == EnaPciCandidateValidationStatus.MissingCapabilitiesList
            && !invalidCapabilityPointerResult.Succeeded
            && invalidCapabilityPointerResult.Status == EnaPciCandidateValidationStatus.InvalidCapabilityPointer
            && selection.Succeeded
            && selection.CandidateCount == 0
            && selection.RequiredCandidateCapacity == 0;
    }

    private static bool TestPreservesPciSourceMetadata()
    {
        PciEcamScannedDevice[] devices =
        [
            CreateDevice(
                segmentIndex: 4,
                segmentGroup: 0x0099,
                ecamBasePhysicalAddress: 0x00000008_80000000,
                segmentStartBus: 0x20,
                segmentEndBus: 0x2F,
                bus: 0x22,
                device: 0x05,
                function: 0x03,
                vendorId: EnaPciCandidateSelector.AmazonVendorId,
                deviceId: EnaPciCandidateSelector.LowLatencyQueueVirtualFunctionDeviceId,
                revisionId: 0x07,
                headerType: PciConfigurationHeader.HeaderTypeMultifunctionBit,
                capabilityPointer: 0x80)
        ];
        EnaPciCandidate[] candidates = new EnaPciCandidate[1];

        EnaPciCandidateSelectionResult result = EnaPciCandidateSelector.Select(devices, candidates);
        EnaPciCandidate candidate = candidates[0];

        return result.Succeeded
            && result.CandidateCount == 1
            && candidate.SourceIndex == 0
            && candidate.SegmentIndex == 4
            && candidate.SegmentGroup == 0x0099
            && candidate.EcamBasePhysicalAddress == 0x00000008_80000000UL
            && candidate.SegmentStartBus == 0x20
            && candidate.SegmentEndBus == 0x2F
            && candidate.Bus == 0x22
            && candidate.Device == 0x05
            && candidate.Function == 0x03
            && candidate.VendorId == EnaPciCandidateSelector.AmazonVendorId
            && candidate.DeviceId == EnaPciCandidateSelector.LowLatencyQueueVirtualFunctionDeviceId
            && candidate.RevisionId == 0x07
            && candidate.ClassCode == EnaPciCandidateSelector.NetworkControllerClassCode
            && candidate.Subclass == EnaPciCandidateSelector.EthernetControllerSubclass
            && candidate.ProgrammingInterface == EnaPciCandidateSelector.EthernetProgrammingInterface
            && candidate.HeaderType == PciConfigurationHeader.HeaderTypeMultifunctionBit
            && candidate.CapabilityPointer == 0x80
            && candidate.DeviceKind == EnaPciDeviceKind.LowLatencyQueueVirtualFunction;
    }

    private static bool TestReportsDestinationExhaustion()
    {
        PciEcamScannedDevice[] devices =
        [
            CreateDevice(
                vendorId: EnaPciCandidateSelector.AmazonVendorId,
                deviceId: EnaPciCandidateSelector.VirtualFunctionDeviceId,
                bus: 0,
                device: 1,
                function: 0),
            CreateDevice(
                vendorId: EnaPciCandidateSelector.AmazonVendorId,
                deviceId: EnaPciCandidateSelector.LowLatencyQueueVirtualFunctionDeviceId,
                bus: 0,
                device: 2,
                function: 0),
            CreateDevice(
                vendorId: 0x1AF4,
                deviceId: 0x1041,
                bus: 0,
                device: 3,
                function: 0)
        ];
        EnaPciCandidate[] candidates = new EnaPciCandidate[1];

        EnaPciCandidateSelectionResult result = EnaPciCandidateSelector.Select(devices, candidates);

        return !result.Succeeded
            && result.Status == EnaPciCandidateSelectionStatus.DestinationTooSmall
            && result.InspectedDeviceCount == devices.Length
            && result.CandidateCount == 1
            && result.RequiredCandidateCapacity == 2
            && result.FailingSourceIndex == 1
            && candidates[0].SourceIndex == 0
            && candidates[0].DeviceKind == EnaPciDeviceKind.VirtualFunction;
    }

    private static PciEcamScannedDevice CreateDevice(
        ushort vendorId = EnaPciCandidateSelector.AmazonVendorId,
        ushort deviceId = EnaPciCandidateSelector.VirtualFunctionDeviceId,
        byte classCode = EnaPciCandidateSelector.NetworkControllerClassCode,
        byte subclass = EnaPciCandidateSelector.EthernetControllerSubclass,
        byte programmingInterface = EnaPciCandidateSelector.EthernetProgrammingInterface,
        byte headerType = PciConfigurationHeader.Type0HeaderType,
        bool hasCapabilitiesList = true,
        byte capabilityPointer = PciConfigurationHeader.FirstCapabilityPointer,
        int segmentIndex = 0,
        ushort segmentGroup = 0,
        ulong ecamBasePhysicalAddress = 0xE0000000,
        byte segmentStartBus = 0,
        byte segmentEndBus = 0xFF,
        byte bus = 0,
        byte device = 0,
        byte function = 0,
        byte revisionId = 0)
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
                revisionId,
                classCode,
                subclass,
                programmingInterface,
                headerType,
                isMultifunction: (headerType & PciConfigurationHeader.HeaderTypeMultifunctionBit) != 0,
                hasCapabilitiesList,
                capabilityPointer));
    }
}
