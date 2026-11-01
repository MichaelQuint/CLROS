using OS.Std.Text;

namespace OS.Kernel.Runtime;
public enum NativeAotClassConstructorRunnerPreflightFormatStatus
{
    Success = 0,
    InvalidResult = 1,
    DestinationTooSmall = 2
}
