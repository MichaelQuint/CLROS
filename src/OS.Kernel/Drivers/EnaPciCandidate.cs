namespace OS.Drivers;

public readonly struct EnaPciCandidate
{
    public EnaPciCandidate(int sourceIndex, int segmentIndex, ushort segmentGroup, ulong ecamBasePhysicalAddress, byte segmentStartBus, byte segmentEndBus, byte bus, byte device, byte function, ushort vendorId, ushort deviceId, byte revisionId, byte classCode, byte subclass, byte programmingInterface, byte headerType, byte capabilityPointer, EnaPciDeviceKind deviceKind)
    {
        SourceIndex = sourceIndex;
        SegmentIndex = segmentIndex;
        SegmentGroup = segmentGroup;
        EcamBasePhysicalAddress = ecamBasePhysicalAddress;
        SegmentStartBus = segmentStartBus;
        SegmentEndBus = segmentEndBus;
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
        CapabilityPointer = capabilityPointer;
        DeviceKind = deviceKind;
    }

    public int SourceIndex
    {
        get;
    }
    public int SegmentIndex
    {
        get;
    }
    public ushort SegmentGroup
    {
        get;
    }
    public ulong EcamBasePhysicalAddress
    {
        get;
    }
    public byte SegmentStartBus
    {
        get;
    }
    public byte SegmentEndBus
    {
        get;
    }
    public byte Bus
    {
        get;
    }
    public byte Device
    {
        get;
    }
    public byte Function
    {
        get;
    }
    public ushort VendorId
    {
        get;
    }
    public ushort DeviceId
    {
        get;
    }
    public byte RevisionId
    {
        get;
    }
    public byte ClassCode
    {
        get;
    }
    public byte Subclass
    {
        get;
    }
    public byte ProgrammingInterface
    {
        get;
    }
    public byte HeaderType
    {
        get;
    }
    public byte CapabilityPointer
    {
        get;
    }
    public EnaPciDeviceKind DeviceKind
    {
        get;
    }
}
