using System.Runtime.InteropServices;

namespace OS.Pal;
[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostByteSpan
{
    public readonly ulong Address;
    public readonly ulong Length;
    public OSHostByteSpan(ulong address, ulong length)
    {
        Address = address;
        Length = length;
    }
}
