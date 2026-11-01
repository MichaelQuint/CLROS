namespace OS.Kernel.Compatibility.LinuxX64;
/// <summary>Linux x64 kernel signal-mask value, owned by one admitted thread.
/// The thread service serializes query/update/clone publication and commits
/// After once the input copy succeeds, before old-mask copyout. Copying this value intentionally
/// gives a child an independent inherited snapshot; there is no global mask.
/// This type performs no user access, allocation, delivery, pending-signal,
/// sigaction, altstack or sigreturn operation. Those require the signal service.
/// </summary>

public readonly partial struct LinuxSignalMask
{
    public const ulong SetBytes = 8, Block = 0, Unblock = 1, Set = 2;
    // Linux signals 9/19 cannot be blocked. musl's reserved signals 32-34
    // remain real kernel bits; pthread_sigmask filters them at its libc API.
    public const ulong Unblockable = (1UL << 8) | (1UL << 18);
    public readonly ulong Blocked;
    /// <summary>Initialize from the parent's current kernel mask, stripping only
    /// SIGKILL/SIGSTOP. Caller owns the associated live thread generation.</summary>

    public LinuxSignalMask(ulong inherited)
    {
        Blocked = inherited & ~Unblockable;
    }

    /// <summary>Evaluate rt_sigprocmask's exact eight-byte kernel representation.
    /// With no input set, how is ignored and the mask is queried. Invalid size
    /// or input operation returns EINVAL without a state change. Delivery is
    /// neither accepted nor discarded here; the caller must route it explicitly.</summary>

    public Result Evaluate(ulong how, bool hasSet, ulong requested, ulong setBytes)
    {
        if (setBytes != SetBytes || hasSet && how > Set)
            return new(-22, this, this);
        if (!hasSet)
            return new(0, this, this);
        ulong next = how == Block ? Blocked | requested : how == Unblock ? Blocked & ~requested : requested;
        return new(0, this, new(next));
    }

    /// <summary>Inspect a Linux signal number 1-64. Invalid numbers are never
    /// blocked and cause no shift-wrap or state change.</summary>

    public bool Contains(uint signal) => signal is >= 1 and <= 64 && (Blocked & (1UL << (int)(signal - 1))) != 0;
}
