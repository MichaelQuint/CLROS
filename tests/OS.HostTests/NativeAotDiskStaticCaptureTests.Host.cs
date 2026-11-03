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
    private sealed class Host
    {
        internal readonly string Root = HostToolProcess.FindRepositoryRoot();
        internal readonly string Evidence, WebRoot, Serial, Destination;
        internal readonly byte[] Index = "<!doctype html><html><title>CLROS host peer</title><body>Physical static middleware</body></html>\n"u8.ToArray();
        internal readonly byte[] Nonce = "CLROS_FIXTURE_A_v1\n"u8.ToArray();
        internal readonly TaskCompletionSource BodyStarted = new(TaskCreationOptions.RunContinuationsAsynchronously), BodyFinished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly ConcurrentQueue<string> Requests = new();
        internal int Launches, Port;
        internal bool Joined, OwnerCancelled;
        private readonly Mode mode;
        internal Host(Mode mode)
        {
            this.mode = mode;
            Evidence = Path.Combine(Root, "artifacts", "disk-static-peer-host-" + Guid.NewGuid().ToString("N"));
            WebRoot = Path.Combine(Evidence, "wwwroot");
            Serial = Path.Combine(Evidence, "synthetic-readiness.log");
            Destination = Serial + ".disk-http";
            Directory.CreateDirectory(WebRoot);
            File.WriteAllBytes(Path.Combine(WebRoot, "index.html"), Index);
            File.WriteAllBytes(Path.Combine(WebRoot, "fixture-nonce.txt"), Nonce);
            foreach (string path in Directory.GetFiles(WebRoot))
                File.SetLastWriteTimeUtc(path, new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        }

        internal async Task<ProcessResult> RunAsync(int port, CancellationToken token)
        {
            Launches++;
            Port = port;
            var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { ContentRootPath = Evidence, WebRootPath = WebRoot });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture));
            await using var app = builder.Build();
            app.Use(async (context, next) =>
            {
                Requests.Enqueue(context.Request.Path.Value!);
                if (mode == Mode.ResetHeaders)
                {
                    context.Abort();
                    return;
                }

                if (mode == Mode.WrongStatus && context.Request.Path == "/healthz")
                {
                    context.Response.StatusCode = 503;
                    await context.Response.WriteAsync("host-unavailable", context.RequestAborted);
                    return;
                }

                if (mode == Mode.CancelBody && context.Request.Path == "/index.html")
                {
                    context.Response.ContentType = "text/html";
                    context.Response.ContentLength = Index.Length;
                    await context.Response.Body.WriteAsync(Index.AsMemory(0, 8), context.RequestAborted);
                    await context.Response.Body.FlushAsync(context.RequestAborted);
                    BodyStarted.TrySetResult();
                    using var bodyStop = CancellationTokenSource.CreateLinkedTokenSource(token, context.RequestAborted);
                    try
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, bodyStop.Token);
                    }
                    finally
                    {
                        BodyFinished.TrySetResult();
                    }

                    return;
                }

                await next(context);
            });
            StaticFileApplication.Configure(app);
            app.MapGet("/healthz", () => TypedResults.Text("OS_MINIMAL_API_OK", "text/plain"));
            try
            {
                await app.StartAsync(token);
                await File.WriteAllTextAsync(Serial, "M10_018_LISTENING synthetic-host-only\r\n", token);
                // Poll only for the peer's closed receipt, never for a response
                // winner. Preserve partial output and keep the host cancellable.
                string receipt = Path.Combine(Destination, "http.json");
                for (;;)
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        using var file = new FileStream(receipt, FileMode.Open, FileAccess.Read, FileShare.None);
                        using var document = JsonDocument.Parse(file);
                        break;
                    }
                    catch (IOException)
                    {
                    }
                    catch (JsonException)
                    {
                    }

                    await Task.Delay(20, token);
                }

                // Let the completed peer dispose its keep-alive client before
                // this synthetic runner terminates, as in the staged peer harness.
                await Task.Delay(100, token);
                return new(0, "synthetic-host-only", "", false, false, false, TimeSpan.Zero);
            }
            finally
            {
                OwnerCancelled = token.IsCancellationRequested;
                using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await app.StopAsync(stop.Token);
                Joined = true;
            }
        }
    }
}
