using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace OS.Tooling;

public static partial class NativeAotServiceQuotaPeer
{
    private const int OwnerRunnerMaximumBytes = 64 * 1024 * 1024;
    /// <summary>Original owned peer/runner results and copied report identity.
    /// Only the process-owning factory constructs this object. Expected runner22
    /// is a capture disposition, not guest, quota, packet or cleanup acceptance.</summary>
    public sealed class Completion
    {
        private Completion(string root, int port, string path, string serial, string hash,
            ProcessResult runner, ProcessResult peer, DateTimeOffset started, DateTimeOffset ended, Observation observation)
        {
            Root = root; Port = port; Path = path; SerialPath = serial; Sha256 = hash;
            Runner = runner; Peer = peer; PeerStartedUtc = started; PeerCompletedUtc = ended; Report = observation;
        }

        internal string Root { get; }
        public int Port { get; }
        public string Path { get; }
        public string SerialPath { get; }
        public string RunnerOutputPath => Path + ".runner.log";
        public string RunnerErrorPath => Path + ".runner.stderr.log";
        public string Sha256 { get; }
        public ProcessResult Runner { get; }
        public ProcessResult Peer { get; }
        public DateTimeOffset PeerStartedUtc { get; }
        public DateTimeOffset PeerCompletedUtc { get; }
        public Observation Report { get; }

        internal void Verify(AuthorityFiles files)
        {
            var report = files.Open(Root, Path, "original quota peer report", "quota_peer_owner_report", ReportMaximumBytes + 2, singleLink: true);
            byte[] bytes = report.ReadBytes(ReportMaximumBytes + 2, "quota_peer_owner_report");
            Need(Convert.ToHexString(SHA256.HashData(bytes)) == Sha256 && ReadOwnedReport(bytes).Port == Port, "owner_report_identity");
            var error = files.Open(Root, Path + ".stderr.log", "original quota peer stderr", "quota_peer_owner_stderr",
                ReportMaximumBytes + 2, allowEmpty: true, singleLink: true);
            var runnerOutput = files.Open(Root, RunnerOutputPath, "original quota runner stdout", "quota_peer_owner_runner_stdout",
                OwnerRunnerMaximumBytes, allowEmpty: true, singleLink: true);
            var runnerError = files.Open(Root, RunnerErrorPath, "original quota runner stderr", "quota_peer_owner_runner_stderr",
                OwnerRunnerMaximumBytes, allowEmpty: true, singleLink: true);
            Need(error.Length == 0 && runnerError.Length == 0 && runnerOutput.ReadBytes(OwnerRunnerMaximumBytes, "quota_peer_owner_runner_stdout")
                .AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(Runner.StandardOutput)), "owner_log_identity");
            AuthorityFiles.RequireDistinct(report, error, runnerOutput, runnerError);
        }

        internal static async Task<Completion> CreateOwnedAsync(string root, string peerLog, string serial,
            Func<int, CancellationToken, Task<ProcessResult>> runRunner, CancellationToken cancellation)
        {
            ArgumentNullException.ThrowIfNull(runRunner); cancellation.ThrowIfCancellationRequested();
            root = System.IO.Path.GetFullPath(root); peerLog = System.IO.Path.GetFullPath(peerLog, root);
            serial = System.IO.Path.GetFullPath(serial, root);
            var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            string[] logs = [peerLog, peerLog + ".stderr.log", peerLog + ".runner.log", peerLog + ".runner.stderr.log"];
            Need(!logs.Contains(serial, comparer), "owner_path_alias");
            AuthorityFiles.RequirePlainPath(root, System.IO.Path.GetDirectoryName(peerLog)!, "quota_peer_owner_log_path");
            AuthorityFiles.RequirePlainPath(root, System.IO.Path.GetDirectoryName(serial)!, "quota_peer_owner_serial_path");
            Need(logs.All(path => !System.IO.Path.Exists(path)), "owner_log_not_fresh");
            Need(!System.IO.Path.Exists(serial), "owner_serial_not_fresh");

            using AuthorityFiles inputs = new(); string tool = HostTools.ManagedTool(root, "OS.Tooling");
            _ = inputs.Open(System.IO.Path.GetDirectoryName(tool)!, tool, "quota peer executable", "quota_peer_owner_executable", 32 * 1024 * 1024, singleLink: true);
            int port; using (var reservation = new TcpListener(IPAddress.Loopback, 0))
            { reservation.Start(); port = ((IPEndPoint)reservation.LocalEndpoint).Port; }
            using var owned = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            owned.CancelAfter(TimeSpan.FromSeconds(960));
            Task<ProcessResult>? runner = null, peer = null; Task? ready = null;
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
                    { runnerResult = await runner; owned.Token.ThrowIfCancellationRequested(); throw new ToolFailure("quota_peer_owner_runner_before_listening"); }
                    await ready;
                    peerStarted = DateTimeOffset.UtcNow;
                    peer = ProcessRunner.RunAsync(new("dotnet", [tool, "nativeaot", "service", "quota-peer", "--port", port.ToString(CultureInfo.InvariantCulture)], root)
                    { Timeout = TimeSpan.FromSeconds(600), OutputCharacterLimit = ReportMaximumBytes + 2 }, owned.Token);
                    var first = await Task.WhenAny(runner, peer);
                    if (first == runner)
                    {
                        runnerResult = await runner; owned.Token.ThrowIfCancellationRequested(); RequireQuotaRunnerResult(runnerResult);
                        // Expected22 must leave the still-finishing peer alive.
                        peerResult = await peer; peerEnded = DateTimeOffset.UtcNow;
                    }
                    else
                    {
                        peerResult = await peer; peerEnded = DateTimeOffset.UtcNow;
                        owned.Token.ThrowIfCancellationRequested();
                        RequireQuotaPeerResult(peerResult, port);
                        runnerResult = await runner;
                    }
                    owned.Token.ThrowIfCancellationRequested();
                    RequireQuotaRunnerResult(runnerResult); RequireQuotaPeerResult(peerResult, port);
                    inputs.VerifyUnchanged();
                }
                finally
                {
                    owned.Cancel();
                    if (ready is not null) try { await ready; } catch { }
                    if (runner is not null) try { runnerResult = await runner; } catch { }
                    if (peer is not null)
                    {
                        try { peerResult = await peer; } catch { }
                        if (peerEnded == default) peerEnded = DateTimeOffset.UtcNow;
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
            cancellation.ThrowIfCancellationRequested(); inputs.VerifyUnchanged();
            byte[] reportBytes = Encoding.UTF8.GetBytes(peerResult!.StandardOutput);
            var result = new Completion(root, port, peerLog, serial, Convert.ToHexString(SHA256.HashData(reportBytes)),
                runnerResult!, peerResult!, peerStarted, peerEnded, ReadOwnedReport(reportBytes));
            result.Verify(inputs); inputs.VerifyUnchanged(); return result;
        }
    }

