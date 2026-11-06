namespace OS.Kernel.Diagnostics;
/// <summary>
/// Stable status contract for the disabled null-page-fault probe plan.
/// Values are append-only. ReadyDisabled is metadata readiness only.
/// </summary>

public enum NullPageFaultProbePlanStatus : byte
{
    Uninitialized = 0,
    ReadyDisabled = 1,
    InvalidRequestState = 2,
    PageZeroPolicyUnavailable = 3,
    EnabledByDefaultRejected = 4,
    ExecutionAttemptedRejected = 5,
    ExecutionRequestedRejected = 6,
    OwnedDiagnosticsUnavailable = 7,
    PageFaultHandlerUnavailable = 8
}
