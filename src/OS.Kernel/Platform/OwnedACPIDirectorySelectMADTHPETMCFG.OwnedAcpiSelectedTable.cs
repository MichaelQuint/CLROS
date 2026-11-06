namespace OS.Kernel.Platform;
/// <summary>Metadata into the caller's retained bytes, never a physical-memory view.</summary>

public readonly struct OwnedAcpiSelectedTable
{
    public readonly ulong PhysicalAddress;
    public readonly ulong Offset;
    public readonly ulong Length;
    public readonly ulong RootIndex;
    internal OwnedAcpiSelectedTable(ulong physicalAddress, ulong offset, ulong length, ulong rootIndex)
    {
        PhysicalAddress = physicalAddress;
        Offset = offset;
        Length = length;
        RootIndex = rootIndex;
    }
}
