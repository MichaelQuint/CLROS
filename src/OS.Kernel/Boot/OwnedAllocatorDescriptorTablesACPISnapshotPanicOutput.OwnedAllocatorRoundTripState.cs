using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;

namespace OS.Boot;
public struct OwnedAllocatorRoundTripState
{
    internal OwnedAllocatorRoundTripStatus StatusValue;
    internal ulong PageValue;
    internal bool AllocatedValue, WrittenValue, RestoredValue, FreedValue;
    public OwnedAllocatorRoundTripStatus Status => StatusValue;
    public ulong Page => PageValue;
    public bool Allocated => AllocatedValue;
    public bool Written => WrittenValue;
    public bool Restored => RestoredValue;
    public bool Freed => FreedValue;
}
