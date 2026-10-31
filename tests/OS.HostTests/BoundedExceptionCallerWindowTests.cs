using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Runtime;
using Core = OS.Kernel.Runtime.BoundedExceptionCallerWindow;
using Status = OS.Kernel.Runtime.BoundedExceptionCallerWindowStatus;

internal static unsafe class BoundedExceptionCallerWindowTests
{
    private const NativeAotLayoutProfile Profile = NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian;
    private static void Need(bool value, string reason) => HostCheck.True(value, reason);

    [HostTest("runtime.bounded-caller-window.physical-callers-protected-pc", HostLane.Fast, 0)]
    private static void PhysicalCallers()
    {
        using Fixture f = new();
        var frames = Seed<BoundedExceptionSearchFrame>(4);
        var rows = Seed<NativeAotEhClauseResult>(17);
        byte[][] before = f.InputBytes();
        var result = f.Build(3, frames, rows);
        Need(result.Succeeded && result.FrameCount == 3 && result.RowCount == 2, "three physical frames and two decoded rows");
        uint[] offsets = [0, 0, 1], counts = [0, 1, 1], roots = [0x6000, 0x6100, 0x6200];
        for (int i = 0; i < 3; i++)
        {
            var expected = f.Initial;
            expected.Rip = f.Address(roots[i] + 0x20);
            expected.Rsp += (ulong)i * 40;
            Need(Same(frames[i].Context, expected) && frames[i].ProtectedOffset == 0x1f &&
                frames[i].CodeBytes == 0x80 && frames[i].RowOffset == offsets[i] && frames[i].RowCount == counts[i],
                "intact return RIP, GP/XMM values, progressing RSP and contiguous rows " + i);
        }
        Need(rows[0].Succeeded && rows[0].Kind == NativeAotEhClauseKind.FaultOrFinally &&
            rows[0].HandlerAddress == f.Address(0x6140) && rows[1].Succeeded &&
            rows[1].Kind == NativeAotEhClauseKind.Typed && rows[1].HandlerAddress == f.Address(0x6240) &&
            rows[1].TypeAddress == f.Address(0x1800) && rows[0].ContainsOffset(31) && rows[1].ContainsOffset(31) &&
            !rows[0].ContainsOffset(32) && !rows[1].ContainsOffset(32), "protected call byte is inside the half-open try; return RIP is outside");
        Zero(rows.AsSpan(2, 14)); Tail(frames.AsSpan(), 3); Tail(rows.AsSpan(), 16);
        f.Unchanged(before);
    }

    [HostTest("runtime.bounded-caller-window.capacities-and-tails", HostLane.Fast, 1)]
    private static void CapacitiesAndTails()
    {
        using Fixture f = new();
        Need(!default(BoundedExceptionCallerWindowResult).Succeeded && default(BoundedExceptionCallerWindowResult).FrameCount == 0,
            "default result fails");
        foreach (uint count in new uint[] { 0, 5, uint.MaxValue })
        {
            var frames = Seed<BoundedExceptionSearchFrame>(5); var rows = Seed<NativeAotEhClauseResult>(17);
            Failed(f.Build(count, frames, rows), Status.CapacityExceeded);
            Tail(frames.AsSpan(), 0); Tail(rows.AsSpan(), 0);
        }
        var shortFrames = Seed<BoundedExceptionSearchFrame>(2); var untouchedRows = Seed<NativeAotEhClauseResult>(17);
        Failed(f.Build(3, shortFrames, untouchedRows), Status.OutputTooSmall);
        Tail(shortFrames.AsSpan(), 0); Tail(untouchedRows.AsSpan(), 0);
        foreach (int rowLength in new[] { 0, 1 })
        {
            var frames = Seed<BoundedExceptionSearchFrame>(4); var rows = Seed<NativeAotEhClauseResult>(rowLength);
            Cleared(f.Build(3, frames, rows), Status.OutputTooSmall, frames, rows, 3);
        }
        var maximumFrames = Seed<BoundedExceptionSearchFrame>(5); var maximumRows = Seed<NativeAotEhClauseResult>(17);
        var maximum = f.Build(4, maximumFrames, maximumRows);
        Need(maximum.Succeeded && maximum.FrameCount == 4 && maximum.RowCount == 2 &&
            maximumFrames[3].RowOffset == 2 && maximumFrames[3].RowCount == 0 &&
            maximumFrames[3].Context.Rip == f.Initial.Rip && maximumFrames[3].Context.Rsp == f.Initial.Rsp + 120,
            "four-frame capacity and repeated method in a distinct progressing frame");
        Tail(maximumFrames.AsSpan(), 4); Tail(maximumRows.AsSpan(), 16);
        var oneFrame = Seed<BoundedExceptionSearchFrame>(2); var noRows = Array.Empty<NativeAotEhClauseResult>();
        var one = f.Build(1, oneFrame, noRows);
        Need(one.Succeeded && one.FrameCount == 1 && one.RowCount == 0, "authenticated no-EH membership needs no row capacity");
        Tail(oneFrame.AsSpan(), 1);
    }

