using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OS.Tooling;

internal static partial class HttpWorkloadBenchmarkTests
{
    // Real loopback HTTP and joined host lifetime only. Synthetic readiness,
    // process results and browser callbacks grant no original guest authority.
    private sealed class BenchmarkHost
    {
        internal readonly string Serial;
        internal readonly ConcurrentQueue<string> Requests = new();
        internal readonly TaskCompletionSource BodyStarted = new(TaskCreationOptions.RunContinuationsAsynchronously),
            BodyFinished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Launches, Port;
        internal bool Joined, OwnerCancelled;
        private readonly string evidence, webroot, mode;
        private readonly WebRootDiskInventory.Inspection inventory;

        internal BenchmarkHost(string root, WebRootDiskInventory.Inspection inventory, string mode)
        {
            this.inventory = inventory;
            this.mode = mode;
            evidence = Fresh(root);
            webroot = Path.Combine(evidence, "wwwroot");
            Directory.CreateDirectory(webroot);
            Serial = Path.Combine(evidence, "synthetic-readiness.log");
            foreach (var entry in inventory.Entries.Where(entry => !entry.IsDirectory))
            {
                string path = Path.Combine(webroot, entry.Path[(WebRootDiskInventory.Root.Length + 1)..]);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, entry.CopyBytes());
                File.SetLastWriteTimeUtc(path, DateTimeOffset.FromUnixTimeSeconds((long)entry.ModifiedSeconds).UtcDateTime);
            }
        }

        internal async Task<ProcessResult> RunAsync(int port, CancellationToken cancellation)
        {
            Launches++;
            Port = port;
            if (mode == "early")
            {
                Joined = true;
                return new(0, "synthetic-host-only", "", false, false, false, TimeSpan.Zero);
            }
            var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
            {
                ContentRootPath = evidence,
                WebRootPath = webroot
            });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:" + port);
            await using var app = builder.Build();
            app.Use(async (context, next) =>
            {
                Requests.Enqueue(context.Request.Method + " " + context.Request.Path);
                if (mode == "bad-status" && context.Request.Path == "/healthz")
                {
                    context.Response.StatusCode = 503;
                    await context.Response.WriteAsync("unavailable", context.RequestAborted);
                    return;
                }
                if (mode == "partial" && context.Request.Path == NativeAotDiskTimingPeer.Target &&
                    context.Request.Headers.Range.Count != 0)
                {
                    var entry = inventory.Find(WebRootDiskInventory.Root + NativeAotDiskTimingPeer.Target)!;
                    var metadata = new DiskStaticHttpContract.Asset(entry, true);
                    context.Response.StatusCode = 206;
                    context.Response.ContentType = metadata.MediaType;
                    context.Response.ContentLength = 65536;
                    context.Response.Headers.ETag = metadata.ETag;
                    context.Response.Headers.ContentRange = $"bytes 0-65535/{metadata.Bytes}";
                    await context.Response.Body.WriteAsync(entry.CopyBytes().AsMemory(0, 8), context.RequestAborted);
                    await context.Response.Body.FlushAsync(context.RequestAborted);
                    BodyStarted.TrySetResult();
                    using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellation, context.RequestAborted);
                    try
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, stop.Token);
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
                await app.StartAsync(cancellation);
                await File.WriteAllTextAsync(Serial, "M10_018_LISTENING synthetic-host-only\r\n", cancellation);
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellation);
                throw new InvalidOperationException("unreachable_host_lifetime");
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                return new(1, "synthetic-host-only", "", false, false, true, TimeSpan.Zero);
            }
            finally
            {
                OwnerCancelled = cancellation.IsCancellationRequested;
                using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await app.StopAsync(stop.Token);
                Joined = true;
            }
        }
    }
}
