using OS.Std.Text;

namespace OS.Pal;
public enum OSHostAbiStatusDomain : int
{
    Config = 0,
    Environment = 1,
    Time = 2,
    ConsoleLog = 3,
    Memory = 4,
    PackageFile = 5,
    Socket = 6
}