    [HostTest("runtime.bounded-caller-window.late-failure-clears-admitted-outputs", HostLane.Fast, 2)]
    private static void LateFailures()
    {
        using Fixture f = new();
        var frames = Seed<BoundedExceptionSearchFrame>(5); var rows = Seed<NativeAotEhClauseResult>(17);
        // Three complete frames precede this late out-of-window RSP.
        Cleared(f.Build(4, frames, rows, stackBytes: 0x178), Status.InvalidFrame, frames, rows, 4);
        Fill(frames); Fill(rows);
        W64(f.Stack + 0x170, f.Address(0x6f20)); // Executable return PC, but no runtime-function membership.
        Cleared(f.Build(4, frames, rows), Status.SnapshotRejected, frames, rows, 4);
        W64(f.Stack + 0x170, f.Initial.Rip);
        Fill(frames); Fill(rows);
        W64(f.Stack + 0x148, f.Address(0x1700));
        Cleared(f.Build(3, frames, rows), Status.UnwindRejected, frames, rows, 3);
        W64(f.Stack + 0x148, f.Address(0x6220));
        Fill(frames); Fill(rows);
        W64(f.Stack + 0x120, f.Address(0x6100));
        Cleared(f.Build(3, frames, rows), Status.SnapshotRejected, frames, rows, 3);
        W64(f.Stack + 0x120, f.Address(0x6120));
        Fill(frames); Fill(rows);
        W32(f.Image + 0x192c, 10); // Invalid final contribution, even while selecting the first no-EH frame.
        Cleared(f.Build(1, frames, rows), Status.CatalogRejected, frames, rows, 1);
        W32(f.Image + 0x192c, 9);
        Fill(frames); Fill(rows);
        f.Image[0x1704] = 0; // Last physical EH row loses its nonzero handler.
        Cleared(f.Build(1, frames, rows), Status.CatalogRejected, frames, rows, 1);
    }

    [HostTest("runtime.bounded-caller-window.original-authority", HostLane.Fast, 3)]
    private static void OriginalAuthority()
    {
        using Fixture f = new(); using Fixture other = new();
        var frames = Seed<BoundedExceptionSearchFrame>(4); var rows = Seed<NativeAotEhClauseResult>(17);
        Cleared(f.Build(3, frames, rows, handle: f.Handle + 8), Status.SnapshotRejected, frames, rows, 3);
        Fill(frames); Fill(rows);
        Cleared(f.Build(3, frames, rows, owner: other.Owner), Status.SnapshotRejected, frames, rows, 3);
        Fill(frames); Fill(rows);
        Cleared(f.Build(3, frames, rows, snapshot: other.Snapshot), Status.SnapshotRejected, frames, rows, 3);
        Fill(frames); Fill(rows);
        f.Owner[0] ^= 1;
        Cleared(f.Build(3, frames, rows), Status.SnapshotRejected, frames, rows, 3);
        f.Owner[0] ^= 1; Fill(frames); Fill(rows);
        f.Snapshot[0] ^= 1;
        Cleared(f.Build(3, frames, rows), Status.SnapshotRejected, frames, rows, 3);
        f.Snapshot[0] ^= 1; Fill(frames); Fill(rows);
        var foreignPc = f.Initial; foreignPc.Rip = other.Initial.Rip;
        var foreign = Core.Build(Profile, other.Capture, other.Mapping, f.Owner, Fixture.Capacity, f.Handle,
            f.Snapshot, Fixture.Capacity, 0x1900, 48, f.Stack, Fixture.StackBytes, foreignPc, 3, frames, rows);
        Cleared(foreign, Status.SnapshotRejected, frames, rows, 3);
        Fill(frames); Fill(rows);
        Cleared(f.Build(3, frames, rows, profile: default), Status.CatalogRejected, frames, rows, 3);
    }

