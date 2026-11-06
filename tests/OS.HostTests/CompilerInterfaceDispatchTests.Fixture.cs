using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Memory;
using OS.Kernel.Runtime;
using Core = OS.Kernel.Runtime.CompilerInterfaceDispatch;
using Status = OS.Kernel.Runtime.CompilerInterfaceDispatchStatus;
using Alloc = OS.Kernel.Runtime.GuestTestRhpNewFast;
using Module = OS.Kernel.Runtime.TypeManagerModuleMetadataLiveNativeAOTImage;

internal static unsafe partial class CompilerInterfaceDispatchTests
{
    // Synthetic compiler-layout bytes with genuine original module registration
    // and genuine heap/collecting receipts. Returned RX addresses are compared,
    // never executed. Native callsites and argument preservation are guest proof.
    private sealed class Fixture : IDisposable
    {
        internal const uint ImageSize = 0x18000;
        internal const ulong Capacity = 32768;
        internal const NativeAotLayoutProfile Profile = NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian;
        internal readonly byte* Image = (byte*)NativeMemory.AlignedAlloc(ImageSize, 4096);
        internal readonly byte* Owner = (byte*)NativeMemory.AllocZeroed(4096);
        private readonly byte* runnerAllocation = (byte*)NativeMemory.AllocZeroed((nuint)Capacity + 16);
        private readonly byte* heapAllocation = (byte*)NativeMemory.AlignedAlloc(65536, 4096);
        internal readonly byte* Runner;
        internal RhpNewFastHeapAccess Heap;
        internal RhpNewFastBumpState* Allocator => (RhpNewFastBumpState*)(heapAllocation + 0x3000);

