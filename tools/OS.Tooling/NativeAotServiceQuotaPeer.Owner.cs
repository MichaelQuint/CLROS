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
            ProcessResult runner, ProcessResult peer, DateTimeOffset started, DateTimeOffset ended, Observation observation,
            string? wirePath = null, string? wireSha256 = null, WireTrace? wire = null)
        {
            Root = root; Port = port; Path = path; SerialPath = serial; Sha256 = hash;
            Runner = runner; Peer = peer; PeerStartedUtc = started; PeerCompletedUtc = ended; Report = observation;
            WirePath = wirePath; WireSha256 = wireSha256; Wire = wire;
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
        /// <summary>Optional separately owned raw-I/O companion. Its local
        /// endpoints/ticks and partial I/O confer no guest/packet authority.</summary>
        public string? WirePath { get; }
        public string? WireSha256 { get; }
        public WireTrace? Wire { get; }

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
            Need((WirePath is null ? error.Length == 0 : error.ReadUtf8(ReportMaximumBytes + 2, "quota_peer_owner_stderr") == Peer.StandardError) &&
                runnerError.Length == 0 && runnerOutput.ReadBytes(OwnerRunnerMaximumBytes, "quota_peer_owner_runner_stdout")
                .AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(Runner.StandardOutput)), "owner_log_identity");
            if (WirePath is string wirePath)
            {
                var wire = files.Open(Root, wirePath, "original quota peer wire", "quota_peer_owner_wire", WireMaximumReportBytes, singleLink: true);
                var receipt = ReadWireReceipt(Peer.StandardError);
                Need(string.Equals(wire.Sha256, WireSha256, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(wire.Sha256, receipt.Sha256, StringComparison.OrdinalIgnoreCase) && wire.Length == receipt.Bytes,
                    "owner_wire_identity");
                RequireOwnedWire(InspectWireReport(wire.ReadBytes(WireMaximumReportBytes, "quota_peer_owner_wire")), Report);
                AuthorityFiles.RequireDistinct(report, error, runnerOutput, runnerError, wire);
            }
            else AuthorityFiles.RequireDistinct(report, error, runnerOutput, runnerError);
        }

        internal static async Task<Completion> CreateOwnedAsync(string root, string peerLog, string serial,
            Func<int, CancellationToken, Task<ProcessResult>> runRunner, CancellationToken cancellation, bool observeWire = false)
        {
            ArgumentNullException.ThrowIfNull(runRunner); cancellation.ThrowIfCancellationRequested();
            root = System.IO.Path.GetFullPath(root); peerLog = System.IO.Path.GetFullPath(peerLog, root);
            serial = System.IO.Path.GetFullPath(serial, root);
            var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            string? wirePath = observeWire ? RequireWireTarget(root, peerLog + ".wire.json") : null;
            string[] logs = [peerLog, peerLog + ".stderr.log", peerLog + ".runner.log", peerLog + ".runner.stderr.log",
                .. wirePath is null ? Array.Empty<string>() : new[] { wirePath }];
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
                    string[] arguments = [tool, "nativeaot", "service", "quota-peer", "--port", port.ToString(CultureInfo.InvariantCulture),
                        .. wirePath is null ? Array.Empty<string>() : new[] { "--wire-report", wirePath }];
                    peer = ProcessRunner.RunAsync(new("dotnet", arguments, root)
                    { Timeout = TimeSpan.FromSeconds(MaximumPeerSeconds), OutputCharacterLimit = ReportMaximumBytes + 2 + (observeWire ? 256 : 0) }, owned.Token);
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
                        RequireQuotaPeerResult(peerResult, port, observeWire);
                        runnerResult = await runner;
                    }
                    owned.Token.ThrowIfCancellationRequested();
                    RequireQuotaRunnerResult(runnerResult); RequireQuotaPeerResult(peerResult, port, observeWire);
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
            var observation = ReadOwnedReport(reportBytes); string? wireHash = null; WireTrace? wireTrace = null;
            if (wirePath is not null)
            {
                var file = inputs.Open(root, wirePath, "original quota peer wire", "quota_peer_owner_wire", WireMaximumReportBytes, singleLink: true);
                var receipt = ReadWireReceipt(peerResult!.StandardError);
                Need(string.Equals(file.Sha256, receipt.Sha256, StringComparison.OrdinalIgnoreCase) && file.Length == receipt.Bytes,
                    "owner_wire_process_binding");
                wireTrace = InspectWireReport(file.ReadBytes(WireMaximumReportBytes, "quota_peer_owner_wire"));
                RequireOwnedWire(wireTrace, observation); wireHash = file.Sha256;
            }
            var result = new Completion(root, port, peerLog, serial, Convert.ToHexString(SHA256.HashData(reportBytes)),
                runnerResult!, peerResult!, peerStarted, peerEnded, observation, wirePath, wireHash, wireTrace);
            result.Verify(inputs); inputs.VerifyUnchanged(); return result;
        }
    }

    /// <summary>Reserve one loopback forward, wait at most120s for the original
    /// listening row, and own one separate quota peer child beside the supplied
    /// runner. The callback must use a bounded process API, honor cancellation and
    /// confirm termination; the existing QEMU runner remains the sole PCAP owner.
    /// The peer has400s startup and540s operation, subject to an independent900s
    /// child bound starting after listen. The independent960s owner clock starts before the
    /// runner, which retains its separately configured bound. Outer caps may
    /// reject before all inner budgets expire. Expected runner22 awaits the peer; every
    /// failure/cancellation cancels and joins both tasks before return/throw.
    /// Existing plain log directories must be under root; both children's fresh stdout/stderr are
    /// retained exactly even on failure. This does not open publication admission
    /// or inspect serial/summary/packets/native/guest cleanup. The QemuSmoke quota
    /// capture-owner guard remains closed until those separate joins exist.</summary>
    public static Task<Completion> RunOwnedAsync(string root, string peerLog, string serial,
        Func<int, CancellationToken, Task<ProcessResult>> runRunner, CancellationToken cancellation = default) =>
        Completion.CreateOwnedAsync(root, peerLog, serial, runRunner, cancellation);


    /// <summary>Explicit observed owner. Owns the same two children and four logs
    /// plus peerLog+.wire.json. Only a complete quota-run matching every original
    /// v1 sample may construct Completion. Partial child failures preserve their
    /// separately written wire evidence and ordinary logs, then reject.
    /// No HTTP/packet/native/kernel/guest authority is added.</summary>
    public static Task<Completion> RunObservedOwnedAsync(string root, string peerLog, string serial,
        Func<int, CancellationToken, Task<ProcessResult>> runRunner, CancellationToken cancellation = default) =>
        Completion.CreateOwnedAsync(root, peerLog, serial, runRunner, cancellation, observeWire: true);

    // Child-only companion sink. The original stdout remains the v1 frame.
    // CreateNew reserves this exact plain artifact leaf before any socket work.
    // The callback closes the writer and holds/verifies the original single-link
    // bytes while RunObservedAsync returns or rethrows its original peer error.
    internal static async Task<Observation> RunWithWireReportAsync(string root, int port, string path,
        CancellationToken cancellation = default)
    {
        path = RequireWireTarget(root, path);
        using AuthorityFiles files = new();
        using FileStream output = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        bool written = false;
        try
        {
            var run = await RunObservedAsync(port, cancellation, trace =>
            {
                Need(!written, "wire_callback_duplicate");
                byte[] bytes = Encoding.UTF8.GetBytes(SerializeWireReport(trace));
                Need(bytes.Length is >0 and <=WireMaximumReportBytes, "wire_report_bounds");
                output.Write(bytes); output.Flush(flushToDisk: true);
                var created = OS.Tooling.Native.NativeFile.GetIdentity(output.SafeFileHandle);
                output.Dispose();
                var saved = files.Open(root, path, "child quota wire", "quota_peer_wire", WireMaximumReportBytes, singleLink: true);
                string hash = Convert.ToHexString(SHA256.HashData(bytes));
                Need(created.Key == saved.InitialIdentity.Key && created.LinkCount == 1 &&
                    saved.InitialIdentity.LinkCount == created.LinkCount && created.Length == (ulong)bytes.Length &&
                    saved.InitialIdentity.Length == created.Length && string.Equals(saved.Sha256, hash, StringComparison.OrdinalIgnoreCase),
                    "wire_report_identity");
                files.VerifyUnchanged(); written = true;
                // Bind the parent's file read to this actual child, rather than
                // learning the expected digest from a later substituted file.
                Console.Error.Write("QUOTA_WIRE_REPORT kind=" + trace.Purpose + " sha256=" + hash + " bytes=" +
                    bytes.Length.ToString(CultureInfo.InvariantCulture) + "\r\n");
            });
            Need(written && run.Wire.Purpose == "quota-run", "wire_report_missing");
            RequireOwnedWire(run.Wire, run.Service); return run.Service;
        }
        finally { output.Dispose(); files.VerifyUnchanged(); }
    }

    private static string RequireWireTarget(string root, string path)
    {
        Need(!string.IsNullOrEmpty(path) && !path.Any(char.IsControl) && System.IO.Path.IsPathFullyQualified(path), "wire_path_absolute");
        string full = System.IO.Path.GetFullPath(path);
        Need(string.Equals(path, full, StringComparison.Ordinal), "wire_path_canonical");
        string leaf = System.IO.Path.GetFileName(full), directory = System.IO.Path.GetDirectoryName(full)!;
        Need(leaf.Length > 0 && leaf.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) < 0 &&
            !leaf.EndsWith('.') && !leaf.EndsWith(' '), "wire_path_leaf");
        if (OperatingSystem.IsWindows())
        {
            string device = leaf.Split('.')[0].ToUpperInvariant();
            Need(device is not ("CON" or "PRN" or "AUX" or "NUL") &&
                !(device.Length == 4 && (device.StartsWith("COM", StringComparison.Ordinal) ||
                device.StartsWith("LPT", StringComparison.Ordinal)) && device[3] is >= '0' and <= '9'), "wire_path_device");
        }
        string artifacts = System.IO.Path.Combine(System.IO.Path.GetFullPath(root), "artifacts");
        AuthorityFiles.RequirePlainPath(artifacts, directory, "quota_peer_wire_path");
        Need(Directory.Exists(directory) && !System.IO.Path.Exists(full), "wire_path_not_fresh");
        return full;
    }

    private static void RequireOwnedWire(WireTrace wire, Observation report)
    {
        Need(wire.Purpose == "quota-run" && wire.Port == report.Port && wire.Samples.Count == report.Samples.Count,
            "owner_wire_report");
        for (int i = 0; i < wire.Samples.Count; i++)
        {
            var actual = wire.Samples[i]; var expected = report.Samples[i];
            Need(actual.Name == expected.Name && actual.ElapsedTicks == expected.ElapsedTicks &&
                actual.BodyBase64 == Convert.ToBase64String(Encoding.ASCII.GetBytes(expected.Body)), "owner_wire_sample");
        }
    }

    private static void RequireQuotaRunnerResult(ProcessResult result)
    {
        QemuTranscripts.RequireCompleteProcess(result, "quota_peer_owner_runner_incomplete");
        Need(result.ExitCode == 22 && result.StandardError.Length == 0, "owner_runner_disposition");
    }

    private readonly record struct WireReceipt(string Sha256, int Bytes);

    /// <summary>Inspect a borrowed canonical successful child receipt and return
    /// copied digest/byte-count values only. Partial/extra/noncanonical records
    /// reject. Supplied stderr text grants no child, file or Completion authority.</summary>
    public static (string Sha256, int Bytes) InspectWireReceiptForTests(string text)
    {
        var receipt = ReadWireReceipt(text); return (receipt.Sha256, receipt.Bytes);
    }

    private static WireReceipt ReadWireReceipt(string text)
    {
        Need(text.Length is >0 and <=256 && text.EndsWith("\r\n", StringComparison.Ordinal), "owner_wire_process_receipt");
        string[] fields = text[..^2].Split(' ', StringSplitOptions.None);
        Need(fields.Length == 4 && fields[0] == "QUOTA_WIRE_REPORT" && fields[1] == "kind=quota-run" &&
            fields[2].StartsWith("sha256=", StringComparison.Ordinal) && fields[3].StartsWith("bytes=", StringComparison.Ordinal),
            "owner_wire_process_receipt");
        string hash = fields[2][7..], count = fields[3][6..];
        Need(hash.Length == 64 && hash.All(char.IsAsciiHexDigit) && hash == hash.ToUpperInvariant() &&
            int.TryParse(count, NumberStyles.None, CultureInfo.InvariantCulture, out int bytes) && bytes is >0 and <=WireMaximumReportBytes &&
            count == bytes.ToString(CultureInfo.InvariantCulture), "owner_wire_process_receipt");
        return new(hash, int.Parse(count, NumberStyles.None, CultureInfo.InvariantCulture));
    }

    private static void RequireQuotaPeerResult(ProcessResult result, int port, bool observeWire = false)
    {
        QemuTranscripts.RequireCompleteProcess(result, "quota_peer_owner_peer_incomplete");
        Need(result.ExitCode == 0 && (observeWire || result.StandardError.Length == 0), "owner_peer_failed");
        if (observeWire) _ = ReadWireReceipt(result.StandardError);
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
