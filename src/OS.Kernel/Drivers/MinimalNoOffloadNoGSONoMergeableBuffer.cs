namespace OS.Drivers;

/// <summary>One split RX/TX pair, selecting only VERSION_1. MAC, checksum, GSO,
/// mergeable buffers, event-index, indirect, packed and control queues remain
/// unnegotiated. The modern packet header is still twelve bytes.</summary>
public static unsafe class MinimalNoOffloadNoGSONoMergeableBuffer
{
    public static VirtioFeatureStatus Select(ulong offered, out ulong selected) =>
        BoundedVirtioFeatures.Select(offered, BoundedVirtioFeatures.Version1, out selected);

    public static VirtioNegotiationResult Negotiate(VirtioMappedRegion common, uint resetReads,
        void* context, delegate* unmanaged<void*, ulong, uint*, byte> read32,
        delegate* unmanaged<void*, ulong, uint, byte> write32,
        delegate* unmanaged<void*, ulong, byte*, byte> read8,
        delegate* unmanaged<void*, ulong, byte, byte> write8) =>
        ResetACKNOWLEDGEDRIVERFEATURESOKNegotiation.Run(common, BoundedVirtioFeatures.Version1,
            resetReads, context, read32, write32, read8, write8);
}
