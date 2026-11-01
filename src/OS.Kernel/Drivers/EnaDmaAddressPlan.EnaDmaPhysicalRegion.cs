namespace OS.Drivers;
public readonly struct EnaDmaPhysicalRegion
{
    public EnaDmaPhysicalRegion(EnaDmaRegionKind kind, ulong basePhysicalAddress, ulong lengthBytes, uint alignment)
    {
        Kind = kind;
        BasePhysicalAddress = basePhysicalAddress;
        LengthBytes = lengthBytes;
        Alignment = alignment;
    }

    public EnaDmaRegionKind Kind { get; }
    public ulong BasePhysicalAddress { get; }
    public ulong LengthBytes { get; }
    public uint Alignment { get; }
    public bool IsBaseAligned => Alignment != 0 && (BasePhysicalAddress % Alignment) == 0;
}
