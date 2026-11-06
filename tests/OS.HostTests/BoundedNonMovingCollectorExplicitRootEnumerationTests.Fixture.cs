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
    internal sealed class Fixture : IDisposable
    {
        private readonly byte* storage;
        internal byte* Image => storage + 4096;
        internal byte* Owner => storage + 0xB000;

        internal readonly EfiLoadedImageRangeCapturePlan Capture;
        internal readonly M01LoadedImageVirtualMappingEvidence Mapping;
        internal readonly ulong Handle;
        internal ulong Mt => Address(0x1400);
        internal GCStaticReadableWindow Window => new(Address(0x1100), 0xF00);

        internal Fixture()
        {
            storage = (byte*)NativeMemory.AlignedAlloc(0xD000, 4096);
            if (storage == null)
                throw new OutOfMemoryException();
            new Span<byte>(storage, 0xD000).Fill(0xA5);
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
            var module = TypeManagerModuleMetadataLiveNativeAOTImage.RegisterOnce(Capture, Mapping, Owner, 4096);
            Require(module.Succeeded, "original TypeManager registration " + module.Status);
            Handle = module.Handle;
        }

        internal ulong Address(int offset) => (ulong)(Image + offset);
        internal void Type(uint flags, uint size, long count)
        {
            W32((byte*)Mt, flags);
            W32((byte*)Mt + 4, size);
            W64((byte*)Mt + 8, 0);
            W64((byte*)Mt + 16, 0);
            W64((byte*)Mt - 8, unchecked((ulong)count));
        }

        internal void Series(int index, long adjusted, ulong offset)
        {
            byte* p = (byte*)Mt - 24 - index * 16;
            W64(p, unchecked((ulong)adjusted));
            W64(p + 8, offset);
        }

        internal CollectorTypeMapResult Inspect(bool abbreviated = false, GCStaticReadableWindow window = default, ulong mt = 0, ulong handle = 0) => Core.InspectType(NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian, Capture, Mapping, Owner, 4096, handle == 0 ? Handle : handle, window.Bytes == 0 ? Window : window, mt == 0 ? Mt : mt, abbreviated);
        internal void Expect(Status expected)
        {
            byte[] before = Snapshot();
            var result = Inspect();
            Require(result.Status == expected && !result.Succeeded && result.MethodTable == 0 && result.SeriesCount == 0 && !result.TryGetSeries(0, out _), "expected " + expected + ", got " + result.Status);
            Require(Snapshot().AsSpan().SequenceEqual(before), "failure preserves complete input span");
        }

        internal byte[] Snapshot() => new ReadOnlySpan<byte>(storage, 0xD000).ToArray();
        internal void Guards()
        {
            foreach (int offset in new[]
            {
                0,
                0x9000,
                0xA000,
                0xC000
            }

            )
                for (int i = 0; i < 4096; i++)
                    Require(storage[offset + i] == 0xA5, "outer canary");
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

        public void Dispose() => NativeMemory.AlignedFree(storage);
    }
}
