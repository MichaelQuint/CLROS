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
public static class NativeAotWebRacePeer
{
    private const int SelectedLimit = 64 * 1024, AttemptsLimit = 8 * 1024 * 1024,
        WireLimit = 32 * 1024 * 1024, RunnerLimit = 64 * 1024 * 1024;

    /// <summary>Original producer reports and joined runner result. Only the
    /// owning factory constructs this receipt. Copied pipe PID and report roles
    /// do not authenticate QEMU, serial, native source, packets or guest cleanup.</summary>
    public sealed class Completion
    {
        private Completion(string root, int port, string health, string path, string summary,
            WebRaceRendezvous.Report gate, string summaryHash, ProcessResult runner,
            DateTimeOffset started, DateTimeOffset ended, QemuTranscripts.WebSignalRaceSelection selection,
            QemuTranscripts.WebSignalRaceHostHttpInspection http)
        {
            Root = root; Port = port; Health = health; Path = path; SummaryPath = summary;
            Gate = gate; SummarySha256 = summaryHash; Runner = runner;
            ProducerStartedUtc = started; ProducerCompletedUtc = ended; Selection = selection; Http = http;
        }

        internal string Root { get; }
        internal string Health { get; }
        public int Port { get; }
        public string Path { get; }
        public string SummaryPath { get; }
        public string RunnerOutputPath => Path + ".runner.log";
        public string RunnerErrorPath => Path + ".runner.stderr.log";
        public WebRaceRendezvous.Report Gate { get; }
        public string SummarySha256 { get; }
        public ProcessResult Runner { get; }
        public DateTimeOffset ProducerStartedUtc { get; }
        public DateTimeOffset ProducerCompletedUtc { get; }
        public QemuTranscripts.WebSignalRaceSelection Selection { get; }
        public QemuTranscripts.WebSignalRaceHostHttpInspection Http { get; }

