namespace OS.Kernel.Platform;
public interface IPlatformTimerSource
{
    PlatformTimerMetadata ReadMonotonic();
}
