namespace OS.Drivers;
public enum PciSizedBarKind : byte
{
    Uninitialized,
    Absent,
    Memory32,
    Memory64,
    UpperHalf,
    Io32
}
