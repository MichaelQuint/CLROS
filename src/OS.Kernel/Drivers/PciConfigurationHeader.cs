using OS.Std.Binary;

namespace OS.Drivers;

public enum PciConfigurationHeaderStatus
{
    Success = 0,
    BufferTooSmall = 1,
    InvalidVendor = 2,
    UnsupportedHeaderType = 3,
    InvalidCapabilityPointerAlignment = 4,
    InvalidCapabilityPointerRange = 5
}

public readonly struct PciType0ConfigurationHeader
{
    public PciType0ConfigurationHeader(
        ushort vendorId,
        ushort deviceId,
        ushort statusRegister,
        byte revisionId,
        byte programmingInterface,
        byte subclass,
        byte classCode,
        byte headerType,
        uint bar0,
        uint bar1,
        uint bar2,
        uint bar3,
        uint bar4,
        uint bar5,
        bool hasCapabilitiesList,
        byte capabilityPointer)
    {
        VendorId = vendorId;
        DeviceId = deviceId;
        StatusRegister = statusRegister;
        RevisionId = revisionId;
        ProgrammingInterface = programmingInterface;
        Subclass = subclass;
        ClassCode = classCode;
        HeaderType = headerType;
        Bar0 = bar0;
        Bar1 = bar1;
        Bar2 = bar2;
        Bar3 = bar3;
        Bar4 = bar4;
        Bar5 = bar5;
        HasCapabilitiesList = hasCapabilitiesList;
        CapabilityPointer = capabilityPointer;
    }

    public ushort VendorId { get; }

    public ushort DeviceId { get; }

    public ushort StatusRegister { get; }

    public byte RevisionId { get; }

    public byte ProgrammingInterface { get; }

    public byte Subclass { get; }

    public byte ClassCode { get; }

    public byte HeaderType { get; }

    public byte HeaderTypeCode => (byte)(HeaderType & PciConfigurationHeader.HeaderTypeMask);

    public bool IsMultifunction => (HeaderType & PciConfigurationHeader.HeaderTypeMultifunctionBit) != 0;

    public uint Bar0 { get; }

    public uint Bar1 { get; }

    public uint Bar2 { get; }

    public uint Bar3 { get; }

    public uint Bar4 { get; }

    public uint Bar5 { get; }

    public bool HasCapabilitiesList { get; }

    public byte CapabilityPointer { get; }
}

public readonly struct PciConfigurationHeaderResult
{
    private PciConfigurationHeaderResult(
        bool isInitialized,
        PciConfigurationHeaderStatus status,
        PciType0ConfigurationHeader header)
    {
        IsInitialized = isInitialized;
        Status = status;
        Header = header;
    }

    public bool IsInitialized { get; }

    public PciConfigurationHeaderStatus Status { get; }

    public PciType0ConfigurationHeader Header { get; }

    public bool Succeeded =>
        IsInitialized && Status == PciConfigurationHeaderStatus.Success;

    public static PciConfigurationHeaderResult Success(PciType0ConfigurationHeader header)
    {
        return new PciConfigurationHeaderResult(
            true,
            PciConfigurationHeaderStatus.Success,
            header);
    }

    public static PciConfigurationHeaderResult Fail(PciConfigurationHeaderStatus status)
    {
        return new PciConfigurationHeaderResult(true, status, default);
    }
}

public static class PciConfigurationHeader
{
    public const int Type0HeaderLength = 64;
    public const byte HeaderTypeMask = 0x7F;
    public const byte HeaderTypeMultifunctionBit = 0x80;
    public const byte Type0HeaderType = 0;
    public const ushort InvalidVendorId = 0xFFFF;
    public const ushort StatusCapabilitiesListBit = 0x0010;
    public const byte FirstCapabilityPointer = 0x40;
    public const byte LastCapabilityPointer = 0xFC;

