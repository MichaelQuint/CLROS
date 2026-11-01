namespace OS.Boot;
/// <summary>
/// Identifies the only continuation contract accepted by the first
/// owned-stack switch plan.
/// </summary>

public enum OwnedBootStackSwitchContinuationContract : byte
{
    Uninitialized = 0,
    StaticNonReturningRspAndHandoff = 1
}
