using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OS.Tools.QemuRunner;

namespace OS.Tooling;
public static partial class NativeAotWebRacePeer
{
    /// <summary>Original producer reports and joined runner result. Only the
    /// owning factory constructs this receipt. Copied pipe PID and report roles
    /// do not authenticate QEMU, serial, native source, packets or guest cleanup.</summary>

    public sealed class Completion
    {
        private Completion(string root, int port, string health, string path, string summary, WebRaceRendezvous.Report gate, string summaryHash, ProcessResult runner, DateTimeOffset started, DateTimeOffset ended, QemuTranscripts.WebSignalRaceSelection selection, QemuTranscripts.WebSignalRaceHostHttpInspection http, bool eventOwned)
        {
            Root = root;
            Port = port;
            Health = health;
            Path = path;
            SummaryPath = summary;
            Gate = gate;
            SummarySha256 = summaryHash;
            Runner = runner;
            EventOwned = eventOwned;
            ProducerStartedUtc = started;
            ProducerCompletedUtc = ended;
            Selection = selection;
            Http = http;
        }

        internal string Root { get; }
        internal string Health { get; }
        public int Port { get; }
        /// <summary>Immutable profile selected by the owning factory, checked
        /// against the pipe, original reports and runner summary.</summary>

        public bool EventOwned { get; }
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
            Need(Gate.EventOwned == EventOwned, "profile_identity");
            var reports = ReadReports(files, Root, Port, Health, Path, EventOwned);
            Need(reports.Digests == Gate.Digests && reports.Selection == Selection, "report_identity");
            var summary = files.Open(Root, SummaryPath, "original web race runner summary", "web_race_owner_summary", RunnerLimit, singleLink: true);
            Need(summary.Sha256.Equals(SummarySha256, StringComparison.OrdinalIgnoreCase), "summary_identity");
            RequireSummary(summary.ReadUtf8(RunnerLimit, "web_race_owner_summary"), Gate);
            var output = files.Open(Root, RunnerOutputPath, "original web race runner stdout", "web_race_owner_stdout", RunnerLimit, allowEmpty: true, singleLink: true);
            var error = files.Open(Root, RunnerErrorPath, "original web race runner stderr", "web_race_owner_stderr", RunnerLimit, allowEmpty: true, singleLink: true);
            Need(output.ReadBytes(RunnerLimit, "web_race_owner_stdout").AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(Runner.StandardOutput)) && error.ReadBytes(RunnerLimit, "web_race_owner_stderr").AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(Runner.StandardError)), "log_identity");
            AuthorityFiles.RequireDistinct(reports.Selected, reports.Attempts, reports.Wire, summary, output, error);
        }

        internal static async Task<Completion> CreateAsync(string root, int port, string health, string path, string summary, Func<string, CancellationToken, Task<ProcessResult>> runRunner, CancellationToken cancellation, string? initialWaitSerial, bool eventOwned)
        {
            ArgumentNullException.ThrowIfNull(runRunner);
            cancellation.ThrowIfCancellationRequested();
            Need(port is >= 1 and <= 65535 && health is { Length: >= 1 and <= 1024 } && !health.Any(char.IsControl), "inputs");
            root = System.IO.Path.GetFullPath(root);
            path = System.IO.Path.GetFullPath(path, root);
            summary = System.IO.Path.GetFullPath(summary, root);
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
            var pipe = eventOwned ? WebRaceRendezvous.CreateEventOwnedProducer() : WebRaceRendezvous.CreateProducer();
            Task<ProcessResult>? runner = null;
            Task<WebRaceRendezvous.Ready>? ready = null;
            Task<NativeAotWebPayload.OwnedRaceCapture>? capture = null;
            NativeAotWebPayload.OwnedRaceCapture? captured = null;
            ProcessResult? runnerResult = null;
            Reports? reports = null;
            WebRaceRendezvous.Ready? peer = null;
            DateTimeOffset started = default, ended = default;
            Exception? failure = null;
            try
            {
                runner = runRunner(pipe.PipeName, owned.Token) ?? throw new ToolFailure("web_race_owner_runner_missing");
                ready = pipe.WaitForReadyAsync(port, owned.Token);
                _ = await Task.WhenAny(ready, runner);
                owned.Token.ThrowIfCancellationRequested();
                Need(!runner.IsCompleted, "runner_before_ready");
                peer = await ready;
                Need(peer.EventOwned == eventOwned, "ready_profile");
                Need(!runner.IsCompleted, "runner_before_http");
                started = DateTimeOffset.UtcNow;
                ulong[]? initial = null;
                var startHooks = new NativeAotWebPayload.RaceStartHooks(async (status, token) =>
                {
                    initial = status.ToArray();
                    await WaitForInitialWaitAsync(root, initialWaitSerial, initial, token, eventOwned);
                }, token => WaitForInitialWaitAsync(root, initialWaitSerial, initial!, token, eventOwned), token => WaitForRecoveryWaitAsync(root, initialWaitSerial, initial!, token, eventOwned));
                capture = NativeAotWebPayload.CaptureOwnedObservedSignalRaceAsync(port, health, path, owned.Token, startHooks, eventOwned);
                _ = await Task.WhenAny(capture, runner);
                owned.Token.ThrowIfCancellationRequested();
                Need(!runner.IsCompleted, "runner_during_http");
                captured = await capture;
                ended = DateTimeOffset.UtcNow;
                owned.Token.ThrowIfCancellationRequested();
                reports = ReadReports(files, root, port, health, path, eventOwned);
                captured.Selected.Verify(reports.Selected);
                captured.Attempts.Verify(reports.Attempts);
                captured.Wire.Verify(reports.Wire);
                Need(reports.Selected.ReadUtf8(SelectedLimit, "web_race_owner_selected") == captured.SelectedJson, "selected_return_identity");
                files.VerifyUnchanged();
                Need(!runner.IsCompleted, "runner_before_done");
                await pipe.CompleteAsync(reports.Digests, owned.Token);
                runnerResult = await runner;
                owned.Token.ThrowIfCancellationRequested();
                RequireRunner(runnerResult);
            }
            catch (Exception error)
            {
                failure = error;
            }

            // Close the rendezvous before joining on failure, so an awaiting
            // runner cannot depend on the producer's still-open pipe to finish.
            try
            {
                if (failure is not null)
                    await pipe.DisposeAsync();
            }
            catch (Exception error)
            {
                AddFailure(ref failure, error);
            }

            try
            {
                owned.Cancel();
            }
            catch (Exception error)
            {
                AddFailure(ref failure, error);
            }

            if (ready is not null)
                try
                {
                    _ = await ready;
                }
                catch (Exception error)
                {
                    JoinFailure(ref failure, error);
                }

            if (capture is not null)
                try
                {
                    captured = await capture;
                }
                catch (Exception error)
                {
                    JoinFailure(ref failure, error);
                }

            if (runner is not null)
                try
                {
                    runnerResult = await runner;
                }
                catch (Exception error)
                {
                    JoinFailure(ref failure, error);
                }

            try
            {
                await pipe.DisposeAsync();
            }
            catch (Exception error)
            {
                AddFailure(ref failure, error);
            }

            try
            {
                captured?.Dispose();
            }
            catch (Exception error)
            {
                AddFailure(ref failure, error);
            }

            if (runnerResult is not null)
            {
                try
                {
                    await WriteLog(output, runnerResult.StandardOutput);
                }
                catch (Exception error)
                {
                    AddFailure(ref failure, error);
                }

                try
                {
                    await WriteLog(errorLog, runnerResult.StandardError);
                }
                catch (Exception error)
                {
                    AddFailure(ref failure, error);
                }

                if (!runnerResult.TerminationConfirmed)
                    AddFailure(ref failure, new ToolFailure("web_race_owner_cleanup_unconfirmed"));
            }

            if (failure is not null)
                ExceptionDispatchInfo.Capture(failure).Throw();
            cancellation.ThrowIfCancellationRequested();
            // Writers must close before the immutable receipt reopens originals.
            await output.DisposeAsync();
            await errorLog.DisposeAsync();
            var summaryFile = files.Open(root, summary, "original web race runner summary", "web_race_owner_summary", RunnerLimit, singleLink: true);
            var gate = new WebRaceRendezvous.Report(port, peer!.QemuProcessId, reports!.Digests, eventOwned);
            RequireSummary(summaryFile.ReadUtf8(RunnerLimit, "web_race_owner_summary"), gate);
            var result = new Completion(root, port, health, path, summary, gate, summaryFile.Sha256.ToUpperInvariant(), runnerResult!, started, ended, reports.Selection, reports.Http, eventOwned);
            result.Verify(files);
            files.VerifyUnchanged();
            return result;
        }
    }
}