    private const int VendorIdOffset = 0x00;
    private const int DeviceIdOffset = 0x02;
    private const int StatusRegisterOffset = 0x06;
    private const int RevisionIdOffset = 0x08;
    private const int ProgrammingInterfaceOffset = 0x09;
    private const int SubclassOffset = 0x0A;
    private const int ClassCodeOffset = 0x0B;
    private const int HeaderTypeOffset = 0x0E;
    private const int Bar0Offset = 0x10;
    private const int Bar1Offset = 0x14;
    private const int Bar2Offset = 0x18;
    private const int Bar3Offset = 0x1C;
    private const int Bar4Offset = 0x20;
    private const int Bar5Offset = 0x24;
    private const int CapabilityPointerOffset = 0x34;

    public static PciConfigurationHeaderResult ParseType0(ReadOnlySpan<byte> configurationSpace)
    {
        if (configurationSpace.Length < Type0HeaderLength)
        {
            return PciConfigurationHeaderResult.Fail(PciConfigurationHeaderStatus.BufferTooSmall);
        }

        ushort vendorId = EndianPrimitives.ReadUInt16LittleEndian(
            configurationSpace.Slice(VendorIdOffset, sizeof(ushort)));
        if (vendorId == InvalidVendorId)
        {
            return PciConfigurationHeaderResult.Fail(PciConfigurationHeaderStatus.InvalidVendor);
        }

        byte headerType = configurationSpace[HeaderTypeOffset];
        if ((headerType & HeaderTypeMask) != Type0HeaderType)
        {
            return PciConfigurationHeaderResult.Fail(PciConfigurationHeaderStatus.UnsupportedHeaderType);
        }

        ushort statusRegister = EndianPrimitives.ReadUInt16LittleEndian(
            configurationSpace.Slice(StatusRegisterOffset, sizeof(ushort)));
        bool hasCapabilitiesList = (statusRegister & StatusCapabilitiesListBit) != 0;
        byte capabilityPointer = 0;
        if (hasCapabilitiesList)
        {
            capabilityPointer = configurationSpace[CapabilityPointerOffset];
            PciConfigurationHeaderStatus capabilityPointerStatus = ValidateCapabilityPointer(capabilityPointer);
            if (capabilityPointerStatus != PciConfigurationHeaderStatus.Success)
            {
                return PciConfigurationHeaderResult.Fail(capabilityPointerStatus);
            }
        }

        PciType0ConfigurationHeader header = new PciType0ConfigurationHeader(
            vendorId,
            EndianPrimitives.ReadUInt16LittleEndian(configurationSpace.Slice(DeviceIdOffset, sizeof(ushort))),
            statusRegister,
            configurationSpace[RevisionIdOffset],
            configurationSpace[ProgrammingInterfaceOffset],
            configurationSpace[SubclassOffset],
            configurationSpace[ClassCodeOffset],
            headerType,
            EndianPrimitives.ReadUInt32LittleEndian(configurationSpace.Slice(Bar0Offset, sizeof(uint))),
            EndianPrimitives.ReadUInt32LittleEndian(configurationSpace.Slice(Bar1Offset, sizeof(uint))),
            EndianPrimitives.ReadUInt32LittleEndian(configurationSpace.Slice(Bar2Offset, sizeof(uint))),
            EndianPrimitives.ReadUInt32LittleEndian(configurationSpace.Slice(Bar3Offset, sizeof(uint))),
            EndianPrimitives.ReadUInt32LittleEndian(configurationSpace.Slice(Bar4Offset, sizeof(uint))),
            EndianPrimitives.ReadUInt32LittleEndian(configurationSpace.Slice(Bar5Offset, sizeof(uint))),
            hasCapabilitiesList,
            capabilityPointer);

        return PciConfigurationHeaderResult.Success(header);
    }

    private static PciConfigurationHeaderStatus ValidateCapabilityPointer(byte capabilityPointer)
    {
        if ((capabilityPointer & 0x03) != 0)
        {
            return PciConfigurationHeaderStatus.InvalidCapabilityPointerAlignment;
        }

        if (capabilityPointer < FirstCapabilityPointer || capabilityPointer > LastCapabilityPointer)
        {
            return PciConfigurationHeaderStatus.InvalidCapabilityPointerRange;
        }

        return PciConfigurationHeaderStatus.Success;
    }
}
