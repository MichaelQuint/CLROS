using System.Globalization;
using OS.Tooling;
using Q = OS.Tooling.QemuTranscripts;

// Independently authored synthetic copied rows only. No ELF, native producer,
// scheduler, user copy, device, physical persistence or original capture proof.
internal static class NativeAotLogUploadCaptureTests
{
    private const ulong Main = 4294967297, Peer = 4294967298;

    [HostTest("log-upload wait values retain every original index and full numeric call without capture authority")]
    private static void Census()
    {
        string[] rows = Simple();
        string[] original = rows.ToArray();
        var report = Read(rows);
        HostCheck.True(report.OriginalRowCount == rows.Length && report.Calls.Count == 1 && report.EnvelopeRows.Count == 3 &&
            report.Operations.Count == 1 && report.ResidualRows.Count == 4 && report.UnjoinedSource4Wakes.Count == 0);
        HostCheck.True(report.Calls[0].RecordIndex == 1 && report.Calls[0]["a2"] == 19 && report.Calls[0]["rsp"] == 8000000 &&
            report.Calls[0]["returnRsp"] == 8000000 && report.Calls[0]["waitSource"] == 5 &&
            report.Operations[0].Begin.RecordIndex == 2 && report.Operations[0].Attempts[0].Wake.RecordIndex == 4 &&
            report.Operations[0].Attempts[0].Complete.RecordIndex == 5 && report.Operations[0].Attempts[0].Repark is null);
        int[] census = report.Calls.Select(row => row.RecordIndex).Concat(report.EnvelopeRows.Select(row => row.RecordIndex))
            .Concat(report.ResidualRows.Select(row => row.RecordIndex)).Order().ToArray();
        HostCheck.True(census.SequenceEqual(Enumerable.Range(0, rows.Length)) && rows.SequenceEqual(original) &&
            report.ResidualRows[^1].Text == "FUTURE_OUTER_RECORD remains=unqualified" &&
            !report.GrantsSchedulerAuthority && !report.GrantsPackageOrProcessAuthority && !report.GrantsGuestOrNativeAuthority &&
            !report.GrantsWholeCaptureQualification && !report.GrantsPhysicalOrPersistenceAuthority);
    }

    [HostTest("log-upload wait values own immutable calls envelopes attempts and residual census")]
    private static void Immutable()
    {
        string[] rows = Simple();
        var report = Read(rows);
        rows[1] = "changed";
        rows[7] = "changed";
        HostCheck.True(report.Calls[0]["nr"] == 17 && report.Calls[0].Text.StartsWith("M16_008_CALL ", StringComparison.Ordinal) &&
            report.ResidualRows[^1].Text == "FUTURE_OUTER_RECORD remains=unqualified");
        bool denied = false;
        try
        {
            ((IDictionary<string, ulong>)report.Calls[0].Values)["nr"] = 99;
        }
        catch (NotSupportedException)
        {
            denied = true;
        }
        HostCheck.True(denied);
        denied = false;
        try
        {
            ((IList<Q.NativeAotLogUploadWaitAttempt>)report.Operations[0].Attempts).Clear();
        }
        catch (NotSupportedException)
        {
            denied = true;
        }
        HostCheck.True(denied);
        denied = false;
        try
        {
            ((IList<Q.WritableDiskRow>)report.ResidualRows).Clear();
        }
        catch (NotSupportedException)
        {
            denied = true;
        }
        HostCheck.True(denied);
    }

    [HostTest("log-upload wait values preserve short EOF EIO and observed interrupted completion bits")]
    private static void ReturnFacts()
    {
        foreach (ulong value in new[] { 0UL, 7UL, unchecked((ulong)-5L), unchecked((ulong)-4L) })
        {
            string[] rows = Simple();
            rows[5] = Set(rows[5], "valueBits", value);
            if (value == unchecked((ulong)-4L))
            {
                rows[4] = Set(rows[4], "completion", 5);
                rows[5] = Set(rows[5], "completion", 5);
            }
            var report = Read(rows);
            var attempt = report.Operations[0].Attempts[0];
            HostCheck.True(attempt.Complete["valueBits"] == value && attempt.Complete["outputBytes"] == 19 &&
                attempt.Wake["completion"] == attempt.Complete["completion"] && !report.GrantsSchedulerAuthority);
        }
        // Prefix zero is an actual captured fact, including for a requested read;
        // requested-byte/copy semantics remain the separate original consumer.
        string[] zero = Simple();
        foreach (int i in new[] { 2, 4, 5 })
            zero[i] = Set(zero[i], i == 5 ? "outputBytes" : "outputPrefix", 0);
        HostCheck.True(Read(zero).Operations[0].Begin["outputPrefix"] == 0);
    }

