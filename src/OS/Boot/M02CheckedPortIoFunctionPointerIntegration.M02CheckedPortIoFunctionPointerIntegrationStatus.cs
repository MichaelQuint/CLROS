namespace OS.Boot;
public enum M02CheckedPortIoFunctionPointerIntegrationStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    PageFinalizationNotReady = 2,
    SafeExecutionAuditNotReady = 3,
    PortIoAlreadyExecuted = 4,
    PrivilegedStubAlreadyTargeted = 5,
    InvalidPageAddress = 6,
    InvalidPageByteCount = 7,
    InvalidOut8Offset = 8,
    InvalidIn8Offset = 9,
    AddressOverflow = 10,
    BindingRejected = 11
}
