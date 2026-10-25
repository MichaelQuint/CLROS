using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Storage;
using OS.Tooling;
using F = OS.Tooling.LinuxWritableDiskProofFixture;
using Q = OS.Tooling.QemuTranscripts;
using B = OS.Storage.WritableBlockSession;

// Synthetic copied protocol/value controls only. No native producer, device,
// process, CPU execution, physical persistence or reboot authority is supplied.
internal static unsafe class QemuWritableDiskTranscriptTests
{
    [HostTest("writable serial values retain exact OUT IN FLUSH frontiers and immutable copied payloads")]
    private static void BlockValues()
    {
        var w = new Writer(); w.Write(140000); w.Flush(); w.Read(140000);
        var input = w.Selected; var report = Q.InspectWritableBlockValues(input, w.Before, w.After);
        HostCheck.True(report.Io.Count == 6 && report.Io[0].Kind == B.Operation.Write && report.Io[^1].IsCopied && !report.Dirty &&
            !report.HasUncompletedPublication && !report.HasHeldRead && !report.GrantsDeviceOrPersistenceAuthority && report.After.FlushAcknowledgedBytes == 512);
        byte[] expected = report.Io[0].CopyPayload(), mutable = report.Io[0].CopyPayload(); mutable[0] ^= 1;
        input[0] = new(0, "changed"); HostCheck.True(report.Io[0].CopyPayload().AsSpan().SequenceEqual(expected));
        HostCheck.True(report.States[0].Text.StartsWith("M16_027_STATE ", StringComparison.Ordinal));
        HostCheck.True(report.Io.All(r => r.RecordIndex >= 0 && r.DataHex.Length == r.PayloadBytes * 2));
    }

    [HostTest("writable serial standalone report preserves cancelled failed and unresolved facts without terminal authority")]
    private static void Failures()
    {
        var cancelled = new Writer(); cancelled.Read(140000, cancelled: true);
        var c = Q.InspectWritableBlockValues(cancelled.Selected, cancelled.Before, cancelled.After);
        HostCheck.True(c.HasCancelled && !c.HasHeldRead && !c.HasUncompletedPublication && !c.HasDeviceError && c.Io[^1].PayloadBytes == 0);
        var error = new Writer(); error.Write(140000, error: true);
        var e = Q.InspectWritableBlockValues(error.Selected, error.Before, error.After);
        HostCheck.True(e.HasDeviceError && e.Dirty && e.After.CompletedBytes == 0 && e.After.SubmittedBytes == 512);
        var pending = new Writer(); pending.Write(140000, pending: true);
        var p = Q.InspectWritableBlockValues(pending.Selected, pending.Before, pending.After);
        HostCheck.True(p.HasUncompletedPublication && p.Dirty && !p.HasDeviceError && p.After.Completed == 0);
        var held = new Writer(); held.Read(140000, held: true);
        HostCheck.True(Q.InspectWritableBlockValues(held.Selected, held.Before, held.After).HasHeldRead);
        HostCheck.True(!e.GrantsDeviceOrPersistenceAuthority && !p.GrantsDeviceOrPersistenceAuthority);
    }

    [HostTest("writable serial state faults retain dirty unresolved bytes through stop without allowing retry or failure rollback")]
    private static void States()
    {
        var w = new Writer(); w.Write(140000, pending: true); w.State(failure: B.Status.Timeout);
        var pending = Q.InspectWritableBlockValues(w.Selected, w.Before, w.After);
        HostCheck.True(pending.HasStateFailure && pending.HasUncompletedPublication && pending.Dirty && !pending.HasDeviceError);
        w.State(retired: true, failure: B.Status.Timeout);
        var stopped = Q.InspectWritableBlockValues(w.Selected, w.Before, w.After);
        HostCheck.True(stopped.HasStateFailure && stopped.HasUncompletedPublication && stopped.Dirty && !stopped.GrantsDeviceOrPersistenceAuthority);
        var rollback = w.Selected; int fault = Array.FindIndex(rollback, x => x.Text.StartsWith("M16_027_STATE ", StringComparison.Ordinal) && Field(x.Text, "status") == (ulong)B.Status.Timeout);
        Reject(() => Q.InspectWritableBlockValues(Change(rollback, fault + 1, "status", 1), w.Before, w.After));
        Reject(() => Q.InspectWritableBlockValues(Change(rollback, fault + 1, "retry", 0), w.Before, w.After));
        Reject(() => Q.InspectWritableBlockValues(Change(rollback, fault + 1, "running", 1), w.Before, w.After));
        var error = new Writer(); error.Write(140000, error: true); var rows = error.Selected;
        int state = Array.FindLastIndex(rows, x => x.Text.StartsWith("M16_027_STATE ", StringComparison.Ordinal));
        Reject(() => Q.InspectWritableBlockValues(Change(rows, state, "status", 1), error.Before, error.After));
    }