    [HostTest("log-upload wait values join repeated observed reparks with jumping sequences and unchanged request")]
    private static void Repark()
    {
        string[] rows = [Call(), Begin(), Wake(clock: 101), Complete(action: 3, value: 0, clock: 102), ReparkRow(1, 7, 103),
            "M16_027_IO outside=table-clock-domain clock=0", Wake(sequence: 7, clock: 103), Complete(sequence: 7, action: 3, value: 0, clock: 104),
            ReparkRow(7, 11, 105), Wake(sequence: 11, completion: 5, clock: 106), Complete(sequence: 11, completion: 5, value: unchecked((ulong)-4L), clock: 107)];
        var result = Read(rows);
        var attempts = result.Operations[0].Attempts;
        HostCheck.True(attempts.Count == 3 && attempts[0].Repark!["sequence"] == 7 && attempts[1].Repark!["sequence"] == 11 &&
            attempts[2].Repark is null && result.ResidualRows.Single().RecordIndex == 5 && result.EnvelopeRows.Count == 9);
        Reject(Change(rows, 4, "previous", 2), "repark_identity");
        Reject(Change(rows, 4, "sequence", 1), "repark_identity");
        Reject(Change(rows, 4, "request", 8), "repark_identity");
        Reject(Change(rows, 6, "sequence", 1), "request_identity");
        Reject(Change(rows, 7, "valueBits", 1), "repark_missing");
        Reject(rows.Where((_, index) => index != 4).ToArray(), "repark_missing");
        Reject(rows.Append(ReparkRow(11, 12, 108)).ToArray(), "transition_unmatched");
    }

    [HostTest("log-upload wait values allow unrelated identities and explicitly unjoined readonly wakes")]
    private static void Interleaving()
    {
        string[] rows = [Call(), Begin(), Call(identity: Peer, source: 4, sequence: 10, clock: 100),
            Wake(identity: Peer, sequence: 10, source: 4, prefix: 20000, clock: 101),
            Call(identity: 8589934595, source: 0, sequence: 0, clock: 101), Wake(clock: 102), Complete(clock: 103)];
        var report = Read(rows);
        HostCheck.True(report.Calls.Count == 3 && report.Operations.Count == 1 && report.UnjoinedSource4Wakes.Single().RecordIndex == 3 &&
            report.UnjoinedSource4Wakes[0]["outputPrefix"] == 20000 && report.ResidualRows.Single().RecordIndex == 3 && report.EnvelopeRows.Count == 3);
        Reject(Change(rows, 3, "source", 3), "wake_values");
        Reject(Change(rows, 3, "outputPrefix", (ulong)uint.MaxValue + 1), "wake_values");
        Reject(Change(rows, 3, "state", 9), "wake_values");
        Reject(Change(rows, 3, "completion", 0), "wake_values");
        Reject(Change(rows, 3, "identity", 1), "wake_values");
        Reject(rows.Select((row, i) => i == 3 ? row + " injected=0" : row).ToArray());
    }

    [HostTest("log-upload wait values retain known device rows for outer joins and reject unknown writable record families")]
    private static void ResidualFamilies()
    {
        string[] original = Simple();
        string[] rows = original.Concat(new[] { "M16_027_STATE", "M16_027_BOUND", "M16_027_STOP", "M16_027_END" }
            .Select(tag => tag + " synthetic=unjoined-requires-independent-parser")).ToArray();
        var report = Read(rows);
        HostCheck.True(report.ResidualRows.Count == 8 && report.ResidualRows[^1].RecordIndex == rows.Length - 1 &&
            !report.GrantsWholeCaptureQualification && report.ResidualRows[^1].Text == rows[^1]);
        foreach (string tag in new[] { "M16_027_UNKNOWN", "M16_027_BEGNI", "M16_027_WAKE_LATE", "M16_027_COMPLETE_EXTRA",
            "M16_027_IO_EXTRA", "M16_027_BOUNDARY", "M16_027", "M16_027x" })
            Reject(original.Append(tag + " unexpected=1").ToArray(), "unknown_writable_record");
        // The namespace rule does not discard unrelated future parent evidence.
        HostCheck.True(Read(original.Append("M16_028_FUTURE preserved=1").ToArray()).ResidualRows[^1].Text == "M16_028_FUTURE preserved=1");
    }

