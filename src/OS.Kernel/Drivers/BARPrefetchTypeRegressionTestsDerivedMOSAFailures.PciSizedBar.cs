namespace OS.Drivers;

public readonly struct PciSizedBar
{
    public readonly PciSizedBarKind Kind;
    public readonly ulong Base, Bytes;
    public readonly bool Prefetchable;
    internal PciSizedBar(PciSizedBarKind kind, ulong address, ulong bytes, bool prefetchable)
    {
        Kind = kind;
        Base = address;
        Bytes = bytes;
        Prefetchable = prefetchable;
    }

    public bool IsAssigned => Bytes != 0 && Base != 0;
}
