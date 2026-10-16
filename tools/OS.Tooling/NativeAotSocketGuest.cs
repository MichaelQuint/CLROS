using System.Globalization;
using System.Text.RegularExpressions;

namespace OS.Tooling;

/// <summary>Value-only decoding of original socket stdout and epoll wait
/// observations. Bindings reject orphan/duplicate completions and malformed
/// repark/resume records. Complete scheduler, packet, sealed-program and parent
/// provenance validation remain separate; these reports grant no guest authority.</summary>
public static class NativeAotSocketGuest
{
    /// <summary>Which serialized observation was captured from the real owner.</summary>
    public enum ObservationKind { Wake = 1, Repark = 2, Complete = 3, Poll = 4, Expire = 5 }
    /// <summary>An actual poll, retained source3 completion or repark record. Poll
    /// Identity is zero on the supervisor or the current worker; Ready is the
    /// actual FIFO count. Idle marks one completed native outer idle step.
    /// Immediate Expire retains the preceding call's Ordinal and actual deadline.
    /// A nonzero
    /// PreviousSequence denotes repark; other fields retain canonical kernel
    /// enum values and exact observed table clock, without execution authority.</summary>
    public readonly record struct Readiness(int RecordIndex, ObservationKind Kind, ulong Identity, ulong Sequence, uint Completion, uint State,
        ulong Clock, ulong PreviousSequence, ulong Deadline, ulong ValueBits, uint Ready = 0, bool Idle = false, ulong Ordinal = 0);

    /// <summary>Owned immutable derived text and observations. PendingWaits may
    /// include waits later retired by process exit; this is not a drained report.</summary>
    public sealed class Capture
    {
        internal Capture(NativeAotThreadingOutput.Capture output, Readiness[] readiness,
            IReadOnlyList<QemuTranscripts.LinuxCallWaitObservation> waits, int pending)
        { Output = output; Readiness = Array.AsReadOnly(readiness); Waits = waits; PendingWaits = pending; }
        public NativeAotThreadingOutput.Capture Output { get; }
        public IReadOnlyList<Readiness> Readiness { get; }
        public IReadOnlyList<QemuTranscripts.LinuxCallWaitObservation> Waits { get; }
        public int PendingWaits { get; }
    }

