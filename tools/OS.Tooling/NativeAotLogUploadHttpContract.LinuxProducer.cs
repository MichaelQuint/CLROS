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
    /// <summary>Root-serialized Linux reference only. Freshly publish this exact
    /// HTTP app and compose the unchanged console's fresh LinuxReference in a
    /// separate namespace. Keep that live owner and original source/compiler/
    /// runtime/publication handles through an additional --verify of HTTP data.
    /// Own each setsid/strace session, join capture/group cleanup and retain raw
    /// write-trace/verify-trace files, including failures and incomplete lexical
    /// projection. Portable controls omit Windows sharing-denial and physical
    /// I/O-error injection. Positive completion requires natural exit0, exact
    /// delivered/file bytes, refused endpoint and the distinct verifier. Failed
    /// receipts retain actual partial state without asserting rollback. Existing
    /// HTTP sessions and the additional verifier have90-second limits. The
    /// composed console owner retains its existing2-minute trace and45-minute
    /// publication limits; cancellation is checked between publisher calls.
    /// Bounded fd censuses are observed boundary counts, never a peak/fit proof;
    /// this owner neither changes descriptor limits nor drops retained inputs.
    /// No CLROS, syscall closure, device-FLUSH, recovery or persistence authority.
    /// Caller must hold the external exclusive Linux-builder lease.</summary>

    public static async Task<LinuxReference> RunLinuxReferenceAsync(string root, string recipeRoot, string destination, TextWriter output, CancellationToken cancellation = default)
    {
        Need(OperatingSystem.IsLinux(), "linux_builder_required");
        cancellation.ThrowIfCancellationRequested();
        root = Path.GetFullPath(root);
        recipeRoot = Path.GetFullPath(recipeRoot);
        destination = Path.GetFullPath(destination, root);
        Need(root != recipeRoot, "original_recipe_required");
        RequireFreshOutput(root, destination);
        RequireCurrentSources(root, fresh: true);
        NativeAotLogUploadContract.RequireCurrentSources(root);
        Need(ToolingBuildInputs.Validate(root).Count == 0, "tooling_inputs_changed");
        const string marker = "nativeaot.log-upload-http-reference";
        output.WriteLine("TEST_START " + marker);
        string[] SourcePaths() => ToolingBuildInputs.SourceInputs(root).Concat(new[] { Path.Combine(root, "apps", Application, "Program.cs"), Path.Combine(root, "apps", Application, Application + ".csproj") }).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        Dictionary<string, string> Sources() => SourcePaths().ToDictionary(path => Path.GetRelativePath(root, path).Replace('\\', '/'), NativeAotWebPayload.HashFile, StringComparer.Ordinal);
        var before = Sources();
        Need(before.Count is > 0 and <= 8192, "source_capacity");
        AuthorityFiles held = new();
        NativeAotLogUploadContract.LinuxReference? verifier = null;
        bool transferred = false;
        List<object> commands = [], fdBoundaries = [];
        List<Control> controls = [];
        List<ResponseFact> responses = [];
        Dictionary<string, string> native = new(StringComparer.Ordinal);
        bool complete = false;
        string failure = "";
        NativeAotPayload.Inspection? image = null;
        NativeAotLogUploadContract.FileValues? files = null;
        NativeAotLogUploadContract.TraceValues? writeTrace = null, verifyTrace = null;
        string strace = "", originalNative = "";
        ReadOnlyCollection<LinuxToolValue> selectedTools = Array.AsReadOnly(Array.Empty<LinuxToolValue>());
        FileStream? sourceLease = null, recipeLease = null;
        try
        {
            foreach (string path in SourcePaths())
                held.Open(root, path, "original compiled HTTP producer source", "log_upload_http_linux_source", 16 * 1024 * 1024, singleLink: true);
            foreach (string path in new[]
            {
                Path.Combine(root, "global.json"),
                Path.Combine(root, "Directory.Build.props"),
                Path.Combine(root, "Directory.Build.targets"),
                Path.Combine(root, "tools", "Directory.Build.props"),
                Path.Combine(root, "apps", Application, "Program.cs"),
                Path.Combine(root, "apps", Application, Application + ".csproj"),
                Path.Combine(root, "apps", NativeAotLogUploadContract.Application, "Program.cs"),
                Path.Combine(root, "apps", NativeAotLogUploadContract.Application, NativeAotLogUploadContract.Application + ".csproj")
            }

            )
                held.Open(root, path, "original HTTP reference source", "log_upload_http_linux_source", 16 * 1024 * 1024, singleLink: true);
            string producer = Path.GetDirectoryName(typeof(NativeAotLogUploadHttpContract).Assembly.Location)!;
            string[] producerFiles = Directory.GetFiles(producer, "*.dll").Concat(Directory.GetFiles(producer, "*.deps.json")).Concat(Directory.GetFiles(producer, "*.runtimeconfig.json")).Order(StringComparer.Ordinal).ToArray();
            Need(producerFiles.Length is > 0 and <= 64, "producer_capacity");
            foreach (string path in producerFiles)
                held.Open(Path.GetPathRoot(path)!, path, "original HTTP producer closure", "log_upload_http_linux_producer", 64 * 1024 * 1024, singleLink: true);
            string recipe = Path.Combine(recipeRoot, "artifacts", "socket-qualification-r01");
            foreach (string name in new[]
            {
                "commands.json",
                "native-inputs.json",
                "receipt.json"
            }

            )
                held.Open(recipeRoot, Path.Combine(recipe, name), "original socket recipe", "log_upload_http_linux_recipe", 16 * 1024 * 1024, singleLink: true);
            // This existing validator authenticates E260, all32 retained
            // outputs, the three relocated producer outputs and original ELF.
            // Historical absolute native-map rows are not current filenames.
            _ = NativeAotSocketPackage.Create(root, recipeRoot, asynchronous: true);
            originalNative = held.Open(recipeRoot, Path.Combine(recipe, "native-inputs.json"), "original recipe native inputs", "log_upload_http_linux_recipe", 16 * 1024 * 1024, singleLink: true).ReadUtf8(16 * 1024 * 1024, "log_upload_http_linux_recipe");
            selectedTools = InspectLinuxRecipeTools(originalNative, LinuxToolNames.ToDictionary(name => name, name =>
            {
                string path = ResolveToolPath(name);
                return new LinuxToolValue(name, path, NativeAotWebPayload.HashFile(path));
            }, StringComparer.Ordinal));
            foreach (var tool in selectedTools)
                HoldNative(tool.Path, tool.Sha256);
            strace = selectedTools.Single(tool => tool.Name == "strace").Path;
            // Reject an unsupported provider before publication/tool execution.
            _ = TracePlan(["--version"]);
            Directory.CreateDirectory(destination);
            AuthorityFiles.RequirePlainPath(root, destination, "log_upload_http_linux_output");
            using var ownership = new FileStream(Path.Combine(destination, "ownership.lock"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
            try
            {
                // The later nested console publication cannot retroactively
                // authenticate the system archives used by this first link.
                string gcc = selectedTools.Single(tool => tool.Name == "gcc").Path;
                foreach (string library in LinuxLibraryNames)
                {
                    cancellation.ThrowIfCancellationRequested();
                    CheckInputs();
                    string queryLog = "native-library-" + library;
                    // ExecuteAsync uses its existing logical setsid wrapper;
                    // this is distinct from the pinned physical trace plan.
                    commands.Add(new
                    {
                        Executable = "setsid",
                        Arguments = new[] { gcc, "-print-file-name=" + library },
                        WorkingDirectory = root,
                        Phase = "HTTP static-link discovery"
                    });
                    bool succeeded = false;
                    string queryFailure = "";
                    try
                    {
                        cancellation.ThrowIfCancellationRequested();
                        await NativeAotWebPayload.ExecuteAsync(gcc, ["-print-file-name=" + library], root, Path.Combine(destination, queryLog), TimeSpan.FromMinutes(2));
                        succeeded = true;
                    }
                    catch (Exception error)
                    {
                        queryFailure = error.Message.ReplaceLineEndings(" ");
                        throw;
                    }
                    finally
                    {
                        await Save(destination, queryLog + ".process.json", JsonSerializer.Serialize(new
                        {
                            NormalOwnedSessionReturned = succeeded,
                            Executable = gcc,
                            Arguments = new[] { "-print-file-name=" + library },
                            MaximumSeconds = 120,
                            Failure = queryFailure
                        }));
                    }

                    var answer = held.Open(root, Path.Combine(destination, queryLog + ".stdout.log"), "original HTTP static library query", "log_upload_http_linux_library", 4096, singleLink: true);
                    var errors = held.Open(root, Path.Combine(destination, queryLog + ".stderr.log"), "original HTTP static library query stderr", "log_upload_http_linux_library", 0, allowEmpty: true, singleLink: true);
                    string path = InspectLinuxLibraryPath(library, answer.ReadUtf8(4096, "log_upload_http_linux_library"));
                    Need(File.Exists(path) && errors.Length == 0, "static_library_missing");
                    path = File.ResolveLinkTarget(path, true)?.FullName ?? Path.GetFullPath(path);
                    HoldNative(path, NativeAotWebPayload.HashFile(path));
                    CheckInputs();
                }

                CheckInputs();
                cancellation.ThrowIfCancellationRequested();
                ObserveDescriptors("before-http-publication");
                string publication = Path.Combine(destination, "http-publication");
                commands.Add(new
                {
                    Producer = nameof(NativeAotSocketFixturePublisher),
                    Application,
                    RecipeRoot = recipeRoot,
                    Output = publication
                });
                Need(await Publisher.RunAsync(root, ["publish", "--recipe-root", recipeRoot, "--output", publication], output) == 0, "linux_publication_failed");
                cancellation.ThrowIfCancellationRequested();
                CheckInputs();
                string[] publishedFiles = Directory.GetFiles(publication, "*", SearchOption.AllDirectories).Where(path => Path.GetFileName(path) != "publication.lock").Order(StringComparer.Ordinal).ToArray();
                Need(publishedFiles.Length is > 0 and <= 512, "publication_capacity");
                foreach (string path in publishedFiles)
                    held.Open(root, path, "original HTTP publication", "log_upload_http_linux_publication", 256 * 1024 * 1024, allowEmpty: true, singleLink: true);
                var publicationFile = held.Open(root, Path.Combine(publication, "publication.json"), "original HTTP publication receipt", "log_upload_http_linux_publication", 16 * 1024 * 1024, singleLink: true);
                string publicationJson = publicationFile.ReadUtf8(16 * 1024 * 1024, "log_upload_http_linux_publication");
                var values = InspectLinuxPublicationValues(publicationJson, before);
                using (var parsed = JsonDocument.Parse(publicationJson))
                {
                    foreach (var row in parsed.RootElement.GetProperty("native").EnumerateObject())
                        HoldNative(row.Name, row.Value.GetString()!, allowEmpty: true);
                    var outputs = parsed.RootElement.GetProperty("outputs").EnumerateObject().ToArray();
                    Need(outputs.Select(row => row.Name).Order(StringComparer.Ordinal).SequenceEqual(publishedFiles.Where(path => Path.GetFileName(path) != "publication.json").Select(path => Path.GetRelativePath(publication, path).Replace('\\', '/')).Order(StringComparer.Ordinal)), "publication_output_inventory");
                    foreach (var row in outputs)
                        Need(held.Open(root, Path.Combine(publication, row.Name), "original HTTP output", "log_upload_http_linux_publication", 256 * 1024 * 1024, allowEmpty: true, singleLink: true).Sha256.Equals(row.Value.GetString(), StringComparison.OrdinalIgnoreCase), "publication_output_changed");
                }

                string executable = Path.Combine(publication, Application, Application);
                var elf = held.Open(root, executable, "original HTTP native executable", "log_upload_http_linux_elf", NativeAotPayload.MaximumImageBytes, singleLink: true);
                image = NativeAotPayload.Inspect(elf.ReadBytes(NativeAotPayload.MaximumImageBytes, "log_upload_http_linux_elf"));
                Need(image.Sha256.Equals(values.ImageSha256, StringComparison.OrdinalIgnoreCase), "publication_image");
                ObserveDescriptors("after-http-publication-holds");
                // A live original owner is composed, not reconstructed from a
                // copied old receipt or ELF. Its own fresh write/verify data is
                // separate from the HTTP app's data and proves no HTTP effect.
                commands.Add(new
                {
                    Producer = nameof(NativeAotLogUploadContract),
                    Output = Path.Combine(destination, "verifier-reference")
                });
                verifier = await NativeAotLogUploadContract.RunLinuxReferenceAsync(root, recipeRoot, Path.Combine(destination, "verifier-reference"), output, cancellation);
                sourceLease = NativeAotPayload.OpenQualificationLease(root);
                recipeLease = NativeAotPayload.OpenQualificationLease(recipeRoot);
                // Reuse the original selected roles and their live binary holds.
                // A later path merely appearing in native cannot replace a role.
                CheckInputs();
                ObserveDescriptors("after-console-reference-holds");
                foreach (string name in new[]
                {
                    "missing-upload",
                    "nonempty-log",
                    "nonempty-upload",
                    "cancel-body",
                    "positive"
                }

                )
                {
                    var session = await Session(name, executable);
                    controls.Add(session.Control);
                    if (name == "positive")
                        writeTrace = session.Trace;
                }

                string data = Path.Combine(destination, "positive", "data");
                var log = held.Open(root, Path.Combine(data, NativeAotLogUploadContract.LogName), "original HTTP log", "log_upload_http_linux_log", 4096, singleLink: true);
                var upload = held.Open(root, Path.Combine(data, NativeAotLogUploadContract.UploadName), "original HTTP upload", "log_upload_http_linux_upload", 5126, singleLink: true);
                var guard = held.Open(root, Path.Combine(data, "GUARD.BIN"), "original HTTP guard", "log_upload_http_linux_guard", 64, singleLink: true);
                AuthorityFiles.RequireDistinct(log, upload, guard, elf);
                files = NativeAotLogUploadContract.InspectFileValues(log.ReadBytes(4096, "log_upload_http_linux_log"), upload.ReadBytes(5126, "log_upload_http_linux_upload"));
                Need(guard.ReadBytes(64, "log_upload_http_linux_guard").AsSpan().SequenceEqual(Guard), "guard_changed");
                string verifyExecutable = Path.Combine(verifier.Directory, "publication", NativeAotLogUploadContract.Application, NativeAotLogUploadContract.Application);
                var verifyElf = held.Open(root, verifyExecutable, "original unchanged console verifier", "log_upload_http_linux_verifier", NativeAotPayload.MaximumImageBytes, singleLink: true);
                Need(verifyElf.Sha256.Equals(verifier.Image.Sha256, StringComparison.OrdinalIgnoreCase), "verifier_identity");
                string[] verifyArguments = NativeAotLogUploadContract.LinuxTraceArguments(verifyExecutable, data, Path.Combine(destination, "verify-trace"), "verify");
                LinuxTracePlan verifyPlan = TracePlan(verifyArguments);
                commands.Add(new
                {
                    verifyPlan.Executable,
                    verifyPlan.Arguments,
                    WorkingDirectory = root
                });
                bool verifyJoined = false, verifyNatural = false;
                int? verifyExit = null;
                ObserveDescriptors("before-additional-verifier");
                cancellation.ThrowIfCancellationRequested();
                using (var verifyProcess = StartTrace(verifyPlan))
                using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
                {
                    deadline.CancelAfter(TimeSpan.FromSeconds(90));
                    Task verifyStdout = NativeAotWebPayload.CaptureAsync(verifyProcess.StandardOutput, Path.Combine(destination, "verify.stdout.log"), verifyProcess);
                    Task verifyStderr = NativeAotWebPayload.CaptureAsync(verifyProcess.StandardError, Path.Combine(destination, "verify.stderr.log"), verifyProcess);
                    try
                    {
                        await Task.WhenAll(verifyProcess.WaitForExitAsync(deadline.Token), verifyStdout, verifyStderr).WaitAsync(deadline.Token);
                        verifyExit = verifyProcess.ExitCode;
                        verifyNatural = true;
                        Need(verifyExit == 0, "linux_verifier_exit");
                    }
                    finally
                    {
                        try
                        {
                            await NativeAotWebPayload.StopSessionAsync(verifyProcess);
                            verifyJoined = true;
                        }
                        finally
                        {
                            try
                            {
                                await Task.WhenAll(verifyStdout, verifyStderr).WaitAsync(TimeSpan.FromSeconds(10));
                            }
                            finally
                            {
                                await Save(destination, "verify-process.json", JsonSerializer.Serialize(new
                                {
                                    Joined = verifyJoined,
                                    NaturalExitObserved = verifyNatural,
                                    ExitCode = verifyExit
                                }));
                            }
                        }
                    }
                }

                var verifyOut = held.Open(root, Path.Combine(destination, "verify.stdout.log"), "original independent verifier stdout", "log_upload_http_linux_verify", MaximumTranscriptBytes, singleLink: true);
                var verifyError = held.Open(root, Path.Combine(destination, "verify.stderr.log"), "original independent verifier stderr", "log_upload_http_linux_verify", 0, allowEmpty: true, singleLink: true);
                Need(NativeAotLogUploadContract.InspectTranscriptValues(verifyOut.ReadUtf8(MaximumTranscriptBytes, "log_upload_http_linux_verify"), "verify").ExpectedFiles == files.Value && verifyError.Length == 0, "verifier_files");
                verifyTrace = ReadTraces(destination, "verify");
                Need(verifyTrace.ExitedFiles == verifyTrace.Files && verifyTrace.Rows.Any(row => row.Syscall == "execve") && verifyTrace.Rows.Any(row => row.Syscall == "exit_group"), "verify_trace_exit");
                InspectLinuxExecveValues(verifyTrace, verifyExecutable, ["--verify", "--root", data]);
                ObserveDescriptors("after-additional-verifier-join");
                await Save(destination, "verify-trace-values.json", JsonSerializer.Serialize(verifyTrace));
                CheckInputs();
                complete = true;
            }
            catch (Exception error)
            {
                failure = error is ToolFailure tool ? tool.Reason : error.Message.ReplaceLineEndings(" ");
                throw;
            }
            finally
            {
                await Save(destination, "commands.json", JsonSerializer.Serialize(commands));
                await Save(destination, "sources.json", JsonSerializer.Serialize(before));
                await Save(destination, "native-inputs.json", JsonSerializer.Serialize(native));
                ObserveDescriptors("before-final-receipt");
                await Save(destination, "fd-boundaries.json", JsonSerializer.Serialize(new
                {
                    ObservedBoundaries = fdBoundaries,
                    PeakOrCapacityProof = false,
                    DescriptorLimitsChanged = false
                }));
                string[] outputs = OutputPaths();
                await Save(destination, "receipt.json", JsonSerializer.Serialize(new
                {
                    Schema = "CLROS_LOG_UPLOAD_HTTP_LINUX_REFERENCE_V1",
                    Complete = complete,
                    Failure = failure,
                    HttpImage = image,
                    VerifierImage = verifier?.Image,
                    ConsoleReferenceDirectory = verifier?.Directory,
                    Files = files,
                    Responses = responses,
                    Controls = controls,
                    WriteTraceProjectionComplete = writeTrace?.CompleteTraceProjection,
                    VerifyTraceProjectionComplete = verifyTrace?.CompleteTraceProjection,
                    Sources = before,
                    Native = native,
                    Outputs = outputs.ToDictionary(path => Path.GetRelativePath(destination, path).Replace('\\', '/'), NativeAotWebPayload.HashFile, StringComparer.Ordinal),
                    WindowsSharingDenialControlsRun = false,
                    LinuxPhysicalIoErrorInjectionQualified = false,
                    SyscallClosureQualified = false,
                    CallerCancellationRequested = cancellation.IsCancellationRequested,
                    DeviceFlushObserved = false,
                    GuestQualified = false,
                    ColdRebootQualified = false,
                    PhysicalPersistenceQualified = false
                }));
            }

            string[] artifacts = OutputPaths();
            Need(artifacts.Length is > 0 and <= 2048, "artifact_capacity");
            var originals = artifacts.Select(path => held.Open(root, path, "original HTTP reference evidence", "log_upload_http_linux_evidence", 256 * 1024 * 1024, allowEmpty: true, singleLink: true)).ToArray();
            AuthorityFiles.RequireDistinct(originals);
            CheckInputs();
            // This last count is written only to the caller-owned producer
            // output: all evidence files are already immutable and held.
            ObserveDescriptors("after-final-output-holds");
            var result = new LinuxReference(destination, image!, verifier!, files!.Value, responses, writeTrace!, verifyTrace!, held, originals.Select(file => new NativeAotLogUploadContract.ArtifactIdentity(file.Path, file.Length, file.Sha256)));
            transferred = true;
            output.WriteLine("TEST_PASS " + marker);
            return result;
        }
        catch (Exception error)
        {
            // A final evidence/identity check can fail after the provisional
            // receipt was written. Preserve those exact bytes, then revoke its
            // Complete flag before throwing. No live process is represented by
            // these evidence handles, and no result is transferred on failure.
            string receipt = Path.Combine(destination, "receipt.json");
            if (complete && File.Exists(receipt))
            {
                held.Dispose();
                verifier?.Dispose();
                try
                {
                    string previous = Path.Combine(destination, "receipt-before-late-failure.json");
                    File.Copy(receipt, previous);
                    var failed = JsonNode.Parse(await File.ReadAllTextAsync(previous, CancellationToken.None))!.AsObject();
                    failed["Complete"] = false;
                    failed["Failure"] = error is ToolFailure tool ? tool.Reason : error.GetType().Name;
                    failed["LateEvidenceFailure"] = true;
                    failed["PreviousReceipt"] = Path.GetFileName(previous);
                    failed["PreviousReceiptSha256"] = NativeAotWebPayload.HashFile(previous);
                    await File.WriteAllTextAsync(receipt, failed.ToJsonString(), CancellationToken.None);
                }
                catch (Exception retainedFailure)
                {
                    throw new AggregateException(error, retainedFailure);
                }
            }

            throw;
        }
        finally
        {
            recipeLease?.Dispose();
            sourceLease?.Dispose();
            if (!transferred)
            {
                held.Dispose();
                verifier?.Dispose();
            }
        }

        string[] OutputPaths() => Directory.GetFiles(destination, "*", SearchOption.AllDirectories).Where(path => Path.GetFileName(path) is not ("ownership.lock" or "publication.lock")).Order(StringComparer.Ordinal).ToArray();
        void HoldNative(string path, string hash, bool allowEmpty = false)
        {
            Need(CanonicalLinux(path) && IsHash(hash) && (native.Count < 16384 || native.ContainsKey(path)), "native_identity");
            Need(!native.TryGetValue(path, out string? prior) || prior.Equals(hash, StringComparison.OrdinalIgnoreCase), "native_identity_changed");
            RetainCurrentNativeInput(held, path, hash, allowEmpty);
            native[path] = hash;
        }

        void CheckInputs()
        {
            RequireCurrentSources(root);
            NativeAotLogUploadContract.RequireCurrentSources(root);
            Need(ToolingBuildInputs.Validate(root).Count == 0 && before.OrderBy(row => row.Key, StringComparer.Ordinal).SequenceEqual(Sources().OrderBy(row => row.Key, StringComparer.Ordinal)), "linux_sources_changed");
            verifier?.VerifyUnchanged();
            held.VerifyUnchanged();
        }

        void ObserveDescriptors(string phase)
        {
            Need(fdBoundaries.Count < 32, "fd_observation_capacity");
            int? count = null;
            string? censusFailure = null;
            try
            {
                int observed = 0;
                foreach (string _ in Directory.EnumerateFileSystemEntries("/proc/self/fd"))
                    if (++observed > 65536)
                        throw new IOException("fd_census_capacity");
                count = observed;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                censusFailure = error.GetType().Name;
            }

            // A diagnostic census must not prevent failure receipts or group
            // cleanup when the original resource failure was descriptor exhaustion.
            fdBoundaries.Add(new
            {
                Phase = phase,
                ObservedOpenFileDescriptors = count,
                NativeInputPaths = native.Count,
                NestedConsoleArtifacts = verifier?.Artifacts.Count ?? 0,
                CensusFailure = censusFailure,
                PeakOrCapacityProof = false
            });
            output.WriteLine("LOG_UPLOAD_HTTP_FD_BOUNDARY phase=" + phase + " observed=" + (count?.ToString(CultureInfo.InvariantCulture) ?? "unavailable") + " nativeInputs=" + native.Count.ToString(CultureInfo.InvariantCulture) + " peakOrCapacityProof=false");
        }

        string ResolveToolPath(string name)
        {
            string? found = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Select(directory => Path.Combine(directory, name)).FirstOrDefault(File.Exists);
            Need(found is not null, "native_tool_missing");
            string path = File.ResolveLinkTarget(found!, true)?.FullName ?? Path.GetFullPath(found!);
            Need(CanonicalLinux(path), "recipe_tool_paths");
            return path;
        }

        LinuxTracePlan TracePlan(string[] arguments) => InspectLinuxTracePlan(originalNative, selectedTools.ToDictionary(tool => tool.Name, StringComparer.Ordinal), arguments);
        Process StartTrace(LinuxTracePlan plan) => Process.Start(plan.PrepareStartInfo(root)) ?? throw new IOException("log_upload_http_linux_process_start");
        NativeAotLogUploadContract.TraceValues ReadTraces(string directory, string phase)
        {
            string[] paths = Directory.GetFiles(directory, phase + "-trace.*").Order(StringComparer.Ordinal).ToArray();
            Need(paths.Length is > 0 and <= 64, "trace_inventory");
            Dictionary<string, string> copied = new(StringComparer.Ordinal);
            long total = 0;
            foreach (string path in paths)
            {
                var file = held.Open(root, path, "original joined trace", "log_upload_http_linux_trace", 4 * 1024 * 1024, singleLink: true);
                Need((total += file.Length) <= 16 * 1024 * 1024, "trace_capacity");
                copied.Add(Path.GetFileName(path), file.ReadUtf8(4 * 1024 * 1024, "log_upload_http_linux_trace"));
            }

            return NativeAotLogUploadContract.InspectTraceValues(copied, phase);
        }

        async Task<(Control Control, NativeAotLogUploadContract.TraceValues? Trace)> Session(string name, string executable)
        {
            CheckInputs();
            cancellation.ThrowIfCancellationRequested();
            string directory = Path.Combine(destination, name), data = Path.Combine(directory, "data");
            Directory.CreateDirectory(data);
            string log = Path.Combine(data, NativeAotLogUploadContract.LogName), upload = Path.Combine(data, NativeAotLogUploadContract.UploadName);
            await File.WriteAllBytesAsync(log, name == "nonempty-log" ? [0x71] : [], cancellation);
            if (name != "missing-upload")
                await File.WriteAllBytesAsync(upload, name == "nonempty-upload" ? [0x72] : [], cancellation);
            await File.WriteAllBytesAsync(Path.Combine(data, "GUARD.BIN"), Guard, cancellation);
            string baseline = Snapshot(data), startup = "";
            int port = ReservePort();
            Dictionary<string, string> identities = new(StringComparer.Ordinal);
            using (AuthorityFiles seeds = new())
            {
                foreach (string path in Directory.GetFiles(data))
                {
                    var file = seeds.Open(root, path, "original HTTP seed", "log_upload_http_linux_seed", 64, allowEmpty: true, singleLink: true);
                    identities.Add(Path.GetFileName(path), file.InitialIdentity.Key);
                }

                seeds.VerifyUnchanged();
                await Save(directory, "initial-files.json", JsonSerializer.Serialize(new
                {
                    Snapshot = baseline,
                    Identities = identities
                }));
            }

            string[] arguments = LinuxTraceArguments(executable, data, Path.Combine(directory, "write-trace"), port);
            LinuxTracePlan plan = TracePlan(arguments);
            commands.Add(new
            {
                plan.Executable,
                plan.Arguments,
                WorkingDirectory = root,
                Phase = name
            });
            ObserveDescriptors("before-" + name);
            cancellation.ThrowIfCancellationRequested();
            using var process = StartTrace(plan);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            deadline.CancelAfter(TimeSpan.FromSeconds(90));
            using CancellationTokenSource monitorStop = new();
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task stdout = Capture(process.StandardOutput, "server.stdout.log", true), stderr = Capture(process.StandardError, "server.stderr.log", false), monitor = Monitor();
            bool natural = false, joined = false, refused = false, succeeded = false, cancelledBody = false;
            int? exit = null;
            string sessionFailure = "";
            TcpClient? heldPartial = null;
            List<ResponseFact> rows = [];
            NativeAotLogUploadContract.TraceValues? trace = null;
            try
            {
                if (name is "missing-upload" or "nonempty-log" or "nonempty-upload")
                {
                    await process.WaitForExitAsync(deadline.Token);
                    await Task.WhenAll(stdout, stderr).WaitAsync(deadline.Token);
                    natural = true;
                    exit = process.ExitCode;
                    Need(exit == 1 && File.ReadAllText(Path.Combine(directory, "server.stderr.log")).Length == 0 && File.ReadAllText(Path.Combine(directory, "server.stdout.log")).Replace("\r\n", "\n", StringComparison.Ordinal) == "TEST_START m10.027.http\nTEST_FAIL m10.027.http " + (name == "missing-upload" ? "FileNotFoundException" : "InvalidOperationException") + "\n" && Snapshot(data) == baseline, "linux_preflight");
                }
                else
                {
                    Need(await Task.WhenAny(ready.Task, process.WaitForExitAsync()).WaitAsync(deadline.Token) == ready.Task && !process.HasExited, "linux_ready");
                    using HttpClient client = Client(port);
                    await WaitPhase(client, "ready", deadline.Token);
                    RequireStartup(data);
                    startup = Snapshot(data);
                    var owner = Observe();
                    Need(Observe() == owner, "linux_process_identity_changed");
                    await Save(directory, "process-owner.json", JsonSerializer.Serialize(new
                    {
                        Identity = owner,
                        Executable = executable,
                        ImageSha256 = image!.Sha256,
                        Arguments = arguments[8..]
                    }));
                    await Save(directory, "pre-request.json", startup);
                    if (name == "cancel-body")
                    {
                        heldPartial = await Partial(port, deadline.Token);
                        await WaitPhase(client, "receiving", deadline.Token);
                        Need(Snapshot(data) == startup && Observe() == owner, "linux_partial_modified");
                        cancelledBody = true;
                    }
                    else
                    {
                        await Request(ResponseKind.Health);
                        await Request(ResponseKind.StartupLog);
                        await Request(ResponseKind.WrongType, new byte[5126], "text/plain");
                        await Request(ResponseKind.WrongLength, [1]);
                        await Request(ResponseKind.Oversized, new byte[5127]);
                        Need(Snapshot(data) == startup, "linux_bad_request_modified");
                        using (var partial = await Partial(port, deadline.Token))
                        {
                            await WaitPhase(client, "receiving", deadline.Token);
                            await Request(ResponseKind.Busy, NativeAotLogUploadContract.ExpectedUpload());
                            Need(Snapshot(data) == startup, "linux_busy_modified");
                        }

                        await WaitPhase(client, "ready", deadline.Token);
                        Need(Snapshot(data) == startup, "linux_abort_modified");
                        await Request(ResponseKind.Accepted, NativeAotLogUploadContract.ExpectedUpload());
                        NativeAotLogUploadContract.InspectFileValues(File.ReadAllBytes(log), File.ReadAllBytes(upload));
                        string accepted = Snapshot(data);
                        await Request(ResponseKind.Busy, NativeAotLogUploadContract.ExpectedUpload());
                        Need(Snapshot(data) == accepted, "linux_duplicate_modified");
                        await Request(ResponseKind.Log);
                        Need(Observe() == owner, "linux_process_identity_changed");
                        await Request(ResponseKind.Upload);
                        await process.WaitForExitAsync(deadline.Token);
                        await Task.WhenAll(stdout, stderr).WaitAsync(deadline.Token);
                        natural = true;
                        exit = process.ExitCode;
                        Need(exit == 0, "linux_server_exit");
                        InspectServerTranscriptValues(File.ReadAllText(Path.Combine(directory, "server.stdout.log")), File.ReadAllText(Path.Combine(directory, "server.stderr.log")));
                        Need(File.ReadAllBytes(Path.Combine(data, "GUARD.BIN")).AsSpan().SequenceEqual(Guard), "guard_changed");
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

                        string prefix = (rows.Count + 1).ToString("D2", CultureInfo.InvariantCulture);
                        if (bytes is not null)
                            await File.WriteAllBytesAsync(Path.Combine(directory, prefix + ".request-body"), bytes, CancellationToken.None);
                        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
                        byte[] body = await ReadBounded(response, deadline.Token);
                        await File.WriteAllBytesAsync(Path.Combine(directory, prefix + ".body"), body, CancellationToken.None);
                        string contentType = response.Content.Headers.ContentType?.ToString() ?? "";
                        await Save(directory, prefix + ".response.json", JsonSerializer.Serialize(new
                        {
                            ExpectedKind = kind,
                            Method = method,
                            Target = target,
                            Status = (int)response.StatusCode,
                            ContentType = contentType,
                            Bytes = body.Length,
                            Sha256 = Hash(body),
                            CapturedBeforeValueValidation = true
                        }));
                        rows.Add(InspectResponseValues(kind, method, target, (int)response.StatusCode, contentType, body));
                    }
                }
            }
            catch (Exception error)
            {
                sessionFailure = error is ToolFailure tool ? tool.Reason : error.GetType().Name;
                throw;
            }
            finally
            {
                Exception? cleanup = null;
                try
                {
                    await NativeAotWebPayload.StopSessionAsync(process);
                    joined = true;
                    exit = process.ExitCode;
                }
                catch (Exception error)
                {
                    cleanup = error;
                }

                monitorStop.Cancel();
                try
                {
                    await monitor;
                }
                catch (OperationCanceledException) when (monitorStop.IsCancellationRequested)
                {
                }
                catch (Exception error)
                {
                    cleanup = cleanup is null ? error : new AggregateException(cleanup, error);
                }

                try
                {
                    await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(10));
                }
                catch (Exception error)
                {
                    cleanup = cleanup is null ? error : new AggregateException(cleanup, error);
                }

                heldPartial?.Dispose();
                ObserveDescriptors("after-" + name + "-join");
                try
                {
                    await Refused(port, CancellationToken.None);
                    refused = true;
                }
                catch (Exception error)
                {
                    cleanup = cleanup is null ? error : new AggregateException(cleanup, error);
                }

                string actual = "", snapshotFailure = "", projectionFailure = "";
                try
                {
                    actual = Snapshot(data);
                    using AuthorityFiles final = new();
                    Need(Directory.GetFiles(data).Length == identities.Count, "linux_file_identity_changed");
                    foreach (var seed in identities)
                        Need(final.Open(root, Path.Combine(data, seed.Key), "original HTTP final file", "log_upload_http_linux_final", 8192, allowEmpty: true, singleLink: true).InitialIdentity.Key == seed.Value, "linux_file_identity_changed");
                    final.VerifyUnchanged();
                }
                catch (Exception error)
                {
                    snapshotFailure = error.Message;
                }

                try
                {
                    trace = ReadTraces(directory, "write");
                }
                catch (Exception error)
                {
                    projectionFailure = error.Message;
                }

                if (succeeded && (trace is null || trace.ExitedFiles != trace.Files || !trace.Rows.Any(row => row.Syscall == "execve") || !trace.Rows.Any(row => row.Syscall == "exit_group")))
                    cleanup = cleanup is null ? new ToolFailure("log_upload_http_linux_trace_exit") : cleanup;
                if (trace is not null && natural)
                    try
                    {
                        InspectLinuxExecveValues(trace, executable, ["--serve-once", "--root", data, "--urls", "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture)]);
                    }
                    catch (Exception error)
                    {
                        cleanup = cleanup is null ? error : new AggregateException(cleanup, error);
                    }

                await Save(directory, "http.json", JsonSerializer.Serialize(new
                {
                    Schema = "CLROS_LOG_UPLOAD_HTTP_LINUX_SESSION_V1",
                    Name = name,
                    Complete = succeeded && cleanup is null && snapshotFailure.Length == 0,
                    ExpectedFailure = name != "positive",
                    Failure = sessionFailure,
                    CleanupFailure = cleanup?.Message,
                    NaturalExitObserved = natural,
                    Joined = joined,
                    EndpointRefused = refused,
                    ExitCode = exit,
                    ControlledBodyCancellation = cancelledBody,
                    CallerCancellationRequested = cancellation.IsCancellationRequested,
                    WaitCancelled = deadline.IsCancellationRequested,
                    Port = port,
                    Responses = rows,
                    BeforeFilesJson = baseline,
                    PreRequestFilesJson = startup,
                    ActualFilesJson = actual,
                    SnapshotFailure = snapshotFailure,
                    Trace = trace,
                    TraceProjectionFailure = projectionFailure,
                    DeviceFlushObserved = false,
                    GuestQualified = false,
                    PhysicalPersistenceQualified = false
                }));
                if (cleanup is not null)
                    throw cleanup;
                Need(snapshotFailure.Length == 0, "linux_snapshot_failed");
            }

            Need(joined && refused && (!cancelledBody || Snapshot(data) == startup), "linux_session_cleanup");
            if (name == "positive")
                responses.AddRange(rows);
            return (new(name, name != "positive", joined, cancelledBody, exit!.Value, refused), trace);
            NativeAotSqliteContract.LinuxRecoveryIdentity Observe()
            {
                string ReadProc(string path)
                {
                    using FileStream file = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using MemoryStream bytes = new();
                    byte[] buffer = new byte[4096];
                    for (int read; (read = file.Read(buffer)) != 0;)
                    {
                        Need(bytes.Length + read <= 65536, "linux_proc_capacity");
                        bytes.Write(buffer, 0, read);
                    }

                    return new UTF8Encoding(false, true).GetString(bytes.ToArray());
                }

                string children = ReadProc($"/proc/{process.Id}/task/{process.Id}/children");
                string[] ids = children.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                Need(ids.Length == 1 && ids[0].All(char.IsAsciiDigit), "linux_proc_children");
                string child = ids[0];
                string commandLine = ReadProc($"/proc/{child}/cmdline");
                Need(File.ResolveLinkTarget($"/proc/{process.Id}/exe", true)?.FullName == strace && commandLine.EndsWith('\0') && commandLine[..^1].Split('\0').SequenceEqual(new[] { executable }.Concat(arguments[8..])), "linux_process_arguments");
                // Reuse this existing copied proc/session parser only. It has
                // no SQLite semantics and grants no process/signal authority.
                return NativeAotSqliteContract.InspectLinuxRecoveryIdentityValues(process.Id, ReadProc($"/proc/{process.Id}/stat"), children, ReadProc($"/proc/{child}/stat"), ReadProc($"/proc/{child}/status"), File.ResolveLinkTarget($"/proc/{child}/exe", true)?.FullName ?? "", executable);
            }

            async Task Capture(StreamReader reader, string name, bool standardOutput)
            {
                try
                {
                    using FileStream logFile = new(Path.Combine(directory, name), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                    char[] buffer = new char[1024];
                    StringBuilder text = new();
                    for (int count; (count = await reader.ReadAsync(buffer)) != 0;)
                    {
                        text.Append(buffer, 0, count);
                        byte[] bytes = Encoding.UTF8.GetBytes(buffer.AsSpan(0, count).ToString());
                        Need(logFile.Length + bytes.Length <= MaximumTranscriptBytes, "linux_console_capacity");
                        await logFile.WriteAsync(bytes);
                        await logFile.FlushAsync();
                        if (standardOutput && text.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Contains(Ready, StringComparison.Ordinal))
                            ready.TrySetResult();
                    }
                }
                catch
                {
                    deadline.Cancel();
                    throw;
                }
            }

            async Task Monitor()
            {
                try
                {
                    while (true)
                    {
                        string[] paths = Directory.GetFiles(directory, "write-trace.*");
                        Need(paths.Length <= 64, "linux_trace_capacity");
                        long bytes = 0;
                        foreach (string path in paths)
                        {
                            long length = new FileInfo(path).Length;
                            Need(length <= 4 * 1024 * 1024 && (bytes += length) <= 16 * 1024 * 1024, "linux_trace_capacity");
                        }

                        await Task.Delay(100, monitorStop.Token);
                    }
                }
                catch (OperationCanceledException) when (monitorStop.IsCancellationRequested)
                {
                }
                catch
                {
                    deadline.Cancel();
                    throw;
                }
            }
        }
    }

}
