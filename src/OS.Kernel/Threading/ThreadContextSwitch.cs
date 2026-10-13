namespace OS.Kernel.Threading;

/// <summary>
/// Admission for the bounded single-CPU CPL0 cooperative switch. Context values
/// use M09-001; mappings, guards, lifetime and CPU observations are supplied by
/// the trusted owner. Admission is read-only and does not execute a transfer or
/// grant mapping/restore authority. Complete operations require external ownership
/// and IRQ-off serialization. General suspension, CPL3, XSAVE/AVX, CET and SMP
/// are unsupported. A captured continuation never replaces a parked return slot.
/// </summary>
public static unsafe class ThreadContextSwitch
{
    public enum Status : byte
    {
        Ok, Uninitialized, InvalidOwner, UnsupportedCpu, InvalidRoot, InvalidRange,
        Overlap, InvalidIdentity, StaleIdentity, InvalidContext, InvalidStack,
        InvalidContinuation, InvalidFlags, InvalidTls
    }
    public enum ResumeKind : byte { Entry = 1, CapturedCall = 2 }
    public const uint Page = 4096;
    public const ulong AdmittedFlags = 0x00200cd7; // Arithmetic, DF, ID, mandatory bit1; no IF/TF/RF/AC/privilege bits.

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

    /// <summary>
    /// One pinned current generation, its live mapped context and guarded stack.
    /// Identity uses the existing generation-high32/slot-plus-one-low32 contract.
    /// ResumeRip is independently retained by the owner: initial entry or exact
    /// instruction after CALL. CapturedCall RSP is entryRSP+8, not an intermediate
    /// helper/pushed RSP. FS/user-GS may be zero or point within the declared owned
    /// context arena. Descriptors neither issue identities nor own CPU selectors.
    /// </summary>
    public struct Descriptor
    {
        public ulong Identity, CurrentIdentity;
        public X64ThreadContext* Context;
        public uint ContextBytes;
        public ulong StackGuardStart, StackBytes, ResumeRip;
        public ResumeKind Kind;
    }

    /// <summary>
    /// Validate trusted owner observations/range shape before context access.
    /// No hardware read, mapping or privileged instruction occurs. Rejection
    /// changes no input; valid-looking pointers still require mapped storage.
    /// </summary>
    public static Status AdmitOwner(Owner owner)
    {
        if (!owner.Initialized) return Status.Uninitialized;
        if (owner.Key == 0 || owner.Key != owner.CurrentKey || !owner.GuardsVerified || !owner.MappingsVerified)
            return Status.InvalidOwner;
        if (!Root(owner.Root) || owner.Root != owner.CurrentRoot) return Status.InvalidRoot;
        if (owner.CpuCount != 1 || owner.Cs != 8 || owner.Ss != 16 || owner.Tr != 24 || owner.Fs != 0 || owner.Gs != 0 ||
            owner.KernelGs != owner.CurrentKernelGs || !Canonical(owner.KernelGs) ||
            (owner.Cr0 & 0x80000001) != 0x80000001 || (owner.Cr0 & 0xc) != 0 ||
            (owner.Cr4 & ~0x7ffUL) != 0 || (owner.Cr4 & 0x220) != 0x220 ||
            (owner.Efer & 0xd00) != 0xd00 || (owner.BasicEdx & 0x07000000) != 0x07000000 ||
            (owner.ExtendedEdx & 0x20100000) != 0x20100000 || (owner.Flags & 2) == 0 ||
            (owner.Flags & ~AdmittedFlags) != 0 || (owner.Flags & 0x400) != 0)
            return Status.UnsupportedCpu;
        uint mask = owner.ObservedMxcsrMask == 0 ? X64ThreadContext.FallbackMxcsrMask : owner.ObservedMxcsrMask;
        if ((mask & ~0xffffU) != 0 || (X64ThreadContext.InitialMxcsr & ~mask) != 0) return Status.UnsupportedCpu;
        if (!Pages(owner.ContextArena, owner.ContextArenaBytes) || !Pages(owner.CodeStart, owner.CodeBytes) ||
            !Stack(owner.SwitchStackGuardStart, owner.SwitchStackBytes)) return Status.InvalidRange;
        if (Overlap(owner.ContextArena, owner.ContextArenaBytes, owner.CodeStart, owner.CodeBytes) ||
            Overlap(owner.ContextArena, owner.ContextArenaBytes, owner.SwitchStackGuardStart, owner.SwitchStackBytes) ||
            Overlap(owner.CodeStart, owner.CodeBytes, owner.SwitchStackGuardStart, owner.SwitchStackBytes)) return Status.Overlap;
        return Status.Ok;
    }

