using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OS.Tools.QemuRunner;

namespace OS.Tooling;
/// <summary>Own the explicit web-race producer beside a bounded runner. The
/// pipe orders lifetimes; original reports and process results are checked
/// separately. This component does not admit a native image or guest capture.</summary>

public static partial class NativeAotWebRacePeer
{
    private const int SelectedLimit = 64 * 1024, AttemptsLimit = 8 * 1024 * 1024, WireLimit = 32 * 1024 * 1024, RunnerLimit = 64 * 1024 * 1024;
    /// <summary>Create fresh runner logs under existing plain root directories,
    /// then start the bounded callback with a fresh same-user pipe name. The
    /// callback must use ProcessRunner, honor cancellation, pass that name and
    /// the same port/summary to normal continuing-web, and confirm termination.
    /// Wait for READY after ordinary controls, own the observed HTTP task, hold
    /// and frame its three reports, then send DONE and join the runner. Every
    /// failure closes the pipe, cancels and joins all started tasks and retains
    /// exact available runner logs plus partial HTTP reports before throwing.
    /// The 330s owner bound does not extend the runner's existing 285s deadline.
    /// Explicit v2 holds phase2 before short churn and rechecks a provisional
    /// initial-wait row from initialWaitSerial before the one start request.
    /// After phase4, wait for a provisional second pending wait on that same
    /// thread/epoll before consuming overlap GETs; phase4 alone cannot order it.
    /// Missing serial cannot complete this production path; final immutable
    /// replay still proves the exact original wait/request/actor order.
    /// Complete receipts still require separate native/source/serial/PCAP admission.</summary>

    public static Task<Completion> RunOwnedAsync(string root, int port, string expectedHealthBody, string observationPath, string summaryPath, Func<string, CancellationToken, Task<ProcessResult>> runRunner, CancellationToken cancellation = default, string? initialWaitSerial = null) => Completion.CreateAsync(root, port, expectedHealthBody, observationPath, summaryPath, runRunner, cancellation, initialWaitSerial, false);
    /// <summary>Own the explicit event-v3 counterpart of RunOwnedAsync with
    /// identical path/source/process/report ownership and joined failure rules.
    /// Use a fresh v3 pipe and observed150s HTTP capture; require selected-v3
    /// originals and a provisional exact zero-deadline initial wait before the
    /// start request, then a second pending same-thread/epoll wait before
    /// overlap requests. The callback must pass the explicit runner mode and v3
    /// pipe with the original <=285s deadline; the owner remains330s. Complete
    /// native/source/serial/packet/replay admission is still separate.</summary>

    public static Task<Completion> RunEventOwnedAsync(string root, int port, string expectedHealthBody, string observationPath, string summaryPath, Func<string, CancellationToken, Task<ProcessResult>> runRunner, CancellationToken cancellation = default, string? initialWaitSerial = null) => Completion.CreateAsync(root, port, expectedHealthBody, observationPath, summaryPath, runRunner, cancellation, initialWaitSerial, true);
    /// <summary>Inspect bounded copied serial rows for a provisional pending
    /// initial wait after the selected waiter gettid. Explicit event mode checks
    /// maxevents1/raw all-ones/zero deadline; default preserves historical positive
    /// deadlines. Missing identity, unsupported selected fields, resumed or
    /// repeated waits return false; this is not a full record/descriptor decoder.
    /// This pure seam authenticates no original row, descriptor, process or
    /// runtime and never replaces complete held replay and causal admission.</summary>

    public static bool InspectInitialWaitForTests(string serial, int waiterTid, bool eventOwned = false) => HasPendingWait(serial, waiterTid, eventOwned, 1);
    /// <summary>Inspect bounded copied rows for a provisional second pending
    /// wait by the selected thread on its original epoll descriptor. Event mode
    /// requires maxevents1/all-ones timeout/zero deadline. Reject missing,
    /// resumed, repeated or mismatched waits. This delays external requests;
    /// it authenticates no original or blocked state and never replaces full
    /// held replay, HTTP byte coverage and recovery/overlap order validation.</summary>

