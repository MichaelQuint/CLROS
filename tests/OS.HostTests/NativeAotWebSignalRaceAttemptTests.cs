using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using OS.Tooling;

// Deterministic HTTP-handler controls exercise owned logical requests and
// retained bytes only. They establish no real socket, signal or guest result.
internal static partial class NativeAotWebSignalRaceAttemptTests
{
    private const string Endpoint = "http://127.0.0.1:23458", Health = "OS_MINIMAL_API_OK";
    private static string Output()
    {
        string directory = Path.GetFullPath(Path.Combine("artifacts", "host-tests", "race-attempt-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "observations.json");
    }

    private static async Task<(string Failure, string Json, JsonDocument Log)> CaptureFailure(Peer peer, string path, CancellationToken token)
    {
        using var client = new HttpClient(peer);
        string failure = "";
        try
        {
            await NativeAotWebPayload.CaptureSignalRaceAsync(client, Endpoint, Health, path, token);
        }
        catch (Exception error) when (error is InvalidDataException or IOException or HttpRequestException or OperationCanceledException or DecoderFallbackException)
        {
            failure = error is InvalidDataException ? error.Message : error.GetType().Name;
        }

        HostCheck.True(failure.Length > 0);
        string selected = File.ReadAllText(path), json = File.ReadAllText(path + ".attempts.json");
        var summary = NativeAotWebPayload.InspectSignalRaceAttempts(json, selected, Endpoint, Health);
        HostCheck.True(!summary.SemanticComplete);
        HostCheck.True(!json.Contains("secret-rich", StringComparison.Ordinal));
        return (failure, json, JsonDocument.Parse(json));
    }

    [HostTest("web race attempt log retains partial fault cancellation invalid UTF8 and overflow bytes")]
    private static async Task PartialBodies()
    {
        foreach (string mode in new[]
        {
            "partial",
            "utf8",
            "cancel",
            "overflow"
        }

        )
        {
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var peer = new Peer(mode, stop);
            var result = await CaptureFailure(peer, Output(), stop.Token);
            using var log = result.Log;
            var row = log.RootElement.GetProperty("attempts")[0];
            HostCheck.Equal(1, peer.Calls);
            HostCheck.True(peer.Stream!.Closed);
            HostCheck.Equal(0, log.RootElement.GetProperty("selections").GetArrayLength());
            HostCheck.Equal(mode == "cancel" ? "cancelled" : "faulted", row.GetProperty("outcome").GetString());
            HostCheck.True(Convert.FromBase64String(row.GetProperty("bodyBase64").GetString()!).SequenceEqual(mode == "overflow" ? new byte[4096] : new byte[] { 65, 255 }));
            HostCheck.Equal(mode == "utf8", row.GetProperty("bodyComplete").GetBoolean());
            HostCheck.Equal(mode switch
            {
                "partial" => "IOException",
                "utf8" => "DecoderFallbackException",
                "cancel" => "cancelled",
                _ => "web_signalrace_response_size"
            }, row.GetProperty("error").GetString());
        }
    }

    [HostTest("web race attempt log retains rejected status content type and pre-header failures")]
    private static async Task RejectedResponses()
    {
        foreach (string mode in new[]
        {
            "status",
            "media",
            "before-headers"
        }

        )
        {
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var peer = new Peer(mode, stop);
            var result = await CaptureFailure(peer, Output(), stop.Token);
            using var log = result.Log;
            var row = log.RootElement.GetProperty("attempts")[0];
            HostCheck.Equal(1, peer.Calls);
            HostCheck.Equal(0, log.RootElement.GetProperty("selections").GetArrayLength());
            HostCheck.Equal(mode != "before-headers", row.GetProperty("headers").GetBoolean());
            HostCheck.Equal(mode switch
            {
                "status" => "web_signalrace_churn_status",
                "media" => "web_signalrace_content_type",
                _ => "HttpRequestException"
            }, result.Failure);
            HostCheck.Equal(mode == "status" ? "response" : "faulted", row.GetProperty("outcome").GetString());
            HostCheck.Equal(mode == "status" ? "rejected status body" : "", Encoding.UTF8.GetString(Convert.FromBase64String(row.GetProperty("bodyBase64").GetString()!)));
            if (mode == "media")
                HostCheck.True(peer.Stream!.Closed);
        }
    }

    [HostTest("web race response headers end pending overlap even while body read remains blocked")]
    private static async Task HeadersEndOverlap()
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var peer = new Peer("headers-pending", stop);
        var result = await CaptureFailure(peer, Output(), stop.Token);
        using var log = result.Log;
        HostCheck.Equal("web_signalrace_lost_overlap", result.Failure);
        HostCheck.Equal(4, peer.Calls);
        HostCheck.True(peer.Stream!.Closed);
        var row = log.RootElement.GetProperty("attempts")[3];
        HostCheck.True(row.GetProperty("headers").GetBoolean());
        HostCheck.Equal("cancelled", row.GetProperty("outcome").GetString());
        HostCheck.Equal("QQ==", row.GetProperty("bodyBase64").GetString());
        HostCheck.Equal(3, log.RootElement.GetProperty("selections").GetArrayLength());
    }

    [HostTest("web race primary failure survives joining another faulted owned request")]
    private static async Task PrimaryFailure()
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var peer = new Peer("join-fault", stop);
        var result = await CaptureFailure(peer, Output(), stop.Token);
        using var log = result.Log;
        HostCheck.Equal("web_signalrace_lost_overlap", result.Failure);
        HostCheck.True(peer.ChurnJoined);
        HostCheck.Equal(result.Failure, log.RootElement.GetProperty("failure").GetString());
        var rows = log.RootElement.GetProperty("attempts");
        HostCheck.Equal(6, rows.GetArrayLength());
        HostCheck.Equal("HttpRequestException", rows[5].GetProperty("error").GetString());
        HostCheck.Equal("cancelled", rows[3].GetProperty("outcome").GetString());
    }

    [HostTest("web race companion output collision refuses before any HTTP attempt")]
    private static async Task FreshOutput()
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var peer = new Peer("before-headers", stop);
        using var client = new HttpClient(peer);
        string path = Output();
        File.WriteAllText(path + ".attempts.json", "retained original");
        bool refused = false;
        try
        {
            await NativeAotWebPayload.CaptureSignalRaceAsync(client, Endpoint, Health, path, stop.Token);
        }
        catch (IOException)
        {
            refused = true;
        }

        HostCheck.True(refused);
        HostCheck.Equal(0, peer.Calls);
        HostCheck.Equal("retained original", File.ReadAllText(path + ".attempts.json"));
    }
}