    [HostTest("runtime.bounded-caller-window.overlapping-outputs-untouched", HostLane.Fast, 4)]
    private static void OverlappingOutputs()
    {
        using Fixture f = new();
        var rows = Seed<NativeAotEhClauseResult>(17);
        foreach (nint address in new nint[] { (nint)f.Owner, (nint)f.Snapshot, (nint)f.Stack })
        {
            var frames = new Span<BoundedExceptionSearchFrame>((void*)address, 3);
            byte[] before = MemoryMarshal.AsBytes(frames).ToArray();
            Failed(f.Build(3, frames, rows), Status.StorageOverlap);
            Need(MemoryMarshal.AsBytes(frames).SequenceEqual(before), "overlapping original storage was not cleared");
            Tail(rows.AsSpan(), 0);
        }
        byte* shared = (byte*)NativeMemory.Alloc(4096);
        try
        {
            Need(shared != null, "shared output fixture allocation");
            new Span<byte>(shared, 4096).Fill(0xa5);
            var frames = new Span<BoundedExceptionSearchFrame>(shared, 3);
            var overlappingRows = new Span<NativeAotEhClauseResult>(shared + 3 * sizeof(BoundedExceptionSearchFrame) - 8, 16);
            Failed(f.Build(3, frames, overlappingRows), Status.StorageOverlap);
            Need(new ReadOnlySpan<byte>(shared, 4096).IndexOfAnyExcept((byte)0xa5) == -1, "mutually overlapping outputs remain untouched");
        }
        finally { NativeMemory.Free(shared); }
    }

    [HostTest("runtime.bounded-caller-window.mutable-scratch-and-unlisted-eh", HostLane.Fast, 5)]
    private static void MutableScratchAndNoEh()
    {
        using Fixture f = new();
        var frames = new Span<BoundedExceptionSearchFrame>(f.Image + 0x2400, 4);
        var rows = new Span<NativeAotEhClauseResult>(f.Image + 0x3000, 17);
        Fill(frames); Fill(rows);
        var accepted = f.Build(3, frames, rows);
        Need(accepted.Succeeded && accepted.FrameCount == 3 && accepted.RowCount == 2, "distinct registered mutable image scratch admitted");
        Tail(frames, 3); Tail(rows, 16);
        foreach (uint rva in new uint[] { 0x1b00, 0x6800, 0x3f00 })
        {
            var unadmitted = new Span<BoundedExceptionSearchFrame>(f.Image + rva, 3);
            Fill(unadmitted); byte[] before = MemoryMarshal.AsBytes(unadmitted).ToArray();
            var separateRows = Seed<NativeAotEhClauseResult>(17);
            Failed(f.Build(3, unadmitted, separateRows), Status.StorageOverlap);
            Need(MemoryMarshal.AsBytes(unadmitted).SequenceEqual(before), "readonly, executable or partially mutable scratch untouched");
            Tail(separateRows.AsSpan(), 0);
        }
        using Fixture unlisted = new(unlistedEh: true);
        var actualEh = NativeAotEhClauseMetadata.ReadClause(Profile, unlisted.Capture, unlisted.Mapping,
            unlisted.Owner, Fixture.Capacity, unlisted.Handle, unlisted.Snapshot, Fixture.Capacity,
            new(0x6000, 0x80, 0x1a00, 1), 0);
        Need(actualEh.Status == NativeAotEhClauseStatus.IndexOutOfRange && actualEh.Count == 0,
            "real unlisted empty EH stream is distinct from NoEhInfo");
        var deniedFrames = Seed<BoundedExceptionSearchFrame>(2); var deniedRows = Seed<NativeAotEhClauseResult>(17);
        Cleared(unlisted.Build(1, deniedFrames, deniedRows), Status.CatalogRejected, deniedFrames, deniedRows, 1);
    }

