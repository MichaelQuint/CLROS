using System.Globalization;
using System.Text;
using OS.Tooling;

internal static class NativeAotSocketGuestTests
{
    private const string Identity = "4294967297";
    private static string Call(uint source = 3, ulong sequence = 1, ulong deadline = 0, uint ordinal = 1) =>
        "M16_008_CALL identity=" + Identity + " nr=281 ordinal=" + ordinal + " waitSource=" + source + " waitDeadline=" + deadline +
        " waitSequence=" + sequence + " signalDepth=0 signalFrame=0 blockedSignals=0 returnRip=4194818 returnRsp=1879048176" +
        " returnFlags=514 returnFs=0 returnGs=0 returnValueBits=0\r\n";
    private static string Wake(uint completion = 1, ulong sequence = 1, ulong clock = 10, uint state = 1) =>
        $"M16_012_WAKE identity={Identity} sequence={sequence} completion={completion} state={state} clock={clock}\r\n";
    private static string Complete(uint completion = 1, long result = 2, ulong sequence = 1, ulong clock = 11) =>
        $"M16_012_COMPLETE identity={Identity} sequence={sequence} completion={completion} valueBits={unchecked((ulong)result)} clock={clock}\r\n";
    private static string Resume(long result = 2) => $"M16_008_RESUME identity={Identity} valueBits={unchecked((ulong)result)} status=1\r\n";
    private static byte[] Raw(string records = "", bool asynchronous = false, bool split = false, string? stdout = null)
    {
        byte[] payload = Encoding.UTF8.GetBytes(stdout ?? NativeAotSocketPayloadTests.FixtureOutput(asynchronous));
        using MemoryStream raw = new(); raw.Write("TEST_START m16-009\r\n"u8); raw.Write(Encoding.ASCII.GetBytes(records));
        int[] lengths = split ? [1, 19, payload.Length - 20] : [payload.Length]; int offset = 0;
        foreach (int length in lengths)
        {
            raw.Write(Encoding.ASCII.GetBytes("M16_005_WRITE fd=1 bytes=" + length.ToString(CultureInfo.InvariantCulture) + " data="));
            raw.Write(payload.AsSpan(offset, length)); raw.Write("\r\n"u8); offset += length;
        }
        raw.Write("CLROS_M16_009_READY\r\nTEST_PASS m16-009\r\n"u8); return raw.ToArray();
    }

    [HostTest("socket guest stdout preserves arbitrary write segmentation for both original modes")]
    private static void Framing()
    {
        foreach (bool asynchronous in new[] { false, true })
        {
            var capture = NativeAotSocketGuest.Read(asynchronous, Raw(asynchronous: asynchronous, split: true));
            HostCheck.Equal(NativeAotSocketPayloadTests.FixtureOutput(asynchronous), capture.Output.Stdout);
            HostCheck.Equal(3, capture.Output.WriteLengths.Count); HostCheck.Equal(0, capture.Readiness.Count);
            HostCheck.Equal(0, capture.PendingWaits);
            Reject(() => NativeAotSocketGuest.Read(!asynchronous, Raw(asynchronous: asynchronous)), "socket_");
        }
        string noPending = NativeAotSocketPayloadTests.FixtureOutput(true).Replace("accept_pending=4", "accept_pending=0", StringComparison.Ordinal);
        Reject(() => NativeAotSocketGuest.Read(true, Raw(asynchronous: true, stdout: noPending)), "pending_async_operations");
    }

    [HostTest("socket guest epoll wait source requires explicit admission and full current return observations")]
    private static void WaitSource()
    {
        string[] rows = Call(deadline: 100).TrimEnd('\r', '\n').Split("\r\n", StringSplitOptions.None);
        Reject(() => QemuTranscripts.ReadLinuxCallWaits(rows), "linux_call_ordinal_shape");
        var wait = QemuTranscripts.ReadLinuxCallWaits(rows, allowEpoll: true).Single();
        HostCheck.True(wait.Source == 3 && wait.Sequence == 1 && wait.Deadline == 100);
        HostCheck.Equal(1, QemuTranscripts.ReadLinuxCallReturns(rows, allowEpoll: true).Count);
        foreach (string text in new[] { Call(source: 4), Call(sequence: 0), Call(source: 2, deadline: 1), Call(deadline: ulong.MaxValue) })
            Reject(() => QemuTranscripts.ReadLinuxCallWaits([text.TrimEnd('\r', '\n')], allowEpoll: true), "linux_call_");
    }

