using System.Runtime.InteropServices;

namespace OS.Pal;
[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostMemoryQueryResult
{
    public readonly OSHostMemoryRange Range;
    public readonly ulong PageSizeBytes;
    public readonly ulong StateFlags;
    public OSHostMemoryQueryResult(OSHostMemoryRange range, ulong pageSizeBytes, ulong stateFlags)
    {
        Range = range;
        PageSizeBytes = pageSizeBytes;
        StateFlags = stateFlags;
    }
}
