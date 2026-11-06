using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OS.Tooling;

public static partial class NativeAotLogUploadHttpContract
{
    /// <summary>Build the separate pinned managed Web app and unchanged console
    /// verifier, then own real Windows-job children and bounded loopback HTTP.
    /// Exercise preflight rejection, bad framing, a pending-body busy/abort,
    /// cancellation and real sharing-denied output opens before/after effects. The positive app
    /// stops only after its final response callback; require exact delivered
    /// bytes, actual exit0, refused endpoint and distinct console verification.
    /// Source/build handles remain held through completion. Failure receipts
    /// use an uncancelled writer after every launched child is joined. App data
    /// are not held against writes during the positive operation. No Linux,
    /// native/guest, disk image, device FLUSH or power-loss claim is granted.
    /// Call under this isolated checkout's serialized managed-build ownership.</summary>

    public static async Task<ManagedReference> RunManagedReferenceAsync(string root, string destination, CancellationToken cancellation = default)
    {
        Need(OperatingSystem.IsWindows(), "managed_windows_required");
        root = Path.GetFullPath(root);
        destination = Path.GetFullPath(destination, root);
        RequireFreshOutput(root, destination);
        NativeAotLogUploadContract.RequireCurrentSources(root);
        Need(ToolingBuildInputs.Validate(root).Count == 0, "tooling_inputs_changed");
        using var lease = NativeAotPayload.OpenQualificationLease(root);
        using AuthorityFiles held = new();
        string appDirectory = Path.Combine(root, "apps", Application), oldDirectory = Path.Combine(root, "apps", NativeAotLogUploadContract.Application);
        var inputs = new List<AuthorityFile>();
        foreach (string path in new[]
        {
            Path.Combine(appDirectory, Application + ".csproj"),
            Path.Combine(appDirectory, "Program.cs"),
            Path.Combine(oldDirectory, NativeAotLogUploadContract.Application + ".csproj"),
            Path.Combine(oldDirectory, "Program.cs"),
            Path.Combine(root, "global.json"),
            Path.Combine(root, "Directory.Build.props"),
            Path.Combine(root, "Directory.Build.targets"),
            typeof(NativeAotLogUploadHttpContract).Assembly.Location
        }

        )
            inputs.Add(held.Open(root, path, "managed HTTP source/producer", "log_upload_http_input", 64 * 1024 * 1024, singleLink: true));
        Directory.CreateDirectory(destination);
        AuthorityFiles.RequirePlainPath(root, destination, "log_upload_http_output");
        using var ownership = new FileStream(Path.Combine(destination, "ownership.lock"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        bool complete = false;
        string failure = "";
        List<object> commands = [];
        List<Control> controls = [];
        List<ResponseFact> responses = [];
        NativeAotLogUploadContract.FileValues? files = null;
        try
        {
            cancellation.ThrowIfCancellationRequested();
            var sdk = await Run("sdk", ["--version"]);
            Need(sdk.StandardOutput.Trim() == NativeAotPayload.SdkVersion, "sdk_pin");
            foreach (string directory in new[]
            {
                appDirectory,
                oldDirectory
            }

            )
            {
                string name = Path.GetFileName(directory), project = Path.Combine(directory, name + ".csproj");
                await Run(name + "-restore", ["restore", project, "--nologo"]);
                await Run(name + "-build", ["build", project, "--no-restore", "--no-incremental", "--nologo", "-m:1", "-p:UseSharedCompilation=false"]);
                string assetsPath = Path.Combine(directory, "obj", "project.assets.json");
                var assets = held.Open(root, assetsPath, "managed HTTP assets", "log_upload_http_assets", 32 * 1024 * 1024, singleLink: true);
                inputs.Add(assets);
                using (var parsed = JsonDocument.Parse(assets.ReadBytes(32 * 1024 * 1024, "log_upload_http_assets")))
                {
                    string[] libraries = parsed.RootElement.GetProperty("libraries").EnumerateObject().Select(row => row.Name).Order(StringComparer.Ordinal).ToArray();
                    Need(libraries.All(library => !library.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) && !library.Contains("EntityFramework", StringComparison.OrdinalIgnoreCase)), "foreign_dependency");
                    await Save(destination, name + "-dependencies.json", JsonSerializer.Serialize(libraries));
                }

                string bin = Path.Combine(directory, "bin", "Debug", NativeAotPayload.TargetFramework);
                string[] built = Directory.GetFiles(bin, "*", SearchOption.AllDirectories);
                Need(built.Length is > 0 and <= 64, "built_capacity");
                inputs.AddRange(built.Select(path => held.Open(root, path, "managed HTTP built output", "log_upload_http_built", 64 * 1024 * 1024, allowEmpty: true, singleLink: true)));
                using var runtime = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(bin, name + ".runtimeconfig.json")));
                var options = runtime.RootElement.GetProperty("runtimeOptions");
                Need(options.GetProperty("tfm").GetString() == NativeAotPayload.TargetFramework && options.GetProperty("rollForward").GetString() == "Disable", "runtime_pin");
                JsonElement[] frameworks = options.TryGetProperty("frameworks", out var multiple) ? multiple.EnumerateArray().ToArray() : [options.GetProperty("framework")];
                Need(frameworks.Select(frame => frame.GetProperty("name").GetString()).Order(StringComparer.Ordinal).SequenceEqual((name == Application ? new[] { "Microsoft.AspNetCore.App", "Microsoft.NETCore.App" } : new[] { "Microsoft.NETCore.App" }).Order(StringComparer.Ordinal)) && frameworks.All(frame => frame.GetProperty("version").GetString() == NativeAotPayload.RuntimeVersion), "runtime_pin");
            }

            string server = Path.Combine(appDirectory, "bin", "Debug", NativeAotPayload.TargetFramework, Application + ".dll");
            foreach (string control in new[]
            {
                "missing-upload",
                "nonempty-log",
                "nonempty-upload",
                "cancel-body",
                "io-open-denied",
                "log-append-denied"
            }

            )
                controls.Add(await Session(control, server));
            controls.Add(await Session("positive", server));
            string positive = Path.Combine(destination, "positive", "data");
            // These handles are acquired only after the server is gone. They
            // cannot manufacture its positive writes or failed-write controls.
            var log = held.Open(root, Path.Combine(positive, NativeAotLogUploadContract.LogName), "original managed HTTP log", "log_upload_http_log", 4096, singleLink: true);
            var upload = held.Open(root, Path.Combine(positive, NativeAotLogUploadContract.UploadName), "original managed HTTP upload", "log_upload_http_upload", 5126, singleLink: true);
            var guard = held.Open(root, Path.Combine(positive, "GUARD.BIN"), "managed HTTP guard", "log_upload_http_guard", 64, singleLink: true);
            AuthorityFiles.RequireDistinct(log, upload, guard);
            files = NativeAotLogUploadContract.InspectFileValues(log.ReadBytes(4096, "log_upload_http_log"), upload.ReadBytes(5126, "log_upload_http_upload"));
            Need(guard.ReadBytes(64, "log_upload_http_guard").AsSpan().SequenceEqual(Guard), "guard_changed");
            string verifier = Path.Combine(oldDirectory, "bin", "Debug", NativeAotPayload.TargetFramework, NativeAotLogUploadContract.Application + ".dll");
            var verified = await Run("independent-console-verify", [verifier, "--verify", "--root", positive]);
            Need(verified.StandardError.Length == 0, "verifier_stderr");
            Need(NativeAotLogUploadContract.InspectTranscriptValues(verified.StandardOutput, "verify").ExpectedFiles == files.Value, "verifier_files");
            await Save(destination, "inputs.json", JsonSerializer.Serialize(inputs.Select(input => new { input.Path, input.Length, input.Sha256 })));
            held.VerifyUnchanged();
            NativeAotLogUploadContract.RequireCurrentSources(root);
            Need(ToolingBuildInputs.Validate(root).Count == 0, "tooling_inputs_changed");
            complete = true;
        }
        catch (Exception error)
        {
            failure = error is ToolFailure tool ? tool.Reason : error.GetType().Name;
            throw;
        }
        finally
        {
            await Save(destination, "commands.json", JsonSerializer.Serialize(commands));
            await Save(destination, "receipt.json", JsonSerializer.Serialize(new
            {
                Schema = "CLROS_LOG_UPLOAD_HTTP_MANAGED_REFERENCE_V1",
                Complete = complete,
                Failure = failure,
                Files = files,
                Responses = responses,
                Controls = controls,
                FlushRequested = true,
                DeviceFlushObserved = false,
                LinuxNativeAotQualified = false,
                GuestQualified = false,
                ColdRebootQualified = false,
                PhysicalPersistenceQualified = false
            }));
        }

