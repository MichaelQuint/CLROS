using OS.Drivers;

namespace OS.Storage;

/// <summary>Exclusive nonthrowing MMIO access; 1 means success. BusMasterOff must
/// disable and read back PCI bus mastering and INTx. Queue-only use requires
/// MSI-X disabled; an admitted IRQ adapter may instead hold delivery masked
/// during configuration and must disable it before cleanup/token release.
/// Fence is the admitted real DMA/MMIO fence. Numeric callbacks are not proof of
/// those properties; the platform adapter owns mappings and device authority.</summary>
public unsafe struct VirtioBlkQueueAccess
{
    public void* Context;
    public delegate* unmanaged<void*, ulong, byte*, byte> Read8;
    public delegate* unmanaged<void*, ulong, byte, byte> Write8;
    public delegate* unmanaged<void*, ulong, ushort*, byte> Read16;
    public delegate* unmanaged<void*, ulong, uint, byte> Write16;
    public delegate* unmanaged<void*, ulong, uint*, byte> Read32;
    public delegate* unmanaged<void*, ulong, uint, byte> Write32;
    public delegate* unmanaged<void*, byte> Fence, BusMasterOff;
}
