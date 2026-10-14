using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Kernel.Runtime;
using Row = NativeAotEhClauseMetadataTests.Row;
using Status = OS.Kernel.Runtime.BoundedExceptionFrameSearchStatus;

internal static unsafe class BoundedExceptionFrameSearchTests
{
    private static void Need(bool value, string why) { if (!value) throw new InvalidOperationException(why); }
    private static bool Same(X64UnwindContext a, X64UnwindContext b) =>
        new ReadOnlySpan<byte>(&a, sizeof(X64UnwindContext)).SequenceEqual(new ReadOnlySpan<byte>(&b, sizeof(X64UnwindContext)));
    private static BoundedExceptionCleanupEntry[] Output(int length = 9)
    {
        var output = new BoundedExceptionCleanupEntry[length];
        MemoryMarshal.AsBytes(output.AsSpan()).Fill(0xA5); return output;
    }
    private static void ZeroOutputs(BoundedExceptionCleanupEntry[] output, int first = 0)
    {
        for (int i = first; i < Math.Min(8, output.Length); i++)
            Need(output[i].FrameIndex == 0 && output[i].Handler == 0 && Same(output[i].Context, default), "zero cleanup " + i);
        foreach (byte b in MemoryMarshal.AsBytes(output.AsSpan(Math.Min(8, output.Length)))) Need(b == 0xA5, "output tail unchanged");
    }
    private static void Failed(BoundedExceptionFrameSearchResult r, Status status, BoundedExceptionCleanupEntry[] output,
        BoundedExceptionDispatchStatus dispatch = default)
    {
        Need(r.Status == status && !r.Succeeded && r.DispatchStatus == dispatch && r.CatchFrameIndex == 0 &&
            r.CatchClauseIndex == 0 && r.CatchHandler == 0 && r.CleanupCount == 0 && Same(r.CatchContext, default),
            "atomic result: " + r.Status + "/" + r.DispatchStatus);
        ZeroOutputs(output);
    }

    [HostTest("runtime.bounded-frame-search.outer-catch", HostLane.Fast, 0)]
    private static void OuterCatch()
    {
        using Fixture f = new();
        var a = f.Rows(0, new(20, 6, 1, 0x100), new(20, 16, 1, 0x200), new(20, 26, 0, 0x300, 0x14C0));
        var b = f.Rows(1, new(20, 6, 1, 0x100), new(20, 26, 0, 0x300, 0x14C0));
        var c = f.Rows(3, new(20, 6, 1, 0x100), new(20, 16, 1, 0x200), new(20, 26, 0, 0x400, 0x1440));
        var rows = a.Concat(b).Concat(c).ToArray();
        BoundedExceptionSearchFrame[] frames = [f.Frame(0, 0, 3), f.Frame(1, 3, 2), f.Frame(2, 5, 0), f.Frame(3, 5, 3)];
        var output = Output(); var r = BoundedExceptionFrameSearch.Select(frames, rows, f.Matches(rows), output);
        Need(r.Succeeded && r.CatchFrameIndex == 3 && r.CatchClauseIndex == 2 && r.CatchHandler == c[2].HandlerAddress &&
            r.CleanupCount == 5 && Same(r.CatchContext, frames[3].Context), "outer catch and intact full context");
        ulong[] handlers = [a[0].HandlerAddress, a[1].HandlerAddress, b[0].HandlerAddress, c[0].HandlerAddress, c[1].HandlerAddress];
        uint[] owners = [0, 0, 1, 3, 3];
        for (int i = 0; i < handlers.Length; i++) Need(output[i].Handler == handlers[i] && output[i].FrameIndex == owners[i] &&
            Same(output[i].Context, frames[owners[i]].Context), "physical frame and inner-to-outer cleanup " + i);
        ZeroOutputs(output, 5);
    }

