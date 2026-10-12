using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OS.Tooling;

/// <summary>
/// Host-only qualification of upstream static Linux x64 NativeAOT payloads.
/// Inspection consumes supplied bytes without mapping or executing them. Qualification
/// requires a Linux builder and owns only its child processes and fresh output directory.
/// Neither operation grants CLROS loader, syscall, runtime or guest authority.
/// </summary>
public static class NativeAotPayload
{
    public const int MaximumImageBytes = 128 * 1024 * 1024;
    public const ulong MaximumMappedBytes = 1024 * 1024 * 1024;
    public const string SdkVersion = "10.0.401";
    public const string RuntimeVersion = "10.0.12";

    public sealed record Segment(uint Type, uint Flags, ulong Offset, ulong Address,
        ulong FileSize, ulong MemorySize, ulong Alignment);
    public sealed record Inspection(ulong Entry, int ProgramHeaderCount,
        IReadOnlyList<Segment> Loads, Segment? Tls, string Sha256);

    /// <summary>
    /// Accepts bounded little-endian ELF64 ET_EXEC/x86-64 images with file-backed executable
    /// entry, disjoint W^X loads, optional contained TLS, and no interpreter or dynamic loader.
    /// Throws InvalidDataException on unsupported/malformed bytes. Returned metadata owns no input.
    /// Section headers, relocations, auxv and instruction requirements remain separate review inputs.
    /// </summary>
    public static Inspection Inspect(ReadOnlySpan<byte> image)
    {
        Require(image.Length is >= 64 and <= MaximumImageBytes, "elf_image_size");
        Require(image[..4].SequenceEqual("\u007fELF"u8) && image[4] == 2 && image[5] == 1 && image[6] == 1,
            "elf_identification");
        Require(image[7] is 0 or 3 && image[8] == 0 && image.Slice(9, 7).IndexOfAnyExcept((byte)0) < 0,
            "elf_abi");
        Require(U16(image, 16) == 2 && U16(image, 18) == 62 && U32(image, 20) == 1 &&
            U32(image, 48) == 0 && U16(image, 52) == 64 && U16(image, 54) == 56, "elf_header");
        int count = U16(image, 56);
        ulong headers = U64(image, 32);
        Require(count is > 0 and <= 128 && headers >= 64 && Fits(headers, (ulong)count * 56, (ulong)image.Length),
            "elf_program_headers");
        ulong entry = U64(image, 24);
        var loads = new List<Segment>();
        Segment? tls = null;
        for (int i = 0; i < count; i++)
        {
            int at = checked((int)headers + i * 56);
            var row = new Segment(U32(image, at), U32(image, at + 4), U64(image, at + 8),
                U64(image, at + 16), U64(image, at + 32), U64(image, at + 40), U64(image, at + 48));
            Require(row.Type is not (2 or 3), row.Type == 3 ? "elf_interpreter" : "elf_dynamic_segment");
            Require(Fits(row.Offset, row.FileSize, (ulong)image.Length), "elf_segment_file_bounds");
            if (row.Type == 0x6474e551) Require((row.Flags & 1) == 0, "elf_executable_stack");
            if (row.Type is not (1 or 7)) continue;
            Require(row.Flags <= 7 && row.FileSize <= row.MemorySize && row.MemorySize > 0 &&
                row.Address != 0 && row.Address < 0x800000000000UL &&
                row.MemorySize <= Math.Min(MaximumMappedBytes, 0x800000000000UL - row.Address), "elf_segment_memory_bounds");
            Require(row.Alignment <= 1 || ((row.Alignment & (row.Alignment - 1)) == 0 &&
                row.Address % row.Alignment == row.Offset % row.Alignment), "elf_segment_alignment");
            if (row.Type == 7)
            {
                Require(tls is null, "elf_duplicate_tls");
                tls = row;
            }
            else
            {
                Require((row.Flags & 3) != 3 && (row.Flags & 4) != 0, "elf_load_permissions");
                Require(row.Address % 4096 == row.Offset % 4096, "elf_load_page_alignment");
                Require(loads.All(other => PageStart(row.Address) >= PageEnd(other) ||
                    PageStart(other.Address) >= PageEnd(row)), "elf_overlapping_load_pages");
                Require(loads.Sum(other => (decimal)other.MemorySize) + row.MemorySize <= MaximumMappedBytes,
                    "elf_mapped_size_limit");
                loads.Add(row);
            }
        }
        Require(loads.Any(row => (row.Flags & 1) != 0 && entry >= row.Address &&
            entry - row.Address < row.FileSize), "elf_entry");
        if (tls is not null)
            Require(loads.Any(row => tls.Address >= row.Address &&
                Fits(tls.Address - row.Address, tls.MemorySize, row.MemorySize) &&
                Fits(tls.Address - row.Address, tls.FileSize, row.FileSize) &&
                tls.Offset == row.Offset + (tls.Address - row.Address)), "elf_tls_load_association");
        return new(entry, count, loads.AsReadOnly(), tls, Convert.ToHexString(SHA256.HashData(image)));
    }

