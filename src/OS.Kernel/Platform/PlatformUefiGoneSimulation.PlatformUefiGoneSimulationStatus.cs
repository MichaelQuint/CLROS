namespace OS.Kernel.Platform;
public enum PlatformUefiGoneSimulationStatus
{
    Unknown = 0,
    Success = 1,
    SimulationDenied = 2,
    ConsoleCallNotDenied = 3,
    MemoryMapCallNotDenied = 4,
    ConfigurationTableCallNotDenied = 5,
    TimerCallNotDenied = 6,
    RandomCallNotDenied = 7,
    PhysicalMemoryCallNotDenied = 8,
    FramebufferCallNotDenied = 9,
    FileSystemCallNotDenied = 10
}