    [HostTest("log-upload wait values prohibit another call on a waiting identity and singleton attachment overlap")]
    private static void Exclusivity()
    {
        string[] rows = [Call(), Begin(), Call(identity: Peer, clock: 101), Begin(identity: Peer, request: 2, clock: 101),
            Wake(clock: 102), Complete(clock: 103), Wake(identity: Peer, request: 2, clock: 104), Complete(identity: Peer, request: 2, clock: 105)];
        Reject(rows, "request_reuse");
        // Different attachments are representable copied facts; an original owner
        // must independently bind its actual single attachment and process.
        foreach (int i in new[] { 3, 6, 7 })
            rows[i] = Set(rows[i], "requestOwner", 9000);
        HostCheck.True(Read(rows).Operations.Count == 2);
        Reject([Call(), Begin(), Call(ordinal: 2, source: 0, sequence: 0, clock: 101), Wake(clock: 102), Complete(clock: 103)], "call_while_waiting");
        string[] serial = [Call(), Begin(), Wake(), Complete(), Call(ordinal: 2, sequence: 8, clock: 104),
            Begin(ordinal: 2, sequence: 8, request: 2, clock: 104), Wake(sequence: 8, request: 2, clock: 105), Complete(sequence: 8, request: 2, clock: 106)];
        HostCheck.True(Read(serial).Operations.Count == 2);
        foreach (int index in new[] { 5, 6, 7 })
            serial[index] = Set(serial[index], "request", 1);
        Reject(serial, "request_reuse");
    }

    [HostTest("log-upload wait values reject orphan duplicate missing late and unsupported teardown transitions")]
    private static void Transitions()
    {
        string[] basic = [Call(), Begin(), Wake(), Complete()];
        for (int i = 0; i < basic.Length; i++)
            Reject(basic.Where((_, index) => index != i).ToArray());
        for (int i = 0; i < basic.Length; i++)
            Reject(basic.Take(i + 1).Concat([basic[i]]).Concat(basic.Skip(i + 1)).ToArray());
        Reject([Call(), "unrelated interrupts=immediate-BEGIN", Begin(), Wake(), Complete()], "begin_missing");
        Reject([Call(), Begin(), Complete(completion: 5, value: unchecked((ulong)-4L))], "complete_without_wake_unsupported");
        Reject([Call(), Begin(), Wake(), Complete(action: 4)], "teardown_unsupported");
        Reject([Call(), Begin(), Wake(), Complete(action: 0)], "complete_result");
        Reject([Call(), Begin(), Wake(), Complete(), Wake(clock: 104)], "wake_unmatched");
        Reject([Call(), Begin(), Wake()], "wait_unresolved");
        Reject([Call(), Begin(), Wake(), Complete(action: 3, value: 0), ReparkRow(1, 2, 103)], "wait_unresolved");
    }

    [HostTest("log-upload wait values join exact owner epoch generation identity syscall RIP and original prefix")]
    private static void JoinedIdentity()
    {
        string[] rows = Simple();
        foreach (string field in new[] { "identity", "ordinal", "sequence", "nr", "rip" })
            Reject(Change(rows, 2, field, field == "identity" ? Peer : 999), "begin_call");
        foreach (int index in new[] { 4, 5 })
        {
            foreach (string field in new[] { "identity", "sequence", "requestOwner", "requestEpoch", "request" })
                Reject(Change(rows, index, field, 999));
            Reject(Change(rows, index, index == 4 ? "outputPrefix" : "outputBytes", 18), "request_identity");
        }
        foreach (string field in new[] { "nr", "rip", "completion" })
            Reject(Change(rows, 5, field, 999), "complete_result");
        foreach (ulong state in new[] { 0UL, 2UL, 3UL, 4UL, 5UL, 6UL, 8UL })
            Reject(Change(rows, 4, "state", state), "wake_state");
        foreach (ulong completion in new[] { 2UL, 3UL, 4UL })
            Reject(Change(rows, 4, "completion", completion), "wake_state");
        Reject(Change(rows, 2, "clock", 99), "clock");
        Reject(Change(rows, 4, "clock", 99), "clock");
        Reject(Change(rows, 5, "clock", 100), "clock");
    }

