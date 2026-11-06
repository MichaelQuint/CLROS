using System.Runtime.CompilerServices;
using OS.Kernel.Platform;

internal static partial class PlatformFramebufferGuardTests
{
    private sealed class FramebufferSourceDouble : IPlatformFramebufferSource
    {
        private readonly PlatformFramebufferSourceResult _result;
        public FramebufferSourceDouble(PlatformFramebufferSourceResult result)
        {
            _result = result;
        }

        public int CaptureCount
        {
            get; private set;
        }

        public static FramebufferSourceDouble Success(PlatformFramebufferMetadata metadata)
        {
            return new FramebufferSourceDouble(PlatformFramebufferSourceResult.Captured(metadata));
        }

        public PlatformFramebufferSourceResult CaptureSnapshot()
        {
            CaptureCount++;
            return _result;
        }
    }
}