    /// <summary>Borrow a raw inner M16-009 tail, decode exact fd1 byte envelopes
    /// and full selected app stdout, then bind epoll completion/repark/resume to
    /// recorded generation/sequence. Input is not retained or executed. Invalid
    /// UTF8, malformed records, unsupported states or inconsistent bindings throw.
    /// This deliberately does not accept a complete guest lifecycle by itself.</summary>
    public static Capture Read(bool asynchronous, ReadOnlySpan<byte> rawTail)
    {
        var output = NativeAotThreadingOutput.Read(asynchronous ? "NativeAotSocketSmokeAsync" : "NativeAotSocketSmoke", rawTail);
        if (asynchronous)
        {
            var observed = NativeAotSocketPayload.ValidateAsyncOutput(output.Stdout);
            Need(observed.PendingAccepts > 0 && observed.PendingReceives > 0 && observed.PendingSends > 0, "pending_async_operations");
        }
        Need(output.NormalizedTail.EndsWith("\r\n", StringComparison.Ordinal), "raw_terminal");
        string[] rows = output.NormalizedTail[..^2].Split("\r\n", StringSplitOptions.None);
        var waits = QemuTranscripts.ReadLinuxCallWaits(rows, allowEpoll: true);
        _ = QemuTranscripts.ReadLinuxCallReturns(rows, allowEpoll: true);
        var calls = waits.ToDictionary(value => value.RecordIndex);
        Dictionary<ulong, (ulong Sequence, ulong Deadline, uint Completion, ulong? Returned)> pending = [];
        Dictionary<ulong, uint> activeSources = [];
        Dictionary<ulong, ulong> sequences = []; List<Readiness> readiness = []; ulong clock = 0;
        for (int i = 0; i < rows.Length; i++)
        {
            string row = rows[i];
            if (calls.TryGetValue(i, out var call) && call.Source != 0)
            {
                Need(!activeSources.ContainsKey(call.Identity) && call.Sequence > sequences.GetValueOrDefault(call.Identity), "wait_sequence");
                activeSources.Add(call.Identity, call.Source); sequences[call.Identity] = call.Sequence;
                if (call.Source == 3) pending.Add(call.Identity, (call.Sequence, call.Deadline, 0, null));
            }
            if (row.StartsWith("M16_012_POLL ", StringComparison.Ordinal))
            {
                ulong[] value = Record(row, "M16_012_POLL", "identity", "clock", "ready", "idle");
                Need((value[0]==0 || (uint)value[0] is >=1 and <=16 && value[0]>>32!=0) &&
                    value[1] >= clock && value[1] <= long.MaxValue && value[2] <= 16 && value[3]<=1 &&
                    (value[3]==0 || value[0]==0), "poll_state");
                clock = value[1];
                readiness.Add(new(i, ObservationKind.Poll, value[0], 0, 0, 0, clock, 0, 0, 0, (uint)value[2],value[3]==1));
            }
            else if (row.StartsWith("M16_012_EXPIRE ", StringComparison.Ordinal))
            {
                ulong[] value = Record(row, "M16_012_EXPIRE", "identity", "ordinal", "deadline", "clock");
                Need(calls.TryGetValue(i-1, out var previous) && previous.Identity==value[0] && previous.Ordinal==value[1] &&
                    previous.Source==0 && value[2]!=0 && value[2]<=value[3] && value[3]>=clock && value[3]<=long.MaxValue,"expiry_owner");
                clock=value[3];readiness.Add(new(i,ObservationKind.Expire,value[0],0,0,0,clock,0,value[2],0,Ordinal:value[1]));
            }
            else if (row.StartsWith("M16_012_WAKE ", StringComparison.Ordinal))
            {
                ulong[] value = Record(row, "M16_012_WAKE", "identity", "sequence", "completion", "state", "clock");
                Need(pending.TryGetValue(value[0], out var wait) && wait.Sequence == value[1] && wait.Completion == 0, "wake_owner");
                Need(value[2] is 1 or 2 or 5 && value[3] is 1 or 5 && value[4] >= clock && value[4] <= long.MaxValue, "wake_state");
                // A won completion may remain retained while the table clock
                // advances beyond its old deadline. Do not replace that winner.
                Need(value[2] != 2 || wait.Deadline != 0 && value[4] >= wait.Deadline, "wake_deadline");
                clock = value[4]; pending[value[0]] = (wait.Sequence, wait.Deadline, (uint)value[2], null);
                readiness.Add(new(i, ObservationKind.Wake, value[0], value[1], (uint)value[2], (uint)value[3], clock, 0, wait.Deadline, 0));
            }
            else if (row.StartsWith("M16_012_REPARK ", StringComparison.Ordinal))
            {
                ulong[] value = Record(row, "M16_012_REPARK", "identity", "previous", "sequence", "deadline", "clock");
                Need(pending.TryGetValue(value[0], out var wait) && wait.Sequence == value[1] && wait.Completion == 1 && wait.Returned is null &&
                    value[2] > wait.Sequence && value[3] == wait.Deadline && value[4] >= clock && value[4] <= long.MaxValue &&
                    (value[3] == 0 || value[4] < value[3]), "repark_owner");
                clock = value[4]; pending[value[0]] = (value[2], value[3], 0, null); sequences[value[0]] = value[2];
                readiness.Add(new(i, ObservationKind.Repark, value[0], value[2], 0, 0, clock, value[1], value[3], 0));
            }
            else if (row.StartsWith("M16_012_COMPLETE ", StringComparison.Ordinal))
            {
                ulong[] value = Record(row, "M16_012_COMPLETE", "identity", "sequence", "completion", "valueBits", "clock");
                Need(pending.TryGetValue(value[0], out var wait) && wait.Sequence == value[1] && wait.Completion != 0 &&
                    value[2] == wait.Completion && wait.Returned is null && value[4] >= clock && value[4] <= long.MaxValue, "completion_owner");
                long result = unchecked((long)value[3]);
                Need(wait.Completion == 5 ? result == -4 : result is >= 0 and <= 1024 &&
                    (result != 0 || wait.Deadline != 0 && value[4] >= wait.Deadline), "completion_result");
                clock = value[4]; pending[value[0]] = (wait.Sequence, wait.Deadline, wait.Completion, value[3]);
                readiness.Add(new(i, ObservationKind.Complete, value[0], value[1], (uint)value[2], 0, clock, 0, wait.Deadline, value[3]));
            }
            else if (row.StartsWith("M16_012_", StringComparison.Ordinal)) throw new ToolFailure("socket_guest_unknown_readiness");
            else if (row.StartsWith("M16_008_RESUME ", StringComparison.Ordinal))
            {
                ulong[] value = Record(row, "M16_008_RESUME", "identity", "valueBits", "status");
                Need(value[2] == 1, "resume_owner");
                Need(activeSources.Remove(value[0], out uint source), "resume_owner");
                // The complete protocol owns futex/pipe result semantics; their
                // explicit new wait is still required before another resume.
                if (source != 3) continue;
                Need(pending.TryGetValue(value[0], out var wait), "resume_owner");
                Need(wait.Returned.HasValue, "resume_owner");
                Need(value[1] == wait.Returned, "resume_result");
                pending.Remove(value[0]);
            }
        }
        return new(output, readiness.ToArray(), waits, pending.Count);
    }

    private static ulong[] Record(string row, string prefix, params string[] fields)
    {
        string[] values = row.Split(' '); Need(values.Length == fields.Length + 1 && values[0] == prefix, "record_shape");
        ulong[] result = new ulong[fields.Length];
        for (int i = 0; i < fields.Length; i++)
        {
            string name = fields[i] + "="; Need(values[i + 1].StartsWith(name, StringComparison.Ordinal), "record_field");
            string text = values[i + 1][name.Length..];
            Need(Regex.IsMatch(text, @"\A(0|[1-9][0-9]*)\z", RegexOptions.CultureInvariant) &&
                ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out _), "record_number");
            result[i] = ulong.Parse(text, CultureInfo.InvariantCulture);
        }
        return result;
    }
    private static void Need(bool condition, string reason) { if (!condition) throw new ToolFailure("socket_guest_" + reason); }
}
