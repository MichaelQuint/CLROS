namespace OS.Drivers;

public readonly struct VirtioPciCandidate
{
    public VirtioPciCandidate(int sourceIndex, int segmentIndex, ushort segmentGroup, ulong ecamBasePhysicalAddress, byte segmentStartBus, byte segmentEndBus, byte bus, byte device, byte function, ushort vendorId, ushort deviceId, VirtioPciDeviceKind deviceKind, byte virtioDeviceId, byte capabilityPointer)
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
        DeviceKind = deviceKind;
        VirtioDeviceId = virtioDeviceId;
        CapabilityPointer = capabilityPointer;
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
    public VirtioPciDeviceKind DeviceKind
    {
        get;
    }
    public byte VirtioDeviceId
    {
        get;
    }
    public byte CapabilityPointer
    {
        get;
    }
}
