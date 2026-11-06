using OS.Std;

namespace OS.Std.Text;

public enum AsciiTokenSplitStatus
{
    Success = 0,
    InvalidRange = 1,
    InvalidDelimiter = 2,
    DestinationTooSmall = 3,
    NonAsciiInput = 4,
    InvalidOptions = 5
}
