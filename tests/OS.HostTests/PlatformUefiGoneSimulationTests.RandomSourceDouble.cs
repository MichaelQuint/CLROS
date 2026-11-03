using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Platform;
using OS.Std.Binary;

internal static partial class PlatformUefiGoneSimulationTests
{
    private sealed class RandomSourceDouble : IPlatformRandomSource
    {
        public int FillCount { get; private set; }

        public PlatformRandomSourceResult FillBytes(Span<byte> destination)
        {
            FillCount++;
            destination[0] = 0x5A;
            return PlatformRandomSourceResult.Filled(destination.Length);
        }
    }
}
