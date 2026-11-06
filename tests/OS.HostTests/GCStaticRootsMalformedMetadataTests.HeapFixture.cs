using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using OS.Boot;
using OS.Kernel.Runtime;
using OS.Kernel.Memory;
using Core = OS.Kernel.Runtime.GCStaticRootsMalformedMetadata;
using Status = OS.Kernel.Runtime.GCStaticMetadataStatus;
using Module = OS.Kernel.Runtime.TypeManagerModuleMetadataLiveNativeAOTImage;

internal static unsafe partial class GCStaticRootsMalformedMetadataTests
{
    private sealed class HeapFixture : IDisposable
    {
        internal readonly byte* Storage = (byte*)NativeMemory.AlignedAlloc(65536, 4096);
        internal RhpNewFastHeapAccess Access;
        internal RhpNewFastBumpState* Bump;
        internal byte* Page;
        internal HeapFixture(byte* imageBump = null)
        {
            Check(Storage != null, "heap fixture");
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
            Bump = (RhpNewFastBumpState*)(imageBump == null ? Storage + 0x3000 : imageBump);
            *Access.Allocator = new(true, 4096, 0, 1, 2, 8192, 0, 0);
            Access.Nodes[0] = new(0, 0, 0x300000000, 0x300002000, 2, 8192, -1);
            PhysicalAddress.TryCreateFromUefiBoundary(0x100000, out var image);
            PhysicalAddress.TryCreateFromUefiBoundary(0x300000000, out var arena);
            Check(BootReservedRange.TryCreate(BootReservationKind.LoadedImage, image, 1, out Access.Reservations[0]) == BootReservedRangeStatus.Success, "image reservation");
            Check(BootReservedRange.TryCreate(BootReservationKind.AllocatorArena, arena, 2, out Access.Reservations[1]) == BootReservedRangeStatus.Success, "arena reservation");
            NativeMemory.Clear(Access.Heap, 216);
            NativeMemory.Clear(Bump, 80);
            var h = ManagedHeapPageRangeLiveAllocator.ReserveAndInitializeOnce(Access.Allocator, Access.AllocatorCapacity, Access.Nodes, Access.NodeCount, Access.NodeCapacity, Access.Reservations, Access.ReservationCount, Access.ReservationCapacity, Access.ParentIndex, Access.Backing, Access.BackingCapacity, Access.Heap, Access.HeapCapacity);
            Check(h.IsReady, "genuine retained heap");
            Page = (byte*)h.MappedStart;
            Check(GuestTestRhpNewFast.InitializeOnce(Access, Bump, 96).Succeeded, "genuine bump");
        }

        public void Dispose() => NativeMemory.AlignedFree(Storage);
    }
}
