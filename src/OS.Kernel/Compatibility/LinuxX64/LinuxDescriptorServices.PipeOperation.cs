namespace OS.Kernel.Compatibility.LinuxX64;
public static unsafe partial class LinuxDescriptorServices
{
    /// <summary>One stable, noncopied kernel operation retaining its original
    /// open description after the fd closes/reuses. The thread owner admits its
    /// disjoint mapped storage, keeps it pinned across Park and releases it only
    /// after completion/exit. No user pointer or scratch buffer is pinned: every
    /// actual copy rechecks the current user mapping. Default is inactive.</summary>

    public struct PipeOperation
    {
        internal State* Owner;
        internal ulong Self, Inode, Address;
        internal uint Description, Pipe, Requested, Done, Active;
        internal bool Write;
        public readonly bool IsActive => Active == 1;
        public readonly uint TransferredBytes => Done;
    }
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_025_PROBE
#endif
}
