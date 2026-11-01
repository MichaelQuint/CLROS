namespace OS.Kernel.Platform;
public interface IPlatformFramebuffer
{
    PlatformFramebufferResult CaptureSnapshot();
}
