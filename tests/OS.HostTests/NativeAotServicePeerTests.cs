using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using OS.Tooling;

internal static class NativeAotServicePeerTests
{
    [HostTest("service peer report binds ordered HTTP refusals progress duration and stop response without guest authority")]
    private static void Complete()
    {
        string text = Report().ToJsonString();
        var result = NativeAotServicePeer.InspectOutput(text);
        HostCheck.Equal(8080, result.Port);
        HostCheck.Equal(10, result.Exchanges);
        HostCheck.Equal(2, result.StatusSamples);
        HostCheck.Equal(300100L, result.DurationMilliseconds);
        HostCheck.Equal(4097L, result.Calls);
        HostCheck.Equal(8192L, result.RandomBytes);
        HostCheck.True(result.Lifecycle is null);
        HostCheck.Equal(text, Report().ToJsonString());
        var retries = Report();
        retries["connectAttempts"] = 400;
        Shift(retries, 399000);
        HostCheck.Equal(699100L, NativeAotServicePeer.InspectOutput(retries.ToJsonString()).DurationMilliseconds);
    }

    [HostTest("service peer v2 lifecycle report requires explicit mode and returns typed observations")]
    private static void LifecycleReportMode()
    {
        var report = LifecycleReport();
        string text = report.ToJsonString();
        var result = NativeAotServicePeer.InspectOutput(text, lifecycle: true);
        var expected = new NativeAotServiceLifecyclePeer.Observation(8080, 12, 32768, 1653448133, 524288,
            "0A131BA05D7CBD5F28DD2FAB7DA2B8BECE4122EB1A76FDA0D0159C66B520F8A1", 1, 1, 100, 200, 300,
            "CLROS_LIFECYCLE_STATUS active=0 waiting=0 waits=2 canceled=2 echoes=1 streams=1 rejected=1 read=65536 written=524288");
        HostCheck.True(result.Lifecycle == expected);
        HostCheck.Equal(8080, result.Port);
        HostCheck.Equal(10, result.Exchanges);
        Reject(text);
        Reject(Report().ToJsonString(), lifecycle: true);
        report["schema"] = "clros.nativeaot.service.peer.v1";
        Reject(report.ToJsonString());
        Reject(report.ToJsonString(), lifecycle: true);
        report = Report();
        report["schema"] = "clros.nativeaot.service.peer.v2";
        Reject(report.ToJsonString());
        Reject(report.ToJsonString(), lifecycle: true);
    }

    [HostTest("service peer v2 rejects missing extra duplicate and mistyped lifecycle fields")]
    private static void LifecycleReportShape()
    {
        foreach (string name in LifecycleReport()["lifecycle"]!.AsObject().Select(field => field.Key))
        {
            var report = LifecycleReport();
            report["lifecycle"]!.AsObject().Remove(name);
            Reject(report.ToJsonString(), lifecycle: true);
        }
        foreach (Action<JsonObject> alter in new Action<JsonObject>[] {
            r => r.Remove("lifecycle"), r => r["extra"] = 1, r => r["lifecycle"]!.AsObject()["extra"] = 1,
            r => r["lifecycle"] = null, r => r["lifecycle"]!.AsObject()["echoBytes"] = "32768",
            r => r["lifecycle"]!.AsObject()["terminalStatus"] = null })
        {
            var report = LifecycleReport();
            alter(report);
            Reject(report.ToJsonString(), lifecycle: true);
        }
        string text = LifecycleReport().ToJsonString();
        Reject(text.Replace("\"echoBytes\":32768", "\"echoBytes\":32768,\"echoBytes\":32768", StringComparison.Ordinal), lifecycle: true);
        Reject(text.Replace("\"echoBytes\":32768", "\"echoBytes\":32768,\"\\u0065choBytes\":32768", StringComparison.Ordinal), lifecycle: true);
    }

