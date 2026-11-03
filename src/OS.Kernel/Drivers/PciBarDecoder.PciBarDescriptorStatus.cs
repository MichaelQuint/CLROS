namespace OS.Drivers;

public enum PciBarDescriptorStatus
{
    Success = 0,
    Absent = 1,
    ConsumedByPrevious64BitBar = 2,
    ReservedMemoryType = 3,
    Memory64AtLastIndex = 4
}
