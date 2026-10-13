namespace OS.Kernel.Threading;

/// <summary>
/// Version-one saved values for the single-CPU, four-level x64 x87/SSE profile.
/// The caller owns a live, exclusively accessed buffer aligned to 16 bytes;
/// the CLR's natural struct alignment does not supply this guarantee. No managed
/// references, allocation, instruction execution or restore authority is present.
/// All GPRs are recorded: a cooperative callee-saved set or SYSCALL's transformed
/// RCX/R11 is not a complete asynchronously interrupted context. FS and user-GS
/// are thread values; selectors, CR3, kernel-GS and TSS belong to the trusted
/// CPU/transition owner. XSAVE/AVX, CET, SMP and concurrent publication are unsupported.
/// </summary>
public unsafe struct X64ThreadContext
{
    public enum Status : byte
    { Ok, InvalidBuffer, BufferTooSmall, Uninitialized, InvalidAddress, InvalidFlags, InvalidMxcsrMask, InvalidFpState }

    public const uint Bytes = 688, Alignment = 16, VersionOffset = 0, ReservedOffset = 8;
    public const uint RaxOffset = 16, RbxOffset = 24, RcxOffset = 32, RdxOffset = 40;
    public const uint RsiOffset = 48, RdiOffset = 56, RbpOffset = 64, RspOffset = 72;
    public const uint R8Offset = 80, R9Offset = 88, R10Offset = 96, R11Offset = 104;
    public const uint R12Offset = 112, R13Offset = 120, R14Offset = 128, R15Offset = 136;
    public const uint RipOffset = 144, RflagsOffset = 152, FsBaseOffset = 160, UserGsBaseOffset = 168;
    public const uint FxSaveOffset = 176, FxSaveBytes = 512;
    public const uint FxControlWordOffset = 0, FxMxcsrOffset = 24, FxMxcsrMaskOffset = 28;
    public const uint FxSt0Offset = 32, FxXmm0Offset = 160, FxRegisterStride = 16;
    public const ushort InitialControlWord = 0x027f; // Win64 double precision, masked x87 exceptions.
    public const uint InitialMxcsr = 0x1f80, FallbackMxcsrMask = 0xffbf;
    // Arithmetic/status, TF/IF/DF/OF, RF/AC/ID and mandatory bit1. IOPL, NT,
    // virtual-mode flags and reserved bits are outside this structural profile.
    public const ulong AllowedRflags = 0x00250fd7;

    private ulong _version, _reserved;
    public ulong Rax, Rbx, Rcx, Rdx, Rsi, Rdi, Rbp, Rsp;
    public ulong R8, R9, R10, R11, R12, R13, R14, R15;
    public ulong Rip, Rflags, FsBase, UserGsBase;
    public fixed byte FxSave[512];

    /// <summary>Software completion marker only; use Validate before admitting a
    /// snapshot. A matching marker never authorizes mapping or execution.</summary>
    public readonly bool IsInitialized => _version == 1 && _reserved == 0;

    /// <summary>
    /// Initialize borrowed unpublished storage with zero GPR/data values and legal
    /// Win64 FP controls. The mask must be independently obtained by the trusted CPU
    /// owner; zero uses Intel's 0xffbf fallback. Scalar addresses must be canonical,
    /// RIP/RSP nonzero, and flags in the declared profile. All checks precede the
    /// single-owner copy; rejection leaves the complete destination unchanged.
    /// This does not validate mapped code, stack ABI/ownership or TLS lifetime.
    /// </summary>
    public static Status Initialize(X64ThreadContext* destination, uint destinationBytes,
        ulong rip, ulong rsp, uint observedMxcsrMask, ulong flags = 2, ulong fsBase = 0, ulong userGsBase = 0)
    {
        Status storage = Storage(destination, destinationBytes);
        if (storage != Status.Ok) return storage;
        if (!Addresses(rip, rsp, fsBase, userGsBase)) return Status.InvalidAddress;
        if (!Flags(flags)) return Status.InvalidFlags;
        if (!Mask(observedMxcsrMask, out _)) return Status.InvalidMxcsrMask;
        X64ThreadContext candidate = default;
        candidate.Rip = rip; candidate.Rsp = rsp; candidate.Rflags = flags;
        candidate.FsBase = fsBase; candidate.UserGsBase = userGsBase;
        byte* fp = candidate.FxSave;
        *(ushort*)(fp + FxControlWordOffset) = InitialControlWord;
        *(uint*)(fp + FxMxcsrOffset) = InitialMxcsr;
        *(uint*)(fp + FxMxcsrMaskOffset) = observedMxcsrMask;
        candidate._reserved = 0; candidate._version = 1;
        *destination = candidate;
        return Status.Ok;
    }

