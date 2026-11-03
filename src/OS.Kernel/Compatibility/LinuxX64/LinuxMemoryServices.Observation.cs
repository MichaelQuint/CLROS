using OS.Kernel.Memory;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxMemoryServices
{
#if M16_014_PROBE

#else
#endif
#if M10_004_PROBE

#else
#endif
    /// <summary>Copied physical-pool and metadata occupancy from one audited
    /// serialized owner. Free runs are disjoint physical extents, independent
    /// of virtual holes; retained PROT_NONE backing stays occupied. Table use
    /// includes the actual static and dynamic root. These values grant no
    /// allocation, consumer classification, partition or execution authority.</summary>

    public readonly struct Observation
    {
        internal Observation(uint pool, uint backed, uint free, uint runs, uint largest, uint regions, uint tables, uint tableCapacity)
        {
            PoolPages = pool;
            BackedPages = backed;
            FreePages = free;
            FreeRuns = runs;
            LargestFreeRunPages = largest;
            DynamicRegions = regions;
            RegionCapacity = MaximumRegions;
            UsedTables = tables;
            TableCapacity = tableCapacity;
        }

        /// <summary>Actual admitted pool capacity and occupied/free 4KiB pages;
        /// retained static mappings are outside this pool.</summary>

        public readonly uint PoolPages, BackedPages, FreePages;
        /// <summary>Number of maximal free physical runs and largest run in
        /// 4KiB pages. Both are zero when the pool has no free pages.</summary>

        public readonly uint FreeRuns, LargestFreeRunPages;
        /// <summary>Published dynamic region inventory and its compile-time limit.</summary>

        public readonly uint DynamicRegions, RegionCapacity;
        /// <summary>Current audited Root.UsedTables and admitted table arena capacity.</summary>

        public readonly uint UsedTables, TableCapacity;
    }
}
