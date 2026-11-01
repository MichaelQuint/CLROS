using System.Runtime.InteropServices;

namespace OS.Pal;
[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostEnvironmentEnumerationQuery
{
    public readonly uint Index;
    public readonly uint Reserved;
    public readonly OSHostByteSpan KeyDestination;
    public readonly OSHostByteSpan ValueDestination;
    public OSHostEnvironmentEnumerationQuery(uint index, uint reserved, OSHostByteSpan keyDestination, OSHostByteSpan valueDestination)
    {
        Index = index;
        Reserved = reserved;
        KeyDestination = keyDestination;
        ValueDestination = valueDestination;
    }
}
