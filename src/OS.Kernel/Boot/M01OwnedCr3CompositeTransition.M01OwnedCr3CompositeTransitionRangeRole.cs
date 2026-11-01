namespace OS.Boot;
public enum M01OwnedCr3CompositeTransitionRangeRole : byte
{
    Uninitialized = 0,
    CompositeCode = 1,
    OwnedStack = 2,
    LoadedImageContinuationPage = 3,
    ProbePage = 4,
    CompleteTableArena = 5,
    RuntimeStateAndCanary = 6,
    OwnedRoot = 7,
    CompositeCodeAndCallGate = 8,
    CompositeOwnedStack = 9,
    ProtectionProbePage = 10,
    CompleteTableAndStateArena = 11,
    RuntimeStateAndCanaryPage = 12,
    OwnedRootPage = 13
}
#if !NOSTDLIB_EFI
#endif
