using OS.Std.Binary;

namespace OS.Drivers;

public enum NitroNvmePlanStatus
{
    Success = 0,
    UnsupportedHeaderType = 1,
    UnsupportedClassCode = 2,
    UnsupportedSubclass = 3,
    UnsupportedProgrammingInterface = 4,
    InvalidRegisterBarIndex = 5,
    RegisterBarUnavailable = 6,
    RegisterBarIsIo = 7,
    RegisterBarIsMalformed = 8,
    RegisterBarBaseAddressZero = 9,
    RegisterBarApertureMissing = 10,
    RegisterBarApertureSizeNotPowerOfTwo = 11,
    RegisterBarApertureMisaligned = 12,
    RegisterBarApertureTooSmall = 13,
    ControllerIdentifyBufferTooSmall = 14,
    ControllerIdentifyBufferTooLarge = 15,
    NamespaceIdentifyBufferTooSmall = 16,
    NamespaceIdentifyBufferTooLarge = 17,
    NamespaceCountZero = 18,
    NamespaceIdZero = 19,
    NamespaceIdOutOfRange = 20,
    MalformedSerialNumber = 21,
    MalformedModelNumber = 22,
    SelectedLbaFormatOutOfRange = 23,
    ImpossibleLogicalBlockSize = 24,
    ZeroCapacity = 25,
    CapacityOverflow = 26
}

public readonly struct NvmeIdentityTextField
{
    public NvmeIdentityTextField(
        int offset,
        int length,
        int maximumLength)
    {
        Offset = offset;
        Length = length;
        MaximumLength = maximumLength;
        IsPresent = length > 0;
    }

    public int Offset { get; }

    public int Length { get; }

    public int MaximumLength { get; }

    public bool IsPresent { get; }

    public bool TryGetSlice(ReadOnlySpan<byte> source, out ReadOnlySpan<byte> value)
    {
        if (!IsPresent ||
            Offset < 0 ||
            Length < 0 ||
            Offset > source.Length ||
            source.Length - Offset < Length)
        {
            value = default;
            return false;
        }

        value = source.Slice(Offset, Length);
        return true;
    }
}

public readonly struct NitroNvmePciSnapshot
{
    public NitroNvmePciSnapshot(
        int sourceIndex,
        PciEcamScannedDevice device,
        ushort subsystemVendorId,
        ushort subsystemId,
        PciBarDescriptorSet bars,
        PciBarApertureSet apertures,
        PciMsixCapabilitySnapshot msix,
        byte registerBarIndex)
    {
        SourceIndex = sourceIndex;
        Device = device;
        SubsystemVendorId = subsystemVendorId;
        SubsystemId = subsystemId;
        Bars = bars;
        Apertures = apertures;
        Msix = msix;
        RegisterBarIndex = registerBarIndex;
    }

    public int SourceIndex { get; }

    public PciEcamScannedDevice Device { get; }

    public ushort SubsystemVendorId { get; }

    public ushort SubsystemId { get; }

    public PciBarDescriptorSet Bars { get; }

    public PciBarApertureSet Apertures { get; }

    public PciMsixCapabilitySnapshot Msix { get; }

    public byte RegisterBarIndex { get; }
}

public readonly struct NitroNvmeNamespaceIdentity
{
    internal NitroNvmeNamespaceIdentity(
        uint namespaceCount,
        uint namespaceId,
        ulong totalLogicalBlocks,
        ulong logicalBlockSizeBytes,
        ulong capacityBytes,
        byte lbaFormatIndex,
        byte lbaDataSizeShift,
        NvmeIdentityTextField serialNumber,
        NvmeIdentityTextField modelNumber)
    {
        NamespaceCount = namespaceCount;
        NamespaceId = namespaceId;
        TotalLogicalBlocks = totalLogicalBlocks;
        LogicalBlockSizeBytes = logicalBlockSizeBytes;
        CapacityBytes = capacityBytes;
        LbaFormatIndex = lbaFormatIndex;
        LbaDataSizeShift = lbaDataSizeShift;
        SerialNumber = serialNumber;
        ModelNumber = modelNumber;
    }

    public uint NamespaceCount { get; }

    public uint NamespaceId { get; }

    public ulong TotalLogicalBlocks { get; }

    public ulong LogicalBlockSizeBytes { get; }

    public ulong CapacityBytes { get; }

    public byte LbaFormatIndex { get; }

    public byte LbaDataSizeShift { get; }

    public NvmeIdentityTextField SerialNumber { get; }

    public NvmeIdentityTextField ModelNumber { get; }
}