    /// <summary>
    /// Read-only shape validation over live caller-owned storage. Accepts canonical
    /// addresses in either four-level half, with nonzero RIP/RSP and optional zero
    /// FS/user-GS. The saved mask must match the independently trusted observation;
    /// the image cannot widen its own permitted MXCSR bits. Saved x87/data values
    /// are opaque captured state, not proof of fresh initialization. A CPU owner
    /// must separately admit mappings, privilege, stack/TLS lifetimes and features
    /// before any restore. Invalid pointers fail shape checks before dereference;
    /// valid-looking addresses still require the caller's mapped-storage guarantee.
    /// </summary>
    public static Status Validate(X64ThreadContext* context, uint contextBytes, uint observedMxcsrMask)
    {
        Status storage = Storage(context, contextBytes);
        if (storage != Status.Ok) return storage;
        if (!context->IsInitialized) return Status.Uninitialized;
        if (!Addresses(context->Rip, context->Rsp, context->FsBase, context->UserGsBase)) return Status.InvalidAddress;
        if (!Flags(context->Rflags)) return Status.InvalidFlags;
        if (!Mask(observedMxcsrMask, out uint effective)) return Status.InvalidMxcsrMask;
        byte* fp = context->FxSave;
        if (*(uint*)(fp + FxMxcsrMaskOffset) != observedMxcsrMask ||
            (*(uint*)(fp + FxMxcsrOffset) & ~effective) != 0) return Status.InvalidFpState;
        return Status.Ok;
    }

    /// <summary>
    /// Copy a validated snapshot between borrowed live buffers under exclusive
    /// ownership. A local complete value supports overlap. Failure leaves all
    /// destination bytes unchanged. Copying does not save hardware registers,
    /// publish a runnable thread or grant restore authority.
    /// </summary>
    public static Status Copy(X64ThreadContext* destination, uint destinationBytes,
        X64ThreadContext* source, uint sourceBytes, uint observedMxcsrMask)
    {
        Status valid = Validate(source, sourceBytes, observedMxcsrMask);
        if (valid != Status.Ok) return valid;
        Status storage = Storage(destination, destinationBytes);
        if (storage != Status.Ok) return storage;
        X64ThreadContext snapshot = *source;
        *destination = snapshot;
        return Status.Ok;
    }

    private static Status Storage(X64ThreadContext* value, uint bytes)
    {
        ulong address = (ulong)value;
        if (sizeof(nuint) != 8 || address == 0 || (address & (Alignment - 1)) != 0) return Status.InvalidBuffer;
        if (bytes < Bytes) return Status.BufferTooSmall;
        if (address > ulong.MaxValue - (bytes - 1UL) || !Canonical(address) || !Canonical(address + bytes - 1))
            return Status.InvalidBuffer;
        return Status.Ok;
    }
    private static bool Canonical(ulong value) => value >> 48 == ((value & (1UL << 47)) == 0 ? 0UL : 0xffffUL);
    private static bool Addresses(ulong rip, ulong rsp, ulong fs, ulong gs) =>
        rip != 0 && rsp != 0 && Canonical(rip) && Canonical(rsp) && Canonical(fs) && Canonical(gs);
    private static bool Flags(ulong value) => (value & 2) != 0 && (value & ~AllowedRflags) == 0;
    private static bool Mask(uint observed, out uint effective)
    {
        effective = observed == 0 ? FallbackMxcsrMask : observed;
        return (effective & ~0xffffU) == 0 && (InitialMxcsr & ~effective) == 0;
    }
}