    [HostTest("runtime.bounded-caller-window.zero-allocation-input-atomicity", HostLane.Fast, 6)]
    private static void AllocationAndAtomicity()
    {
        using Fixture f = new();
        var frames = Seed<BoundedExceptionSearchFrame>(4); var rows = Seed<NativeAotEhClauseResult>(17);
        byte[][] before = f.InputBytes();
        Need(!RuntimeHelpers.IsReferenceOrContainsReferences<BoundedExceptionSearchFrame>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAotEhClauseResult>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<BoundedExceptionCallerWindowResult>(), "POD outputs");
        for (int i = 0; i < 16; i++) _ = f.Build(3, frames, rows);
        long allocated = GC.GetAllocatedBytesForCurrentThread(); int successes = 0;
        for (int i = 0; i < 32; i++) if (f.Build(3, frames, rows).Succeeded) successes++;
        long after = GC.GetAllocatedBytesForCurrentThread();
        Need(successes == 32 && after == allocated, "warmed assembly allocates zero bytes");
        f.Unchanged(before); Tail(frames.AsSpan(), 3); Tail(rows.AsSpan(), 16);
    }

    private static T[] Seed<T>(int count) where T : unmanaged { var values = new T[count]; Fill(values); return values; }
    private static void Fill<T>(T[] values) where T : unmanaged => Fill(values.AsSpan());
    private static void Fill<T>(Span<T> values) where T : unmanaged => MemoryMarshal.AsBytes(values).Fill(0xa5);
    private static void Zero<T>(Span<T> values) where T : unmanaged =>
        Need(MemoryMarshal.AsBytes(values).IndexOfAnyExcept((byte)0) == -1, "admitted unused/failure output cleared");
    private static void Tail<T>(Span<T> values, int first) where T : unmanaged =>
        Need(MemoryMarshal.AsBytes(values.Slice(first)).IndexOfAnyExcept((byte)0xa5) == -1, "unowned output tail unchanged");
    private static bool Same(X64UnwindContext left, X64UnwindContext right) =>
        new ReadOnlySpan<byte>(&left, sizeof(X64UnwindContext)).SequenceEqual(new ReadOnlySpan<byte>(&right, sizeof(X64UnwindContext)));
    private static void Failed(BoundedExceptionCallerWindowResult result, Status status) =>
        Need(result.Status == status && !result.Succeeded && result.FrameCount == 0 && result.RowCount == 0,
            "zero rejected result: " + result.Status + " expected " + status);
    private static void Cleared(BoundedExceptionCallerWindowResult result, Status status,
        Span<BoundedExceptionSearchFrame> frames, Span<NativeAotEhClauseResult> rows, int count)
    {
        Failed(result, status); Zero(frames.Slice(0, count)); Tail(frames, count);
        int cleared = Math.Min(16, rows.Length); Zero(rows.Slice(0, cleared)); Tail(rows, cleared);
    }

    internal sealed class Fixture : IDisposable
    {
        internal const int ImageBytes = 0x8000;
        internal const ulong Capacity = 0x4000, StackBytes = 0x800;
        internal readonly byte* Image = (byte*)NativeMemory.AlignedAlloc(ImageBytes, 4096);
        private readonly byte* ownerBacking = (byte*)NativeMemory.AllocZeroed((nuint)Capacity + 16);
        private readonly byte* snapshotBacking = (byte*)NativeMemory.AllocZeroed((nuint)Capacity + 16);
        private readonly byte* stackBacking = (byte*)NativeMemory.AllocZeroed((nuint)StackBytes + 16);
        internal byte* Owner => ownerBacking + 8;
        internal byte* Snapshot => snapshotBacking + 8;
        internal byte* Stack => stackBacking + 8;
        internal readonly EfiLoadedImageRangeCapturePlan Capture;
        internal readonly M01LoadedImageVirtualMappingEvidence Mapping;
        internal readonly ulong Handle;
        internal readonly X64UnwindContext Initial;

