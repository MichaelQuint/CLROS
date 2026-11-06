using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace OS.Tooling;

public static partial class NativeAotServiceQuotaPeer
{
    /// <summary>Original owned peer/runner results and copied report identity.
    /// Only the process-owning factory constructs this object. Expected runner22
    /// is a capture disposition, not guest, quota, packet or cleanup acceptance.</summary>

    public sealed class Completion
    {
        private Completion(string root, int port, string path, string serial, string hash, ProcessResult runner, ProcessResult peer, DateTimeOffset started, DateTimeOffset ended, Observation observation, string? wirePath = null, string? wireSha256 = null, WireTrace? wire = null)
        {
            Root = root;
            Port = port;
            Path = path;
            SerialPath = serial;
            Sha256 = hash;
            Runner = runner;
            Peer = peer;
            PeerStartedUtc = started;
            PeerCompletedUtc = ended;
            Report = observation;
            WirePath = wirePath;
            WireSha256 = wireSha256;
            Wire = wire;
        }

        internal string Root
        {
            get;
        }
        public int Port
        {
            get;
        }
        public string Path
        {
            get;
        }
        public string SerialPath
        {
            get;
        }
        public string RunnerOutputPath => Path + ".runner.log";
        public string RunnerErrorPath => Path + ".runner.stderr.log";
        public string Sha256
        {
            get;
        }
        public ProcessResult Runner
        {
            get;
        }
        public ProcessResult Peer
        {
            get;
        }
        public DateTimeOffset PeerStartedUtc
        {
            get;
        }
        public DateTimeOffset PeerCompletedUtc
        {
            get;
        }
        public Observation Report
        {
            get;
        }
        /// <summary>Optional separately owned raw-I/O companion. Its local
        /// endpoints/ticks and partial I/O confer no guest/packet authority.</summary>

        public string? WirePath
        {
            get;
        }
        public string? WireSha256
        {
            get;
        }
        public WireTrace? Wire
        {
            get;
        }

        internal void Verify(AuthorityFiles files)
        {
            var report = files.Open(Root, Path, "original quota peer report", "quota_peer_owner_report", ReportMaximumBytes + 2, singleLink: true);
            byte[] bytes = report.ReadBytes(ReportMaximumBytes + 2, "quota_peer_owner_report");
            Need(Convert.ToHexString(SHA256.HashData(bytes)) == Sha256 && ReadOwnedReport(bytes).Port == Port, "owner_report_identity");
            var error = files.Open(Root, Path + ".stderr.log", "original quota peer stderr", "quota_peer_owner_stderr", ReportMaximumBytes + 2, allowEmpty: true, singleLink: true);
            var runnerOutput = files.Open(Root, RunnerOutputPath, "original quota runner stdout", "quota_peer_owner_runner_stdout", OwnerRunnerMaximumBytes, allowEmpty: true, singleLink: true);
            var runnerError = files.Open(Root, RunnerErrorPath, "original quota runner stderr", "quota_peer_owner_runner_stderr", OwnerRunnerMaximumBytes, allowEmpty: true, singleLink: true);
            Need((WirePath is null ? error.Length == 0 : error.ReadUtf8(ReportMaximumBytes + 2, "quota_peer_owner_stderr") == Peer.StandardError) && runnerError.Length == 0 && runnerOutput.ReadBytes(OwnerRunnerMaximumBytes, "quota_peer_owner_runner_stdout").AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(Runner.StandardOutput)), "owner_log_identity");
            if (WirePath is string wirePath)
            {
                var wire = files.Open(Root, wirePath, "original quota peer wire", "quota_peer_owner_wire", WireMaximumReportBytes, singleLink: true);
                var receipt = ReadWireReceipt(Peer.StandardError);
                Need(string.Equals(wire.Sha256, WireSha256, StringComparison.OrdinalIgnoreCase) && string.Equals(wire.Sha256, receipt.Sha256, StringComparison.OrdinalIgnoreCase) && wire.Length == receipt.Bytes, "owner_wire_identity");
                RequireOwnedWire(InspectWireReport(wire.ReadBytes(WireMaximumReportBytes, "quota_peer_owner_wire")), Report);
                AuthorityFiles.RequireDistinct(report, error, runnerOutput, runnerError, wire);
            }
            else
                AuthorityFiles.RequireDistinct(report, error, runnerOutput, runnerError);
        }

