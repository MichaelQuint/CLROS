using OS.Kernel.Memory;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxMemoryServices
{
    /// <summary>
    /// Borrowed low-4GiB physical pool and target table arena, their kernel
    /// aliases, distinct scratch tables and immutable non-pool static mappings.
    /// ReservationBytes is an independent virtual quota, not physical capacity.
    /// The owner proves physical allocation, inactive-root authority and lifetime.
    /// Optional ImageVirtualStart/ImagePhysicalStart/ImageBytes identify its
    /// exclusively retained, page-aligned ELF image, excluding the initial stack.
    /// All zero disables image protection changes. Original image rows retain
    /// exact linear physical identity; only non-executable user data may change.
    /// </summary>

    public struct Admission
    {
        public byte* Pool;
        public ulong PoolPhysical, RootPhysical, ReservationBytes;
        public ulong ImageVirtualStart, ImagePhysicalStart, ImageBytes;
        public uint PoolPages, TablePages, StaticCount;
        public ulong* Tables;
        public ulong* CandidateTables;
        public X64UserMappingRange* StaticRanges;
        public X64PageTableEncodingPreconditions Conditions;
    }
}
