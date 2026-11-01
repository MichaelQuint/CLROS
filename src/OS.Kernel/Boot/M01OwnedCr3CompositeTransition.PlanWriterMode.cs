namespace OS.Boot;
#if !NOSTDLIB_EFI
#endif
public static unsafe partial class M01OwnedCr3CompositeTransition
{
    private enum PlanWriterMode : byte
    {
        Measure = 0,
        Emit = 1,
        Compare = 2
    }
}