    [HostTest("socket guest completion binds retained generation sequence clock value and actual resume")]
    private static void Completion()
    {
        var capture = NativeAotSocketGuest.Read(true, Raw(Call() + Wake() + Complete() + Resume(), asynchronous: true));
        HostCheck.True(capture.Readiness.Count == 2 && capture.Waits.Count == 1 && capture.PendingWaits == 0);
        HostCheck.Equal(NativeAotSocketGuest.ObservationKind.Wake, capture.Readiness[0].Kind);
        HostCheck.Equal(NativeAotSocketGuest.ObservationKind.Complete, capture.Readiness[1].Kind);
        HostCheck.Equal(2UL, capture.Readiness[1].ValueBits);
        // A report may retain a wait for the complete lifecycle consumer. It is
        // deliberately not a drained-process or guest-acceptance receipt.
        HostCheck.Equal(1, NativeAotSocketGuest.Read(false, Raw(Call())).PendingWaits);
        var mixed = NativeAotSocketGuest.Read(false, Raw(Call(source: 1) + Resume(0) + Call(sequence: 2, ordinal: 2) +
            Wake(sequence: 2) + Complete(sequence: 2) + Resume()));
        HostCheck.True(mixed.PendingWaits == 0 && mixed.Waits.Count == 2);
    }

    [HostTest("socket guest stolen readiness reparks the original deadline and advances the exact wait sequence")]
    private static void Repark()
    {
        string repark = $"M16_012_REPARK identity={Identity} previous=1 sequence=2 deadline=100 clock=12\r\n";
        var capture = NativeAotSocketGuest.Read(false, Raw(Call(deadline: 100) + Wake(state: 5) + repark +
            Wake(sequence: 2, clock: 13) + Complete(sequence: 2, clock: 14) + Resume()));
        HostCheck.True(capture.Readiness.Count == 4 && capture.PendingWaits == 0);
        var observation = capture.Readiness[1];
        HostCheck.True(observation.Kind == NativeAotSocketGuest.ObservationKind.Repark && observation.PreviousSequence == 1 &&
            observation.Sequence == 2 && observation.Deadline == 100);
        Reject(() => NativeAotSocketGuest.Read(false, Raw(Call(deadline: 100) + Wake() + repark.Replace("deadline=100", "deadline=101", StringComparison.Ordinal))), "repark_owner");
    }

    [HostTest("socket guest preserves already-won completions across clock advance and a deadline may still collect fresh events")]
    private static void Winners()
    {
        foreach ((uint completion, long result) in new[] { (1U, 0L), (1U, 2L), (2U, 0L), (2U, 2L), (5U, -4L) })
        {
            var capture = NativeAotSocketGuest.Read(false, Raw(Call(deadline: 5) + Wake(completion) + Complete(completion, result) + Resume(result)));
            HostCheck.Equal(0, capture.PendingWaits); HostCheck.Equal(unchecked((ulong)result), capture.Readiness[1].ValueBits);
        }
        Reject(() => NativeAotSocketGuest.Read(false, Raw(Call() + Wake(2) + Complete(2, 0) + Resume(0))), "wake_deadline");
        Reject(() => NativeAotSocketGuest.Read(false, Raw(Call() + Wake(5) + Complete(5, 0) + Resume(0))), "completion_result");
    }

    [HostTest("socket guest rejects orphan duplicate mismatched and fabricated epoll completion observations")]
    private static void Negatives()
    {
        foreach ((string records, string reason) in new[] {
            (Wake(), "wake_owner"), (Call() + Wake() + Wake(), "wake_owner"),
            (Call() + Wake().Replace("sequence=1", "sequence=01", StringComparison.Ordinal), "record_number"),
            (Call() + Wake(state: 2), "wake_state"), (Call() + Wake(sequence: 2), "wake_owner"),
            (Call() + Wake() + Complete(clock: 9), "completion_owner"),
            (Call() + Wake() + Resume(), "resume_owner"),
            (Call() + Wake() + Complete(sequence: 2), "completion_owner"),
            (Call() + Wake() + Complete(result: 0), "completion_result"),
            (Call() + Wake() + Complete(result: 1025), "completion_result"),
            (Call() + Wake() + Complete() + Resume(3), "resume_result"),
            (Call() + Wake() + Complete() + Resume() + Resume(), "resume_owner"),
            (Call() + Wake() + Complete() + Resume() + Call(source: 1, sequence: 2, ordinal: 2) + Resume(0) + Resume(0), "resume_owner"),
            (Call(source: 1) + Resume(0) + Call(sequence: 1, ordinal: 2), "wait_sequence"),
            (Call() + Wake() + Complete() + Complete(), "completion_owner"),
            (Call() + "M16_012_GUESS ready=true\r\n", "unknown_readiness"),
            (Call() + Call(sequence: 2, ordinal: 2), "wait_sequence") })
            Reject(() => NativeAotSocketGuest.Read(false, Raw(records)), reason);
    }

    private static void Reject(Action action, string reason)
    {
        string? failure = null; try { action(); } catch (Exception error) when (error is ToolFailure or InvalidDataException or ArgumentException) { failure = error.Message; }
        HostCheck.True(failure?.Contains(reason, StringComparison.Ordinal) == true, reason + " actual=" + failure);
    }
}
