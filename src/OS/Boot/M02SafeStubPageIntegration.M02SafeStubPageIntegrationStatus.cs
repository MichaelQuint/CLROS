namespace OS.Boot;
public enum M02SafeStubPageIntegrationStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    MissingPageState = 2,
    InvalidPageState = 3,
    MaterializationRejected = 4,
    ReadbackRejected = 5,
    FinalizationRejected = 6,
    InvalidPageImage = 7,
    MissingRuntimeState = 8,
    MissingGateAudit = 9,
    GateAuditRejected = 10,
    ProbePageMismatch = 11,
    SafeEntryMismatch = 12,
    SafeReturnMismatch = 13,
    InvocationCountMismatch = 14,
    FirmwareCallbackObserved = 15,
    RestorationMismatch = 16,
    ProtectionAuditMismatch = 17,
    RuntimeTerminalMismatch = 18
}
