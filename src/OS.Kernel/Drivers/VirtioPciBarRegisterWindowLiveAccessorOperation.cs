using OS.Std.Binary;

namespace OS.Drivers;
public enum VirtioPciBarRegisterWindowLiveAccessorOperation
{
    None = 0,
    Read16 = 1,
    Write16 = 2,
    Write64 = 3
}
