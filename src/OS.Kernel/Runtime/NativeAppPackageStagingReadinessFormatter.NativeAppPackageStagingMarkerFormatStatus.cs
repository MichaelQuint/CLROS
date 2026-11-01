using OS.Manifest;
using OS.Std.Text;
using OS.Storage;

namespace OS.Kernel.Runtime;
public enum NativeAppPackageStagingMarkerFormatStatus
{
    Success = 0,
    DestinationTooSmall = 1,
    InvalidStatus = 2,
    MarkerTooLong = 3
}
