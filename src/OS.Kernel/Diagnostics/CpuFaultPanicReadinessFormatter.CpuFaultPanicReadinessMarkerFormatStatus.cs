using OS.Std.Text;

namespace OS.Kernel.Diagnostics;

public enum CpuFaultPanicReadinessMarkerFormatStatus
{
    Success = 0,
    DestinationTooSmall = 1,
    InvalidStatus = 2,
    MarkerTooLong = 3
}
