using OS.Kernel.Platform;

namespace OS.Boot;
public readonly struct BoundedExitRetryResult
{
    internal BoundedExitRetryResult(BoundedExitRetryStatus status, uint windowsStarted = 0, uint exitCalls = 0, FinalMapExitWindowResult last = default)
    {
        Status = status;
        WindowsStarted = windowsStarted;
        ExitCalls = exitCalls;
        Last = last;
    }

    public BoundedExitRetryStatus Status { get; }
    public uint WindowsStarted { get; }
    public uint ExitCalls { get; }
    public FinalMapExitWindowResult Last { get; }
    public bool Succeeded => Status == BoundedExitRetryStatus.Completed && Last.Succeeded;
}