    [HostTest("runtime.bounded-frame-search.recursive-target-identity", HostLane.Fast, 1)]
    private static void RecursiveIdentity()
    {
        using Fixture f = new();
        var row = f.Rows(0, new(20, 6, 1, 0x100), new(50, 26, 0, 0x100, 0x1440));
        var rows = row.Concat(row).Concat(row).ToArray();
        BoundedExceptionSearchFrame[] frames = [f.Frame(0, 0, 2), f.Frame(0, 2, 2, rsp: 0x100100), f.Frame(0, 4, 2, 55, 0x100200)];
        var output = Output(); var r = BoundedExceptionFrameSearch.Select(frames, rows, f.Matches(rows), output);
        Need(r.Succeeded && r.CatchFrameIndex == 2 && r.CleanupCount == 2 && output[0].Handler == output[1].Handler &&
            output[1].Handler == r.CatchHandler && output[0].Context.Rsp != output[1].Context.Rsp &&
            output[1].Context.Rsp != r.CatchContext.Rsp, "same handler at distinct recursive frame contexts admitted");
        ZeroOutputs(output, 2);
        frames[1] = f.Frame(0, 2, 2);
        output = Output(); Failed(BoundedExceptionFrameSearch.Select(frames, rows, f.Matches(rows), output), Status.InvalidFrame, output);
    }

    [HostTest("runtime.bounded-frame-search.inner-catch-invalid-tail", HostLane.Fast, 2)]
    private static void InnerCatchAndTail()
    {
        using Fixture f = new();
        var a = f.Rows(0, new(20, 6, 1, 0x100), new(20, 26, 0, 0x300, 0x1440));
        var b = f.Rows(1, new(20, 6, 1, 0x100), new(20, 26, 0, 0x400, 0x1440));
        var rows = a.Concat(b).ToArray(); var matches = f.Matches(rows);
        BoundedExceptionSearchFrame[] frames = [f.Frame(0, 0, 2), f.Frame(1, 2, 2)];
        var output = Output(); var r = BoundedExceptionFrameSearch.Select(frames, rows, matches, output);
        Need(r.Succeeded && r.CatchFrameIndex == 0 && r.CleanupCount == 1 && output[0].Handler == a[0].HandlerAddress, "first frame wins");
        ZeroOutputs(output, 1);
        matches[3] = default;
        output = Output(); Failed(BoundedExceptionFrameSearch.Select(frames, rows, matches, output), Status.DispatchRejected, output,
            BoundedExceptionDispatchStatus.InvalidTypeMatch);
        matches = f.Matches(rows); matches[3] = f.Owner.Match(rows[3].TypeAddress, handle: f.Owner.Handle ^ 1);
        output = Output(); Failed(BoundedExceptionFrameSearch.Select(frames, rows, matches, output), Status.DispatchRejected, output,
            BoundedExceptionDispatchStatus.InvalidTypeMatch);
        matches = f.Matches(rows); rows[3] = default;
        output = Output(); Failed(BoundedExceptionFrameSearch.Select(frames, rows, matches, output), Status.DispatchRejected, output,
            BoundedExceptionDispatchStatus.InvalidClause);
    }

    [HostTest("runtime.bounded-frame-search.no-eh-no-match-default", HostLane.Fast, 3)]
    private static void NoMatch()
    {
        using Fixture f = new(); var output = Output();
        Failed(default, Status.Uninitialized, new BoundedExceptionCleanupEntry[8]);
        Failed(BoundedExceptionFrameSearch.Select([], [], [], output), Status.Empty, output);
        BoundedExceptionSearchFrame[] empty = [f.Frame(0, 0, 0), f.Frame(1, 0, 0)];
        output = Output(); Failed(BoundedExceptionFrameSearch.Select(empty, [], [], output), Status.NoMatchingCatch, output);
        var rows = f.Rows(1, new(20, 6, 1, 0x100), new(20, 26, 0, 0x400, 0x14C0));
        BoundedExceptionSearchFrame[] frames = [f.Frame(0, 0, 0), f.Frame(1, 0, 2)];
        output = Output(); Failed(BoundedExceptionFrameSearch.Select(frames, rows, f.Matches(rows), output), Status.NoMatchingCatch, output);
        output = Output(7); Failed(BoundedExceptionFrameSearch.Select(frames, rows, f.Matches(rows), output), Status.OutputTooSmall, output);
    }

