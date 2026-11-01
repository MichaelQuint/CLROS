using OS.Boot;

namespace OS.Kernel.Memory;
public readonly struct BootMemoryReservedRange
{
    public BootMemoryReservedRange(int sourceRegionIndex, uint uefiType, ulong physicalStart, ulong physicalEndExclusive, ulong pageCount, ulong byteLength, ulong attribute)
    {
        SourceRegionIndex = sourceRegionIndex;
        UefiType = uefiType;
        PhysicalStart = physicalStart;
        PhysicalEndExclusive = physicalEndExclusive;
        PageCount = pageCount;
        ByteLength = byteLength;
        Attribute = attribute;
    }

    public int SourceRegionIndex { get; }
    public uint UefiType { get; }
    public ulong PhysicalStart { get; }
    public ulong PhysicalEndExclusive { get; }
    public ulong PageCount { get; }
    public ulong ByteLength { get; }
    public ulong Attribute { get; }
}
