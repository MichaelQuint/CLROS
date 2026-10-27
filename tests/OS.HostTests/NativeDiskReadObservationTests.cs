using System.Collections;
using System.Globalization;
using OS.Tooling;
using Reads = OS.Tooling.NativeDiskReadObservations;

internal static class NativeDiskReadObservationTests
{
    private sealed record Fixture(byte[] Image, List<string> Rows, Reads.Boundary Boundary);

    [HostTest("disk reset retirement keeps the exact last pending submit without inventing completion or copied bytes")]
    private static void PendingReset()
    {
        foreach (ulong bootstrap in new[] { 9UL, 65534UL, ulong.MaxValue - 3 })
        {
            var full = Vector(bootstrap, bootstrap, false, true, false);
            var boundary = full.Boundary with { Completed = full.Boundary.Completed - 1, Copied = full.Boundary.Copied - 1 };
            string[] rows = full.Rows.Take(full.Rows.Count - 2).ToArray();
            var reset = ResetFor(boundary, 3, 1);
            var actual = Reads.InspectResetRetired(rows, full.Image, boundary, reset);
            HostCheck.Equal(boundary, actual.Counters); HostCheck.Equal(1UL, actual.Cancelled);
            HostCheck.True(actual.ResetRetirement == reset && actual.Rows.Count == rows.Length &&
                actual.Rows[^1].Kind == Reads.Kind.Submitted && actual.Rows[^1].Generation == boundary.Submitted &&
                actual.Rows[^1].DataHex == "" && actual.Rows.Count(r => r.Kind == Reads.Kind.CompletionValidated) == 2);
            Reject(full, rows, boundary: boundary); // The ordinary consumer remains drained-only.
            string retained = actual.Rows[2].DataHex; Array.Fill(rows, "changed"); Array.Clear(full.Image);
            HostCheck.Equal(retained, actual.Rows[2].DataHex);
        }
        HostCheck.True(Inspect(Vector()).ResetRetirement is null);
    }

    [HostTest("disk reset retirement rejects stale generations sectors incomplete quiescence changed counters and ready data")]
    private static void PendingResetRejections()
    {
        var full = Vector(9, 9, false, true, false);
        var boundary = full.Boundary with { Completed = full.Boundary.Completed - 1, Copied = full.Boundary.Copied - 1 };
        string[] rows = full.Rows.Take(full.Rows.Count - 2).ToArray(); var reset = ResetFor(boundary, 3, 1);
        void Deny(Reads.PendingResetRetirement changed, IReadOnlyList<string>? selected = null, Reads.Boundary? counters = null)
        {
            bool rejected = false;
            try { _ = Reads.InspectResetRetired(selected ?? rows, full.Image, counters ?? boundary, changed); }
            catch (ToolFailure) { rejected = true; }
            HostCheck.True(rejected);
        }
        foreach (var changed in new[]
        {
            reset with { Owner = reset.Owner + 8 }, reset with { Epoch = reset.Epoch + 1 }, reset with { Generation = reset.Generation - 1 },
            reset with { Sector = reset.Sector + 1 }, reset with { SubmittedBeforeStop = reset.SubmittedBeforeStop - 1 },
            reset with { CompletedBeforeStop = reset.CompletedBeforeStop + 1 }, reset with { CopiedBeforeStop = reset.CopiedBeforeStop + 1 },
            reset with { CancelledBeforeStop = 0 }, reset with { StopStatus = OS.Storage.ContinuingReadOnlyBlockService.Status.ResetFailed },
            reset with { ResetReads = 0 }, reset with { ResetReads = 2049 }, reset with { ResetComplete = false },
            reset with { BusMasterDisabled = false }, reset with { DmaReleased = false }, reset with { PoolMask = 1 }
        }) Deny(changed);
        Deny(reset, rows[..^1]); Deny(reset, full.Rows); Deny(reset, full.Rows.Take(full.Rows.Count - 1).ToArray());
        Deny(reset, [..rows, rows[^1]]); Deny(reset, counters: boundary with { Submitted = boundary.Submitted + 1 });
        Deny(reset, counters: boundary with { Completed = boundary.Completed + 1 });
        Deny(reset, counters: boundary with { BootstrapCopied = boundary.BootstrapCopied - 1 });
        Deny(reset, Replace(rows, 0, Set(rows[0], "sector", "2")));
    }

    private static Reads.PendingResetRetirement ResetFor(Reads.Boundary boundary, ulong sector, ulong cancelled) =>
        new(boundary.Owner, boundary.Epoch, boundary.Submitted, sector, boundary.Submitted, boundary.Completed, boundary.Copied, cancelled,
            OS.Storage.ContinuingReadOnlyBlockService.Status.Success, 1, true, true, true, 0);

