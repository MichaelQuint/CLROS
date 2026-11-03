using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxContinuingWritableDescriptorServices
{
    /// <summary>Zeroed caller-owned fd/description arrays. FD0..2 are reserved.
    /// A description survives its last close while a pending request borrows it;
    /// duplicates share its cursor, independent opens never do.</summary>

    public struct Descriptor
    {
        internal ulong Generation;
        internal uint Description, Flags;
        internal bool Reserved;
    }
}