    /// <summary>CLI: inspect &lt;file&gt; or qualify --output &lt;fresh directory&gt;. Failures emit no pass marker.</summary>
    public static async Task<int> RunAsync(string root, string[] args, TextWriter output)
    {
        string marker = args.FirstOrDefault() == "inspect" ? "nativeaot.payload.inspect" : "m16-001";
        output.WriteLine("TEST_START " + marker);
        try
        {
            if (args is ["inspect", var path])
            {
                Inspection inspected = InspectFile(Path.GetFullPath(path));
                output.WriteLine("NATIVEAOT_PAYLOAD_INSPECTED " + JsonSerializer.Serialize(inspected));
            }
            else if (args is ["qualify", "--output", var destination])
                await QualifyAsync(Path.GetFullPath(root), Path.GetFullPath(destination), output);
            else throw new ArgumentException("nativeaot payload inspect <file> | qualify --output <fresh-directory>");
            output.WriteLine("TEST_PASS " + marker);
            return 0;
        }
        catch (Exception error)
        {
            output.WriteLine("TEST_FAIL " + marker + " " + error.Message.ReplaceLineEndings(" "));
            return 1;
        }
    }

    private static Inspection InspectFile(string path)
    {
        Require(new FileInfo(path).Length <= MaximumImageBytes, "elf_image_size");
        return Inspect(File.ReadAllBytes(path));
    }

