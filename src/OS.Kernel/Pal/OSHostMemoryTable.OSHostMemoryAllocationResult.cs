using System.Runtime.InteropServices;

namespace OS.Pal;

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostMemoryAllocationResult
{
    public readonly OSHostStatus Status;
    public readonly uint Reserved;
    public readonly OSHostHandle Allocation;
    public readonly OSHostMemoryRange Range;
    public OSHostMemoryAllocationResult(OSHostStatus status, OSHostHandle allocation, OSHostMemoryRange range)
    {
        Status = status;
        Reserved = 0;
        Allocation = allocation;
        Range = range;
    }

    public bool Succeeded => Status == OSHostStatus.Ok && Allocation.IsValid && Range.BaseAddress != 0 && Range.ByteLength != 0;

    public static OSHostMemoryAllocationResult FromStatus(OSHostStatus status)
    {
        return new OSHostMemoryAllocationResult(status, default, default);
    }
}
