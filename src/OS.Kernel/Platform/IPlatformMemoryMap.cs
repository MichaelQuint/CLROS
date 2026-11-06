using OS.Boot;

namespace OS.Kernel.Platform;

public interface IPlatformMemoryMap
{
    PlatformMemoryMapResult CopySnapshot();
}
