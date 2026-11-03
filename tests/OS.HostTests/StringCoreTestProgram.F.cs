using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
#if STRING_CORE_TEST
using OS.Boot;
using OS.Kernel.Boot;
using OS.Kernel.Memory;
using OS.Kernel.Runtime;
using OS.Runtime;

internal static unsafe partial class StringCoreTestProgram
{

    private sealed class F : IDisposable
    {
        internal readonly byte* Storage = (byte*)NativeMemory.AlignedAlloc(98304, 4096);
        internal RhpNewFastHeapAccess Original;
        internal RhpNewFastHeapAccess* Access => (RhpNewFastHeapAccess*)(Storage + 0x3000);
        internal RhpNewFastBumpState* Bump => (RhpNewFastBumpState*)(Storage + 0x3100);
        internal RhpAllocationControl* Control => (RhpAllocationControl*)(Storage + 0x3200);
        internal RhpObservationStorage* Records => (RhpObservationStorage*)(Storage + 0x4000);
        internal byte* Page => Storage + 0x8000;
        internal byte* Image => Storage + 0xC000;
        internal F()
        {
            Need(Storage != null, "native storage");
            Reset();
            Prefixes();
        }
        internal void Prefixes()
        {
            PrefixBytes(1, 0x50000000, 24, 0);
            PrefixBytes(2, 0xE0020001, 24, Token(5));
            PrefixBytes(3, 0xD0000002, 22, Token(4));
            PrefixBytes(4, 0x50000000, 24, 0);
            PrefixBytes(5, 0x14000000, 24, 0);
        }
        internal void PrefixBytes(ulong id, uint flags, uint size, ulong related)
        {
            W32((byte*)Token(id), flags);
            W32((byte*)Token(id) + 4, size);
            W64((byte*)Token(id) + 8, related);
        }
        internal ulong StringToken(ulong id) => Token(id >= 3 ? 3UL : id);
        internal RhpAllocationAttempt Strings(ulong id, long length) => RhpNewFastAllocationCore.AllocateStrings(Access, Bump, Control, Records,
            StringToken(id), StringToken(id), id, id == 1 ? 1UL : id == 2 ? 6UL : 5UL, length, id == 1 ? 0 : id == 2 ? Token(5) : Token(4));
        internal RhpAdmissionStatus Done(ulong id, ulong reference, int length, ulong scalar) =>
            RhpNewFastAllocationCore.CompleteStrings(Access, Bump, Control, Records, id, reference, length, scalar, scalar);

