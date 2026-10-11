using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Kernel.Runtime;
using Row = NativeAotEhClauseMetadataTests.Row;
using Status = OS.Kernel.Runtime.BoundedExceptionDispatchStatus;

internal static unsafe class BoundedExceptionDispatchPlanTests
{
    private static void Need(bool value, string why) { if (!value) throw new InvalidOperationException(why); }
    private static void Failed(BoundedExceptionDispatchResult r, Status status) => Need(
        r.Status == status && !r.Succeeded && r.CatchIndex == 0 && r.CatchHandler == 0 &&
        r.CleanupCount == 0 && r.FirstCleanup == 0 && r.SecondCleanup == 0, "atomic failure " + status + ": " + r.Status);

    [HostTest("runtime.bounded-eh-plan.search-order")]
    private static void SearchOrder()
    {
        using Fixture f = new();
        var rows = f.Rows(new(20, 6, 1, 0x100), new(20, 16, 1, 0x200),
            new(20, 26, 0, 0x300, 0x14C0), new(20, 26, 0, 0x400, 0x1440));
        var matches = f.Matches(rows);
        Need(matches[2].Succeeded && !matches[2].Matches && matches[3].Succeeded && matches[3].Matches,
            "real same-image sibling nonmatch and retained-object base match");
        var r = BoundedExceptionDispatchPlan.Select(rows, matches, 25);
        Need(r.Succeeded && r.CatchIndex == 3 && r.CatchHandler == f.Owner.Address(0x6400) &&
            r.CleanupCount == 2 && r.FirstCleanup == f.Owner.Address(0x6100) &&
            r.SecondCleanup == f.Owner.Address(0x6200), "inner-to-outer cleanup then later matching sibling");
        var same = f.Rows(new(20, 26, 0, 0x300, 0x1440), new(20, 26, 0, 0x400, 0x1400));
        r = BoundedExceptionDispatchPlan.Select(same, f.Matches(same), 25);
        Need(r.Succeeded && r.CatchIndex == 0 && r.CleanupCount == 0 &&
            r.FirstCleanup == 0 && r.SecondCleanup == 0, "first matching typed sibling wins");
    }

    [HostTest("runtime.bounded-eh-plan.half-open-and-enclosing-cleanup")]
    private static void HalfOpen()
    {
        using Fixture f = new();
        var rows = f.Rows(new(20, 6, 1, 0x100), new(20, 16, 0, 0x300, 0x1440), new(10, 36, 1, 0x200));
        var matches = f.Matches(rows);
        var r = BoundedExceptionDispatchPlan.Select(rows, matches, 20);
        Need(r.Succeeded && r.CatchIndex == 1 && r.CleanupCount == 1, "closed try start");
        r = BoundedExceptionDispatchPlan.Select(rows, matches, 26);
        Need(r.Succeeded && r.CatchIndex == 1 && r.CleanupCount == 0 && r.FirstCleanup == 0,
            "open cleanup end and enclosing cleanup omitted");
        Failed(BoundedExceptionDispatchPlan.Select(rows, matches, 36), Status.NoMatchingCatch);
        Failed(BoundedExceptionDispatchPlan.Select(rows, matches, 19), Status.NoMatchingCatch);
    }

    [HostTest("runtime.bounded-eh-plan.nested-typed-catches")]
    private static void NestedTyped()
    {
        using Fixture f = new();
        var rows = f.Rows(new(20, 6, 0, 0x300, 0x14C0), new(20, 16, 1, 0x100), new(20, 26, 0, 0x400, 0x1440));
        var r = BoundedExceptionDispatchPlan.Select(rows, f.Matches(rows), 25);
        Need(r.Succeeded && r.CatchIndex == 2 && r.CleanupCount == 1 && r.FirstCleanup == f.Owner.Address(0x6100),
            "inner nonmatch falls through to outer catch with intervening cleanup");
        rows = f.Rows(new(20, 6, 0, 0x300, 0x1440), new(20, 16, 1, 0x100), new(20, 26, 0, 0x400, 0x1400));
        r = BoundedExceptionDispatchPlan.Select(rows, f.Matches(rows), 25);
        Need(r.Succeeded && r.CatchIndex == 0 && r.CleanupCount == 0 && r.FirstCleanup == 0,
            "inner match wins before enclosing cleanup");
    }

