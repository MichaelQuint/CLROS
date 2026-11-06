using System.Buffers.Binary;
using System.Runtime.InteropServices;
using OS.Drivers;

internal static unsafe partial class ECAMPageVendorDeviceHeaderTests
{
    private struct Context
    {
        public uint* Words;
        public uint Calls;
    }
}
