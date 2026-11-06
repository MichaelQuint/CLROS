using OS.Std;

namespace OS.Std.Text;

public enum AsciiDecimalParseStatus
{
    Success = 0,
    Empty = 1,
    InvalidDigit = 2,
    Overflow = 3,
    SignOnly = 4
}
