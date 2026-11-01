namespace OS.Kernel.Diagnostics;
public enum FaultInfrastructureReadinessStatus
{
    Ready = 0,
    SerialDiagnosticsNotReady = 1,
    IdtTableNotReady = 2,
    IdtrNotReady = 3,
    GdtNotReady = 4,
    TssIstNotReady = 5,
    DoubleFaultIstNotReady = 6
}
