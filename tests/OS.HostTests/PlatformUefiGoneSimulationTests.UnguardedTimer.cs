using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Platform;
using OS.Std.Binary;

internal static partial class PlatformUefiGoneSimulationTests
{
    private sealed class UnguardedTimer : IPlatformTimer
    {
        public int ReadCount
        {
            get; private set;
        }

        public PlatformTimerResult ReadMonotonic()
        {
            ReadCount++;
            return PlatformTimerResult.Read(FirmwareCallResult.Allowed(PlatformTimerOperations.ReadMonotonic, FirmwareServicesMode.BootServicesAvailable), new PlatformTimerMetadata(monotonicTicks: 123, frequencyHertz: 1000));
        }
    }
}
