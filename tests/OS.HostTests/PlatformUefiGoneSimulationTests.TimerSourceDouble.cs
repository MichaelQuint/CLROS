using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Platform;
using OS.Std.Binary;

internal static partial class PlatformUefiGoneSimulationTests
{
    private sealed class TimerSourceDouble : IPlatformTimerSource
    {
        public int ReadCount { get; private set; }

        public PlatformTimerMetadata ReadMonotonic()
        {
            ReadCount++;
            return new PlatformTimerMetadata(monotonicTicks: 123, frequencyHertz: 1000);
        }
    }
}
