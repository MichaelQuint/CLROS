using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Platform;
using OS.Std.Binary;

internal static partial class AcpiRootSdtSnapshotTests
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
