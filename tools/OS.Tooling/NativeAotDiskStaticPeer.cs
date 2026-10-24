using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;

namespace OS.Tooling;

/// <summary>Own an initial bounded disk HTTP diagnostic and its original runner.
/// Fresh response bodies and partial failures are retained. This deliberately
/// supplies no complete guest/native/packet or Blazor acceptance authority.</summary>
public static class NativeAotDiskStaticPeer
{
    public sealed record DiagnosticCompletion(int Port, string Directory, ProcessResult Runner);

    public static async Task<DiagnosticCompletion> RunDiagnosticAsync(string root, string serial,
        Func<int, CancellationToken, Task<ProcessResult>> runRunner, CancellationToken cancellation)
    {
        root = Path.GetFullPath(root); serial = Path.GetFullPath(serial, root);
        string directory = serial + ".disk-http";
        if (!directory.StartsWith(Path.Combine(root, "artifacts") + Path.DirectorySeparatorChar,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) || Path.Exists(directory))
            throw new ToolFailure("disk_http_fresh_artifacts_required");
        AuthorityFiles.RequirePlainPath(root, Path.GetDirectoryName(directory)!, "disk_http_path");
        Directory.CreateDirectory(directory);
        int port;
        using (TcpListener reservation = new(IPAddress.Loopback, 0))
        { reservation.Start(); port = ((IPEndPoint)reservation.LocalEndpoint).Port; }
        using var owned = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        Task<ProcessResult> runner = runRunner(port, owned.Token);
        Task? peer = null;
        try
        {
            peer = Observe();
            if (await Task.WhenAny(peer, runner) == runner) throw new ToolFailure("disk_http_runner_exited_before_diagnostic");
            await peer;
            var result = await runner;
            QemuTranscripts.RequireCompleteProcess(result, "disk_http_runner_incomplete");
            if (!result.Succeeded) throw new ToolFailure("disk_http_runner_failed");
            return new(port, directory, result);
        }
        finally
        {
            owned.Cancel();
            if (peer is not null) try { await peer; } catch { }
            try { _ = await runner; } catch { }
        }

        async Task Observe()
        {
            using var startup = CancellationTokenSource.CreateLinkedTokenSource(owned.Token);
            startup.CancelAfter(TimeSpan.FromSeconds(240));
            await NativeAotSocketPeer.WaitForSerialAsync(serial,
                text => text.Contains("M10_018_LISTENING ", StringComparison.Ordinal), startup.Token);
            using var matrix = CancellationTokenSource.CreateLinkedTokenSource(owned.Token);
            matrix.CancelAfter(TimeSpan.FromSeconds(110));
            using HttpClient client = new(new SocketsHttpHandler
            { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.None, UseProxy = false,
                MaxConnectionsPerServer = 1 })
            { BaseAddress = new Uri("http://127.0.0.1:" + port), Timeout = TimeSpan.FromSeconds(60),
                DefaultRequestVersion = HttpVersion.Version11, DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact };
            List<object> rows = [];
            try
            {
                foreach (string path in new[] { "/healthz", "/api/status", "/api/items", "/index.html", "/fixture-nonce.txt" })
                {
                    using var response = await client.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, matrix.Token);
                    using var source = await response.Content.ReadAsStreamAsync(matrix.Token);
                    using MemoryStream body = new(); byte[] buffer = new byte[8192];
                    for (int read; (read = await source.ReadAsync(buffer, matrix.Token)) != 0;)
                    { if (body.Length + read > 65536) throw new ToolFailure("disk_http_diagnostic_capacity"); body.Write(buffer, 0, read); }
                    byte[] bytes = body.ToArray(); string file = rows.Count.ToString("D3") + ".body";
                    using (FileStream output = new(Path.Combine(directory, file), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                    { await output.WriteAsync(bytes, matrix.Token); output.Flush(true); }
                    rows.Add(new { Path = path, Status = (int)response.StatusCode, Bytes = bytes.Length,
                        Sha256 = Convert.ToHexString(SHA256.HashData(bytes)), File = file,
                        Headers = response.Headers.Concat(response.Content.Headers).Select(x => x.Key + ": " + string.Join(",", x.Value)).ToArray() });
                    if (response.StatusCode != HttpStatusCode.OK) throw new ToolFailure("disk_http_diagnostic_status " + path);
                }
            }
            finally
            {
                using FileStream receipt = new(Path.Combine(directory, "diagnostic.json"), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                await JsonSerializer.SerializeAsync(receipt, new { Port = port, Responses = rows, CompleteGuestAuthority = false }, cancellationToken: cancellation);
                receipt.Flush(true);
            }
        }
    }
}
