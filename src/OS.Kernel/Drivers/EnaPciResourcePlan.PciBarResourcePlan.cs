using OS.Std.Binary;

namespace OS.Drivers;
public readonly struct PciBarResourcePlan
{
    internal PciBarResourcePlan(byte barIndex, PciBarKind kind, ulong baseAddress, ulong apertureLength, bool isPrefetchable)
    {
        BarIndex = barIndex;
        Kind = kind;
        BaseAddress = baseAddress;
        ApertureLength = apertureLength;
        IsPrefetchable = isPrefetchable;
        EndAddressExclusive = baseAddress + apertureLength;
    }

    public byte BarIndex { get; }
    public PciBarKind Kind { get; }
    public ulong BaseAddress { get; }
    public ulong ApertureLength { get; }
    public bool IsPrefetchable { get; }
    public ulong EndAddressExclusive { get; }
}
