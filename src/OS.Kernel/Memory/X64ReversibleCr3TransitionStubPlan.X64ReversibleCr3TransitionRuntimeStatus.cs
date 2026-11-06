namespace OS.Kernel.Memory;
/// <summary>
/// Status written by the transition stub itself. Values are append-only.
/// </summary>

public enum X64ReversibleCr3TransitionRuntimeStatus : byte
{
    Uninitialized = 0,
    Entered = 1,
    Success = 2,
    InvalidState = 3,
    FirmwareCallbackForbidden = 4,
    InvalidOwnedCr3 = 5,
    InvalidStackRange = 6,
    InvalidCanary = 7,
    RspOutsideExpectedRange = 8,
    InterruptDisableReadbackMismatch = 9,
    La57NotSupported = 10,
    PcideNotSupported = 11,
    PgeClearReadbackMismatch = 12,
    OwnedCr3ReadbackMismatch = 13,
    CanaryBeforeMismatch = 14,
    CanaryAfterMismatch = 15,
    FirmwareCr3ReadbackMismatch = 16,
    Cr4RestoreReadbackMismatch = 17,
    RflagsRestoreReadbackMismatch = 18,
    RspRestoreMismatch = 19,
    PostRestoreCanaryMismatch = 20
}
