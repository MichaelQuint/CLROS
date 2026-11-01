using OS.Pal;
using OS.Std.Text;

namespace OS.Kernel.Runtime;
public enum NativeAppExitParseStatus
{
    Success = 0,
    InvalidPrefix = 1,
    InvalidFieldOrder = 2,
    MalformedCode = 3,
    InvalidStatusToken = 4,
    ExtraFields = 5,
    TruncatedFields = 6
}