        internal void Verify(AuthorityFiles files)
        {
            var reports = ReadReports(files, Root, Port, Health, Path);
            Need(reports.Digests == Gate.Digests && reports.Selection == Selection, "report_identity");
            var summary = files.Open(Root, SummaryPath, "original web race runner summary", "web_race_owner_summary", RunnerLimit, singleLink: true);
            Need(summary.Sha256.Equals(SummarySha256, StringComparison.OrdinalIgnoreCase), "summary_identity");
            RequireSummary(summary.ReadUtf8(RunnerLimit, "web_race_owner_summary"), Gate);
            var output = files.Open(Root, RunnerOutputPath, "original web race runner stdout", "web_race_owner_stdout", RunnerLimit, allowEmpty: true, singleLink: true);
            var error = files.Open(Root, RunnerErrorPath, "original web race runner stderr", "web_race_owner_stderr", RunnerLimit, allowEmpty: true, singleLink: true);
            Need(output.ReadBytes(RunnerLimit, "web_race_owner_stdout").AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(Runner.StandardOutput)) &&
                error.ReadBytes(RunnerLimit, "web_race_owner_stderr").AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(Runner.StandardError)), "log_identity");
            AuthorityFiles.RequireDistinct(reports.Selected, reports.Attempts, reports.Wire, summary, output, error);
        }

        internal static async Task<Completion> CreateAsync(string root, int port, string health, string path, string summary,
            Func<string, CancellationToken, Task<ProcessResult>> runRunner, CancellationToken cancellation,string? initialWaitSerial)
        {
            ArgumentNullException.ThrowIfNull(runRunner); cancellation.ThrowIfCancellationRequested();
            Need(port is >= 1 and <= 65535 && health is { Length: >= 1 and <= 1024 } && !health.Any(char.IsControl), "inputs");
            root = System.IO.Path.GetFullPath(root); path = System.IO.Path.GetFullPath(path, root); summary = System.IO.Path.GetFullPath(summary, root);
            string[] targets = [path, path + ".attempts.json", path + ".wire.json", path + ".runner.log", path + ".runner.stderr.log", summary];
            RequireFreshTargets(root, targets);
            using AuthorityFiles files = new();
            HoldProducer(files, root, "OS.Tooling", typeof(NativeAotWebRacePeer).Assembly.Location);
            HoldProducer(files, root, "OS.QemuRunner", typeof(WebRaceRendezvous).Assembly.Location);
            Need(ToolingBuildInputs.Validate(root).Count == 0, "stale_inputs");
            using var owned = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            owned.CancelAfter(TimeSpan.FromSeconds(330));
            await using var output = new FileStream(path + ".runner.log", FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            await using var errorLog = new FileStream(path + ".runner.stderr.log", FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            var pipe = WebRaceRendezvous.CreateProducer();
            Task<ProcessResult>? runner = null; Task<WebRaceRendezvous.Ready>? ready = null; Task<NativeAotWebPayload.OwnedRaceCapture>? capture = null;
            NativeAotWebPayload.OwnedRaceCapture? captured = null;
            ProcessResult? runnerResult = null; Reports? reports = null; WebRaceRendezvous.Ready? peer = null;
            DateTimeOffset started = default, ended = default; Exception? failure = null;
            try
            {
                runner = runRunner(pipe.PipeName, owned.Token) ?? throw new ToolFailure("web_race_owner_runner_missing");
                ready = pipe.WaitForReadyAsync(port, owned.Token);
                _ = await Task.WhenAny(ready, runner);
                owned.Token.ThrowIfCancellationRequested();
                Need(!runner.IsCompleted, "runner_before_ready"); peer = await ready;
                Need(!runner.IsCompleted, "runner_before_http");
                started = DateTimeOffset.UtcNow;
                ulong[]? initial=null;
                var startHooks=new NativeAotWebPayload.RaceStartHooks(async (status,token)=>
                {
                    initial=status.ToArray();await WaitForInitialWaitAsync(root,initialWaitSerial,initial,token);
                },token=>WaitForInitialWaitAsync(root,initialWaitSerial,initial!,token),_=>Task.CompletedTask);
                capture = NativeAotWebPayload.CaptureOwnedObservedSignalRaceAsync(port, health, path, owned.Token,startHooks);
                _ = await Task.WhenAny(capture, runner);
                owned.Token.ThrowIfCancellationRequested();
                Need(!runner.IsCompleted, "runner_during_http");
                captured = await capture; ended = DateTimeOffset.UtcNow; owned.Token.ThrowIfCancellationRequested();
                reports = ReadReports(files, root, port, health, path);
                captured.Selected.Verify(reports.Selected); captured.Attempts.Verify(reports.Attempts); captured.Wire.Verify(reports.Wire);
                Need(reports.Selected.ReadUtf8(SelectedLimit, "web_race_owner_selected") == captured.SelectedJson, "selected_return_identity");
                files.VerifyUnchanged();
                Need(!runner.IsCompleted, "runner_before_done");
                await pipe.CompleteAsync(reports.Digests, owned.Token);
                runnerResult = await runner; owned.Token.ThrowIfCancellationRequested();
                RequireRunner(runnerResult);
            }
            catch (Exception error) { failure = error; }
            // Close the rendezvous before joining on failure, so an awaiting
            // runner cannot depend on the producer's still-open pipe to finish.
            try { if (failure is not null) await pipe.DisposeAsync(); }
            catch (Exception error) { AddFailure(ref failure, error); }
            try { owned.Cancel(); } catch (Exception error) { AddFailure(ref failure, error); }
            if (ready is not null) try { _ = await ready; } catch (Exception error) { JoinFailure(ref failure, error); }
            if (capture is not null) try { captured = await capture; } catch (Exception error) { JoinFailure(ref failure, error); }
            if (runner is not null) try { runnerResult = await runner; } catch (Exception error) { JoinFailure(ref failure, error); }
            try { await pipe.DisposeAsync(); } catch (Exception error) { AddFailure(ref failure, error); }
            try { captured?.Dispose(); } catch (Exception error) { AddFailure(ref failure, error); }
            if (runnerResult is not null)
            {
                try { await WriteLog(output, runnerResult.StandardOutput); } catch (Exception error) { AddFailure(ref failure, error); }
                try { await WriteLog(errorLog, runnerResult.StandardError); } catch (Exception error) { AddFailure(ref failure, error); }
                if (!runnerResult.TerminationConfirmed) AddFailure(ref failure, new ToolFailure("web_race_owner_cleanup_unconfirmed"));
            }
            if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
            cancellation.ThrowIfCancellationRequested();
            // Writers must close before the immutable receipt reopens originals.
            await output.DisposeAsync(); await errorLog.DisposeAsync();
            var summaryFile = files.Open(root, summary, "original web race runner summary", "web_race_owner_summary", RunnerLimit, singleLink: true);
            var gate = new WebRaceRendezvous.Report(port, peer!.QemuProcessId, reports!.Digests);
            RequireSummary(summaryFile.ReadUtf8(RunnerLimit, "web_race_owner_summary"), gate);
            var result = new Completion(root, port, health, path, summary, gate, summaryFile.Sha256.ToUpperInvariant(), runnerResult!, started, ended, reports.Selection, reports.Http);
            result.Verify(files); files.VerifyUnchanged(); return result;
        }
    }

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
    /// Missing serial cannot complete this production path; final immutable
    /// replay still proves the exact original wait/request/actor order.
    /// Complete receipts still require separate native/source/serial/PCAP admission.</summary>
    public static Task<Completion> RunOwnedAsync(string root, int port, string expectedHealthBody,
        string observationPath, string summaryPath, Func<string, CancellationToken, Task<ProcessResult>> runRunner,
        CancellationToken cancellation = default,string? initialWaitSerial=null) => Completion.CreateAsync(root, port, expectedHealthBody,
            observationPath, summaryPath, runRunner, cancellation,initialWaitSerial);

    // A provisional live read delays the one start POST; it confers no native
    // authority. Full immutable replay/role validation later requires the exact
    // original initial-wait < start-consumption < actor-write relationship.
    private static async Task WaitForInitialWaitAsync(string root,string? serial,ulong[] status,CancellationToken cancellation)
    {
        Need(!string.IsNullOrEmpty(serial)&&status is {Length:6}&&status[0]==2&&status[1]==1&&
            status[2] is >0 and <=int.MaxValue,"initial_wait_inputs");
        string path=System.IO.Path.GetFullPath(serial!,root);AuthorityFiles.RequirePlainPath(root,path,"web_race_owner_initial_path");
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);deadline.CancelAfter(TimeSpan.FromMilliseconds(500));
        try{await NativeAotSocketPeer.WaitForSerialAsync(path,text=>HasInitialWait(text,(int)status[2]),deadline.Token);}
        catch(OperationCanceledException)when(!cancellation.IsCancellationRequested){throw new ToolFailure("web_race_owner_initial_wait_timeout");}
    }

    private static bool HasInitialWait(string text,int waiterTid)
    {
        int start=text.IndexOf("TEST_START m10.004\r\n",StringComparison.Ordinal),end=text.LastIndexOf("\r\n",StringComparison.Ordinal);
        if(start<0||end<start||waiterTid<=0)return false;
        string? identity=null;bool pending=false;int waits=0;
        foreach(string row in text[start..end].Split("\r\n",StringSplitOptions.None))
        {
            var call=Regex.Match(row,@"^M16_008_CALL identity=([1-9][0-9]*) nr=([0-9]+) .* valueBits=([0-9]+) action=([0-9]+) status=([0-9]+) ",RegexOptions.CultureInvariant);
            if(call.Success)
            {
                string current=call.Groups[1].Value;
                if(call.Groups[2].Value=="186"&&call.Groups[3].Value==waiterTid.ToString(CultureInfo.InvariantCulture)&&
                    call.Groups[4].Value=="1"&&call.Groups[5].Value=="1")
                {if(identity is not null&&identity!=current)return false;identity=current;}
                if(current!=identity)continue;
                pending=false;
                if(call.Groups[2].Value=="232")
                {
                    waits++;pending=call.Groups[3].Value=="0"&&call.Groups[4].Value=="3"&&call.Groups[5].Value=="9"&&
                        Regex.IsMatch(row,@" ordinal=[1-9][0-9]* waitSource=3 waitDeadline=[1-9][0-9]* waitSequence=[1-9][0-9]*(?: |$)",RegexOptions.CultureInvariant);
                }
            }
            else if(identity is not null&&(row.StartsWith("M16_008_RESUME identity="+identity+" ",StringComparison.Ordinal)||
                row.StartsWith("M16_008_EXIT identity="+identity+" ",StringComparison.Ordinal)||row.StartsWith("M16_008_REAP old="+identity+" ",StringComparison.Ordinal)))pending=false;
        }
        return identity is not null&&pending&&waits==1;
    }

    private sealed record Reports(AuthorityFile Selected, AuthorityFile Attempts, AuthorityFile Wire,
        WebRaceRendezvous.Digests Digests, QemuTranscripts.WebSignalRaceSelection Selection,
        QemuTranscripts.WebSignalRaceHostHttpInspection Http);

    private static Reports ReadReports(AuthorityFiles files, string root, int port, string health, string path)
    {
        var selected = files.Open(root, path, "original web race selection", "web_race_owner_selected", SelectedLimit, singleLink: true);
        var attempts = files.Open(root, path + ".attempts.json", "original web race attempts", "web_race_owner_attempts", AttemptsLimit, singleLink: true);
        var wire = files.Open(root, path + ".wire.json", "original web race wire", "web_race_owner_wire", WireLimit, singleLink: true);
        AuthorityFiles.RequireDistinct(selected, attempts, wire);
        string endpoint = "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture);
        string selectedJson = selected.ReadUtf8(SelectedLimit, "web_race_owner_selected"), attemptsJson = attempts.ReadUtf8(AttemptsLimit, "web_race_owner_attempts");
        using(var version=JsonDocument.Parse(selectedJson))
            Need(version.RootElement.GetProperty("schema").GetString()=="clros.web.signalrace.v2","start_contract");
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
        foreach (string expected in new[] { "QEMU_SMOKE_STATUS pass", "WEB_RACE_GATE " + JsonSerializer.Serialize(gate),
            "WEB_PROCESS_ALIVE_AFTER_CONTROLS true", "WEB_PROCESS_ALIVE_AFTER_RACE_GATE true",
            "WEB_PROCESS_ID " + gate.QemuProcessId.ToString(CultureInfo.InvariantCulture), "WEB_ENDPOINT_STOPPED_REFUSED true",
            "WEB_PROCESS_TERMINATION_REQUESTED true", "WEB_PROCESS_DEATH_CONFIRMED true", "WEB_PROCESS_SPONTANEOUS_EXIT false", "WEB_PROCESS_FAILURE none" })
        {
            string key = expected[..expected.IndexOf(' ')];
            Need(lines.Count(line => line.StartsWith(key + " ", StringComparison.Ordinal)) == 1 && lines.Contains(expected, StringComparer.Ordinal), "runner_summary");
        }
    }

    private static async Task WriteLog(Stream file, string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text); Need(bytes.Length <= RunnerLimit, "log_limit");
        await file.WriteAsync(bytes); await file.FlushAsync();
    }
    private static void JoinFailure(ref Exception? failure, Exception error)
    {
        if (failure is not null && error is OperationCanceledException or ObjectDisposedException) return;
        AddFailure(ref failure, error);
    }
    private static void AddFailure(ref Exception? failure, Exception error)
    {
        if (ReferenceEquals(failure, error) || failure is AggregateException aggregate && aggregate.InnerExceptions.Contains(error)) return;
        failure = failure is null ? error : new AggregateException(failure, error);
    }
    private static void Need(bool condition, string reason) { if (!condition) throw new ToolFailure("web_race_owner_" + reason); }
}