    [HostTest("log-upload wait values bound copied descriptor capacity prefix and actual field widths")]
    private static void Bounds()
    {
        string[] rows = Simple();
        foreach (uint descriptors in new[] { 0U, 3U, 33U, uint.MaxValue })
            Reject(rows, "input", descriptors);
        foreach (uint output in new[] { 0U, 8193U, uint.MaxValue })
            Reject(rows, "input", output: output);
        foreach (string field in new[] { "fd", "description", "reservedFd", "reservedDescription" })
            Reject(Change(rows, 2, field, 32), "begin_values");
        foreach (string field in new[] { "fdGeneration", "reservationGeneration" })
            Reject(Change(rows, 2, field, (ulong)uint.MaxValue + 1), "begin_values");
        Reject(Change(rows, 2, "outputPrefix", 20), "begin_values", output: 19);
        foreach (string field in new[] { "requestOwner", "requestEpoch", "request", "fdGeneration", "providerEpoch", "providerRequest" })
            Reject(Change(rows, 2, field, 0), "begin_values");
        foreach (ulong operation in new[] { 0UL, 8UL, 256UL })
            Reject(Change(rows, 2, "operation", operation), "begin_values");
        foreach (ulong phase in new[] { 0UL, 3UL, 256UL })
            Reject(Change(rows, 2, "phase", phase), "begin_values");
        // Both admitted byte phases, every actual operation, and nullable provider
        // tokens are copied shapes, not syscall/operation semantic permission.
        foreach (ulong operation in Enumerable.Range(1, 7).Select(i => (ulong)i))
            HostCheck.True(Read(Change(Change(rows, 2, "operation", operation), 2, "phase", 2)).Operations.Count == 1);
        string[] optional = rows.ToArray();
        foreach (string field in new[] { "fdOwner", "fd", "fdGeneration", "providerOwner", "providerEpoch", "providerRequest" })
            optional[2] = Set(optional[2], field, 0);
        HostCheck.True(Read(optional).Operations.Count == 1);
        foreach (int i in new[] { 2, 4, 5 })
            rows[i] = Set(rows[i], i == 5 ? "outputBytes" : "outputPrefix", 8192);
        HostCheck.True(Read(rows).Operations[0].Begin["outputPrefix"] == 8192);
        rows[2] = Set(rows[2], "fd", 3);
        foreach (int i in new[] { 2, 4, 5 })
            rows[i] = Set(rows[i], i == 5 ? "outputBytes" : "outputPrefix", 1);
        HostCheck.True(Read(rows, descriptors: 4, output: 1).Operations[0].Begin["outputPrefix"] == 1);
    }

    [HostTest("log-upload wait values require exact full CALL schema finite ordinals and source5 blocked result")]
    private static void Calls()
    {
        string[] rows = Simple();
        foreach ((string field, ulong value) in new (string, ulong)[] { ("action", 1), ("status", 1), ("valueBits", 1), ("returnValueBits", 1) })
            Reject(Change(rows, 1, field, value), "blocked_call");
        foreach ((string field, ulong value) in new (string, ulong)[] { ("waitSource", 6), ("waitSequence", 0), ("waitDeadline", 1), ("identity", 1), ("ordinal", 4097) })
            Reject(Change(rows, 1, field, value));
        foreach (string addition in new[] { " outputPrefix=19", " extra=0", " a0=0" })
        {
            var copy = rows.ToArray();
            copy[1] += addition;
            Reject(copy);
        }
        foreach (string removed in new[] { " a0=5", " rsp=8000000", " clock=100", " returnFs=7000000" })
        {
            var copy = rows.ToArray();
            copy[1] = copy[1].Replace(removed, "", StringComparison.Ordinal);
            HostCheck.True(copy[1] != rows[1], "negative vector must actually remove a field");
            Reject(copy);
        }
        Reject([Call(ordinal: 2), Begin(ordinal: 2), Wake(), Complete()]);
        Reject([Call(), Begin(), Wake(), Complete(), Call(ordinal: 3, source: 0, sequence: 0, clock: 104)]);
        Reject([Call(), Begin(), Wake(), Complete(), Call(ordinal: 1, source: 0, sequence: 0, clock: 104)]);
    }

    [HostTest("log-upload wait values reject malformed unknown fields noncanonical decimals overflow and bounded input")]
    private static void Malformed()
    {
        string[] rows = Simple();
        foreach (int index in new[] { 1, 2, 4, 5 })
        {
            foreach (string value in new[] { "00", "+1", "01", "0x1", "18446744073709551616", "-1", "one" })
            {
                var copy = rows.ToArray();
                copy[index] = ReplaceValue(copy[index], "identity", value);
                Reject(copy);
            }
            foreach (string suffix in new[] { " unknown=0", " ", "\t", "\0" })
            {
                var copy = rows.ToArray();
                copy[index] += suffix;
                Reject(copy);
            }
        }
        Reject(null!, "input");
        Reject([], "input");
        Reject([null!], "input");
        Reject([""], "input");
        Reject([new string('x', 2049)], "input");
        Reject(["non-ascii=é"], "input");
        string[] maximum = Enumerable.Repeat("UNJOINED_OUTER_ROW", Q.MaximumNativeAotLogUploadWaitRows - 4)
            .Concat([Call(), Begin(), Wake(), Complete()]).ToArray();
        HostCheck.True(Read(maximum).OriginalRowCount == Q.MaximumNativeAotLogUploadWaitRows);
        Reject(maximum.Append("overflow").ToArray(), "input");
    }

