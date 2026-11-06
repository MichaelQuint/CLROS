using System.Runtime.InteropServices;
using OS.Drivers;

internal static unsafe partial class PCIBusesDevicesFunctionsBoundedDiagnosticsTests
{
    private struct Context
    {
        public uint Calls, BusNumbers;
        public bool Bridge;
    }
}
