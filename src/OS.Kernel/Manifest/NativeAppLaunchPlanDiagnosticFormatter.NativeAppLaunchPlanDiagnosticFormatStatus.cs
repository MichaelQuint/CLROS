using OS.Boot;
using OS.Std.Text;

namespace OS.Manifest;

public enum NativeAppLaunchPlanDiagnosticFormatStatus
{
    Success = 0,
    InvalidResult = 1,
    SourceRangeInvalid = 2,
    DestinationTooSmall = 3
}
