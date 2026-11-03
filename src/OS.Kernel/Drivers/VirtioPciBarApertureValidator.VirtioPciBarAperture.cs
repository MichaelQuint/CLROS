namespace OS.Drivers;

public readonly struct VirtioPciBarAperture
{
    internal VirtioPciBarAperture(VirtioPciCapabilityType capabilityType, byte barIndex, PciBarKind barKind, ulong barBaseAddressBits, uint offset, uint length, ulong endOffset, ulong barSize, uint notifyOffsetMultiplier)
    {
        CapabilityType = capabilityType;
        BarIndex = barIndex;
        BarKind = barKind;
        BarBaseAddressBits = barBaseAddressBits;
        Offset = offset;
        Length = length;
        EndOffset = endOffset;
        BarSize = barSize;
        NotifyOffsetMultiplier = notifyOffsetMultiplier;
    }

    public VirtioPciCapabilityType CapabilityType
    {
        get;
    }
    public byte BarIndex
    {
        get;
    }
    public PciBarKind BarKind
    {
        get;
    }
    public ulong BarBaseAddressBits
    {
        get;
    }
    public uint Offset
    {
        get;
    }
    public uint Length
    {
        get;
    }
    public ulong EndOffset
    {
        get;
    }
    public ulong BarSize
    {
        get;
    }
    public uint NotifyOffsetMultiplier
    {
        get;
    }
}
