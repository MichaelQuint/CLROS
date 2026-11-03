using OS.Kernel.Runtime;
using OS.Std.Text;

namespace OS.Kernel.Diagnostics;

public enum DiagnosticStubCallableBindingPlanDiagnosticFormatStatus
{
    Success = 0,
    InvalidResult = 1,
    DestinationTooSmall = 2
}
