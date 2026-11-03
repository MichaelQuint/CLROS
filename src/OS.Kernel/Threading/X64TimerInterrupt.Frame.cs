namespace OS.Kernel.Threading;

public static unsafe partial class X64TimerInterrupt
{
    /// <summary>Actual stack image after fifteen pushes and a sixteen-aligned
    /// prefix. The last five words are the hardware no-error IRETQ frame, including
    /// old RSP/SS even at CPL0. The pointer borrows the owned IST until IRETQ; no
    /// caller may retain it or publish a partially modified return frame. Complete
    /// is written last before callback entry. Kernel-GS base is never changed.</summary>

    public struct Frame
    {
        // Natural sequential8-byte fields avoid adding interop attributes to the
        // explicit no-stdlib EFI surface. The owner supplies16-byte stack alignment;
        // focused tests independently check every byte offset and total size.
        public fixed byte FxSave[512];
        public ulong FsBase, GsBase, EntryFlags, Complete;
        public ulong R15, R14, R13, R12, R11, R10, R9, R8;
        public ulong Rdi, Rsi, Rbp, Rbx, Rdx, Rcx, Rax;
        public ulong Rip, Cs, Rflags, Rsp, Ss;
    }
}
