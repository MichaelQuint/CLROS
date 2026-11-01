using OS.Kernel.Platform;

namespace OS.Boot;
public unsafe struct FinalMapExitWindowState
{
    internal uint Phase;
    internal TypedExitBootServicesState Exit;
    public bool IsConsumed => Phase == 2;
}
