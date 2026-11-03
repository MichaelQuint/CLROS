using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Memory;
using OS.Kernel.Runtime;
using Heap = OS.Kernel.Runtime.ManagedHeapPageRangeLiveAllocator;
using Status = OS.Kernel.Runtime.ManagedHeapPageRangeStatus;

internal static unsafe partial class ManagedHeapPageRangeLiveAllocatorTests
{
    private struct Input
    {
        public BootPageAllocatorState* Allocator;
        public ulong AllocatorCapacity;
        public BootPageFreeListNode* Nodes;
        public ulong NodeCount, NodeCapacity;
        public BootReservedRange* Ledger;
        public ulong ReservationCount, ReservationCapacity, ParentIndex;
        public byte* Backing;
        public ulong BackingCapacity;
        public ManagedHeapPageRangeState* Heap;
        public ulong HeapCapacity;
    }
}
