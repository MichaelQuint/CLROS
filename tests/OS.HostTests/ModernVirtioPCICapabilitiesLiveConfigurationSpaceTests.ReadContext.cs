using System.Buffers.Binary;
using System.Runtime.InteropServices;
using OS.Drivers;

internal static unsafe partial class ModernVirtioPCICapabilitiesLiveConfigurationSpaceTests
{
    private struct ReadContext
    {
        public byte* Bytes;
        public uint Calls;
    }
}
