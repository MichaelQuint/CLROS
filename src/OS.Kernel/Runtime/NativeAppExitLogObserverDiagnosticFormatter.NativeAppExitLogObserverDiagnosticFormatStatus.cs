using OS.Pal;
using OS.Std.Text;

namespace OS.Kernel.Runtime;
public enum NativeAppExitLogObserverDiagnosticFormatStatus
{
    Success = 0,
    InvalidResult = 1,
    DestinationTooSmall = 2
}
