namespace OS.Drivers;
public readonly struct EnaAdminQueueRegionPlan
{
    internal EnaAdminQueueRegionPlan(uint offset, uint length, uint depth, uint entrySize, uint alignment)
    {
        Offset = offset;
        Length = length;
        Depth = depth;
        EntrySize = entrySize;
        Alignment = alignment;
        EndOffset = offset + length;
    }

    public uint Offset { get; }
    public uint Length { get; }
    public uint Depth { get; }
    public uint EntrySize { get; }
    public uint Alignment { get; }
    public uint EndOffset { get; }
    public bool IsAligned => Alignment != 0 && (Offset % Alignment) == 0;
}