        internal readonly EfiLoadedImageRangeCapturePlan Capture;
        internal readonly M01LoadedImageVirtualMappingEvidence Mapping;
        internal readonly ulong Handle;
        internal ulong DerivedSealedBytes = 4;
        internal Fixture(bool inline = false, uint textFlags = 0x60000020, byte[]? info = null, uint infoId = 209, uint cellsId = 210, uint referencesId = 331)
        {
            Check(Image != null && Owner != null && runnerAllocation != null && heapAllocation != null, "fixture allocation");
            NativeMemory.Clear(Image, ImageSize);
            NativeMemory.Clear(heapAllocation, 65536);
            Runner = inline ? Image + 0x6000 : runnerAllocation + 8;
            *(ulong*)(Runner - 8) = 0x0123456789ABCDEF;
            *(ulong*)(Runner + Capacity) = 0xFEDCBA9876543210;
            *(ushort*)Image = 0x5A4D;
            *(uint*)(Image + 60) = 0x80;
            *(uint*)(Image + 0x80) = 0x4550;
            *(ushort*)(Image + 0x84) = 0x8664;
            *(ushort*)(Image + 0x86) = 4;
            *(ushort*)(Image + 0x94) = 0xF0;
            *(ushort*)(Image + 0x98) = 0x20B;
            *(uint*)(Image + 0xD0) = ImageSize;
            *(uint*)(Image + 0xD4) = 0x400;
            Section(0, ".rdata", 0x1000, 0x4000, 0x40000040);
            Section(1, ".data", 0x5000, 0xA000, 0xC0000040);
            Section(2, ".modules", 0x10000, 16, 0x40000040);
            Section(3, ".text", 0x11000, 0x1000, textFlags);
            *(ulong*)(Image + 0x10000) = Address(0x1000);
            *(uint*)(Image + 0x1000) = 0x00525452;
            *(ushort*)(Image + 0x1004) = 26;
            *(ushort*)(Image + 0x1006) = 1;
            *(ushort*)(Image + 0x100C) = 4;
            Image[0x100E] = 16;
            Image[0x100F] = 1;
            // Five explicit NativeArray leaves: indices 0..4 each select the
            // corresponding external reference and interface slot zero.
            info ??= [40, 1, 2, 102, 54, 30, 0, 0, 2, 0, 30, 4, 0, 6, 0, 32, 8, 0];
            info.CopyTo(new Span<byte>(Image + 0x1200, info.Length));
            ModuleSection(0, 204, 0x5000, 16);
            ModuleSection(1, infoId, 0x1200, info.Length);
            ModuleSection(2, cellsId, 0x5100, 80);
            ModuleSection(3, referencesId, 0x1300, 20);
            var observation = EfiLoadedImageProtocolObservation.CaptureFromUefiBoundary(0, 0x1000, 0x2000, (ulong)Image, ImageSize);
            Capture = EfiLoadedImageRangeCapturePlan.Create(&observation, 1, 1, 4096);
            Mapping = M01LoadedImageVirtualMappingEvidence.CaptureCallerAttestedIdentity(Capture);
            var registration = Module.RegisterOnce(Capture, Mapping, Owner, 4096);
            Check(registration.Succeeded, "registered fixture module " + registration.Status);
            Handle = registration.Handle;
            Type(0x1800, 0x50000000, 24, 0, [], []);
            Type(0x1900, 0x54000000, 0, 0, [], []);
            Type(0x1A00, 0x54000000, 0, 0, [], []);
            Type(0x1B00, 0x54830000, 1, 0, [], [], composition: 0x4200);
            Image[0x4200] = 1;
            Type(0x1C00, 0x54830000, 1, 0, [], [], composition: 0x4210);
            Image[0x4210] = 2;
            Type(0x1D00, 0x50000000, 24, 0x1800, [], []);
            Type(0x1E00, 0x50000000, 24, 0x1D00, [], []);
            Type(0x1F00, 0x56800000, 0, 0, [], [], definition: 0x1B00, composition: 0x1D00);
            Type(0x2000, 0x56800000, 0, 0, [], [], definition: 0x1B00, composition: 0x1E00);
            Type(0x2100, 0x56800000, 0, 0, [], [], definition: 0x1C00, composition: 0x1D00);
            Type(0x2200, 0x56800000, 0, 0, [], [], definition: 0x1C00, composition: 0x1E00);
            Type(0x2300, 0x50440000, 24, 0x1800, [0x11000], [0x1900, 0x2000, 0x2100], 0x3800, 0x3900);
            Map(0x3800, (0, 0, 1), (1, 0, 2), (2, 0, 3));
            Rel(0x3900, 0x11010);
            Rel(0x3904, 0x11030);
            Rel(0x3908, 0x11040);
            Type(0x2400, 0x50440000, 24, 0x1800, [0x11000], [0x1900, 0x1F00, 0x2200], 0x3A00, 0x3A80);
            Map(0x3A00, (0, 0, 1), (1, 0, 2), (2, 0, 3));
            Rel(0x3A80, 0x11020);
            Rel(0x3A84, 0x11050);
            Rel(0x3A88, 0x11060);
            Type(0x2500, 0x50440000, 24, 0x1800, [0x11000], [0x1900], 0x3B00, 0x3C00);
            Map(0x3B00, (0, 0, 1));
            Rel(0x3C00, 0x11070);
            Type(0x2600, 0x50000000, 24, 0x2500, [0x11080, 0x11090, 0x110A0], [0x1A00, 0x1900]);
            int[] requested = [0x1900, 0x1F00, 0x2200, 0x2000, 0x2100];
            for (int i = 0; i < requested.Length; i++)
                Rel(0x1300 + i * 4, requested[i]);
            Heap = new()
            {
                Allocator = (BootPageAllocatorState*)(heapAllocation + 0x100),
                AllocatorCapacity = 1,
                Nodes = (BootPageFreeListNode*)(heapAllocation + 0x300),
                NodeCount = 1,
                NodeCapacity = 2,
                Reservations = (BootReservedRange*)(heapAllocation + 0x1000),
                ReservationCount = 2,
                ReservationCapacity = 3,
                ParentIndex = 1,
                Heap = (ManagedHeapPageRangeState*)(heapAllocation + 0x2000),
                HeapCapacity = 232,
                Backing = heapAllocation + 0x5000,
                BackingCapacity = 8208
            };
            *Heap.Allocator = new(true, 4096, 0, 1, 2, 8192, 0, 0);
            Heap.Nodes[0] = new(0, 0, 0x300000000, 0x300002000, 2, 8192, -1);
            PhysicalAddress.TryCreateFromUefiBoundary(0x100000, out var image);
            PhysicalAddress.TryCreateFromUefiBoundary(0x300000000, out var arena);
            BootReservedRange.TryCreate(BootReservationKind.LoadedImage, image, 1, out Heap.Reservations[0]);
            BootReservedRange.TryCreate(BootReservationKind.AllocatorArena, arena, 2, out Heap.Reservations[1]);
            var heap = ManagedHeapPageRangeLiveAllocator.ReserveAndInitializeOnce(Heap.Allocator, 1, Heap.Nodes, 1, 2, Heap.Reservations, 2, 3, 1, Heap.Backing, Heap.BackingCapacity, Heap.Heap, 232);
            Check(heap.IsReady && Alloc.InitializeOnce(Heap, Allocator, Alloc.RequiredCollectingStateBytes).Succeeded && Alloc.EnableCollection(Heap, Allocator, Alloc.RequiredCollectingStateBytes) == RhpNewFastStatus.Success, "original collecting owner");
        }

        internal Status Bind(ulong bytes = 80, uint cells = 8, uint cache = 8, byte* storage = null, ulong start = 0, bool admitSealed = true)
        {
            CompilerInterfaceDispatchSealedTable* tables = stackalloc CompilerInterfaceDispatchSealedTable[4];
            tables[0] = new()
            {
                DeclaringType = Address(0x2300),
                Table = Address(0x3900),
                Bytes = 12
            };
            tables[1] = new()
            {
                DeclaringType = Address(0x2400),
                Table = Address(0x3A80),
                Bytes = 12
            };
            tables[2] = new()
            {
                DeclaringType = Address(0x2500),
                Table = Address(0x3C00),
                Bytes = 4
            };
            tables[3] = new()
            {
                DeclaringType = Address(0x2600),
                Table = Address(0x3E00),
                Bytes = DerivedSealedBytes
            };
            uint count = (*(uint*)(Image + 0x2600) & 0x00400000) != 0 ? 4U : 3U;
            return Core.Bind(Capture, Mapping, Owner, 4096, Handle, Heap, Allocator, Alloc.RequiredCollectingStateBytes, storage == null ? Runner : storage, Capacity, Address(0x11000), start == 0 ? Address(0x5100) : start, bytes, cells, cache, admitSealed ? tables : null, admitSealed ? count : 0);
        }

