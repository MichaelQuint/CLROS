using System.Runtime.InteropServices;
using OS.Kernel.Runtime;
using OS.Tooling;
using Row = NativeAotEhClauseMetadataTests.Row;
using Status = OS.Kernel.Runtime.NativeAotNormalFinallyStatus;
using PlanStatus = OS.Kernel.Runtime.BoundedExceptionDispatchStatus;
using PhysicalFixture = BoundedExceptionCallerWindowTests.Fixture;
using PlanFixture = BoundedExceptionDispatchPlanTests.Fixture;

internal static unsafe class BoundedNormalFinallyTests
{
    private const NativeAotLayoutProfile Profile = NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian;
    private static void Need(bool value, string reason) => HostCheck.True(value, reason);
    private static bool Same<T>(T left, T right) where T : unmanaged =>
        new ReadOnlySpan<byte>(&left, sizeof(T)).SequenceEqual(new ReadOnlySpan<byte>(&right, sizeof(T)));
    private static void Failed(NativeAotNormalFinallyResult result, Status status) => Need(
        result.Status == status && !result.Succeeded && Same(result.Parent, default(X64UnwindContext)) &&
        result.Bounds.RootMethodRva == 0 && result.Bounds.CodeBytes == 0 && result.Bounds.EhRva == 0 &&
        result.Bounds.EhBytes == 0 && result.ProtectedOffset == 0 && result.ExitedClauseIndex == 0,
        "atomic normal-finally failure " + result.Status + " expected " + status);
    private static void PlanFailed(BoundedExceptionDispatchResult result, PlanStatus status) => Need(
        result.Status == status && !result.Succeeded && result.CatchIndex == 0 && result.CatchHandler == 0 &&
        result.CleanupCount == 0 && result.FirstCleanup == 0 && result.SecondCleanup == 0,
        "atomic exited-finally selection failure " + result.Status + " expected " + status);

