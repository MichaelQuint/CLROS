using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OS.Tooling;

// Real host Kestrel/middleware checks for the external peer. The supplied
// synthetic readiness/runner observations carry no native or CLROS authority.
internal static class NativeAotStagedStaticPeerTests
{
    [HostTest("staged HTTP peer checks real host middleware ranges conditionals JSON concurrent reads abort and recovery")]
    private static async Task Positive() => await Run(false);

    [HostTest("staged HTTP peer preserves changed-body failure and joins its cancelled host owner")]
    private static async Task ChangedBody() => await Run(true);

    private static async Task Run(bool changeBody)
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string evidence = Path.Combine(root, "artifacts", "staged-peer-host-" + Guid.NewGuid().ToString("N"));
        string webroot = Path.Combine(evidence, "wwwroot"), destination = Path.Combine(evidence, "peer");
        string serial = Path.Combine(evidence, "synthetic-readiness.log");
        Directory.CreateDirectory(webroot);
        foreach (var asset in NativeAotStaticFilePackage.StagedAssets())
        {
            string path = Path.Combine(evidence, asset.Path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            byte[] bytes = (byte[])asset.Bytes.Clone();
            if (changeBody && asset.Path == "wwwroot/site.css")
                bytes[0] ^= 1;
            await File.WriteAllBytesAsync(path, bytes);
            File.SetLastWriteTimeUtc(path, new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        }
        bool joined = false;
        async Task<ProcessResult> Host(int port, CancellationToken token)
        {
            var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { ContentRootPath = evidence, WebRootPath = webroot });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:" + port);
            await using var app = builder.Build();
            StaticFileApplication.Configure(app);
            app.MapGet("/healthz", () => TypedResults.Text("OS_MINIMAL_API_OK", "text/plain"));
            app.MapGet("/config", () => TypedResults.Json(new StaticFileConfigurationResponse("minimal.api.static-files", "v1", "/app/wwwroot"),
                StaticFileJsonContext.Default.StaticFileConfigurationResponse));
            try
            {
                await app.StartAsync(token);
                await File.WriteAllTextAsync(serial, "M10_017_LISTENING synthetic-host-only\r\n", token);
                while (!File.Exists(Path.Combine(destination, "http.json")))
                    await Task.Delay(20, token);
                await Task.Delay(100, token);
                return new(0, "synthetic-host-only", "", false, false, false, TimeSpan.Zero);
            }
            finally
            {
                using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await app.StopAsync(stop.Token);
                joined = true;
            }
        }
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        bool rejected = false;
        try
        {
            var completion = await NativeAotStagedStaticPeer.RunAsync(root, serial, destination, Host, deadline.Token);
            HostCheck.True(!changeBody && completion.Runner.Succeeded);
            HostCheck.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                await File.ReadAllBytesAsync(Path.Combine(destination, "http.json")))), completion.ReceiptSha256);
            using var receipt = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(destination, "http.json")));
            HostCheck.True(receipt.RootElement.GetProperty("Complete").GetBoolean());
            HostCheck.Equal(26, receipt.RootElement.GetProperty("Responses").GetArrayLength());
            HostCheck.True(File.Exists(Path.Combine(destination, "abort.json")) && File.Exists(Path.Combine(destination, "stopped.json")));
        }
        catch (ToolFailure error) when (changeBody && error.Reason.StartsWith("static_peer_response", StringComparison.Ordinal))
        {
            rejected = true;
            using var failed = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(destination, "http.failed.json")));
            HostCheck.True(!failed.RootElement.GetProperty("Complete").GetBoolean());
            HostCheck.True(failed.RootElement.GetProperty("Responses").EnumerateArray().Any(row => row.GetProperty("Path").GetString() == "/site.css"));
        }
        HostCheck.True(joined && rejected == changeBody);
    }
}
