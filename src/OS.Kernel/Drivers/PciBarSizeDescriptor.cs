namespace OS.Drivers;
public readonly struct PciBarSizeDescriptor
{
    public PciBarSizeDescriptor(byte index, PciBarKind kind, PciBarSizeDescriptorStatus status, uint rawReadback, uint rawUpperReadback, ulong baseAddressBits, ulong sizeBytes, bool isPrefetchable, bool consumesNextSlot, byte pairBarIndex) : this(index, kind, status, rawReadback, rawUpperReadback, baseAddressBits, sizeBytes, isPrefetchable, consumesNextSlot, pairBarIndex, isInitialized: true)
    {
    }

    private PciBarSizeDescriptor(byte index, PciBarKind kind, PciBarSizeDescriptorStatus status, uint rawReadback, uint rawUpperReadback, ulong baseAddressBits, ulong sizeBytes, bool isPrefetchable, bool consumesNextSlot, byte pairBarIndex, bool isInitialized)
    {
        Index = index;
        Kind = kind;
        Status = status;
        RawReadback = rawReadback;
        RawUpperReadback = rawUpperReadback;
        BaseAddressBits = baseAddressBits;
        SizeBytes = sizeBytes;
        IsPrefetchable = isPrefetchable;
        ConsumesNextSlot = consumesNextSlot;
        PairBarIndex = pairBarIndex;
        IsInitialized = isInitialized;
    }

    public byte Index { get; }
    public PciBarKind Kind { get; }
    public PciBarSizeDescriptorStatus Status { get; }
    public uint RawReadback { get; }
    public uint RawUpperReadback { get; }
    public ulong BaseAddressBits { get; }
    public ulong SizeBytes { get; }
    public bool IsPrefetchable { get; }
    public bool ConsumesNextSlot { get; }
    public byte PairBarIndex { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == PciBarSizeDescriptorStatus.Success;
}
