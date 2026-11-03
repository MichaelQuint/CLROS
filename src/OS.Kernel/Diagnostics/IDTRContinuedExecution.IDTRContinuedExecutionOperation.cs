namespace OS.Kernel.Diagnostics;

public enum IDTRContinuedExecutionOperation : byte
{
    None = 0,
    PrepareContext = 1,
    EmitStub = 2,
    ValidateStubReadBack = 3,
    AuditExecution = 4
}
#if !NOSTDLIB_EFI
#endif
