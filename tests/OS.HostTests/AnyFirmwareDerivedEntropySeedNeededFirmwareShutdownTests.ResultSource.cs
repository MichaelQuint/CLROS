using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Platform;
using CaptureSeed = OS.Boot.AnyFirmwareDerivedEntropySeedNeededFirmwareShutdown;

internal static partial class AnyFirmwareDerivedEntropySeedNeededFirmwareShutdownTests
{
    private sealed class ResultSource(PlatformRandomResult result) : IPlatformRandom
    {
        internal int Calls;
        public PlatformRandomResult FillBytes(Span<byte> destination)
        {
            Calls++;
            destination.Fill(0x61);
            return result;
        }
    }
}
