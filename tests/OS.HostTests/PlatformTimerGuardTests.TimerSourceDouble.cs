using System.Runtime.CompilerServices;
using OS.Kernel.Platform;

internal static partial class PlatformTimerGuardTests
{
    private sealed class TimerSourceDouble : IPlatformTimerSource
    {
        private readonly PlatformTimerMetadata _metadata;
        public TimerSourceDouble(PlatformTimerMetadata metadata)
        {
            _metadata = metadata;
        }

        public int ReadCount { get; private set; }

        public PlatformTimerMetadata ReadMonotonic()
        {
            ReadCount++;
            return _metadata;
        }
    }
}
