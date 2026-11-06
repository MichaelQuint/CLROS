using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;
using Net = OS.Drivers.ContinuingVirtioNet;

internal static unsafe partial class ContinuingVirtioNetTests
{
    private struct FenceState
    {
        internal uint Calls, Fail;
    }
}