    [HostTest("runtime.bounded-eh-plan.disjoint-tail")]
    private static void Disjoint()
    {
        using Fixture f = new();
        var rows = f.Rows(new(0, 10, 0, 0x300, 0x1440), new(20, 26, 0, 0x400, 0x1400));
        var matches = f.Matches(rows);
        var r = BoundedExceptionDispatchPlan.Select(rows, matches, 25);
        Need(r.Succeeded && r.CatchIndex == 1 && r.CleanupCount == 0, "noncontaining matching typed catch skipped");
        matches[0] = f.Owner.Match(1);
        Failed(BoundedExceptionDispatchPlan.Select(rows, matches, 25), Status.InvalidTypeMatch);
    }

    [HostTest("runtime.bounded-eh-plan.invalid-tail-and-receipts")]
    private static void InvalidTail()
    {
        using Fixture f = new();
        var rows = f.Rows(new(20, 16, 0, 0x300, 0x1440), new(10, 36, 0, 0x400, 0x1400));
        var matches = f.Matches(rows);
        rows[1] = default;
        Failed(BoundedExceptionDispatchPlan.Select(rows, matches, 25), Status.InvalidClause);
        rows = f.Rows(new(20, 16, 0, 0x300, 0x1440), new(10, 36, 0, 0x400, 0x1400));
        matches[1] = default;
        Failed(BoundedExceptionDispatchPlan.Select(rows, matches, 25), Status.InvalidTypeMatch);
        matches = f.Matches(rows); matches[1] = f.Owner.Match(1);
        Failed(BoundedExceptionDispatchPlan.Select(rows, matches, 25), Status.InvalidTypeMatch);
        Failed(BoundedExceptionDispatchPlan.Select(rows, matches.AsSpan(0, 1), 25), Status.InvalidTypeMatch);
        rows[1] = rows[0];
        Failed(BoundedExceptionDispatchPlan.Select(rows, f.Matches(rows), 25), Status.InvalidClause);
    }

    [HostTest("runtime.bounded-eh-plan.unsupported-topology")]
    private static void Topology()
    {
        using Fixture f = new();
        foreach (var rows in new[] {
            f.Rows(new(20, 16, 1, 0x100), new(20, 6, 0, 0x300, 0x1440)),
            f.Rows(new(20, 16, 1, 0x100), new(25, 16, 0, 0x300, 0x1440)),
            f.Rows(new(20, 6, 1, 0x100), new(20, 6, 1, 0x200), new(20, 16, 0, 0x300, 0x1440)),
            f.Rows(new(20, 16, 1, 0x100), new(20, 16, 0, 0x300, 0x1440)) })
            Failed(BoundedExceptionDispatchPlan.Select(rows, f.Matches(rows), 25), Status.InvalidNesting);
        var filter = f.Rows(new Row(20, 16, 2, 0x100, 0x200));
        Failed(BoundedExceptionDispatchPlan.Select(filter, f.Matches(filter), 25), Status.UnsupportedClause);
        var marker = f.Rows(new Row(0, 0, 1, 0));
        Failed(BoundedExceptionDispatchPlan.Select(marker, f.Matches(marker), 25), Status.UnsupportedClause);
        var alias = f.Rows(new(20, 6, 1, 0x100), new(20, 16, 1, 0x100), new(20, 26, 0, 0x300, 0x1440));
        Failed(BoundedExceptionDispatchPlan.Select(alias, f.Matches(alias), 25), Status.InvalidClause);
    }

    [HostTest("runtime.bounded-eh-plan.capacity-and-default")]
    private static void Capacity()
    {
        Failed(default, Status.Uninitialized);
        Failed(BoundedExceptionDispatchPlan.Select([], [], 0), Status.Empty);
        Failed(BoundedExceptionDispatchPlan.Select(new NativeAotEhClauseResult[5], new ExceptionTypeMatchResult[5], 0), Status.CapacityExceeded);
        using Fixture f = new();
        var rows = f.Rows(new(20, 6, 1, 0x100), new(20, 16, 1, 0x200), new(20, 26, 1, 0x300), new(20, 36, 0, 0x400, 0x1440));
        Failed(BoundedExceptionDispatchPlan.Select(rows, f.Matches(rows), 25), Status.CapacityExceeded);
        var noMatch = f.Rows(new Row(20, 26, 0, 0x300, 0x14C0));
        Failed(BoundedExceptionDispatchPlan.Select(noMatch, f.Matches(noMatch), 25), Status.NoMatchingCatch);
    }

