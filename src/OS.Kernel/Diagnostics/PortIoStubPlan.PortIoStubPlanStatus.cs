namespace OS.Kernel.Diagnostics;

public enum PortIoStubPlanStatus
{
    Success = 0,
    InvalidOperationKind = 1,
    OutputBufferTooSmall = 2,
    MissingDestination = 3
}
