using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxContinuingWritableDescriptorServices
{
    /// <summary>Copied limits for one optional forward candidate search.
    /// No wrap, automatic continuation or reservation follows. A budget/end
    /// result returns EAGAIN with its distinct backend failure; it never proves
    /// whole-volume ENOSPC. Actual growth still checks both FAT copies again.</summary>

    public readonly struct FreeSearch(uint firstCluster, uint clusterBudget, uint sectorReadBudget)
    {
        public readonly uint FirstCluster = firstCluster, ClusterBudget = clusterBudget, SectorReadBudget = sectorReadBudget;
    }
#if !NOSTDLIB_EFI || M16_027_TIMING_PROBE
#endif
}
