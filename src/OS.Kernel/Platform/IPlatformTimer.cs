namespace OS.Kernel.Platform;

public interface IPlatformTimer
{
    PlatformTimerResult ReadMonotonic();
}
