using OS.Std.Binary;

namespace OS.Drivers;

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

        ushort vendorId = EndianPrimitives.ReadUInt16LittleEndian(configurationSpace.Slice(VendorIdOffset, sizeof(ushort)));
        if (vendorId == InvalidVendorId)
        {
            return PciConfigurationHeaderResult.Fail(PciConfigurationHeaderStatus.InvalidVendor);
        }

        byte headerType = configurationSpace[HeaderTypeOffset];
        if ((headerType & HeaderTypeMask) != Type0HeaderType)
        {
            return PciConfigurationHeaderResult.Fail(PciConfigurationHeaderStatus.UnsupportedHeaderType);
        }

        ushort statusRegister = EndianPrimitives.ReadUInt16LittleEndian(configurationSpace.Slice(StatusRegisterOffset, sizeof(ushort)));
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

        PciType0ConfigurationHeader header = new PciType0ConfigurationHeader(vendorId, EndianPrimitives.ReadUInt16LittleEndian(configurationSpace.Slice(DeviceIdOffset, sizeof(ushort))), statusRegister, configurationSpace[RevisionIdOffset], configurationSpace[ProgrammingInterfaceOffset], configurationSpace[SubclassOffset], configurationSpace[ClassCodeOffset], headerType, EndianPrimitives.ReadUInt32LittleEndian(configurationSpace.Slice(Bar0Offset, sizeof(uint))), EndianPrimitives.ReadUInt32LittleEndian(configurationSpace.Slice(Bar1Offset, sizeof(uint))), EndianPrimitives.ReadUInt32LittleEndian(configurationSpace.Slice(Bar2Offset, sizeof(uint))), EndianPrimitives.ReadUInt32LittleEndian(configurationSpace.Slice(Bar3Offset, sizeof(uint))), EndianPrimitives.ReadUInt32LittleEndian(configurationSpace.Slice(Bar4Offset, sizeof(uint))), EndianPrimitives.ReadUInt32LittleEndian(configurationSpace.Slice(Bar5Offset, sizeof(uint))), hasCapabilitiesList, capabilityPointer);
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
