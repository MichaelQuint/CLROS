using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Runtime;
using Core = OS.Kernel.Runtime.NativeAotEhClauseMetadata;
using Status = OS.Kernel.Runtime.NativeAotEhClauseStatus;
using Unwind = OS.Kernel.Runtime.X64PERuntimeFunctionUnwindMetadataHostileInput;

internal static unsafe class NativeAotEhClauseMetadataTests
{

    private static void Check(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
    private static void Failure(NativeAotEhClauseResult r, Status expected, string reason) => Check(
        r.Status == expected && !r.Succeeded && r.Index == 0 && r.Count == 0 && r.TryStartOffset == 0 &&
        r.TryEndOffset == 0 && r.HandlerAddress == 0 && r.FilterAddress == 0 && r.TypeAddress == 0 &&
        !r.IsTryGroupBoundary && !r.ContainsOffset(0), reason + ": " + r.Status + " expected " + expected);

    [HostTest("runtime.nativeaot-eh-clause.wire-discovery", HostLane.Fast, 0)]
    private static void WireDiscovery()
    {
        foreach (bool associated in new[] { false, true })
        {
            using Fixture f = new(associated);
            f.Blob(Table(new Row(4, 20, 0, 0x100, 0x1700), new Row(8, 16, 1, 0x100), new Row(12, 24, 2, 0x100, 0x200)));
            f.Bind();
            var typed = f.Read(0); var fault = f.Read(1); var filter = f.Read(2);
            Check(typed.Succeeded && typed.Count == 3 && typed.Kind == NativeAotEhClauseKind.Typed &&
                typed.TryStartOffset == 4 && typed.TryEndOffset == 24 && typed.HandlerAddress == f.Address(0x6100) &&
                typed.TypeAddress == f.Address(0x1700) && typed.FilterAddress == 0, "fixed type RVA and root-relative handler");
            Check(fault.Succeeded && fault.Kind == NativeAotEhClauseKind.FaultOrFinally && fault.TypeAddress == 0 &&
                fault.HandlerAddress == f.Address(0x6100), "fault/finally wire kind");
            Check(filter.Succeeded && filter.Kind == NativeAotEhClauseKind.Filter && filter.FilterAddress == f.Address(0x6200), "filter entry");
            Failure(f.Read(3), Status.IndexOutOfRange, "complete valid table index");
        }
        // Without Windows handler flags, one unwind code is NOT rounded up before the NativeAOT byte.
        using (Fixture odd = new())
        {
            new Span<byte>(odd.Image + 0x1200, 32).Clear();
            odd.Image[0x1200] = 1; odd.Image[0x1201] = 1; odd.Image[0x1202] = 1;
            odd.Image[0x1204] = 1; odd.Image[0x1205] = 0x30; odd.Image[0x1206] = 4; W32(odd.Image + 0x1207, 0x1600);
            odd.Bind(); Check(odd.Read().Succeeded, "unrounded odd-count trailer discovery");
        }
        using (Fixture none = new(true))
        {
            none.Image[0x1208] = 16; none.Bounds = new(0x6000, 0x400, 0, 0); none.Bind();
            Failure(none.Read(), Status.NoEhInfo, "associated data without EH");
            none.Bounds = new(0x6000, 0x400, 0x1600, 1);
            Failure(none.Read(), Status.BoundsRejected, "no EH cannot admit invented blob");
        }
        using Fixture empty = new(); empty.Blob([0]); empty.Bind();
        Failure(empty.Read(), Status.IndexOutOfRange, "empty complete table");
    }

    [HostTest("runtime.nativeaot-eh-clause.compression-truncation", HostLane.Fast, 1)]
    private static void CompressionTruncation()
    {
        // Deliberately nonminimal widths are legal to the pinned runtime reader; all values remain bounded.
        for (int width = 1; width <= 5; width++)
        {
            using Fixture f = new();
            byte[] blob = Table(width, new Row(4, 20, 0, 0x100, 0x1700), new Row(8, 16, 2, 0x100, 0x200));
            f.Blob(blob); f.Bind();
            Check(f.Read(0).Succeeded && f.Read(1).FilterAddress == f.Address(0x6200), "compressed width " + width);
            for (int bytes = 1; bytes < blob.Length; bytes++)
            {
                f.Bounds = new(0x6000, 0x400, 0x1600, (uint)bytes);
                Failure(f.Read(), Status.MetadataRejected, "truncated field width " + width + " bytes " + bytes);
            }
        }
        // High value bits in each width must be decoded, rather than silently truncated to a small valid start.
        foreach (uint start in new uint[] { 0x3FFF, 0x1FFFFF, 0xFFFFFFF, 0xFFFFFFFF })
        {
            using Fixture f = new(); f.Blob(Table(new Row(start, 1, 1, 0x100))); f.Bind();
            Failure(f.Read(), Status.BoundsRejected, "wide unsigned start including overflow");
        }
        using Fixture prefix = new(); byte[] permissive = Table(5, new Row(4, 20, 1, 0x100));
        permissive[0] = 0xFF; prefix.Blob(permissive); prefix.Bind();
        Check(prefix.Read().Succeeded, "five-byte runtime prefix uses low four bits");
    }

    [HostTest("runtime.nativeaot-eh-clause.order-containment", HostLane.Fast, 2)]
    private static void OrderContainment()
    {
        using Fixture f = new();
        f.Blob(Table(new Row(4, 40, 0, 0x100, 0x1700), new Row(8, 8, 1, 0x100),
            new Row(4, 40, 2, 0x100, 0x200), new Row(0, 0, 1, 0),
            new Row(4, 40, 1, 0x100), new Row(7, 0, 1, 0x100)));
        f.Bind();
        for (uint i = 0; i < 6; i++) Check(f.Read(i).Succeeded && f.Read(i).Index == i && f.Read(i).Count == 6, "physical index preserved");
        var first = f.Read(0); Check(!first.ContainsOffset(3) && first.ContainsOffset(4) && first.ContainsOffset(43) && !first.ContainsOffset(44), "end-exclusive containment");
        Check(f.Read(1).TryStartOffset == 8 && f.Read(2).Kind == NativeAotEhClauseKind.Filter && f.Read(4).TryStartOffset == 4, "nested/equal rows unsorted and undeduplicated");
        var marker = f.Read(3);
        Check(marker.IsTryGroupBoundary && marker.HandlerAddress == 0 && !marker.ContainsOffset(0), "empty fault boundary grants no callable target");
        var empty = f.Read(5);
        Check(!empty.IsTryGroupBoundary && empty.HandlerAddress == f.Address(0x6100) && !empty.ContainsOffset(7), "other empty nonmatching try remains legal");
    }

    [HostTest("runtime.nativeaot-eh-clause.original-authority", HostLane.Fast, 3)]
    private static void OriginalAuthority()
    {
        using Fixture f = new(); f.Bind();
        Failure(Core.ReadClause((NativeAotLayoutProfile)255, default, default, null, 0, 0, null, 0, default, 0), Status.UnsupportedProfile, "profile precedes pointers");
        f.Owner[0] ^= 1; f.Bounds = new(0x6000, 0x400, 0xFFFFFFFF, 0xFFFFFFFF);
        Failure(f.Read(), Status.ModuleRejected, "stale original owner precedes hostile payload bounds"); f.Owner[0] ^= 1;
        f.Bounds = new(0x6000, 0x400, 0x1600, 1); f.Snapshot[Unwind.StateBytes] ^= 1;
        Failure(f.Read(), Status.SnapshotRejected, "stale snapshot precedes payload decode"); f.Snapshot[Unwind.StateBytes] ^= 1;
        using Fixture other = new(); other.Bind();
        Failure(Core.ReadClause(NativeAotLayoutProfile.Ilc10_0_12WinX64LittleEndian, f.Capture, f.Mapping, f.Owner,
            Fixture.Capacity, f.Handle, other.Snapshot, Fixture.Capacity, f.Bounds, 0), Status.SnapshotRejected, "different valid original image snapshot");
        new ReadOnlySpan<byte>(f.Snapshot, (int)Fixture.Capacity).CopyTo(new Span<byte>(other.Snapshot, (int)Fixture.Capacity));
        Failure(Core.ReadClause(NativeAotLayoutProfile.Ilc10_0_12WinX64LittleEndian, f.Capture, f.Mapping, f.Owner,
            Fixture.Capacity, f.Handle, other.Snapshot, Fixture.Capacity, f.Bounds, 0), Status.SnapshotRejected, "copied snapshot not original");
        f.Image[0x1201] = 1;
        Failure(f.Read(), Status.SnapshotRejected, "changed original unwind prefix"); f.Image[0x1201] = 0;
        W64(f.Image + 0x2000, other.Address(0x2000));
        Failure(f.Read(), Status.ModuleRejected, "changed published module identity");
    }

    [HostTest("runtime.nativeaot-eh-clause.bounds-targets", HostLane.Fast, 4)]
    private static void BoundsTargets()
    {
        // A legal try and funclet lie beyond the root row's 0x80 bytes, inside the exact whole method.
        using (Fixture valid = new())
        {
            valid.Blob(Table(new Row(0x180, 0x80, 1, 0x100))); valid.Bind();
            Check(valid.Read().Succeeded && valid.Read().TryEndOffset == 0x200, "complete contribution includes funclets");
            var saved = valid.Bounds;
            foreach (var bounds in new NativeAotEhMethodBounds[] {
                new(0x6001, 0x3FF, saved.EhRva, saved.EhBytes), new(0x6000, 0x20, saved.EhRva, saved.EhBytes),
                new(0x6000, 0x80, saved.EhRva, saved.EhBytes), new(0x6000, 0x2000, saved.EhRva, saved.EhBytes),
                new(0x6000, 0xFFFFFFFF, saved.EhRva, saved.EhBytes), new(0xFFFFFFFF, 2, saved.EhRva, saved.EhBytes),
                new(0x6000, 0x400, 0x1601, saved.EhBytes), new(0x6000, 0x400, 0x1600, 0) })
            { valid.Bounds = bounds; Failure(valid.Read(), Status.BoundsRejected, "exact contribution bounds"); }
            valid.Bounds = new(0x6000, 0x400, 0x1600, Core.MaximumEhBytes + 1);
            Failure(valid.Read(), Status.CapacityExceeded, "EH byte cap");
            valid.Bounds = new(0x6000, 0x400, 0x1600, saved.EhBytes + 1);
            Failure(valid.Read(), Status.MetadataRejected, "padding is not payload");
        }
        (Row Row, Status Expected)[] rows = [
            (new(0, 1, 3, 0x100), Status.UnsupportedFormat), (new(0xFFFFFFFF, 1, 1, 0x100), Status.BoundsRejected),
            (new(0, 1, 1, 0x400), Status.TargetRejected), (new(0, 1, 1, 0xFFFFFFFF), Status.TargetRejected),
            (new(0, 1, 1, 0x101), Status.TargetRejected), (new(0, 1, 1, 0x90), Status.SnapshotRejected),
            (new(0, 1, 1, 0), Status.UnsupportedFormat), (new(0, 1, 1, 0x200), Status.UnsupportedFormat),
            (new(0, 1, 2, 0x100, 0x100), Status.UnsupportedFormat), (new(0, 1, 2, 0x100, 0x201), Status.TargetRejected),
            (new(0, 1, 0, 0x100, 0), Status.TargetRejected), (new(0, 1, 0, 0x100, 0x1701), Status.TargetRejected),
            (new(0, 1, 0, 0x100, 0x1FF0), Status.TargetRejected), (new(0, 1, 0, 0x100, 0x2000), Status.TargetRejected),
            (new(0, 1, 0, 0x100, 0x6000), Status.TargetRejected), (new(0, 1, 0, 0x100, 0xFFFFFFF8), Status.TargetRejected) ];
        foreach (var item in rows) { using Fixture f = new(); f.Blob(Table(item.Row)); f.Bind(); Failure(f.Read(), item.Expected, "hostile row"); }
        using (Fixture intervening = new())
        {
            // These numeric extents still contain the target, but the pinned manager finds
            // root0x6100 before root0x6000. The caller's claimed contribution cannot override it.
            intervening.Image[0x1244] = 0; intervening.Image[0x1284] = 1;
            intervening.Blob(Table(new Row(0, 0, 1, 0), new Row(0, 4, 1, 0x200))); intervening.Bind();
            Failure(intervening.Read(0), Status.TargetRejected, "late intervening root hides even an earlier boundary row");
            Failure(intervening.Read(1), Status.TargetRejected, "listed target belongs to another nearest root");
        }
        foreach (byte flags in new byte[] { 1, 2, 3, 12, 32, 0x84 })
        { using Fixture f = new(); f.Image[0x1208] = flags; f.Bind(); Failure(f.Read(), Status.UnsupportedFormat, "unsupported root flags"); }
        using (Fixture f = new())
        {
            f.Image[0x1244] = 5; f.Bind(); Failure(f.Read(), Status.UnsupportedFormat, "funclet cannot own root EH trailer");
        }
        using (Fixture f = new())
        {
            f.Blob([0x82]); f.Bind(); Failure(f.Read(), Status.CapacityExceeded, "record cap precedes rows");
        }
        foreach (uint rva in new uint[] { 0, 0x2000, 0x6000, 0x80000000, 0xFFFFFFF0 })
        {
            using Fixture f = new(); W32(f.Image + 0x1209, rva); f.Bounds = new(0x6000, 0x400, rva, 8); f.Bind();
            Failure(f.Read(), rva == 0 || rva > 0x7FFFFFFF ? Status.BoundsRejected : Status.MetadataRejected, "EH pointer permissions");
        }
        using (Fixture chain = new())
        {
            new Span<byte>(chain.Image + 0x1200, 32).Clear(); chain.Image[0x1200] = 33;
            W32(chain.Image + 0x1204, 0x6100); W32(chain.Image + 0x1208, 0x6180); W32(chain.Image + 0x120C, 0x1240);
            chain.Bind(); Failure(chain.Read(), Status.UnsupportedFormat, "listed chain explicitly unsupported");
        }
    }

    [HostTest("runtime.nativeaot-eh-clause.atomicity-allocation", HostLane.Fast, 5)]
    private static void AtomicityAllocation()
    {
        using Fixture f = new(); f.Blob(Table(new Row(0, 4, 1, 0x100), new Row(4, 8, 0, 0x100, 0x1701))); f.Bind();
        byte[] image = Bytes(f.Image, Fixture.ImageBytes), owner = Bytes(f.Owner, (int)Fixture.Capacity), snapshot = Bytes(f.Snapshot, (int)Fixture.Capacity);
        Failure(f.Read(0), Status.TargetRejected, "late invalid target hides earlier row");
        Failure(f.Read(20), Status.TargetRejected, "invalid index cannot bypass late malformed target");
        Check(Bytes(f.Image, Fixture.ImageBytes).AsSpan().SequenceEqual(image) && Bytes(f.Owner, (int)Fixture.Capacity).AsSpan().SequenceEqual(owner) &&
            Bytes(f.Snapshot, (int)Fixture.Capacity).AsSpan().SequenceEqual(snapshot), "all caller inputs unchanged on late failure");
        // EH payload is intentionally not in the earlier unwind-prefix snapshot: stateless calls validate current stable bytes.
        f.Blob(Table(new Row(0, 4, 1, 0x100), new Row(4, 8, 0, 0x100, 0x1700)));
        for (int i = 0; i < 16; i++) { _ = f.Read(0); _ = f.Read(1); }
        long before = GC.GetAllocatedBytesForCurrentThread(); bool good = true;
        for (int i = 0; i < 64; i++) good &= f.Read((uint)(i & 1)).Succeeded;
        long after = GC.GetAllocatedBytesForCurrentThread(); Check(good && before == after, "warmed zero allocations");
        Check(Bytes(f.Owner, (int)Fixture.Capacity).AsSpan().SequenceEqual(owner) && Bytes(f.Snapshot, (int)Fixture.Capacity).AsSpan().SequenceEqual(snapshot), "no hidden state/cache mutation");
        f.Guards();
    }

    private readonly record struct Row(uint Start, uint Length, uint Kind, uint Handler, uint Extra = 0);
    private static byte[] Table(params Row[] rows) => Table(0, rows);
    private static byte[] Table(int width, params Row[] rows)
    {
        List<byte> bytes = new(); Add(bytes, (uint)rows.Length, width);
        foreach (Row row in rows)
        {
            Add(bytes, row.Start, width); Add(bytes, row.Length << 2 | row.Kind, width); Add(bytes, row.Handler, width);
            if (row.Kind == 0) { for (int i = 0; i < 4; i++) bytes.Add((byte)(row.Extra >> (8 * i))); }
            if (row.Kind == 2) Add(bytes, row.Extra, width);
        }
        return bytes.ToArray();
    }
    private static void Add(List<byte> bytes, uint value, int width)
    {
        int minimum = value < 128 ? 1 : value < 16384 ? 2 : value < 2097152 ? 3 : value < 268435456 ? 4 : 5;
        width = Math.Max(width, minimum);
        if (width == 5) { bytes.Add(15); for (int i = 0; i < 4; i++) bytes.Add((byte)(value >> (8 * i))); return; }
        uint prefix = (1U << (width - 1)) - 1;
        bytes.Add((byte)(value << width | prefix)); value >>= 8 - width;
        for (int i = 1; i < width; i++) { bytes.Add((byte)value); value >>= 8; }
    }

    private sealed class Fixture : IDisposable
    {
        internal const int ImageBytes = 0x8000;
        internal const ulong Capacity = 0x4000;
        internal readonly byte* Image;
        private readonly byte* ownerAllocation;
        private readonly byte* snapshotAllocation;
        internal byte* Owner => ownerAllocation + 8;
        internal byte* Snapshot => snapshotAllocation + 8;
        internal EfiLoadedImageRangeCapturePlan Capture { get; }
        internal M01LoadedImageVirtualMappingEvidence Mapping { get; }
        internal ulong Handle;
        internal NativeAotEhMethodBounds Bounds;
        internal Fixture(bool associated = false)
        {
            Image = (byte*)NativeMemory.AlignedAlloc(ImageBytes, 4096);
            ownerAllocation = (byte*)NativeMemory.AllocZeroed((nuint)(Capacity + 16));
            snapshotAllocation = (byte*)NativeMemory.AllocZeroed((nuint)(Capacity + 16));
            if (Image == null || ownerAllocation == null || snapshotAllocation == null) throw new OutOfMemoryException();
            NativeMemory.Clear(Image, ImageBytes);
            *(ulong*)ownerAllocation = *(ulong*)snapshotAllocation = 0x0123456789ABCDEF;
            *(ulong*)(Owner + Capacity) = *(ulong*)(Snapshot + Capacity) = 0xFEDCBA9876543210;
            W16(Image, 0x5A4D); W32(Image + 60, 0x80); W32(Image + 0x80, 0x4550);
            W16(Image + 0x84, 0x8664); W16(Image + 0x86, 4); W16(Image + 0x94, 0xF0);
            W16(Image + 0x98, 0x20B); W32(Image + 0xD0, ImageBytes); W32(Image + 0xD4, 0x400);
            W32(Image + 0x104, 16); W32(Image + 0x120, 0x1100); W32(Image + 0x124, 36);
            Section(0, ".rdata", 0x1000, 0x1000, 0x40000040); Section(1, ".data", 0x2000, 0x2000, 0xC0000040);
            Section(2, ".modules", 0x5000, 16, 0x40000040); Section(3, ".text", 0x6000, 0x1000, 0x60000020);
            W64(Image + 0x5000, Address(0x1000)); W32(Image + 0x1000, 0x00525452); W16(Image + 0x1004, 16);
            W16(Image + 0x100C, 1); Image[0x100E] = 24; Image[0x100F] = 1;
            W32(Image + 0x1010, 204); W64(Image + 0x1018, Address(0x2000));
            Function(0, 0x6000, 0x1200); Function(1, 0x6100, 0x1240); Function(2, 0x6200, 0x1280);
            Image[0x1200] = 9; W32(Image + 0x1204, 0x6500); Image[0x1208] = (byte)(associated ? 20 : 4);
            if (associated) W32(Image + 0x1209, 0x1800);
            W32(Image + (associated ? 0x120D : 0x1209), 0x1600);
            Image[0x1240] = Image[0x1280] = 1; Image[0x1244] = 1; Image[0x1284] = 2;
            Blob(Table(new Row(0, 4, 1, 0x100)));
            var observation = EfiLoadedImageProtocolObservation.CaptureFromUefiBoundary(0, 0x1000, 0x2000, (ulong)Image, ImageBytes);
            Capture = EfiLoadedImageRangeCapturePlan.Create(&observation, 1, 1, 4096);
            Mapping = M01LoadedImageVirtualMappingEvidence.CaptureCallerAttestedIdentity(Capture);
        }
        internal ulong Address(uint rva) => (ulong)Image + rva;
        internal void Blob(byte[] bytes) { new Span<byte>(Image + 0x1600, 0x100).Clear(); bytes.CopyTo(new Span<byte>(Image + 0x1600, bytes.Length)); Bounds = new(0x6000, 0x400, 0x1600, (uint)bytes.Length); }
        internal void Bind()
        {
            var module = TypeManagerModuleMetadataLiveNativeAOTImage.RegisterOnce(Capture, Mapping, Owner, Capacity);
            Check(module.Succeeded, "module registration " + module.Status); Handle = module.Handle;
            var snapshot = Unwind.Bind(Capture, Mapping, Owner, Capacity, Handle, Snapshot, Capacity);
            Check(snapshot.Succeeded, "unwind snapshot " + snapshot.Status);
        }
        internal NativeAotEhClauseResult Read(uint index = 0) => Core.ReadClause(NativeAotLayoutProfile.Ilc10_0_12WinX64LittleEndian,
            Capture, Mapping, Owner, Capacity, Handle, Snapshot, Capacity, Bounds, index);
        private void Function(uint index, uint begin, uint unwind)
        {
            byte* p = Image + 0x1100 + index * 12; W32(p, begin); W32(p + 4, begin + 0x80); W32(p + 8, unwind);
        }
        private void Section(int i, string name, uint start, uint size, uint flags)
        {
            byte* p = Image + 0x188 + i * 40; for (int n = 0; n < name.Length; n++) p[n] = (byte)name[n];
            W32(p + 8, size); W32(p + 12, start); W32(p + 36, flags);
        }
        internal void Guards() => Check(*(ulong*)ownerAllocation == 0x0123456789ABCDEF && *(ulong*)snapshotAllocation == 0x0123456789ABCDEF &&
            *(ulong*)(Owner + Capacity) == 0xFEDCBA9876543210 && *(ulong*)(Snapshot + Capacity) == 0xFEDCBA9876543210, "caller canaries");
        public void Dispose() { NativeMemory.AlignedFree(Image); NativeMemory.Free(ownerAllocation); NativeMemory.Free(snapshotAllocation); }
    }
    private static byte[] Bytes(byte* p, int bytes) => new ReadOnlySpan<byte>(p, bytes).ToArray();
    private static void W16(byte* p, ushort v) { p[0] = (byte)v; p[1] = (byte)(v >> 8); }
    private static void W32(byte* p, uint v) { W16(p, (ushort)v); W16(p + 2, (ushort)(v >> 16)); }
    private static void W64(byte* p, ulong v) { W32(p, (uint)v); W32(p + 4, (uint)(v >> 32)); }
}
