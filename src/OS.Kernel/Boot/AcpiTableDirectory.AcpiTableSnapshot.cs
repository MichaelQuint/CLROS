using OS.Std.Binary;

namespace OS.Boot;
public readonly struct AcpiTableSnapshot
{
    public AcpiTableSnapshot(ulong physicalAddress, uint signature, uint length, int pointerIndex, int sourceIndex)
    {
        PhysicalAddress = physicalAddress;
        Signature = signature;
        Length = length;
        PointerIndex = pointerIndex;
        SourceIndex = sourceIndex;
    }

    public ulong PhysicalAddress { get; }
    public uint Signature { get; }
    public uint Length { get; }
    public int PointerIndex { get; }
    public int SourceIndex { get; }
}
