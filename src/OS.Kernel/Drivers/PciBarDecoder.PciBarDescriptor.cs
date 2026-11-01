namespace OS.Drivers;
public readonly struct PciBarDescriptor
{
    public PciBarDescriptor(byte index, PciBarKind kind, PciBarDescriptorStatus status, uint rawValue, uint rawUpperValue, ulong baseAddressBits, bool isPrefetchable, bool consumesNextSlot, byte pairBarIndex)
    {
        Index = index;
        Kind = kind;
        Status = status;
        RawValue = rawValue;
        RawUpperValue = rawUpperValue;
        BaseAddressBits = baseAddressBits;
        IsPrefetchable = isPrefetchable;
        ConsumesNextSlot = consumesNextSlot;
        PairBarIndex = pairBarIndex;
    }

    public byte Index { get; }
    public PciBarKind Kind { get; }
    public PciBarDescriptorStatus Status { get; }
    public uint RawValue { get; }
    public uint RawUpperValue { get; }
    public ulong BaseAddressBits { get; }
    public bool IsPrefetchable { get; }
    public bool ConsumesNextSlot { get; }
    public byte PairBarIndex { get; }
    public bool IsPresent => Kind == PciBarKind.Io || Kind == PciBarKind.Memory32 || Kind == PciBarKind.Memory64;
    public bool IsMemory => Kind == PciBarKind.Memory32 || Kind == PciBarKind.Memory64;
    public bool IsIo => Kind == PciBarKind.Io;
    public bool IsMalformed => Kind == PciBarKind.Malformed;
}
