using OS.Std.Text;

namespace OS.Kernel.Runtime;

public enum NativeAotClassConstructorStateDiagnosticFormatStatus
{
    Success = 0,
    InvalidResult = 1,
    DestinationTooSmall = 2
}
