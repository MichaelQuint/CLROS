namespace OS.Kernel.Platform;
public interface IPlatformFramebufferSource
{
    PlatformFramebufferSourceResult CaptureSnapshot();
}
