using System.Runtime.InteropServices;

namespace OS.Pal;

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostFileInfo
{
    public readonly ulong ByteLength;
    public readonly ulong Attributes;
    public readonly long ModifiedUnixNanoseconds;
    public OSHostFileInfo(ulong byteLength, ulong attributes, long modifiedUnixNanoseconds)
    {
        ByteLength = byteLength;
        Attributes = attributes;
        ModifiedUnixNanoseconds = modifiedUnixNanoseconds;
    }
}
