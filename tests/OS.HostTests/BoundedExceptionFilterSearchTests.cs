using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Kernel.Runtime;
using Row = NativeAotEhClauseMetadataTests.Row;
using Status = OS.Kernel.Runtime.BoundedExceptionFilterSearchStatus;

internal static unsafe class BoundedExceptionFilterSearchTests
{
    private static void Failed(BoundedExceptionFilterSearchResult result, Status status) => HostCheck.True(
        result.Status == status && !result.Succeeded && !result.RequiresFilter && result.ClauseIndex == 0 &&
        result.FilterAddress == 0 && result.HandlerAddress == 0, "atomic filter-search failure " + status);

    private static BoundedExceptionFilterReceipt Receipt(NativeAotEhClauseResult row, int value) =>
        new(row.Index, row.FilterAddress, value);

    [HostTest("runtime.bounded-filter-search.ordered-prefix", HostLane.Fast, 0)]
    private static void OrderedPrefix()
    {
        using Fixture fixture = new();
        var rows = fixture.Rows(new(20, 16, 2, 0x100, 0x200), new(20, 16, 2, 0x300, 0x400));
        BoundedExceptionFilterReceipt[] receipts = new BoundedExceptionFilterReceipt[2];
        var result = BoundedExceptionFilterSearch.Select(rows, receipts, 25);
        HostCheck.True(result.RequiresFilter && !result.Succeeded && result.ClauseIndex == 0 &&
            result.FilterAddress == fixture.Owner.Address(0x6200) && result.HandlerAddress == 0);
        receipts[0] = Receipt(rows[0], 0);
        result = BoundedExceptionFilterSearch.Select(rows, receipts, 25);
        HostCheck.True(result.RequiresFilter && result.ClauseIndex == 1 &&
            result.FilterAddress == fixture.Owner.Address(0x6400) && result.HandlerAddress == 0);
        foreach (int value in new[] { 1, -1, int.MinValue, int.MaxValue })
        {
            receipts[1] = Receipt(rows[1], value);
            result = BoundedExceptionFilterSearch.Select(rows, receipts, 25);
            HostCheck.True(result.Succeeded && !result.RequiresFilter && result.ClauseIndex == 1 &&
                result.HandlerAddress == fixture.Owner.Address(0x6300) && result.FilterAddress == 0,
                "every signed nonzero result is true");
        }
        receipts[0] = Receipt(rows[0], -1); receipts[1] = default;
        result = BoundedExceptionFilterSearch.Select(rows, receipts, 25);
        HostCheck.True(result.Succeeded && result.ClauseIndex == 0 && result.HandlerAddress == rows[0].HandlerAddress &&
            result.FilterAddress == 0, "first true row wins with untouched pending tail");
    }

    [HostTest("runtime.bounded-filter-search.half-open-and-all-false", HostLane.Fast, 1)]
    private static void HalfOpen()
    {
        using Fixture fixture = new(); var rows = fixture.Four();
        var receipts = rows.Select(row => Receipt(row, 0)).ToArray();
        Failed(BoundedExceptionFilterSearch.Select(rows, receipts, 25), Status.NoMatch);
        receipts[3] = Receipt(rows[3], -3);
        foreach (uint offset in new uint[] { 20, 35 })
        {
            var result = BoundedExceptionFilterSearch.Select(rows, receipts, offset);
            HostCheck.True(result.Succeeded && result.ClauseIndex == 3 && result.HandlerAddress == rows[3].HandlerAddress,
                "four-row bound and half-open active range");
        }
        foreach (uint offset in new uint[] { 0, 19, 36, uint.MaxValue })
        {
            Failed(BoundedExceptionFilterSearch.Select(rows, receipts, offset), Status.NoMatch);
            Failed(BoundedExceptionFilterSearch.Select(rows, new BoundedExceptionFilterReceipt[4], offset), Status.NoMatch);
        }
        receipts[0] = default;
        Failed(BoundedExceptionFilterSearch.Select(rows, receipts, 36), Status.InvalidReceipt);
    }

