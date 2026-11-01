using OS.Std.Memory;

namespace OS.Drivers;
/// <summary>Explicit one-shot interrupt completion. Wait must arm the admitted
/// MSI-X source, observe a real returning handler, and restore IF=0 and the owned
/// original IDTR on every return. Mask must disable that source; 1 means success.
/// These callbacks do not replace the required coherent-DMA/MMIO/fence authority.</summary>

public unsafe struct VirtqueueInterruptAccess
{
    public void* Context;
    public delegate* unmanaged<void*, uint, byte> Wait;
    public delegate* unmanaged<void*, byte> Mask;
}
