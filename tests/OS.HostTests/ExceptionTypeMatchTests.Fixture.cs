using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Memory;
using OS.Kernel.Runtime;
using Alloc = OS.Kernel.Runtime.GuestTestRhpNewFast;
using Core = OS.Kernel.Runtime.ExceptionTypeMatch;
using Status = OS.Kernel.Runtime.ExceptionTypeMatchStatus;

internal static unsafe partial class ExceptionTypeMatchTests
{
    internal sealed class Fixture : IDisposable
    {
        internal const ulong StateCapacity = 5520;
        internal byte* ImageStorage = (byte*)NativeMemory.AlignedAlloc(0xD000, 4096);
        internal byte* HeapStorage = (byte*)NativeMemory.AlignedAlloc(65536, 4096);
        internal byte* Image => ImageStorage + 4096;
        internal byte* Owner => ImageStorage + 0xB000;
        internal RhpNewFastBumpState* State => (RhpNewFastBumpState*)(HeapStorage + 0x3000);

        internal RhpNewFastHeapAccess Access;
        internal readonly EfiLoadedImageRangeCapturePlan Capture;
        internal readonly M01LoadedImageVirtualMappingEvidence Mapping;
        internal readonly ulong Handle;
        internal readonly RhpNewFastResult Receipt;
        internal ulong Root => Address(0x1400);
        internal ulong Base => Address(0x1440);
        internal ulong Derived => Address(0x1480);
        internal ulong Sibling => Address(0x14C0);
        internal ulong Other => Address(0x1500);

        internal Fixture(Action<nint>? prepareImage = null)
        {
            Require(ImageStorage != null && HeapStorage != null, "fixture allocation");
            new Span<byte>(ImageStorage, 0xD000).Fill(0xA5);
            new Span<byte>(HeapStorage, 65536).Fill(0xA5);
            NativeMemory.Clear(Image, 0x8000);
            NativeMemory.Clear(Owner, 4096);
            W16(Image, 0x5A4D);
            W32(Image + 60, 0x80);
            W32(Image + 0x80, 0x4550);
            W16(Image + 0x84, 0x8664);
            W16(Image + 0x86, 4);
            W16(Image + 0x94, 0xF0);
            W16(Image + 0x98, 0x20B);
            W32(Image + 0xD0, 0x8000);
            W32(Image + 0xD4, 0x400);
            Section(0, ".rdata", 0x1000, 0x1000, 0x40000040);
            Section(1, ".data", 0x2000, 0x2000, 0xC0000040);
            Section(2, ".modules", 0x5000, 8, 0x40000040);
            Section(3, ".text", 0x6000, 0x1000, 0x60000020);
            W64(Image + 0x5000, Address(0x1000));
            W32(Image + 0x1000, 0x00525452);
            W16(Image + 0x1004, 26);
            W16(Image + 0x1006, 1);
            W16(Image + 0x100C, 4);
            Image[0x100E] = 16;
            Image[0x100F] = 1;
            Row(0, 201, 4, 0x1200);
            Row(1, 202, 8, 0x2100);
            Row(2, 204, 16, 0x2000);
            Row(3, 213, 0, 0x1210);
            var observation = EfiLoadedImageProtocolObservation.CaptureFromUefiBoundary(0, 0x1000, 0x2000, (ulong)Image, 0x8000);
            Capture = EfiLoadedImageRangeCapturePlan.Create(&observation, 1, 1, 4096);
            Mapping = M01LoadedImageVirtualMappingEvidence.CaptureCallerAttestedIdentity(Capture);
            prepareImage?.Invoke((nint)Image);
            var module = TypeManagerModuleMetadataLiveNativeAOTImage.RegisterOnce(Capture, Mapping, Owner, 4096);
            Require(module.Succeeded, "original module registration " + module.Status);
            Handle = module.Handle;
            Type(0x1400, 0);
            Type(0x1440, Root);
            Type(0x1480, Base);
            Type(0x14C0, Base);
            Type(0x1500, 0);
            Access = new()
            {
                Allocator = (BootPageAllocatorState*)(HeapStorage + 0x100),
                AllocatorCapacity = 1,
                Nodes = (BootPageFreeListNode*)(HeapStorage + 0x300),
                NodeCount = 1,
                NodeCapacity = 2,
                Reservations = (BootReservedRange*)(HeapStorage + 0x1000),
                ReservationCount = 2,
                ReservationCapacity = 3,
                ParentIndex = 1,
                Heap = (ManagedHeapPageRangeState*)(HeapStorage + 0x2000),
                HeapCapacity = 232,
                Backing = HeapStorage + 0x5000,
                BackingCapacity = 8208
            };
            *Access.Allocator = new(true, 4096, 0, 1, 2, 8192, 0, 0);
            Access.Nodes[0] = new(0, 0, 0x300000000, 0x300002000, 2, 8192, -1);
            PhysicalAddress.TryCreateFromUefiBoundary(0x100000, out var image);
            PhysicalAddress.TryCreateFromUefiBoundary(0x300000000, out var arena);
            BootReservedRange.TryCreate(BootReservationKind.LoadedImage, image, 1, out Access.Reservations[0]);
            BootReservedRange.TryCreate(BootReservationKind.AllocatorArena, arena, 2, out Access.Reservations[1]);
            NativeMemory.Clear(Access.Heap, 216);
            NativeMemory.Clear(State, (nuint)StateCapacity);
            var heap = ManagedHeapPageRangeLiveAllocator.ReserveAndInitializeOnce(Access.Allocator, 1, Access.Nodes, 1, 2, Access.Reservations, 2, 3, 1, Access.Backing, Access.BackingCapacity, Access.Heap, 232);
            Require(heap.IsReady && Alloc.RequiredCollectingStateBytes == StateCapacity && sizeof(RhpNewFastBumpState) == 80 && Alloc.InitializeOnce(Access, State, StateCapacity).Succeeded && Alloc.EnableCollection(Access, State, StateCapacity) == RhpNewFastStatus.Success, "original heap ownership");
            Receipt = Allocate(Derived);
        }

