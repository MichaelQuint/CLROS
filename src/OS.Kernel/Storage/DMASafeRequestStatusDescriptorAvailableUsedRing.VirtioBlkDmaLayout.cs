using OS.Drivers;
using OS.Kernel.Platform;

namespace OS.Storage;

/// <summary>Numeric one-page layout. CPU, physical and device bases retain their
/// distinct domains; the adapter authenticates coherent WB/RW/NX mappings.</summary>
public readonly struct VirtioBlkDmaLayout
{
    public const ushort QueueSize = 8;
    public const uint PageBytes = 4096, RequestOffset = 256, RequestBytes = 16,
        StatusOffset = 272, StatusBytes = 1, DataOffset = 512, DataBytes = 512;
    // Explicit continuing reads may use more of the same retained data area.
    // DataBytes remains the legacy single-sector wire contract.
    public const uint MaximumReadSectors = 4, MaximumReadBytes = MaximumReadSectors * DataBytes;
    public readonly DmaAddressRange Range;
    public readonly VirtqueueSplitRingPlan Rings;
    public bool IsInitialized => Range.IsInitialized && Rings.QueueSize == QueueSize;
    internal VirtioBlkDmaLayout(DmaAddressRange range, VirtqueueSplitRingPlan rings)
    {
        Range = range;
        Rings = rings;
    }
}
