namespace OS.Kernel.Memory;

public static unsafe partial class X64ReversibleCr3TransitionStubPlan
{
    private enum Register : byte
    {
        Rax = 0,
        Rcx = 1,
        Rdx = 2,
        Rsp = 4,
        R8 = 8,
        R9 = 9,
        R10 = 10,
        R11 = 11
    }
}
