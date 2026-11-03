using OS.Pal;
using OS.Std.Text;

namespace OS.Kernel.Runtime;

public enum NativeAppExitMarkerStatus
{
    Success = 0,
    InvalidExitCode = 1,
    InvalidStatus = 2,
    DestinationTooSmall = 3,
    MarkerTooLong = 4
}
