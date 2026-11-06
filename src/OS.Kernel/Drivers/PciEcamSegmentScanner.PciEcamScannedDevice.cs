namespace OS.Drivers;

public readonly struct PciEcamScannedDevice
{
    public PciEcamScannedDevice(int segmentIndex, PciEcamSegment segment, PciScannedDevice device)
    {
        SegmentIndex = segmentIndex;
        Segment = segment;
        Device = device;
    }

    public int SegmentIndex
    {
        get;
    }
    public PciEcamSegment Segment
    {
        get;
    }
    public PciScannedDevice Device
    {
        get;
    }
    public ulong EcamBasePhysicalAddress => Segment.BasePhysicalAddress;
    public ushort SegmentGroup => Segment.SegmentGroup;
    public byte SegmentStartBus => Segment.StartBus;
    public byte SegmentEndBus => Segment.EndBus;
    public byte Bus => Device.Bus;
    public byte DeviceNumber => Device.Device;
    public byte Function => Device.Function;
    public ushort VendorId => Device.VendorId;
    public ushort DeviceId => Device.DeviceId;
    public byte RevisionId => Device.RevisionId;
    public byte ClassCode => Device.ClassCode;
    public byte Subclass => Device.Subclass;
    public byte ProgrammingInterface => Device.ProgrammingInterface;
    public byte HeaderType => Device.HeaderType;
    public bool IsMultifunction => Device.IsMultifunction;
    public bool HasCapabilitiesList => Device.HasCapabilitiesList;
    public byte CapabilityPointer => Device.CapabilityPointer;
}
