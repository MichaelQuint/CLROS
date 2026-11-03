using OS.Std;

namespace OS.Std.Text;
[Flags]
public enum AsciiTokenSplitOptions
{
    None = 0,
    TrimWhiteSpace = 1,
    RemoveEmptyEntries = 2
}
