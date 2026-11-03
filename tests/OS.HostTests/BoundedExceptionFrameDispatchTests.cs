using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Kernel.Runtime;
using Row = NativeAotEhClauseMetadataTests.Row;
using Status = OS.Kernel.Runtime.BoundedExceptionFrameDispatchStatus;
using SearchStatus = OS.Kernel.Runtime.BoundedExceptionFrameSearchStatus;

internal static unsafe partial class BoundedExceptionFrameDispatchTests
{
    private static bool Same<T>(T a, T b)
        where T : unmanaged => new ReadOnlySpan<byte>(&a, sizeof(T)).SequenceEqual(new ReadOnlySpan<byte>(&b, sizeof(T)));
    private static BoundedExceptionCleanupEntry[] Scratch(int length = 9)
    {
        var scratch = new BoundedExceptionCleanupEntry[length];
        MemoryMarshal.AsBytes(scratch.AsSpan()).Fill(0xA5);
        return scratch;
    }

    private static void Cleared(BoundedExceptionCleanupEntry[] scratch, int first = 0)
    {
        int end = Math.Min(8, scratch.Length);
        for (int i = first; i < end; i++)
            HostCheck.True(Same(scratch[i], default), "cleared scratch " + i);
        foreach (byte b in MemoryMarshal.AsBytes(scratch.AsSpan(end)))
            HostCheck.True(b == 0xA5, "untouched scratch tail");
    }

    private static void Rejected(BoundedExceptionSearchFrame[] frames, NativeAotEhClauseResult[] rows, ExceptionTypeMatchResult[] matches, Status status, SearchStatus search, BoundedExceptionDispatchStatus dispatch = default, int scratchLength = 9)
    {
        var scratch = Scratch(scratchLength);
        BoundedExceptionFrameTransfer transfer = default;
        new Span<byte>(&transfer, sizeof(BoundedExceptionFrameTransfer)).Fill(0xA5);
        var result = BoundedExceptionFrameDispatch.Select(frames, rows, matches, scratch, out transfer);
        HostCheck.True(!result.Succeeded && result.Status == status && result.SearchStatus == search && result.DispatchStatus == dispatch && result.CatchFrameIndex == 0 && result.CleanupFrameIndex == 0, "rejected result " + result.Status + "/" + result.SearchStatus + "/" + result.DispatchStatus);
        HostCheck.True(Same(transfer, default), "default rejected transfer");
        Cleared(scratch);
    }

    [HostTest("runtime.bounded-frame-dispatch.distinct-contexts-and-abi", HostLane.Fast, 0)]
    private static void DistinctContexts()
    {
        HostCheck.True(Marshal.SizeOf<BoundedExceptionFrameTransfer>() == 496 && sizeof(BoundedExceptionFrameTransfer) == 496 && Marshal.OffsetOf<BoundedExceptionFrameTransfer>(nameof(BoundedExceptionFrameTransfer.CatchHandler)).ToInt32() == 0 && Marshal.OffsetOf<BoundedExceptionFrameTransfer>(nameof(BoundedExceptionFrameTransfer.CleanupHandler)).ToInt32() == 8 && Marshal.OffsetOf<BoundedExceptionFrameTransfer>(nameof(BoundedExceptionFrameTransfer.CatchCaller)).ToInt32() == 16 && Marshal.OffsetOf<BoundedExceptionFrameTransfer>(nameof(BoundedExceptionFrameTransfer.CleanupCaller)).ToInt32() == 256 && !RuntimeHelpers.IsReferenceOrContainsReferences<BoundedExceptionFrameTransfer>() && !RuntimeHelpers.IsReferenceOrContainsReferences<BoundedExceptionFrameDispatchResult>(), "independent flat POD layout");
        using Fixture f = new();
        var cleanup = f.Rows(0, new Row(20, 6, 1, 0x100));
        var typed = f.Rows(1, new Row(20, 26, 0, 0x400, 0x1440));
        var rows = cleanup.Concat(typed).ToArray();
        BoundedExceptionSearchFrame[] frames = [f.Frame(0, 0, 1), f.Frame(1, 1, 1)];
        var scratch = Scratch();
        var result = BoundedExceptionFrameDispatch.Select(frames, rows, f.Matches(rows), scratch, out var transfer);
        HostCheck.True(result.Succeeded && result.Status == Status.Success && result.SearchStatus == SearchStatus.Success && result.DispatchStatus == BoundedExceptionDispatchStatus.Success && result.CatchFrameIndex == 1 && result.CleanupFrameIndex == 0 && transfer.CatchHandler == typed[0].HandlerAddress && transfer.CleanupHandler == cleanup[0].HandlerAddress, "one earlier cleanup and outer typed catch");
        HostCheck.True(Same(transfer.CatchCaller, frames[1].Context) && Same(transfer.CleanupCaller, frames[0].Context) && !Same(transfer.CatchCaller, transfer.CleanupCaller), "entire separate integer/vector contexts preserved");
        HostCheck.True(scratch[0].FrameIndex == 0 && scratch[0].Handler == transfer.CleanupHandler && Same(scratch[0].Context, transfer.CleanupCaller), "original search cleanup retained");
        Cleared(scratch, 1);
    }