    [HostTest("disk observation joins ordered copied sectors and cancellation to exact image bytes")]
    private static void Complete()
    {
        var f = Vector(9, 7, false, true, false); var actual = Inspect(f);
        HostCheck.Equal(f.Boundary, actual.Counters); HostCheck.Equal(1UL, actual.Cancelled);
        HostCheck.Equal(8, actual.Rows.Count);
        HostCheck.Equal(Reads.Kind.Submitted, actual.Rows[0].Kind);
        HostCheck.Equal(Reads.Kind.CompletionValidated, actual.Rows[1].Kind);
        HostCheck.Equal(Reads.Kind.Copied, actual.Rows[2].Kind);
        HostCheck.True(actual.Rows[2].Generation == 10 && actual.Rows[2].Sequence == 8 && actual.Rows[2].Sector == 1);
        HostCheck.True(actual.Rows[4].Bytes == 0 && actual.Rows[4].DataHex == "");
        foreach (var copied in actual.Rows.Where(row => row.Kind == Reads.Kind.Copied))
            HostCheck.True(Convert.FromHexString(copied.DataHex).AsSpan().SequenceEqual(f.Image.AsSpan((int)copied.Sector * 512, 512)));
        // A value-only sector join does not authenticate unobserved image bytes.
        f.Image[0] ^= 1; HostCheck.Equal(8, Inspect(f).Rows.Count);
        var cancelled = Vector(10, 5, true); actual = Inspect(cancelled);
        HostCheck.Equal(1UL, actual.Cancelled); HostCheck.Equal(2, actual.Rows.Count); HostCheck.Equal(5UL, actual.Counters.Copied);
    }

    [HostTest("disk observation handles cumulative ushort ring wrap and final ulong generation")]
    private static void WrapAndMaximum()
    {
        var f = Vector(65534, 65530, false, false, true, false); var actual = Inspect(f);
        HostCheck.Equal((ushort)65535, actual.Rows[0].AvailableIndex);
        HostCheck.Equal((ushort)65534, actual.Rows[0].ConsumedIndex);
        HostCheck.Equal((ushort)0, actual.Rows[3].AvailableIndex);
        HostCheck.Equal((ushort)65535, actual.Rows[3].ConsumedIndex);
        HostCheck.Equal((ushort)0, actual.Rows[4].ConsumedIndex);
        HostCheck.Equal(65538UL, actual.Counters.Submitted); HostCheck.Equal(65533UL, actual.Counters.Copied);
        actual = Inspect(Vector(ulong.MaxValue - 1, ulong.MaxValue - 1, false));
        HostCheck.Equal(ulong.MaxValue, actual.Rows[0].Generation); HostCheck.Equal(ulong.MaxValue, actual.Counters.Copied);
        f = Vector(); for (int i = 0; i < f.Rows.Count; i++) f.Rows[i] = Set(f.Rows[i], "clock", "0");
        HostCheck.True(Inspect(f).Rows.All(row => row.Clock == 0)); // Equal clocks are monotonic.
    }

    [HostTest("disk observation result is immutable and retains neither caller image nor row collection")]
    private static void Copies()
    {
        var f = Vector(); byte[] before = f.Image.ToArray(); string[] rows = f.Rows.ToArray(); var actual = Inspect(f);
        HostCheck.True(f.Image.AsSpan().SequenceEqual(before) && f.Rows.SequenceEqual(rows));
        string copied = actual.Rows[2].DataHex; Reads.Observation first = actual.Rows[0];
        Array.Clear(f.Image); f.Rows[0] = "mutated"; f.Rows.Clear();
        HostCheck.Equal(first, actual.Rows[0]); HostCheck.Equal(copied, actual.Rows[2].DataHex);
        HostCheck.True(Convert.FromHexString(copied).AsSpan().SequenceEqual(before.AsSpan(512, 512)));
        bool immutable = false;
        try { ((IList<Reads.Observation>)actual.Rows)[0] = default; } catch (NotSupportedException) { immutable = true; }
        HostCheck.True(immutable);
    }