    [HostTest("runtime.bounded-filter-search.invalid-and-late-receipts", HostLane.Fast, 2)]
    private static void Receipts()
    {
        using Fixture fixture = new();
        var rows = fixture.Rows(new(20, 16, 2, 0x100, 0x200), new(20, 16, 2, 0x300, 0x400));
        Failed(BoundedExceptionFilterSearch.Select(rows, new BoundedExceptionFilterReceipt[1], 25), Status.InvalidReceipt);
        Failed(BoundedExceptionFilterSearch.Select(rows, new BoundedExceptionFilterReceipt[3], 25), Status.InvalidReceipt);
        foreach (var wrong in new[] {
            new BoundedExceptionFilterReceipt(1, rows[0].FilterAddress, 0),
            new BoundedExceptionFilterReceipt(uint.MaxValue, rows[0].FilterAddress, 1),
            new BoundedExceptionFilterReceipt(0, 0, 0),
            new BoundedExceptionFilterReceipt(0, rows[0].HandlerAddress, 0),
            new BoundedExceptionFilterReceipt(0, rows[1].FilterAddress, -1),
            new BoundedExceptionFilterReceipt(0, rows[0].FilterAddress + 1, 1) })
            Failed(BoundedExceptionFilterSearch.Select(rows, [wrong, default], 25), Status.InvalidReceipt);
        foreach (int first in new[] { 1, -1 })
            foreach (int late in new[] { 0, 1, -1 })
                Failed(BoundedExceptionFilterSearch.Select(rows, [Receipt(rows[0], first), Receipt(rows[1], late)], 25), Status.InvalidReceipt);
        foreach (int late in new[] { 0, 1, -1 })
            Failed(BoundedExceptionFilterSearch.Select(rows, [default, Receipt(rows[1], late)], 25), Status.InvalidReceipt);
        Failed(BoundedExceptionFilterSearch.Select(rows, [Receipt(rows[0], 0), Receipt(rows[0], 0)], 25), Status.InvalidReceipt);
        var absentWithPayload = Changed(Receipt(rows[0], 1), nameof(BoundedExceptionFilterReceipt.HasValue), false);
        Failed(BoundedExceptionFilterSearch.Select(rows, [absentWithPayload, default], 25), Status.InvalidReceipt);
    }

    [HostTest("runtime.bounded-filter-search.complete-tail-validation", HostLane.Fast, 3)]
    private static void CompleteTail()
    {
        using Fixture fixture = new();
        var rows = fixture.Rows(new(20, 16, 2, 0x100, 0x200), new(20, 16, 2, 0x300, 0x400));
        BoundedExceptionFilterReceipt[] receipts = [Receipt(rows[0], 1), default];
        var validTail = rows[1];
        foreach (var invalid in new[] {
            default(NativeAotEhClauseResult), rows[0],
            Changed(validTail, nameof(NativeAotEhClauseResult.Count), 1U),
            Changed(validTail, nameof(NativeAotEhClauseResult.Index), uint.MaxValue),
            Changed(validTail, nameof(NativeAotEhClauseResult.HandlerAddress), 0UL),
            Changed(validTail, nameof(NativeAotEhClauseResult.FilterAddress), 0UL),
            Changed(validTail, nameof(NativeAotEhClauseResult.TypeAddress), fixture.Owner.Root),
            Changed(validTail, nameof(NativeAotEhClauseResult.TryEndOffset), 20U),
            Changed(validTail, nameof(NativeAotEhClauseResult.TryEndOffset), 19U) })
        {
            rows[1] = invalid;
            Failed(BoundedExceptionFilterSearch.Select(rows, receipts, 25), Status.InvalidClause);
        }
        rows[1] = Changed(validTail, nameof(NativeAotEhClauseResult.Kind), (NativeAotEhClauseKind)255);
        Failed(BoundedExceptionFilterSearch.Select(rows, receipts, 25), Status.UnsupportedClause);
        rows[1] = Changed(validTail, nameof(NativeAotEhClauseResult.IsTryGroupBoundary), true);
        Failed(BoundedExceptionFilterSearch.Select(rows, receipts, 25), Status.UnsupportedClause);
    }

