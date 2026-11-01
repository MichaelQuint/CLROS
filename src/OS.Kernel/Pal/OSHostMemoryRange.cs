using System.Runtime.InteropServices;

namespace OS.Pal;
[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostMemoryRange
{
    public readonly ulong BaseAddress;
    public readonly ulong ByteLength;
    public readonly ulong ProtectionFlags;
    public OSHostMemoryRange(ulong baseAddress, ulong byteLength, ulong protectionFlags)
    {
        BaseAddress = baseAddress;
        ByteLength = byteLength;
        ProtectionFlags = protectionFlags;
    }
}
