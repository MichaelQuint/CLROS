using System.Net;
using System.Net.Sockets;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OS.Tooling;

/// <summary>
/// Linux NativeAOT socket measurement and its separate external peer. Publication
/// reuses validated original arguments, owned caches/output and bounded Linux
/// children. The peer owns four loopback clients and a two-minute deadline.
/// Neither Linux loopback nor the standalone peer confers CLROS guest acceptance.
/// </summary>
public static class NativeAotSocketPayload
{
    private const string App = "NativeAotSocketSmoke";

    /// <summary>Runs the peer or Linux-only qualifier. Qualification requires
    /// retained original web source/evidence, a fresh source cache and output.
    /// Every failure emits a failure marker and nonzero status.</summary>
    public static async Task<int> RunAsync(string root, string[] args, TextWriter output)
    {
        string marker = args.FirstOrDefault() == "qualify" ? "nativeaot.socket.payload" : "nativeaot.socket.peer";
        output.WriteLine("TEST_START " + marker);
        try
        {
            if (args is ["qualify", "--recipe-root", var recipeRoot, "--recipe-evidence", var recipeEvidence,
                "--output", var destination])
                await QualifyAsync(Path.GetFullPath(root), Path.GetFullPath(recipeRoot),
                    Path.GetFullPath(recipeEvidence), Path.GetFullPath(destination), output);
            else if (args is ["peer", "--port", var value] &&
                int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int port) && port is >= 1 and <= 65535)
                await RunPeerAsync(port, output);
            else throw new ArgumentException("nativeaot socket peer --port <1..65535> | qualify --recipe-root <original-source> --recipe-evidence <web-evidence> --output <fresh-directory>");
            output.WriteLine("TEST_PASS " + marker);
            return 0;
        }
        catch (Exception error)
        {
            output.WriteLine("TEST_FAIL " + marker + " " + error.Message.ReplaceLineEndings(" "));
            return 1;
        }
    }

    /// <summary>
    /// Exchanges independent literal bytes with a separately started application
    /// through the specified loopback port (normally a QEMU forward). The first
    /// receive is deliberately held for 250ms to exercise backpressure; reset
    /// publication holds its single byte for 100ms before abortive close. These
    /// bounded integration stimuli are not exact timing assertions.
    /// </summary>
    public static async Task RunPeerAsync(int port, TextWriter output)
    {
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await RunPeerCoreAsync(port, output, deadline.Token);
    }

    private static async Task RunPeerCoreAsync(int port, TextWriter output, CancellationToken token, int initialHoldMilliseconds = 250)
    {
        byte[] request = "CLROS_SOCKET_REQUEST"u8.ToArray();
        byte[] reply = "CLROS_SOCKET_REPLY"u8.ToArray();
        byte[] buffer = new byte[4096];
        for (int index = 0; index < 4; index++)
        {
            using Socket socket = await ConnectAsync(port, token);
            if (index == 1)
            {
                if (await socket.SendAsync(new byte[] { 0x52 }, SocketFlags.None, token) != 1)
                    throw new InvalidDataException("socket_peer_reset_byte");
                await Task.Delay(100, token);
                socket.LingerState = new LingerOption(true, 0);
                output.WriteLine("SOCKET_PEER_CONNECTION index=1 sent=1 close=reset");
                continue;
            }
            await SendAllAsync(socket, request.AsMemory(0, 5), token);
            await SendAllAsync(socket, request.AsMemory(5), token);
            int expected = index == 0 ? 65536 : reply.Length;
            if (index == 0) await Task.Delay(initialHoldMilliseconds, token);
            else socket.Shutdown(SocketShutdown.Send);
            int received = 0;
            while (received < expected)
            {
                int count = await socket.ReceiveAsync(buffer.AsMemory(0, Math.Min(buffer.Length, expected - received)),
                    SocketFlags.None, token);
                if (count == 0) throw new InvalidDataException("socket_peer_early_eof");
                for (int i = 0; i < count; i++)
                {
                    byte wanted = index == 0 ? (byte)((received + i) % 251) : reply[received + i];
                    if (buffer[i] != wanted) throw new InvalidDataException("socket_peer_payload");
                }
                received += count;
            }
            if (await socket.ReceiveAsync(buffer.AsMemory(0, 1), SocketFlags.None, token) != 0)
                throw new InvalidDataException("socket_peer_trailing_payload");
            output.WriteLine($"SOCKET_PEER_CONNECTION index={index} sent={request.Length} received={received} close=eof");
        }
    }

    /// <summary>Observed counters from four finite connections. Partial/blocked
    /// sends are observations, not required on every host and not guest proof.</summary>
    public sealed record Observation(int Received, int Sent, int PartialSends, int BlockedSends);

    /// <summary>Actual incomplete ValueTasks observed before await. These are
    /// distinct from synchronous EAGAIN counters; native traces must independently
    /// establish the epoll/syscall path. Cancellation and close are required too.</summary>
    public sealed record AsyncObservation(int Received, int Sent, int PartialSends,
        int PendingSends, int PendingAccepts, int PendingReceives);

    /// <summary>Checks the actual fixture's complete ordered stdout. Independent
    /// malformed/missing/failure/counter controls can test this parser; only its
    /// qualifier binds these observations to native image and trace hashes.</summary>
    public static Observation ValidateOutput(string text)
    {
        Need(text.Length <= 32768, "socket_output_size");
        string[] lines = text.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Need(lines.Length == 7 && lines[0] == "TEST_START nativeaot.socket" &&
            lines[1] == "SOCKET_LISTEN port=8080 connections=4 deadline_ms=30000", "socket_output_order");
        string[] keys = ["received", "sent", "accept_blocked", "receive_blocked", "send_blocked", "send_partial", "eof", "reset"];
        int[] totals = new int[keys.Length];
        for (int index = 0; index < 4; index++)
        {
            string prefix = "SOCKET_CONNECTION index=" + index.ToString(CultureInfo.InvariantCulture) + " ";
            Need(lines[index + 2].StartsWith(prefix, StringComparison.Ordinal), "socket_output_order");
            string[] values = lines[index + 2][prefix.Length..].Split(' ');
            Need(values.Length == keys.Length, "socket_output_counters");
            int[] row = new int[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                Need(values[i].StartsWith(keys[i] + "=", StringComparison.Ordinal) &&
                    int.TryParse(values[i].AsSpan(keys[i].Length + 1), NumberStyles.None,
                        CultureInfo.InvariantCulture, out row[i]) && row[i] >= 0, "socket_output_counters");
                totals[i] = checked(totals[i] + row[i]);
            }
            Need(row[0] == (index == 1 ? 1 : 20) && row[1] == (index == 0 ? 65536 : index == 1 ? 0 : 18) &&
                row[6] == (index >= 2 ? 1 : 0) && row[7] == (index == 1 ? 1 : 0) &&
                row[5] <= row[1] && (index != 1 || row[4] == 0), "socket_output_bytes_or_close");
        }
        string pass = "TEST_PASS nativeaot.socket connections=4 " + string.Join(' ', keys.Select((key, i) =>
            key + "=" + totals[i].ToString(CultureInfo.InvariantCulture)));
        Need(lines[6] == pass, "socket_output_terminal");
        return new(totals[0], totals[1], totals[5], totals[4]);
    }

    /// <summary>Validate the separate async fixture, including genuinely pending
    /// accept cancellation/close and exact four-connection byte/EOF/reset totals.
    /// A parser pass is not execution or epoll acceptance.</summary>
    public static AsyncObservation ValidateAsyncOutput(string text)
    {
        Need(text.Length <= 32768, "socket_async_output_size");
        string[] lines = text.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Need(lines.Length == 8 && lines[0] == "TEST_START nativeaot.socket.async" &&
            lines[1] == "SOCKET_ASYNC_LISTEN port=8080 connections=4 deadline_ms=30000" &&
            lines[6] == "SOCKET_ASYNC_TERMINAL cancelled_accept=1 closed_accept=1" &&
            lines[7].StartsWith("TEST_PASS nativeaot.socket.async connections=4 ", StringComparison.Ordinal), "socket_async_output_order");
        for (int index = 0; index < 4; index++)
            Need(lines[index + 2].StartsWith("SOCKET_ASYNC_CONNECTION index=" + index.ToString(CultureInfo.InvariantCulture) + " ",
                StringComparison.Ordinal), "socket_async_output_order");
        // The same wire protocol/counter sums are checked by the existing strict
        // parser; only the profile and genuinely different pending labels vary.
        string common = string.Join('\n', lines.Where((_, index) => index != 6))
            .Replace("nativeaot.socket.async", "nativeaot.socket", StringComparison.Ordinal)
            .Replace("SOCKET_ASYNC_LISTEN", "SOCKET_LISTEN", StringComparison.Ordinal)
            .Replace("SOCKET_ASYNC_CONNECTION", "SOCKET_CONNECTION", StringComparison.Ordinal)
            .Replace("accept_pending=", "accept_blocked=", StringComparison.Ordinal)
            .Replace("receive_pending=", "receive_blocked=", StringComparison.Ordinal)
            .Replace("send_pending=", "send_blocked=", StringComparison.Ordinal);
        Observation validated = ValidateOutput(common);
        int Total(string key) => int.Parse(lines[7].Split(' ').Single(value => value.StartsWith(key + "=", StringComparison.Ordinal))
            .AsSpan(key.Length + 1), NumberStyles.None, CultureInfo.InvariantCulture);
        return new(validated.Received, validated.Sent, validated.PartialSends, validated.BlockedSends,
            Total("accept_pending"), Total("receive_pending"));
    }

    /// <summary>Reuses the original publisher's recorded argument vector after
    /// web evidence validation by the caller. Only app and output arguments may
    /// differ; independent fixed-recipe comparison rejects changed flags/pins.
    /// This constructs arguments without executing or granting receipt authority.</summary>
    public static string[] PublishArguments(string commandsJson, string destination)
    {
        Need(commandsJson.Length <= 2 * 1024 * 1024, "socket_publish_recipe_size");
        using JsonDocument document = JsonDocument.Parse(commandsJson);
        string[] args = document.RootElement.EnumerateArray()
            .Where(row => row.GetProperty("executable").GetString() == "dotnet")
            .Select(row => row.GetProperty("arguments").EnumerateArray().Select(value => value.GetString()!).ToArray())
            .Single(row => row.Length >= 2 && row[0] == "publish" && row[1] == "apps/NativeAotHello/NativeAotHello.csproj");
        string[] expected = NativeAotThreadingPayload.PublishArguments("NativeAotSuspensionSmoke", destination).ToArray();
        args[1] = expected[1];
        int at = Array.IndexOf(args, "-o");
        Need(at >= 0 && at + 1 < args.Length, "socket_publish_recipe");
        args[at + 1] = Path.GetFullPath(destination);
        Need(args.SequenceEqual(expected), "socket_publish_recipe");
        args[1] = "apps/" + App + "/" + App + ".csproj";
        return args;
    }

    private static async Task QualifyAsync(string root, string recipeRoot, string recipeEvidence,
        string destination, TextWriter output)
    {
        Need(OperatingSystem.IsLinux(), "linux_builder_required");
        using var lease = NativeAotPayload.OpenQualificationLease(root);
        using var recipeLease = root == recipeRoot ? null : NativeAotPayload.OpenQualificationLease(recipeRoot);
        NativeAotWebPayload.ValidateQualification(recipeRoot, recipeEvidence);
        Need(ToolingBuildInputs.Validate(root).Count == 0, "tooling_inputs_changed");
        Need(destination.StartsWith(Path.Combine(root, "artifacts") + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
            !Path.Exists(destination), "fresh_artifacts_output_required");
        AuthorityFiles.RequirePlainPath(root, Path.Combine(root, "apps", App), "socket_source_path");
        foreach (string relative in new[] { "bin", "obj" })
            Need(!Path.Exists(Path.Combine(root, "apps", App, relative)), "fresh_socket_cache_required");
        foreach (string relative in new[] { "apps/Directory.Build.props", "apps/Directory.Build.targets", "NuGet.Config", "nuget.config" })
            Need(!Path.Exists(Path.Combine(root, relative)), "socket_source_override");
        string[] SourcePaths() => Directory.EnumerateFiles(Path.Combine(root, "apps", App), "*", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
            .Concat(ToolingBuildInputs.SourceInputs(root)).Distinct().Order(StringComparer.Ordinal).ToArray();
        var sources = SourcePaths().ToDictionary(path => Path.GetRelativePath(root, path).Replace('\\', '/'), NativeAotWebPayload.HashFile);
        EfiBuildFiles.InitializeDirectory(destination, "socket_evidence_path");
        using var ownership = new FileStream(Path.Combine(destination, "qualification.lock"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        string publish = Path.Combine(destination, App);
        var commands = new List<object>();
        var native = new Dictionary<string, string>(StringComparer.Ordinal);
        NativeAotPayload.Inspection? image = null;
        Observation? observed = null;
        AsyncObservation? asyncObserved = null;
        async Task Run(string name, string executable, string[] arguments, TimeSpan timeout)
        {
            string log = Path.Combine(destination, name);
            commands.Add(new { executable, arguments, workingDirectory = root, log });
            await NativeAotWebPayload.ExecuteAsync(executable, arguments, root, log, timeout);
        }
        try
        {
            var recipeNative = JsonSerializer.Deserialize<Dictionary<string, string>>(
                await File.ReadAllTextAsync(Path.Combine(recipeEvidence, "native-inputs.json")))!;
            foreach (string tool in new[] { "dotnet", "clang", "ld", "gcc", "readelf", "nm", "strace", "setsid" })
            {
                string path = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
                    .Select(directory => Path.Combine(directory, tool)).First(File.Exists);
                path = File.ResolveLinkTarget(path, true)?.FullName ?? Path.GetFullPath(path);
                string hash = NativeAotWebPayload.HashFile(path);
                Need(recipeNative.TryGetValue(path, out string? prior) && hash == prior, "socket_recipe_tool_changed");
                native.TryAdd(path, hash);
            }
            await Run("sdk", "dotnet", ["--version"], TimeSpan.FromMinutes(1));
            Need((await File.ReadAllTextAsync(Path.Combine(destination, "sdk.stdout.log"))).Trim() == NativeAotPayload.SdkVersion, "sdk_pin_mismatch");
            string[] args = PublishArguments(await File.ReadAllTextAsync(Path.Combine(recipeEvidence, "commands.json")), publish);
            output.WriteLine("NATIVEAOT_SOCKET_PUBLISH " + App); await output.FlushAsync();
            await Run("publish", "dotnet", args, TimeSpan.FromMinutes(45));
            string executable = Path.Combine(publish, App);
            Need(new FileInfo(executable).Length <= NativeAotPayload.MaximumImageBytes, "elf_image_size");
            image = NativeAotPayload.Inspect(await File.ReadAllBytesAsync(executable));
            string intermediate = Path.Combine(root, "apps", App, "obj", "Release", NativeAotPayload.TargetFramework, "linux-musl-x64");
            foreach (string path in new[] { executable, Path.Combine(intermediate, App + ".dll"), Path.Combine(intermediate, "native", App + ".o"),
                typeof(NativeAotSocketPayload).Assembly.Location, Path.Combine(recipeEvidence, "web-receipt.json") })
                native.Add(path, NativeAotWebPayload.HashFile(path));
            string assetsPath = Path.Combine(root, "apps", App, "obj", "project.assets.json");
            File.Copy(assetsPath, Path.Combine(destination, "assets.json"));
            using (JsonDocument assets = JsonDocument.Parse(await File.ReadAllTextAsync(assetsPath)))
                foreach (var row in NativeAotPayload.CapturePackageInputs(assets.RootElement)) native.TryAdd(row.Key, row.Value);
            foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "apps", App, "obj"), "*.rsp", SearchOption.AllDirectories))
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
            await Run("elf", "readelf", ["-h", "-l", "-d", "-n", "-W", executable], TimeSpan.FromMinutes(2));
            await Run("symbols", "nm", ["-a", executable], TimeSpan.FromMinutes(2));
            observed = ValidateOutput(await RunSocketAsync(executable, publish, destination, false, commands, output));
            asyncObserved = ValidateAsyncOutput(await RunSocketAsync(executable, publish, destination, true, commands, output));
            Need(asyncObserved.PendingAccepts > 0 && asyncObserved.PendingReceives > 0 && asyncObserved.PendingSends > 0,
                "socket_async_waits_not_observed");
            string[] traces = Directory.GetFiles(destination, App + "*-trace.*");
            Need(traces.Length is > 0 and <= 64, "socket_trace_inventory");
            var calls = new SortedSet<string>(StringComparer.Ordinal);
            var asyncCalls = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string file in traces)
            {
                Need(new FileInfo(file).Length is > 0 and <= 4 * 1024 * 1024, "socket_trace_size");
                string text = await File.ReadAllTextAsync(file, new UTF8Encoding(false, true));
                Need(text.TrimEnd('\r', '\n').EndsWith("+++ exited with 0 +++", StringComparison.Ordinal) &&
                    !text.Contains("+++ killed by", StringComparison.Ordinal), "socket_trace_exit");
                foreach (string line in text.Split('\n'))
                {
                    Match match = Regex.Match(line, "^([a-zA-Z0-9_]+)\\(");
                    if (match.Success)
                    {
                        string call = match.Groups[1].Value; calls.Add(call);
                        if (Path.GetFileName(file).StartsWith(App + "-async-trace.", StringComparison.Ordinal)) asyncCalls.Add(call);
                    }
                }
            }
            Need(calls.Contains("execve") && calls.Contains("exit_group") && calls.Contains("socket"), "socket_trace_incomplete");
            Need(asyncCalls.Contains("epoll_ctl") && (asyncCalls.Contains("epoll_pwait") || asyncCalls.Contains("epoll_wait")), "socket_async_trace_incomplete");
            Need(SourcePaths().Select(path => Path.GetRelativePath(root, path).Replace('\\', '/')).SequenceEqual(sources.Keys) &&
                sources.All(row => NativeAotWebPayload.HashFile(Path.Combine(root, row.Key)) == row.Value) &&
                ToolingBuildInputs.Validate(root).Count == 0 && native.All(row => NativeAotWebPayload.HashFile(row.Key) == row.Value), "socket_inputs_changed");
            NativeAotWebPayload.ValidateQualification(recipeRoot, recipeEvidence);
            await File.WriteAllLinesAsync(Path.Combine(destination, "observed-syscalls.txt"), calls);
            await File.WriteAllLinesAsync(Path.Combine(destination, "async-observed-syscalls.txt"), asyncCalls);
        }
        finally
        {
            await File.WriteAllTextAsync(Path.Combine(destination, "commands.json"), JsonSerializer.Serialize(commands));
            await File.WriteAllTextAsync(Path.Combine(destination, "native-inputs.json"), JsonSerializer.Serialize(native));
        }
        var outputs = Directory.GetFiles(destination, "*", SearchOption.AllDirectories).Where(path => Path.GetFileName(path) != "qualification.lock")
            .ToDictionary(path => Path.GetRelativePath(destination, path).Replace('\\', '/'), NativeAotWebPayload.HashFile);
        await File.WriteAllTextAsync(Path.Combine(destination, "receipt.json"), JsonSerializer.Serialize(new {
            schema = "clros.nativeaot.socket.v2", sdk = NativeAotPayload.SdkVersion, runtime = NativeAotPayload.RuntimeVersion,
            image = image ?? throw new InvalidDataException("socket_inspection_missing"),
            observed = observed ?? throw new InvalidDataException("socket_observations_missing"),
            asyncObserved = asyncObserved ?? throw new InvalidDataException("socket_async_observations_missing"),
            sources, native, outputs, boundary = "Linux builder loopback only; no CLROS guest or epoll acceptance." }));
    }

    private static async Task<string> RunSocketAsync(string executable, string publish, string destination,
        bool asynchronous, List<object> commands, TextWriter output)
    {
        string name = asynchronous ? "async" : "run";
        string trace = App + (asynchronous ? "-async" : "") + "-trace";
        string[] arguments = ["-ff", "-s", "256", "-o", Path.Combine(destination, trace), executable];
        if (asynchronous) arguments = [.. arguments, "--async"];
        commands.Add(new { name, executable = "strace", arguments });
        using var process = NativeAotWebPayload.Start("strace", arguments, publish);
        Task stdout = NativeAotWebPayload.CaptureAsync(process.StandardOutput, Path.Combine(destination, name + ".stdout.log"), process);
        Task stderr = NativeAotWebPayload.CaptureAsync(process.StandardError, Path.Combine(destination, name + ".stderr.log"), process);
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        Task peer = RunPeerCoreAsync(8080, output, deadline.Token, asynchronous ? 1000 : 250);
        try
        {
            Task exited = process.WaitForExitAsync(deadline.Token);
            Task first = await Task.WhenAny(peer, exited);
            Need(first != exited || process.ExitCode == 0, "socket_payload_exited_early_" + name);
            await peer; await exited;
            Need(process.ExitCode == 0, "socket_payload_exit_" + name);
            await Task.WhenAll(stdout, stderr).WaitAsync(deadline.Token);
        }
        finally
        {
            deadline.Cancel();
            await NativeAotWebPayload.StopSessionAsync(process);
            try { await peer; } catch (OperationCanceledException) { }
            await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(10));
        }
        return await File.ReadAllTextAsync(Path.Combine(destination, name + ".stdout.log"));
    }

    private static void Need(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }

    private static async Task<Socket> ConnectAsync(int port, CancellationToken token)
    {
        while (true)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                socket.NoDelay = true;
                socket.ReceiveBufferSize = 4096;
                await socket.ConnectAsync(IPAddress.Loopback, port, token);
                return socket;
            }
            catch (SocketException error) when (error.SocketErrorCode == SocketError.ConnectionRefused)
            {
                socket.Dispose();
                await Task.Delay(50, token);
            }
            catch { socket.Dispose(); throw; }
        }
    }

    private static async Task SendAllAsync(Socket socket, ReadOnlyMemory<byte> bytes, CancellationToken token)
    {
        while (!bytes.IsEmpty)
        {
            int sent = await socket.SendAsync(bytes, SocketFlags.None, token);
            if (sent <= 0) throw new InvalidDataException("socket_peer_send");
            bytes = bytes[sent..];
        }
    }
}
