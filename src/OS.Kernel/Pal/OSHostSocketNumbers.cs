using System.Runtime.InteropServices;

namespace OS.Pal;

public static class OSHostSocketNumbers
{
    public static ushort HostToNetwork16(ushort value)
    {
        return (ushort)((value << 8) | (value >> 8));
    }

    public static ushort NetworkToHost16(ushort value)
    {
        return HostToNetwork16(value);
    }
}
