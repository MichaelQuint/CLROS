using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Memory;
using OS.Kernel.Runtime;
using Leaf = OS.Kernel.Runtime.CheckedReferenceBulkBarriersIncludingDotNetN10Bulk;
using Heap = OS.Kernel.Runtime.ManagedHeapPageRangeLiveAllocator;
using Status = OS.Kernel.Runtime.CheckedReferenceBulkStatus;

internal static unsafe partial class CheckedReferenceBulkBarriersIncludingDotNetN10BulkTests
{
    private sealed class Fixture : IDisposable
    {
        internal readonly byte* Storage = (byte*)NativeMemory.AlignedAlloc(65536, 4096);
        internal RhpNewFastHeapAccess Access;
        internal RhpNewFastBumpState* State;
        internal byte* Page;
        internal int PageOffset => (int)(Page - Storage);

        internal Fixture()
        {
            Require(Storage != null, "native caller storage");
            Reset();
        }

        internal void Reset(bool secondPage = false)
        {
            new Span<byte>(Storage, 65536).Fill(0xA5);
            Access = new RhpNewFastHeapAccess
            {
                Allocator = (BootPageAllocatorState*)(Storage + 0x100),
                AllocatorCapacity = 1,
                Nodes = (BootPageFreeListNode*)(Storage + 0x300),
                NodeCount = 1,
                NodeCapacity = 2,
                Reservations = (BootReservedRange*)(Storage + 0x1000),
                ReservationCount = 2,
                ReservationCapacity = 3,
                ParentIndex = 1,
                Heap = (ManagedHeapPageRangeState*)(Storage + 0x2000),
                HeapCapacity = 232,
                Backing = Storage + 0x5000,
                BackingCapacity = 8208
            };
            State = (RhpNewFastBumpState*)(Storage + 0x3000);
            *Access.Allocator = new BootPageAllocatorState(true, 4096, 0, 1, secondPage ? 1UL : 2UL, secondPage ? 4096UL : 8192UL, secondPage ? 1UL : 0UL, secondPage ? 4096UL : 0UL);
            Access.Nodes[0] = Node(0x300000000 + (secondPage ? 4096UL : 0UL), secondPage ? 1UL : 2UL);
            Access.Reservations[0] = Range(BootReservationKind.LoadedImage, 0x100000, 1);
            Access.Reservations[1] = Range(BootReservationKind.AllocatorArena, 0x300000000, 2);
            new Span<byte>(Access.Heap, 216).Clear();
            new Span<byte>(State, 80).Clear();
            ManagedHeapPageRangeResult retained = Heap.ReserveAndInitializeOnce(Access.Allocator, Access.AllocatorCapacity, Access.Nodes, Access.NodeCount, Access.NodeCapacity, Access.Reservations, Access.ReservationCount, Access.ReservationCapacity, Access.ParentIndex, Access.Backing, Access.BackingCapacity, Access.Heap, Access.HeapCapacity);
            Require(retained.IsReady, "real retained-page initialization");
            Page = (byte*)retained.MappedStart;
        }

        internal void Pattern()
        {
            for (int i = 0; i < 4096; i++)
                Page[i] = (byte)((i * 37 + (i / 8) * 11 + 0x83) & 255);
        }

        internal byte[] Snapshot() => new ReadOnlySpan<byte>(Storage, 65536).ToArray();
        internal void Unchanged(byte[] expected) => Require(new ReadOnlySpan<byte>(Storage, 65536).SequenceEqual(expected), "complete storage/page/metadata/cursor/canary byte oracle");
        public void Dispose() => NativeMemory.AlignedFree(Storage);
    }
}
