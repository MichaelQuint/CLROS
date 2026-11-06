namespace OS.Drivers;

public enum PciBarDecodeStatus
{
    Success = 0,
    ReservedMemoryType = 1,
    Memory64AtLastIndex = 2
}
