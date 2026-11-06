namespace OS.Pal;

using OS.Std.Text;

public enum OSHostExportDomain : int
{
    Memory = 0,
    Time = 1,
    ConsoleLog = 2,
    Entropy = 3,
    PackageFile = 4,
    Config = 5,
    Environment = 6,
    Socket = 7,
    Timer = 8,
    Thread = 9,
    Wait = 10,
    Event = 11,
    Semaphore = 12,
    Mutex = 13,
    Cpu = 14
}
