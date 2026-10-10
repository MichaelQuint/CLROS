using OS.Drivers;

namespace OS.Storage;

/// <summary>Bounded single-queue block feature policy. VERSION_1 is required and
/// RO is accepted if offered. Capacity/requests are separate contracts; absence
/// of RO is not permission for this profile to submit writes.</summary>
public static unsafe class MinimalVirtioBlkFeatureSet
{
    public static VirtioFeatureStatus Select(ulong offered, out ulong selected) =>
        BoundedVirtioFeatures.SelectBlock(offered, out selected);

    public static VirtioNegotiationResult Negotiate(VirtioMappedRegion common, uint resetReads,
        void* context, delegate* unmanaged<void*, ulong, uint*, byte> read32,
        delegate* unmanaged<void*, ulong, uint, byte> write32,
        delegate* unmanaged<void*, ulong, byte*, byte> read8,
        delegate* unmanaged<void*, ulong, byte, byte> write8) =>
        ResetACKNOWLEDGEDRIVERFEATURESOKNegotiation.RunBlock(common, resetReads,
            context, read32, write32, read8, write8);
}