        internal void Reset()
        {
            new Span<byte>(Storage, 98304).Fill(0xA5);
            Original = new RhpNewFastHeapAccess
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
                PhysicalAddress.TryCreateFromUefiBoundary(i == 4 ? 0x400000UL : 0x100000UL + (ulong)i * 0x10000, out var physical);
                Need(BootReservedRange.TryCreate(i == 4 ? BootReservationKind.AllocatorArena : BootReservationKind.LoadedImage,
                    physical, i == 4 ? 2UL : 1UL, out Original.Reservations[i]) == BootReservedRangeStatus.Success, "flat fixture ledger");
            }
            new Span<byte>(Original.Heap, 216).Clear();
            new Span<byte>(Access, 104).Clear();
            new Span<byte>(Bump, 80).Clear();
            new Span<byte>(Control, 96).Clear();
            new Span<byte>(Records, 6672).Clear();
            new Span<byte>(Image, 16384).Clear();
            var reserved = ManagedHeapPageRangeLiveAllocator.ReserveAndInitializeOnce(Original.Allocator, 1, Original.Nodes, 1, 1,
                Original.Reservations, 5, 5, 4, Page, 8192, Original.Heap, 216);
            Need(reserved.IsReady, "actual original reserve");
            W32(Image, 0x5A4D);
            W32(Image + 60, 0x80);
            W32(Image + 0x80, 0x4550);
            W32(Image + 0x84, 0x00018664);
            W32(Image + 0x94, 0xF0);
            W32(Image + 0x98, 0x20B);
            W32(Image + 0xD0, 0x4000);
            W32(Image + 0xD4, 0x400);
            W32(Image + 0x190, 0x1000);
            W32(Image + 0x194, 0x1000);
            W32(Image + 0x198, 0x1000);
            W32(Image + 0x1AC, 0x40000040);
            for (ulong id = 1; id <= 10; id++)
            {
                uint flags = id <= 4 ? 0x50000000U : id is 5 or 6 ? 0x20000000U : 0x40000000U;
                if (id == 2 || id == 3 || id == 8 || id == 9)
                    flags |= 0x01000000;
                if (id == 3 || id == 4 || id == 9)
                    flags |= 0x00800000;
                if (id == 10)
                    flags = 0x48000300; // Nullable element18 and permitted encoded value offset.
                W32((byte*)Token(id), flags);
                W32((byte*)Token(id) + 4, Size(id));
                W64((byte*)Token(id) + 8, 0xFEDCBA9876543210);
                W64((byte*)Token(id) + 16, 0x1234567800020001);
            }
        }
        internal uint Size(ulong id) => id is 2 or 3 or 4 or 8 ? 32U : 24U;
        internal ulong Token(ulong id) => (ulong)Image + 0x1100 + (id - 1) * 32;
        internal RhpAdmissionStatus Prepare() => RhpNewFastAllocationCore.Prepare(Original, Access, Bump, Control, Records, Image, 16384);
        internal RhpNewFastResult Bind() => RhpNewFastAllocationCore.Bind(Access, Bump, Control, Records);
        internal RhpAllocationAttempt Allocate(ulong id, ulong token = 0) => RhpNewFastAllocationCore.Allocate(Access, Bump, Control, Records,
            token == 0 ? Token(id) : token, Token(id), id, id <= 4 ? 1UL : 2UL);
        internal RhpAdmissionStatus Complete(ulong id, ulong reference)
        {
            ulong scalar = id <= 4 ? id * 1111 : 0, offset = id <= 4 ? (id == 1 ? 8UL : 16UL) : 0;
            if (id <= 4)
                W64((byte*)reference + offset, scalar);
            return RhpNewFastAllocationCore.Complete(Access, Bump, Control, Records, id, reference, offset, scalar, scalar);
        }
        internal RhpAdmissionStatus Check() => RhpNewFastAllocationCore.CheckCompleted(Access, Bump, Control, Records);
        internal ManagedHeapPageRangeResult Retained() => RhpNewFastAllocationCore.Retained(Original);
        internal bool Prefix(ulong token, ulong expected) => RhpNewFastAllocationCore.TryReadPrefix(Image, 16384, token, expected, out _, out _, out _, out _);
        internal byte[] Snapshot() => new ReadOnlySpan<byte>(Storage, 98304).ToArray();
        internal byte[] PageSnapshot() => new ReadOnlySpan<byte>(Page, 4096).ToArray();
        internal byte[] BumpSnapshot() => new ReadOnlySpan<byte>(Bump, 80).ToArray();
        internal void Unchanged(byte[] x) => Need(new ReadOnlySpan<byte>(Storage, 98304).SequenceEqual(x), "all supplied bytes unchanged");
        internal void PageUnchanged(byte[] x) => Need(new ReadOnlySpan<byte>(Page, 4096).SequenceEqual(x), "all4096 actual page bytes unchanged");
        internal void BumpUnchanged(byte[] x) => Need(new ReadOnlySpan<byte>(Bump, 80).SequenceEqual(x), "all80 actual bump bytes unchanged");
        internal void Only(byte[] before, params (int Start, int Count)[] allowed)
        {
            for (int i = 0; i < before.Length; i++)
                if (before[i] != Storage[i] && !allowed.Any(x => i >= x.Start && i < x.Start + x.Count))
                    throw new InvalidOperationException("outside allowed mutation at " + i);
        }
        public void Dispose() => NativeMemory.AlignedFree(Storage);
    }
}
#else
#endif
