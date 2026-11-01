namespace OS.Kernel.Threading;
public static unsafe partial class ThreadContextSwitch
{
    /// <summary>
    /// Independently captured CPU/ownership observations and declared live ranges.
    /// True proof fields mean the trusted caller performed actual guard/W^X checks;
    /// copying or fabricating this POD supplies no authority. Context/code/switch
    /// stack ranges are disjoint; guard extents include two nonpresent end pages.
    /// Only the current four-level identity root below4GiB is admitted here.
    /// </summary>

    public struct Owner
    {
        public bool Initialized, GuardsVerified, MappingsVerified;
        public ulong Key, CurrentKey, Root, CurrentRoot, KernelGs, CurrentKernelGs;
        public ulong Cr0, Cr4, Efer, Flags;
        public uint CpuCount, BasicEdx, ExtendedEdx, ObservedMxcsrMask;
        public ushort Cs, Ss, Tr, Fs, Gs;
        public ulong ContextArena, ContextArenaBytes, CodeStart, CodeBytes;
        public ulong SwitchStackGuardStart, SwitchStackBytes;
    }
}
