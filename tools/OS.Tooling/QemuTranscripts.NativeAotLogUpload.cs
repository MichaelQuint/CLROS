using System.Collections.ObjectModel;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    public const int MaximumNativeAotLogUploadWaitRows = 65536;
    /// <summary>Synchronously borrow bounded original rows and caller-validated
    /// capacity values, returning independent immutable observations. All CALLs
    /// require exact full numeric fields and the explicit source0..5 finite
    /// ordinal reader. Source5 requires immediate BEGIN, an observed Ready wake,
    /// matching completion, and immediate repark when blocked again. Per-thread
    /// sequences may jump; each attachment has one active request. Return bits
    /// may describe errors or shorts and grant no user-copy or success authority.
    /// Teardown action4 and early interrupted completions without an observed
    /// wake are unsupported and throw, as do incomplete or conflicting envelopes.
    /// Syscall-specific arguments, requested bytes, RESUME, signals, actual
    /// scheduler/resources and every residual row remain outer obligations.
    /// Table clocks are compared only with other table clocks, never device IO.
    /// No input is retained or modified; there is no I/O, callback or execution.</summary>

    public static NativeAotLogUploadWaitReport ReadNativeAotLogUploadWritableWaits(string[] originalRows, uint descriptorCapacity, uint copiedOutputCapacity)
    {
        LavNeed(originalRows is { Length: > 0 and <= MaximumNativeAotLogUploadWaitRows } && descriptorCapacity is >= 4 and <= 32 && copiedOutputCapacity is >= 1 and <= 8192, "input");
        string[] rows = originalRows!.ToArray();
        LavNeed(rows.All(row => row is { Length: > 0 and <= 2048 } && row.All(c => c is >= ' ' and <= '~')), "input");
        var observations = ReadLinuxWritableDiskCallObservations(rows);
        var waits = observations.Waits.ToDictionary(row => row.RecordIndex);
        List<NativeAotLogUploadWaitRow> calls = [], envelopes = [], readonlyWakes = [];
        List<NativeAotLogUploadWaitOperation> operations = [];
        List<WritableDiskRow> residual = [];
        Dictionary<ulong, LavPending> active = [];
        Dictionary<(ulong Owner, ulong Epoch), LavPending> attachments = [];
        Dictionary<(ulong Owner, ulong Epoch), ulong> generations = [];
        Dictionary<ulong, ulong> sequences = [];
        ulong clock = 0;
        for (int i = 0; i < rows.Length; i++)
        {
            string tag = rows[i].Split(' ', 2)[0];
            if (tag == "M16_008_CALL")
            {
                var call = LavRead(rows, i, tag, LavCallFields);
                calls.Add(call);
                LavClock(call, ref clock);
                LavNeed(waits.TryGetValue(i, out var wait) && !active.ContainsKey(call["identity"]), "call_while_waiting");
                LavNeed(call["action"] <= 6 && call["status"] <= 9, "call_result");
                if (wait.Source != 5)
                    continue;
                LavNeed(call["action"] == 3 && call["status"] == 9 && call["valueBits"] == 0 && call["returnValueBits"] == 0, "blocked_call");
                LavNeed(i + 1 < rows.Length && rows[i + 1].StartsWith("M16_027_BEGIN ", StringComparison.Ordinal), "begin_missing");
                var begin = LavRead(rows, ++i, "M16_027_BEGIN", LavBeginFields);
                LavClock(begin, ref clock);
                LavBegin(begin, descriptorCapacity, copiedOutputCapacity);
                LavNeed(begin["identity"] == wait.Identity && begin["ordinal"] == wait.Ordinal && begin["sequence"] == wait.Sequence && begin["nr"] == call["nr"] && begin["rip"] == call["rip"], "begin_call");
                var attachment = (begin["requestOwner"], begin["requestEpoch"]);
                generations.TryGetValue(attachment, out ulong generation);
                sequences.TryGetValue(wait.Identity, out ulong sequence);
                LavNeed(!attachments.ContainsKey(attachment) && begin["request"] > generation && wait.Sequence > sequence, "request_reuse");
                var pending = new LavPending(call, begin);
                active.Add(wait.Identity, pending);
                attachments.Add(attachment, pending);
                generations[attachment] = begin["request"];
                sequences[wait.Identity] = wait.Sequence;
                envelopes.Add(begin);
                continue;
            }

            if (tag == "M16_027_WAKE")
            {
                var wake = LavRead(rows, i, tag, LavWakeFields);
                LavWakeShape(wake);
                LavClock(wake, ref clock);
                if (wake["source"] == 4)
                {
                    readonlyWakes.Add(wake);
                    residual.Add(new(i, rows[i]));
                    continue;
                }

                LavNeed(active.TryGetValue(wake["identity"], out var pending), "wake_unmatched");
                LavMatch(wake, pending!, "outputPrefix");
                LavNeed(pending!.Wake is null && wake["completion"] is 1 or 5 && wake["state"] == 1, "wake_state");
                pending.Wake = wake;
                envelopes.Add(wake);
                continue;
            }

            if (tag == "M16_027_COMPLETE")
            {
                var complete = LavRead(rows, i, tag, LavCompleteFields);
                LavClock(complete, ref clock);
                LavNeed(complete["action"] != 4, "teardown_unsupported");
                LavNeed(active.TryGetValue(complete["identity"], out var pending), "complete_unmatched");
                LavMatch(complete, pending!, "outputBytes");
                LavNeed(pending!.Wake is not null, "complete_without_wake_unsupported");
                LavNeed(complete["nr"] == pending.Call["nr"] && complete["rip"] == pending.Call["rip"] && complete["completion"] == pending.Wake!["completion"] && complete["action"] is 1 or 3, "complete_result");
                envelopes.Add(complete);
                NativeAotLogUploadWaitRow? repark = null;
                if (complete["action"] == 3)
                {
                    LavNeed(complete["valueBits"] == 0 && i + 1 < rows.Length && rows[i + 1].StartsWith("M16_027_REPARK ", StringComparison.Ordinal), "repark_missing");
                    repark = LavRead(rows, ++i, "M16_027_REPARK", LavReparkFields);
                    LavClock(repark, ref clock);
                    LavNeed(repark["identity"] == pending.Call["identity"] && repark["previous"] == pending.Sequence && repark["sequence"] > pending.Sequence && repark["request"] == pending.Begin["request"], "repark_identity");
                    pending.Sequence = repark["sequence"];
                    sequences[repark["identity"]] = pending.Sequence;
                    envelopes.Add(repark);
                }

                pending.Attempts.Add(new(pending.Wake!, complete, repark));
                pending.Wake = null;
                if (repark is null)
                {
                    active.Remove(complete["identity"]);
                    attachments.Remove((complete["requestOwner"], complete["requestEpoch"]));
                    operations.Add(new(pending.Call, pending.Begin, pending.Attempts));
                }

                continue;
            }

            LavNeed(tag is not ("M16_027_BEGIN" or "M16_027_REPARK"), "transition_unmatched");
            LavNeed(!tag.StartsWith("M16_027", StringComparison.Ordinal) || tag is "M16_027_IO" or "M16_027_STATE" or "M16_027_BOUND" or "M16_027_STOP" or "M16_027_END", "unknown_writable_record");
            residual.Add(new(i, rows[i]));
        }

        LavNeed(active.Count == 0 && attachments.Count == 0, "wait_unresolved");
        LavNeed(calls.Count + envelopes.Count + residual.Count == rows.Length, "census");
        return new(rows.Length, descriptorCapacity, copiedOutputCapacity, calls, envelopes, operations, residual, readonlyWakes);
    }

    private const string LavCallFields = "identity nr a0 a1 a2 a3 a4 a5 rip rsp flags fs gs clock valueBits action status ordinal waitSource waitDeadline waitSequence signalDepth signalFrame blockedSignals returnRip returnRsp returnFlags returnFs returnGs returnValueBits";
    private const string LavBeginFields = "identity ordinal sequence requestOwner requestEpoch request nr rip outputPrefix fdOwner fd fdGeneration description bindingGeneration reservedFd reservedDescription reservationGeneration providerOwner providerEpoch providerRequest operation phase clock";
    private const string LavWakeFields = "identity sequence source requestOwner requestEpoch request outputPrefix completion state clock";
    private const string LavCompleteFields = "identity sequence requestOwner requestEpoch request nr rip outputBytes completion action valueBits clock";
    private const string LavReparkFields = "identity previous sequence request clock";
    private static NativeAotLogUploadWaitRow LavRead(string[] rows, int index, string tag, string fields) => new(index, rows[index], ReadRecord(rows[index], tag, fields.Split(' ').Select(field => field + ":dec").ToArray()));
    private static void LavNeed(bool value, string reason) => Need(value, "log_upload_wait_" + reason);
    private static void LavClock(NativeAotLogUploadWaitRow row, ref ulong prior)
    {
        LavNeed(row["clock"] >= prior, "clock");
        prior = row["clock"];
    }

    private static bool LavIdentity(ulong identity) => (identity >> 32) != 0 && (uint)identity != 0;
    private static void LavBegin(NativeAotLogUploadWaitRow b, uint descriptors, uint output)
    {
        LavNeed(LavIdentity(b["identity"]) && b["ordinal"] is >= 1 and <= 4096 && b["sequence"] != 0 && b["requestOwner"] != 0 && b["requestEpoch"] != 0 && b["request"] != 0 && b["outputPrefix"] <= output && b["fd"] < descriptors && b["description"] < descriptors && b["reservedFd"] < descriptors && b["reservedDescription"] < descriptors && b["fdGeneration"] <= uint.MaxValue && b["reservationGeneration"] <= uint.MaxValue && (b["fdOwner"] == 0 ? b["fd"] == 0 && b["fdGeneration"] == 0 : b["fdGeneration"] != 0) && (b["providerOwner"] == 0 ? b["providerEpoch"] == 0 && b["providerRequest"] == 0 : b["providerEpoch"] != 0 && b["providerRequest"] != 0) && b["operation"] is >= 1 and <= 7 && b["phase"] is 1 or 2, "begin_values");
    }

    private static void LavWakeShape(NativeAotLogUploadWaitRow w) => LavNeed(LavIdentity(w["identity"]) && w["sequence"] != 0 && w["source"] is 4 or 5 && w["requestOwner"] != 0 && w["requestEpoch"] != 0 && w["request"] != 0 && w["outputPrefix"] <= uint.MaxValue && w["completion"] is >= 1 and <= 5 && w["state"] <= 8, "wake_values");
    private static void LavMatch(NativeAotLogUploadWaitRow row, LavPending p, string output) => LavNeed(row["identity"] == p.Begin["identity"] && row["sequence"] == p.Sequence && row["requestOwner"] == p.Begin["requestOwner"] && row["requestEpoch"] == p.Begin["requestEpoch"] && row["request"] == p.Begin["request"] && row[output] == p.Begin["outputPrefix"], "request_identity");
}
