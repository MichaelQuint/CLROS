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
    private sealed class Fixture : IDisposable
    {
        internal readonly byte* Storage = (byte*)NativeMemory.AlignedAlloc(65536, 4096);
        internal Input Input;
        internal Fixture()
        {
            Require(Storage != null, "host buffer allocation");
            Reset();
        }

        internal void Reset()
        {
            new Span<byte>(Storage, 65536).Fill(0xA5);
            Input = new Input
            {
                Allocator = (BootPageAllocatorState*)Storage,
                AllocatorCapacity = 1,
                Nodes = (BootPageFreeListNode*)(Storage + 0x100),
                NodeCount = 1,
                NodeCapacity = 2,
                Ledger = (BootReservedRange*)(Storage + 0x1000),
                ReservationCount = 2,
                ReservationCapacity = 3,
                ParentIndex = 1,
                Backing = Storage + 0x4000,
                BackingCapacity = 8192,
                Heap = (ManagedHeapPageRangeState*)(Storage + 0x2000),
                HeapCapacity = Heap.RequiredStateBytes
            };
            *Input.Allocator = State(2, 0);
            Input.Nodes[0] = Node(0x90000000, 2);
            Input.Nodes[1] = default;
            Input.Ledger[0] = Range(BootReservationKind.LoadedImage, 0x100000, 1);
            Input.Ledger[1] = Range(BootReservationKind.AllocatorArena, 0x90000000, 2);
            new Span<byte>(Input.Heap, (int)Input.HeapCapacity).Clear();
        }

        internal byte[] Snapshot() => new ReadOnlySpan<byte>(Storage, 65536).ToArray();
        internal void AssertUnchanged(byte[] before) => Require(new ReadOnlySpan<byte>(Storage, 65536).SequenceEqual(before), "complete caller storage preserved");
        internal void AssertOnlyChanged(byte[] before, params (int Start, int Length)[] changed)
        {
            for (int i = 0; i < before.Length; i++)
            {
                bool allowed = false;
                foreach (var range in changed)
                    allowed |= i >= range.Start && i < range.Start + range.Length;
                if (!allowed)
                    Require(Storage[i] == before[i], "outside byte changed at " + i);
            }
        }

        public void Dispose() => NativeMemory.AlignedFree(Storage);
    }
}
