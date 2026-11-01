namespace OS.Boot;
#if !NOSTDLIB_EFI
#endif
public enum M01OwnedCr3CompositeCallGateInterruptPolicy : byte
{
    Uninitialized = 0,
    PreserveEntryState = 1
}
