using System.Runtime.InteropServices;

namespace OS.Pal;
[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostConfigQuery
{
    public readonly OSHostByteSpan KeyUtf8;
    public readonly OSHostByteSpan Destination;
    public OSHostConfigQuery(OSHostByteSpan keyUtf8, OSHostByteSpan destination)
    {
        KeyUtf8 = keyUtf8;
        Destination = destination;
    }
}