    [HostTest("disk observation rejects malformed decimal exact fields suffix and lowercase sector payloads")]
    private static void Shapes()
    {
        var f = Vector();
        string[] badSubmit =
        [ " " + f.Rows[0], f.Rows[0] + " ", f.Rows[0] + "\r", f.Rows[0] + "\n",
          f.Rows[0].Replace(" owner=", "\towner=", StringComparison.Ordinal),
          f.Rows[0].Replace(" owner=", "  owner=", StringComparison.Ordinal),
          f.Rows[0].Replace(" epoch=", " unknown=", StringComparison.Ordinal),
          Set(f.Rows[0], "kind", "3"), Set(f.Rows[0], "kind", "00"), Set(f.Rows[0], "owner", "+7"),
          Set(f.Rows[0], "owner", "-7"), Set(f.Rows[0], "owner", "\u0661"), Set(f.Rows[0], "owner", "18446744073709551616"),
          Set(f.Rows[0], "epoch", "01"), Set(f.Rows[0], "clock", "1.0"),
          Set(f.Rows[0], "clock", "18446744073709551616"), Set(f.Rows[0], "available", "65536"),
          f.Rows[0] + " extra=0", f.Rows[0] + " copied=committed data=" + new string('0', 1024) ];
        foreach (string row in badSubmit) Reject(f, Replace(f.Rows, 0, row));
        string hex = Inspect(f).Rows[2].DataHex;
        foreach (string row in new[]
        {
            Set(f.Rows[2], "data", hex.ToUpperInvariant()), Set(f.Rows[2], "data", hex[..1023]),
            Set(f.Rows[2], "data", hex + "0"), Set(f.Rows[2], "data", "g" + hex[1..]),
            Set(f.Rows[2], "copied", "pending"), Set(f.Rows[2], "bytes", "0"),
            f.Rows[2][..f.Rows[2].IndexOf(" copied=", StringComparison.Ordinal)], f.Rows[2] + " unknown=1"
        }) Reject(f, Replace(f.Rows, 2, row));
    }

    [HostTest("disk observation rejects stale owner epoch generation sector sequence clock and ring associations")]
    private static void Associations()
    {
        var f = Vector();
        (int Row, string Field, string Value)[] mutants =
        [ (0,"owner","1"), (1,"epoch","2"), (2,"owner","1"), (0,"generation","0"),
          (0,"generation","2"), (1,"generation","2"), (3,"generation","1"), (2,"generation","2"),
          (1,"sector","2"), (2,"sector","2"), (0,"sequence","2"), (1,"sequence","2"),
          (2,"sequence","2"), (0,"bytes","0"), (1,"bytes","1"), (1,"bytes","513"),
          (0,"available","0"), (0,"consumed","1"), (1,"available","2"), (1,"consumed","0"),
          (2,"available","0"), (2,"consumed","0"), (1,"clock","99"), (2,"clock","100") ];
        foreach (var mutant in mutants) Reject(f, Replace(f.Rows, mutant.Row, Set(f.Rows[mutant.Row], mutant.Field, mutant.Value)));
        var wrap = Vector(65535, 65535, false);
        Reject(wrap, Replace(wrap.Rows, 0, Set(wrap.Rows[0], "available", "65535")));
        Reject(wrap, Replace(wrap.Rows, 1, Set(wrap.Rows[1], "consumed", "65535")));
    }

    [HostTest("disk observation rejects missing extra duplicate reordered and uncopied positive lifetimes")]
    private static void Ordering()
    {
        var f = Vector();
        for (int i = 0; i < f.Rows.Count; i++) { var rows = f.Rows.ToList(); rows.RemoveAt(i); Reject(f, rows); }
        foreach (int duplicate in new[] { 0, 1, 2 })
        { var rows = f.Rows.ToList(); rows.Insert(duplicate + 1, rows[duplicate]); Reject(f, rows); }
        foreach (var pair in new[] { (0, 1), (1, 2), (2, 3) })
        { var rows = f.Rows.ToList(); (rows[pair.Item1], rows[pair.Item2]) = (rows[pair.Item2], rows[pair.Item1]); Reject(f, rows); }
        Reject(f, [.. f.Rows, f.Rows[0]]); Reject(f, [.. f.Rows, "TEST_PASS unrelated"]);
        var cancelled = Vector(0, 0, true, false);
        string falseCopy = Set(Set(f.Rows[2], "sequence", "1"), "consumed", "1");
        Reject(cancelled, [cancelled.Rows[0], cancelled.Rows[1], falseCopy, .. cancelled.Rows.Skip(2)]);
        // A positive completion cannot silently become a cancelled/omitted copy.
        Reject(f, Replace(f.Rows, 1, Set(f.Rows[1], "bytes", "0")));
    }

    [HostTest("disk observation compares actual full sector bytes and bounds unsigned64 LBA before multiplication")]
    private static void ImageBytesAndLba()
    {
        var f = Vector();
        foreach (int index in new[] { 0, 255, 511 })
        { byte[] image = f.Image.ToArray(); image[512 + index] ^= 1; Reject(f, image: image); }
        string hex = Inspect(f).Rows[2].DataHex;
        Reject(f, Replace(f.Rows, 2, Set(f.Rows[2], "data", (hex[0] == '0' ? "1" : "0") + hex[1..])));
        foreach (ulong sector in new[] { (ulong)f.Image.Length / 512, (ulong)long.MaxValue + 1, ulong.MaxValue / 512 + 1, ulong.MaxValue })
            Reject(f, Replace(f.Rows, 0, Set(f.Rows[0], "sector", N(sector))));
        Reject(f, image: []); Reject(f, image: new byte[511]); Reject(f, image: new byte[513]);
        var cancel = Vector(0, 0, true); Reject(cancel, Replace(cancel.Rows, 0, Set(cancel.Rows[0], "sector", N(ulong.MaxValue))));
    }

