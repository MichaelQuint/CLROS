namespace OS.Kernel.Diagnostics;

public enum CpuFaultPanicReadinessStatus
{
    Ready = 0,
    FaultFrameInvalid = 1,
    PanicReportFailed = 2
}
