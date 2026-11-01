namespace OS.Boot;
#if !NOSTDLIB_EFI
#endif
public enum M01OwnedCr3CompositeCallGateObservationLifecycle : byte
{
    Uninitialized = 0,
    Armed = 1,
    InProgress = 2,
    Completed = 3
}
