namespace OS.Boot;
/// <summary>
/// Identifies the caller-attested state of one owned boot-stack switch plan.
/// Existing values are append-only and the zero-valued default is invalid.
/// These values do not represent processor observations.
/// </summary>

public enum OwnedBootStackSwitchState : byte
{
    Uninitialized = 0,
    Planned = 1,
    Armed = 2,
    TransferRecorded = 3,
    ContinuationObserved = 4,
    Failed = 5
}