        internal Fixture(bool unlistedEh = false)
        {
            Need(Image != null && ownerBacking != null && snapshotBacking != null && stackBacking != null, "fixture storage");
            NativeMemory.Clear(Image, ImageBytes);
            W64(ownerBacking, 0x123456789abcdef0); W64(Owner + Capacity, 0xfedcba9876543210);
            W64(snapshotBacking, 0x123456789abcdef0); W64(Snapshot + Capacity, 0xfedcba9876543210);
            W64(stackBacking, 0x123456789abcdef0); W64(Stack + StackBytes, 0xfedcba9876543210);
            // Independent mapped PE, ReadyToRun module and five original runtime-function rows.
            W16(Image, 0x5a4d); W32(Image + 60, 0x80); W32(Image + 0x80, 0x4550);
            W16(Image + 0x84, 0x8664); W16(Image + 0x86, 4); W16(Image + 0x94, 0xf0);
            W16(Image + 0x98, 0x20b); W32(Image + 0xd0, ImageBytes); W32(Image + 0xd4, 0x400);
            W32(Image + 0x104, 16); W32(Image + 0x120, 0x1100); W32(Image + 0x124, 60);
            Section(0, ".rdata", 0x1000, 0x1000, 0x40000040); Section(1, ".data", 0x2000, 0x2000, 0xc0000040);
            Section(2, ".modules", 0x5000, 16, 0x40000040); Section(3, ".text", 0x6000, 0x1000, 0x60000020);
            new Span<byte>(Image + 0x6000, 0x1000).Fill(0x90);
            W64(Image + 0x5000, Address(0x1000)); W32(Image + 0x1000, 0x00525452); W16(Image + 0x1004, 26); W16(Image + 0x1006, 1);
            W16(Image + 0x100c, 1); Image[0x100e] = 16; Image[0x100f] = 1;
            W32(Image + 0x1010, 204); W32(Image + 0x1014, 16); W64(Image + 0x1018, Address(0x2000));
            Function(0, 0x6000, 0x6080, 0x1200); Function(1, 0x6100, 0x6140, 0x1240);
            Function(2, 0x6140, 0x6180, 0x1280); Function(3, 0x6200, 0x6240, 0x12c0);
            Function(4, 0x6240, 0x6280, 0x1300);
            Info(0x1200, false, 0); Info(0x1240, true, 0x1600); Info(0x12c0, true, 0x1700);
            Image[0x1280] = Image[0x1300] = 1; Image[0x1284] = Image[0x1304] = 1;
            // NativeAOT compressed count/start/(length<<2|kind)/handler; typed RVA is fixed-width.
            new byte[] { 2, 0, 5, 2, 128 }.CopyTo(new Span<byte>(Image + 0x1600, 5));
            new byte[] { 2, 0, 1, 2, 128, 0, 0x18, 0, 0 }.CopyTo(new Span<byte>(Image + 0x1700, 9));
            W32(Image + 0x1900, 0x43454843); W32(Image + 0x1904, 1); W32(Image + 0x1908, 2); W32(Image + 0x190c, 48);
            CatalogRow(0, 0x6100, 0x1600, 5); CatalogRow(1, 0x6200, 0x1700, 9);
            if (unlistedEh) { Image[0x1206] = 4; W32(Image + 0x1207, 0x1a00); Image[0x1a00] = 0; }
            var observation = EfiLoadedImageProtocolObservation.CaptureFromUefiBoundary(0, 0x1000, 0x2000, (ulong)Image, ImageBytes);
            Capture = EfiLoadedImageRangeCapturePlan.Create(&observation, 1, 1, 4096);
            Mapping = M01LoadedImageVirtualMappingEvidence.CaptureCallerAttestedIdentity(Capture);
            var module = TypeManagerModuleMetadataLiveNativeAOTImage.RegisterOnce(Capture, Mapping, Owner, Capacity);
            Need(module.Succeeded, "real module registration " + module.Status); Handle = module.Handle;
            var snapshot = X64PERuntimeFunctionUnwindMetadataHostileInput.Bind(Capture, Mapping, Owner, Capacity, Handle, Snapshot, Capacity);
            Need(snapshot.Succeeded, "real original unwind snapshot " + snapshot.Status);
            X64UnwindContext initial = new() { Rip = Address(0x6020), Rsp = (ulong)Stack + 0x100 };
            foreach (byte register in new byte[] { 3, 5, 6, 7, 12, 13, 14, 15 }) initial.SetInteger(register, 0x1234567800000000UL + register);
            for (byte register = 6; register <= 15; register++)
                initial.SetVector(register, new() { Low = 0x1122334400000000UL + register, High = 0xaabbccdd00000000UL + register });
            Initial = initial;
            W64(Stack + 0x120, Address(0x6120)); W64(Stack + 0x148, Address(0x6220)); W64(Stack + 0x170, Initial.Rip);
        }

