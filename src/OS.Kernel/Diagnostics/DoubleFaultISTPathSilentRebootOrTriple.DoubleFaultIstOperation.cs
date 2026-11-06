namespace OS.Kernel.Diagnostics;

public enum DoubleFaultIstOperation : byte
{
    None,
    PrepareContext,
    MaterializeArmedIdt,
    AuditArmedIdt,
    EmitStream,
    ValidateStreamReadBack,
    AuditSyntheticObservation
}
