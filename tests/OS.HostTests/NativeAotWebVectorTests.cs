using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

internal static class NativeAotWebVectorTests
{
    [HostTest("m16-013 real managed Kestrel streams fixed segmented bytes then retains healthy progress")]
    private static void SegmentedResponse() => Run().GetAwaiter().GetResult();

    private static async Task Run()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string directory = Path.Combine(root, "artifacts", "web-vector-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        using var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start();
        int port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();
        string assembly = Path.Combine(AppContext.BaseDirectory, "MinimalApiSmoke.dll");
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = directory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in new[]{"exec","--runtimeconfig",Path.Combine(AppContext.BaseDirectory,"OS.HostTests.runtimeconfig.json"),
            "--depsfile",Path.Combine(AppContext.BaseDirectory,"OS.HostTests.deps.json"),assembly,"--urls","http://127.0.0.1:"+port})
            start.ArgumentList.Add(argument);
        using var child = Process.Start(start) ?? throw new InvalidOperationException("app launch failed");
        Task<string> output = child.StandardOutput.ReadToEndAsync(), error = child.StandardError.ReadToEndAsync();
        string observed = "";
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (context, token) =>
        {
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { ReceiveBufferSize = 1024 };
            try
            {
                await socket.ConnectAsync(context.DnsEndPoint, token);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
        };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:" + port), Timeout = Timeout.InfiniteTimeSpan };
        try
        {
            while (true)
            {
                HostCheck.True(!child.HasExited, "app exited before listener");
                try
                {
                    using var health = await client.GetAsync("/healthz", deadline.Token);
                    health.EnsureSuccessStatusCode();
                    break;
                }
                catch (HttpRequestException)
                {
                    await Task.Delay(25, deadline.Token);
                }
            }
            for (int repetition = 0; repetition < 2; repetition++)
            {
                using var response = await client.GetAsync("/__clros/vectors", HttpCompletionOption.ResponseHeadersRead, deadline.Token);
                HostCheck.Equal(HttpStatusCode.OK, response.StatusCode);
                HostCheck.Equal(49152L, response.Content.Headers.ContentLength ?? -1);
                HostCheck.Equal("application/octet-stream", response.Content.Headers.ContentType?.MediaType ?? "");
                await Task.Delay(100, deadline.Token); // Peer stimulus, not evidence of an actual blocked send.
                await using var body = await response.Content.ReadAsStreamAsync(deadline.Token);
                byte[] buffer = new byte[257];
                int total = 0, count;
                while ((count = await body.ReadAsync(buffer, deadline.Token)) != 0)
                {
                    HostCheck.True(total + count <= 49152, "response overflow");
                    for (int i = 0; i < count; i++)
                        HostCheck.Equal((byte)((total + i) % 251), buffer[i]);
                    total += count;
                }
                HostCheck.Equal(49152, total);
                HostCheck.Equal("OS_MINIMAL_API_OK", await client.GetStringAsync("/healthz", deadline.Token));
            }
            using var wrongMethod = await client.PostAsync("/__clros/vectors", new ByteArrayContent([]), deadline.Token);
            HostCheck.Equal(HttpStatusCode.MethodNotAllowed, wrongMethod.StatusCode);
        }
        finally
        {
            if (!child.HasExited)
                child.Kill(entireProcessTree: true);
            await child.WaitForExitAsync();
            observed = await error;
            string stdout = await output;
            await File.WriteAllTextAsync(Path.Combine(directory, "stderr.log"), observed);
            await File.WriteAllTextAsync(Path.Combine(directory, "stdout.log"), stdout);
        }
        HostCheck.Equal(2, observed.Split("CLROS_VECTOR_STAGED bytes=49152 blocks=3", StringSplitOptions.None).Length - 1);
        HostCheck.True(!observed.Contains("CLROS_APP_FAIL", StringComparison.Ordinal));
    }
}
