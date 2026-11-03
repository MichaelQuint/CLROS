using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Kernel.Platform;
using OS.Std.Binary;

internal static partial class PciEcamPhysicalHeaderReaderTests
{
    private readonly struct PhysicalMemoryEntry
    {
        public PhysicalMemoryEntry(ulong physicalAddress, byte[] bytes)
        {
            PhysicalAddress = physicalAddress;
            Bytes = bytes;
        }

        public ulong PhysicalAddress
        {
            get;
        }
        public byte[] Bytes
        {
            get;
        }
    }
}
