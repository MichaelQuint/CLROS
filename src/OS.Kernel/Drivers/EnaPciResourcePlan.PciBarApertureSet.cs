using OS.Std.Binary;

namespace OS.Drivers;
public readonly struct PciBarApertureSet
{
    public PciBarApertureSet(ulong bar0Length, ulong bar1Length, ulong bar2Length, ulong bar3Length, ulong bar4Length, ulong bar5Length)
    {
        Bar0Length = bar0Length;
        Bar1Length = bar1Length;
        Bar2Length = bar2Length;
        Bar3Length = bar3Length;
        Bar4Length = bar4Length;
        Bar5Length = bar5Length;
    }

    public ulong Bar0Length { get; }
    public ulong Bar1Length { get; }
    public ulong Bar2Length { get; }
    public ulong Bar3Length { get; }
    public ulong Bar4Length { get; }
    public ulong Bar5Length { get; }

    public bool TryGet(byte index, out ulong length)
    {
        switch (index)
        {
            case 0:
                length = Bar0Length;
                return true;
            case 1:
                length = Bar1Length;
                return true;
            case 2:
                length = Bar2Length;
                return true;
            case 3:
                length = Bar3Length;
                return true;
            case 4:
                length = Bar4Length;
                return true;
            case 5:
                length = Bar5Length;
                return true;
            default:
                length = 0;
                return false;
        }
    }
}
