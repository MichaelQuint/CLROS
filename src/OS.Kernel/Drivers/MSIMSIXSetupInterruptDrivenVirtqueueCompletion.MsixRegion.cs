using OS.Std.Binary;

namespace OS.Drivers;
public readonly struct MsixRegion
{
    public readonly ulong PhysicalStart, PageStart;
    public readonly uint Bytes, PageCount;
    internal MsixRegion(ulong start, uint bytes)
    {
        PhysicalStart = start;
        Bytes = bytes;
        PageStart = start & ~4095UL;
        PageCount = (uint)(((start & 4095) + bytes + 4095) / 4096);
    }

    public bool IsValid => Bytes != 0 && Bytes <= 32768 && PhysicalStart >= 0x100000 && (PhysicalStart & 7) == 0 && PhysicalStart <= 0x0000800000000000UL - Bytes && PageStart == (PhysicalStart & ~4095UL) && PageCount == ((PhysicalStart & 4095) + Bytes + 4095) / 4096;
}