    [HostTest("service peer v2 rejects narrowing overflow mismatched port and invalid lifecycle observations")]
    private static void LifecycleReportValues()
    {
        foreach (string name in new[] { "port", "sequentialConnections", "echoBytes", "streamBytes", "resetRequests", "timeoutRequests" })
            foreach (long delta in new[] { -4294967296L, 4294967296L })
            {
                var report = LifecycleReport();
                var value = report["lifecycle"]!.AsObject();
                value[name] = value[name]!.GetValue<int>() + delta;
                Reject(report.ToJsonString(), lifecycle: true);
            }
        foreach (Action<JsonObject> alter in new Action<JsonObject>[] {
            v => v["echoHash"] = 1653448133L + 4294967296L, v => v["echoHash"] = 1653448133L - 4294967296L,
            v => v["holdStartedTicks"] = ulong.MaxValue, v => v["port"] = 8081, v => v["sequentialConnections"] = 11,
            v => v["holdStartedTicks"] = 0, v => v["healthyCompletedTicks"] = 99, v => v["holdReleasedTicks"] = 199,
            v => v["holdReleasedTicks"] = TimeSpan.FromSeconds(240).Ticks, v => v["streamSha256"] = "wrong",
            v => v["terminalStatus"] = "CLROS_LIFECYCLE_STATUS active=1 waiting=0 waits=2 canceled=2 echoes=1 streams=1 rejected=1 read=65536 written=524288" })
        {
            var report = LifecycleReport();
            alter(report["lifecycle"]!.AsObject());
            Reject(report.ToJsonString(), lifecycle: true);
        }
    }

    [HostTest("service peer rejects malformed unknown duplicate typed ranged and oversized reports")]
    private static void Schema()
    {
        foreach (string text in new[] { "", "null", "[]", "{}", Report().ToJsonString() + "{}", new string(' ', 65537),
            Report().ToJsonString().Replace("\"port\":8080", "\"port\":8080,\"port\":8080", StringComparison.Ordinal),
            Report().ToJsonString().Replace("\"port\":8080", "\"port\":8080,\"\\u0070ort\":8080", StringComparison.Ordinal) })
            Reject(text);
        foreach (Action<JsonObject> alter in new Action<JsonObject>[] {
            r => r["extra"] = 1, r => r.Remove("port"), r => r["schema"] = "other", r => r["port"] = 0, r => r["port"] = 65536,
            r => r["connectAttempts"] = 0, r => r["connectAttempts"] = 401, r => r["connectAttempts"] = 2, r => r["durationMilliseconds"] = 300000,
            r => r["durationMilliseconds"] = 800000, r => Row(r, 0)["status"] = "200", r => Row(r, 0)["extra"] = true,
            r => Row(r, 0).Remove("method"), r => Row(r, 0)["bodyBase64"] = "?", r => Row(r, 0)["bodyBase64"] = Convert.ToBase64String(new byte[8193]),
            r => Row(r, 0)["dateUtc"] = "yesterday", r => Row(r, 0)["dateUtc"] = "2026-10-18T00:00:00.0000000+01:00",
            r => r["exchanges"]!.AsArray().RemoveAt(9),
            r => r["exchanges"] = new JsonArray(Enumerable.Range(0, 74).Select(_ => (JsonNode?)Row(r, 0).DeepClone()).ToArray()) })
        {
            var report = Report();
            alter(report);
            Reject(report.ToJsonString());
        }
    }

    [HostTest("service peer rejects substituted requests premature stop counter identity time phase and Date regressions")]
    private static void Progress()
    {
        foreach (Action<JsonObject> alter in new Action<JsonObject>[] {
            r => Row(r, 0)["path"] = "/other", r => Row(r, 2)["status"] = 200, r => Row(r, 4)["status"] = 200,
            r => Row(r, 9)["bodyBase64"] = Encode("other"), r => Row(r, 3)["method"] = "GET", r => Row(r, 1)["index"] = 0,
            r => Row(r, 7)["startedMilliseconds"] = 0, r => Row(r, 7)["completedMilliseconds"] = 300001,
            r => Row(r, 9)["completedMilliseconds"] = 300101, r => Row(r, 7)["dateUtc"] = "2026-10-17T00:00:00.0000000+00:00",
            r => ChangeStatus(r, 1, "calls", 1), r => ChangeStatus(r, 7, "phase", 4), r => ChangeStatus(r, 7, "failure", 1),
            r => ChangeStatus(r, 7, "managedWorkerId", 8), r => ChangeStatus(r, 7, "managedWorkerId", 0),
            r => ChangeStatus(r, 7, "calls", 4096), r => ChangeStatus(r, 7, "randomBytes", 4096),
            r => ChangeStatus(r, 7, "elapsedMilliseconds", 300000), r => ChangeStatus(r, 7, "iterations", 0),
            r => ChangeStatus(r, 6, "calls", 4098), r => ChangeStatus(r, 6, "randomRequests", 2),
            r => ChangeStatus(r, 6, "elapsedMilliseconds", 300002) })
        {
            var report = Report();
            alter(report);
            Reject(report.ToJsonString());
        }
        var dates = Report();
        for (int i = 0; i < 10; i++)
            Row(dates, i)["dateUtc"] = "2026-10-18T00:00:00.0000000+00:00";
        Reject(dates.ToJsonString());
    }