    [HostTest("writable serial values reject malformed payload identity counters flags stages and flush credit")]
    private static void BlockMutations()
    {
        var w = new Writer(); w.Write(140000); w.Flush(); w.Read(140000); var rows = w.Selected;
        void RejectRows(Q.WritableDiskRow[] value) => Reject(() => Q.InspectWritableBlockValues(value, w.Before, w.After));
        int[] io = rows.Select((r, i) => (r, i)).Where(x => x.r.Text.StartsWith("M16_027_IO ", StringComparison.Ordinal)).Select(x => x.i).ToArray();
        foreach (string field in new[] { "owner", "epoch", "generation", "kind", "sector", "flags", "status", "submitted", "completed", "submittedBytes", "completedBytes", "available", "consumed", "completionBytes", "payloadBytes" })
            RejectRows(Change(rows, io[0], field, Field(rows[io[0]].Text, field) + 1));
        RejectRows(Change(rows, io[3], "flushAcknowledgedBytes", 0)); RejectRows(Change(rows, io[2], "flushTargetBytes", 0));
        RejectRows(Change(rows, io[^1], "payloadBytes", 0));
        var lateCancel = rows.Select(x => x.Text).ToList(); lateCancel.Insert(io[1] + 1, Changed(rows[io[1]].Text, "cancelled", 1));
        RejectRows(lateCancel.Select((s, i) => new Q.WritableDiskRow(i, s)).ToArray());
        RejectRows(Replace(rows, io[0], rows[io[0]].Text + " extra=1"));
        RejectRows(Replace(rows, io[0], rows[io[0]].Text.Replace(" data=", " data=A", StringComparison.Ordinal)));
        RejectRows(Replace(rows, io[0], Regex.Replace(rows[io[0]].Text, @"(?<= data=).", "F")));
        RejectRows(Replace(rows, io[0], rows[io[0]].Text.Replace("generation=1", "generation=01", StringComparison.Ordinal)));
        RejectRows(rows.Where((_, i) => i != io[0]).ToArray()); RejectRows(rows.Where((_, i) => i != io[1]).ToArray());
        var duplicate = rows.ToList(); duplicate.Insert(io[0], rows[io[0]]); RejectRows(duplicate.ToArray());
        var reordered = (Q.WritableDiskRow[])rows.Clone(); (reordered[io[0]], reordered[io[1]]) = (reordered[io[1]], reordered[io[0]]); RejectRows(reordered);
        Reject(() => Q.InspectWritableBlockValues(rows, w.Before, w.After with { CompletedBytes = 0 }));
        RejectRows(Change(rows, io[^1], "clock", 0)); RejectRows(Change(rows, 0, "dirty", 1));
    }

    [HostTest("writable serial values cross ushort wrap and reject row bounds overflow and reused indexes")]
    private static void Bounds()
    {
        var w = new Writer(65534); w.Read(4); w.Write(5); w.Flush();
        var report = Q.InspectWritableBlockValues(w.Selected, w.Before, w.After);
        HostCheck.True(report.Io.Any(r => r.Available == 0) && report.After.Submitted == 65537);
        Reject(() => Q.InspectWritableBlockValues(new Oversize(), w.Before, w.After));
        Reject(() => Q.InspectWritableBlockValues([new(-1, "row")], w.Before, w.After));
        Reject(() => Q.InspectWritableBlockValues([new(0, new string('x', 2049))], w.Before, w.After));
        Reject(() => Q.InspectWritableBlockValues([new(0, "M16_027_IO\n")], w.Before, w.After));
        Reject(() => Q.InspectWritableBlockValues(w.Selected, w.Before with { Submitted = ulong.MaxValue, Completed = ulong.MaxValue }, w.After));
        Reject(() => Q.InspectWritableBlockValues(w.Selected, w.Before with { SectorCount = ulong.MaxValue }, w.After));
        Reject(() => Q.InspectWritableBlockValues(w.Selected, w.Before with { Owner = ulong.MaxValue }, w.After));
    }

    [HostTest("writable canonical thirty and forty call leaves join exact syscall waits fd generations and preserve authority limits")]
    private static void Leaves()
    {
        foreach (bool log in new[] { false, true })
        {
            var e = Synthetic(log); var r = Inspect(e);
            HostCheck.True(r.Calls == (log ? 40 : 30) && r.Waits.Count > 10 && r.Transfers == (r.Waits.Count + 1) * 2 && r.Epochs == 0 &&
                r.CheckedCallAndWaitValues && !r.CheckedImageValues && !r.CheckedAuxiliaryJoins && !r.NativeQualified && !r.FirmwareQualified &&
                !r.ProcessQualified && !r.PacketQualified && !r.PhysicalPersistenceQualified && !r.RebootQualified && !r.WholeCaptureQualified);
            foreach (var row in e.Selected) HostCheck.True(r.Auxiliary.Any(a => a == row));
            foreach (var wait in r.Waits) HostCheck.True(wait.WindowFirstRecordIndex < wait.CallRecordIndex && wait.BeginRecordIndex == wait.CallRecordIndex + 1 && wait.CompleteRecordIndex > wait.BeginRecordIndex);
            var first = r.Waits[0]; HostCheck.True(r.Block.Io.Any(x => x.RecordIndex > first.WindowFirstRecordIndex && x.RecordIndex < first.CallRecordIndex));
            var saved = r.Auxiliary[0]; e.All[saved.RecordIndex] = "mutated caller array"; HostCheck.Equal(saved, r.Auxiliary[0]);
        }
    }