    [HostTest("runtime.bounded-filter-search.unsupported-topology", HostLane.Fast, 4)]
    private static void Topology()
    {
        using Fixture fixture = new();
        foreach (var rows in new[] {
            fixture.Rows(new Row(20, 16, 0, 0x100, 0x1440)),
            fixture.Rows(new Row(20, 16, 1, 0x100)),
            fixture.Rows(new(20, 16, 2, 0x100, 0x200), new(20, 16, 0, 0x300, 0x1440)),
            fixture.Rows(new(20, 16, 2, 0x100, 0x200), new(20, 16, 1, 0x300)) })
            Failed(BoundedExceptionFilterSearch.Select(rows, new BoundedExceptionFilterReceipt[rows.Length], 25), Status.UnsupportedClause);
        var marker = fixture.Rows(new Row(0, 0, 1, 0));
        Failed(BoundedExceptionFilterSearch.Select(marker, [default], 25), Status.UnsupportedClause);
        foreach (var second in new[] {
            new Row(20, 8, 2, 0x300, 0x400), new Row(10, 36, 2, 0x300, 0x400),
            new Row(25, 16, 2, 0x300, 0x400), new Row(40, 16, 2, 0x300, 0x400) })
        {
            var rows = fixture.Rows(new Row(20, 16, 2, 0x100, 0x200), second);
            Failed(BoundedExceptionFilterSearch.Select(rows, [Receipt(rows[0], 1), default], 25), Status.InvalidTopology);
        }
    }

    [HostTest("runtime.bounded-filter-search.capacity-and-default", HostLane.Fast, 5)]
    private static void Capacity()
    {
        Failed(default, Status.Uninitialized);
        Failed(BoundedExceptionFilterSearch.Select([], [], 0), Status.Empty);
        Failed(BoundedExceptionFilterSearch.Select(new NativeAotEhClauseResult[5], new BoundedExceptionFilterReceipt[5], 0), Status.CapacityExceeded);
        Failed(BoundedExceptionFilterSearch.Select(new NativeAotEhClauseResult[1], new BoundedExceptionFilterReceipt[1], 0), Status.InvalidClause);
        using Fixture fixture = new(); var two = fixture.Rows(new(20, 16, 2, 0x100, 0x200), new(20, 16, 2, 0x300, 0x400));
        Failed(BoundedExceptionFilterSearch.Select(two.AsSpan(0, 1), [default], 25), Status.InvalidClause);
    }

    [HostTest("runtime.bounded-filter-search.no-allocation-or-input-mutation", HostLane.Fast, 6)]
    private static void NoAllocation()
    {
        using Fixture fixture = new(); var rows = fixture.Four();
        var receipts = rows.Select(row => Receipt(row, row.Index == 3 ? -1 : 0)).ToArray();
        var beforeRows = rows.ToArray(); var beforeReceipts = receipts.ToArray();
        byte[] image = fixture.Owner.ImageSnapshot(), heap = fixture.Owner.HeapSnapshot();
        byte[] snapshot = new ReadOnlySpan<byte>(fixture.Snapshot, 0x4000).ToArray();
        HostCheck.True(!RuntimeHelpers.IsReferenceOrContainsReferences<BoundedExceptionFilterReceipt>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<BoundedExceptionFilterSearchResult>(), "POD receipt/result");
        for (int i = 0; i < 128; i++) _ = BoundedExceptionFilterSearch.Select(rows, receipts, 25);
        long before = GC.GetAllocatedBytesForCurrentThread(); int successes = 0;
        for (int i = 0; i < 128; i++) if (BoundedExceptionFilterSearch.Select(rows, receipts, 25).Succeeded) successes++;
        long after = GC.GetAllocatedBytesForCurrentThread();
        HostCheck.True(after == before && successes == 128, "warmed zero allocations");
        HostCheck.True(rows.SequenceEqual(beforeRows) && receipts.SequenceEqual(beforeReceipts), "rows and receipts unchanged");
        HostCheck.True(new ReadOnlySpan<byte>(fixture.Snapshot, 0x4000).SequenceEqual(snapshot), "unwind snapshot unchanged");
        fixture.Owner.Unchanged(image, heap);
    }