        internal static async Task<Completion> CreateOwnedAsync(string root, string peerLog, string serial, Func<int, CancellationToken, Task<ProcessResult>> runRunner, CancellationToken cancellation, bool observeWire = false)
        {
            ArgumentNullException.ThrowIfNull(runRunner);
            cancellation.ThrowIfCancellationRequested();
            root = System.IO.Path.GetFullPath(root);
            peerLog = System.IO.Path.GetFullPath(peerLog, root);
            serial = System.IO.Path.GetFullPath(serial, root);
            var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            string? wirePath = observeWire ? RequireWireTarget(root, peerLog + ".wire.json") : null;
            string[] logs = [peerLog, peerLog + ".stderr.log", peerLog + ".runner.log", peerLog + ".runner.stderr.log", ..wirePath is null ? Array.Empty<string>() : new[]
            {
                wirePath
            }

            ];
            Need(!logs.Contains(serial, comparer), "owner_path_alias");
            AuthorityFiles.RequirePlainPath(root, System.IO.Path.GetDirectoryName(peerLog)!, "quota_peer_owner_log_path");
            AuthorityFiles.RequirePlainPath(root, System.IO.Path.GetDirectoryName(serial)!, "quota_peer_owner_serial_path");
            Need(logs.All(path => !System.IO.Path.Exists(path)), "owner_log_not_fresh");
            Need(!System.IO.Path.Exists(serial), "owner_serial_not_fresh");
            using AuthorityFiles inputs = new();
            string tool = HostTools.ManagedTool(root, "OS.Tooling");
            _ = inputs.Open(System.IO.Path.GetDirectoryName(tool)!, tool, "quota peer executable", "quota_peer_owner_executable", 32 * 1024 * 1024, singleLink: true);
            int port;
            using (var reservation = new TcpListener(IPAddress.Loopback, 0))
            {
                reservation.Start();
                port = ((IPEndPoint)reservation.LocalEndpoint).Port;
            }

            using var owned = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            owned.CancelAfter(TimeSpan.FromSeconds(960));
            Task<ProcessResult>? runner = null, peer = null;
            Task? ready = null;
            ProcessResult? runnerResult = null, peerResult = null;
            DateTimeOffset peerStarted = default, peerEnded = default;
            await using (FileStream log = new(peerLog, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            await using (FileStream errors = new(peerLog + ".stderr.log", FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            await using (FileStream runnerOutput = new(peerLog + ".runner.log", FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            await using (FileStream runnerErrors = new(peerLog + ".runner.stderr.log", FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                try
                {
                    runner = runRunner(port, owned.Token);
                    ready = WaitForQuotaListeningAsync(root, serial, owned.Token);
                    if (await Task.WhenAny(ready, runner) == runner)
                    {
                        runnerResult = await runner;
                        owned.Token.ThrowIfCancellationRequested();
                        throw new ToolFailure("quota_peer_owner_runner_before_listening");
                    }

                    await ready;
                    peerStarted = DateTimeOffset.UtcNow;
                    string[] arguments = [tool, "nativeaot", "service", "quota-peer", "--port", port.ToString(CultureInfo.InvariantCulture), ..wirePath is null ? Array.Empty<string>() : new[]
                    {
                        "--wire-report",
                        wirePath
                    }

                    ];
                    peer = ProcessRunner.RunAsync(new("dotnet", arguments, root)
                    {
                        Timeout = TimeSpan.FromSeconds(MaximumPeerSeconds),
                        OutputCharacterLimit = ReportMaximumBytes + 2 + (observeWire ? 256 : 0)
                    }, owned.Token);
                    var first = await Task.WhenAny(runner, peer);
                    if (first == runner)
                    {
                        runnerResult = await runner;
                        owned.Token.ThrowIfCancellationRequested();
                        RequireQuotaRunnerResult(runnerResult);
                        // Expected22 must leave the still-finishing peer alive.
                        peerResult = await peer;
                        peerEnded = DateTimeOffset.UtcNow;
                    }
                    else
                    {
                        peerResult = await peer;
                        peerEnded = DateTimeOffset.UtcNow;
                        owned.Token.ThrowIfCancellationRequested();
                        RequireQuotaPeerResult(peerResult, port, observeWire);
                        runnerResult = await runner;
                    }

                    owned.Token.ThrowIfCancellationRequested();
                    RequireQuotaRunnerResult(runnerResult);
                    RequireQuotaPeerResult(peerResult, port, observeWire);
                    inputs.VerifyUnchanged();
                }
                finally
                {
                    owned.Cancel();
                    if (ready is not null)
                        try
                        {
                            await ready;
                        }
                        catch
                        {
                        }

                    if (runner is not null)
                        try
                        {
                            runnerResult = await runner;
                        }
                        catch
                        {
                        }

                    if (peer is not null)
                    {
                        try
                        {
                            peerResult = await peer;
                        }
                        catch
                        {
                        }

                        if (peerEnded == default)
                            peerEnded = DateTimeOffset.UtcNow;
                    }

                    if (peerResult is not null)
                    {
                        await WriteOwnedLog(log, peerResult.StandardOutput, ReportMaximumBytes + 2);
                        await WriteOwnedLog(errors, peerResult.StandardError, ReportMaximumBytes + 2);
                    }

                    if (runnerResult is not null)
                    {
                        await WriteOwnedLog(runnerOutput, runnerResult.StandardOutput, OwnerRunnerMaximumBytes);
                        await WriteOwnedLog(runnerErrors, runnerResult.StandardError, OwnerRunnerMaximumBytes);
                    }

                    if (runnerResult is { TerminationConfirmed: false } || peerResult is { TerminationConfirmed: false })
                        throw new ToolFailure("quota_peer_owner_cleanup_unconfirmed");
                }
            }

            cancellation.ThrowIfCancellationRequested();
            inputs.VerifyUnchanged();
            byte[] reportBytes = Encoding.UTF8.GetBytes(peerResult!.StandardOutput);
            var observation = ReadOwnedReport(reportBytes);
            string? wireHash = null;
            WireTrace? wireTrace = null;
            if (wirePath is not null)
            {
                var file = inputs.Open(root, wirePath, "original quota peer wire", "quota_peer_owner_wire", WireMaximumReportBytes, singleLink: true);
                var receipt = ReadWireReceipt(peerResult!.StandardError);
                Need(string.Equals(file.Sha256, receipt.Sha256, StringComparison.OrdinalIgnoreCase) && file.Length == receipt.Bytes, "owner_wire_process_binding");
                wireTrace = InspectWireReport(file.ReadBytes(WireMaximumReportBytes, "quota_peer_owner_wire"));
                RequireOwnedWire(wireTrace, observation);
                wireHash = file.Sha256;
            }

            var result = new Completion(root, port, peerLog, serial, Convert.ToHexString(SHA256.HashData(reportBytes)), runnerResult!, peerResult!, peerStarted, peerEnded, observation, wirePath, wireHash, wireTrace);
            result.Verify(inputs);
            inputs.VerifyUnchanged();
            return result;
        }
    }
}