    [HostTest("writable native descriptor policy binds ELF payload bytes and rejects package envelope length")]
    private static void PackagePayloadPolicy()
    {
        foreach (bool log in new[] { false, true })
        {
            var e = Synthetic(log); int policy = Find(e, "M16_007_POLICY ");
            ulong expected = (ulong)(log ? F.LogUploadElfBytes : F.ElfBytes);
            HostCheck.Equal(expected, Field(e.All[policy], "packageBytes"));
            HostCheck.True(expected != (ulong)e.Package.Length, "regression mutation must change the field");
            HostCheck.True(Inspect(e).CheckedCallAndWaitValues);
            Reject(() => Inspect(Change(e, policy, "packageBytes", (ulong)e.Package.Length)));
        }
    }

    [HostTest("writable leaf rejects stale begin fd reservation provider wait repark and completion identities")]
    private static void WaitMutations()
    {
        var e = Synthetic(false); int begin = Find(e, "M16_027_BEGIN "), wake = Find(e, "M16_027_WAKE "), complete = Find(e, "M16_027_COMPLETE ");
        foreach (string key in new[] { "identity", "ordinal", "sequence", "requestOwner", "requestEpoch", "request", "nr", "rip", "outputPrefix", "fdOwner", "fdGeneration", "description", "bindingGeneration", "reservedFd", "reservedDescription", "reservationGeneration", "providerOwner", "providerEpoch", "operation" })
            Reject(() => Inspect(Change(e, begin, key, Field(e.All[begin], key) + 1)));
        Reject(() => Inspect(Change(e, begin, "phase", 3)));
        foreach (string key in new[] { "identity", "sequence", "source", "requestOwner", "requestEpoch", "request", "outputPrefix", "completion" })
            Reject(() => Inspect(Change(e, wake, key, Field(e.All[wake], key) + 1)));
        foreach (ulong state in new ulong[] { 0, 2, 3, 4, 5, 6, 7, 8, 255 }) Reject(() => Inspect(Change(e, wake, "state", state)));
        Reject(() => Inspect(e with { All = e.All.Select((text, i) => i == wake ? text.Replace(" state=1", "", StringComparison.Ordinal) : text).ToArray() }));
        foreach (string key in new[] { "identity", "sequence", "requestOwner", "requestEpoch", "request", "nr", "rip", "outputBytes", "completion", "action", "valueBits" })
            Reject(() => Inspect(Change(e, complete, key, Field(e.All[complete], key) + 1)));
        int borrowed = Array.FindIndex(e.All, x => x.StartsWith("M16_027_BEGIN ", StringComparison.Ordinal) && Field(x, "operation") == 3);
        foreach (string key in new[] { "fd", "fdOwner", "fdGeneration", "description", "bindingGeneration" }) Reject(() => Inspect(Change(e, borrowed, key, Field(e.All[borrowed], key) + 1)));
        Reject(() => Inspect(e.Remove(wake))); Reject(() => Inspect(e.Remove(begin))); Reject(() => Inspect(e.Insert(begin, e.All[begin])));
        var reordered = (string[])e.All.Clone(); (reordered[wake], reordered[complete]) = (reordered[complete], reordered[wake]); Reject(() => Inspect(e with { All = reordered }));
        Reject(() => Inspect(Change(e, complete, "completion", 5))); // Interrupted is not this canonical positive program.
    }

