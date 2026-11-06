using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Platform;
using CaptureSeed = OS.Boot.AnyFirmwareDerivedEntropySeedNeededFirmwareShutdown;

internal static partial class AnyFirmwareDerivedEntropySeedNeededFirmwareShutdownTests
{
    private sealed class Fixture
    {
        internal readonly byte[] Scratch = Pattern(112, 0xA1);
        internal readonly byte[] Destination = Pattern(112, 0xD3);
        internal readonly FirmwareServicesGuard Firmware = new();
        internal readonly Source Provider = new();
        internal readonly GuardedPlatformRandom Random;
        internal FirmwareEntropySeedPublication Publication;
        internal Fixture()
        {
            Random = new(Firmware, Provider);
            Provider.Owner = this;
        }

        internal FirmwareEntropySeedResult Capture(int count = 32) => CaptureSeed.Capture(Random, count, Scratch.AsSpan(8, 96), Destination.AsSpan(8, 96), ref Publication);
    }
}