    [HostTest("runtime.bounded-eh-plan.no-allocation-or-mutation")]
    private static void NoAllocation()
    {
        using Fixture f = new();
        var rows = f.Rows(new(20, 6, 1, 0x100), new(20, 16, 1, 0x200), new(20, 26, 0, 0x300, 0x1440));
        var matches = f.Matches(rows);
        var beforeRows = rows.ToArray(); var beforeMatches = matches.ToArray();
        byte[] image = f.Owner.ImageSnapshot(), heap = f.Owner.HeapSnapshot();
        Need(!RuntimeHelpers.IsReferenceOrContainsReferences<BoundedExceptionDispatchResult>(), "POD result");
        for (int i = 0; i < 128; i++) _ = BoundedExceptionDispatchPlan.Select(rows, matches, 25);
        long before = GC.GetAllocatedBytesForCurrentThread(); int succeeded = 0;
        for (int i = 0; i < 128; i++) if (BoundedExceptionDispatchPlan.Select(rows, matches, 25).Succeeded) succeeded++;
        Need(GC.GetAllocatedBytesForCurrentThread() == before && succeeded == 128, "zero allocation");
        Need(rows.SequenceEqual(beforeRows) && matches.SequenceEqual(beforeMatches), "caller inputs unchanged");
        f.Owner.Unchanged(image, heap);
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
            Need(bound.Succeeded, "same-image unwind binding " + bound.Status);
        }
        private static void PrepareImage(nint address)
        {
            byte* image = (byte*)address;
            W32(image + 0x104, 16); W32(image + 0x120, 0x1800); W32(image + 0x124, 60);
            for (int i = 0; i < 5; i++)
            {
                uint begin = 0x6000U + (uint)i * 0x100, unwind = 0x1900U + (uint)i * 0x20;
                W32(image + 0x1800 + i * 12, begin); W32(image + 0x1804 + i * 12, begin + 0x80);
                W32(image + 0x1808 + i * 12, unwind); image[unwind] = (byte)(i == 0 ? 9 : 1);
                if (i != 0) image[unwind + 4] = 1;
            }
            W32(image + 0x1904, 0x6500); image[0x1908] = 4; W32(image + 0x1909, 0x1A00);
        }
        internal NativeAotEhClauseResult[] Rows(params Row[] rows)
        {
            for (int i = 1; i < 5; i++) Owner.Image[0x1904 + i * 0x20] = 1;
            foreach (var row in rows) if (row.Kind == 2)
                Owner.Image[0x1904 + (row.Extra / 0x100) * 0x20] = 2;
            byte[] blob = NativeAotEhClauseMetadataTests.Table(rows);
            new Span<byte>(Owner.Image + 0x1A00, 0x100).Clear(); blob.CopyTo(new Span<byte>(Owner.Image + 0x1A00, blob.Length));
            var result = new NativeAotEhClauseResult[rows.Length];
            for (uint i = 0; i < result.Length; i++)
            {
                result[i] = NativeAotEhClauseMetadata.ReadClause(NativeAotLayoutProfile.Ilc10_0_12WinX64LittleEndian,
                    Owner.Capture, Owner.Mapping, Owner.Owner, 4096, Owner.Handle, snapshot, 0x4000,
                    new(0x6000, 0x500, 0x1A00, (uint)blob.Length), i);
                Need(result[i].Succeeded, "decoded original row " + i + ": " + result[i].Status);
            }
            return result;
        }
        internal ExceptionTypeMatchResult[] Matches(NativeAotEhClauseResult[] rows)
        {
            var matches = new ExceptionTypeMatchResult[rows.Length];
            for (int i = 0; i < rows.Length; i++) if (rows[i].Kind == NativeAotEhClauseKind.Typed)
                matches[i] = Owner.Match(rows[i].TypeAddress);
            return matches;
        }
        public void Dispose() { NativeMemory.Free(snapshot); Owner.Dispose(); }
    }
    private static void W32(byte* p, uint value) { for (int i = 0; i < 4; i++) p[i] = (byte)(value >> (i * 8)); }
}
