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
    private sealed class RootFixture : IDisposable
    {
        internal readonly Fixture Image;
        internal readonly HeapFixture Heap;
        private readonly byte* externalOwner;
        internal RhpNewFastHeapAccess Access;
        internal byte* Root;
        internal ulong RootCapacity = 0x1000, BumpCapacity = 96;
        internal RootFixture(Action<Fixture>? configure = null, bool externalRoot = false, int bumpOffset = 0)
        {
            Image = new(configure);
            Heap = new(bumpOffset == 0 ? null : Image.Image + bumpOffset);
            Access = Heap.Access;
            if (externalRoot)
            {
                externalOwner = (byte*)NativeMemory.Alloc(4112);
                Check(externalOwner != null, "external root allocation");
                new Span<byte>(externalOwner, 4112).Fill(0x5A);
                Root = externalOwner + 8;
                NativeMemory.Clear(Root, 4096);
            }
            else
                Root = Image.Image + 0x2800;
            Check(Core.RequiredRootOwnerBytes <= 0x800, "bounded root fixture");
        }

        internal GCStaticRootResult Call(bool query = false) => query ? Core.TryGetRetainedRoot(Image.Capture, Image.Mapping, Image.Owner, Fixture.Capacity, Image.Handle, Image.Metadata, Image.Cells, Image.Object, Access, Heap.Bump, BumpCapacity, Root, RootCapacity) : Core.MaterializeOnce(Image.Capture, Image.Mapping, Image.Owner, Fixture.Capacity, Image.Handle, Image.Metadata, Image.Cells, Image.Object, Access, Heap.Bump, BumpCapacity, Root, RootCapacity);
        internal byte[] Snapshot() => Image.Snapshot().Concat(new ReadOnlySpan<byte>(Heap.Storage, 65536).ToArray()).Concat(externalOwner == null ? Array.Empty<byte>() : new ReadOnlySpan<byte>(externalOwner, 4112).ToArray()).ToArray();
        internal void Unchanged(byte[] before) => Check(before.AsSpan().SequenceEqual(Snapshot()), "entire image/Type/root/heap/authority bytes unchanged");
        internal GCStaticRootResult Reject(GCStaticRootStatus? expected = null, bool query = false)
        {
            byte[] before = Snapshot();
            var r = Call(query);
            Unchanged(before);
            Check(!r.Succeeded && (!expected.HasValue || r.Status == expected.Value), "root reject expected=" + expected + " actual=" + r.Status);
            Check(r.ObjectReference == 0 && r.Cell == 0 && r.SeriesStart == 0 && r.SeriesBytes == 0 && r.AllocationBytes == 0, "no usable rejected root");
            return r;
        }

        internal void OnlyPublicationChanges(byte[] before, uint size)
        {
            byte[] after = Snapshot();
            int heapOffset = Image.Snapshot().Length;
            ulong bump = (ulong)Heap.Bump, image = (ulong)Image.Image;
            int bumpOffset = bump >= image && bump - image < Fixture.ImageSize ? 4096 + (int)(bump - image) : heapOffset + (int)((byte*)Heap.Bump - Heap.Storage);
            (int Start, int Bytes)[] allowed = [(4096 + (int)(Image.Cell - (ulong)Image.Image), 8), (externalOwner == null ? 4096 + (int)(Root - Image.Image) : heapOffset + 65536 + 8, (int)Core.RequiredRootOwnerBytes), (heapOffset + (int)(Heap.Page - Heap.Storage), (int)size), (bumpOffset + 64, 16)];
            for (int i = 0; i < before.Length; i++)
                if (!allowed.Any(r => i >= r.Start && i - r.Start < r.Bytes))
                    Check(before[i] == after[i], "publication wrote outside exact intervals " + i);
        }

        public void Dispose()
        {
            Image.Dispose();
            Heap.Dispose();
            NativeMemory.Free(externalOwner);
        }
    }
}