    private static Q.NativeAotLogUploadWaitReport Read(string[] rows, uint descriptors = 32, uint output = 8192) =>
        Q.ReadNativeAotLogUploadWritableWaits(rows, descriptors, output);
    private static string[] Simple() => ["TEST_START outer-synthetic", Call(), Begin(), "M16_027_IO synthetic=unjoined clock=999999999",
        Wake(), Complete(), "M16_008_RESUME identity=4294967297 valueBits=19 status=1", "FUTURE_OUTER_RECORD remains=unqualified"];
    private static string Call(ulong identity = Main, ulong ordinal = 1, uint source = 5, ulong sequence = 1, ulong clock = 100) =>
        FormattableString.Invariant($"M16_008_CALL identity={identity} nr=17 a0=5 a1=6000000 a2=19 a3=11 a4=0 a5=0 rip=4231031 rsp=8000000 flags=2 fs=7000000 gs=0 clock={clock} valueBits=0 action={(source == 0 ? 1 : 3)} status={(source == 0 ? 1 : 9)} ordinal={ordinal} waitSource={source} waitDeadline=0 waitSequence={sequence} signalDepth=0 signalFrame=0 blockedSignals=0 returnRip=4231031 returnRsp=8000000 returnFlags=2 returnFs=7000000 returnGs=0 returnValueBits=0");
    private static string Begin(ulong identity = Main, ulong ordinal = 1, ulong sequence = 1, ulong request = 1, ulong clock = 100) =>
        FormattableString.Invariant($"M16_027_BEGIN identity={identity} ordinal={ordinal} sequence={sequence} requestOwner=8000 requestEpoch=12 request={request} nr=17 rip=4231031 outputPrefix=19 fdOwner=9000 fd=5 fdGeneration=7 description=2 bindingGeneration=9 reservedFd=0 reservedDescription=0 reservationGeneration=0 providerOwner=10000 providerEpoch=12 providerRequest=77 operation=2 phase=1 clock={clock}");
    private static string Wake(ulong identity = Main, ulong sequence = 1, uint source = 5, ulong request = 1,
        ulong prefix = 19, ulong completion = 1, ulong clock = 101) =>
        FormattableString.Invariant($"M16_027_WAKE identity={identity} sequence={sequence} source={source} requestOwner=8000 requestEpoch=12 request={request} outputPrefix={prefix} completion={completion} state=1 clock={clock}");
    private static string Complete(ulong identity = Main, ulong sequence = 1, ulong request = 1, ulong completion = 1,
        ulong action = 1, ulong value = 19, ulong clock = 102) =>
        FormattableString.Invariant($"M16_027_COMPLETE identity={identity} sequence={sequence} requestOwner=8000 requestEpoch=12 request={request} nr=17 rip=4231031 outputBytes=19 completion={completion} action={action} valueBits={value} clock={clock}");
    private static string ReparkRow(ulong previous, ulong sequence, ulong clock) =>
        FormattableString.Invariant($"M16_027_REPARK identity=4294967297 previous={previous} sequence={sequence} request=1 clock={clock}");
    private static string[] Change(string[] rows, int index, string field, ulong value)
    {
        var copy = rows.ToArray();
        copy[index] = Set(copy[index], field, value);
        return copy;
    }
    private static string Set(string row, string field, ulong value) => ReplaceValue(row, field, value.ToString(CultureInfo.InvariantCulture));
    private static string ReplaceValue(string row, string field, string value) => string.Join(' ', row.Split(' ').Select(part =>
        part.StartsWith(field + "=", StringComparison.Ordinal) ? field + "=" + value : part));
    private static void Reject(string[] rows, string reason = "", uint descriptors = 32, uint output = 8192)
    {
        try
        {
            _ = Read(rows, descriptors, output);
        }
        catch (ToolFailure failure)
        {
            HostCheck.True(reason.Length == 0 || failure.Message == "log_upload_wait_" + reason, "unexpected rejection: " + failure.Message);
            return;
        }
        HostCheck.True(false, "copied malformed rows accepted: " + reason);
    }
}
