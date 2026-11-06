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
    private sealed class Fixture : IDisposable
    {
        internal const int ImageSize = 0x8000;
        internal const ulong Capacity = 0x1000;
        private readonly byte* imageAllocation;
        private readonly byte* ownerAllocation;
        internal readonly byte* Image;
        internal readonly byte* Owner;
        internal readonly EfiLoadedImageRangeCapturePlan Capture;
        internal readonly M01LoadedImageVirtualMappingEvidence Mapping;
        internal readonly ulong Handle;
        internal ulong Entry, Cell, Mt, Object;
        internal GCStaticReadableWindow Metadata, Cells;
        internal Fixture(Action<Fixture>? configure = null, bool inlineOwner = false)
        {
            imageAllocation = (byte*)NativeMemory.AlignedAlloc(ImageSize + 8192, 4096);
            ownerAllocation = (byte*)NativeMemory.Alloc((nuint)(Capacity + 16));
            if (imageAllocation == null || ownerAllocation == null)
                throw new OutOfMemoryException();
            new Span<byte>(imageAllocation, ImageSize + 8192).Fill(0xA5);
            new Span<byte>(ownerAllocation, (int)Capacity + 16).Fill(0x5A);
            Image = imageAllocation + 4096;
            NativeMemory.Clear(Image, ImageSize);
            Owner = inlineOwner ? Image + 0x2800 : ownerAllocation + 8;
            NativeMemory.Clear(Owner, (nuint)Capacity);
            W16(Image, 0x5A4D);
            W32(Image + 60, 0x80);
            W32(Image + 0x80, 0x4550);
            W16(Image + 0x84, 0x8664);
            W16(Image + 0x86, 4);
            W16(Image + 0x94, 0xF0);
            W16(Image + 0x98, 0x20B);
            W32(Image + 0xD0, ImageSize);
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
            WriteRow(1, 202, 8, 0x2100);
            WriteRow(2, 204, 16, 0x2000);
            WriteRow(3, 213, 0, 0x1210);
            SetLocations(0x1200, 0x2200, 0x1400, 0x1180);
            Metadata = new(Address(0x1100), 0xF00);
            Cells = new(Address(0x2000), 0x2000);
            configure?.Invoke(this);
            var observation = EfiLoadedImageProtocolObservation.CaptureFromUefiBoundary(0, 0x1000, 0x2000, (ulong)Image, ImageSize);
            Capture = EfiLoadedImageRangeCapturePlan.Create(&observation, 1, 1, 4096);
            Mapping = M01LoadedImageVirtualMappingEvidence.CaptureCallerAttestedIdentity(Capture);
            var r = Module.RegisterOnce(Capture, Mapping, Owner, Capacity);
            Check(r.Succeeded, "fixture genuine registration " + r.Status);
            Handle = r.Handle;
            // Pattern bytes outside the Type frozen headers and metadata payloads.
            new Span<byte>(Image + 0x7000, 0x1000).Fill(0xC3);
        }

        internal ulong Address(int offset) => (ulong)Image + (uint)offset;
        internal byte* Row(int i) => Image + 0x1010 + 16 * i;
        internal void SetLocations(int entry, int cell, int mt, int obj)
        {
            Entry = Address(entry);
            Cell = Address(cell);
            Mt = Address(mt);
            Object = Address(obj);
            WriteRow(0, 201, 4, entry);
            W32((byte*)Entry, unchecked((uint)(cell - entry)));
            W64((byte*)Cell, unchecked((uint)(mt - cell + 1)));
            byte[] literal = [0xF0, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 8, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 24, 0, 0, 0];
            literal.CopyTo(new Span<byte>((void*)(Mt - 24), 32));
            W64((byte*)Mt + 8, Object);
            W32((byte*)Object, 0x50000000);
            W32((byte*)Object + 4, 24);
            W64((byte*)Object + 8, 0);
        }

        private void WriteRow(int i, uint id, uint length, int start)
        {
            byte* r = Row(i);
            W32(r, id);
            W32(r + 4, length);
            W64(r + 8, Address(start));
        }

        private void Section(int i, string name, uint start, uint size, uint flags)
        {
            byte* p = Image + 0x188 + i * 40;
            for (int n = 0; n < name.Length; n++)
                p[n] = (byte)name[n];
            W32(p + 8, size);
            W32(p + 12, start);
            W32(p + 36, flags);
        }

        internal GCStaticMetadataResult Validate() => Core.ValidateOne(Capture, Mapping, Owner, Capacity, Handle, Metadata, Cells, Object);
        internal GCStaticMetadataResult Expect(Status status)
        {
            byte[] before = Snapshot();
            var result = Validate();
            AssertUnchanged(before);
            Check(result.Status == status, "expected=" + status + " actual=" + result.Status + " module=" + result.ModuleStatus);
            if (status != Status.Success)
                NoObservations(result);
            return result;
        }

        internal byte[] Snapshot()
        {
            byte[] result = new byte[ImageSize + 8192 + (int)Capacity + 16];
            new ReadOnlySpan<byte>(imageAllocation, ImageSize + 8192).CopyTo(result);
            new ReadOnlySpan<byte>(ownerAllocation, (int)Capacity + 16).CopyTo(result.AsSpan(ImageSize + 8192));
            return result;
        }

        internal void AssertUnchanged(byte[] before) => Check(before.AsSpan().SequenceEqual(Snapshot()), "entire mapped image/owner/canaries unchanged");
        public void Dispose()
        {
            NativeMemory.AlignedFree(imageAllocation);
            NativeMemory.Free(ownerAllocation);
        }
    }
}
