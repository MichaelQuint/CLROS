namespace OS.Kernel.Diagnostics;
public readonly struct PanicSafeOwnedCom1FaultRecord
{
    public PanicSafeOwnedCom1FaultRecord(int vector, bool hasErrorCode, ulong errorCode, ulong rip, ulong rsp, ulong rbp, ulong rflags, ulong cr2, ulong cr3, ulong rax, ulong rbx, ulong rcx, ulong rdx, ulong rsi, ulong rdi, ulong r8, ulong r9, ulong r10, ulong r11, ulong r12, ulong r13, ulong r14, ulong r15)
    {
        Vector = vector;
        HasErrorCode = hasErrorCode;
        ErrorCode = errorCode;
        Rip = rip;
        Rsp = rsp;
        Rbp = rbp;
        Rflags = rflags;
        Cr2 = cr2;
        Cr3 = cr3;
        Rax = rax;
        Rbx = rbx;
        Rcx = rcx;
        Rdx = rdx;
        Rsi = rsi;
        Rdi = rdi;
        R8 = r8;
        R9 = r9;
        R10 = r10;
        R11 = r11;
        R12 = r12;
        R13 = r13;
        R14 = r14;
        R15 = r15;
    }

    public int Vector { get; }
    public bool HasErrorCode { get; }
    public ulong ErrorCode { get; }
    public ulong Rip { get; }
    public ulong Rsp { get; }
    public ulong Rbp { get; }
    public ulong Rflags { get; }
    public ulong Cr2 { get; }
    public ulong Cr3 { get; }
    public ulong Rax { get; }
    public ulong Rbx { get; }
    public ulong Rcx { get; }
    public ulong Rdx { get; }
    public ulong Rsi { get; }
    public ulong Rdi { get; }
    public ulong R8 { get; }
    public ulong R9 { get; }
    public ulong R10 { get; }
    public ulong R11 { get; }
    public ulong R12 { get; }
    public ulong R13 { get; }
    public ulong R14 { get; }
    public ulong R15 { get; }
}
#if !NOSTDLIB_EFI
#endif
