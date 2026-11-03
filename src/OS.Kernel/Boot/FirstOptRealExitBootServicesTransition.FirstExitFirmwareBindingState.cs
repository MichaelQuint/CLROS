using OS.Kernel.Platform;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using OS.Std.Hash;

#if M16_004_PROBE || M16_005_PROBE
using OS.Kernel.Compatibility.LinuxX64;
#endif
namespace OS.Boot;
public unsafe struct FirstExitFirmwareBindingState
{
    internal uint Phase;
    internal byte* SystemTable;
    internal byte* BootServices;
    internal ulong SystemBytes, BootBytes, MapFunction, ExitFunction;
    internal uint SystemCrc, BootCrc;
    internal void* ImageHandle;
    public bool IsReady => Phase == 1;
    public bool IsConsumed => Phase == 2;
    public bool RetainedPointersCleared => SystemTable == null && BootServices == null && MapFunction == 0 && ExitFunction == 0 && ImageHandle == null;
}
