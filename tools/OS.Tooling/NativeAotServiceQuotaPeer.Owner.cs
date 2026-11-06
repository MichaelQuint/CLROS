using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace OS.Tooling;

public static partial class NativeAotServiceQuotaPeer
{
    private const int OwnerRunnerMaximumBytes = 64 * 1024 * 1024;
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

    public static Task<Completion> RunOwnedAsync(string root, string peerLog, string serial, Func<int, CancellationToken, Task<ProcessResult>> runRunner, CancellationToken cancellation = default) => Completion.CreateOwnedAsync(root, peerLog, serial, runRunner, cancellation);
    /// <summary>Explicit observed owner. Owns the same two children and four logs
    /// plus peerLog+.wire.json. Only a complete quota-run matching every original
    /// v1 sample may construct Completion. Partial child failures preserve their
    /// separately written wire evidence and ordinary logs, then reject.
    /// No HTTP/packet/native/kernel/guest authority is added.</summary>

    public static Task<Completion> RunObservedOwnedAsync(string root, string peerLog, string serial, Func<int, CancellationToken, Task<ProcessResult>> runRunner, CancellationToken cancellation = default) => Completion.CreateOwnedAsync(root, peerLog, serial, runRunner, cancellation, observeWire: true);
    // Child-only companion sink. The original stdout remains the v1 frame.
    // CreateNew reserves this exact plain artifact leaf before any socket work.
    // The callback closes the writer and holds/verifies the original single-link
    // bytes while RunObservedAsync returns or rethrows its original peer error.
    internal static async Task<Observation> RunWithWireReportAsync(string root, int port, string path, CancellationToken cancellation = default)
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
                Need(bytes.Length is > 0 and <= WireMaximumReportBytes, "wire_report_bounds");
                output.Write(bytes);
                output.Flush(flushToDisk: true);
                var created = OS.Tooling.Native.NativeFile.GetIdentity(output.SafeFileHandle);
                output.Dispose();
                var saved = files.Open(root, path, "child quota wire", "quota_peer_wire", WireMaximumReportBytes, singleLink: true);
                string hash = Convert.ToHexString(SHA256.HashData(bytes));
                Need(created.Key == saved.InitialIdentity.Key && created.LinkCount == 1 && saved.InitialIdentity.LinkCount == created.LinkCount && created.Length == (ulong)bytes.Length && saved.InitialIdentity.Length == created.Length && string.Equals(saved.Sha256, hash, StringComparison.OrdinalIgnoreCase), "wire_report_identity");
                files.VerifyUnchanged();
                written = true;
                // Bind the parent's file read to this actual child, rather than
                // learning the expected digest from a later substituted file.
                Console.Error.Write("QUOTA_WIRE_REPORT kind=" + trace.Purpose + " sha256=" + hash + " bytes=" + bytes.Length.ToString(CultureInfo.InvariantCulture) + "\r\n");
            });
            Need(written && run.Wire.Purpose == "quota-run", "wire_report_missing");
            RequireOwnedWire(run.Wire, run.Service);
            return run.Service;
        }
        finally
        {
            output.Dispose();
            files.VerifyUnchanged();
        }
    }

    private static string RequireWireTarget(string root, string path)
    {
        Need(!string.IsNullOrEmpty(path) && !path.Any(char.IsControl) && System.IO.Path.IsPathFullyQualified(path), "wire_path_absolute");
        string full = System.IO.Path.GetFullPath(path);
        Need(string.Equals(path, full, StringComparison.Ordinal), "wire_path_canonical");
        string leaf = System.IO.Path.GetFileName(full), directory = System.IO.Path.GetDirectoryName(full)!;
        Need(leaf.Length > 0 && leaf.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) < 0 && !leaf.EndsWith('.') && !leaf.EndsWith(' '), "wire_path_leaf");
        if (OperatingSystem.IsWindows())
        {
            string device = leaf.Split('.')[0].ToUpperInvariant();
            Need(device is not ("CON" or "PRN" or "AUX" or "NUL") && !(device.Length == 4 && (device.StartsWith("COM", StringComparison.Ordinal) || device.StartsWith("LPT", StringComparison.Ordinal)) && device[3] is >= '0' and <= '9'), "wire_path_device");
        }

        string artifacts = System.IO.Path.Combine(System.IO.Path.GetFullPath(root), "artifacts");
        AuthorityFiles.RequirePlainPath(artifacts, directory, "quota_peer_wire_path");
        Need(Directory.Exists(directory) && !System.IO.Path.Exists(full), "wire_path_not_fresh");
        return full;
    }

    private static void RequireOwnedWire(WireTrace wire, Observation report)
    {
        Need(wire.Purpose == "quota-run" && wire.Port == report.Port && wire.Samples.Count == report.Samples.Count, "owner_wire_report");
        for (int i = 0; i < wire.Samples.Count; i++)
        {
            var actual = wire.Samples[i];
            var expected = report.Samples[i];
            Need(actual.Name == expected.Name && actual.ElapsedTicks == expected.ElapsedTicks && actual.BodyBase64 == Convert.ToBase64String(Encoding.ASCII.GetBytes(expected.Body)), "owner_wire_sample");
        }
    }

    private static void RequireQuotaRunnerResult(ProcessResult result)
    {
        QemuTranscripts.RequireCompleteProcess(result, "quota_peer_owner_runner_incomplete");
        Need(result.ExitCode == 22 && result.StandardError.Length == 0, "owner_runner_disposition");
    }

    /// <summary>Inspect a borrowed canonical successful child receipt and return
    /// copied digest/byte-count values only. Partial/extra/noncanonical records
    /// reject. Supplied stderr text grants no child, file or Completion authority.</summary>

    public static (string Sha256, int Bytes) InspectWireReceiptForTests(string text)
    {
        var receipt = ReadWireReceipt(text);
        return (receipt.Sha256, receipt.Bytes);
    }

    private static WireReceipt ReadWireReceipt(string text)
    {
        Need(text.Length is > 0 and <= 256 && text.EndsWith("\r\n", StringComparison.Ordinal), "owner_wire_process_receipt");
        string[] fields = text[..^2].Split(' ', StringSplitOptions.None);
        Need(fields.Length == 4 && fields[0] == "QUOTA_WIRE_REPORT" && fields[1] == "kind=quota-run" && fields[2].StartsWith("sha256=", StringComparison.Ordinal) && fields[3].StartsWith("bytes=", StringComparison.Ordinal), "owner_wire_process_receipt");
        string hash = fields[2][7..], count = fields[3][6..];
        Need(hash.Length == 64 && hash.All(char.IsAsciiHexDigit) && hash == hash.ToUpperInvariant() && int.TryParse(count, NumberStyles.None, CultureInfo.InvariantCulture, out int bytes) && bytes is > 0 and <= WireMaximumReportBytes && count == bytes.ToString(CultureInfo.InvariantCulture), "owner_wire_process_receipt");
        return new(hash, int.Parse(count, NumberStyles.None, CultureInfo.InvariantCulture));
    }

    private static void RequireQuotaPeerResult(ProcessResult result, int port, bool observeWire = false)
    {
        QemuTranscripts.RequireCompleteProcess(result, "quota_peer_owner_peer_incomplete");
        Need(result.ExitCode == 0 && (observeWire || result.StandardError.Length == 0), "owner_peer_failed");
        if (observeWire)
            _ = ReadWireReceipt(result.StandardError);
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
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(120));
        long offset = 0;
        string tail = "";
        byte[] buffer = new byte[8192];
        while (true)
        {
            deadline.Token.ThrowIfCancellationRequested();
            if (File.Exists(serial))
            {
                AuthorityFiles.RequirePlainPath(root, serial, "quota_peer_owner_serial_path");
                using var input = new FileStream(serial, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                Need(input.Length >= offset && input.Length <= 128 * 1024 * 1024, "owner_serial_bounds");
                input.Position = offset;
                for (int count; (count = await input.ReadAsync(buffer, deadline.Token)) != 0;)
                {
                    offset += count;
                    tail += Encoding.ASCII.GetString(buffer, 0, count);
                    if (tail.Contains("\nCLROS_M16_014_LISTENING\r\n", StringComparison.Ordinal))
                        return;
                    if (tail.Length > 16384)
                        tail = tail[^8192..];
                }
            }

            await Task.Delay(100, deadline.Token);
        }
    }
}
