using OS.Drivers;
using OS.Kernel.Platform;
using OS.Std.Binary;

namespace OS.Boot;

public enum AcpiMcfgDiscoveryStatus
{
    Success = 0,
    DirectoryFailed = 1,
    McfgParseFailed = 2,
    SelectedSourceInvalid = 3
}