    // Real separate peer child against a deliberately wrong local endpoint.
    // The synthetic serial/runner supplies no guest or execution authority.
    [HostTest("service peer rejects wrong HTTP endpoint and joins cancelled runner before completion")]
    private static void OwnedFailure() => OwnedFailureAsync().GetAwaiter().GetResult();
    private static async Task OwnedFailureAsync()
    {
        string root = HostToolProcess.FindRepositoryRoot(), directory = Path.Combine(root, "artifacts", "host-tests", "service-peer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string serial = Path.Combine(directory, "serial.log"), log = Path.Combine(directory, "peer.log");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        bool stopped = false, served = false;
        int starts = 0;
        string? failure = null;
        try
        {
            _ = await NativeAotServicePeer.RunAsync(root, log, serial, async (port, token) =>
            {
                starts++;
                using var listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                File.WriteAllText(serial, "\nCLROS_M16_014_LISTENING\r\n");
                try
                {
                    using var client = await listener.AcceptTcpClientAsync(token);
                    await using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                    while (await reader.ReadLineAsync(token) is { Length: > 0 })
                    {
                    }
                    await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nDate: Sun, 18 Oct 2026 00:00:00 GMT\r\nContent-Length: 5\r\nConnection: close\r\n\r\nwrong"), token);
                    await stream.FlushAsync(token);
                    served = true;
                    client.Close();
                    await Task.Delay(Timeout.Infinite, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { }
                finally { stopped = true; }
                return new ProcessResult(1, "synthetic-runner", "", false, false, false, TimeSpan.Zero);
            }, deadline.Token);
        }
        catch (ToolFailure error) { failure = error.Reason; }
        HostCheck.True(served && stopped);
        HostCheck.Equal("service_peer_or_runner_failed", failure ?? "accepted");
        HostCheck.True(File.Exists(log) && File.ReadAllText(log).Length == 0 &&
            File.ReadAllText(log + ".stderr.log").Contains("service_peer_http_exchange", StringComparison.Ordinal));
        bool fresh = false;
        try
        {
            _ = await NativeAotServicePeer.RunAsync(root, log, serial, (_, _) => { starts++; throw new InvalidOperationException(); }, deadline.Token);
        }
        catch (IOException) { fresh = true; }
        HostCheck.True(fresh);
        HostCheck.Equal(1, starts);
    }

    [HostTest("service raw HTTP peer retains client through ContentLength or final chunk until serverFIN then sends clientFIN")]
    private static void TransportEof() => TransportEofAsync().GetAwaiter().GetResult();
    private static async Task TransportEofAsync()
    {
        foreach (int kind in new[] { 0, 1, 2 })
        {
            bool post = kind == 1;
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var copied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            string method = post ? "POST" : "GET", path = post ? "/__clros/service/start" : kind == 2 ? "/__clros/service/status" : "/healthz";
            Task<NativeAotServicePeer.HttpResponseObservation> pending = NativeAotServicePeer.ExchangeForTestsAsync(port, method, path,
                deadline.Token, () => copied.TrySetResult());
            try
            {
                using var client = await listener.AcceptTcpClientAsync(deadline.Token);
                await using var stream = client.GetStream();
                HostCheck.Equal(method + " " + path + " HTTP/1.1\r\nHost: clros.service\r\nConnection: close\r\n" +
                    (post ? "Content-Length: 0\r\n" : "") + "\r\n", await ReadRequestAsync(stream, deadline.Token));
                byte[] body = post ? [] : Enumerable.Range(0, 8192).Select(i => (byte)(i % 251)).ToArray();
                byte[] response = kind == 2 ? ChunkedResponse(body) : Response(body, post ? "409 Conflict" : "200 OK");
                await stream.WriteAsync(response.AsMemory(0, 17), deadline.Token);
                await stream.WriteAsync(response.AsMemory(17), deadline.Token);
                await copied.Task.WaitAsync(deadline.Token);
                Task<int> clientFin = stream.ReadAsync(new byte[1], deadline.Token).AsTask();
                HostCheck.True(!pending.IsCompleted && !clientFin.IsCompleted);
                client.Client.Shutdown(SocketShutdown.Send);
                var result = await pending;
                HostCheck.True(result.Body.AsSpan().SequenceEqual(body));
                HostCheck.Equal(post ? 409 : 200, result.Status);
                HostCheck.Equal(TimeSpan.Zero, result.DateUtc.Offset);
                HostCheck.Equal(0, await clientFin);
            }
            finally { deadline.Cancel(); try { _ = await pending; } catch (Exception) when (pending.IsCompleted) { } }
        }
    }

    [HostTest("service raw HTTP peer rejects malformed ambiguous truncated trailing and oversized responses")]
    private static void TransportFailures() => TransportFailuresAsync().GetAwaiter().GetResult();
    private static async Task TransportFailuresAsync()
    {
        string valid = Encoding.ASCII.GetString(Response("ok"u8.ToArray()));
        string[] malformed = [valid.Replace("HTTP/1.1", "HTTP/1.0", StringComparison.Ordinal),
            valid.Replace("Date: Sun, 18 Oct 2026 00:00:00 GMT\r\n", "", StringComparison.Ordinal),
            valid.Replace("Sun, 18 Oct 2026 00:00:00 GMT", "yesterday", StringComparison.Ordinal),
            valid.Replace("Date:", "Date: Sun, 18 Oct 2026 00:00:00 GMT\r\nDate:", StringComparison.Ordinal),
            valid.Replace("Content-Length: 2", "Content-Length: 2\r\ncontent-length: 2", StringComparison.Ordinal),
            valid.Replace("Content-Length: 2", "Transfer-Encoding: chunked\r\nContent-Length: 2", StringComparison.Ordinal),
            valid.Replace("Connection: close", "Connection: keep-alive", StringComparison.Ordinal),
            valid.Replace("Connection: close", "Connection: close\r\nX: a\nb", StringComparison.Ordinal),
            valid.Replace("Content-Length: 2", "Content-Length: 02", StringComparison.Ordinal),
            valid.Replace("Content-Length: 2", "Content-Length: -1", StringComparison.Ordinal),
            valid.Replace("Content-Length: 2", "Content-Length: 8193", StringComparison.Ordinal),
            valid.Replace("Content-Length: 2", "Content-Length: 9223372036854775808", StringComparison.Ordinal),
            valid[..^1], valid + "x", "HTTP/1.1 200 OK\r\nX: " + new string('x', 8192), valid[..^6]];
        foreach (string response in malformed)
        {
            bool refused = false;
            try
            {
                _ = await ExchangeResponseAsync(Encoding.ASCII.GetBytes(response));
            }
            catch (ToolFailure) { refused = true; }
            HostCheck.True(refused);
        }
        string chunked = Encoding.ASCII.GetString(ChunkedResponse("ok"u8.ToArray()));
        foreach (string response in new[] { chunked.Replace("\r\n2\r\nok", "\r\nxyz\r\nok", StringComparison.Ordinal),
            chunked.Replace("\r\n2\r\nok", "\r\n2;x=1\r\nok", StringComparison.Ordinal),
            chunked.Replace("\r\n2\r\nok", "\r\n2001\r\nok", StringComparison.Ordinal), chunked[..^1], chunked + "x",
            chunked.Replace("0\r\n\r\n", "0\r\nX: a\r\n\r\n", StringComparison.Ordinal),
            chunked.Replace("ok\r\n", "ok\nx", StringComparison.Ordinal) })
        {
            bool refused = false;
            try
            {
                _ = await ExchangeResponseAsync(Encoding.ASCII.GetBytes(response));
            }
            catch (ToolFailure) { refused = true; }
            HostCheck.True(refused);
        }
    }

    [HostTest("service raw HTTP peer cancellation joins a read waiting for genuine EOF")]
    private static void TransportCancellation() => TransportCancellationAsync().GetAwaiter().GetResult();
    private static async Task TransportCancellationAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var cancel = new CancellationTokenSource();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var copied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = NativeAotServicePeer.ExchangeForTestsAsync(((IPEndPoint)listener.LocalEndpoint).Port, "GET", "/healthz", cancel.Token, () => copied.TrySetResult());
        try
        {
            using var client = await listener.AcceptTcpClientAsync(deadline.Token);
            await using var stream = client.GetStream();
            _ = await ReadRequestAsync(stream, deadline.Token);
            await stream.WriteAsync(ChunkedResponse([]), deadline.Token);
            await copied.Task.WaitAsync(deadline.Token);
            HostCheck.True(!pending.IsCompleted);
            cancel.Cancel();
            bool refused = false;
            try
            {
                _ = await pending;
            }
            catch (OperationCanceledException) { refused = true; }
            HostCheck.True(refused && pending.IsCompleted);
        }
        finally { cancel.Cancel(); try { _ = await pending; } catch (Exception) when (pending.IsCompleted) { } }
    }

    private static byte[] Response(byte[] body, string status = "200 OK") => [..Encoding.ASCII.GetBytes("HTTP/1.1 " + status +
        "\r\nDate: Sun, 18 Oct 2026 00:00:00 GMT\r\nContent-Length: " + body.Length.ToString(CultureInfo.InvariantCulture) + "\r\nConnection: close\r\n\r\n"), ..body];
    private static byte[] ChunkedResponse(byte[] body)
    {
        byte[] header = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nDate: Sun, 18 Oct 2026 00:00:00 GMT\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\n");
        return body.Length == 0 ? [.. header, .. "0\r\n\r\n"u8.ToArray()] :
            [.. header, .. Encoding.ASCII.GetBytes(body.Length.ToString("x", CultureInfo.InvariantCulture) + "\r\n"), .. body, .. "\r\n0\r\n\r\n"u8.ToArray()];
    }
    private static async Task<string> ReadRequestAsync(NetworkStream stream, CancellationToken token)
    {
        byte[] bytes = new byte[512];
        int length = 0;
        while (bytes.AsSpan(0, length).IndexOf("\r\n\r\n"u8) < 0)
        {
            HostCheck.True(length < bytes.Length);
            int read = await stream.ReadAsync(bytes.AsMemory(length), token);
            HostCheck.True(read > 0);
            length += read;
        }
        return Encoding.ASCII.GetString(bytes, 0, length);
    }
    private static async Task<NativeAotServicePeer.HttpResponseObservation> ExchangeResponseAsync(byte[] response)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var pending = NativeAotServicePeer.ExchangeForTestsAsync(((IPEndPoint)listener.LocalEndpoint).Port, "GET", "/healthz", deadline.Token);
        try
        {
            using var client = await listener.AcceptTcpClientAsync(deadline.Token);
            await using var stream = client.GetStream();
            _ = await ReadRequestAsync(stream, deadline.Token);
            try
            {
                await stream.WriteAsync(response, deadline.Token);
            }
            catch (Exception error) when (error is IOException or SocketException) { }
            try
            {
                client.Client.Shutdown(SocketShutdown.Send);
            }
            catch (SocketException error) when (error.SocketErrorCode is SocketError.NotConnected or SocketError.ConnectionReset or SocketError.Shutdown) { }
            return await pending;
        }
        finally { deadline.Cancel(); try { _ = await pending; } catch (Exception) when (pending.IsCompleted) { } }
    }

    private static void Shift(JsonObject report, long milliseconds)
    {
        report["durationMilliseconds"] = report["durationMilliseconds"]!.GetValue<long>() + milliseconds;
        foreach (var node in report["exchanges"]!.AsArray())
        {
            var row = node!.AsObject();
            row["startedMilliseconds"] = row["startedMilliseconds"]!.GetValue<long>() + milliseconds;
            row["completedMilliseconds"] = row["completedMilliseconds"]!.GetValue<long>() + milliseconds;
            row["dateUtc"] = DateTimeOffset.Parse(row["dateUtc"]!.GetValue<string>(), CultureInfo.InvariantCulture).AddMilliseconds(milliseconds).ToString("O", CultureInfo.InvariantCulture);
        }
    }

    private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    private static JsonObject Row(JsonObject report, int index) => report["exchanges"]![index]!.AsObject();
    private static void ChangeStatus(JsonObject report, int index, string field, long value)
    {
        var row = Row(report, index);
        var status = JsonNode.Parse(Convert.FromBase64String(row["bodyBase64"]!.GetValue<string>()))!.AsObject();
        status[field] = value;
        row["bodyBase64"] = Encode(status.ToJsonString());
    }
    private static JsonObject Report()
    {
        string Status(int phase, int worker, long calls, long bytes, long elapsed) => new JsonObject
        {
            ["contract"] = "clros.service.v1",
            ["phase"] = phase,
            ["failure"] = 0,
            ["managedWorkerId"] = worker,
            ["calls"] = calls,
            ["randomRequests"] = phase == 0 ? 0 : 1,
            ["randomBytes"] = bytes,
            ["iterations"] = phase == 0 ? 0 : 1,
            ["elapsedMilliseconds"] = elapsed
        }.ToJsonString();
        var exchanges = new JsonArray();
        void Add(string method, string path, int status, long start, long end, string body)
        {
            exchanges.Add(new JsonObject
            {
                ["index"] = exchanges.Count,
                ["method"] = method,
                ["path"] = path,
                ["status"] = status,
                ["startedMilliseconds"] = start,
                ["completedMilliseconds"] = end,
                ["dateUtc"] = new DateTimeOffset(2026, 10, 18, 0, 0, 0, TimeSpan.Zero).AddSeconds(end / 1000).ToString("O", CultureInfo.InvariantCulture),
                ["bodyBase64"] = Encode(body)
            });
        }
        const string path = "/__clros/service/";
        Add("GET", "/healthz", 200, 0, 10, "CLROS_SERVICE_HEALTH_OK");
        Add("GET", path + "status", 200, 10, 20, Status(0, 0, 0, 0, 0));
        Add("POST", path + "stop", 409, 20, 30, "");
        Add("POST", path + "start", 200, 30, 40, "CLROS_SERVICE_STARTED");
        Add("POST", path + "start", 409, 40, 50, "");
        Add("POST", path + "stop", 409, 50, 60, "");
        Add("GET", path + "status", 200, 60, 70, Status(1, 7, 1, 8192, 1));
        Add("GET", path + "status", 200, 300050, 300060, Status(1, 7, 4097, 8192, 300001));
        Add("GET", "/healthz", 200, 300070, 300080, "CLROS_SERVICE_HEALTH_OK");
        Add("POST", path + "stop", 200, 300080, 300090, "CLROS_SERVICE_STOPPED");
        return new JsonObject { ["schema"] = "clros.nativeaot.service.peer.v1", ["port"] = 8080, ["durationMilliseconds"] = 300100L, ["connectAttempts"] = 1, ["exchanges"] = exchanges };
    }
    private static JsonObject LifecycleReport()
    {
        var report = Report();
        report["schema"] = "clros.nativeaot.service.peer.v2";
        report["lifecycle"] = new JsonObject
        {
            ["port"] = 8080,
            ["sequentialConnections"] = 12,
            ["echoBytes"] = 32768,
            ["echoHash"] = 1653448133L,
            ["streamBytes"] = 524288,
            ["streamSha256"] = "0A131BA05D7CBD5F28DD2FAB7DA2B8BECE4122EB1A76FDA0D0159C66B520F8A1",
            ["resetRequests"] = 1,
            ["timeoutRequests"] = 1,
            ["holdStartedTicks"] = 100L,
            ["healthyCompletedTicks"] = 200L,
            ["holdReleasedTicks"] = 300L,
            ["terminalStatus"] = "CLROS_LIFECYCLE_STATUS active=0 waiting=0 waits=2 canceled=2 echoes=1 streams=1 rejected=1 read=65536 written=524288"
        };
        return report;
    }
    private static void Reject(string text, bool lifecycle = false)
    {
        bool rejected = false;
        try
        {
            _ = NativeAotServicePeer.InspectOutput(text, lifecycle);
        }
        catch (Exception error) when (error is ToolFailure or InvalidDataException) { rejected = true; }
        HostCheck.True(rejected);
    }
}