    [HostTest("runtime.bounded-frame-search.slices-context-and-method-bounds", HostLane.Fast, 4)]
    private static void Bounds()
    {
        using Fixture f = new(); var rows = f.Rows(0, new Row(20, 26, 0, 0x400, 0x1440)); var matches = f.Matches(rows);
        var good = f.Frame(0, 0, 1); var context = good.Context;
        foreach (var bad in new[] {
            default(BoundedExceptionSearchFrame), new(context, 25, 0, 0, 1), new(context, 0x500, 0x500, 0, 1),
            new(context, 25, uint.MaxValue, 0, 1), new(new X64UnwindContext { Rip = 1, Rsp = 0x100000 }, 25, 0x500, 0, 1),
            new(new X64UnwindContext { Rip = ulong.MaxValue, Rsp = 0x100000 }, 0, 0x500, 0, 1),
            new(new X64UnwindContext { Rip = context.Rip, Rsp = 0 }, 25, 0x500, 0, 1),
            new(new X64UnwindContext { Rip = context.Rip, Rsp = context.Rsp + 1 }, 25, 0x500, 0, 1) })
        {
            var output = Output(); Failed(BoundedExceptionFrameSearch.Select([bad], rows, matches, output), Status.InvalidFrame, output);
        }
        foreach (var bad in new[] { new BoundedExceptionSearchFrame(context, 25, 0x500, uint.MaxValue, 1), new(context, 25, 0x500, 1, 1),
            new(context, 25, 0x500, 0, 2), new(context, 25, 0x500, 0, 0) })
        {
            var output = Output(); Failed(BoundedExceptionFrameSearch.Select([bad], rows, matches, output), Status.InvalidSlice, output);
        }
        var small = new BoundedExceptionSearchFrame(context, 25, 0x400, 0, 1);
        var targetOutput = Output(); Failed(BoundedExceptionFrameSearch.Select([small], rows, matches, targetOutput), Status.InvalidClauseRange, targetOutput);
        var wrongMatches = Output(); Failed(BoundedExceptionFrameSearch.Select([good], rows, [], wrongMatches), Status.DispatchRejected,
            wrongMatches, BoundedExceptionDispatchStatus.InvalidTypeMatch);
        var tailOutput = Output(); var tail = f.Frame(1, 1, 0, rsp: context.Rsp - 8);
        Failed(BoundedExceptionFrameSearch.Select([good, tail], rows, matches, tailOutput), Status.InvalidFrame, tailOutput);
        // A genuine return PC need not be aligned: method+26 is intentionally byte-granular.
        var successOutput = Output(); Need(BoundedExceptionFrameSearch.Select([good], rows, matches, successOutput).Succeeded, "unaligned RIP admitted");
    }

    [HostTest("runtime.bounded-frame-search.no-match-cleanup-validation", HostLane.Fast, 5)]
    private static void UnmatchedControls()
    {
        using Fixture f = new();
        void Reject(NativeAotEhClauseResult[] rows, Status status, BoundedExceptionDispatchStatus dispatch = default)
        {
            var output = Output(); Failed(BoundedExceptionFrameSearch.Select([f.Frame(0, 0, (uint)rows.Length)], rows, f.Matches(rows), output), status, output, dispatch);
        }
        Reject(f.Rows(0, new(20, 6, 1, 0x100), new(20, 16, 1, 0x100), new(20, 26, 0, 0x400, 0x14C0)), Status.DuplicateCleanup);
        Reject(f.Rows(0, new(20, 6, 1, 0x100), new(20, 16, 1, 0x200), new(20, 26, 1, 0x300), new(20, 36, 0, 0x400, 0x14C0)), Status.CapacityExceeded);
        Reject(f.Rows(0, new(20, 16, 1, 0x100), new(25, 16, 0, 0x400, 0x14C0)), Status.DispatchRejected, BoundedExceptionDispatchStatus.InvalidNesting);
        Reject(f.Rows(0, new Row(0, 0, 1, 0)), Status.DispatchRejected, BoundedExceptionDispatchStatus.UnsupportedClause);
        Reject(f.Rows(0, new Row(20, 16, 2, 0x100, 0x200)), Status.DispatchRejected, BoundedExceptionDispatchStatus.UnsupportedClause);
        Reject(f.Rows(0, new(20, 6, 1, 0x100), new(20, 26, 0, 0x100, 0x1440)), Status.DispatchRejected, BoundedExceptionDispatchStatus.InvalidClause);
    }

