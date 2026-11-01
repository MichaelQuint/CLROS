using OS.Std.Memory;

namespace OS.Drivers;
/// <summary>Exclusive, non-throwing access (1=success). The adapter authenticates
/// MMIO widths/mappings, coherent DMA, the real fence and PCI bus-master writes.
/// Read callbacks must reload device/shared memory on every invocation.</summary>

public unsafe struct VirtqueuePollingAccess
{
    public void* Context;
    public delegate* unmanaged<void*, ulong, byte*, byte> Read8;
    public delegate* unmanaged<void*, ulong, byte, byte> Write8;
    public delegate* unmanaged<void*, ulong, ushort*, byte> Read16;
    public delegate* unmanaged<void*, ulong, uint, byte> Write16;
    public delegate* unmanaged<void*, ulong, uint*, byte> Read32;
    public delegate* unmanaged<void*, ulong, uint, byte> Write32;
    public delegate* unmanaged<void*, byte> Fence;
    public delegate* unmanaged<void*, byte, byte> BusMaster;
}
