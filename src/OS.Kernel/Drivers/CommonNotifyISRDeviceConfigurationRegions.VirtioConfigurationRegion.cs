namespace OS.Drivers;
/// <summary>A bounded physical declaration, not evidence of an installed mapping.</summary>

public readonly struct VirtioConfigurationRegion
{
    public readonly ulong PhysicalStart, PageStart, PageCount;
    public readonly uint Bytes;
    internal VirtioConfigurationRegion(ulong start, uint bytes)
    {
        PhysicalStart = start;
        Bytes = bytes;
        PageStart = start & ~4095UL;
        PageCount = ((start & 4095) + bytes + 4095) / 4096;
    }

    public bool IsValid => Bytes != 0 && PageCount != 0 && PageCount <= 16 && PhysicalStart >= 0x100000 && PhysicalStart < 0x0000800000000000UL - Bytes && PageStart == (PhysicalStart & ~4095UL) && PageCount == ((PhysicalStart & 4095) + Bytes + 4095) / 4096;
}
