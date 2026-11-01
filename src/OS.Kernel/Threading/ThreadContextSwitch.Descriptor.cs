namespace OS.Kernel.Threading;
public static unsafe partial class ThreadContextSwitch
{
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
}
