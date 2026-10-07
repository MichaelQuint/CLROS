using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Runtime;
using Core = OS.Kernel.Runtime.X64PERuntimeFunctionUnwindMetadataHostileInput;
using Status = OS.Kernel.Runtime.X64UnwindMetadataStatus;

internal static unsafe class X64PERuntimeFunctionUnwindMetadataHostileInputTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;
        Run("m04.020 per-image lookup preserves original module and half-open ranges", TwoImages, ref failures);
        Run("m04.020 directory and function bounds reject before snapshot publication", DirectoryAndFunctions, ref failures);
        Run("m04.020 version-one opcode operand and frame forms decode", Opcodes, ref failures);
        Run("m04.020 truncated reserved and inconsistent unwind codes reject", HostileCodes, ref failures);
        Run("m04.020 handler prefix requires same-image executable target", Handlers, ref failures);
        Run("m04.020 chained identities frames cycles and bounded depth are enforced", Chains, ref failures);
        Run("m04.020 full snapshot capacity and original owner aliases reject", Storage, ref failures);
        Run("m04.020 valid replacement copied state and stale registration cannot authorize lookup", RetainedIdentity, ref failures);
        Run("m04.020 metadata validation and lookup allocate no managed storage", Allocation, ref failures);
        if (failures != 0) throw new InvalidOperationException("M04-020 host failures=" + failures);
        Console.WriteLine("CLROS_M04_020_READY hostOnly=true guestIntegrated=false unwindExecution=false");
    }

    private static void Run(string name, Action test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);
        try { test(); Console.WriteLine("TEST_PASS " + name); }
        catch (Exception e) { failures++; Console.WriteLine("TEST_FAIL " + name + " " + e); }
    }
    private static void Check(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
    private static void Failure(X64UnwindMetadataResult result, Status expected, string reason)
    {
        Check(result.Status == expected && !result.Succeeded && result.RequiredStorage == 0 && result.ImageBase == 0 &&
            result.ModuleOwner == 0 && result.ModuleHandle == 0 && result.FunctionCount == 0 && result.FunctionIndex == 0 &&
            result.BeginRva == 0 && result.EndRva == 0 && result.UnwindRva == 0 && result.PrimaryUnwindRva == 0 &&
            result.HandlerRva == 0 && result.LanguageDataStartRva == 0 && result.FixedAllocationBytes == 0 &&
            result.StackBytes == 0 && result.ChainDepth == 0 && result.PrologBytes == 0 && result.CodeSlots == 0 &&
            result.FrameRegister == 0 && result.FrameOffset == 0 && result.Flags == 0 && result.PrimaryFlags == 0,
            reason + " actual=" + result.Status);
    }

    private static void SameResult(X64UnwindMetadataResult actual, X64UnwindMetadataResult expected, string reason) =>
        Check(actual.Status == expected.Status && actual.RequiredStorage == expected.RequiredStorage &&
            actual.ImageBase == expected.ImageBase && actual.ModuleOwner == expected.ModuleOwner && actual.ModuleHandle == expected.ModuleHandle &&
            actual.FunctionCount == expected.FunctionCount && actual.FunctionIndex == expected.FunctionIndex &&
            actual.BeginRva == expected.BeginRva && actual.EndRva == expected.EndRva && actual.UnwindRva == expected.UnwindRva &&
            actual.PrimaryUnwindRva == expected.PrimaryUnwindRva && actual.HandlerRva == expected.HandlerRva &&
            actual.LanguageDataStartRva == expected.LanguageDataStartRva && actual.FixedAllocationBytes == expected.FixedAllocationBytes &&
            actual.StackBytes == expected.StackBytes && actual.ChainDepth == expected.ChainDepth && actual.PrologBytes == expected.PrologBytes &&
            actual.CodeSlots == expected.CodeSlots && actual.FrameRegister == expected.FrameRegister && actual.FrameOffset == expected.FrameOffset &&
            actual.Flags == expected.Flags && actual.PrimaryFlags == expected.PrimaryFlags, reason);

    private static void TwoImages()
    {
        using Fixture a = new(), b = new();
        Check(!RuntimeHelpers.IsReferenceOrContainsReferences<X64UnwindMetadataResult>() && !default(X64UnwindMetadataResult).Succeeded, "POD/default");
        Check(a.Bind().Succeeded && b.Bind().Succeeded, "two originals");
        var x = a.Lookup(0x6000); var y = b.Lookup(0x6000);
        Check(x.Succeeded && y.Succeeded && x.ImageBase != y.ImageBase && x.ModuleOwner != y.ModuleOwner &&
            x.ModuleHandle != y.ModuleHandle && x.ImageBase == (ulong)a.Image && y.ImageBase == (ulong)b.Image &&
            x.BeginRva == y.BeginRva && x.EndRva == 0x6080 && x.FunctionIndex == 0 && x.FunctionCount == 2 &&
            x.UnwindRva == 0x1200 && x.PrimaryUnwindRva == 0x1200 && x.FixedAllocationBytes == 32 && x.StackBytes == 40, "owning bases, data");
        Check(a.Lookup(0x607F).Succeeded && a.Lookup(0x6100).Succeeded, "range interiors");
        byte[] image = Bytes(a.Image, Fixture.ImageBytes), owner = Bytes(a.Owner, (int)Fixture.Capacity), snapshot = Bytes(a.Snapshot, (int)Fixture.Capacity);
        SameResult(a.LookupFunction(0), x, "first index matches active PC");
        SameResult(b.LookupFunction(0), y, "indexed lookup retains the other original image");
        SameResult(a.LookupFunction(1), a.Lookup(0x6100), "last index matches its beginning");
        SameResult(a.LookupFunction(1), a.Lookup(0x617F), "last index matches its final included PC");
        foreach (uint index in new uint[] { x.FunctionCount, uint.MaxValue })
            Failure(a.LookupFunction(index), Status.NotFound, "index count/uint maximum");
        Check(Bytes(a.Image, Fixture.ImageBytes).AsSpan().SequenceEqual(image) &&
            Bytes(a.Owner, (int)Fixture.Capacity).AsSpan().SequenceEqual(owner) &&
            Bytes(a.Snapshot, (int)Fixture.Capacity).AsSpan().SequenceEqual(snapshot), "indexed lookup preserves all backing bytes");
        foreach (uint pc in new uint[] { 0, 0x6080, 0x60FF, 0x6180, Fixture.ImageBytes })
            Failure(a.Lookup(pc), Status.NotFound, "exclusive end/gap/outside");
        Failure(Core.Lookup(a.Snapshot, Fixture.Capacity, b.Address(0x6000)), Status.NotFound, "foreign absolute PC");
        Failure(Core.Validate(a.Capture, b.Mapping, a.Owner, Fixture.Capacity, a.Handle), Status.ModuleRejected, "foreign mapping");
        Failure(Core.Validate(a.Capture, a.Mapping, b.Owner, Fixture.Capacity, b.Handle), Status.ModuleRejected, "foreign module");
        a.Guards(); b.Guards();
    }

    private static void DirectoryAndFunctions()
    {
        (Action<Fixture>, Status)[] cases = [
            (f => W32(f.Image + 0x104, 3), Status.DirectoryAbsent),
            (f => W32(f.Image + 0x104, 0xFFFFFFFF), Status.DirectoryRejected),
            (f => { W32(f.Image + 0x120, 0); W32(f.Image + 0x124, 0); }, Status.DirectoryAbsent),
            (f => W32(f.Image + 0x120, 0x1101), Status.DirectoryRejected),
            (f => W32(f.Image + 0x124, 23), Status.DirectoryRejected),
            (f => W32(f.Image + 0x120, 0xFFFFFFFC), Status.DirectoryRejected),
            (f => f.Row(0, 0x6000, 0x6000, 0x1200), Status.FunctionRejected),
            (f => f.Row(0, 0x6000, 0xFFFFFFFF, 0x1200), Status.FunctionRejected),
            (f => f.Row(0, 0x1000, 0x1080, 0x1200), Status.FunctionRejected),
            (f => f.Row(1, 0x6000, 0x6080, 0x1240), Status.FunctionRejected),
            (f => f.Row(1, 0x6070, 0x6120, 0x1240), Status.FunctionRejected),
            (f => { f.Row(0, 0x6100, 0x6180, 0x1200); f.Row(1, 0x6000, 0x6080, 0x1240); }, Status.FunctionRejected),
            (f => f.Row(0, 0x6000, 0x6080, 0x1201), Status.UnwindRejected),
            (f => { f.Row(0, 0x6000, 0x6080, 0x1FFC); f.Image[0x1FFC] = 1; f.Image[0x1FFE] = 255; }, Status.UnwindRejected),
            (f => f.Row(0, 0x6000, 0x6080, 0x6000), Status.UnwindRejected)
        ];
        foreach (var (change, expected) in cases)
        {
            using Fixture f = new(); change(f); f.Register();
            byte[] original = Bytes(f.Image, Fixture.ImageBytes);
            Failure(f.Bind(), expected, "directory/function");
            Check(Zero(f.Snapshot, Fixture.Capacity) && Bytes(f.Image, Fixture.ImageBytes).AsSpan().SequenceEqual(original), "rejection does not publish or mutate image");
        }
    }

    private static void Opcodes()
    {
        // Each complete form includes only its meaningful dependencies; no native execution is implied.
        (byte Frame, byte[] Codes, uint Allocation, uint Stack)[] cases = [
            (0, [1, 0x30], 0, 8),
            (0, [5, 0x01, 17, 0], 136, 136),
            (0, [5, 0x11, 0, 0, 8, 0], 524288, 524288),
            (0, [5, 0xF2], 128, 128),
            (0x25, [16, 0x64, 4, 0, 12, 0x03, 8, 0x72, 1, 0x50], 64, 72),
            (0, [8, 0x35, 0, 0, 8, 0, 4, 0x72], 64, 64),
            (0, [8, 0x68, 2, 0, 4, 0x72], 64, 64),
            (0, [8, 0x79, 0, 0, 16, 0, 4, 0x72], 64, 64),
            (0, [0, 0x0A], 0, 40),
            (0, [0, 0x1A], 0, 48)
        ];
        foreach (var (frame, codes, allocation, stack) in cases)
        {
            using Fixture f = new(); f.Unwind(0x1200, 1, 16, frame, codes);
            Check(f.Bind().Succeeded, "opcode form " + codes[1]);
            var result = f.Lookup(0x6001);
            Check(result.Succeeded && result.FixedAllocationBytes == allocation && result.StackBytes == stack &&
                result.FrameRegister == (frame & 15) && result.FrameOffset == (frame >> 4), "decoded operands");
        }
    }

    private static void HostileCodes()
    {
        (byte Header, byte Frame, byte[] Codes, Status Expected)[] cases = [
            (2, 0, [], Status.UnsupportedFormat), (0x41, 0, [], Status.UnwindRejected),
            (0x29, 0, [], Status.UnwindRejected), (1, 0x10, [], Status.UnwindRejected),
            (1, 4, [], Status.UnwindRejected), (1, 5, [], Status.UnwindRejected),
            (1, 0, [5, 1], Status.UnwindRejected), (1, 0, [5, 0x11, 0, 0], Status.UnwindRejected),
            (1, 0, [5, 0x21, 0, 0, 8, 0], Status.UnwindRejected),
            (1, 0, [5, 1, 1, 0], Status.UnwindRejected),
            (1, 0, [5, 0x11, 1, 0, 8, 0], Status.UnwindRejected),
            (1, 0, [5, 6], Status.UnsupportedFormat), (1, 0, [5, 7], Status.UnsupportedFormat),
            (1, 0, [5, 0], Status.UnwindRejected), (1, 0, [5, 0x40], Status.UnwindRejected),
            (1, 0, [5, 0x58, 0, 0], Status.UnwindRejected),
            (1, 0, [5, 0x35, 1, 0, 8, 0], Status.UnwindRejected),
            (1, 0, [5, 0x69, 8, 0, 16, 0], Status.UnwindRejected),
            (1, 0, [4, 0x32, 5, 0x50], Status.UnwindRejected),
            (1, 0, [17, 0x32], Status.UnwindRejected),
            (1, 0, [5, 0x50, 4, 0x32], Status.UnwindRejected),
            (1, 0, [5, 0x2A], Status.UnwindRejected),
            (1, 5, [12, 3, 8, 0x64, 4, 0, 4, 0x72], Status.UnwindRejected),
            (1, 0xF5, [12, 3, 4, 0x72], Status.UnwindRejected),
            (1, 5, [12, 0x13, 4, 0x72], Status.UnwindRejected)
        ];
        foreach (var (header, frame, codes, expected) in cases)
        {
            using Fixture f = new(); f.Unwind(0x1200, header, 16, frame, codes);
            Failure(f.Bind(), expected, "unwind header/opcode");
            Check(Zero(f.Snapshot, Fixture.Capacity), "unwind failure publishes nothing");
        }
    }

    private static void Handlers()
    {
        foreach (byte flags in new byte[] { 1, 2, 3 })
        {
            using Fixture f = new(); f.Unwind(0x1200, (byte)(1 | flags << 3), 0, 0, []); W32(f.Image + 0x1204, 0x6500);
            Check(f.Bind().Succeeded, "handler prefix");
            var result = f.Lookup(0x6000);
            Check(result.Succeeded && result.HandlerRva == 0x6500 && result.LanguageDataStartRva == 0x1208 && result.PrimaryFlags == flags, "handler identity only");
        }
        foreach (uint handler in new uint[] { 0, 0x1500, 0x2000, 0x7000, 0xFFFFFFFF })
        {
            using Fixture f = new(); f.Unwind(0x1200, 9, 0, 0, []); W32(f.Image + 0x1204, handler);
            Failure(f.Bind(), Status.UnwindRejected, "handler permissions/range");
        }
    }

    private static void Chains()
    {
        using (Fixture f = new())
        {
            W32(f.Image + 0x124, 36); f.Row(2, 0x6200, 0x6280, 0x1280);
            f.Chain(0x1240, 0x6000, 0x6080, 0x1200); f.Chain(0x1280, 0x6100, 0x6180, 0x1240);
            Check(f.Bind().Succeeded, "listed two-hop chain");
            var r = f.Lookup(0x6200);
            SameResult(f.LookupFunction(2), r, "indexed chain uses the same primary walk");
            Check(r.Succeeded && r.BeginRva == 0x6200 && r.UnwindRva == 0x1280 && r.PrimaryUnwindRva == 0x1200 &&
                r.ChainDepth == 2 && r.FixedAllocationBytes == 32 && r.StackBytes == 40 && r.Flags == 4 && r.PrimaryFlags == 0, "chain resolves owning primary");
        }
        (Action<Fixture>, Status)[] bad = [
            (f => f.Chain(0x1240, 0x6100, 0x6180, 0x1240), Status.ChainCycle),
            (f => { f.Chain(0x1200, 0x6100, 0x6180, 0x1240); f.Chain(0x1240, 0x6000, 0x6080, 0x1200); }, Status.ChainCycle),
            (f => f.Chain(0x1240, 0x6001, 0x6080, 0x1200), Status.FunctionRejected),
            (f => f.Chain(0x1240, 0xFFFFFFF0, 0xFFFFFFFF, 0x1200), Status.FunctionRejected),
            (f => { f.Chain(0x1240, 0x6000, 0x6080, 0x1200); f.Image[0x1243] = 5; }, Status.UnwindRejected),
            (f => f.Unwind(0x1240, 33, 4, 0, [4, 0x32]), Status.UnsupportedFormat)
        ];
        foreach (var (change, expected) in bad) { using Fixture f = new(); change(f); Failure(f.Bind(), expected, "hostile chain"); }
        using Fixture longChain = new();
        W32(longChain.Image + 0x124, 33 * 12);
        // Move the table so 33 entries and all unwind prefixes remain disjoint.
        W32(longChain.Image + 0x120, 0x1800);
        for (uint i = 0; i < 33; i++)
        {
            byte* row = longChain.Image + 0x1800 + i * 12;
            W32(row, 0x6000 + i * 16); W32(row + 4, 0x6010 + i * 16); W32(row + 8, 0x1200 + i * 16);
            longChain.Unwind((int)(0x1200 + i * 16), 1, 0, 0, []);
            if (i != 32) longChain.Chain((int)(0x1200 + i * 16), 0x6010 + i * 16, 0x6020 + i * 16, 0x1210 + i * 16);
        }
        Failure(longChain.Bind(), Status.CapacityExceeded, "bounded chain depth");
    }

    private static void Storage()
    {
        using Fixture f = new(); f.Register();
        var measured = Core.Validate(f.Capture, f.Mapping, f.Owner, Fixture.Capacity, f.Handle);
        Check(measured.Succeeded && measured.RequiredStorage > Core.StateBytes, "measured caller storage");
        foreach (var item in new (ulong Start, ulong Bytes, Status Expected)[] {
            (0, Fixture.Capacity, Status.StorageRejected), ((ulong)f.Snapshot + 1, Fixture.Capacity - 1, Status.StorageRejected),
            ((ulong)f.Snapshot, measured.RequiredStorage - 1, Status.StorageRejected), (ulong.MaxValue - 7, 16, Status.StorageRejected),
            ((ulong)f.Owner + 8, Fixture.Capacity, Status.StorageOverlap), (f.Address(0x1100), Fixture.Capacity, Status.StorageOverlap),
            (f.Address(0x2000), 1024, Status.StorageOverlap), (f.Address(0x3800), 4096, Status.StorageOverlap) })
        {
            byte[] image = Bytes(f.Image, Fixture.ImageBytes), owner = Bytes(f.Owner, (int)Fixture.Capacity);
            Failure(Core.Bind(f.Capture, f.Mapping, f.Owner, Fixture.Capacity, f.Handle, (byte*)item.Start, item.Bytes), item.Expected, "storage authority");
            Check(Bytes(f.Image, Fixture.ImageBytes).AsSpan().SequenceEqual(image) && Bytes(f.Owner, (int)Fixture.Capacity).AsSpan().SequenceEqual(owner) && Zero(f.Snapshot, Fixture.Capacity), "alias rejection no mutation");
        }
        new Span<byte>(f.Snapshot + measured.RequiredStorage, (int)(Fixture.Capacity - measured.RequiredStorage)).Fill(0xA5);
        Check(f.Bind().Succeeded, "prefix publication");
        for (ulong i = measured.RequiredStorage; i < Fixture.Capacity; i++) Check(f.Snapshot[i] == 0xA5, "capacity tail preserved");
        Failure(f.Bind(), Status.AlreadyBound, "once only"); f.Guards();
        using Fixture inside = new(); inside.Register();
        Check(Core.Bind(inside.Capture, inside.Mapping, inside.Owner, Fixture.Capacity, inside.Handle, inside.Image + 0x2800, 0x800).Succeeded &&
            Core.Lookup(inside.Image + 0x2800, 0x800, inside.Address(0x6000)).Succeeded, "original image RW private storage");
    }

    private static void RetainedIdentity()
    {
        using Fixture f = new(); Check(f.Bind().Succeeded, "bind");
        f.Image[0x1205] = 0x42; // Well-formed replacement: allocation 40 instead of 32.
        Failure(f.Lookup(0x6000), Status.MetadataChanged, "well formed metadata replacement");
        Failure(f.LookupFunction(0), Status.MetadataChanged, "indexed well formed metadata replacement");
        Failure(f.LookupFunction(2), Status.MetadataChanged, "invalid index cannot bypass metadata revalidation"); f.Image[0x1205] = 0x32;
        Check(f.Lookup(0x6000).Succeeded, "original restored");
        f.Image[0x1241] = 1;
        Failure(f.LookupFunction(0), Status.MetadataChanged, "indexed lookup revalidates the unselected function too"); f.Image[0x1241] = 0;
        f.Snapshot[Core.StateBytes + 3] ^= 1;
        Failure(f.Lookup(0x6000), Status.MetadataChanged, "snapshot change");
        Failure(f.LookupFunction(0), Status.MetadataChanged, "indexed snapshot change");
        Failure(f.LookupFunction(uint.MaxValue), Status.MetadataChanged, "maximum index cannot bypass snapshot revalidation"); f.Snapshot[Core.StateBytes + 3] ^= 1;
        using Fixture copy = new(); new ReadOnlySpan<byte>(f.Snapshot, (int)Fixture.Capacity).CopyTo(new Span<byte>(copy.Snapshot, (int)Fixture.Capacity));
        Failure(copy.Lookup(0x6000), Status.NotBound, "copied state is not original owner");
        Failure(copy.LookupFunction(0), Status.NotBound, "indexed copied snapshot");
        Failure(copy.LookupFunction(uint.MaxValue), Status.NotBound, "maximum index cannot bypass copied snapshot");
        copy.Register(); byte[] owner = Bytes(f.Owner, (int)Fixture.Capacity);
        new ReadOnlySpan<byte>(copy.Owner, (int)Fixture.Capacity).CopyTo(new Span<byte>(f.Owner, (int)Fixture.Capacity));
        Failure(f.Lookup(0x6000), Status.ModuleRejected, "copied registration cannot replace original owner");
        Failure(f.LookupFunction(0), Status.ModuleRejected, "indexed copied registration");
        owner.AsSpan().CopyTo(new Span<byte>(f.Owner, (int)Fixture.Capacity));
        Check(f.LookupFunction(0).Succeeded, "original owner restoration remains usable");
        SameResult(f.LookupFunction(0), f.Lookup(0x6000), "original registration and snapshot restored");
        f.Owner[0] ^= 1;
        Failure(f.Lookup(0x6000), Status.ModuleRejected, "retained module phase changed");
        Failure(f.LookupFunction(0), Status.ModuleRejected, "indexed retained module phase changed");
        Failure(f.LookupFunction(2), Status.ModuleRejected, "invalid index cannot bypass stale registration"); f.Owner[0] ^= 1;
        W64(f.Image + 0x2000, copy.Address(0x2000));
        Failure(f.Lookup(0x6000), Status.ModuleRejected, "published module identity changed");
        Failure(f.LookupFunction(0), Status.ModuleRejected, "indexed published module identity changed");
        f.Guards(); copy.Guards();
    }

    private static void Allocation()
    {
        using Fixture f = new(); Check(f.Bind().Succeeded, "bind");
        for (int i = 0; i < 16; i++)
        {
            _ = f.Lookup(0x6000); _ = f.LookupFunction(0); _ = f.LookupFunction(1); _ = f.LookupFunction(2);
            _ = Core.Validate(f.Capture, f.Mapping, f.Owner, Fixture.Capacity, f.Handle);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        bool good = true;
        for (int i = 0; i < 32; i++) good &= f.Lookup(0x6000).Succeeded && f.LookupFunction(0).Succeeded &&
            f.LookupFunction(1).Succeeded && f.LookupFunction(2).Status == Status.NotFound &&
            Core.Validate(f.Capture, f.Mapping, f.Owner, Fixture.Capacity, f.Handle).Succeeded;
        long after = GC.GetAllocatedBytesForCurrentThread();
        Check(good && before == after, "no managed allocation");
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
        internal Fixture()
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
            W32(Image + 0x104, 16); W32(Image + 0x120, 0x1100); W32(Image + 0x124, 24);
            Section(0, ".rdata", 0x1000, 0x1000, 0x40000040); Section(1, ".data", 0x2000, 0x2000, 0xC0000040);
            Section(2, ".modules", 0x5000, 16, 0x40000040); Section(3, ".text", 0x6000, 0x1000, 0x60000020);
            W64(Image + 0x5000, Address(0x1000)); W32(Image + 0x1000, 0x00525452); W16(Image + 0x1004, 16);
            W16(Image + 0x100C, 1); Image[0x100E] = 24; Image[0x100F] = 1;
            W32(Image + 0x1010, 204); W64(Image + 0x1018, Address(0x2000));
            Row(0, 0x6000, 0x6080, 0x1200); Row(1, 0x6100, 0x6180, 0x1240);
            Unwind(0x1200, 1, 5, 0, [5, 0x32, 1, 0x50]); Unwind(0x1240, 1, 0, 0, []);
            var observation = EfiLoadedImageProtocolObservation.CaptureFromUefiBoundary(0, 0x1000, 0x2000, (ulong)Image, ImageBytes);
            Capture = EfiLoadedImageRangeCapturePlan.Create(&observation, 1, 1, 4096);
            Mapping = M01LoadedImageVirtualMappingEvidence.CaptureCallerAttestedIdentity(Capture);
        }
        internal ulong Address(uint rva) => (ulong)Image + rva;
        internal void Register()
        {
            if (Handle != 0) return;
            var result = TypeManagerModuleMetadataLiveNativeAOTImage.RegisterOnce(Capture, Mapping, Owner, Capacity);
            Check(result.Succeeded, "original module registration " + result.Status); Handle = result.Handle;
        }
        internal X64UnwindMetadataResult Bind() { Register(); return Core.Bind(Capture, Mapping, Owner, Capacity, Handle, Snapshot, Capacity); }
        internal X64UnwindMetadataResult Lookup(uint pc) => Core.Lookup(Snapshot, Capacity, Address(pc));
        internal X64UnwindMetadataResult LookupFunction(uint index) => Core.LookupFunction(Snapshot, Capacity, index);
        internal void Row(uint i, uint begin, uint end, uint unwind)
        {
            byte* p = Image + 0x1100 + i * 12; W32(p, begin); W32(p + 4, end); W32(p + 8, unwind);
        }
        internal void Unwind(int rva, byte header, byte prolog, byte frame, byte[] codes)
        {
            new Span<byte>(Image + rva, 16).Clear(); Image[rva] = header; Image[rva + 1] = prolog;
            Image[rva + 2] = (byte)(codes.Length / 2); Image[rva + 3] = frame;
            codes.AsSpan().CopyTo(new Span<byte>(Image + rva + 4, codes.Length));
        }
        internal void Chain(int rva, uint begin, uint end, uint unwind)
        {
            Unwind(rva, 33, 0, 0, []); W32(Image + rva + 4, begin); W32(Image + rva + 8, end); W32(Image + rva + 12, unwind);
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
    private static bool Zero(byte* p, ulong bytes) { for (ulong i = 0; i < bytes; i++) if (p[i] != 0) return false; return true; }
    private static void W16(byte* p, ushort v) { p[0] = (byte)v; p[1] = (byte)(v >> 8); }
    private static void W32(byte* p, uint v) { W16(p, (ushort)v); W16(p + 2, (ushort)(v >> 16)); }
    private static void W64(byte* p, ulong v) { W32(p, (uint)v); W32(p + 4, (uint)(v >> 32)); }
}
