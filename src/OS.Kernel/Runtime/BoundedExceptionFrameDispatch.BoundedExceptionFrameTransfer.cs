using System;

namespace OS.Kernel.Runtime;
/// <summary>Sequential x64 POD: targets at 0/8 and 240-byte contexts at 16/256; total 496 bytes.</summary>

public readonly struct BoundedExceptionFrameTransfer
{
    internal BoundedExceptionFrameTransfer(ulong catchHandler, ulong cleanupHandler, X64UnwindContext catchCaller, X64UnwindContext cleanupCaller)
    {
        CatchHandler = catchHandler;
        CleanupHandler = cleanupHandler;
        CatchCaller = catchCaller;
        CleanupCaller = cleanupCaller;
    }

    public readonly ulong CatchHandler, CleanupHandler;
    public readonly X64UnwindContext CatchCaller, CleanupCaller;
}