    private static async Task QualifyAsync(string root, string destination, TextWriter output)
    {
        Require(OperatingSystem.IsLinux(), "linux_builder_required");
        Require(!Directory.Exists(destination) && !File.Exists(destination), "fresh_output_required");
        Directory.CreateDirectory(destination);
        using var ownership = new FileStream(Path.Combine(destination, "qualification.lock"), FileMode.CreateNew,
            FileAccess.Write, FileShare.None);
        Require(ToolingBuildInputs.Validate(root).Count == 0, "tooling_inputs_changed");
        string[] apps = ["NativeAotHello", "NativeAotRuntimeSmoke", "MinimalApiSmoke"];
        string[] inputs = apps.SelectMany(app => Directory.EnumerateFiles(Path.Combine(root, "apps", app), "*", SearchOption.AllDirectories))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Where(path => !path.Split('/').Any(part => part is "bin" or "obj" or "artifacts"))
            .Concat(ToolingBuildInputs.SourceInputs(root).Select(path => Path.GetRelativePath(root, path).Replace('\\', '/')))
            .Concat(new[] { "NuGet.Config", "nuget.config" }.Where(path => File.Exists(Path.Combine(root, path))))
            .Distinct().Order().ToArray();
        Dictionary<string, string> hashes = HashInputs(root, inputs);
        await File.WriteAllTextAsync(Path.Combine(destination, "inputs.json"), JsonSerializer.Serialize(hashes));
        var commands = new List<object>();
        async Task<string> Run(string name, string executable, string[] arguments, TimeSpan? timeout = null)
        {
            string log = Path.Combine(destination, name);
            commands.Add(new { executable, arguments, workingDirectory = root, log });
            await ExecuteAsync(executable, arguments, root, log, timeout ?? TimeSpan.FromMinutes(2));
            return await File.ReadAllTextAsync(log + ".stdout.log");
        }
        try
        {
            string sdk = await Run("sdk-version", "dotnet", ["--version"]);
            Require(sdk.Trim() == SdkVersion, "sdk_pin_mismatch");
            await Run("dotnet-info", "dotnet", ["--info"]);
            await Run("compiler", "clang", ["--version"]);
            await Run("linker", "ld", ["--version"]);
            await Run("kernel", "uname", ["-a"]);
            await Run("packages", "apk", ["info", "-vv"]);
            await Run("cpu", "cat", ["/proc/cpuinfo"]);
            var inspections = new Dictionary<string, Inspection>();
            foreach (string app in apps)
            {
                output.WriteLine("NATIVEAOT_PAYLOAD_PUBLISH " + app);
                string publish = Path.Combine(destination, app);
                await Run(app + "-publish", "dotnet", ["publish", $"apps/{app}/{app}.csproj", "-c", "Release",
                    "-r", "linux-musl-x64", "--self-contained", "true", "-o", publish, "--nologo",
                    "-p:PublishAot=true", "-p:StaticExecutable=true", "-p:PositionIndependentExecutable=false",
                    "-p:InvariantGlobalization=true", "-p:ServerGarbageCollection=false", "-p:EventSourceSupport=false",
                    "-p:EnableDiagnostics=false", "-p:DebuggerSupport=false", "-p:RuntimeFrameworkVersion=" + RuntimeVersion,
                    "-p:TargetLatestRuntimePatch=false", "-p:UseSharedCompilation=false", "-nr:false"], TimeSpan.FromMinutes(45));
                string executable = Path.Combine(publish, app);
                inspections.Add(app, InspectFile(executable));
                output.WriteLine("NATIVEAOT_PAYLOAD_INSPECTED " + app);
                await Run(app + "-elf", "readelf", ["-h", "-l", "-d", "-n", "-W", executable]);
                await Run(app + "-symbols", "nm", ["-a", executable]);
                if (app != "MinimalApiSmoke")
                {
                    string text = await Run(app + "-run", "strace", ["-ff", "-s", "256", "-o",
                        Path.Combine(destination, app + "-trace"), executable]);
                    string marker = app == "NativeAotHello" ? "TEST_PASS nativeaot.hello" : "TEST_PASS nativeaot.runtime";
                    Require(text.Replace("\r", "").Split('\n').Contains(marker), "payload_run_marker");
                }
                else await RunHttpAsync(root, destination, executable, commands);
            }
            string[] currentApps = apps.SelectMany(app => Directory.EnumerateFiles(Path.Combine(root, "apps", app), "*", SearchOption.AllDirectories))
                .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
                .Where(path => !path.Split('/').Any(part => part is "bin" or "obj" or "artifacts")).Distinct().Order().ToArray();
            Require(currentApps.SequenceEqual(inputs.Where(path => path.StartsWith("apps/"))) &&
                ToolingBuildInputs.Validate(root).Count == 0 &&
                HashInputs(root, inputs).OrderBy(x => x.Key).SequenceEqual(hashes.OrderBy(x => x.Key)), "source_inputs_changed");
            await File.WriteAllTextAsync(Path.Combine(destination, "inspection.json"), JsonSerializer.Serialize(inspections));
            string[] syscalls = Directory.GetFiles(destination, "*-trace.*").SelectMany(File.ReadLines)
                .Select(line => Regex.Match(line, "^([a-zA-Z0-9_]+)\\("))
                .Where(match => match.Success).Select(match => match.Groups[1].Value).Distinct().Order().ToArray();
            Require(syscalls.Contains("execve") && syscalls.Contains("exit_group"), "trace_inventory_incomplete");
            await File.WriteAllLinesAsync(Path.Combine(destination, "observed-syscalls.txt"), syscalls);
            await File.WriteAllTextAsync(Path.Combine(destination, "boundary.txt"),
                "Linux builder qualification only. Observed syscalls are not exhaustive; source/error-path, auxv, TLS and CPU review remain required. No CLROS guest authority.\n");
        }
        finally
        {
            await File.WriteAllTextAsync(Path.Combine(destination, "commands.json"), JsonSerializer.Serialize(commands));
        }
    }

