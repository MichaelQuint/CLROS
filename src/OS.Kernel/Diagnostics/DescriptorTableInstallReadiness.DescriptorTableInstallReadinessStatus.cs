namespace OS.Kernel.Diagnostics;
public enum DescriptorTableInstallReadinessStatus
{
    Ready = 0,
    IdtTableNotReady = 1,
    IdtrNotReady = 2,
    GdtNotReady = 3,
    TssIstNotReady = 4,
    DoubleFaultIstNotReady = 5,
    StubPlanNotReady = 6,
    OutputBufferTooSmall = 7
}