    [HostTest("disk observation rejects wrong bootstrap terminal counters bounds and numeric overflow")]
    private static void BoundariesAndBounds()
    {
        var f = Vector(0, 0, false, true, false); var b = f.Boundary;
        foreach (var boundary in new[]
        {
            b with { Owner = 0 }, b with { Epoch = 0 }, b with { Owner = b.Owner + 8 }, b with { Epoch = b.Epoch + 1 },
            b with { BootstrapCompleted = 1 }, b with { BootstrapCopied = 1 }, b with { BootstrapCopied = 4 },
            b with { Submitted = 2 }, b with { Completed = 2 }, b with { Submitted = 4, Completed = 4 },
            b with { Copied = 1 }, b with { Copied = 3 }, b with { Copied = 4 },
            b with { BootstrapCompleted = ulong.MaxValue, BootstrapCopied = ulong.MaxValue, Submitted = ulong.MaxValue, Completed = ulong.MaxValue, Copied = ulong.MaxValue }
        }) Reject(f, boundary: boundary);
        Reject(f, []); Reject(f, new CountOnly(Reads.MaximumRows + 1));
        Reject(f, Replace(f.Rows, 0, new string('x', Reads.MaximumRowChars + 1)));
        Reject(f, Replace(f.Rows, 0, null!));
        var last = Vector(ulong.MaxValue - 1, ulong.MaxValue - 1, false);
        Reject(last, Replace(last.Rows, 0, Set(last.Rows[0], "sequence", "18446744073709551616")));
        Reject(last, [.. last.Rows, last.Rows[0]]);
    }

    private static Reads.Inspection Inspect(Fixture f) => Reads.Inspect(f.Rows, f.Image, f.Boundary);
    private static string N(ulong value) => value.ToString(CultureInfo.InvariantCulture);
    private static Fixture Vector(ulong bootstrap = 0, ulong bootstrapCopied = 0, params bool[] cancellations)
    {
        if (cancellations.Length == 0) cancellations = [false, false];
        byte[] image = Enumerable.Range(0, 8 * 512).Select(i => unchecked((byte)((i * 31) ^ (i >> 8) ^ 0x5a))).ToArray();
        List<string> rows = []; ulong submitted = bootstrap, completed = bootstrap, copied = bootstrapCopied, clock = 100;
        for (int i = 0; i < cancellations.Length; i++)
        {
            ulong generation = checked(++submitted), sector = (ulong)(i % 3 + 1);
            Row(0, generation, sector, submitted, 512); completed = checked(completed + 1);
            Row(1, generation, sector, completed, cancellations[i] ? 0U : 512U);
            if (!cancellations[i]) { copied = checked(copied + 1); Row(2, generation, sector, copied, 512); }
        }
        return new(image, rows, new(0x400008, 0x123456, bootstrap, bootstrapCopied, submitted, completed, copied));
        void Row(uint kind, ulong generation, ulong sector, ulong sequence, uint bytes)
        {
            string row = FormattableString.Invariant($"M16_023_IO kind={kind} owner={0x400008UL} epoch={0x123456UL} generation={generation} sector={sector} sequence={sequence} bytes={bytes} available={unchecked((ushort)submitted)} consumed={unchecked((ushort)completed)} clock={clock++}");
            if (kind == 2) row += " copied=committed data=" + Convert.ToHexString(image.AsSpan((int)sector * 512, 512)).ToLowerInvariant();
            rows.Add(row);
        }
    }
    private static string Set(string row, string key, string value)
    {
        string prefix = " " + key + "="; int start = row.IndexOf(prefix, StringComparison.Ordinal) + prefix.Length;
        int end = row.IndexOf(' ', start); if (end < 0) end = row.Length;
        return row[..start] + value + row[end..];
    }
    private static string[] Replace(IReadOnlyList<string> rows, int index, string value)
    { string[] copy = rows.ToArray(); copy[index] = value; return copy; }
    private static void Reject(Fixture f, IReadOnlyList<string>? rows = null, byte[]? image = null, Reads.Boundary? boundary = null)
    {
        bool rejected = false;
        try { _ = Reads.Inspect(rows ?? f.Rows, image ?? f.Image, boundary ?? f.Boundary); }
        catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected);
    }
    private sealed class CountOnly(int count) : IReadOnlyList<string>
    {
        public int Count => count;
        public string this[int index] => throw new InvalidOperationException("count_rejection_must_precede_indexing");
        public IEnumerator<string> GetEnumerator() => throw new InvalidOperationException("count_rejection_must_precede_enumeration");
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
