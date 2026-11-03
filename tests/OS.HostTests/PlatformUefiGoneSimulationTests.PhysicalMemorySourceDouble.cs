using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Platform;
using OS.Std.Binary;

internal static partial class PlatformUefiGoneSimulationTests
{
    private sealed class PhysicalMemorySourceDouble : IPlatformPhysicalMemorySource
    {
        public int ReadCount
        {
            get; private set;
        }

        public PlatformPhysicalMemorySourceResult ReadBytes(ulong physicalAddress, int byteCount, Span<byte> destination)
        {
            ReadCount++;
            destination[0] = 0xA5;
            return PlatformPhysicalMemorySourceResult.Read(byteCount);
        }
    }
}
