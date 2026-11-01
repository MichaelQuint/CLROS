using System.Runtime.InteropServices;

namespace OS.Pal;
[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostMemoryTableEntry
{
    public readonly OSHostHandle Allocation;
    public readonly OSHostMemoryRange Range;
    public readonly ulong StateFlags;
    public OSHostMemoryTableEntry(OSHostHandle allocation, OSHostMemoryRange range, ulong stateFlags)
    {
        Allocation = allocation;
        Range = range;
        StateFlags = stateFlags;
    }
}
