namespace OS.Drivers;
public readonly struct PciScannedDevice
{
    public PciScannedDevice(byte bus, byte device, byte function, ushort vendorId, ushort deviceId, byte revisionId, byte classCode, byte subclass, byte programmingInterface, byte headerType, bool isMultifunction, bool hasCapabilitiesList, byte capabilityPointer)
    {
        Bus = bus;
        Device = device;
        Function = function;
        VendorId = vendorId;
        DeviceId = deviceId;
        RevisionId = revisionId;
        ClassCode = classCode;
        Subclass = subclass;
        ProgrammingInterface = programmingInterface;
        HeaderType = headerType;
        IsMultifunction = isMultifunction;
        HasCapabilitiesList = hasCapabilitiesList;
        CapabilityPointer = capabilityPointer;
    }

    public byte Bus { get; }
    public byte Device { get; }
    public byte Function { get; }
    public ushort VendorId { get; }
    public ushort DeviceId { get; }
    public byte RevisionId { get; }
    public byte ClassCode { get; }
    public byte Subclass { get; }
    public byte ProgrammingInterface { get; }
    public byte HeaderType { get; }
    public bool IsMultifunction { get; }
    public bool HasCapabilitiesList { get; }
    public byte CapabilityPointer { get; }
}