    [HostTest("writable source five repark retains original request and prefix while advancing only the actual wait sequence")]
    private static void Repark()
    {
        var original = Synthetic(false); int at = Find(original, "M16_027_COMPLETE "); var all = original.All.ToList();
        string terminal = all[at]; ulong clock = Field(terminal, "clock");
        for (int i = at + 1; i < all.Count; i++)
        {
            string row = all[i];
            if (row.StartsWith("M16_008_CALL ", StringComparison.Ordinal) && Field(row, "waitSequence") != 0) row = Changed(row, "waitSequence", Field(row, "waitSequence") + 1);
            if (row.StartsWith("M16_027_BEGIN ", StringComparison.Ordinal) || row.StartsWith("M16_027_WAKE ", StringComparison.Ordinal) || row.StartsWith("M16_027_COMPLETE ", StringComparison.Ordinal))
                row = Changed(row, "sequence", Field(row, "sequence") + 1);
            if (row.StartsWith("M16_008_CALL ", StringComparison.Ordinal) || row.StartsWith("M16_027_BEGIN ", StringComparison.Ordinal) || row.StartsWith("M16_027_WAKE ", StringComparison.Ordinal) || row.StartsWith("M16_027_COMPLETE ", StringComparison.Ordinal))
                row = Changed(row, "clock", Field(row, "clock") + 10);
            if (row.StartsWith("M16_008_SUPERVISOR ", StringComparison.Ordinal) || row.StartsWith("M16_008_OWNER_END ", StringComparison.Ordinal) || row.StartsWith("M16_027_END ", StringComparison.Ordinal))
                row = Changed(row, "transfers", Field(row, "transfers") + 2);
            if (row.StartsWith("M16_008_BUDGET_END ", StringComparison.Ordinal)) row = Changed(row, "nanos", Field(row, "nanos") + 10);
            all[i] = row;
        }
        all[at] = Changed(Changed(terminal, "action", 3), "valueBits", 0);
        string final = Changed(Changed(terminal, "sequence", 2), "clock", clock + 3);
        all.InsertRange(at + 1, [
            $"M16_027_REPARK identity=4294967297 previous=1 sequence=2 request=1 clock={clock + 1}",
            "M16_008_SUPERVISOR identity=4294967297 action=3 transfers=4 ready=0 active=1",
            $"M16_027_WAKE identity=4294967297 sequence=2 source=5 requestOwner={WriteAttachment} requestEpoch=272 request=1 outputPrefix=0 completion=1 state=1 clock={clock + 2}", final]);
        var e = original with { All = all.ToArray(), Count = original.Count + 4 }; var report = Inspect(e);
        HostCheck.True(report.Waits[0].Sequence == 2 && report.Transfers == Inspect(original).Transfers + 2 && report.Auxiliary.Any(r => r.Text.StartsWith("M16_027_REPARK ", StringComparison.Ordinal)));
        int repark = Find(e, "M16_027_REPARK ");
        foreach (string field in new[] { "identity", "previous", "sequence", "request" }) Reject(() => Inspect(Change(e, repark, field, Field(e.All[repark], field) + 1)));
        Reject(() => Inspect(Change(e, repark, "clock", 0))); Reject(() => Inspect(e.Remove(repark)));
    }

    [HostTest("writable leaf rejects unknown dropped projected rows policy package lifecycle and unexplained IO")]
    private static void LeafMutations()
    {
        var e = Synthetic(true);
        foreach (string prefix in new[] { "M16_027_BOUND ", "M16_027_STOP ", "M16_027_END " }) Reject(() => Inspect(e.Remove(Find(e, prefix))));
        int bound = Find(e, "M16_027_BOUND "), stop = Find(e, "M16_027_STOP "), end = Find(e, "M16_027_END ");
        foreach (string key in new[] { "owner", "epoch", "bar", "capacity", "engineEpoch", "providerEpoch", "pid", "dataFirst", "webLast" })
            Reject(() => Inspect(Change(e, bound, key, Field(e.All[bound], key) + 1)));
        Reject(() => Inspect(Change(e, bound, "pages", 257)));
        Reject(() => Inspect(Change(e, stop, "rows", Field(e.All[stop], "rows") + 1))); Reject(() => Inspect(Change(e, end, "submittedBytes", 0)));
        Reject(() => Inspect(Change(e, Find(e, "M16_008_OWNER_END "), "exit", 99)));
        Reject(() => Inspect(e.Replace(Find(e, "M16_007_POLICY "), e.All[Find(e, "M16_007_POLICY ")].Replace("descriptors=32", "descriptors=16", StringComparison.Ordinal))));
        Reject(() => Inspect(e.Insert(Find(e, "M16_008_CALL "), "UNREVIEWED_ROW value=0")));
        Reject(() => Inspect(e.Insert(Find(e, "M16_008_CALL "), "M16_027_UNKNOWN value=0")));
        var missing = e.Selected.Skip(1).ToArray(); Reject(() => Q.InspectWritableDiskLeafValues(e.Leaf, e.First, e.All.Length, missing, e.Package, true));
        byte[] bad = e.Package; bad[^1] ^= 1; Reject(() => Q.InspectWritableDiskLeafValues(e.Leaf, e.First, e.All.Length, e.Selected, bad, true));
        Reject(() => Q.InspectWritableDiskLeafValues(e.Leaf, e.First, e.All.Length - 100, e.Selected, e.Package, true));
        int call = Find(e, "M16_008_CALL "); Reject(() => Inspect(Change(e, call, "ordinal", 2))); Reject(() => Inspect(Change(e, call, "a1", 0)));
        Reject(() => Inspect(e.Replace(e.First + e.Count - 1, "TEST_PASS m16-009 trailing")));
    }