        internal ulong Address(int offset) => (ulong)(Image + offset);
        internal void Type(int offset, ulong parent, uint flags = ClassFlags, uint size = 24)
        {
            byte* p = Image + offset;
            NativeMemory.Clear(p, 24);
            W32(p, flags);
            W32(p + 4, size);
            W64(p + 8, parent);
            W32(p + 20, 42);
        }

        internal RhpNewFastResult Allocate(ulong mt)
        {
            var result = Alloc.TryAllocate(Access, State, StateCapacity, Profile, new NativeAotMethodTablePrefix(ClassFlags, 24, 0, 0, 0, 42), mt);
            Require(result.AllocationSucceeded, "original allocation " + result.Status);
            return result;
        }

        internal ExceptionTypeMatchResult Match(ulong target, RhpNewFastResult? receipt = null, ulong? reference = null, ulong? handle = null, ulong ownerCapacity = 4096, ulong stateCapacity = StateCapacity, NativeAotLayoutProfile profile = Profile)
        {
            RhpNewFastResult original = receipt ?? Receipt;
            return Core.Match(profile, Capture, Mapping, Owner, ownerCapacity, handle ?? Handle, Access, State, stateCapacity, original, reference ?? original.ObjectReference, target);
        }

        internal byte[] ImageSnapshot() => new ReadOnlySpan<byte>(ImageStorage, 0xD000).ToArray();
        internal byte[] HeapSnapshot() => new ReadOnlySpan<byte>(HeapStorage, 65536).ToArray();
        internal void Unchanged(byte[] image, byte[] heap)
        {
            Require(new ReadOnlySpan<byte>(ImageStorage, 0xD000).SequenceEqual(image) && new ReadOnlySpan<byte>(HeapStorage, 65536).SequenceEqual(heap), "all input and canary bytes preserved");
        }

        private void Row(int index, uint id, uint length, int start)
        {
            byte* p = Image + 0x1010 + index * 16;
            W32(p, id);
            W32(p + 4, length);
            W64(p + 8, Address(start));
        }

        private void Section(int index, string name, uint start, uint size, uint flags)
        {
            byte* p = Image + 0x188 + index * 40;
            for (int i = 0; i < name.Length; i++)
                p[i] = (byte)name[i];
            W32(p + 8, size);
            W32(p + 12, start);
            W32(p + 36, flags);
        }

        public void Dispose()
        {
            NativeMemory.AlignedFree(ImageStorage);
            NativeMemory.AlignedFree(HeapStorage);
        }
    }
}
