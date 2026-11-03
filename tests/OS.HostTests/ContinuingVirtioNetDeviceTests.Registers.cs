using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;
using Nic = OS.Drivers.ContinuingVirtioNetDevice;

internal static unsafe partial class ContinuingVirtioNetDeviceTests
{
    private struct Registers
    {
        internal ulong Descriptor, Available, Used;
        internal ushort Size, Maximum, Vector, Enabled, Offset;
    }
}