    [HostTest("runtime.bounded-frame-search.capacity-eight-cleanups", HostLane.Fast, 6)]
    private static void Capacity()
    {
        using Fixture f = new(); var groups = new NativeAotEhClauseResult[4][];
        for (int i = 0; i < 3; i++) groups[i] = f.Rows(i, new(20, 6, 1, 0x100), new(20, 16, 1, 0x200), new(20, 26, 0, 0x300, 0x14C0));
        groups[3] = f.Rows(3, new(20, 6, 1, 0x100), new(20, 16, 1, 0x200), new(20, 26, 0, 0x300, 0x14C0), new(20, 26, 0, 0x400, 0x1440));
        var rows = groups.SelectMany(g => g).ToArray(); var matches = f.Matches(rows);
        BoundedExceptionSearchFrame[] frames = [f.Frame(0, 0, 3), f.Frame(1, 3, 3), f.Frame(2, 6, 3), f.Frame(3, 9, 4)];
        var output = Output(); var r = BoundedExceptionFrameSearch.Select(frames, rows, matches, output);
        Need(r.Succeeded && r.CleanupCount == 8 && output[7].FrameIndex == 3 && output[7].Handler == groups[3][1].HandlerAddress, "four frames/four rows/eight cleanups accepted");
        ZeroOutputs(output, 8);
        output = Output(); Failed(BoundedExceptionFrameSearch.Select(new BoundedExceptionSearchFrame[5], rows, matches, output), Status.CapacityExceeded, output);
        output = Output(); Failed(BoundedExceptionFrameSearch.Select(frames, new NativeAotEhClauseResult[17], new ExceptionTypeMatchResult[17], output), Status.CapacityExceeded, output);
        output = Output(); Failed(BoundedExceptionFrameSearch.Select([f.Frame(0, 0, 5)], rows, matches, output), Status.CapacityExceeded, output);
        // An over-capacity no-match tail cannot be ignored after an inner catch.
        var first = f.Rows(0, new Row(20, 26, 0, 0x400, 0x1440));
        var last = f.Rows(1, new(20, 6, 1, 0x100), new(20, 16, 1, 0x200), new(20, 26, 1, 0x300));
        rows = first.Concat(last).ToArray(); output = Output();
        Failed(BoundedExceptionFrameSearch.Select([f.Frame(0, 0, 1), f.Frame(1, 1, 3)], rows, f.Matches(rows), output), Status.CapacityExceeded, output);
    }