    /// <summary>
    /// Read-only pre-transfer admission of two complete contexts. The owner pins
    /// descriptor identities/ranges and all bytes until transfer/restoration ends.
    /// Entry RSP is8mod16 with return/shadow space; captured CALL continuation RSP
    /// is0mod16 with shadow space. Exact retained RIP, complete FP state and stricter
    /// IRQ-off flags are required. Failure leaves both contexts/owner unchanged.
    /// Native capture/restore and final guest qualification are separate from this
    /// admission result; never count Ok as execution evidence.
    /// </summary>
    public static Status Admit(Owner owner, Descriptor source, Descriptor destination)
    {
        Status status = AdmitOwner(owner);
        if (status != Status.Ok) return status;
        status = Shape(owner, source);
        if (status != Status.Ok) return status;
        status = Shape(owner, destination);
        if (status != Status.Ok) return status;
        if (source.Identity == destination.Identity || (uint)source.Identity == (uint)destination.Identity)
            return Status.StaleIdentity;
        if (Overlap((ulong)source.Context, source.ContextBytes, (ulong)destination.Context, destination.ContextBytes) ||
            Overlap(source.StackGuardStart, source.StackBytes, destination.StackGuardStart, destination.StackBytes)) return Status.Overlap;
        status = Values(owner, source);
        return status != Status.Ok ? status : Values(owner, destination);
    }

    private static Status Shape(Owner owner, Descriptor value)
    {
        if ((uint)value.Identity == 0 || (uint)(value.Identity >> 32) == 0 ||
            (uint)value.CurrentIdentity == 0 || (uint)(value.CurrentIdentity >> 32) == 0) return Status.InvalidIdentity;
        if (value.Identity != value.CurrentIdentity) return Status.StaleIdentity;
        if (value.Kind != ResumeKind.Entry && value.Kind != ResumeKind.CapturedCall) return Status.InvalidContinuation;
        ulong context = (ulong)value.Context;
        if (context == 0 || (context & 15) != 0 || value.ContextBytes < X64ThreadContext.Bytes ||
            !Contains(owner.ContextArena, owner.ContextArenaBytes, context, value.ContextBytes)) return Status.InvalidContext;
        if (!Stack(value.StackGuardStart, value.StackBytes)) return Status.InvalidStack;
        if (Overlap(value.StackGuardStart, value.StackBytes, owner.ContextArena, owner.ContextArenaBytes) ||
            Overlap(value.StackGuardStart, value.StackBytes, owner.CodeStart, owner.CodeBytes) ||
            Overlap(value.StackGuardStart, value.StackBytes, owner.SwitchStackGuardStart, owner.SwitchStackBytes)) return Status.Overlap;
        if (!Contains(owner.CodeStart, owner.CodeBytes, value.ResumeRip, 1)) return Status.InvalidContinuation;
        return Status.Ok;
    }
    private static Status Values(Owner owner, Descriptor value)
    {
        if (X64ThreadContext.Validate(value.Context, value.ContextBytes, owner.ObservedMxcsrMask) != X64ThreadContext.Status.Ok)
            return Status.InvalidContext;
        if (value.Context->Rip != value.ResumeRip) return Status.InvalidContinuation;
        if ((value.Context->Rflags & ~AdmittedFlags) != 0) return Status.InvalidFlags;
        ulong low = value.StackGuardStart + Page, high = value.StackGuardStart + (value.StackBytes - Page);
        ulong alignment = value.Kind == ResumeKind.Entry ? 8UL : 0UL;
        ulong shadow = value.Kind == ResumeKind.Entry ? 40UL : 32UL;
        if ((value.Context->Rsp & 15) != alignment || value.Context->Rsp < low + 32 || value.Context->Rsp > high - shadow)
            return Status.InvalidStack;
        if (!Tls(owner, value.Context->FsBase) || !Tls(owner, value.Context->UserGsBase)) return Status.InvalidTls;
        return Status.Ok;
    }
    private static bool Tls(Owner owner, ulong address) => address == 0 || Contains(owner.ContextArena, owner.ContextArenaBytes, address, 8);
    private static bool Root(ulong value) => value != 0 && value < 0x100000000 && (value & (Page - 1)) == 0;
    private static bool Pages(ulong address, ulong bytes) => bytes >= Page && (address & (Page - 1)) == 0 &&
        (bytes & (Page - 1)) == 0 && Range(address, bytes);
    private static bool Stack(ulong address, ulong bytes) => bytes >= 3UL * Page && Pages(address, bytes);
    private static bool Range(ulong address, ulong bytes) => address != 0 && bytes != 0 && bytes - 1 <= ulong.MaxValue - address &&
        Canonical(address) && Canonical(address + (bytes - 1)) && ((address ^ (address + (bytes - 1))) & (1UL << 47)) == 0;
    private static bool Contains(ulong outer, ulong outerBytes, ulong inner, ulong innerBytes) => Range(inner, innerBytes) &&
        inner >= outer && inner - outer <= outerBytes && innerBytes <= outerBytes - (inner - outer);
    private static bool Overlap(ulong a, ulong aBytes, ulong b, ulong bBytes) => a <= b + (bBytes - 1) && b <= a + (aBytes - 1);
    private static bool Canonical(ulong value) => value >> 48 == ((value & (1UL << 47)) == 0 ? 0UL : 0xffffUL);
}
