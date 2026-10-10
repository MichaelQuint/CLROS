namespace OS.Drivers;

public enum PciSizedBarKind : byte { Uninitialized, Absent, Memory32, Memory64, UpperHalf, Io32 }

public readonly struct PciSizedBar
{
    public readonly PciSizedBarKind Kind;
    public readonly ulong Base, Bytes;
    public readonly bool Prefetchable;
    internal PciSizedBar(PciSizedBarKind kind, ulong address, ulong bytes, bool prefetchable)
    { Kind = kind; Base = address; Bytes = bytes; Prefetchable = prefetchable; }
    public bool IsAssigned => Bytes != 0 && Base != 0;
}

/// <summary>
/// Independent BAR bit/extent checks. In particular prefetch is bit 3 != 0,
/// memory type is bits 2:1, and a 64-bit BAR consumes its following slot.
/// No reference implementation is copied or required at runtime.
/// </summary>
public static class BARPrefetchTypeRegressionTestsDerivedMOSAFailures
{
    public static PciBarProbeStatus Format(uint low, uint slot, out PciSizedBarKind kind)
    {
        kind = PciSizedBarKind.Uninitialized;
        if (slot >= 6) return PciBarProbeStatus.InvalidInput;
        if ((low & 1) != 0)
        {
            if ((low & 2) != 0) return PciBarProbeStatus.UnsupportedType;
            kind = PciSizedBarKind.Io32;
        }
        else if ((low & 6) == 0) kind = PciSizedBarKind.Memory32;
        else if ((low & 6) == 4 && slot < 5) kind = PciSizedBarKind.Memory64;
        else return PciBarProbeStatus.UnsupportedType;
        return PciBarProbeStatus.Success;
    }

    public static PciBarProbeStatus Decode(uint low, uint high, uint maskLow, uint maskHigh,
        uint slot, out PciSizedBar bar)
    {
        bar = default;
        var status = Format(low, slot, out var kind);
        if (status != PciBarProbeStatus.Success) return status;
        uint flags = kind == PciSizedBarKind.Io32 ? 3u : 15u;
        if ((low & flags) != (maskLow & flags)) return PciBarProbeStatus.InvalidMask;
        ulong address = low & ~flags, mask = maskLow & ~flags;
        bool wide = kind == PciSizedBarKind.Memory64;
        if (wide) { address |= (ulong)high << 32; mask |= (ulong)maskHigh << 32; }
        if (mask == 0)
        {
            if (address != 0 || wide) return PciBarProbeStatus.InvalidMask;
            bar = new(PciSizedBarKind.Absent, 0, 0, false);
            return PciBarProbeStatus.Success;
        }
        ulong size = wide ? unchecked(~mask + 1) : (ulong)unchecked((uint)(~(uint)mask + 1));
        if (size == 0 || (size & (size - 1)) != 0 || size < (kind == PciSizedBarKind.Io32 ? 4UL : 16UL))
            return PciBarProbeStatus.InvalidMask;
        if ((address & (size - 1)) != 0 || address > ~0UL - size || (!wide && address + size > 0x100000000UL))
            return PciBarProbeStatus.InvalidExtent;
        bar = new(kind, address, size, kind != PciSizedBarKind.Io32 && (low & 8) != 0);
        return PciBarProbeStatus.Success;
    }
}
