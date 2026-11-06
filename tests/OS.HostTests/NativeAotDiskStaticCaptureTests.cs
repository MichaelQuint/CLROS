using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OS.Tooling;

// Real in-process host Kestrel exercises peer ownership only. Synthetic
// readiness and runner values grant no CLROS, native, disk or guest authority.
internal static partial class NativeAotDiskStaticCaptureTests
{
    [HostTest("disk static startup admits its four loads plus63 retained ranges without widening historical profiles")]
    private static void StartupRanges()
    {
        QemuTranscripts.InspectLinuxStartupRangeValues(67, 4, diskStatic: true);
        QemuTranscripts.InspectLinuxStartupRangeValues(65, 2, diskFiles: true);
        QemuTranscripts.InspectLinuxStartupRangeValues(64, 4);
        void Reject(Action action)
        {
            bool rejected = false;
            try
            {
                action();
            }
            catch (Exception e) when (e is ToolFailure or InvalidDataException)
            {
                rejected = true;
            }

            HostCheck.True(rejected);
        }

        foreach (ulong count in new ulong[]
        {
            64,
            65,
            66,
            68,
            135
        }

        )
            Reject(() => QemuTranscripts.InspectLinuxStartupRangeValues(count, 4, diskStatic: true));
        Reject(() => QemuTranscripts.InspectLinuxStartupRangeValues(67, 3, diskStatic: true));
        Reject(() => QemuTranscripts.InspectLinuxStartupRangeValues(65, 4));
        Reject(() => QemuTranscripts.InspectLinuxStartupRangeValues(67, 4, diskFiles: true));
        Reject(() => QemuTranscripts.InspectLinuxStartupRangeValues(67, 4, diskFiles: true, diskStatic: true));
        // Selecting the public mode never bypasses canonical package admission.
        Reject(() => QemuTranscripts.ValidateLinuxStartupTail([], LinuxDiskFileProofFixture.CreatePackage(), diskStatic: true));
    }

    [HostTest("disk static peer completes five real host middleware and API responses and stops its endpoint")]
    private static async Task Healthy() => await Run(Mode.Healthy);
    [HostTest("disk static peer preserves a valid incomplete wrong-status receipt and joins its cancelled host")]
    private static async Task WrongStatus() => await Run(Mode.WrongStatus);
    [HostTest("disk static peer preserves header transport error before owner cancellation and joins its runner")]
    private static async Task ResetHeaders()
    {
        var host = new Host(Mode.ResetHeaders);
        bool failed = false;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            _ = await NativeAotDiskStaticPeer.RunAsync(host.Root, host.Serial, host.RunAsync, deadline.Token);
        }
        catch (HttpRequestException)
        {
            failed = true;
        }

