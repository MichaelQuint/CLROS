using System.Buffers.Binary;
using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Storage;

internal static unsafe partial class LiveVirtioBlkPCIDeviceTests
{
    private struct ReadContext
    {
        public byte* Bytes;
        public BoundedPciAccess* Access;
        public ulong Page;
        public uint Calls, RevokeAt;
        public bool BadAddress;
    }
}
