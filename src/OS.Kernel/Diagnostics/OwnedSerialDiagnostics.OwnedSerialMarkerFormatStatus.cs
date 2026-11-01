using OS.Kernel.Platform;
using OS.Std.Text;

namespace OS.Kernel.Diagnostics;
public enum OwnedSerialMarkerFormatStatus
{
    Success = 0,
    InvalidMarker = 1,
    DestinationTooSmall = 2
}