        HostCheck.True(failed && host.Joined && host.Launches == 1);
        await RequireStopped(host.Port);
        using var failure = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(host.Destination, "failure-000.json")));
        var value = failure.RootElement;
        HostCheck.Equal("headers", value.GetProperty("Stage").GetString());
        HostCheck.True(!value.GetProperty("OwnerCancelled").GetBoolean() && !value.GetProperty("CallerCancelled").GetBoolean());
        HostCheck.True(value.GetProperty("Exceptions")[0].GetProperty("HttpRequestError").GetString() is { Length: > 0 });
        using var joined = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(host.Destination, "runner.json")));
        HostCheck.True(joined.RootElement.GetProperty("Joined").GetBoolean());
        using var receipt = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(host.Destination, "http.json")));
        HostCheck.True(!receipt.RootElement.GetProperty("Complete").GetBoolean() && receipt.RootElement.GetProperty("Responses").GetArrayLength() == 0);
    }

    [HostTest("disk static peer external cancellation during a response body preserves its receipt and joins its host")]
    private static async Task CancelBody() => await Run(Mode.CancelBody);
    [HostTest("disk static peer rejects an existing output directory before launching its host owner")]
    private static async Task FreshDirectory()
    {
        var host = new Host(Mode.Healthy);
        Directory.CreateDirectory(host.Destination);
        string marker = Path.Combine(host.Destination, "preserved.txt");
        await File.WriteAllTextAsync(marker, "existing evidence");
        bool rejected = false;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await NativeAotDiskStaticPeer.RunAsync(host.Root, host.Serial, host.RunAsync, deadline.Token);
        }
        catch (ToolFailure error) when (error.Reason == "disk_http_fresh_artifacts_required")
        {
            rejected = true;
        }

        HostCheck.True(rejected && host.Launches == 0);
        HostCheck.Equal("existing evidence", await File.ReadAllTextAsync(marker));
        HostCheck.True(Directory.GetFiles(host.Destination).Length == 1 && !File.Exists(host.Serial));
    }

    private static async Task Run(Mode mode)
    {
        var host = new Host(mode);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(50));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        var bodyCopied = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<NativeAotDiskStaticPeer.Completion> operation = mode == Mode.CancelBody ? NativeAotDiskStaticPeer.RunCoreAsync(host.Root, host.Serial, host.RunAsync, cancellation.Token, null, null, (ordinal, copied) =>
        {
            if (ordinal == 3 && copied == 8)
                bodyCopied.TrySetResult(copied);
        }) : NativeAotDiskStaticPeer.RunAsync(host.Root, host.Serial, host.RunAsync, cancellation.Token);
        NativeAotDiskStaticPeer.Completion? completion = null;
        bool wrongStatus = false, cancelled = false;
        try
        {
            if (mode == Mode.CancelBody)
            {
                await Task.WhenAll(host.BodyStarted.Task, bodyCopied.Task).WaitAsync(TimeSpan.FromSeconds(20), deadline.Token);
                HostCheck.Equal(8L, await bodyCopied.Task);
                HostCheck.True(host.Index.Length > 8 && !deadline.IsCancellationRequested && File.Exists(host.Serial) && !operation.IsCompleted);
                cancellation.Cancel();
            }

            try
            {
                completion = await operation;
            }
            catch (ToolFailure error) when (mode == Mode.WrongStatus && error.Reason == "disk_http_status /healthz")
            {
                wrongStatus = true;
            }
            catch (OperationCanceledException) when (mode == Mode.CancelBody && cancellation.IsCancellationRequested)
            {
                cancelled = true;
            }
        }
        finally
        {
            cancellation.Cancel();
            // Join even when an assertion or the readiness rendezvous fails.
            try
            {
                await operation;
            }
            catch
            {
            }
        }

        HostCheck.True(host.Launches == 1 && host.Joined);
        using (var joined = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(host.Destination, "runner.json"))))
        {
            HostCheck.Equal("clros-disk-http-runner-v1", joined.RootElement.GetProperty("Schema").GetString());
            HostCheck.True(joined.RootElement.GetProperty("Joined").GetBoolean());
        }

        if (mode != Mode.Healthy)
        {
            int failedOrdinal = mode == Mode.WrongStatus ? 0 : 3;
            using var failure = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(host.Destination, "failure-" + failedOrdinal.ToString("D3", CultureInfo.InvariantCulture) + ".json")));
            var failed = failure.RootElement;
            HostCheck.Equal("clros-disk-http-failure-v1", failed.GetProperty("Schema").GetString());
            HostCheck.Equal(failedOrdinal, failed.GetProperty("Ordinal").GetInt32());
            HostCheck.Equal(mode == Mode.WrongStatus ? "validate" : "body", failed.GetProperty("Stage").GetString());
            HostCheck.True(failed.GetProperty("Exceptions").GetArrayLength() > 0);
            HostCheck.True(failed.GetProperty("CallerCancelled").GetBoolean() == (mode == Mode.CancelBody));
            if (mode == Mode.WrongStatus)
                HostCheck.True(!failed.GetProperty("OwnerCancelled").GetBoolean());
        }

        await RequireStopped(host.Port);
        byte[] receiptBytes = await File.ReadAllBytesAsync(Path.Combine(host.Destination, "http.json"));
        using var receipt = JsonDocument.Parse(receiptBytes);
        var root = receipt.RootElement;
        HostCheck.Equal(DiskStaticHttpContract.Schema, root.GetProperty("Schema").GetString());
        HostCheck.Equal(DiskStaticHttpContract.Scope, root.GetProperty("Scope").GetString());
        HostCheck.Equal(host.Port, root.GetProperty("Port").GetInt32());
        HostCheck.True(root.GetProperty("ClockFrequency").GetInt64() > 0);
        var responses = root.GetProperty("Responses");
        bool healthy = mode == Mode.Healthy;
        HostCheck.True(root.GetProperty("Complete").GetBoolean() == healthy);
        int count = healthy ? 5 : mode == Mode.WrongStatus ? 1 : 3;
        HostCheck.Equal(count, responses.GetArrayLength());
        HostCheck.True(host.Requests.SequenceEqual(DiskStaticHttpContract.Paths.Take(healthy ? 5 : mode == Mode.WrongStatus ? 1 : 4)));
        long finished = 0;
        for (int i = 0; i < count; i++)
        {
            var row = responses[i];
            string name = i.ToString("D3", CultureInfo.InvariantCulture) + ".body";
            byte[] body = await File.ReadAllBytesAsync(Path.Combine(host.Destination, name));
            HostCheck.True(row.GetProperty("Ordinal").GetInt32() == i && row.GetProperty("Method").GetString() == "GET" && row.GetProperty("Path").GetString() == DiskStaticHttpContract.Paths[i] && row.GetProperty("BodyFile").GetString() == name && row.GetProperty("Bytes").GetInt32() == body.Length && row.GetProperty("Sha256").GetString() == Convert.ToHexString(SHA256.HashData(body)) && row.GetProperty("Status").GetInt32() == (mode == Mode.WrongStatus ? 503 : 200));
            long started = row.GetProperty("Started").GetInt64(), ended = row.GetProperty("Finished").GetInt64();
            HostCheck.True(started > 0 && started >= finished && ended >= started);
            finished = ended;
            if (mode == Mode.WrongStatus)
                HostCheck.True(body.AsSpan().SequenceEqual("host-unavailable"u8));
            else if (i == 0)
                HostCheck.True(body.AsSpan().SequenceEqual("OS_MINIMAL_API_OK"u8));
            else if (i == 1)
            {
                using var json = JsonDocument.Parse(body);
                HostCheck.True(json.RootElement.GetProperty("ready").GetBoolean() && json.RootElement.GetProperty("profile").GetString() == "v1" && json.RootElement.GetProperty("message").GetString() == "CLROS ready \u2014 caf\u00e9");
            }
            else if (i == 2)
            {
                using var json = JsonDocument.Parse(body);
                var items = json.RootElement.GetProperty("items");
                HostCheck.True(items.GetArrayLength() == 2 && items[0].GetProperty("name").GetString() == "Blazor" && items[1].GetProperty("name").GetString() == "FAT32" && items[1].GetProperty("detail").ValueKind == JsonValueKind.Null);
            }
            else
                HostCheck.True(body.AsSpan().SequenceEqual(i == 3 ? host.Index : host.Nonce));
        }

        if (healthy)
        {
            HostCheck.True(completion is not null && completion.Runner.Succeeded && completion.Port == host.Port && completion.Directory == host.Destination && completion.ReceiptSha256 == Convert.ToHexString(SHA256.HashData(receiptBytes)));
            using AuthorityFiles held = new();
            var actual = held.Open(host.Root, Path.Combine(host.Destination, "http.json"), "host peer receipt", "disk_http_host_receipt", 65536, singleLink: true);
            // Exercise the actual held-file lower-case digest against the
            // producer's upper-case digest, as the original consumer does.
            QemuTranscripts.InspectDiskStaticReceiptIdentityValues(actual.Sha256, completion!.ReceiptSha256, "{}", "{}");
            void RejectReceipt(string hash, string image, string reason)
            {
                bool rejected = false;
                try
                {
                    QemuTranscripts.InspectDiskStaticReceiptIdentityValues(actual.Sha256, hash, image, "{}");
                }
                catch (ToolFailure failure) when (failure.Reason == reason)
                {
                    rejected = true;
                }

                HostCheck.True(rejected);
            }

            string changed = (completion.ReceiptSha256[0] == '0' ? "1" : "0") + completion.ReceiptSha256[1..];
            RejectReceipt(changed, "{}", "disk_static_http_receipt_identity");
            RejectReceipt(new string('G', 64), "{}", "disk_static_http_receipt_identity");
            RejectReceipt(completion.ReceiptSha256, "{}\n", "disk_static_image_receipt_identity");
            held.VerifyUnchanged();
            using var stopped = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(host.Destination, "stopped.json")));
            HostCheck.True(stopped.RootElement.GetProperty("Port").GetInt32() == host.Port && stopped.RootElement.GetProperty("EndpointRefused").GetBoolean() && stopped.RootElement.GetProperty("RunnerExit").GetInt32() == 0);
        }
        else
        {
            HostCheck.True(host.OwnerCancelled && completion is null && !File.Exists(Path.Combine(host.Destination, "stopped.json")));
            HostCheck.True(mode == Mode.WrongStatus ? wrongStatus : cancelled && host.BodyFinished.Task.IsCompletedSuccessfully);
            if (mode == Mode.CancelBody)
                HostCheck.True(!File.Exists(Path.Combine(host.Destination, "003.body")));
        }
    }

    private static async Task RequireStopped(int port)
    {
        using Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        bool refused = false;
        try
        {
            await socket.ConnectAsync(IPAddress.Loopback, port, deadline.Token);
        }
        catch (SocketException error) when (error.SocketErrorCode == SocketError.ConnectionRefused)
        {
            refused = true;
        }

        HostCheck.True(refused);
    }

    [HostTest("disk static live boundary rejects BAR owner aliases wrong arena size and out-of-lifetime block clocks")]
    private static void LiveBoundary()
    {
        const ulong user = 0x100000, kernel = 0x101000, dma = 0x200000, prior = 0x300000, nicBar = 0x400000, diskBar = 0x500000, owner = 0x900000, pool = 0x1000000, arena = 0x2000000;
        var parent = new QemuTranscripts.WebParentReport(1, user, kernel, 10_000_000, 100_000_000, dma, 90, 99, 1, default!);
        NativeDiskReadObservations.Observation[] io = [new(NativeDiskReadObservations.Kind.Submitted, owner, 1, 1, 4096, 1, 0, 1, 0, 100, ""), new(NativeDiskReadObservations.Kind.CompletionValidated, owner, 1, 1, 4096, 1, 512, 1, 1, 200, "")];
        void Read(ulong changedBar = diskBar, ulong changedOwner = owner, ulong pages = 90, IReadOnlyList<NativeDiskReadObservations.Observation>? observations = null, bool full = false) => QemuTranscripts.InspectDiskStaticLiveBoundaryValues(parent, pool, prior, nicBar, arena, pages, changedBar, changedOwner, 100, 200, observations ?? io, fullDirectoryCoverage: full);
        void Reject(Action action)
        {
            bool rejected = false;
            try
            {
                action();
            }
            catch (ToolFailure)
            {
                rejected = true;
            }

            HostCheck.True(rejected);
        }

        Read();
        Read(pages: 91, full: true);
        Reject(() => Read(pages: 90, full: true));
        Reject(() => Read(pages: 92, full: true));
        foreach (ulong alias in new[]
        {
            nicBar,
            user,
            pool
        }

        )
            Reject(() => Read(changedBar: alias));
        foreach (ulong alias in new[]
        {
            user,
            kernel,
            dma,
            pool,
            prior,
            nicBar,
            arena,
            diskBar
        }

        )
            Reject(() => Read(changedOwner: alias));
        Reject(() => Read(pages: 89));
        Reject(() => Read(observations: new[] { io[0] with { Clock = 99 }, io[1] }));
        Reject(() => Read(observations: new[] { io[0], io[1] with { Clock = 201 } }));
    }
}