    // Host-only malformed POD controls. Positive rows always come from the real metadata reader.
    private static T Changed<T>(T value, string property, object changed) where T : struct
    {
        object boxed = value;
        typeof(T).GetField("<" + property + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(boxed, changed);
        return (T)boxed;
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly ExceptionTypeMatchTests.Fixture Owner = new(PrepareImage);
        internal readonly byte* Snapshot = (byte*)NativeMemory.AllocZeroed(0x4000);
        internal Fixture()
        {
            HostCheck.True(Snapshot != null, "snapshot allocation");
            var bound = X64PERuntimeFunctionUnwindMetadataHostileInput.Bind(Owner.Capture, Owner.Mapping,
                Owner.Owner, 4096, Owner.Handle, Snapshot, 0x4000);
            HostCheck.True(bound.Succeeded, "original image unwind binding " + bound.Status);
        }
        private static void PrepareImage(nint address)
        {
            byte* image = (byte*)address;
            W32(image + 0x104, 16); W32(image + 0x120, 0x1800); W32(image + 0x124, 108);
            for (int i = 0; i < 9; i++)
            {
                uint begin = 0x6000U + (uint)i * 0x100, unwind = 0x1900U + (uint)i * 0x20;
                W32(image + 0x1800 + i * 12, begin); W32(image + 0x1804 + i * 12, begin + 0x80);
                W32(image + 0x1808 + i * 12, unwind); image[unwind] = (byte)(i == 0 ? 9 : 1);
                if (i != 0) image[unwind + 4] = 1;
            }
            W32(image + 0x1904, 0x6500); image[0x1908] = 4; W32(image + 0x1909, 0x1B00);
        }
        internal NativeAotEhClauseResult[] Four() => Rows(
            new(20, 16, 2, 0x100, 0x200), new(20, 16, 2, 0x300, 0x400),
            new(20, 16, 2, 0x500, 0x600), new(20, 16, 2, 0x700, 0x800));
        internal NativeAotEhClauseResult[] Rows(params Row[] rows)
        {
            for (int i = 1; i < 9; i++) Owner.Image[0x1904 + i * 0x20] = 1;
            foreach (var row in rows) if (row.Kind == 2) Owner.Image[0x1904 + (row.Extra / 0x100) * 0x20] = 2;
            byte[] blob = NativeAotEhClauseMetadataTests.Table(rows);
            new Span<byte>(Owner.Image + 0x1B00, 0x100).Clear(); blob.CopyTo(new Span<byte>(Owner.Image + 0x1B00, blob.Length));
            var result = new NativeAotEhClauseResult[rows.Length];
            for (uint i = 0; i < result.Length; i++)
            {
                result[i] = NativeAotEhClauseMetadata.ReadClause(NativeAotLayoutProfile.Ilc10_0_12WinX64LittleEndian,
                    Owner.Capture, Owner.Mapping, Owner.Owner, 4096, Owner.Handle, Snapshot, 0x4000,
                    new(0x6000, 0x900, 0x1B00, (uint)blob.Length), i);
                HostCheck.True(result[i].Succeeded, "decoded physical filter fixture row " + i + ": " + result[i].Status);
            }
            return result;
        }
        public void Dispose() { NativeMemory.Free(Snapshot); Owner.Dispose(); }
    }
    private static void W32(byte* pointer, uint value)
    { for (int i = 0; i < 4; i++) pointer[i] = (byte)(value >> (i * 8)); }
}
