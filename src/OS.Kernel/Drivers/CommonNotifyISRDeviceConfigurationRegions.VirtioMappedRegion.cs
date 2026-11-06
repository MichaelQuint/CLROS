namespace OS.Drivers;
/// <summary>The adapter owns the UC/RW/NX mapping and must revoke access before
/// releasing it. Numeric binding alone does not install or authenticate mappings.</summary>

public readonly struct VirtioMappedRegion
{
    public readonly ulong PhysicalStart, VirtualStart;
    public readonly uint Bytes;
    internal VirtioMappedRegion(ulong physical, ulong address, uint bytes)
    {
        PhysicalStart = physical;
        VirtualStart = address;
        Bytes = bytes;
    }

    public bool IsValid => PhysicalStart >= 0x100000 && VirtualStart >= 4096 && Bytes != 0 && PhysicalStart < 0x0000800000000000UL - Bytes && VirtualStart < 0x0000800000000000UL - Bytes;
}