    [HostTest("writable synthetic copied sector view joins independently extracted combined images and rejects hash bytes guards and extra writes")]
    private static void Images()
    {
        CombinedWebDataImageFixtureTests.WithImage((root, folder, web, data, path, receipt) =>
        {
            byte[] raw = File.ReadAllBytes(path); var e = Synthetic(false, raw);
            var result = Q.InspectWritableDiskValues(e.Leaf, e.First, e.All.Length, e.Selected, e.Package, raw, raw, receipt.Image, receipt.Image);
            HostCheck.True(result.CheckedImageValues && !result.WholeCaptureQualified && !result.PhysicalPersistenceQualified);
            byte[] corrupt = (byte[])raw.Clone(); corrupt[512] ^= 1;
            Reject(() => Q.InspectWritableDiskValues(e.Leaf, e.First, e.All.Length, e.Selected, e.Package, corrupt, raw, receipt.Image, receipt.Image));
            int copied = Array.FindIndex(e.All, x => x.StartsWith("M16_027_IO ", StringComparison.Ordinal) && Field(x, "flags") == 7);
            int nibble = e.All[copied].IndexOf(" data=", StringComparison.Ordinal) + 6;
            string different = e.All[copied][..nibble] + (e.All[copied][nibble] == '0' ? '1' : '0') + e.All[copied][(nibble + 1)..];
            var forged = e.Replace(copied, different);
            Reject(() => Q.InspectWritableDiskValues(forged.Leaf, forged.First, forged.All.Length, forged.Selected, forged.Package, raw, raw, receipt.Image, receipt.Image));
            // Even an OUT of identical guard/slack bytes is outside the fixture's
            // permitted targets; final-byte equality alone cannot authorize it.
            foreach (ulong sector in new[] { receipt.Image.Data.Find("GUARD.TXT")!.Extents[0].FirstLba, (ulong)CombinedWebDataImageFixture.WebFirstLba })
            {
                int published = Array.FindIndex(e.All, x => x.StartsWith("M16_027_IO ", StringComparison.Ordinal) && Field(x, "kind") == 0);
                ulong generation = Field(e.All[published], "generation"); var copy = (string[])e.All.Clone();
                for (int i = 0; i < copy.Length; i++) if (copy[i].StartsWith("M16_027_IO ", StringComparison.Ordinal) && Field(copy[i], "generation") == generation)
                { copy[i] = Changed(copy[i], "sector", sector); int dataAt = copy[i].IndexOf(" data=", StringComparison.Ordinal); copy[i] = copy[i][..(dataAt + 6)] + Convert.ToHexString(raw.AsSpan(checked((int)(sector * 512)), 512)).ToLowerInvariant(); }
                var guarded = e with { All = copy };
                Reject(() => Q.InspectWritableDiskValues(guarded.Leaf, guarded.First, guarded.All.Length, guarded.Selected, guarded.Package, raw, raw, receipt.Image, receipt.Image));
            }
            byte[] extra = (byte[])raw.Clone(); extra[(int)(CombinedWebDataImageFixture.DataLastLba * 512)] ^= 1;
            string changed = Path.Combine(folder, "unobserved-final.raw"); File.WriteAllBytes(changed, extra);
            var final = CombinedWebDataImageFixture.Inspect(changed);
            Reject(() => Q.InspectWritableDiskValues(e.Leaf, e.First, e.All.Length, e.Selected, e.Package, raw, extra, receipt.Image, final));
        });
    }

    private sealed record Evidence(string[] All, int First, int Count, bool Log)
    {
        internal byte[] Package => Log ? F.CreateLogUploadPackage() : F.CreatePackage();
        internal string[] Leaf => All.Skip(First).Take(Count).ToArray();
        internal Q.WritableDiskRow[] Selected => All.Select((s, i) => new Q.WritableDiskRow(i, s)).Where(x => x.Text.StartsWith("M16_027_", StringComparison.Ordinal)).ToArray();
        internal Evidence Replace(int at, string text) { var copy = (string[])All.Clone(); copy[at] = text; return this with { All = copy }; }
        internal Evidence Insert(int at, string text) { var copy = All.ToList(); copy.Insert(at, text); return this with { All = copy.ToArray(), First = at < First ? First + 1 : First, Count = at >= First && at < First + Count ? Count + 1 : Count }; }
        internal Evidence Remove(int at) { var copy = All.ToList(); copy.RemoveAt(at); return this with { All = copy.ToArray(), First = at < First ? First - 1 : First, Count = at >= First && at < First + Count ? Count - 1 : Count }; }
    }
    private static Q.WritableDiskValueReport Inspect(Evidence e) => Q.InspectWritableDiskLeafValues(e.Leaf, e.First, e.All.Length, e.Selected, e.Package, e.Log);
    private static int Find(Evidence e, string prefix) { int at = Array.FindIndex(e.All, s => s.StartsWith(prefix, StringComparison.Ordinal)); HostCheck.True(at >= 0); return at; }
    private static ulong Field(string row, string key) => ulong.Parse(Regex.Match(row, @"(?:^| )" + key + @"=([0-9]+)(?: |$)").Groups[1].Value, CultureInfo.InvariantCulture);
    private static string Changed(string row, string key, ulong value) => Regex.Replace(row, @"(?<=\b" + key + @"=)[0-9]+", value.ToString(CultureInfo.InvariantCulture));
    private static Evidence Change(Evidence e, int at, string field, ulong value) => e.Replace(at, Changed(e.All[at], field, value));
    private static Q.WritableDiskRow[] Change(Q.WritableDiskRow[] rows, int at, string field, ulong value) => Replace(rows, at, Changed(rows[at].Text, field, value));
    private static Q.WritableDiskRow[] Replace(Q.WritableDiskRow[] rows, int at, string text) { var copy = (Q.WritableDiskRow[])rows.Clone(); copy[at] = new(rows[at].RecordIndex, text); return copy; }
    private static void Reject(Action action) { try { action(); throw new InvalidOperationException("expected writable transcript rejection"); } catch (ToolFailure) { } }
    private sealed class Oversize : IReadOnlyList<Q.WritableDiskRow>
    { public int Count => Q.MaximumWritableDiskRows + 1; public Q.WritableDiskRow this[int index] => throw new InvalidOperationException(); public IEnumerator<Q.WritableDiskRow> GetEnumerator() => throw new InvalidOperationException(); System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator(); }

