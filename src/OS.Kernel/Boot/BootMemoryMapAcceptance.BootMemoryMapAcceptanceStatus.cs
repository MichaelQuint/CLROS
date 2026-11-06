namespace OS.Boot;

public enum BootMemoryMapAcceptanceStatus
{
    Success = 0,
    MissingSummary = 1,
    ZeroDescriptors = 2,
    ZeroPages = 3,
    ZeroBytes = 4,
    InvalidPhysicalRange = 5,
    NoConventionalMemory = 6
}
