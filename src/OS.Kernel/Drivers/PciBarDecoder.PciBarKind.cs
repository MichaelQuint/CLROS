namespace OS.Drivers;

public enum PciBarKind
{
    Absent = 0,
    Io = 1,
    Memory32 = 2,
    Memory64 = 3,
    ConsumedByPrevious64BitBar = 4,
    Malformed = 5
}
