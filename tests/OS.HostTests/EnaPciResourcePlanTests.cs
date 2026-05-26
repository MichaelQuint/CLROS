using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Std.Binary;

internal static class EnaPciResourcePlanTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("drivers.ena.pci.resources accepts mmio bar", TestAcceptsMmioBar, ref failures);
        Run("drivers.ena.pci.resources parses msix", TestParsesMsix, ref failures);
        Run(
            "drivers.ena.pci.resources rejects malformed aperture",
            TestRejectsMalformedAperture,
            ref failures);
        Run(
            "drivers.ena.pci.resources rejects malformed msix",
            TestRejectsMalformedMsix,
            ref failures);
        Run(
            "drivers.ena.pci.resources rejects default results",
            TestRejectsDefaultResults,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("ENA PCI resource plan host tests failed: " + failures);
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

    private static bool TestAcceptsMmioBar()
    {
        PciMsixCapabilitySnapshot msix = CreateMsix(
            tableEntryCount: 4,
            tableBarIndicator: 0,
            tableOffset: 0x1000,
            pendingBitArrayBarIndicator: 0,
            pendingBitArrayOffset: 0x2000);
        EnaPciResourcePlanResult result = EnaPciResourcePlanner.Plan(CreateSnapshot(msix: msix));

        return result.Succeeded
            && result.Status == EnaPciResourcePlanStatus.Success
            && result.CandidateValidationStatus == EnaPciCandidateValidationStatus.Success
            && result.FailingBarIndex == PciBarDecoder.NoBarIndex
            && result.Plan.SourceIndex == 7
            && result.Plan.DeviceKind == EnaPciDeviceKind.VirtualFunction
            && result.Plan.Device.VendorId == EnaPciCandidateSelector.AmazonVendorId
            && result.Plan.RegisterBar.BarIndex == 0
            && result.Plan.RegisterBar.Kind == PciBarKind.Memory32
            && result.Plan.RegisterBar.BaseAddress == 0x80000000
            && result.Plan.RegisterBar.ApertureLength == 0x4000
            && result.Plan.RegisterBar.EndAddressExclusive == 0x80004000
            && result.Plan.Msix.TableEntryCount == 4
            && result.Plan.MsixTable.BarIndex == 0
            && result.Plan.MsixTable.Offset == 0x1000
            && result.Plan.MsixTable.Length == 64
            && result.Plan.MsixPendingBitArray.BarIndex == 0
            && result.Plan.MsixPendingBitArray.Offset == 0x2000
            && result.Plan.MsixPendingBitArray.Length == 8;
    }

    private static bool TestRejectsDefaultResults()
    {
        PciMsixCapabilityParseResult parse = default;
        EnaPciResourcePlanResult plan = default;

        return !parse.IsInitialized
            && !parse.Succeeded
            && parse.Status == PciMsixCapabilityParseStatus.Success
            && !plan.IsInitialized
            && !plan.Succeeded
            && plan.Status == EnaPciResourcePlanStatus.Success
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                PciMsixCapabilityParseResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                EnaPciResourcePlanResult>();
    }

    private static bool TestParsesMsix()
    {
        byte[] configurationSpace = CreateConfigurationSpace(
            tableEntryCount: 8,
            tableBarIndicator: 0,
            tableOffset: 0x0800,
            pendingBitArrayBarIndicator: 0,
            pendingBitArrayOffset: 0x1800);
        PciMsixCapabilityParseResult parse =
            PciMsixCapabilityParser.Find(configurationSpace, PciConfigurationHeader.FirstCapabilityPointer);
        EnaPciResourcePlanResult result =
            EnaPciResourcePlanner.Plan(CreateSnapshot(msix: parse.Capability));

        return parse.Succeeded
            && parse.Status == PciMsixCapabilityParseStatus.Success
            && parse.CapabilityOffset == PciConfigurationHeader.FirstCapabilityPointer
            && parse.Capability.IsPresent
            && parse.Capability.TableEntryCount == 8
            && parse.Capability.TableBarIndicator == 0
            && parse.Capability.TableOffset == 0x0800
            && parse.Capability.TableLengthBytes == 128
            && parse.Capability.PendingBitArrayBarIndicator == 0
            && parse.Capability.PendingBitArrayOffset == 0x1800
            && parse.Capability.PendingBitArrayLengthBytes == 8
            && !parse.Capability.FunctionMasked
            && !parse.Capability.Enabled
            && result.Succeeded
            && result.Plan.Msix.TableEntryCount == 8
            && result.Plan.MsixTable.Length == 128
            && result.Plan.MsixPendingBitArray.Length == 8;
    }

    private static bool TestRejectsMalformedAperture()
    {
        PciMsixCapabilitySnapshot msix = CreateMsix(
            tableEntryCount: 2,
            tableBarIndicator: 0,
            tableOffset: 0x0080,
            pendingBitArrayBarIndicator: 0,
            pendingBitArrayOffset: 0x0100);

        EnaPciResourcePlanResult tooSmall = EnaPciResourcePlanner.Plan(CreateSnapshot(
            apertures: new PciBarApertureSet(
                0x40,
                0,
                0,
                0,
                0,
                0),
            msix: msix));
        EnaPciResourcePlanResult notPowerOfTwo = EnaPciResourcePlanner.Plan(CreateSnapshot(
            apertures: new PciBarApertureSet(0x6000, 0, 0, 0, 0, 0),
            msix: msix));
        EnaPciResourcePlanResult misaligned = EnaPciResourcePlanner.Plan(CreateSnapshot(
            bar0Raw: 0x80001000,
            apertures: new PciBarApertureSet(0x4000, 0, 0, 0, 0, 0),
            msix: msix));
        EnaPciResourcePlanResult ioBar = EnaPciResourcePlanner.Plan(CreateSnapshot(
            bar0Raw: 0x0000C001,
            apertures: new PciBarApertureSet(0x100, 0, 0, 0, 0, 0),
            msix: msix));

        return !tooSmall.Succeeded
            && tooSmall.Status == EnaPciResourcePlanStatus.RegisterBarApertureTooSmall
            && tooSmall.FailingBarIndex == 0
            && !notPowerOfTwo.Succeeded
            && notPowerOfTwo.Status == EnaPciResourcePlanStatus.RegisterBarApertureSizeNotPowerOfTwo
            && !misaligned.Succeeded
            && misaligned.Status == EnaPciResourcePlanStatus.RegisterBarApertureMisaligned
            && !ioBar.Succeeded
            && ioBar.Status == EnaPciResourcePlanStatus.RegisterBarIsIo;
    }

    private static bool TestRejectsMalformedMsix()
    {
        EnaPciResourcePlanResult outOfRange = EnaPciResourcePlanner.Plan(CreateSnapshot(
            msix: CreateMsix(
                tableEntryCount: 4,
                tableBarIndicator: 0,
                tableOffset: 0x3FF0,
                pendingBitArrayBarIndicator: 0,
                pendingBitArrayOffset: 0x2000)));
        EnaPciResourcePlanResult overlap = EnaPciResourcePlanner.Plan(CreateSnapshot(
            msix: CreateMsix(
                tableEntryCount: 4,
                tableBarIndicator: 0,
                tableOffset: 0x1000,
                pendingBitArrayBarIndicator: 0,
                pendingBitArrayOffset: 0x1030)));
        EnaPciResourcePlanResult invalidBar = EnaPciResourcePlanner.Plan(CreateSnapshot(
            msix: CreateMsix(
                tableEntryCount: 4,
                tableBarIndicator: 7,
                tableOffset: 0x1000,
                pendingBitArrayBarIndicator: 0,
                pendingBitArrayOffset: 0x2000)));
        EnaPciResourcePlanResult missing = EnaPciResourcePlanner.Plan(CreateSnapshot(
            msix: PciMsixCapabilitySnapshot.Missing,
            preserveMissingMsix: true));

        return !outOfRange.Succeeded
            && outOfRange.Status == EnaPciResourcePlanStatus.MsixRegionOutOfRange
            && !overlap.Succeeded
            && overlap.Status == EnaPciResourcePlanStatus.MsixRegionsOverlap
            && !invalidBar.Succeeded
            && invalidBar.Status == EnaPciResourcePlanStatus.MsixTableBarInvalid
            && !missing.Succeeded
            && missing.Status == EnaPciResourcePlanStatus.MissingMsixCapability;
    }

    private static EnaPciResourceSnapshot CreateSnapshot(
        uint bar0Raw = 0x80000000,
        PciBarApertureSet apertures = default,
        PciMsixCapabilitySnapshot msix = default,
        bool preserveMissingMsix = false)
    {
        if (apertures.Bar0Length == 0 &&
            apertures.Bar1Length == 0 &&
            apertures.Bar2Length == 0 &&
            apertures.Bar3Length == 0 &&
            apertures.Bar4Length == 0 &&
            apertures.Bar5Length == 0)
        {
            apertures = new PciBarApertureSet(0x4000, 0, 0, 0, 0, 0);
        }

        if (!msix.IsPresent && !preserveMissingMsix)
        {
            msix = CreateMsix(
                tableEntryCount: 4,
                tableBarIndicator: 0,
                tableOffset: 0x1000,
                pendingBitArrayBarIndicator: 0,
                pendingBitArrayOffset: 0x2000);
        }

        PciType0ConfigurationHeader header = ParseHeader(CreateConfigurationSpace(
            tableEntryCount: 4,
            tableBarIndicator: 0,
            tableOffset: 0x1000,
            pendingBitArrayBarIndicator: 0,
            pendingBitArrayOffset: 0x2000,
            bar0Raw: bar0Raw));
        PciBarDecodeResult barDecode = PciBarDecoder.Decode(header);
        if (!barDecode.Succeeded)
        {
            throw new InvalidOperationException("test BAR decode failed: " + barDecode.Status);
        }

        return new EnaPciResourceSnapshot(
            sourceIndex: 7,
            CreateDevice(),
            barDecode.Bars,
            apertures,
            msix,
            EnaPciResourcePlanner.DefaultRegisterBarIndex);
    }

    private static PciMsixCapabilitySnapshot CreateMsix(
        ushort tableEntryCount,
        byte tableBarIndicator,
        uint tableOffset,
        byte pendingBitArrayBarIndicator,
        uint pendingBitArrayOffset)
    {
        return new PciMsixCapabilitySnapshot(
            isPresent: true,
            tableEntryCount,
            tableBarIndicator,
            tableOffset,
            pendingBitArrayBarIndicator,
            pendingBitArrayOffset,
            functionMasked: false,
            enabled: false);
    }

    private static PciEcamScannedDevice CreateDevice()
    {
        return new PciEcamScannedDevice(
            segmentIndex: 2,
            new PciEcamSegment(
                basePhysicalAddress: 0x00000008_00000000,
                segmentGroup: 0,
                startBus: 0,
                endBus: 0xFF),
            new PciScannedDevice(
                bus: 0,
                device: 5,
                function: 0,
                vendorId: EnaPciCandidateSelector.AmazonVendorId,
                deviceId: EnaPciCandidateSelector.VirtualFunctionDeviceId,
                revisionId: 1,
                classCode: EnaPciCandidateSelector.NetworkControllerClassCode,
                subclass: EnaPciCandidateSelector.EthernetControllerSubclass,
                programmingInterface: EnaPciCandidateSelector.EthernetProgrammingInterface,
                headerType: PciConfigurationHeader.Type0HeaderType,
                isMultifunction: false,
                hasCapabilitiesList: true,
                capabilityPointer: PciConfigurationHeader.FirstCapabilityPointer));
    }

    private static PciType0ConfigurationHeader ParseHeader(byte[] configurationSpace)
    {
        PciConfigurationHeaderResult result = PciConfigurationHeader.ParseType0(configurationSpace);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("test header parse failed: " + result.Status);
        }

        return result.Header;
    }

    private static byte[] CreateConfigurationSpace(
        ushort tableEntryCount,
        byte tableBarIndicator,
        uint tableOffset,
        byte pendingBitArrayBarIndicator,
        uint pendingBitArrayOffset,
        uint bar0Raw = 0x80000000)
    {
        byte[] configurationSpace = new byte[256];
        EndianPrimitives.WriteUInt16LittleEndian(
            configurationSpace.AsSpan(0x00, sizeof(ushort)),
            EnaPciCandidateSelector.AmazonVendorId);
        EndianPrimitives.WriteUInt16LittleEndian(
            configurationSpace.AsSpan(0x02, sizeof(ushort)),
            EnaPciCandidateSelector.VirtualFunctionDeviceId);
        EndianPrimitives.WriteUInt16LittleEndian(
            configurationSpace.AsSpan(0x06, sizeof(ushort)),
            PciConfigurationHeader.StatusCapabilitiesListBit);
        configurationSpace[0x08] = 1;
        configurationSpace[0x09] = EnaPciCandidateSelector.EthernetProgrammingInterface;
        configurationSpace[0x0A] = EnaPciCandidateSelector.EthernetControllerSubclass;
        configurationSpace[0x0B] = EnaPciCandidateSelector.NetworkControllerClassCode;
        configurationSpace[0x0E] = PciConfigurationHeader.Type0HeaderType;
        EndianPrimitives.WriteUInt32LittleEndian(configurationSpace.AsSpan(0x10, sizeof(uint)), bar0Raw);
        configurationSpace[0x34] = PciConfigurationHeader.FirstCapabilityPointer;

        byte capabilityOffset = PciConfigurationHeader.FirstCapabilityPointer;
        configurationSpace[capabilityOffset] = PciMsixCapabilityParser.CapabilityId;
        configurationSpace[capabilityOffset + 1] = 0;
        EndianPrimitives.WriteUInt16LittleEndian(
            configurationSpace.AsSpan(capabilityOffset + 2, sizeof(ushort)),
            (ushort)(tableEntryCount - 1));
        EndianPrimitives.WriteUInt32LittleEndian(
            configurationSpace.AsSpan(capabilityOffset + 4, sizeof(uint)),
            (tableOffset & PciMsixCapabilityParser.OffsetMask) | tableBarIndicator);
        EndianPrimitives.WriteUInt32LittleEndian(
            configurationSpace.AsSpan(capabilityOffset + 8, sizeof(uint)),
            (pendingBitArrayOffset & PciMsixCapabilityParser.OffsetMask) |
            pendingBitArrayBarIndicator);
        return configurationSpace;
    }
}