    /// <summary>Reserve one loopback forward, wait at most120s for the original
    /// listening row, and own one separate quota peer child beside the supplied
    /// runner. The callback must use a bounded process API, honor cancellation and
    /// confirm termination; the existing QEMU runner remains the sole PCAP owner.
    /// The peer has400s startup plus180s operation within a600s child bound; the
    /// aggregate owner bound is960s. Expected runner22 awaits the peer; every
    /// failure/cancellation cancels and joins both tasks before return/throw.
    /// Existing plain log directories must be under root; both children's fresh stdout/stderr are
    /// retained exactly even on failure. This does not open publication admission
    /// or inspect serial/summary/packets/native/guest cleanup. The QemuSmoke quota
    /// capture-owner guard remains closed until those separate joins exist.</summary>
    public static Task<Completion> RunOwnedAsync(string root, string peerLog, string serial,
        Func<int, CancellationToken, Task<ProcessResult>> runRunner, CancellationToken cancellation = default) =>
        Completion.CreateOwnedAsync(root, peerLog, serial, runRunner, cancellation);

    private static void RequireQuotaRunnerResult(ProcessResult result)
    {
        QemuTranscripts.RequireCompleteProcess(result, "quota_peer_owner_runner_incomplete");
        Need(result.ExitCode == 22 && result.StandardError.Length == 0, "owner_runner_disposition");
    }

    private static void RequireQuotaPeerResult(ProcessResult result, int port)
    {
        QemuTranscripts.RequireCompleteProcess(result, "quota_peer_owner_peer_incomplete");
        Need(result.ExitCode == 0 && result.StandardError.Length == 0, "owner_peer_failed");
        Need(ReadOwnedReport(Encoding.UTF8.GetBytes(result.StandardOutput)).Port == port, "owner_peer_port");
    }

    private static async Task WriteOwnedLog(FileStream stream, string text, int maximum)
    {
        Need(Encoding.UTF8.GetByteCount(text) <= maximum, "owner_log_bounds");
        await stream.WriteAsync(Encoding.UTF8.GetBytes(text), CancellationToken.None);
    }

    private static Observation ReadOwnedReport(ReadOnlySpan<byte> bytes)
    {
        Need(bytes.Length is > 2 and <= ReportMaximumBytes + 2 && bytes[^2] == 13 && bytes[^1] == 10, "owner_report_frame");
        return InspectReport(bytes[..^2]);
    }

    private static async Task WaitForQuotaListeningAsync(string root, string serial, CancellationToken cancellation)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation); deadline.CancelAfter(TimeSpan.FromSeconds(120));
        long offset = 0; string tail = ""; byte[] buffer = new byte[8192];
        while (true)
        {
            deadline.Token.ThrowIfCancellationRequested();
            if (File.Exists(serial))
            {
                AuthorityFiles.RequirePlainPath(root, serial, "quota_peer_owner_serial_path");
                using var input = new FileStream(serial, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                Need(input.Length >= offset && input.Length <= 128 * 1024 * 1024, "owner_serial_bounds"); input.Position = offset;
                for (int count; (count = await input.ReadAsync(buffer, deadline.Token)) != 0;)
                {
                    offset += count; tail += Encoding.ASCII.GetString(buffer, 0, count);
                    if (tail.Contains("\nCLROS_M16_014_LISTENING\r\n", StringComparison.Ordinal)) return;
                    if (tail.Length > 16384) tail = tail[^8192..];
                }
            }
            await Task.Delay(100, deadline.Token);
        }
    }
}