    public static bool InspectRecoveryWaitForTests(string serial, int waiterTid, bool eventOwned = false) => HasPendingWait(serial, waiterTid, eventOwned, 2);
    // A provisional live read delays the one start POST; it confers no native
    // authority. Full immutable replay/role validation later requires the exact
    // original initial-wait < start-consumption < actor-write relationship.
    private static Task WaitForInitialWaitAsync(string root, string? serial, ulong[] status, CancellationToken cancellation, bool eventOwned) => WaitForPendingWaitAsync(root, serial, status, cancellation, eventOwned, false);
    private static Task WaitForRecoveryWaitAsync(string root, string? serial, ulong[] status, CancellationToken cancellation, bool eventOwned) => WaitForPendingWaitAsync(root, serial, status, cancellation, eventOwned, true);
    private static async Task WaitForPendingWaitAsync(string root, string? serial, ulong[] status, CancellationToken cancellation, bool eventOwned, bool recovery)
    {
        Need(!string.IsNullOrEmpty(serial) && status is { Length: 6 } && status[0] == 2 && status[1] == 1 && status[2] is> 0 and <= int.MaxValue, recovery ? "recovery_wait_inputs" : "initial_wait_inputs");
        string path = System.IO.Path.GetFullPath(serial!, root);
        AuthorityFiles.RequirePlainPath(root, path, recovery ? "web_race_owner_recovery_path" : "web_race_owner_initial_path");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromMilliseconds(500));
        try
        {
            await NativeAotSocketPeer.WaitForSerialAsync(path, text => HasPendingWait(text, (int)status[2], eventOwned, recovery ? 2 : 1), deadline.Token);
        }
        catch (OperationCanceledException)when (!cancellation.IsCancellationRequested)
        {
            throw new ToolFailure(recovery ? "web_race_owner_recovery_wait_timeout" : "web_race_owner_initial_wait_timeout");
        }
    }

    private static bool HasPendingWait(string text, int waiterTid, bool eventOwned, int expectedWaits)
    {
        if (string.IsNullOrEmpty(text) || text.Length > 8 * 1024 * 1024 || waiterTid <= 0)
            return false;
        int start = text.IndexOf("TEST_START m10.004\r\n", StringComparison.Ordinal), end = text.LastIndexOf("\r\n", StringComparison.Ordinal);
        if (start < 0 || end < start)
            return false;
        string? identity = null, epoll = null;
        bool pending = false;
        int waits = 0;
        foreach (string row in text[start..end].Split("\r\n", StringSplitOptions.None))
        {
            var call = Regex.Match(row, @"^M16_008_CALL identity=([1-9][0-9]*) nr=([0-9]+) .* valueBits=([0-9]+) action=([0-9]+) status=([0-9]+) ", RegexOptions.CultureInvariant);
            if (call.Success)
            {
                string current = call.Groups[1].Value;
                if (call.Groups[2].Value == "186" && call.Groups[3].Value == waiterTid.ToString(CultureInfo.InvariantCulture) && call.Groups[4].Value == "1" && call.Groups[5].Value == "1")
                {
                    if (identity is not null && identity != current)
                        return false;
                    identity = current;
                }

                if (current != identity)
                    continue;
                pending = false;
                if (call.Groups[2].Value == "232")
                {
                    waits++;
                    if (expectedWaits == 2)
                    {
                        var descriptor = Regex.Match(row, @" a0=([0-9]+)(?: |$)", RegexOptions.CultureInvariant);
                        if (!descriptor.Success)
                            return false;
                        if (epoll is null)
                            epoll = descriptor.Groups[1].Value;
                        else if (epoll != descriptor.Groups[1].Value)
                            return false;
                    }

                    pending = call.Groups[3].Value == "0" && call.Groups[4].Value == "3" && call.Groups[5].Value == "9" && Regex.IsMatch(row, @" ordinal=[1-9][0-9]* waitSource=3 waitDeadline=" + (eventOwned ? "0" : "[1-9][0-9]*") + @" waitSequence=[1-9][0-9]*(?: |$)", RegexOptions.CultureInvariant) && (!eventOwned || Regex.IsMatch(row, @" a2=1 a3=18446744073709551615(?: |$)", RegexOptions.CultureInvariant));
                    if (expectedWaits == 2 && !pending)
                        return false;
                }
            }
            else if (identity is not null && row.StartsWith("M16_008_CALL identity=" + identity + " ", StringComparison.Ordinal))
            {
                if (expectedWaits == 2)
                    return false;
                pending = false;
            }
            else if (identity is not null && (row.StartsWith("M16_008_RESUME identity=" + identity + " ", StringComparison.Ordinal) || row.StartsWith("M16_008_EXIT identity=" + identity + " ", StringComparison.Ordinal) || row.StartsWith("M16_008_REAP old=" + identity + " ", StringComparison.Ordinal)))
                pending = false;
        }

        return identity is not null && pending && waits == expectedWaits;
    }

    private static Reports ReadReports(AuthorityFiles files, string root, int port, string health, string path, bool eventOwned)
    {
        var selected = files.Open(root, path, "original web race selection", "web_race_owner_selected", SelectedLimit, singleLink: true);
        var attempts = files.Open(root, path + ".attempts.json", "original web race attempts", "web_race_owner_attempts", AttemptsLimit, singleLink: true);
        var wire = files.Open(root, path + ".wire.json", "original web race wire", "web_race_owner_wire", WireLimit, singleLink: true);
        AuthorityFiles.RequireDistinct(selected, attempts, wire);
        string endpoint = "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture);
        string selectedJson = selected.ReadUtf8(SelectedLimit, "web_race_owner_selected"), attemptsJson = attempts.ReadUtf8(AttemptsLimit, "web_race_owner_attempts");
        using (var version = JsonDocument.Parse(selectedJson))
            Need(version.RootElement.GetProperty("schema").GetString() == (eventOwned ? "clros.web.signalrace.v3" : "clros.web.signalrace.v2"), "start_contract");
        var selection = NativeAotWebPayload.ValidateSignalRaceEvidence(selectedJson, endpoint, health);
        var http = QemuTranscripts.InspectNativeAotWebSignalRaceHostHttp(wire.ReadBytes(WireLimit, "web_race_owner_wire"), attemptsJson, selectedJson, endpoint, health);
        return new(selected, attempts, wire, new(selected.Sha256.ToUpperInvariant(), attempts.Sha256.ToUpperInvariant(), wire.Sha256.ToUpperInvariant()), selection, http);
    }

    private static void RequireFreshTargets(string root, string[] targets)
    {
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        Need(targets.Distinct(comparer).Count() == targets.Length, "path_alias");
        foreach (string path in targets)
        {
            string parent = System.IO.Path.GetDirectoryName(path)!, leaf = System.IO.Path.GetFileName(path);
            Need(leaf.Length > 0 && leaf.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) < 0 && !path.Any(char.IsControl), "path_invalid");
            AuthorityFiles.RequirePlainPath(root, parent, "web_race_owner_path");
            Need(Directory.Exists(parent) && !System.IO.Path.Exists(path), "output_not_fresh");
        }
    }

    private static void HoldProducer(AuthorityFiles files, string root, string name, string loadedPath)
    {
        string path = HostTools.ManagedTool(root, name);
        var tool = files.Open(root, path, "web race producer " + name, "web_race_owner_producer", 32 * 1024 * 1024, singleLink: true);
        var loaded = files.Open(root, loadedPath, "loaded web race producer " + name, "web_race_owner_loaded", 32 * 1024 * 1024, singleLink: true);
        Need(tool.Sha256 == loaded.Sha256, "producer_identity");
    }

    private static void RequireRunner(ProcessResult result)
    {
        QemuTranscripts.RequireCompleteProcess(result, "web_race_owner_runner_incomplete");
        Need(result.Succeeded && result.StandardError.Length == 0, "runner_failed");
    }

    private static void RequireSummary(string summary, WebRaceRendezvous.Report gate)
    {
        string[] lines = summary.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        foreach (string expected in new[]
        {
            "QEMU_SMOKE_STATUS pass",
            "WEB_RACE_GATE " + JsonSerializer.Serialize(gate),
            "WEB_PROCESS_ALIVE_AFTER_CONTROLS true",
            "WEB_PROCESS_ALIVE_AFTER_RACE_GATE true",
            "WEB_PROCESS_ID " + gate.QemuProcessId.ToString(CultureInfo.InvariantCulture),
            "WEB_ENDPOINT_STOPPED_REFUSED true",
            "WEB_PROCESS_TERMINATION_REQUESTED true",
            "WEB_PROCESS_DEATH_CONFIRMED true",
            "WEB_PROCESS_SPONTANEOUS_EXIT false",
            "WEB_PROCESS_FAILURE none"
        }

        )
        {
            string key = expected[..expected.IndexOf(' ')];
            Need(lines.Count(line => line.StartsWith(key + " ", StringComparison.Ordinal)) == 1 && lines.Contains(expected, StringComparer.Ordinal), "runner_summary");
        }
    }

    private static async Task WriteLog(Stream file, string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        Need(bytes.Length <= RunnerLimit, "log_limit");
        await file.WriteAsync(bytes);
        await file.FlushAsync();
    }

    private static void JoinFailure(ref Exception? failure, Exception error)
    {
        if (failure is not null && error is OperationCanceledException or ObjectDisposedException)
            return;
        AddFailure(ref failure, error);
    }

    private static void AddFailure(ref Exception? failure, Exception error)
    {
        if (ReferenceEquals(failure, error) || failure is AggregateException aggregate && aggregate.InnerExceptions.Contains(error))
            return;
        failure = failure is null ? error : new AggregateException(failure, error);
    }

    private static void Need(bool condition, string reason)
    {
        if (!condition)
            throw new ToolFailure("web_race_owner_" + reason);
    }
}