    private static async Task RunHttpAsync(string root, string destination, string executable, List<object> commands)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        string endpoint = "http://127.0.0.1:" + port;
        string trace = Path.Combine(destination, "MinimalApiSmoke-trace");
        string log = Path.Combine(destination, "MinimalApiSmoke-run");
        string expectedBody = "OS_MINIMAL_API_OK_" + Guid.NewGuid().ToString("N");
        string[] arguments = ["-ff", "-s", "256", "-o", trace, executable, "--urls", endpoint];
        commands.Add(new { executable = "strace", arguments, workingDirectory = root, log });
        using var process = Start("strace", arguments, root, expectedBody);
        Task stdout = CaptureAsync(process.StandardOutput, log + ".stdout.log", process);
        Task stderr = CaptureAsync(process.StandardError, log + ".stderr.log", process);
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        using var client = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            while (true)
            {
                Require(!process.HasExited, "http_payload_exited");
                try
                {
                    using var response = await client.GetAsync(endpoint + "/healthz", deadline.Token);
                    if (response.IsSuccessStatusCode) break;
                }
                catch (HttpRequestException) { }
                await Task.Delay(100, deadline.Token);
            }
            foreach (string path in new[] { "/", "/healthz" })
            {
                using var response = await client.GetAsync(endpoint + path, deadline.Token);
                Require(response.StatusCode == HttpStatusCode.OK &&
                    await response.Content.ReadAsStringAsync(deadline.Token) == expectedBody, "http_status_or_body");
            }
            using var config = JsonDocument.Parse(await client.GetStringAsync(endpoint + "/config", deadline.Token));
            Require(config.RootElement.GetProperty("appId").GetString() == "minimal.api.smoke" &&
                config.RootElement.GetProperty("expectedBody").GetString() == expectedBody, "http_config_contract");
            await File.WriteAllTextAsync(Path.Combine(destination, "http.json"),
                JsonSerializer.Serialize(new { endpoint, paths = new[] { "/", "/healthz", "/config" }, status = 200, body = expectedBody }));
            string[] children = (await File.ReadAllTextAsync($"/proc/{process.Id}/task/{process.Id}/children", deadline.Token))
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Require(children.Length == 1 && int.TryParse(children[0], out _), "http_tracee_identity");
            Require(File.ResolveLinkTarget($"/proc/{children[0]}/exe", true)?.FullName == executable, "http_tracee_executable");
            await ExecuteAsync("kill", ["-TERM", children[0]], root,
                Path.Combine(destination, "MinimalApiSmoke-shutdown"), TimeSpan.FromSeconds(10));
            await process.WaitForExitAsync(deadline.Token);
            Require(process.ExitCode == 0, "http_shutdown_exit");
            await Task.WhenAll(stdout, stderr).WaitAsync(deadline.Token);
        }
        finally
        {
            await StopSessionAsync(process);
            await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    private static async Task ExecuteAsync(string executable, string[] args, string root, string log, TimeSpan timeout)
    {
        using var process = Start(executable, args, root);
        using var deadline = new CancellationTokenSource(timeout);
        Task stdout = CaptureAsync(process.StandardOutput, log + ".stdout.log", process);
        Task stderr = CaptureAsync(process.StandardError, log + ".stderr.log", process);
        try
        {
            await Task.WhenAll(process.WaitForExitAsync(deadline.Token), stdout, stderr).WaitAsync(deadline.Token);
            Require(process.ExitCode == 0, "native_process_exit_" + process.ExitCode + "_" + log);
        }
        finally
        {
            await StopSessionAsync(process);
            await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    private static Process Start(string executable, string[] args, string root, string expectedBody = "OS_MINIMAL_API_OK")
    {
        var start = new ProcessStartInfo("setsid") { WorkingDirectory = root, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        start.ArgumentList.Add(executable);
        foreach (string argument in args) start.ArgumentList.Add(argument);
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["OS_SMOKE_EXPECTED_BODY"] = expectedBody;
        start.Environment["DOTNET_SYSTEM_GLOBALIZATION_INVARIANT"] = "1";
        start.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        return Process.Start(start) ?? throw new IOException("native_process_start");
    }

    private static async Task CaptureAsync(StreamReader reader, string path, Process process)
    {
        try
        {
            await using var log = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            char[] buffer = new char[4096];
            int total = 0;
            while (true)
            {
                int count = await reader.ReadAsync(buffer);
                if (count == 0) return;
                total = checked(total + count);
                Require(total <= 32 * 1024 * 1024, "native_log_limit");
                await log.WriteAsync(Encoding.UTF8.GetBytes(buffer.AsSpan(0, count).ToString()));
            }
        }
        catch
        {
            Kill(-process.Id, 9);
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) when (process.HasExited) { }
            throw;
        }
    }

    private static Dictionary<string, string> HashInputs(string root, string[] paths) =>
        paths.ToDictionary(path => path, path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, path)))));
    private static bool Fits(ulong offset, ulong length, ulong available) => offset <= available && length <= available - offset;
    private static ulong PageStart(ulong address) => address & ~4095UL;
    private static ulong PageEnd(Segment row) => (row.Address + row.MemorySize + 4095) & ~4095UL;

    // Every Linux child starts in its own session/process group. This also covers
    // descendants retaining pipes after a successful compiler/wrapper exit.
    private static async Task StopSessionAsync(Process process)
    {
        int result = Kill(-process.Id, 9);
        Require(result == 0 || Marshal.GetLastPInvokeError() == 3, "native_session_cleanup");
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        var elapsed = Stopwatch.StartNew();
        while (Kill(-process.Id, 0) == 0)
        {
            Require(elapsed.Elapsed < TimeSpan.FromSeconds(10), "native_session_still_live");
            await Task.Delay(10);
        }
        Require(Marshal.GetLastPInvokeError() == 3, "native_session_confirmation");
    }
    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int Kill(int pid, int signal);
    private static ushort U16(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadUInt16LittleEndian(bytes[at..]);
    private static uint U32(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
    private static ulong U64(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadUInt64LittleEndian(bytes[at..]);
    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException(reason);
    }
}
