namespace OS.Drivers;
/// <summary>Numeric MCFG authority, not proof that memory has been mapped.</summary>

public readonly struct BoundedPciWindow
{
    public readonly ulong BusZeroBase, Start, Bytes;
    public readonly ushort Segment;
    public readonly byte AllocationFirst, AllocationLast, FirstBus, LastBus;
    internal BoundedPciWindow(ulong origin, ushort segment, byte first, byte last, byte start, byte end)
    {
        BusZeroBase = origin;
        Segment = segment;
        AllocationFirst = first;
        AllocationLast = last;
        FirstBus = start;
        LastBus = end;
        Start = origin + ((ulong)start << 20);
        Bytes = ((ulong)end - start + 1) << 20;
    }

    public bool IsValid => BusZeroBase >= 0x100000 && (BusZeroBase & 0xfffff) == 0 && AllocationFirst <= FirstBus && FirstBus <= LastBus && LastBus <= AllocationLast && LastBus - FirstBus < 4 && BusZeroBase <= 0x100000000UL - (((ulong)AllocationLast + 1) << 20) && Start == BusZeroBase + ((ulong)FirstBus << 20) && Bytes == ((ulong)LastBus - FirstBus + 1) << 20;
}
