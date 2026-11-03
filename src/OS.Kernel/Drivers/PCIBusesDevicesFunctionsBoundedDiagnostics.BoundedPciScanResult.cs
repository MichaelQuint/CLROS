namespace OS.Drivers;

public struct BoundedPciScanResult
{
    public BoundedPciStatus Status;
    public uint Count, Visited, RequiredCapacity;
    public ushort LastSegment;
    public byte LastBus, LastDevice, LastFunction;
    public bool HasLast, CoversAllocation, HasUnscannedBridge;
    public readonly bool Complete => Status == BoundedPciStatus.Success;
    // Complete refers only to this requested window, never the entire PCI topology.
    public readonly bool CanEstablishAbsenceInAllocation => Complete && CoversAllocation && !HasUnscannedBridge;
}
