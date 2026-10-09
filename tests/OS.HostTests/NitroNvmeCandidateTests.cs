using System.Runtime.CompilerServices;
using System.Text;
using OS.Drivers;
using OS.Std.Binary;

internal static class NitroNvmeCandidateTests
{

    [HostTest("drivers.nvme.nitro rejects default plan result", HostLane.Fast, 5)]
    private static bool TestRejectsDefaultPlanResult()
    {
        NitroNvmePlanResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == NitroNvmePlanStatus.Success
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NitroNvmePlanResult>());
    }

    [HostTest("drivers.nvme.nitro identifies controller", HostLane.Fast, 0)]
    private static bool TestIdentifiesController()
    {
        byte[] controllerIdentify = CreateControllerIdentify(
            namespaceCount: 3,
            serialNumber: "vol0123456789",
            modelNumber: "Amazon Elastic Block Store");
        byte[] namespaceIdentify = CreateNamespaceIdentify(
            totalLogicalBlocks: 1024,
            lbaFormatIndex: 0,
            lbaFormatCount: 1,
            lbaDataSizeShift: 12);
        NitroNvmePciSnapshot snapshot = CreateSnapshot(
            sourceIndex: 6,
            subsystemVendorId: 0x1D0F,
            subsystemId: 0x8061);

        NitroNvmePlanResult result = NitroNvmePlanner.Plan(
            snapshot,
            namespaceId: 1,
            controllerIdentify,
            namespaceIdentify);

        return HostCheck.Result(NitroNvmePlanner.MassStorageClassCode == 0x01
            && NitroNvmePlanner.NonVolatileMemorySubclass == 0x08
            && NitroNvmePlanner.NvmeProgrammingInterface == 0x02
            && result.Succeeded
            && result.Status == NitroNvmePlanStatus.Success
            && result.FailingBarIndex == PciBarDecoder.NoBarIndex
            && result.Plan.SourceIndex == 6
            && result.Plan.Device.VendorId == 0x1D0F
            && result.Plan.Device.DeviceId == 0x8061
            && result.Plan.SubsystemVendorId == 0x1D0F
            && result.Plan.SubsystemId == 0x8061
            && result.Plan.RegisterBar.BarIndex == NitroNvmePlanner.DefaultRegisterBarIndex
            && result.Plan.RegisterBar.Kind == PciBarKind.Memory32
            && result.Plan.RegisterBar.BaseAddress == 0x90000000
            && result.Plan.RegisterBar.ApertureLength == 0x4000
            && result.Plan.NamespaceIdentity.NamespaceCount == 3
            && result.Plan.NamespaceIdentity.NamespaceId == 1);
    }

    [HostTest("drivers.nvme.nitro parses namespace identity", HostLane.Fast, 1)]
    private static bool TestParsesNamespaceIdentity()
    {
        byte[] controllerIdentify = CreateControllerIdentify(
            namespaceCount: 4,
            serialNumber: "vol0abc123",
            modelNumber: "Amazon Elastic Block Store");
        byte[] namespaceIdentify = CreateNamespaceIdentify(
            totalLogicalBlocks: 0x12345,
            lbaFormatIndex: 1,
            lbaFormatCount: 3,
            lbaDataSizeShift: 9);

        NitroNvmePlanResult result = NitroNvmePlanner.Plan(
            CreateSnapshot(msix: CreateMsix(tableEntryCount: 8)),
            namespaceId: 2,
            controllerIdentify,
            namespaceIdentify);
        NitroNvmeNamespaceIdentity identity = result.Plan.NamespaceIdentity;

        return HostCheck.Result(result.Succeeded
            && result.Plan.Msix.IsPresent
            && result.Plan.Msix.TableEntryCount == 8
            && identity.NamespaceCount == 4
            && identity.NamespaceId == 2
            && identity.TotalLogicalBlocks == 0x12345
            && identity.LogicalBlockSizeBytes == 512
            && identity.CapacityBytes == 0x12345UL * 512UL
            && identity.LbaFormatIndex == 1
            && identity.LbaDataSizeShift == 9
            && identity.SerialNumber.Length == 10
            && identity.SerialNumber.MaximumLength == NitroNvmePlanner.ControllerSerialNumberLength
            && identity.ModelNumber.Length == 26
            && FieldEquals(identity.SerialNumber, controllerIdentify, "vol0abc123")
            && FieldEquals(identity.ModelNumber, controllerIdentify, "Amazon Elastic Block Store"));
    }

    [HostTest("drivers.nvme.nitro rejects malformed identity", HostLane.Fast, 2)]
    private static bool TestRejectsMalformedIdentity()
    {
        byte[] validController = CreateControllerIdentify(namespaceCount: 2);
        byte[] badSerialController = CreateControllerIdentify(namespaceCount: 2);
        badSerialController[NitroNvmePlanner.ControllerSerialNumberOffset + 3] = 0x01;

        NitroNvmePlanResult zeroCapacity = NitroNvmePlanner.Plan(
            CreateSnapshot(),
            namespaceId: 1,
            validController,
            CreateNamespaceIdentify(totalLogicalBlocks: 0));
        NitroNvmePlanResult impossibleLba = NitroNvmePlanner.Plan(
            CreateSnapshot(),
            namespaceId: 1,
            validController,
            CreateNamespaceIdentify(totalLogicalBlocks: 8, lbaDataSizeShift: 8));
        NitroNvmePlanResult lbaFormatOutOfRange = NitroNvmePlanner.Plan(
            CreateSnapshot(),
            namespaceId: 1,
            validController,
            CreateNamespaceIdentify(totalLogicalBlocks: 8, lbaFormatIndex: 2, lbaFormatCount: 1));
        NitroNvmePlanResult namespaceOutOfRange = NitroNvmePlanner.Plan(
            CreateSnapshot(),
            namespaceId: 3,
            validController,
            CreateNamespaceIdentify(totalLogicalBlocks: 8));
        NitroNvmePlanResult malformedSerial = NitroNvmePlanner.Plan(
            CreateSnapshot(),
            namespaceId: 1,
            badSerialController,
            CreateNamespaceIdentify(totalLogicalBlocks: 8));
        NitroNvmePlanResult shortNamespaceIdentify = NitroNvmePlanner.Plan(
            CreateSnapshot(),
            namespaceId: 1,
            validController,
            new byte[128]);

        return HostCheck.Result(!zeroCapacity.Succeeded
            && zeroCapacity.Status == NitroNvmePlanStatus.ZeroCapacity
            && !impossibleLba.Succeeded
            && impossibleLba.Status == NitroNvmePlanStatus.ImpossibleLogicalBlockSize
            && !lbaFormatOutOfRange.Succeeded
            && lbaFormatOutOfRange.Status == NitroNvmePlanStatus.SelectedLbaFormatOutOfRange
            && !namespaceOutOfRange.Succeeded
            && namespaceOutOfRange.Status == NitroNvmePlanStatus.NamespaceIdOutOfRange
            && !malformedSerial.Succeeded
            && malformedSerial.Status == NitroNvmePlanStatus.MalformedSerialNumber
            && !shortNamespaceIdentify.Succeeded
            && shortNamespaceIdentify.Status == NitroNvmePlanStatus.NamespaceIdentifyBufferTooSmall);
    }

    [HostTest("drivers.nvme.nitro rejects non nvme device", HostLane.Fast, 3)]
    private static bool TestRejectsNonNvmeDevice()
    {
        byte[] controllerIdentify = CreateControllerIdentify(namespaceCount: 1);
        byte[] namespaceIdentify = CreateNamespaceIdentify(totalLogicalBlocks: 16);

        NitroNvmePlanResult virtioBlock = NitroNvmePlanner.Plan(
            CreateSnapshot(
                vendorId: 0x1AF4,
                deviceId: 0x1042,
                classCode: NitroNvmePlanner.MassStorageClassCode,
                subclass: 0x00,
                programmingInterface: 0x00),
            namespaceId: 1,
            controllerIdentify,
            namespaceIdentify);
        NitroNvmePlanResult networkDevice = NitroNvmePlanner.Plan(
            CreateSnapshot(
                classCode: 0x02,
                subclass: 0x00,
                programmingInterface: 0x00),
            namespaceId: 1,
            controllerIdentify,
            namespaceIdentify);
        NitroNvmePlanResult badProgrammingInterface = NitroNvmePlanner.Plan(
            CreateSnapshot(programmingInterface: 0x01),
            namespaceId: 1,
            controllerIdentify,
            namespaceIdentify);

        return HostCheck.Result(!virtioBlock.Succeeded
            && virtioBlock.Status == NitroNvmePlanStatus.UnsupportedSubclass
            && !networkDevice.Succeeded
            && networkDevice.Status == NitroNvmePlanStatus.UnsupportedClassCode
            && !badProgrammingInterface.Succeeded
            && badProgrammingInterface.Status == NitroNvmePlanStatus.UnsupportedProgrammingInterface);
    }

    [HostTest("drivers.nvme.nitro rejects missing mmio bar", HostLane.Fast, 4)]
    private static bool TestRejectsMissingMmioBar()
    {
        byte[] controllerIdentify = CreateControllerIdentify(namespaceCount: 1);
        byte[] namespaceIdentify = CreateNamespaceIdentify(totalLogicalBlocks: 16);

        NitroNvmePlanResult absentBar = NitroNvmePlanner.Plan(
            CreateSnapshot(bar0Raw: 0),
            namespaceId: 1,
            controllerIdentify,
            namespaceIdentify);
        NitroNvmePlanResult ioBar = NitroNvmePlanner.Plan(
            CreateSnapshot(bar0Raw: 0x0000C001, bar0Length: 0x100),
            namespaceId: 1,
            controllerIdentify,
            namespaceIdentify);
        NitroNvmePlanResult tooSmallAperture = NitroNvmePlanner.Plan(
            CreateSnapshot(bar0Length: 0x1000),
            namespaceId: 1,
            controllerIdentify,
            namespaceIdentify);
        NitroNvmePlanResult misalignedAperture = NitroNvmePlanner.Plan(
            CreateSnapshot(bar0Raw: 0x90001000, bar0Length: 0x4000),
            namespaceId: 1,
            controllerIdentify,
            namespaceIdentify);

        return HostCheck.Result(!absentBar.Succeeded
            && absentBar.Status == NitroNvmePlanStatus.RegisterBarUnavailable
            && absentBar.FailingBarIndex == 0
            && !ioBar.Succeeded
            && ioBar.Status == NitroNvmePlanStatus.RegisterBarIsIo
            && !tooSmallAperture.Succeeded
            && tooSmallAperture.Status == NitroNvmePlanStatus.RegisterBarApertureTooSmall
            && !misalignedAperture.Succeeded
            && misalignedAperture.Status == NitroNvmePlanStatus.RegisterBarApertureMisaligned);
    }

    private static NitroNvmePciSnapshot CreateSnapshot(
        int sourceIndex = 4,
        ushort vendorId = 0x1D0F,
        ushort deviceId = 0x8061,
        ushort subsystemVendorId = 0x1D0F,
        ushort subsystemId = 0x8061,
        byte classCode = NitroNvmePlanner.MassStorageClassCode,
        byte subclass = NitroNvmePlanner.NonVolatileMemorySubclass,
        byte programmingInterface = NitroNvmePlanner.NvmeProgrammingInterface,
        uint bar0Raw = 0x90000000,
        ulong bar0Length = 0x4000,
        PciMsixCapabilitySnapshot msix = default)
    {
        PciEcamScannedDevice device = new PciEcamScannedDevice(
            segmentIndex: 1,
            new PciEcamSegment(
                basePhysicalAddress: 0x00000008_00000000,
                segmentGroup: 0,
                startBus: 0,
                endBus: 0xFF),
            new PciScannedDevice(
                bus: 0,
                device: 4,
                function: 0,
                vendorId,
                deviceId,
                revisionId: 1,
                classCode,
                subclass,
                programmingInterface,
                headerType: PciConfigurationHeader.Type0HeaderType,
                isMultifunction: false,
                hasCapabilitiesList: true,
                capabilityPointer: PciConfigurationHeader.FirstCapabilityPointer));

        PciType0ConfigurationHeader header = ParseHeader(CreateConfigurationSpace(
            vendorId,
            deviceId,
            classCode,
            subclass,
            programmingInterface,
            bar0Raw));
        PciBarDecodeResult barDecode = PciBarDecoder.Decode(header);
        if (!barDecode.Succeeded)
        {
            throw new InvalidOperationException("test BAR decode failed: " + barDecode.Status);
        }

        return new NitroNvmePciSnapshot(
            sourceIndex,
            device,
            subsystemVendorId,
            subsystemId,
            barDecode.Bars,
            new PciBarApertureSet(bar0Length, 0, 0, 0, 0, 0),
            msix,
            NitroNvmePlanner.DefaultRegisterBarIndex);
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
        ushort vendorId,
        ushort deviceId,
        byte classCode,
        byte subclass,
        byte programmingInterface,
        uint bar0Raw)
    {
        byte[] configurationSpace = new byte[PciConfigurationHeader.Type0HeaderLength];
        EndianPrimitives.WriteUInt16LittleEndian(
            configurationSpace.AsSpan(0x00, sizeof(ushort)),
            vendorId);
        EndianPrimitives.WriteUInt16LittleEndian(
            configurationSpace.AsSpan(0x02, sizeof(ushort)),
            deviceId);
        EndianPrimitives.WriteUInt16LittleEndian(
            configurationSpace.AsSpan(0x06, sizeof(ushort)),
            PciConfigurationHeader.StatusCapabilitiesListBit);
        configurationSpace[0x08] = 1;
        configurationSpace[0x09] = programmingInterface;
        configurationSpace[0x0A] = subclass;
        configurationSpace[0x0B] = classCode;
        configurationSpace[0x0E] = PciConfigurationHeader.Type0HeaderType;
        EndianPrimitives.WriteUInt32LittleEndian(configurationSpace.AsSpan(0x10, sizeof(uint)), bar0Raw);
        configurationSpace[0x34] = PciConfigurationHeader.FirstCapabilityPointer;
        return configurationSpace;
    }

    private static PciMsixCapabilitySnapshot CreateMsix(ushort tableEntryCount)
    {
        return new PciMsixCapabilitySnapshot(
            isPresent: true,
            tableEntryCount,
            tableBarIndicator: 0,
            tableOffset: 0x1000,
            pendingBitArrayBarIndicator: 0,
            pendingBitArrayOffset: 0x2000,
            functionMasked: false,
            enabled: false);
    }

    private static byte[] CreateControllerIdentify(
        uint namespaceCount,
        string serialNumber = "vol0123456789",
        string modelNumber = "Amazon Elastic Block Store")
    {
        byte[] identify = new byte[NitroNvmePlanner.IdentifyControllerLength];
        FillAsciiField(
            identify,
            NitroNvmePlanner.ControllerSerialNumberOffset,
            NitroNvmePlanner.ControllerSerialNumberLength,
            serialNumber);
        FillAsciiField(
            identify,
            NitroNvmePlanner.ControllerModelNumberOffset,
            NitroNvmePlanner.ControllerModelNumberLength,
            modelNumber);
        EndianPrimitives.WriteUInt32LittleEndian(
            identify.AsSpan(NitroNvmePlanner.ControllerNamespaceCountOffset, sizeof(uint)),
            namespaceCount);
        return identify;
    }

    private static byte[] CreateNamespaceIdentify(
        ulong totalLogicalBlocks,
        byte lbaFormatIndex = 0,
        byte lbaFormatCount = 1,
        byte lbaDataSizeShift = 12)
    {
        byte[] identify = new byte[NitroNvmePlanner.IdentifyNamespaceLength];
        EndianPrimitives.WriteUInt64LittleEndian(
            identify.AsSpan(NitroNvmePlanner.NamespaceSizeOffset, sizeof(ulong)),
            totalLogicalBlocks);
        identify[NitroNvmePlanner.NamespaceLbaFormatCountOffset] = (byte)(lbaFormatCount - 1);
        identify[NitroNvmePlanner.NamespaceFormattedLbaSizeOffset] = lbaFormatIndex;
        int lbaFormatOffset =
            NitroNvmePlanner.NamespaceLbaFormatTableOffset +
            (lbaFormatIndex * NitroNvmePlanner.NamespaceLbaFormatEntryLength);
        identify[lbaFormatOffset + NitroNvmePlanner.NamespaceLbaDataSizeShiftOffset] = lbaDataSizeShift;
        return identify;
    }

    private static void FillAsciiField(
        byte[] destination,
        int offset,
        int length,
        string value)
    {
        destination.AsSpan(offset, length).Fill(0x20);
        byte[] bytes = Encoding.ASCII.GetBytes(value);
        bytes.AsSpan(0, Math.Min(bytes.Length, length)).CopyTo(destination.AsSpan(offset, length));
    }

    private static bool FieldEquals(
        NvmeIdentityTextField field,
        byte[] source,
        string expected)
    {
        return field.TryGetSlice(source, out ReadOnlySpan<byte> value) &&
            value.SequenceEqual(Encoding.ASCII.GetBytes(expected));
    }
}
