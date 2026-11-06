using OS.Kernel.Platform;

namespace OS.Boot;

public enum TypedExitBootServicesPhase : uint
{
    Empty = 0,
    Ready = 1,
    InFlight = 2,
    Returned = 3
}
