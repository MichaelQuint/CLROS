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
    /// <summary>
    /// One-use caller-owned state. Retain it and all admitted storage until
    /// user termination; do not copy a live state or mutate it concurrently.
    /// Counters separate virtual ownership, physical pool use and table cost.
    /// </summary>

    public struct State
    {
        internal Admission Memory;
        internal uint Count, Initialized;
        internal fixed ulong Regions[(int)MaximumRegions * 4];
        internal void* GuardContext;
        internal delegate* unmanaged<void*, ulong, ulong, byte> Guard;
        internal ulong GuardSelf;
        internal ImageProtection Image;
        public ulong ReservedBytes
        {
            get; internal set;
        }
        public uint BackedPages
        {
            get; internal set;
        }
        /// <summary>Maximum successfully published dynamic pool backing during
        /// this one-use state lifetime. Excludes retained static mappings and
        /// virtual-only reservations; release preserves it, rejection never
        /// advances it. Observe only under the owner's existing serialization.</summary>

        public uint PeakBackedPages
        {
            get; internal set;
        }
        public X64UserAddressSpaceResult Root
        {
            get; internal set;
        }
        public readonly bool HasMutationGuard => Guard != null;
        /// <summary>True while one owned image interval differs from its original
        /// protection. This observation grants no mutation or teardown authority.</summary>

        public readonly bool HasImageProtections => Image.Bytes != 0;
    }
}
