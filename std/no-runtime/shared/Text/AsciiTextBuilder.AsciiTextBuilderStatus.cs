using OS.Std;
using OS.Std.Memory;

namespace OS.Std.Text;
public enum AsciiTextBuilderStatus
{
    Success = 0,
    InvalidOffset = 1,
    DestinationTooSmall = 2,
    NonAsciiInput = 3,
    LengthOverflow = 4,
    InvalidMinimumDigits = 5
}