    [HostTest("runtime.bounded-frame-dispatch.no-eh-frames-and-physical-indices", HostLane.Fast, 1)]
    private static void NoEhFrames()
    {
        using Fixture f = new();
        var rows = f.Rows(1, new Row(20, 6, 1, 0x100)).Concat(f.Rows(3, new Row(20, 26, 0, 0x400, 0x1440))).ToArray();
        BoundedExceptionSearchFrame[] frames = [f.Frame(0, 0, 0), f.Frame(1, 0, 1), f.Frame(2, 1, 0), f.Frame(3, 1, 1)];
        var scratch = Scratch(8);
        var result = BoundedExceptionFrameDispatch.Select(frames, rows, f.Matches(rows), scratch, out var transfer);
        HostCheck.True(result.Succeeded && result.CleanupFrameIndex == 1 && result.CatchFrameIndex == 3 && Same(transfer.CleanupCaller, frames[1].Context) && Same(transfer.CatchCaller, frames[3].Context), "no-EH frames preserve physical ownership and progressing contexts");
        Cleared(scratch, 1);
    }

    [HostTest("runtime.bounded-frame-dispatch.complete-tail-validation", HostLane.Fast, 2)]
    private static void InvalidTail()
    {
        using Fixture f = new();
        var rows = f.Rows(0, new Row(20, 6, 1, 0x100)).Concat(f.Rows(1, new Row(20, 26, 0, 0x400, 0x1440))).Concat(f.Rows(2, new Row(20, 26, 0, 0x400, 0x14C0))).ToArray();
        BoundedExceptionSearchFrame[] frames = [f.Frame(0, 0, 1), f.Frame(1, 1, 1), f.Frame(2, 2, 1)];
        var matches = f.Matches(rows);
        var goodScratch = Scratch();
        HostCheck.True(BoundedExceptionFrameDispatch.Select(frames, rows, matches, goodScratch, out _).Succeeded, "valid tail admitted");
        matches[2] = default;
        Rejected(frames, rows, matches, Status.SearchRejected, SearchStatus.DispatchRejected, BoundedExceptionDispatchStatus.InvalidTypeMatch);
        matches[2] = f.Owner.Match(rows[2].TypeAddress, handle: f.Owner.Handle ^ 1);
        Rejected(frames, rows, matches, Status.SearchRejected, SearchStatus.DispatchRejected, BoundedExceptionDispatchStatus.InvalidTypeMatch);
        matches = f.Matches(rows);
        rows[2] = default;
        Rejected(frames, rows, matches, Status.SearchRejected, SearchStatus.DispatchRejected, BoundedExceptionDispatchStatus.InvalidClause);
        rows = rows[..2];
        matches = matches[..2];
        frames[2] = default;
        Rejected(frames, rows, matches, Status.SearchRejected, SearchStatus.InvalidFrame);
        frames[2] = f.Frame(2, 3, 0);
        Rejected(frames, rows, matches, Status.SearchRejected, SearchStatus.InvalidSlice);
    }

