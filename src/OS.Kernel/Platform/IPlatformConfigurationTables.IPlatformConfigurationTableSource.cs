using OS.Boot;

namespace OS.Kernel.Platform;
public interface IPlatformConfigurationTableSource
{
    PlatformConfigurationTableMetadata GetMetadata();
}