        internal ulong Address(uint rva) => (ulong)Image + rva;
        internal BoundedExceptionCallerWindowResult Build(uint count, Span<BoundedExceptionSearchFrame> frames,
            Span<NativeAotEhClauseResult> rows, ulong? handle = null, byte* owner = null, byte* snapshot = null,
            ulong stackBytes = StackBytes, NativeAotLayoutProfile profile = Profile) =>
            Core.Build(profile, Capture, Mapping, owner == null ? Owner : owner, Capacity, handle ?? Handle,
                snapshot == null ? Snapshot : snapshot, Capacity, 0x1900, 48, Stack, stackBytes, Initial, count, frames, rows);
        internal byte[][] InputBytes() => [new ReadOnlySpan<byte>(Image, ImageBytes).ToArray(),
            new ReadOnlySpan<byte>(ownerBacking, (int)Capacity + 16).ToArray(),
            new ReadOnlySpan<byte>(snapshotBacking, (int)Capacity + 16).ToArray(),
            new ReadOnlySpan<byte>(stackBacking, (int)StackBytes + 16).ToArray()];
        internal void Unchanged(byte[][] before) => Need(new ReadOnlySpan<byte>(Image, ImageBytes).SequenceEqual(before[0]) &&
            new ReadOnlySpan<byte>(ownerBacking, (int)Capacity + 16).SequenceEqual(before[1]) &&
            new ReadOnlySpan<byte>(snapshotBacking, (int)Capacity + 16).SequenceEqual(before[2]) &&
            new ReadOnlySpan<byte>(stackBacking, (int)StackBytes + 16).SequenceEqual(before[3]), "original inputs and canaries unchanged");
        private void Function(int index, uint begin, uint end, uint unwind)
        { byte* row = Image + 0x1100 + index * 12; W32(row, begin); W32(row + 4, end); W32(row + 8, unwind); }
        private void Info(uint rva, bool eh, uint table)
        {
            Image[rva] = eh ? (byte)9 : (byte)1; Image[rva + 1] = 4; Image[rva + 2] = 1;
            Image[rva + 4] = 4; Image[rva + 5] = 0x32; // UWOP_ALLOC_SMALL: 32 bytes.
            if (eh) { W32(Image + rva + 8, 0x6f00); Image[rva + 12] = 4; W32(Image + rva + 13, table); }
        }
        private void CatalogRow(int index, uint root, uint table, uint bytes)
        { byte* row = Image + 0x1910 + index * 16; W32(row, root); W32(row + 4, 0x80); W32(row + 8, table); W32(row + 12, bytes); }
        private void Section(int index, string name, uint start, uint bytes, uint flags)
        { byte* row = Image + 0x188 + index * 40; for (int i = 0; i < name.Length; i++) row[i] = (byte)name[i]; W32(row + 8, bytes); W32(row + 12, start); W32(row + 36, flags); }
        public void Dispose()
        { NativeMemory.AlignedFree(Image); NativeMemory.Free(ownerBacking); NativeMemory.Free(snapshotBacking); NativeMemory.Free(stackBacking); }
    }

    private static void W16(byte* pointer, ushort value) { pointer[0] = (byte)value; pointer[1] = (byte)(value >> 8); }
    private static void W32(byte* pointer, uint value) { for (int i = 0; i < 4; i++) pointer[i] = (byte)(value >> (i * 8)); }
    private static void W64(byte* pointer, ulong value) { for (int i = 0; i < 8; i++) pointer[i] = (byte)(value >> (i * 8)); }
}
