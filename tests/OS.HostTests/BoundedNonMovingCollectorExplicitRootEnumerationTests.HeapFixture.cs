using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Runtime;
using OS.Kernel.Memory;
using Alloc = OS.Kernel.Runtime.GuestTestRhpNewFast;
using Core = OS.Kernel.Runtime.BoundedNonMovingCollectorExplicitRootEnumeration;
using Status = OS.Kernel.Runtime.CollectorTypeMapStatus;

internal static unsafe partial class BoundedNonMovingCollectorExplicitRootEnumerationTests
{
    internal sealed class HeapFixture : IDisposable
    {
        internal const ulong Capacity = 5520;
        internal byte* Storage = (byte*)NativeMemory.AlignedAlloc(65536, 4096);
        internal RhpNewFastHeapAccess Access;
        internal RhpNewFastBumpState* State => (RhpNewFastBumpState*)(Storage + 0x3000);

        internal HeapFixture(bool collecting = true, ulong stateCapacity = Capacity)
        {
            Require(Storage != null && Alloc.RequiredCollectingStateBytes == Capacity && sizeof(RhpNewFastBumpState) == 80, "owner extents");
            new Span<byte>(Storage, 65536).Fill(0xA5);
            Access = new()
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
            *Access.Allocator = new(true, 4096, 0, 1, 2, 8192, 0, 0);
            Access.Nodes[0] = new(0, 0, 0x300000000, 0x300002000, 2, 8192, -1);
            PhysicalAddress.TryCreateFromUefiBoundary(0x100000, out var image);
            PhysicalAddress.TryCreateFromUefiBoundary(0x300000000, out var arena);
            BootReservedRange.TryCreate(BootReservationKind.LoadedImage, image, 1, out Access.Reservations[0]);
            BootReservedRange.TryCreate(BootReservationKind.AllocatorArena, arena, 2, out Access.Reservations[1]);
            NativeMemory.Clear(Access.Heap, 216);
            NativeMemory.Clear(State, (nuint)Capacity);
            var result = ManagedHeapPageRangeLiveAllocator.ReserveAndInitializeOnce(Access.Allocator, 1, Access.Nodes, 1, 2, Access.Reservations, 2, 3, 1, Access.Backing, Access.BackingCapacity, Access.Heap, 232);
            Require(result.IsReady && Alloc.InitializeOnce(Access, State, stateCapacity).Succeeded && (!collecting || Alloc.EnableCollection(Access, State, stateCapacity) == RhpNewFastStatus.Success), "original retained owner initialization");
        }

        internal RhpNewFastResult Allocate(Fixture image)
        {
            var map = image.Inspect();
            Require(map.Succeeded, "type fixture");
            var result = Alloc.TryAllocate(Access, State, Capacity, Profile, map.Prefix, image.Mt);
            Require(result.AllocationSucceeded, "same-owner allocation: " + result.Status);
            return result;
        }

        internal bool Valid(RhpNewFastResult receipt) => Alloc.TryValidateRetainedAllocation(Access, State, Capacity, receipt) == RhpNewFastStatus.Success;
        internal byte[] Snapshot() => new ReadOnlySpan<byte>(Storage, 65536).ToArray();
        internal void Unchanged(byte[] before) => Require(new ReadOnlySpan<byte>(Storage, 65536).SequenceEqual(before), "all owner, page and canary bytes preserved");
        internal void Guards()
        {
            foreach (int offset in new[]
            {
                0,
                0x2F00,
                0x4600,
                0x8000,
                0xA000
            }

            )
                for (int i = 0; i < 64; i++)
                    Require(Storage[offset + i] == 0xA5, "heap outer canary");
        }

        public void Dispose() => NativeMemory.AlignedFree(Storage);
    }
}
