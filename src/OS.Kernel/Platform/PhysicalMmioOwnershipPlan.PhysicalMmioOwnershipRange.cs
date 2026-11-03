using OS.Drivers;
using OS.Kernel.Memory;

namespace OS.Kernel.Platform;

public readonly struct PhysicalMmioOwnershipRange
{
    public PhysicalMmioOwnershipRange(int sourceSegmentIndex, ushort segmentGroup, byte startBus, byte endBus, ulong physicalStart, ulong physicalEndExclusive, ulong byteLength)
    {
        SourceSegmentIndex = sourceSegmentIndex;
        SegmentGroup = segmentGroup;
        StartBus = startBus;
        EndBus = endBus;
        PhysicalStart = physicalStart;
        PhysicalEndExclusive = physicalEndExclusive;
        ByteLength = byteLength;
    }

    public int SourceSegmentIndex
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
    public ulong PhysicalStart
    {
        get;
    }
    public ulong PhysicalEndExclusive
    {
        get;
    }
    public ulong ByteLength
    {
        get;
    }
    public int BusCount => EndBus - StartBus + 1;
}