    private const ulong Owner = 0x1001000, Epoch = 270, Arena = 0x1000000, Boot = 0x900000, WriteAttachment = 0x1007000, Provider = 0x1005000;
    private sealed class Writer
    {
        internal readonly List<string> Rows = [];
        internal ulong Submitted, Completed, SubmittedBytes, CompletedBytes, Flushed, Clock = 10000;
        internal readonly F.BlockBoundary Before;
        internal F.BlockBoundary After => new(Owner, Epoch, CombinedWebDataImageFixture.Sectors, Submitted, Completed, SubmittedBytes, CompletedBytes, Flushed);
        internal int StateIoRows;
        internal byte[]? Image;
        internal Writer(ulong baseline = 0) { Submitted = Completed = baseline; Before = After; State(); }
        internal Q.WritableDiskRow[] Selected => Rows.Select((s, i) => new Q.WritableDiskRow(i, s)).ToArray();
        internal void State(bool retired = false, B.Status failure = B.Status.Success)
        { Rows.Add(FormattableString.Invariant($"M16_027_STATE owner={Owner} epoch={Epoch} bound=1 status={(byte)failure} running={(retired || failure != B.Status.Success ? 0 : 1)} retired={(retired ? 1 : 0)} dirty={(SubmittedBytes != Flushed ? 1 : 0)} retry={(failure == B.Status.Success ? 0 : 1)} submitted={Submitted} completed={Completed} submittedBytes={SubmittedBytes} completedBytes={CompletedBytes} flushAcknowledgedBytes={Flushed} clock={Clock++}")); StateIoRows++; }
        internal byte[] Sector(ulong sector) => Image is null ? Enumerable.Range(0, 512).Select(i => (byte)((i * 19 + (int)sector) & 255)).ToArray() : Image.AsSpan(checked((int)(sector * 512)), 512).ToArray();
        private void Io(B.Operation kind, ulong sector, byte flags, B.Status status, bool cancelled, byte[]? bytes, ulong target = 0)
        { uint count = status == B.Status.Success && kind != B.Operation.Flush ? 512U : 0;
            Rows.Add(FormattableString.Invariant($"M16_027_IO owner={Owner} epoch={Epoch} generation={Submitted} kind={(byte)kind} sector={sector} flags={flags} status={(byte)status} cancelled={(cancelled ? 1 : 0)} submitted={Submitted} completed={Completed} submittedBytes={SubmittedBytes} completedBytes={CompletedBytes} flushTargetBytes={target} flushAcknowledgedBytes={Flushed} available={unchecked((ushort)Submitted)} consumed={unchecked((ushort)Completed)} completionBytes={count} payloadBytes={(bytes is null ? 0 : 512)} clock={Clock++}") + (bytes is null ? "" : " data=" + Convert.ToHexString(bytes).ToLowerInvariant())); StateIoRows++; }
        internal void Write(ulong sector, bool error = false, bool pending = false)
        { Submitted++; SubmittedBytes += 512; State(); byte[] bytes = Sector(sector); Io(B.Operation.Write, sector, 1, B.Status.Uninitialized, false, bytes);
            if (pending) return; Completed++; if (!error) CompletedBytes += 512; State(failure: error ? B.Status.DeviceIoError : B.Status.Success); Io(B.Operation.Write, sector, 3, error ? B.Status.DeviceIoError : B.Status.Success, false, bytes); }
        internal void Flush()
        { ulong target = CompletedBytes; Submitted++; State(); Io(B.Operation.Flush, 0, 1, B.Status.Uninitialized, false, null, target); Completed++; Flushed = target; State(); Io(B.Operation.Flush, 0, 3, B.Status.Success, false, null, target); }
        internal void Read(ulong sector, bool cancelled = false, bool held = false)
        { Submitted++; State(); Io(B.Operation.Read, sector, 1, B.Status.Uninitialized, false, null);
            if (cancelled) Io(B.Operation.Read, sector, 1, B.Status.Uninitialized, true, null);
            Completed++; State(); Io(B.Operation.Read, sector, (byte)(cancelled || held ? 3 : 7), B.Status.Success, cancelled, cancelled || held ? null : Sector(sector)); }
    }

