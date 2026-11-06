namespace OS.Boot;

public static unsafe partial class M01OwnedCr3CompositeTransition
{
    private enum PlanCondition : byte
    {
        Below = 0x82,
        AboveOrEqual = 0x83,
        Equal = 0x84,
        NotEqual = 0x85,
        BelowOrEqual = 0x86,
        Above = 0x87
    }
}
