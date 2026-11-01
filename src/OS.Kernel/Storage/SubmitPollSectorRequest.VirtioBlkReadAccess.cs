using OS.Drivers;

namespace OS.Storage;

/// <summary>Queue access plus an explicit bus-master enable/readback callback.
/// Callbacks return exactly 1 on success and never throw. The adapter authenticates
/// exclusive device ownership, mapped UC MMIO and coherent WB DMA backing.</summary>
public unsafe struct VirtioBlkReadAccess
{
    public VirtioBlkQueueAccess Queue;
    public delegate* unmanaged<void*, byte> BusMasterOn;
    // All null preserves polling. Otherwise all three must be present. Prepare
    // admits and masks MSI-X before queue vector zero is configured. Wait masks
    // delivery and restores IF/IDTR before returning an authenticated route.
    // Stop disables/readbacks MSI-X, including after a failed Prepare or Wait.
    public delegate* unmanaged<void*, VirtioMappedRegion, byte> PrepareInterrupt;
    public delegate* unmanaged<void*, uint, byte> WaitInterrupt;
    public delegate* unmanaged<void*, byte> StopInterrupt;
}
