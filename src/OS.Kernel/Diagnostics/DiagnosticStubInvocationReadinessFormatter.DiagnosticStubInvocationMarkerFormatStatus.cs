using OS.Kernel.Runtime;
using OS.Std.Text;

namespace OS.Kernel.Diagnostics;
public enum DiagnosticStubInvocationMarkerFormatStatus
{
    Success = 0,
    DestinationTooSmall = 1,
    InvalidStatus = 2,
    MarkerTooLong = 3
}
