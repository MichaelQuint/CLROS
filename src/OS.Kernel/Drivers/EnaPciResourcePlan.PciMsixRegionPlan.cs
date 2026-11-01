using OS.Std.Binary;

namespace OS.Drivers;
public readonly struct PciMsixRegionPlan
{
    internal PciMsixRegionPlan(byte barIndex, uint offset, ulong length)
    {
        BarIndex = barIndex;
        Offset = offset;
        Length = length;
        EndOffsetExclusive = (ulong)offset + length;
    }

    public byte BarIndex { get; }
    public uint Offset { get; }
    public ulong Length { get; }
    public ulong EndOffsetExclusive { get; }
}