    [HostTest("runtime.normal-finally.parent-gp-xmm-return-pc", HostLane.Fast, 0)]
    private static void RestoreParent()
    {
        using Fixture f = new();
        byte[][] before = f.Original.InputBytes();
        var result = f.Recover();
        Need(result.Succeeded && Same(result.Parent, f.Expected) && result.Bounds.RootMethodRva == 0x6100 &&
            result.Bounds.CodeBytes == 0x80 && result.Bounds.EhRva == 0x1600 && result.Bounds.EhBytes == 5 &&
            result.ProtectedOffset == 0x1f && result.ExitedClauseIndex == 0,
            "own funclet unwind restores every GP/XMM register and same-root parent; RIP preserved, RIP-1 selects call byte");
        Need(result.Parent.Rsp == f.Current.Rsp + 232 && result.Parent.Rip == f.Original.Address(0x6120), "real progressing parent frame");
        f.Original.Unchanged(before);
        _ = f.Recover(); long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 16; i++) if (!f.Recover().Succeeded) throw new InvalidOperationException("stable normal-finally recovery");
        Need(GC.GetAllocatedBytesForCurrentThread() == allocated, "recovery is allocation-free");
        f.Original.Unchanged(before);
    }

    [HostTest("runtime.normal-finally.funclet-and-parent-rejections", HostLane.Fast, 1)]
    private static void RejectShapes()
    {
        foreach (byte flags in new byte[] { 0, 2, 17, 9, 33 })
        {
            using Fixture f = new(); f.Original.Image[0x12c0] = flags;
            Failed(f.Recover(), Status.FuncletRejected);
        }
        using (Fixture f = new())
        {
            // Plain Windows unwind is mandatory even if NativeAOT metadata still names a handler.
            f.Original.Image[0x1280] = 9; W32(f.Original.Image + 0x12c0, 0x6f00); f.Original.Image[0x12c4] = 1; f.Rebind();
            Failed(f.Recover(), Status.FuncletRejected);
        }
        foreach (uint pc in new uint[] { 0x6020, 0x6140, 0x6180 })
        {
            using Fixture f = new(); var context = f.Current; context.Rip = f.Original.Address(pc);
            Failed(f.Recover(context), Status.FuncletRejected);
        }
        foreach ((uint pc, Status status) in new[] { (0x6150U, Status.ParentRejected), (0x6250U, Status.ParentRejected),
            (0x6100U, Status.ParentRejected), (0x6020U, Status.CatalogRejected), (0x6220U, Status.ClauseRejected) })
        {
            using Fixture f = new(); W64((byte*)f.Current.Rsp + 224, f.Original.Address(pc));
            byte[][] before = f.Original.InputBytes(); Failed(f.Recover(), status); f.Original.Unchanged(before);
        }
        using (Fixture f = new())
        {
            f.Table(new(0, 16, 1, 0x40), new(0, 32, 1, 0x40));
            Failed(f.Recover(), Status.AmbiguousClause);
        }
        using (Fixture f = new())
        {
            f.Table(new Row(0, 32, 0, 0x40, 0x1800));
            Failed(f.Recover(), Status.ClauseRejected);
        }
    }

    [HostTest("runtime.normal-finally.original-authority-and-late-failure", HostLane.Fast, 2)]
    private static void RejectAuthority()
    {
        using Fixture f = new(); using Fixture other = new();
        Failed(f.Recover(profile: default), Status.UnsupportedProfile);
        Failed(f.Recover(handle: f.Original.Handle + 8), Status.ModuleRejected);
        Failed(f.Recover(owner: other.Original.Owner), Status.ModuleRejected);
        Failed(f.Recover(snapshot: other.Original.Snapshot), Status.FuncletRejected);
        Failed(f.Recover(stackBytes: 0x1e8), Status.UnwindRejected);
        var bad = f.Current; bad.Rsp++; Failed(f.Recover(bad), Status.UnwindRejected);
        bad = f.Current; bad.Rsp = ulong.MaxValue - 7; Failed(f.Recover(bad), Status.UnwindRejected);
        W64((byte*)f.Current.Rsp + 224, f.Original.Address(0x1700));
        Failed(f.Recover(), Status.UnwindRejected); W64((byte*)f.Current.Rsp + 224, f.Expected.Rip);
        W32(f.Original.Image + 0x192c, 10); // Invalid final catalog contribution, after the selected root.
        byte[][] before = f.Original.InputBytes(); Failed(f.Recover(), Status.CatalogRejected); f.Original.Unchanged(before);
        W32(f.Original.Image + 0x192c, 9);
        f.Original.Image[0x1704] = 0; // Malformed final stream cannot be hidden by an earlier match.
        before = f.Original.InputBytes(); Failed(f.Recover(), Status.CatalogRejected); f.Original.Unchanged(before);
        f.Original.Image[0x1704] = 128;
        f.Original.Image[0x1285] ^= 1; // Changed original unwind codes invalidate the retained snapshot.
        Failed(f.Recover(), Status.FuncletRejected);
        Need(!default(NativeAotNormalFinallyResult).Succeeded, "default result grants no success");
    }

    [HostTest("runtime.normal-finally.exact-later-handler-index", HostLane.Fast, 5)]
    private static void LaterHandlerIndex()
    {
        using Fixture f = new(); byte* image = f.Original.Image;
        // Reuse the unneeded no-EH row for a sibling; keep the registered directory extent intact.
        new ReadOnlySpan<byte>(image + 0x110c, 24).CopyTo(new Span<byte>(image + 0x1100, 24));
        W32(image + 0x1110, 0x6160); W32(image + 0x1118, 0x6160); W32(image + 0x111c, 0x6180);
        W32(image + 0x1120, 0x1400); image[0x1400] = 1; image[0x1404] = 1;
        f.Rebind(); f.Table(new(0, 16, 1, 0x60), new(0, 32, 1, 0x40));
        byte[][] before = f.Original.InputBytes(); var result = f.Recover();
        Need(result.Succeeded && result.ExitedClauseIndex == 1 && Same(result.Parent, f.Expected),
            "physical handler begin selects the exact later clause; no index or root is guessed");
        f.Original.Unchanged(before);
    }

    [HostTest("runtime.normal-finally.skip-search-and-cleanup", HostLane.Fast, 3)]
    private static void SkipBothPasses()
    {
        using PlanFixture f = new();
        var rows = f.Rows(new(20, 6, 0, 0x100, 0x1440), new(20, 16, 1, 0x200),
            new(20, 26, 1, 0x300), new(20, 36, 0, 0x400, 0x1440));
        var matches = f.Matches(rows);
        var ordinary = BoundedExceptionDispatchPlan.Select(rows, matches, 25);
        Need(ordinary.Succeeded && ordinary.CatchIndex == 0 && ordinary.CleanupCount == 0, "ordinary first match remains unchanged");
        var result = BoundedExceptionDispatchPlan.SelectAfterExitedFinally(rows, matches, 25, 1);
        Need(result.Succeeded && result.CatchIndex == 3 && result.CatchHandler == rows[3].HandlerAddress &&
            result.CleanupCount == 1 && result.FirstCleanup == rows[2].HandlerAddress && result.SecondCleanup == 0,
            "skip earlier matching catch and exited cleanup; preserve later cleanup and original catch index");
        result = BoundedExceptionDispatchPlan.SelectAfterExitedFinally(rows, matches, 25, 2);
        Need(result.Succeeded && result.CatchIndex == 3 && result.CleanupCount == 0 && result.FirstCleanup == 0,
            "second exited cleanup and every earlier clause omitted in both passes");
        result = BoundedExceptionDispatchPlan.SelectAfterExitedFinally(rows, matches, 46, 1);
        Need(result.Succeeded && result.CatchIndex == 3 && result.CleanupCount == 0, "half-open later cleanup end");
        PlanFailed(BoundedExceptionDispatchPlan.SelectAfterExitedFinally(rows, matches, 56, 1), PlanStatus.NoMatchingCatch);
        for (int i = 0; i < 16; i++) _ = BoundedExceptionDispatchPlan.SelectAfterExitedFinally(rows, matches, 25, 1);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 16; i++) if (!BoundedExceptionDispatchPlan.SelectAfterExitedFinally(rows, matches, 25, 1).Succeeded)
            throw new InvalidOperationException("stable exited-finally selection");
        Need(GC.GetAllocatedBytesForCurrentThread() == allocated, "exited-finally selection is allocation-free");
    }

    [HostTest("runtime.normal-finally.skipped-and-tail-validation", HostLane.Fast, 4)]
    private static void ValidateSkippedAndTail()
    {
        using PlanFixture f = new();
        var rows = f.Rows(new(20, 6, 0, 0x100, 0x1440), new(20, 16, 1, 0x200),
            new(20, 26, 0, 0x300, 0x1440), new(10, 56, 0, 0x400, 0x1400));
        var matches = f.Matches(rows);
        foreach (uint index in new uint[] { 0, 2, 3, 4, uint.MaxValue })
            PlanFailed(BoundedExceptionDispatchPlan.SelectAfterExitedFinally(rows, matches, 25, index), PlanStatus.InvalidStart);
        var saved = rows[0]; rows[0] = default;
        PlanFailed(BoundedExceptionDispatchPlan.SelectAfterExitedFinally(rows, matches, 25, 1), PlanStatus.InvalidClause);
        rows[0] = saved;
        var savedMatch = matches[0]; matches[0] = default;
        PlanFailed(BoundedExceptionDispatchPlan.SelectAfterExitedFinally(rows, matches, 25, 1), PlanStatus.InvalidTypeMatch);
        matches[0] = savedMatch;
        rows[3] = default;
        PlanFailed(BoundedExceptionDispatchPlan.SelectAfterExitedFinally(rows, matches, 25, 1), PlanStatus.InvalidClause);
        rows = f.Rows(new(20, 6, 0, 0x100, 0x1440), new(10, 36, 1, 0x200), new(20, 26, 0, 0x300, 0x1440));
        PlanFailed(BoundedExceptionDispatchPlan.SelectAfterExitedFinally(rows, f.Matches(rows), 25, 1), PlanStatus.InvalidNesting);
    }

    [HostTest("runtime.normal-finally.exact-source-admission-controls", HostLane.Fast, 6)]
    private static void SourceAdmissionControls()
    {
        const string include = @"..\OS.Kernel\Runtime\BoundedExceptionDispatchPlan.cs";
        string source = File.ReadAllText(Path.Combine(HostToolProcess.FindRepositoryRoot(), "src/OS.Kernel/Runtime/BoundedExceptionDispatchPlan.cs"));
        Need(EfiSourceAdmission.Inspect(source, include).Succeeded, "reviewed planner source admitted");
        string normalized = source.Replace("\r\n", "\n", StringComparison.Ordinal);
        Need(EfiSourceAdmission.Inspect(normalized.Replace("\n", "\r\n", StringComparison.Ordinal), include).Succeeded,
            "semantic pin admits CRLF/LF equivalence; raw provenance stays independent");
        foreach (string anchor in new[] {
            "if (!row.ContainsOffset(protectedOffset) || (skipExited && (uint)i <= exitedClauseIndex)) continue;",
            "if (skipExited && (uint)i <= exitedClauseIndex) continue;",
            "if (!row.Succeeded || row.Index != (uint)i || row.Count != (uint)clauses.Length)" })
        {
            Need(source.Contains(anchor, StringComparison.Ordinal), "required negative-control anchor");
            var rejected = EfiSourceAdmission.Inspect(source.Replace(anchor, "if (false)", StringComparison.Ordinal), include);
            Need(!rejected.Succeeded && rejected.Reason == "forbidden_active_source" &&
                rejected.Detail.EndsWith(":reviewed_bounded_exception_search_identity", StringComparison.Ordinal),
                "search/cleanup skip or complete-row weakening stays rejected");
        }
        const string parentInclude = @"..\OS.Kernel\Runtime\NativeAotEhClauseMetadata.NormalFinally.cs";
        Need(EfiContracts.CompileItems.Count(item => item.StartsWith(parentInclude + "|", StringComparison.Ordinal)) == 1,
            "parent recovery has one explicit reviewed EFI include");
        string parentSource = File.ReadAllText(Path.Combine(HostToolProcess.FindRepositoryRoot(), "src/OS.Kernel/Runtime/NativeAotEhClauseMetadata.NormalFinally.cs"));
        Need(EfiSourceAdmission.Inspect(parentSource, parentInclude).Succeeded, "exact parent source admitted");
        foreach (string anchor in new[] { "funclet.FunctionIndex != callByte.FunctionIndex", "root.FunctionIndex != protectedByte.FunctionIndex", "if (row.Kind != NativeAotEhClauseKind.FaultOrFinally || row.IsTryGroupBoundary)" })
        {
            Need(parentSource.Contains(anchor, StringComparison.Ordinal), "parent source control anchor");
            var rejected = EfiSourceAdmission.Inspect(parentSource.Replace(anchor, "if (false)", StringComparison.Ordinal), parentInclude);
            Need(!rejected.Succeeded && rejected.Detail.EndsWith(":reviewed_normal_finally_parent_source_identity", StringComparison.Ordinal),
                "parent identity and clause weakening rejected");
        }
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly PhysicalFixture Original = new();
        internal readonly X64UnwindContext Current, Expected;
        internal Fixture()
        {
            // Move the later root's unwind record, leaving room for the finally's own saved GP/XMM codes.
            new ReadOnlySpan<byte>(Original.Image + 0x12c0, 32).CopyTo(new Span<byte>(Original.Image + 0x13c0, 32));
            W32(Original.Image + 0x112c, 0x13c0);
            new Span<byte>(Original.Image + 0x1280, 68).Clear();
            byte* info = Original.Image + 0x1280; info[0] = 1; info[1] = 8; info[2] = 30;
            int slot = 0;
            for (byte register = 6; register <= 15; register++)
            {
                byte* code = info + 4 + slot * 2; code[0] = 8; code[1] = (byte)(register << 4 | 8);
                code[2] = (byte)(register - 6); slot += 2;
            }
            byte* allocation = info + 4 + slot * 2; allocation[0] = 8; allocation[1] = 1; allocation[2] = 20; slot += 2;
            byte[] registers = [3, 5, 6, 7, 12, 13, 14, 15];
            foreach (byte register in registers) { info[4 + slot * 2] = 8; info[5 + slot * 2] = (byte)(register << 4); slot++; }
            info[64] = 1;
            var current = Original.Initial; current.Rip = Original.Address(0x6150); current.Rsp = (ulong)Original.Stack + 0x108;
            var expected = Original.Initial; expected.Rip = Original.Address(0x6120); expected.Rsp = current.Rsp + 232;
            for (int i = 0; i < registers.Length; i++) { W64((byte*)current.Rsp + 160 + i * 8, expected.GetInteger(registers[i])); current.SetInteger(registers[i], 0); }
            for (byte register = 6; register <= 15; register++)
            {
                var vector = expected.GetVector(register); byte* saved = (byte*)current.Rsp + (register - 6) * 16;
                W64(saved, vector.Low); W64(saved + 8, vector.High); current.SetVector(register, default);
            }
            W64((byte*)current.Rsp + 224, expected.Rip); Current = current; Expected = expected; Rebind();
        }
        internal void Rebind()
        {
            NativeMemory.Clear(Original.Snapshot, (nuint)PhysicalFixture.Capacity);
            var bound = X64PERuntimeFunctionUnwindMetadataHostileInput.Bind(Original.Capture, Original.Mapping,
                Original.Owner, PhysicalFixture.Capacity, Original.Handle, Original.Snapshot, PhysicalFixture.Capacity);
            Need(bound.Succeeded, "normal-finally original snapshot " + bound.Status);
        }
        internal void Table(params Row[] rows)
        {
            byte[] blob = NativeAotEhClauseMetadataTests.Table(rows);
            new Span<byte>(Original.Image + 0x1600, 0x100).Clear(); blob.CopyTo(new Span<byte>(Original.Image + 0x1600, blob.Length));
            W32(Original.Image + 0x191c, (uint)blob.Length);
        }
        internal NativeAotNormalFinallyResult Recover(X64UnwindContext? context = null, ulong? handle = null,
            byte* owner = null, byte* snapshot = null, ulong stackBytes = PhysicalFixture.StackBytes, NativeAotLayoutProfile profile = Profile) =>
            NativeAotEhClauseMetadata.RecoverNormalFinallyParent(profile, Original.Capture, Original.Mapping,
                owner == null ? Original.Owner : owner, PhysicalFixture.Capacity, handle ?? Original.Handle,
                snapshot == null ? Original.Snapshot : snapshot, PhysicalFixture.Capacity, 0x1900, 48,
                Original.Stack, stackBytes, context ?? Current);
        public void Dispose() => Original.Dispose();
    }
    private static void W32(byte* pointer, uint value) { for (int i = 0; i < 4; i++) pointer[i] = (byte)(value >> (i * 8)); }
    private static void W64(byte* pointer, ulong value) { for (int i = 0; i < 8; i++) pointer[i] = (byte)(value >> (i * 8)); }
}
