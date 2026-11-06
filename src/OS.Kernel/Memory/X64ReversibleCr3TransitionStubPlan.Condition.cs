namespace OS.Kernel.Memory;

public static unsafe partial class X64ReversibleCr3TransitionStubPlan
{
    private enum Condition : byte
    {
        Below = 0x82,
        AboveOrEqual = 0x83,
        Equal = 0x84,
        NotEqual = 0x85,
        BelowOrEqual = 0x86,
        Above = 0x87
    }
}
