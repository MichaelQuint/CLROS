namespace OS.Boot;
#if !NOSTDLIB_EFI
#endif
public enum M01OwnedCr3CompositeCallGatePgePolicy : byte
{
    Uninitialized = 0,
    ClearDuringOwnedIntervalAndRestoreExact = 1
}
