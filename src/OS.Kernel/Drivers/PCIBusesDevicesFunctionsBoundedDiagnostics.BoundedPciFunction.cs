namespace OS.Drivers;
public struct BoundedPciFunction
{
    public ushort Segment;
    public byte Bus, Device, Function;
    public BoundedPciHeader Header;
}
