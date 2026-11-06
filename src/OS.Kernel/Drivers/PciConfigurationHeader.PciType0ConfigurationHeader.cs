using OS.Std.Binary;

namespace OS.Drivers;

public readonly struct PciType0ConfigurationHeader
{
    public PciType0ConfigurationHeader(ushort vendorId, ushort deviceId, ushort statusRegister, byte revisionId, byte programmingInterface, byte subclass, byte classCode, byte headerType, uint bar0, uint bar1, uint bar2, uint bar3, uint bar4, uint bar5, bool hasCapabilitiesList, byte capabilityPointer)
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

    public ushort VendorId
    {
        get;
    }
    public ushort DeviceId
    {
        get;
    }
    public ushort StatusRegister
    {
        get;
    }
    public byte RevisionId
    {
        get;
    }
    public byte ProgrammingInterface
    {
        get;
    }
    public byte Subclass
    {
        get;
    }
    public byte ClassCode
    {
        get;
    }
    public byte HeaderType
    {
        get;
    }
    public byte HeaderTypeCode => (byte)(HeaderType & PciConfigurationHeader.HeaderTypeMask);
    public bool IsMultifunction => (HeaderType & PciConfigurationHeader.HeaderTypeMultifunctionBit) != 0;
    public uint Bar0
    {
        get;
    }
    public uint Bar1
    {
        get;
    }
    public uint Bar2
    {
        get;
    }
    public uint Bar3
    {
        get;
    }
    public uint Bar4
    {
        get;
    }
    public uint Bar5
    {
        get;
    }
    public bool HasCapabilitiesList
    {
        get;
    }
    public byte CapabilityPointer
    {
        get;
    }
}
