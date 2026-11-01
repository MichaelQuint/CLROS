using OS.Std.Binary;

namespace OS.Drivers;
public readonly struct VmbusGuid
{
    public VmbusGuid(uint data1, ushort data2, ushort data3, byte data4A, byte data4B, byte data4C, byte data4D, byte data4E, byte data4F, byte data4G, byte data4H)
    {
        Data1 = data1;
        Data2 = data2;
        Data3 = data3;
        Data4A = data4A;
        Data4B = data4B;
        Data4C = data4C;
        Data4D = data4D;
        Data4E = data4E;
        Data4F = data4F;
        Data4G = data4G;
        Data4H = data4H;
    }

    public uint Data1 { get; }
    public ushort Data2 { get; }
    public ushort Data3 { get; }
    public byte Data4A { get; }
    public byte Data4B { get; }
    public byte Data4C { get; }
    public byte Data4D { get; }
    public byte Data4E { get; }
    public byte Data4F { get; }
    public byte Data4G { get; }
    public byte Data4H { get; }
    public bool IsZero => Data1 == 0 && Data2 == 0 && Data3 == 0 && Data4A == 0 && Data4B == 0 && Data4C == 0 && Data4D == 0 && Data4E == 0 && Data4F == 0 && Data4G == 0 && Data4H == 0;

    public bool Equals(VmbusGuid other)
    {
        return Data1 == other.Data1 && Data2 == other.Data2 && Data3 == other.Data3 && Data4A == other.Data4A && Data4B == other.Data4B && Data4C == other.Data4C && Data4D == other.Data4D && Data4E == other.Data4E && Data4F == other.Data4F && Data4G == other.Data4G && Data4H == other.Data4H;
    }
}
