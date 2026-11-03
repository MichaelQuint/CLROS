using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Platform;
using OS.Std.Binary;

internal static partial class PlatformUefiGoneSimulationTests
{
    private sealed class FramebufferSourceDouble : IPlatformFramebufferSource
    {
        public int CaptureCount { get; private set; }

        public PlatformFramebufferSourceResult CaptureSnapshot()
        {
            CaptureCount++;
            return PlatformFramebufferSourceResult.Captured(new PlatformFramebufferMetadata(baseAddress: 0xE0000000, byteLength: 800 * 600 * PlatformFramebufferResult.BytesPerPixel, width: 800, height: 600, stridePixels: 800, PlatformFramebufferPixelFormat.Rgbx8888));
        }
    }
}