    private static Evidence Synthetic(bool log, byte[]? raw = null)
    {
        var w = new Writer { Image = raw }; string initialState = w.Rows[0]; w.Rows.Clear(); w.StateIoRows = 0;
        w.Rows.AddRange(Enumerable.Repeat("original parent row", 50)); const int first = 50;
        byte[] package = log ? F.CreateLogUploadPackage() : F.CreatePackage(); var calls = log ? F.LogUploadCalls() : F.Calls();
        ulong stack = Stack(package), clock = 100, sequence = 0, request = 0; int transfers = 0;
        w.Rows.AddRange(["TEST_START m16-009", "M16_006_POLICY physical=536870912 available=268435456 poolPages=4096 vaEnd=68719476736 reservationQuota=17179869184 entropyBytes=32 cpus=1 root=inactive-kernel",
            $"M16_007_POLICY descriptors=32 pipes=2 capacity=4096 scratch=8192 packageBytes={LinuxElfPackage.Validate(package).PayloadBytes} package=declared-immutable proc=owned-snapshot stdin=eof stdoutStderr=serial sigpipe=ignored-mechanical blocking=scheduler",
            "M16_011_BOUND connections=8 rx=8192 tx=32768 epoll=4 watches=64 nic=live", "M16_005_ARMED root=2097152 cs=59 ss=51 kernelStack=3145728 guard=absent syscall=sce-star-lstar-fmask return=iretq fp=x87-sse kernelFsGs=owned-null", "TEST_START m16.027"]);
        w.Rows.Add(initialState); w.StateIoRows++;
        for (ulong sector = 1; sector <= ContinuingFat32Bootstrap.MaximumReads; sector++) w.Read(sector);
        w.Rows.Add(FormattableString.Invariant($"M16_027_BOUND owner={Owner} epoch={Epoch} arena={Arena} pages=65 bar=4026531840 capacity=262144 nativeOwner=8388608 nativeOwnerBytes=1024 boot={Boot} bootBytes=65536 carrier=16809984 carrierBytes=64 observation=16814080 observationBytes=648 engine=16785408 engineEpoch=272 port=16789504 portEpoch=273 mount=16793600 mountEpoch=274 provider={Provider} providerEpoch=272 readAttachment=16801792 writeAttachment={WriteAttachment} dataFirst=131072 dataLast=260062 webFirst=2048 webLast=131038 pid=1 clock={w.Clock++} pci=0:7:0 queue=0 access=readwrite flush=negotiated reservation=retained"));
        w.Rows.Add($"M16_008_BOUND root=1048576 pool=4194304 workers=2 code={LinuxThreadProgram.Bytes} metadata={LinuxThreadProgram.MetadataPages * 4096} userImage=728 timer=idle-only clock=hpet-nanoseconds reuse=off-stack");
        w.Rows.Add("M16_008_BUDGET started=1 epochs=8192 steps=16384 nanos=300000000000");
        Dictionary<ulong, (ulong Generation, ulong Description, ulong Binding)> fds = []; Dictionary<ulong, ulong> generations = [];
        void Supervisor(int action) { transfers += 2; w.Rows.Add($"M16_008_SUPERVISOR identity=4294967297 action={action} transfers={transfers} ready=0 active=1"); }
        for (int ordinal = 0; ordinal < calls.Count; ordinal++)
        {
            var c = calls[ordinal]; bool async = c.Number is 0 or 17 or 74 or 328 or 87 || c.Number == 2 && c.Role != "open.outside"; if (c.Role == "read.closed") async = false;
            if (ordinal == 0) w.Read(140000); // A real dispatch sample may precede CALL/BEGIN serialization.
            if (c.Role == "witness") { string witness = log ? F.LogUploadWitness : F.Witness; w.Rows.Add($"M16_005_WRITE fd=1 bytes={witness.Length} data={witness}"); }
            if (async) { sequence++; request++; }
            bool exit = c.Number == 231; ulong value = unchecked((ulong)(async ? 0 : c.Result));
            w.Rows.Add(FormattableString.Invariant($"M16_008_CALL identity=4294967297 nr={c.Number} a0={c.A0} a1={c.A1} a2={c.A2} a3={c.A3} a4={c.A4} a5={c.A5} rip={c.ReturnRip} rsp={stack} flags=2 fs=0 gs=0 clock={clock++} valueBits={value} action={(async ? 3 : exit ? 2 : 1)} status={(async ? 9 : exit ? 2 : 1)} ordinal={ordinal + 1} waitSource={(async ? 5 : 0)} waitDeadline=0 waitSequence={(async ? sequence : 0)} signalDepth=0 signalFrame=0 blockedSignals=0 returnRip={c.ReturnRip} returnRsp={stack} returnFlags=2 returnFs=0 returnGs=0 returnValueBits={value}"));
            if (async)
            {
                ulong reservedFd = 0, reservedDescription = 0, reservedGeneration = 0, fdOwner = 0, fd = 0, fdGeneration = 0, description = 0, bindingGeneration = 0;
                if (c.Number == 2)
                { reservedFd = 3; while (fds.ContainsKey(reservedFd)) reservedFd++; reservedDescription = 3; while (fds.Values.Any(v => v.Description == reservedDescription)) reservedDescription++; generations.TryGetValue(reservedFd, out reservedGeneration); }
                else if (c.Number != 87) { var binding = fds[c.A0]; fdOwner = Boot + 4096; fd = c.A0; fdGeneration = binding.Generation; description = binding.Description; bindingGeneration = binding.Binding; }
                ulong operation = c.Number switch { 2 => 1, 0 or 17 => 2, 328 => 3, 74 => 4, _ => 5 };
                w.Rows.Add(FormattableString.Invariant($"M16_027_BEGIN identity=4294967297 ordinal={ordinal + 1} sequence={sequence} requestOwner={WriteAttachment} requestEpoch=272 request={request} nr={c.Number} rip={c.ReturnRip} outputPrefix={c.OutputBytes} fdOwner={fdOwner} fd={fd} fdGeneration={fdGeneration} description={description} bindingGeneration={bindingGeneration} reservedFd={reservedFd} reservedDescription={reservedDescription} reservationGeneration={reservedGeneration} providerOwner={Provider} providerEpoch=272 providerRequest={request + 100} operation={operation} phase=1 clock={clock++}"));
                Supervisor(3);
                if (c.Number is 328 or 87 || c.Number == 2 && (c.A1 & 0x40) != 0) { w.Write(140001); w.Flush(); }
                else if (c.Number == 74) w.Flush(); else if (c.Number is 0 or 17 && c.Result > 0) w.Read(140002);
                w.Rows.Add(FormattableString.Invariant($"M16_027_WAKE identity=4294967297 sequence={sequence} source=5 requestOwner={WriteAttachment} requestEpoch=272 request={request} outputPrefix={c.OutputBytes} completion=1 state=1 clock={clock++}"));
                w.Rows.Add(FormattableString.Invariant($"M16_027_COMPLETE identity=4294967297 sequence={sequence} requestOwner={WriteAttachment} requestEpoch=272 request={request} nr={c.Number} rip={c.ReturnRip} outputBytes={c.OutputBytes} completion=1 action=1 valueBits={unchecked((ulong)c.Result)} clock={clock++}"));
                w.Rows.Add($"M16_008_RESUME identity=4294967297 valueBits={unchecked((ulong)c.Result)} status=1");
                if (c.Number == 2 && c.Result >= 0) { generations[reservedFd] = reservedGeneration + 1; fds.Add(reservedFd, (reservedGeneration + 1, reservedDescription, request)); }
            }
            else if (c.Number == 3) fds.Remove(c.A0);
            if (exit) { w.Rows.Add("M16_008_EXIT identity=4294967297 clearTidResult=0"); Supervisor(2); }
        }
        w.Rows.Add("M16_008_REAP old=4294967297 next=8589934593 context=zero stack=zero slot=zero");
        for (int fd = 0; fd < 3; fd++) w.Rows.Add($"M16_011_CLOSE fd={fd} valueBits=0");
        w.State(retired: true);
        w.Rows.Add(FormattableString.Invariant($"M16_027_STOP submitted={w.Submitted} completed={w.Completed} submittedBytes={w.SubmittedBytes} completedBytes={w.CompletedBytes} flushAcknowledgedBytes={w.Flushed} rows={w.StateIoRows} clock={w.Clock++} dma=zero reservation=retained"));
        w.Rows.Add($"M16_008_BUDGET_END nanos={clock++}"); w.Rows.Add($"M16_008_OWNER_END reaped=1 transfers={transfers} irqs=0 exit=42 userReserved=0 userBacked=0 process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained");
        w.Rows.AddRange(["M16_009_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true", "CLROS_M16_009_READY", "TEST_PASS m16-009"]); int count = w.Rows.Count - first;
        w.Rows.Add(FormattableString.Invariant($"M16_027_END owner={Owner} epoch={Epoch} reaped=1 transfers={transfers} irqs=0 submitted={w.Submitted} completed={w.Completed} submittedBytes={w.SubmittedBytes} completedBytes={w.CompletedBytes} flushAcknowledgedBytes={w.Flushed} clock={w.Clock++} process=drained timer=restored descriptors=closed mappings=restored pool=scrubbed nic=retired dma=zero reservation=retained"));
        w.Rows.AddRange(["CLROS_M16_027_READY", "TEST_PASS m16.027", "original parent tail"]); return new(w.Rows.ToArray(), first, count, log);
    }

    private static ulong Stack(byte[] package)
    {
        var view = LinuxElfPackage.Validate(package); byte* seed = stackalloc byte[32]; new Span<byte>(seed, 32).Fill(1);
        byte* storage = (byte*)NativeMemory.AlignedAlloc(view.Manifest.StackBytes, 4096);
        try { fixed (byte* p = package) { HostCheck.Equal(LinuxElfStartupStatus.Ready, LinuxElfStartup.Build(p + view.ManifestOffset, view.ManifestBytes, p + view.PayloadOffset, view.PayloadBytes,
            seed, 32, storage, view.Manifest.StackBytes, 0x70000000UL - view.Manifest.StackBytes, 0, out var initial)); return initial.StackPointer; } }
        finally { NativeMemory.AlignedFree(storage); }
    }
}
