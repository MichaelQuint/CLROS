namespace OS.Drivers;

public readonly struct VirtioPciBindingMetadata
{
    internal VirtioPciBindingMetadata(int segmentIndex, PciEcamSegment segment, byte bus, byte device, byte function, byte capabilityPointer, PciDeviceIdentity identity, VirtioPciDeviceKind deviceKind, byte virtioDeviceId, VirtioPciCapabilitySet capabilities, PciBarDescriptorSet bars, VirtioPciBarApertureSet apertures)
    {
        SegmentIndex = segmentIndex;
        Segment = segment;
        Bus = bus;
        Device = device;
        Function = function;
        CapabilityPointer = capabilityPointer;
        Identity = identity;
        DeviceKind = deviceKind;
        VirtioDeviceId = virtioDeviceId;
        Capabilities = capabilities;
        Bars = bars;
        Apertures = apertures;
    }

    public int SegmentIndex
    {
        get;
    }
    public PciEcamSegment Segment
    {
        get;
    }
    public ulong EcamBasePhysicalAddress => Segment.BasePhysicalAddress;
    public ushort SegmentGroup => Segment.SegmentGroup;
    public byte SegmentStartBus => Segment.StartBus;
    public byte SegmentEndBus => Segment.EndBus;
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
    public byte CapabilityPointer
    {
        get;
    }
    public PciDeviceIdentity Identity
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
    public VirtioPciCapabilitySet Capabilities
    {
        get;
    }
    public PciBarDescriptorSet Bars
    {
        get;
    }
    public VirtioPciBarApertureSet Apertures
    {
        get;
    }
}
