using OS.Std.Text;

namespace OS.Pal;
public enum OSHostStatusCategory : int
{
    Success = 0,
    CallerError = 1,
    ResourceError = 2,
    Unsupported = 3,
    RuntimeUnavailable = 4,
    Unknown = 5
}
