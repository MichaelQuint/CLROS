using OS.Std;

namespace OS.Std.Text;

public enum AsciiKeyValueSplitStatus
{
    Success = 0,
    InvalidRange = 1,
    InvalidDelimiter = 2,
    MissingDelimiter = 3,
    EmptyKey = 4,
    NonAsciiInput = 5,
    InvalidOptions = 6
}
