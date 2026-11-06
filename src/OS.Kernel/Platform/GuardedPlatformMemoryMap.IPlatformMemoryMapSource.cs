using OS.Boot;

namespace OS.Kernel.Platform;

public interface IPlatformMemoryMapSource
{
    UefiMemoryMapSnapshot CaptureSnapshot();
}