    [HostTest("runtime.bounded-frame-dispatch.unsupported-cleanup-shapes-clear-output", HostLane.Fast, 3)]
    private static void UnsupportedShapes()
    {
        using Fixture f = new();
        void Unsupported(BoundedExceptionSearchFrame[] frames, NativeAotEhClauseResult[] rows) => Rejected(frames, rows, f.Matches(rows), Status.UnsupportedPlan, SearchStatus.Success, BoundedExceptionDispatchStatus.Success);
        var catchRows = f.Rows(1, new Row(20, 26, 0, 0x400, 0x1440));
        Unsupported([f.Frame(0, 0, 0), f.Frame(1, 0, 1)], catchRows); // Zero cleanup.
        var same = f.Rows(1, new(20, 6, 1, 0x100), new(20, 26, 0, 0x400, 0x1440));
        Unsupported([f.Frame(0, 0, 0), f.Frame(1, 0, 2)], same); // Outer catch, same-frame cleanup.
        var inner = f.Rows(0, new(20, 6, 1, 0x100), new(20, 26, 0, 0x400, 0x1440));
        Unsupported([f.Frame(0, 0, 2)], inner); // Innermost catch and cleanup.
        var earlier = f.Rows(0, new(20, 6, 1, 0x100), new(20, 16, 1, 0x200));
        catchRows = f.Rows(1, new Row(20, 26, 0, 0x400, 0x1440));
        Unsupported([f.Frame(0, 0, 2), f.Frame(1, 2, 1)], earlier.Concat(catchRows).ToArray()); // Two earlier cleanups.
        var one = f.Rows(0, new Row(20, 6, 1, 0x100));
        same = f.Rows(1, new(20, 6, 1, 0x100), new(20, 26, 0, 0x400, 0x1440));
        Unsupported([f.Frame(0, 0, 1), f.Frame(1, 1, 2)], one.Concat(same).ToArray()); // Earlier plus selected-frame cleanup.
    }

    [HostTest("runtime.bounded-frame-dispatch.default-no-match-and-capacity", HostLane.Fast, 4)]
    private static void FailedSearch()
    {
        BoundedExceptionFrameDispatchResult empty = default;
        HostCheck.True(!empty.Succeeded && empty.Status == Status.Uninitialized && empty.SearchStatus == SearchStatus.Uninitialized && empty.DispatchStatus == BoundedExceptionDispatchStatus.Uninitialized && empty.CatchFrameIndex == 0 && empty.CleanupFrameIndex == 0, "default result has no authority");
        Rejected([], [], [], Status.SearchRejected, SearchStatus.Empty);
        using Fixture f = new();
        Rejected([f.Frame(0, 0, 0), f.Frame(1, 0, 0)], [], [], Status.SearchRejected, SearchStatus.NoMatchingCatch);
        var rows = f.Rows(0, new Row(20, 6, 1, 0x100)).Concat(f.Rows(1, new Row(20, 26, 0, 0x400, 0x14C0))).ToArray();
        BoundedExceptionSearchFrame[] frames = [f.Frame(0, 0, 1), f.Frame(1, 1, 1)];
        var matches = f.Matches(rows);
        Rejected(frames, rows, matches, Status.SearchRejected, SearchStatus.NoMatchingCatch);
        foreach (int capacity in new[]
        {
            0,
            7
        }

        )
            Rejected(frames, rows, matches, Status.SearchRejected, SearchStatus.OutputTooSmall, scratchLength: capacity);
        Rejected(frames, rows, [], Status.SearchRejected, SearchStatus.DispatchRejected, BoundedExceptionDispatchStatus.InvalidTypeMatch);
        Rejected(new BoundedExceptionSearchFrame[5], [], [], Status.SearchRejected, SearchStatus.CapacityExceeded);
    }

    [HostTest("runtime.bounded-frame-dispatch.no-allocation-or-input-mutation", HostLane.Fast, 5)]
    private static void NoAllocation()
    {
        using Fixture f = new();
        var rows = f.Rows(0, new Row(20, 6, 1, 0x100)).Concat(f.Rows(1, new Row(20, 26, 0, 0x400, 0x1440))).ToArray();
        var matches = f.Matches(rows);
        BoundedExceptionSearchFrame[] frames = [f.Frame(0, 0, 1), f.Frame(1, 1, 1)];
        var originalFrames = frames.ToArray();
        var originalRows = rows.ToArray();
        var originalMatches = matches.ToArray();
        byte[] image = f.Owner.ImageSnapshot(), heap = f.Owner.HeapSnapshot();
        var scratch = Scratch();
        for (int i = 0; i < 64; i++)
            _ = BoundedExceptionFrameDispatch.Select(frames, rows, matches, scratch, out _);
        long before = GC.GetAllocatedBytesForCurrentThread();
        int successes = 0;
        for (int i = 0; i < 128; i++)
            if (BoundedExceptionFrameDispatch.Select(frames, rows, matches, scratch, out _).Succeeded)
                successes++;
        HostCheck.True(successes == 128 && GC.GetAllocatedBytesForCurrentThread() == before, "allocation-free admission");
        HostCheck.True(frames.SequenceEqual(originalFrames) && rows.SequenceEqual(originalRows) && matches.SequenceEqual(originalMatches), "caller inputs unchanged");
        f.Owner.Unchanged(image, heap);
        Cleared(scratch, 1);
    }

    private static void W32(byte* p, uint value)
    {
        for (int i = 0; i < 4; i++)
            p[i] = (byte)(value >> (i * 8));
    }
}
