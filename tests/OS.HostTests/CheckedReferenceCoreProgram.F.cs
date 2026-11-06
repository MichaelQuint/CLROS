using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Reflection.Metadata;
using System.Text;
using System.Xml.Linq;
#if CHECKED_REFERENCE_CORE_TEST
using OS.Kernel.Boot;
using OS.Kernel.Memory;
using OS.Kernel.Runtime;
using OS.Runtime;

internal static unsafe partial class CheckedReferenceCoreProgram
{

    private sealed class F : IDisposable
    {
        internal readonly byte* Storage = (byte*)NativeMemory.AlignedAlloc(98304, 4096);
        internal RhpNewFastHeapAccess Original;
        internal RhpNewFastHeapAccess* Access => (RhpNewFastHeapAccess*)(Storage + 0x3000);
        internal RhpNewFastBumpState* Bump => (RhpNewFastBumpState*)(Storage + 0x3100);
        internal RhpAllocationControl* Control => (RhpAllocationControl*)(Storage + 0x3200);
        internal RhpObservationStorage* Records => (RhpObservationStorage*)(Storage + 0x4000);
        internal CheckedReferenceState* State => (CheckedReferenceState*)(Storage + 0x6000);
        internal byte* Page => Storage + 0x8000;
        internal byte* Image => Storage + 0xC000;
        internal static CheckedReferenceGeometry Geometry => new() { CellReference = 8, CellTag = 16, CellPayload = 24, PairFirst = 0, PairLast = 8, PairTag = 16, BlockPayload = 8 };
        internal F()
        {
            Need(Storage != null, "mapped fixture allocation");
            new Span<byte>(Storage, 98304).Fill(0xA5);
            Original = new()
            {
                Allocator = (BootPageAllocatorState*)(Storage + 0x100),
                AllocatorCapacity = 1,
                Nodes = (BootPageFreeListNode*)(Storage + 0x200),
                NodeCount = 1,
                NodeCapacity = 1,
                Reservations = (BootReservedRange*)(Storage + 0x1000),
                ReservationCount = 5,
                ReservationCapacity = 5,
                ParentIndex = 4,
                Heap = (ManagedHeapPageRangeState*)(Storage + 0x2000),
                HeapCapacity = 216,
                Backing = Page,
                BackingCapacity = 8192
            };
            *Original.Allocator = new BootPageAllocatorState(true, 4096, 0, 1, 2, 8192, 0, 0);
            Original.Nodes[0] = new BootPageFreeListNode(0, 0, 0x400000, 0x402000, 2, 8192, -1);
            for (int i = 0; i < 5; i++)
            {
                OS.Boot.PhysicalAddress.TryCreateFromUefiBoundary(i == 4 ? 0x400000UL : 0x100000UL + (ulong)i * 0x10000, out var physical);
                Need(BootReservedRange.TryCreate(i == 4 ? BootReservationKind.AllocatorArena : BootReservationKind.LoadedImage,
                    physical, i == 4 ? 2UL : 1UL, out Original.Reservations[i]) == BootReservedRangeStatus.Success, "original reservation fixture");
            }
            new Span<byte>(Original.Heap, 216).Clear();
            new Span<byte>(Access, 104).Clear();
            new Span<byte>(Bump, 80).Clear();
            new Span<byte>(Control, 96).Clear();
            new Span<byte>(Records, 6672).Clear();
            new Span<byte>(State, sizeof(CheckedReferenceState)).Clear();
            new Span<byte>(Image, 16384).Clear();
            Need(ManagedHeapPageRangeLiveAllocator.ReserveAndInitializeOnce(Original.Allocator, 1, Original.Nodes, 1, 1,
                Original.Reservations, 5, 5, 4, Page, 8192, Original.Heap, 216).IsReady, "one original reserve");
            W32(Image, 0x5A4D);
            W32(Image + 60, 0x80);
            W32(Image + 0x80, 0x4550);
            W32(Image + 0x84, 0x00018664);
            W32(Image + 0x94, 0xF0);
            W32(Image + 0x98, 0x20B);
            W32(Image + 0xD0, 0x4000);
            W32(Image + 0xD4, 0x400);
            W32(Image + 0x190, 0x2000);
            W32(Image + 0x194, 0x1000);
            W32(Image + 0x198, 0x2000);
            W32(Image + 0x1AC, 0x40000040);
            W32((byte*)Token(1), 0x50000000);
            W32((byte*)Token(1) + 4, 24);
            W32((byte*)Token(3), 0x51000000);
            W32((byte*)Token(3) + 4, 56);
            W32((byte*)Token(5), 0x51000000);
            W32((byte*)Token(5) + 4, 208);
        }
        internal ulong Token(ulong id) => (ulong)Image + (id <= 2 ? 0x1100UL : id <= 4 ? 0x1800UL : 0x1F00UL);
        internal ulong Reference(ulong id) => (&Records->Object0)[id - 1].Reference;
        internal ulong Value(ulong index) => index % 3 == 2 ? 0 : index % 3 == 0 ? Reference(1) : Reference(2);
        internal void Start()
        {
            Need(RhpNewFastAllocationCore.Prepare(Original, Access, Bump, Control, Records, Image, 16384) == RhpAdmissionStatus.Success, "prepare");
            Need(RhpNewFastAllocationCore.Bind(Access, Bump, Control, Records).Succeeded, "bind");
        }
        internal void Six()
        {
            Start();
            ulong used = 0;
            for (ulong id = 1; id <= 6; id++)
            {
                ulong size = id <= 2 ? 24UL : id <= 4 ? 56UL : 208UL;
                new Span<byte>(Page + used, (int)size).Fill(0xBC);
                var result = RhpNewFastAllocationCore.AllocateReferences(Access, Bump, Control, Records, Token(id), Token(id), id);
                Need(result.Succeeded && result.Leaf.UsedAfter == used + size && result.Leaf.AllocationCount == id, "allocation result");
                byte[] expected = new byte[size];
                BinaryPrimitives.WriteUInt64LittleEndian(expected.AsSpan(8), Token(id));
                Need(new ReadOnlySpan<byte>(Page + used, (int)size).SequenceEqual(expected), "independent entire allocation bytes");
                ulong reference = result.Leaf.ObjectReference, scalar = id <= 4 ? RoleTag(id) : 0, address = id <= 2 ? reference + 8 : id <= 4 ? reference + 16 : 0;
                if (id <= 4)
                    W64((byte*)address, scalar);
                Control->ConstructorCount++;
                Need(RhpNewFastAllocationCore.CompleteReferenceAllocation(Access, Bump, Control, Records, id, reference, address, scalar) == RhpAdmissionStatus.Success, "completion");
                used += size;
            }
            // A patterned unused tail makes every subsequent whole-page oracle meaningful.
            new Span<byte>(Page + 576, 4096 - 576).Fill(0xA7);
        }
        internal bool PrepareState(CheckedReferenceState* state) => RhpNewFastAllocationCore.PrepareReferences(Access, Bump, Control, Records, state, Geometry);
        internal void Arm()
        {
            Need(PrepareState(State), "prepare six roles");
            W64((byte*)Reference(3) + 40, PairTag);
            for (ulong i = 0; i < 8; i++)
                W64((byte*)Reference(5) + 24 + i * 24, BlockTag(i));
            Need(RhpNewFastAllocationCore.CompleteReferenceScalars(Access, Bump, Control, Records, State), "exact scalar setup");
        }
        internal uint Apply(ulong operation, ulong destination, ulong operand, ulong bytes) => RhpNewFastAllocationCore.ApplyReference(Access, Bump, Control, Records, State, operation, destination, operand, bytes);
        internal void InitialStores()
        {
            for (ulong i = 0; i < 18; i++)
            {
                ulong destination = i < 2 ? Reference(3) + 24 + i * 8 : Reference(5) + 8 + (i - 2) / 2 * 24 + (i % 2) * 8;
                byte[] before = Snapshot();
                Need(Apply(2, destination, Value(i), 8) == 1, "initial exact slot");
                Only(before, (0x6000, sizeof(CheckedReferenceState)), (0x4000 + 8, 4096), (0x8000 + (int)(destination - (ulong)Page), 8));
                Need(Read((byte*)destination) == Value(i) && State->Calls == i + 1, "scalar literal readback");
            }
        }
        internal void Scalars()
        {
            Need(Apply(1, Reference(4) + 8, Reference(1), 8) == 1, "field store");
            Need(Apply(2, Reference(3) + 8, Reference(2), 8) == 1, "checked store");
        }
        internal void PairHelpers()
        {
            Need(Apply(3, Reference(4) + 24, Reference(3) + 24, 8) == 1, "Pair first helper");
            Need(Apply(3, Reference(4) + 32, Reference(3) + 32, 8) == 1, "Pair second helper");
        }
        internal bool Pair(ulong cookie) => RhpNewFastAllocationCore.CompleteReferencePair(Access, Bump, Control, Records, State, cookie);
        internal byte[] Snapshot() => new ReadOnlySpan<byte>(Storage, 98304).ToArray();
        internal void Unchanged(byte[] before) => Need(new ReadOnlySpan<byte>(Storage, 98304).SequenceEqual(before), "all98304 mapped bytes unchanged");
        internal void Only(byte[] before, params (int At, int Count)[] allowed)
        {
            for (int i = 0; i < before.Length; i++)
                if (Storage[i] != before[i] && !allowed.Any(x => i >= x.At && i - x.At < x.Count))
                    throw new InvalidOperationException("unexpected byte " + i);
        }
        public void Dispose() => NativeMemory.AlignedFree(Storage);
    }
}
#else
#endif
