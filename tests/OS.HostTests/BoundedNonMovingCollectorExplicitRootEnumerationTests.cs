using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Runtime;
using Core = OS.Kernel.Runtime.BoundedNonMovingCollectorExplicitRootEnumeration;
using Status = OS.Kernel.Runtime.CollectorTypeMapStatus;

internal static unsafe class BoundedNonMovingCollectorExplicitRootEnumerationTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        Console.WriteLine("TEST_START m04.013.reference_maps");
        try
        {
            using Fixture f = new();
            Require(!default(CollectorTypeMapResult).Succeeded &&
                !default(CollectorTypeMapResult).TryGetSeries(0, out _), "default denied");
            f.Type(0x50000000, 24, 0);
            Require(f.Inspect().SeriesCount == 0 && f.Inspect().Succeeded, "pointer-free fixed class");
            f.Type(0x51000000, 24, 1);
            f.Series(0, -16, 8);
            CollectorTypeMapResult one = f.Inspect();
            Require(one.Succeeded && one.SeriesCount == 1 && one.Prefix.BaseSize == 24 &&
                one.MethodTable == f.Mt && one.TryGetSeries(0, out var series) &&
                series.OffsetFromReference == 8 && series.Bytes == 8 && !one.TryGetSeries(1, out _),
                "ordinary compiler-format reference map");

            f.Type(0x01000000, 24, 1); f.Series(0, -16, 8);
            var abbreviated = f.Inspect(true, new(f.Mt - 24, 40));
            Require(abbreviated.Succeeded && abbreviated.SeriesCount == 1, "exact sixteen-byte static MT window");
            Require(f.Inspect(false, new(f.Mt - 24, 40)).Status == Status.InvalidMethodTable,
                "ordinary prefix cannot read beyond the caller window");

            f.Type(0x51000000, 80, 4);
            ulong[] offsets = [56, 8, 40, 24];
            for (int i = 0; i < 4; i++) f.Series(i, -72, offsets[i]);
            CollectorTypeMapResult four = f.Inspect();
            Require(four.Succeeded && four.SeriesCount == 4, "four disjoint series in metadata order");
            for (uint i = 0; i < 4; i++)
                Require(four.TryGetSeries(i, out series) && series.OffsetFromReference == offsets[i] &&
                    series.Bytes == 8, "every series decoded");
            f.Series(3, -72, 40); f.Expect(Status.OverlappingSeries);

            foreach (long count in new long[] { 0, -1, long.MinValue, 5, long.MaxValue })
            { f.Type(0x51000000, 24, count); f.Expect(Status.UnsupportedSeriesCount); }
            foreach ((long adjusted, ulong offset) in new (long, ulong)[]
            { (long.MinValue, 8), (long.MaxValue, 8), (-24, 8), (-15, 8), (-17, 8), (-16, 0), (-16, 9), (-16, 16), (-16, ulong.MaxValue) })
            { f.Type(0x51000000, 24, 1); f.Series(0, adjusted, offset); f.Expect(Status.InvalidDescriptor); }
            foreach (uint flags in new uint[] { 0x51100000, 0xD1000008, 0x61000008, 0x51200000 })
            { f.Type(flags, 24, 1); f.Series(0, -16, 8); f.Expect(Status.UnsupportedType); }
            foreach (uint size in new uint[] { 0, 16, 25, 4104, uint.MaxValue })
            { f.Type(0x51000000, size, 1); f.Expect(Status.UnsupportedType); }

            f.Type(0x51000000, 24, 1); f.Series(0, -16, 8);
            Require(f.Inspect(false, new(f.Mt - 8, 32)).Status == Status.InvalidDescriptor,
                "complete descriptor extent required");
            Require(f.Inspect(false, new(ulong.MaxValue - 7, 16)).Status == Status.InvalidWindow,
                "window overflow denied before dereference");
            Require(f.Inspect(false, default, f.Mt + 1).Status == Status.InvalidMethodTable, "unaligned MT");
            Require(f.Inspect(false, default, 0x1000).Status == Status.InvalidMethodTable, "foreign MT");
            Require(f.Inspect(false, default, 0, f.Handle ^ 8).Status == Status.ModuleRejected, "wrong module handle");
            Require(Core.InspectType(default, f.Capture, f.Mapping, f.Owner, 4096, f.Handle,
                f.Window, f.Mt).Status == Status.UnsupportedProfile, "unpinned ABI denied");

            // A syntactically valid MT in writable data is not immutable metadata.
            NativeMemory.Copy((void*)(f.Mt - 24), f.Image + 0x2400, 48);
            Require(f.Inspect(false, new(f.Address(0x2400), 48), f.Address(0x2418)).Status == Status.ModuleRejected,
                "registered PE permissions required");
            byte[] before = f.Snapshot();
            for (int i = 0; i < 20; i++) Require(f.Inspect().Succeeded, "warmup");
            long start = GC.GetAllocatedBytesForCurrentThread();
            bool valid = true;
            for (int i = 0; i < 1000; i++) valid &= f.Inspect().Succeeded;
            long bytes = GC.GetAllocatedBytesForCurrentThread() - start;
            Require(valid && bytes == 0 && f.Snapshot().AsSpan().SequenceEqual(before),
                "repeated inspection allocates and changes no caller bytes");
            f.Guards();
            Console.WriteLine("TEST_PASS m04.013.reference_maps");
        }
        catch (Exception error)
        {
            Console.WriteLine("TEST_FAIL m04.013.reference_maps " + error.Message);
            throw;
        }
    }

    private static void Require(bool value, string reason)
    { if (!value) throw new InvalidOperationException(reason); }

    private sealed class Fixture : IDisposable
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
            if (storage == null) throw new OutOfMemoryException();
            new Span<byte>(storage, 0xD000).Fill(0xA5);
            NativeMemory.Clear(Image, 0x8000); NativeMemory.Clear(Owner, 4096);
            W16(Image, 0x5A4D); W32(Image + 60, 0x80); W32(Image + 0x80, 0x4550);
            W16(Image + 0x84, 0x8664); W16(Image + 0x86, 4); W16(Image + 0x94, 0xF0);
            W16(Image + 0x98, 0x20B); W32(Image + 0xD0, 0x8000); W32(Image + 0xD4, 0x400);
            Section(0, ".rdata", 0x1000, 0x1000, 0x40000040);
            Section(1, ".data", 0x2000, 0x2000, 0xC0000040);
            Section(2, ".modules", 0x5000, 8, 0x40000040);
            Section(3, ".text", 0x6000, 0x1000, 0x60000020);
            W64(Image + 0x5000, Address(0x1000)); W32(Image + 0x1000, 0x00525452);
            W16(Image + 0x1004, 16); W16(Image + 0x100C, 4); Image[0x100E] = 24; Image[0x100F] = 1;
            Row(0, 201, 1, 0x1200, 0x1204); Row(1, 202, 1, 0x2100, 0x2108);
            Row(2, 204, 0, 0x2000, 0); Row(3, 213, 1, 0x1210, 0x1210);
            var observation = EfiLoadedImageProtocolObservation.CaptureFromUefiBoundary(
                0, 0x1000, 0x2000, (ulong)Image, 0x8000);
            Capture = EfiLoadedImageRangeCapturePlan.Create(&observation, 1, 1, 4096);
            Mapping = M01LoadedImageVirtualMappingEvidence.CaptureCallerAttestedIdentity(Capture);
            var module = TypeManagerModuleMetadataLiveNativeAOTImage.RegisterOnce(Capture, Mapping, Owner, 4096);
            Require(module.Succeeded, "original TypeManager registration " + module.Status);
            Handle = module.Handle;
        }

        internal ulong Address(int offset) => (ulong)(Image + offset);
        internal void Type(uint flags, uint size, long count)
        {
            W32((byte*)Mt, flags); W32((byte*)Mt + 4, size); W64((byte*)Mt + 8, 0);
            W64((byte*)Mt + 16, 0); W64((byte*)Mt - 8, unchecked((ulong)count));
        }
        internal void Series(int index, long adjusted, ulong offset)
        { byte* p = (byte*)Mt - 24 - index * 16; W64(p, unchecked((ulong)adjusted)); W64(p + 8, offset); }
        internal CollectorTypeMapResult Inspect(bool abbreviated = false,
            GCStaticReadableWindow window = default, ulong mt = 0, ulong handle = 0) =>
            Core.InspectType(NativeAotLayoutProfile.Ilc10_0_8WinX64LittleEndian,
                Capture, Mapping, Owner, 4096, handle == 0 ? Handle : handle,
                window.Bytes == 0 ? Window : window, mt == 0 ? Mt : mt, abbreviated);
        internal void Expect(Status expected)
        {
            byte[] before = Snapshot(); var result = Inspect();
            Require(result.Status == expected && !result.Succeeded &&
                result.MethodTable == 0 && result.SeriesCount == 0 && !result.TryGetSeries(0, out _),
                "expected " + expected + ", got " + result.Status);
            Require(Snapshot().AsSpan().SequenceEqual(before), "failure preserves complete input span");
        }
        internal byte[] Snapshot() => new ReadOnlySpan<byte>(storage, 0xD000).ToArray();
        internal void Guards()
        {
            foreach (int offset in new[] { 0, 0x9000, 0xA000, 0xC000 })
                for (int i = 0; i < 4096; i++) Require(storage[offset + i] == 0xA5, "outer canary");
        }
        private void Row(int index, uint id, uint flags, int start, int end)
        { byte* p = Image + 0x1010 + index * 24; W32(p, id); W32(p + 4, flags); W64(p + 8, Address(start)); W64(p + 16, end == 0 ? 0 : Address(end)); }
        private void Section(int index, string name, uint start, uint size, uint flags)
        { byte* p = Image + 0x188 + index * 40; for (int i = 0; i < name.Length; i++) p[i] = (byte)name[i]; W32(p + 8, size); W32(p + 12, start); W32(p + 36, flags); }
        public void Dispose() => NativeMemory.AlignedFree(storage);
    }

    private static void W16(byte* p, ushort v) { for (int i = 0; i < 2; i++) p[i] = (byte)(v >> (i * 8)); }
    private static void W32(byte* p, uint v) { for (int i = 0; i < 4; i++) p[i] = (byte)(v >> (i * 8)); }
    private static void W64(byte* p, ulong v) { for (int i = 0; i < 8; i++) p[i] = (byte)(v >> (i * 8)); }
}
