namespace OS.Drivers;

public readonly struct PciEcamSegment
{
    public PciEcamSegment(ulong basePhysicalAddress, ushort segmentGroup, byte startBus, byte endBus)
    {
        BasePhysicalAddress = basePhysicalAddress;
        SegmentGroup = segmentGroup;
        StartBus = startBus;
        EndBus = endBus;
    }

    public ulong BasePhysicalAddress
    {
        get;
    }
    public ushort SegmentGroup
    {
        get;
    }
    public byte StartBus
    {
        get;
    }
    public byte EndBus
    {
        get;
    }
}
