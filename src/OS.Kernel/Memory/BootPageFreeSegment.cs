namespace OS.Kernel.Memory;
public readonly struct BootPageFreeSegment
{
    public BootPageFreeSegment(int sourceUsableRangeIndex, ulong physicalStart, ulong physicalEndExclusive, ulong pageCount, ulong byteLength)
    {
        SourceUsableRangeIndex = sourceUsableRangeIndex;
        PhysicalStart = physicalStart;
        PhysicalEndExclusive = physicalEndExclusive;
        PageCount = pageCount;
        ByteLength = byteLength;
    }

    public int SourceUsableRangeIndex { get; }
    public ulong PhysicalStart { get; }
    public ulong PhysicalEndExclusive { get; }
    public ulong PageCount { get; }
    public ulong ByteLength { get; }
}
