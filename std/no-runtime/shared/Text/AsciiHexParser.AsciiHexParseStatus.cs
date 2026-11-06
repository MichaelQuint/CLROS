using OS.Std;

namespace OS.Std.Text;

public enum AsciiHexParseStatus
{
    Success = 0,
    Empty = 1,
    PrefixOnly = 2,
    NonHexDigit = 3,
    Overflow = 4
}
