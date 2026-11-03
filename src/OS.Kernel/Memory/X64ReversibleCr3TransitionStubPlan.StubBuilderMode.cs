namespace OS.Kernel.Memory;

public static unsafe partial class X64ReversibleCr3TransitionStubPlan
{
    private enum StubBuilderMode : byte
    {
        Measure = 0,
        Emit = 1,
        Validate = 2
    }
}