        internal RhpNewFastResult Allocate(int offset)
        {
            byte* p = Image + offset;
            var result = Alloc.TryAllocate(Heap, Allocator, Alloc.RequiredCollectingStateBytes, Profile, new(*(uint*)p, *(uint*)(p + 4), *(ulong*)(p + 8), *(ushort*)(p + 16), *(ushort*)(p + 18), *(uint*)(p + 20)), Address(offset));
            Check(result.AllocationSucceeded, "receiver allocation " + result.Status);
            return result;
        }

        internal CompilerInterfaceDispatchResolution Resolve(RhpNewFastResult receiver, int cell)
        {
            Expect(Core.Resolve(Runner, Capacity, receiver.ObjectReference, Cell(cell), out var result), Status.Success);
            return result;
        }

        internal void Reject(ulong receiver, int cell, Status expected)
        {
            Expect(Core.Resolve(Runner, Capacity, receiver, Cell(cell), out var result), expected);
            Check(result.Target == 0 && result.ReceiverType == 0 && result.AllocationSequence == 0 && !result.CacheHit, "rejection has no dispatch payload");
        }

        internal ulong Address(int offset) => (ulong)Image + (uint)offset;
        internal ulong Cell(int index) => Address(0x5100 + index * 16);
        private void ModuleSection(int index, uint id, int start, int bytes)
        {
            byte* row = Image + 0x1010 + index * 16;
            *(uint*)row = id;
            *(int*)(row + 4) = bytes;
            *(ulong*)(row + 8) = Address(start);
        }

        internal void Rel(int at, int to, int tag = 0) => *(int*)(Image + at) = to - at + tag;
        internal void Type(int at, uint flags, uint size, int parent, int[] slots, int[] interfaces, int map = 0, int sealedTable = 0, int definition = 0, int composition = 0)
        {
            NativeMemory.Clear(Image + at, 0x100);
            *(uint*)(Image + at) = flags;
            *(uint*)(Image + at + 4) = size;
            *(ulong*)(Image + at + 8) = parent == 0 ? 0 : Address(parent);
            *(ushort*)(Image + at + 16) = (ushort)slots.Length;
            *(ushort*)(Image + at + 18) = (ushort)interfaces.Length;
            int tail = at + 24;
            foreach (int slot in slots)
            {
                *(ulong*)(Image + tail) = Address(slot);
                tail += 8;
            }

            foreach (int type in interfaces)
            {
                *(ulong*)(Image + tail) = Address(type);
                tail += 8;
            }

            Rel(tail, 0x5000);
            tail += 8;
            if ((flags & 0x00040000) != 0)
            {
                Rel(tail, map);
                tail += 4;
            }

            if ((flags & 0x00400000) != 0)
            {
                Rel(tail, sealedTable);
                tail += 4;
            }

            if ((flags & 0x02000000) != 0)
            {
                Rel(tail, definition);
                tail += 4;
            }

            if (composition != 0)
                Rel(tail, composition);
        }

        internal void Map(int at, params (ushort Index, ushort Slot, ushort Implementation)[] rows)
        {
            NativeMemory.Clear(Image + at, 0x60);
            *(ushort*)(Image + at) = (ushort)rows.Length;
            for (int i = 0; i < rows.Length; i++)
            {
                byte* p = Image + at + 8 + i * 6;
                *(ushort*)p = rows[i].Index;
                *(ushort*)(p + 2) = rows[i].Slot;
                *(ushort*)(p + 4) = rows[i].Implementation;
            }
        }

        internal byte[] RunnerBytes() => new ReadOnlySpan<byte>(Runner, (int)Capacity).ToArray();
        internal byte[] ImageBytes() => new ReadOnlySpan<byte>(Image, (int)ImageSize).ToArray();
        internal void Guards() => Check(*(ulong*)(Runner - 8) == 0x0123456789ABCDEF && *(ulong*)(Runner + Capacity) == 0xFEDCBA9876543210, "controller canaries");
        private void Section(int i, string name, uint start, uint bytes, uint flags)
        {
            byte* p = Image + 0x188 + i * 40;
            for (int n = 0; n < name.Length; n++)
                p[n] = (byte)name[n];
            *(uint*)(p + 8) = bytes;
            *(uint*)(p + 12) = start;
            *(uint*)(p + 36) = flags;
        }

        public void Dispose()
        {
            NativeMemory.AlignedFree(Image);
            NativeMemory.Free(Owner);
            NativeMemory.Free(runnerAllocation);
            NativeMemory.AlignedFree(heapAllocation);
        }
    }
}
