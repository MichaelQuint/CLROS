using OS.Kernel.Platform;

namespace OS.Boot;

public unsafe struct BoundedExitRetryState
{
    internal uint Phase;
    internal FinalMapExitWindowState First, Retry;
    public bool IsConsumed => Phase == 2;
}