        return new(destination, files!.Value, responses, controls);
        async Task<ProcessResult> Run(string phase, string[] arguments)
        {
            commands.Add(new
            {
                Phase = phase,
                Executable = "dotnet",
                Arguments = arguments,
                WorkingDirectory = root
            });
            var result = await ProcessRunner.RunAsync(new("dotnet", arguments, root)
            {
                Timeout = TimeSpan.FromMinutes(3),
                OutputCharacterLimit = 4 * 1024 * 1024
            }, cancellation);
            await Save(destination, phase + ".stdout.log", result.StandardOutput);
            await Save(destination, phase + ".stderr.log", result.StandardError);
            await Save(destination, phase + ".process.json", JsonSerializer.Serialize(result));
            QemuTranscripts.RequireCompleteProcess(result, "log_upload_http_process_incomplete");
            Need(result.Succeeded, "process_" + phase);
            return result;
        }

        async Task<Control> Session(string name, string server)
        {
            string directory = Path.Combine(destination, name), data = Path.Combine(directory, "data");
            Directory.CreateDirectory(data);
            string log = Path.Combine(data, NativeAotLogUploadContract.LogName), upload = Path.Combine(data, NativeAotLogUploadContract.UploadName);
            await File.WriteAllBytesAsync(log, name == "nonempty-log" ? [0x71] : [], cancellation);
            if (name != "missing-upload")
                await File.WriteAllBytesAsync(upload, name == "nonempty-upload" ? [0x72] : [], cancellation);
            await File.WriteAllBytesAsync(Path.Combine(data, "GUARD.BIN"), Guard, cancellation);
            string before = Snapshot(data);
            int port = ReservePort();
            using var owner = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            StringBuilder observed = new();
            var arguments = new[]
            {
                server,
                "--serve-once",
                "--root",
                data,
                "--urls",
                "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture)
            };
            commands.Add(new
            {
                Phase = name,
                Executable = "dotnet",
                Arguments = arguments,
                WorkingDirectory = root
            });
            Task<ProcessResult> child = ProcessRunner.RunAsync(new("dotnet", arguments, root)
            {
                Timeout = TimeSpan.FromSeconds(90),
                OutputCharacterLimit = 65536,
                OutputReceived = (text, error) =>
            {
                if (!error)
                    lock (observed)
                    {
                        if (observed.Length + text.Length <= MaximumTranscriptBytes)
                            observed.Append(text);
                        if (observed.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Contains(Ready, StringComparison.Ordinal))
                            ready.TrySetResult();
                    }
            }
            }, owner.Token);
            bool succeeded = false, refused = false, joined = false;
            string sessionFailure = "", startup = "", snapshotFailure = "";
            ProcessResult? result = null;
            List<ResponseFact> sessionResponses = [];
            try
            {
                if (name is "missing-upload" or "nonempty-log" or "nonempty-upload")
                {
                    result = await child;
                    QemuTranscripts.RequireCompleteProcess(result, "log_upload_http_preflight_incomplete");
                    string expected = "TEST_START m10.027.http\nTEST_FAIL m10.027.http " + (name == "missing-upload" ? "FileNotFoundException" : "InvalidOperationException") + "\n";
                    Need(result.ExitCode == 1 && result.StandardError.Length == 0 && result.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal) == expected, "preflight_control");
                    Need(Snapshot(data) == before, "preflight_modified");
                }
                else
                {
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                    deadline.CancelAfter(TimeSpan.FromSeconds(40));
                    var first = await Task.WhenAny(ready.Task, child).WaitAsync(deadline.Token);
                    Need(first == ready.Task, "exited_before_ready");
                    await ready.Task;
                    using HttpClient client = Client(port);
                    await WaitPhase(client, "ready", deadline.Token);
                    startup = Snapshot(data);
                    RequireStartup(data);
                    if (name == "cancel-body")
                    {
                        using var partial = await Partial(port, deadline.Token);
                        await WaitPhase(client, "receiving", deadline.Token);
                        Need(Snapshot(data) == startup, "partial_modified");
                        owner.Cancel();
                        result = await child;
                        Need(result.Cancelled && result.TerminationConfirmed && !result.TimedOut && !result.OutputLimitExceeded && result.FailureReason.Length == 0, "cancel_control");
                        Need(Snapshot(data) == startup, "cancel_modified");
                    }
                    else if (name is "io-open-denied" or "log-append-denied")
                    {
                        using (var deny = new FileStream(name == "io-open-denied" ? upload : log, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            using var request = new HttpRequestMessage(HttpMethod.Post, "/upload")
                            {
                                Content = Content(NativeAotLogUploadContract.ExpectedUpload())
                            };
                            using var response = await client.SendAsync(request, deadline.Token);
                            Need((int)response.StatusCode == 500 && (await response.Content.ReadAsByteArrayAsync(deadline.Token)).Length == 0, "io_failure_control");
                        }

                        result = await child;
                        QemuTranscripts.RequireCompleteProcess(result, "log_upload_http_io_incomplete");
                        Need(result.ExitCode == 1 && result.StandardError.Length == 0 && !result.StandardOutput.Contains("TEST_PASS", StringComparison.Ordinal), "io_failure_control");
                        if (name == "io-open-denied")
                            Need(Snapshot(data) == startup, "denied_open_modified");
                        else
                        {
                            // The upload was already written/flushed/closed
                            // when the later log open failed. Preserve it.
                            Need(File.ReadAllBytes(log).AsSpan().SequenceEqual(NativeAotLogUploadContract.ExpectedLog().AsSpan(0, StartupLogBytes)) && File.ReadAllBytes(upload).AsSpan().SequenceEqual(NativeAotLogUploadContract.ExpectedUpload()) && File.ReadAllBytes(Path.Combine(data, "GUARD.BIN")).AsSpan().SequenceEqual(Guard), "partial_effects_control");
                        }
                    }
                    else
                    {
                        await Request(ResponseKind.Health);
                        await Request(ResponseKind.StartupLog);
                        await Request(ResponseKind.WrongType, new byte[5126], "text/plain");
                        await Request(ResponseKind.WrongLength, [0x01]);
                        await Request(ResponseKind.Oversized, new byte[5127]);
                        Need(Snapshot(data) == startup, "bad_request_modified");
                        using (var partial = await Partial(port, deadline.Token))
                        {
                            await WaitPhase(client, "receiving", deadline.Token);
                            await Request(ResponseKind.Busy, NativeAotLogUploadContract.ExpectedUpload());
                            Need(Snapshot(data) == startup, "busy_modified");
                        }

                        await WaitPhase(client, "ready", deadline.Token);
                        Need(Snapshot(data) == startup, "abort_modified");
                        await Save(directory, "pre-request.json", startup);
                        await Request(ResponseKind.Accepted, NativeAotLogUploadContract.ExpectedUpload());
                        NativeAotLogUploadContract.InspectFileValues(File.ReadAllBytes(log), File.ReadAllBytes(upload));
                        string accepted = Snapshot(data);
                        await Request(ResponseKind.Busy, NativeAotLogUploadContract.ExpectedUpload());
                        Need(Snapshot(data) == accepted, "duplicate_modified");
                        await Request(ResponseKind.Log);
                        await Request(ResponseKind.Upload);
                        result = await child;
                        QemuTranscripts.RequireCompleteProcess(result, "log_upload_http_server_incomplete");
                        Need(result.Succeeded, "server_exit");
                        InspectServerTranscriptValues(result.StandardOutput, result.StandardError);
                        Need(File.ReadAllBytes(Path.Combine(data, "GUARD.BIN")).AsSpan().SequenceEqual(Guard), "guard_changed");
                        responses.AddRange(sessionResponses);
                        succeeded = true;
                    }

                    async Task Request(ResponseKind kind, byte[]? bytes = null, string type = "application/octet-stream")
                    {
                        string method = kind is ResponseKind.Health or ResponseKind.StartupLog or ResponseKind.Log or ResponseKind.Upload ? "GET" : "POST";
                        string target = kind == ResponseKind.Health ? "/healthz" : kind is ResponseKind.StartupLog or ResponseKind.Log ? "/log" : "/upload";
                        using var request = new HttpRequestMessage(new HttpMethod(method), target);
                        if (bytes is not null)
                        {
                            request.Content = new ByteArrayContent(bytes);
                            request.Content.Headers.ContentType = new(type);
                        }

                        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
                        byte[] body = await ReadBounded(response, deadline.Token);
                        string contentType = response.Content.Headers.ContentType?.ToString() ?? "";
                        var fact = InspectResponseValues(kind, method, target, (int)response.StatusCode, contentType, body);
                        sessionResponses.Add(fact);
                        string ordinal = sessionResponses.Count.ToString("D2", CultureInfo.InvariantCulture);
                        await File.WriteAllBytesAsync(Path.Combine(directory, ordinal + ".body"), body, CancellationToken.None);
                    }
                }

                await Refused(port, cancellation);
                refused = true;
            }
            catch (Exception error)
            {
                sessionFailure = error is ToolFailure tool ? tool.Reason : error.GetType().Name;
                throw;
            }
            finally
            {
                owner.Cancel();
                try
                {
                    result = await child;
                    joined = result.TerminationConfirmed;
                }
                finally
                {
                    if (result is not null)
                    {
                        await Save(directory, "server.stdout.log", result.StandardOutput);
                        await Save(directory, "server.stderr.log", result.StandardError);
                        await Save(directory, "server.process.json", JsonSerializer.Serialize(result));
                    }

                    string actual = "";
                    try
                    {
                        actual = Snapshot(data);
                    }
                    catch (Exception error)
                    {
                        snapshotFailure = error is ToolFailure tool ? tool.Reason : error.GetType().Name;
                    }

                    await Save(directory, "http.json", JsonSerializer.Serialize(new
                    {
                        Schema = "CLROS_LOG_UPLOAD_HTTP_SESSION_V1",
                        Name = name,
                        Complete = succeeded && snapshotFailure.Length == 0,
                        ExpectedFailure = name != "positive",
                        Failure = sessionFailure,
                        Joined = joined,
                        EndpointRefused = refused,
                        Port = port,
                        Server = result,
                        Responses = sessionResponses,
                        BeforeFilesJson = before,
                        PreRequestFilesJson = startup,
                        ActualFilesJson = actual,
                        SnapshotFailure = snapshotFailure,
                        DeviceFlushObserved = false,
                        LinuxNativeAotQualified = false,
                        GuestQualified = false,
                        PhysicalPersistenceQualified = false
                    }));
                }
            }

            Need(joined, "server_not_joined");
            Need(snapshotFailure.Length == 0, "session_snapshot_failed");
            return new(name, name != "positive", joined, result!.Cancelled, result.ExitCode, refused);
        }
    }

}