public readonly struct NitroNvmePlan
{
    internal NitroNvmePlan(
        int sourceIndex,
        PciEcamScannedDevice device,
        ushort subsystemVendorId,
        ushort subsystemId,
        PciBarResourcePlan registerBar,
        PciMsixCapabilitySnapshot msix,
        NitroNvmeNamespaceIdentity namespaceIdentity)
    {
        SourceIndex = sourceIndex;
        Device = device;
        SubsystemVendorId = subsystemVendorId;
        SubsystemId = subsystemId;
        RegisterBar = registerBar;
        Msix = msix;
        NamespaceIdentity = namespaceIdentity;
    }

    public int SourceIndex { get; }

    public PciEcamScannedDevice Device { get; }

    public ushort SubsystemVendorId { get; }

    public ushort SubsystemId { get; }

    public PciBarResourcePlan RegisterBar { get; }

    public PciMsixCapabilitySnapshot Msix { get; }

    public NitroNvmeNamespaceIdentity NamespaceIdentity { get; }
}

public readonly struct NitroNvmePlanResult
{
    private NitroNvmePlanResult(
        bool isInitialized,
        NitroNvmePlanStatus status,
        byte failingBarIndex,
        NitroNvmePlan plan)
    {
        IsInitialized = isInitialized;
        Status = status;
        FailingBarIndex = failingBarIndex;
        Plan = plan;
    }

    public NitroNvmePlanStatus Status { get; }

    public byte FailingBarIndex { get; }

    public NitroNvmePlan Plan { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == NitroNvmePlanStatus.Success;

    internal static NitroNvmePlanResult Success(NitroNvmePlan plan)
    {
        return new NitroNvmePlanResult(
            isInitialized: true,
            NitroNvmePlanStatus.Success,
            PciBarDecoder.NoBarIndex,
            plan);
    }

    internal static NitroNvmePlanResult Fail(
        NitroNvmePlanStatus status,
        byte failingBarIndex = PciBarDecoder.NoBarIndex)
    {
        return new NitroNvmePlanResult(
            isInitialized: true,
            status,
            failingBarIndex,
            default);
    }
}

public static class NitroNvmePlanner
{
    public const byte MassStorageClassCode = 0x01;
    public const byte NonVolatileMemorySubclass = 0x08;
    public const byte NvmeProgrammingInterface = 0x02;
    public const byte DefaultRegisterBarIndex = 0;
    public const int IdentifyControllerLength = 4096;
    public const int IdentifyNamespaceLength = 4096;
    public const int ControllerSerialNumberOffset = 4;
    public const int ControllerSerialNumberLength = 20;
    public const int ControllerModelNumberOffset = 24;
    public const int ControllerModelNumberLength = 40;
    public const int ControllerNamespaceCountOffset = 516;
    public const int NamespaceSizeOffset = 0;
    public const int NamespaceLbaFormatCountOffset = 25;
    public const int NamespaceFormattedLbaSizeOffset = 26;
    public const int NamespaceLbaFormatTableOffset = 128;
    public const int NamespaceLbaFormatEntryLength = 4;
    public const int NamespaceLbaDataSizeShiftOffset = 2;
    public const byte MinimumLbaDataSizeShift = 9;
    public const byte MaximumLbaDataSizeShift = 30;
    public const ulong MinimumRegisterBarApertureLength =
        NvmeRegisterMap.DoorbellBaseOffset + NvmeRegisterMap.Doorbell.RegisterWidth;

    private const byte FormattedLbaFormatIndexMask = 0x0F;
    private const byte AsciiSpace = 0x20;
    private const byte AsciiDelete = 0x7F;

    public static NitroNvmePlanResult Plan(
        NitroNvmePciSnapshot snapshot,
        uint namespaceId,
        ReadOnlySpan<byte> identifyController,
        ReadOnlySpan<byte> identifyNamespace)
    {
        NitroNvmePlanStatus pciStatus = ValidatePciIdentity(snapshot.Device);
        if (pciStatus != NitroNvmePlanStatus.Success)
        {
            return NitroNvmePlanResult.Fail(pciStatus);
        }

        NitroNvmePlanStatus registerStatus = ValidateRegisterBar(
            snapshot,
            out PciBarResourcePlan registerBar);
        if (registerStatus != NitroNvmePlanStatus.Success)
        {
            return NitroNvmePlanResult.Fail(registerStatus, snapshot.RegisterBarIndex);
        }

        NitroNvmePlanStatus controllerStatus = ValidateIdentifyController(
            identifyController,
            namespaceId,
            out uint namespaceCount,
            out NvmeIdentityTextField serialNumber,
            out NvmeIdentityTextField modelNumber);
        if (controllerStatus != NitroNvmePlanStatus.Success)
        {
            return NitroNvmePlanResult.Fail(controllerStatus);
        }

        NitroNvmePlanStatus namespaceStatus = ParseNamespaceIdentity(
            identifyNamespace,
            namespaceCount,
            namespaceId,
            serialNumber,
            modelNumber,
            out NitroNvmeNamespaceIdentity namespaceIdentity);
        if (namespaceStatus != NitroNvmePlanStatus.Success)
        {
            return NitroNvmePlanResult.Fail(namespaceStatus);
        }

        NitroNvmePlan plan = new NitroNvmePlan(
            snapshot.SourceIndex,
            snapshot.Device,
            snapshot.SubsystemVendorId,
            snapshot.SubsystemId,
            registerBar,
            snapshot.Msix,
            namespaceIdentity);
        return NitroNvmePlanResult.Success(plan);
    }

    private static NitroNvmePlanStatus ValidatePciIdentity(PciEcamScannedDevice device)
    {
        if ((device.HeaderType & PciConfigurationHeader.HeaderTypeMask) !=
            PciConfigurationHeader.Type0HeaderType)
        {
            return NitroNvmePlanStatus.UnsupportedHeaderType;
        }

        if (device.ClassCode != MassStorageClassCode)
        {
            return NitroNvmePlanStatus.UnsupportedClassCode;
        }

        if (device.Subclass != NonVolatileMemorySubclass)
        {
            return NitroNvmePlanStatus.UnsupportedSubclass;
        }

        if (device.ProgrammingInterface != NvmeProgrammingInterface)
        {
            return NitroNvmePlanStatus.UnsupportedProgrammingInterface;
        }

        return NitroNvmePlanStatus.Success;
    }

    private static NitroNvmePlanStatus ValidateRegisterBar(
        NitroNvmePciSnapshot snapshot,
        out PciBarResourcePlan registerBar)
    {
        registerBar = default;
        if (!snapshot.Bars.TryGet(snapshot.RegisterBarIndex, out PciBarDescriptor descriptor))
        {
            return NitroNvmePlanStatus.InvalidRegisterBarIndex;
        }

        if (descriptor.IsMalformed)
        {
            return NitroNvmePlanStatus.RegisterBarIsMalformed;
        }

        if (descriptor.IsIo)
        {
            return NitroNvmePlanStatus.RegisterBarIsIo;
        }

        if (!descriptor.IsMemory)
        {
            return NitroNvmePlanStatus.RegisterBarUnavailable;
        }

        if (descriptor.BaseAddressBits == 0)
        {
            return NitroNvmePlanStatus.RegisterBarBaseAddressZero;
        }

        if (!snapshot.Apertures.TryGet(snapshot.RegisterBarIndex, out ulong apertureLength) ||
            apertureLength == 0)
        {
            return NitroNvmePlanStatus.RegisterBarApertureMissing;
        }

        if (!IsPowerOfTwo(apertureLength))
        {
            return NitroNvmePlanStatus.RegisterBarApertureSizeNotPowerOfTwo;
        }

        if ((descriptor.BaseAddressBits % apertureLength) != 0)
        {
            return NitroNvmePlanStatus.RegisterBarApertureMisaligned;
        }

        if (apertureLength < MinimumRegisterBarApertureLength)
        {
            return NitroNvmePlanStatus.RegisterBarApertureTooSmall;
        }

        registerBar = new PciBarResourcePlan(
            descriptor.Index,
            descriptor.Kind,
            descriptor.BaseAddressBits,
            apertureLength,
            descriptor.IsPrefetchable);
        return NitroNvmePlanStatus.Success;
    }

    private static NitroNvmePlanStatus ValidateIdentifyController(
        ReadOnlySpan<byte> identifyController,
        uint namespaceId,
        out uint namespaceCount,
        out NvmeIdentityTextField serialNumber,
        out NvmeIdentityTextField modelNumber)
    {
        namespaceCount = 0;
        serialNumber = default;
        modelNumber = default;

        if (identifyController.Length < IdentifyControllerLength)
        {
            return NitroNvmePlanStatus.ControllerIdentifyBufferTooSmall;
        }

        if (identifyController.Length > IdentifyControllerLength)
        {
            return NitroNvmePlanStatus.ControllerIdentifyBufferTooLarge;
        }

        namespaceCount = EndianPrimitives.ReadUInt32LittleEndian(
            identifyController.Slice(ControllerNamespaceCountOffset, sizeof(uint)));
        if (namespaceCount == 0)
        {
            return NitroNvmePlanStatus.NamespaceCountZero;
        }

        if (namespaceId == 0)
        {
            return NitroNvmePlanStatus.NamespaceIdZero;
        }

        if (namespaceId > namespaceCount)
        {
            return NitroNvmePlanStatus.NamespaceIdOutOfRange;
        }

        if (!TryParseIdentityText(
            identifyController,
            ControllerSerialNumberOffset,
            ControllerSerialNumberLength,
            out serialNumber))
        {
            return NitroNvmePlanStatus.MalformedSerialNumber;
        }

        if (!TryParseIdentityText(
            identifyController,
            ControllerModelNumberOffset,
            ControllerModelNumberLength,
            out modelNumber))
        {
            return NitroNvmePlanStatus.MalformedModelNumber;
        }

        return NitroNvmePlanStatus.Success;
    }

    private static NitroNvmePlanStatus ParseNamespaceIdentity(
        ReadOnlySpan<byte> identifyNamespace,
        uint namespaceCount,
        uint namespaceId,
        NvmeIdentityTextField serialNumber,
        NvmeIdentityTextField modelNumber,
        out NitroNvmeNamespaceIdentity namespaceIdentity)
    {
        namespaceIdentity = default;
        if (identifyNamespace.Length < IdentifyNamespaceLength)
        {
            return NitroNvmePlanStatus.NamespaceIdentifyBufferTooSmall;
        }

        if (identifyNamespace.Length > IdentifyNamespaceLength)
        {
            return NitroNvmePlanStatus.NamespaceIdentifyBufferTooLarge;
        }

        ulong totalLogicalBlocks = EndianPrimitives.ReadUInt64LittleEndian(
            identifyNamespace.Slice(NamespaceSizeOffset, sizeof(ulong)));
        if (totalLogicalBlocks == 0)
        {
            return NitroNvmePlanStatus.ZeroCapacity;
        }

        byte lbaFormatCount = (byte)(identifyNamespace[NamespaceLbaFormatCountOffset] + 1);
        byte lbaFormatIndex =
            (byte)(identifyNamespace[NamespaceFormattedLbaSizeOffset] & FormattedLbaFormatIndexMask);
        if (lbaFormatIndex >= lbaFormatCount)
        {
            return NitroNvmePlanStatus.SelectedLbaFormatOutOfRange;
        }

        int lbaFormatOffset =
            NamespaceLbaFormatTableOffset + (lbaFormatIndex * NamespaceLbaFormatEntryLength);
        byte lbaDataSizeShift =
            identifyNamespace[lbaFormatOffset + NamespaceLbaDataSizeShiftOffset];
        if (lbaDataSizeShift < MinimumLbaDataSizeShift ||
            lbaDataSizeShift > MaximumLbaDataSizeShift)
        {
            return NitroNvmePlanStatus.ImpossibleLogicalBlockSize;
        }

        ulong logicalBlockSizeBytes = 1UL << lbaDataSizeShift;
        if (totalLogicalBlocks > ulong.MaxValue / logicalBlockSizeBytes)
        {
            return NitroNvmePlanStatus.CapacityOverflow;
        }

        namespaceIdentity = new NitroNvmeNamespaceIdentity(
            namespaceCount,
            namespaceId,
            totalLogicalBlocks,
            logicalBlockSizeBytes,
            totalLogicalBlocks * logicalBlockSizeBytes,
            lbaFormatIndex,
            lbaDataSizeShift,
            serialNumber,
            modelNumber);
        return NitroNvmePlanStatus.Success;
    }

    private static bool TryParseIdentityText(
        ReadOnlySpan<byte> source,
        int offset,
        int maximumLength,
        out NvmeIdentityTextField field)
    {
        field = default;
        ReadOnlySpan<byte> raw = source.Slice(offset, maximumLength);
        int length = raw.Length;
        while (length > 0 && (raw[length - 1] == 0 || raw[length - 1] == AsciiSpace))
        {
            length--;
        }

        for (int index = 0; index < length; index++)
        {
            byte value = raw[index];
            if (value < AsciiSpace || value >= AsciiDelete)
            {
                return false;
            }
        }

        field = new NvmeIdentityTextField(offset, length, maximumLength);
        return true;
    }

    private static bool IsPowerOfTwo(ulong value)
    {
        return value != 0 && (value & (value - 1)) == 0;
    }
}
