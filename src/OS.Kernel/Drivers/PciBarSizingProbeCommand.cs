namespace OS.Drivers;
public readonly struct PciBarSizingProbeCommand
{
    public PciBarSizingProbeCommand(PciBarSizingProbeCommandKind kind, ushort sequenceIndex, byte barIndex, byte pairBarIndex, ushort registerOffset, uint value, bool isUpperPart, PciBarKind barKind)
    {
        Kind = kind;
        SequenceIndex = sequenceIndex;
        BarIndex = barIndex;
        PairBarIndex = pairBarIndex;
        RegisterOffset = registerOffset;
        Value = value;
        IsUpperPart = isUpperPart;
        BarKind = barKind;
    }

    public PciBarSizingProbeCommandKind Kind { get; }
    public ushort SequenceIndex { get; }
    public byte BarIndex { get; }
    public byte PairBarIndex { get; }
    public ushort RegisterOffset { get; }
    public byte AccessWidthBytes => sizeof(uint);
    public uint Value { get; }
    public bool IsUpperPart { get; }
    public PciBarKind BarKind { get; }
}
