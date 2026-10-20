using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace OS.Tooling;

public static partial class NativeAotWebPayload
{
    private sealed record ChurnSample(string kind, int cycle, string @case, int status, string body);

    // The existing qualifier owns the launched app, endpoint, client, deadline,
    // traces and shutdown. This borrows them, joins every request task it starts
    // and selects bounded progress observations; polling is not app workload.
    private static async Task<string> RunChurnAsync(HttpClient client, string endpoint, string expectedBody,
        string observationPath, CancellationToken cancellation)
    {
        using var output = new FileStream(observationPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        List<ChurnSample> samples = [];
        async Task<string> Read(HttpResponseMessage response)
        {
            using var stream = await response.Content.ReadAsStreamAsync(cancellation);
            using MemoryStream bytes = new(); byte[] chunk = new byte[1024];
            for (;;)
            {
                int length = await stream.ReadAsync(chunk, cancellation); if (length == 0) break;
                Require(bytes.Length + length <= 2048, "churn_response_size"); bytes.Write(chunk, 0, length);
            }
            return new UTF8Encoding(false, true).GetString(bytes.ToArray());
        }
        HttpRequestMessage Post(string target = "/__clros/churn")
        {
            var request = new HttpRequestMessage(HttpMethod.Post, endpoint + target) { Content = new ByteArrayContent([]) };
            request.Headers.Add("X-CLROS-Churn", "v1"); return request;
        }
        async Task Observe(string kind, int cycle, string target)
        {
            using var response = await client.GetAsync(endpoint + target, HttpCompletionOption.ResponseHeadersRead, cancellation);
            samples.Add(new(kind, cycle, "", (int)response.StatusCode, await Read(response)));
        }
        async Task Status(string kind, int cycle, bool pending)
        {
            for (int attempt = 0; attempt < 200; attempt++)
            {
                using var response = await client.GetAsync(endpoint + "/__clros/churn/status", HttpCompletionOption.ResponseHeadersRead, cancellation);
                string body = await Read(response); Require(response.StatusCode == HttpStatusCode.OK, "churn_status_http");
                string[] fields = body.Split(' ');
                bool Has(string key, string value) => fields.Contains(key + "=" + value, StringComparer.Ordinal);
                if (Has("active", pending ? "1" : "0") && Has("waiting", pending ? "1" : "0"))
                { samples.Add(new(kind, cycle, "", 200, body)); return; }
                await Task.Delay(5, cancellation);
            }
            throw new InvalidDataException("churn_status_progress");
        }
        try
        {
        await Status("baseline", -1, false);
        string[] bad = ["missing_header", "wrong_header", "duplicate_header", "body", "query"];
        foreach (string reason in bad)
        {
            using var request = Post(reason == "query" ? "/__clros/churn?extra=1" : "/__clros/churn");
            if (reason is "missing_header" or "wrong_header") request.Headers.Remove("X-CLROS-Churn");
            if (reason == "wrong_header") request.Headers.Add("X-CLROS-Churn", "v2");
            if (reason == "duplicate_header") request.Headers.TryAddWithoutValidation("X-CLROS-Churn", "v1");
            if (reason == "body") { request.Content!.Dispose(); request.Content = new StringContent("x"); }
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
            string body = await Read(response); Require(response.StatusCode == HttpStatusCode.BadRequest, "churn_bad_request");
            samples.Add(new("bad", -1, reason, (int)response.StatusCode, body));
        }
        foreach (var (reason, target) in new[] { ("empty_query", "/__clros/churn?"), ("empty_separator_query", "/__clros/churn?&&") })
        {
            // Preserve the actual request target; URI normalization can erase '?'.
            using TcpClient socket = new(); await socket.ConnectAsync(IPAddress.Loopback, new Uri(endpoint).Port, cancellation);
            using NetworkStream stream = socket.GetStream();
            byte[] request = Encoding.ASCII.GetBytes($"POST {target} HTTP/1.1\r\nHost:127.0.0.1:{new Uri(endpoint).Port}\r\nX-CLROS-Churn:v1\r\nContent-Length:0\r\nConnection:close\r\n\r\n");
            await stream.WriteAsync(request, cancellation); using MemoryStream bytes = new(); byte[] chunk = new byte[1024];
            for (;;)
            {
                int length = await stream.ReadAsync(chunk, cancellation); if (length == 0) break;
                Require(bytes.Length + length <= 8192, "churn_raw_response_size"); bytes.Write(chunk, 0, length);
            }
            string response = Encoding.ASCII.GetString(bytes.ToArray()); int split = response.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            Require(response.StartsWith("HTTP/1.1 400 ", StringComparison.Ordinal) && split > 0, "churn_raw_bad_request");
            samples.Add(new("bad", -1, reason, 400, response[(split + 4)..]));
        }
        for (int cycle = 0; cycle < 3; cycle++)
        {
            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            using var request = Post(); Task<HttpResponseMessage> pending = client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel.Token);
            try
            {
                await Status("cancel-pending", cycle, true); await Observe("cancel-health", cycle, "/healthz"); await Status("cancel-ready", cycle, true);
                cancel.Cancel(); bool observed = false;
                try { using var unexpected = await pending; } catch (OperationCanceledException) when (cancel.IsCancellationRequested) { observed = true; }
                Require(observed, "churn_cancellation"); samples.Add(new("cancel", cycle, "", 0, ""));
            }
            finally
            {
                cancel.Cancel();
                try { using var joined = await pending; } catch (OperationCanceledException) when (cancel.IsCancellationRequested) { }
            }
            await Status("cancel-drained", cycle, false);
            using var complete = Post();
            using var finish = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            Task<HttpResponseMessage> running = client.SendAsync(complete, HttpCompletionOption.ResponseHeadersRead, finish.Token);
            try
            {
                await Status("complete-pending", cycle, true); await Observe("complete-health", cycle, "/healthz"); await Status("complete-ready", cycle, true);
                using var response = await running; samples.Add(new("complete", cycle, "", (int)response.StatusCode, await Read(response)));
            }
            finally
            {
                finish.Cancel();
                try { using var joined = await running; } catch (OperationCanceledException) when (finish.IsCancellationRequested) { }
            }
            await Observe("complete-health-after", cycle, "/healthz"); await Status("complete-drained", cycle, false);
        }
        await Status("final", -1, false);
        string json = JsonSerializer.Serialize(new { schema = "clros.web.churn.v1", endpoint, samples });
        ValidateChurnEvidence(json, endpoint, expectedBody); return json;
        }
        finally
        {
            // A partial or rejected report remains rejected. Preserve it even
            // when an HTTP/deadline/grammar failure prevents a sealed receipt.
            byte[] json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { schema = "clros.web.churn.v1", endpoint, samples }));
            await output.WriteAsync(json); await output.FlushAsync();
        }
    }

    private static void ValidateRecordedChurn(string destination, string suffix)
    {
        string path = Path.Combine(destination, "web-churn" + suffix + ".json");
        Require(new FileInfo(path).Length is > 0 and <= 65536, "churn_recorded_size");
        using JsonDocument http = JsonDocument.Parse(File.ReadAllText(Path.Combine(destination, "web-http" + suffix + ".json")));
        ValidateChurnEvidence(File.ReadAllText(path), http.RootElement.GetProperty("endpoint").GetString()!, http.RootElement.GetProperty("body").GetString()!);
    }
}
