namespace OS.Drivers;

public readonly struct VirtqueueSplitRingRegion
{
    internal VirtqueueSplitRingRegion(uint offset, uint length, uint alignment)
    {
        Offset = offset;
        Length = length;
        Alignment = alignment;
        EndOffset = offset + length;
    }

    public uint Offset
    {
        get;
    }
    public uint Length
    {
        get;
    }
    public uint Alignment
    {
        get;
    }
    public uint EndOffset
    {
        get;
    }
    public bool IsAligned => Alignment != 0 && (Offset % Alignment) == 0;
}
