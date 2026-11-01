namespace OS.Boot;
/// <summary>
/// Distinguishes the two terminal failure paths represented by the host-only
/// state machine.
/// </summary>

public enum OwnedBootStackSwitchTerminalCause : byte
{
    Uninitialized = 0,
    ContinuationRspAuditFailed = 1,
    UnexpectedReturnReported = 2
}
