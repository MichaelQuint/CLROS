namespace OS.Kernel.Memory;

public enum X64Cr3ProbeEventKind : byte
{
    Uninitialized = 0,
    PreMarkerEmitted = 1,
    TransitionEntered = 2,
    OwnedRootLoaded = 3,
    OwnedRootReadBack = 4,
    CanaryUpdated = 5,
    FirmwareRootRestored = 6,
    FirmwareRootReadBack = 7,
    CanaryValidated = 8,
    PostMarkerEmitted = 9,
    FirmwareCallbackAttempted = 10,
    InterruptReenabledInsideInterval = 11,
    Timeout = 12,
    Reset = 13,
    TripleFault = 14
}
