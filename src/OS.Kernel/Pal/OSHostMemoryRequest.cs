using System.Runtime.InteropServices;

namespace OS.Pal;
[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostMemoryRequest
{
    public readonly ulong PageCount;
    public readonly ulong AlignmentBytes;
    public readonly ulong ProtectionFlags;
    public OSHostMemoryRequest(ulong pageCount, ulong alignmentBytes, ulong protectionFlags)
    {
        PageCount = pageCount;
        AlignmentBytes = alignmentBytes;
        ProtectionFlags = protectionFlags;
    }
}
