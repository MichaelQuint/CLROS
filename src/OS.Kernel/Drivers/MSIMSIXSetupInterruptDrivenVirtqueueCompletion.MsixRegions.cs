using OS.Std.Binary;

namespace OS.Drivers;
public readonly struct MsixRegions
{
    public readonly MsixRegion Table, Pba;
    internal MsixRegions(MsixRegion table, MsixRegion pba)
    {
        Table = table;
        Pba = pba;
    }
}