    [HostTest("runtime.bounded-frame-search.no-allocation-or-input-mutation", HostLane.Fast, 7)]
    private static void NoAllocation()
    {
        using Fixture f = new(); var rows = f.Rows(0, new(20, 6, 1, 0x100), new(20, 26, 0, 0x400, 0x1440)); var matches = f.Matches(rows);
        BoundedExceptionSearchFrame[] frames = [f.Frame(0, 0, 2)]; var output = Output();
        var originalRows = rows.ToArray(); var originalMatches = matches.ToArray(); var originalFrames = frames.ToArray();
        byte[] image = f.Owner.ImageSnapshot(), heap = f.Owner.HeapSnapshot();
        Need(!RuntimeHelpers.IsReferenceOrContainsReferences<BoundedExceptionSearchFrame>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<BoundedExceptionCleanupEntry>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<BoundedExceptionFrameSearchResult>(), "POD surfaces");
        for (int i = 0; i < 64; i++) _ = BoundedExceptionFrameSearch.Select(frames, rows, matches, output);
        long before = GC.GetAllocatedBytesForCurrentThread(); int successes = 0;
        for (int i = 0; i < 128; i++) if (BoundedExceptionFrameSearch.Select(frames, rows, matches, output).Succeeded) successes++;
        Need(successes == 128 && GC.GetAllocatedBytesForCurrentThread() == before, "zero allocation");
        Need(rows.SequenceEqual(originalRows) && matches.SequenceEqual(originalMatches) && frames.SequenceEqual(originalFrames), "inputs unchanged");
        f.Owner.Unchanged(image, heap); ZeroOutputs(output, 1);
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly ExceptionTypeMatchTests.Fixture Owner = new(PrepareImage);
        private readonly byte* snapshot = (byte*)NativeMemory.AllocZeroed(0x4000);
        internal Fixture()
        {
            Need(snapshot != null, "snapshot allocation");
            var bound = X64PERuntimeFunctionUnwindMetadataHostileInput.Bind(Owner.Capture, Owner.Mapping,
                Owner.Owner, 4096, Owner.Handle, snapshot, 0x4000);
            Need(bound.Succeeded, "same-image binding " + bound.Status);
        }
        private static void PrepareImage(nint address)
        {
            byte* image = (byte*)address;
            W32(image + 0x188 + 3 * 40 + 8, 0x2000); // Four separate root/funclet contributions and a no-EH helper.
            W32(image + 0x104, 16); W32(image + 0x120, 0x1800); W32(image + 0x124, 21 * 12);
            for (int i = 0; i < 21; i++)
            {
                uint begin = i == 20 ? 0x7F00U : 0x6000U + (uint)i * 0x100, unwind = 0x1900U + (uint)i * 0x20;
                W32(image + 0x1800 + i * 12, begin); W32(image + 0x1804 + i * 12, begin + 0x80);
                W32(image + 0x1808 + i * 12, unwind); image[unwind] = (byte)(i < 20 && i % 5 == 0 ? 9 : 1);
                if (i < 20 && i % 5 == 0)
                { W32(image + unwind + 4, 0x7F00); image[unwind + 8] = 4; W32(image + unwind + 9, 0x1C00U + (uint)(i / 5) * 0x100); }
                else if (i < 20) image[unwind + 4] = 1;
            }
        }
        internal NativeAotEhClauseResult[] Rows(int frame, params Row[] rows)
        {
            for (int i = 1; i < 5; i++) Owner.Image[0x1904 + (frame * 5 + i) * 0x20] = 1;
            foreach (var row in rows) if (row.Kind == 2) Owner.Image[0x1904 + (frame * 5 + row.Extra / 0x100) * 0x20] = 2;
            byte[] blob = NativeAotEhClauseMetadataTests.Table(rows); uint table = 0x1C00U + (uint)frame * 0x100;
            new Span<byte>(Owner.Image + table, 0x100).Clear(); blob.CopyTo(new Span<byte>(Owner.Image + table, blob.Length));
            var result = new NativeAotEhClauseResult[rows.Length];
            for (uint i = 0; i < result.Length; i++)
            {
                result[i] = NativeAotEhClauseMetadata.ReadClause(NativeAotLayoutProfile.Ilc11_0_0Rc2WinX64LittleEndian,
                    Owner.Capture, Owner.Mapping, Owner.Owner, 4096, Owner.Handle, snapshot, 0x4000,
                    new(0x6000U + (uint)frame * 0x500, 0x500, table, (uint)blob.Length), i);
                Need(result[i].Succeeded, "original decoded row " + i + ": " + result[i].Status);
            }
            return result;
        }
        internal ExceptionTypeMatchResult[] Matches(NativeAotEhClauseResult[] rows)
        {
            var matches = new ExceptionTypeMatchResult[rows.Length];
            for (int i = 0; i < rows.Length; i++) if (rows[i].Kind == NativeAotEhClauseKind.Typed) matches[i] = Owner.Match(rows[i].TypeAddress);
            return matches;
        }
        internal BoundedExceptionSearchFrame Frame(int frame, uint offset, uint count, uint pc = 25, ulong rsp = 0)
        {
            uint method = count == 0 ? 0x7F00U : 0x6000U + (uint)frame * 0x500;
            X64UnwindContext context = new() { Rip = Owner.Address((int)method) + pc + 1, Rsp = rsp == 0 ? 0x100000U + (uint)frame * 0x100 : rsp,
                Rbx = Owner.Receipt.ObjectReference, Rbp = 0x200000U + (uint)frame * 0x100, R12 = 42, Xmm6 = new() { Low = 0x1234, High = 0x5678 } };
            return new(context, pc, count == 0 ? 0x80U : 0x500U, offset, count);
        }
        public void Dispose() { NativeMemory.Free(snapshot); Owner.Dispose(); }
    }
    private static void W32(byte* p, uint value) { for (int i = 0; i < 4; i++) p[i] = (byte)(value >> (i * 8)); }
}
