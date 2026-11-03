using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OS.Tooling;
/// <summary>Own one short diagnostic: JSON, health, actual CoreLib HEAD, exact64KiB
/// range and health, followed by intentional runner cancellation and joining.
/// Original image extraction supplies expected bytes. This observation grants
/// neither full HTTP/browser qualification nor guest shutdown/cleanup acceptance.</summary>

public static partial class NativeAotDiskTimingPeer
{
    public const string Target = "/_framework/System.Private.CoreLib.wasm";
    public const string Marker = "M10_018_TIMING ";
    public const string DispatchMarker = "M10_018_DISPATCH ";
    public const string DriverMarker = "M10_018_DRIVER ";
    private static readonly string[] DispatchFields = ["bytes", "phaseNs", "pollNs", "dispatchNs", "nestedPollNs", "dispatchNonPollNs", "supervisorNonPollNs", "completed", "clipped", "physicalActive", "phaseComplete"];
    private static readonly string[] DriverFields = ["bytes", "phaseNs", "pollNs", "driverNs", "nestedPollNs", "driverNonPollNs", "dispatchShellNonPollNs", "completed", "clipped", "physicalActive", "phaseComplete"];
    private static readonly string[] Fields = ["bytes", "calls", "steps", "pumpNs", "scanNs", "flushNs", "preNetworkNs", "postNetworkNs", "postReadinessNs", "readinessRefreshNs", "readinessWaitScanNs", "postWaitRecordNs", "networkCalls", "diskReady", "otherReady", "noAdvance", "elapsed", "stepLimit", "failures", "flushedPastBudget", "pollNs", "diskRoundNs", "clockCalls", "clockNs", "clockPreNs", "clockDiskNs", "clockPostNs", "syscallCalls", "syscallNs", "syscallPreNs", "syscallDiskNs", "syscallPostNs", "supervisorCalls", "supervisorNs", "supervisorPreNs", "supervisorDiskNs", "supervisorPostNs", "idleCalls", "idleNs", "idlePreNs", "idleDiskNs", "idlePostNs"];
    /// <summary>Check one bounded copied status DTO and its actual media type.
    /// Reject malformed, duplicate, extra or mismatched fields. This host-only
    /// parser retains no input and grants no original-process or guest authority.</summary>

    internal static void InspectStatusJson(ReadOnlySpan<byte> body, string? mediaType)
    {
        Need(body.Length is> 0 and <= 1024 && mediaType == "application/json", "json_response");
        try
        {
            using var document = JsonDocument.Parse(body.ToArray());
            var value = document.RootElement;
            Need(value.ValueKind == JsonValueKind.Object, "json_response");
            var fields = value.EnumerateObject().ToArray();
            Need(fields.Length == 3 && fields.Select(field => field.Name).Distinct(StringComparer.Ordinal).Count() == 3 && value.TryGetProperty("profile", out var profile) && profile.ValueKind == JsonValueKind.String && profile.GetString() == "v1" && value.TryGetProperty("ready", out var ready) && ready.ValueKind == JsonValueKind.True && value.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String && message.GetString() == "CLROS ready — café", "json_response");
        }
        catch (Exception error)when (error is JsonException or ArgumentException or InvalidOperationException)
        {
            throw new ToolFailure("disk_timing_json_response");
        }
    }

    /// <summary>Validate a copied, single bounded aggregate. Every successful
    /// invocation has exactly one exclusive yield reason. Durations overlap
    /// existing serial/device intervals and must not be added to those totals.
    /// This parser supplies no original-source, runner or native authority.</summary>

    public static IReadOnlyDictionary<string, ulong> InspectMetrics(string text)
    {
        Need(text.Length <= 32 * 1024 * 1024, "metric_extent");
        string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Where(l => l.StartsWith(Marker, StringComparison.Ordinal)).ToArray();
        Need(lines.Length == 1 && lines[0].Length <= 2048, "metric_count");
        string[] tokens = lines[0][Marker.Length..].Split(' ');
        Need(tokens.Length == Fields.Length, "metric_fields");
        Dictionary<string, ulong> values = new(StringComparer.Ordinal);
        for (int i = 0; i < Fields.Length; i++)
        {
            string prefix = Fields[i] + "=";
            Need(tokens[i].StartsWith(prefix, StringComparison.Ordinal), "metric_field");
            string number = tokens[i][prefix.Length..];
            Need(number.Length is> 0 and <= 20 && (number.Length == 1 || number[0] != '0') && number.All(c => c is >= '0' and <= '9') && ulong.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out _), "metric_number");
            values.Add(Fields[i], ulong.Parse(number, CultureInfo.InvariantCulture));
        }

        ulong calls = values["calls"], steps = values["steps"];
        Need(values["bytes"] == 65536 && calls is> 0 and <= 10_000_000 && steps >= calls && steps <= calls * 4 && values["networkCalls"] == calls && values["failures"] == 0 && values["flushedPastBudget"] <= steps && values["postReadinessNs"] <= ulong.MaxValue - values["postWaitRecordNs"] && values["postReadinessNs"] + values["postWaitRecordNs"] == values["postNetworkNs"] && values["readinessRefreshNs"] <= ulong.MaxValue - values["readinessWaitScanNs"] && values["readinessRefreshNs"] + values["readinessWaitScanNs"] == values["postReadinessNs"], "metric_bounds");
        ulong reasons = 0;
        foreach (string field in new[]
        {
            "diskReady",
            "otherReady",
            "noAdvance",
            "elapsed",
            "stepLimit"
        }

        )
        {
            Need(values[field] <= calls, "metric_reason");
            reasons += values[field];
        }

        Need(reasons == calls, "metric_reasons");
        ulong causeCalls = 0, causeTotal = 0, causePre = 0, causeDisk = 0, causePost = 0;
        foreach (string cause in new[]
        {
            "clock",
            "syscall",
            "supervisor",
            "idle"
        }

        )
        {
            ulong count = values[cause + "Calls"], total = values[cause + "Ns"], pre = values[cause + "PreNs"], disk = values[cause + "DiskNs"], post = values[cause + "PostNs"];
            Need(count <= calls && (count != 0 || total == 0) && pre <= ulong.MaxValue - disk && pre + disk <= ulong.MaxValue - post && pre + disk + post == total && causeCalls <= ulong.MaxValue - count && causeTotal <= ulong.MaxValue - total && causePre <= ulong.MaxValue - pre && causeDisk <= ulong.MaxValue - disk && causePost <= ulong.MaxValue - post, "metric_cause");
            causeCalls += count;
            causeTotal += total;
            causePre += pre;
            causeDisk += disk;
            causePost += post;
        }

        Need(causeCalls == calls && causeTotal == values["pollNs"] && causePre == values["preNetworkNs"] && causeDisk == values["diskRoundNs"] && causePost == values["postNetworkNs"], "metric_causes");
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, ulong>(values);
    }

    /// <summary>Validate a separate phase-complete dispatch partition against
    /// the unchanged v5 poll record. Physical activity at Freeze is explicit;
    /// neither record proves dispatch return or guest shutdown. Copied values
    /// grant no original source, native, runner or execution authority.</summary>

    public static IReadOnlyDictionary<string, ulong> InspectDispatchMetrics(string text, IReadOnlyDictionary<string, ulong> pollMetrics)
    {
        Need(text.Length <= 32 * 1024 * 1024, "dispatch_extent");
        string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Where(line => line.StartsWith(DispatchMarker, StringComparison.Ordinal)).ToArray();
        Need(lines.Length == 1 && lines[0].Length <= 1024, "dispatch_count");
        string[] tokens = lines[0][DispatchMarker.Length..].Split(' ');
        Need(tokens.Length == DispatchFields.Length, "dispatch_fields");
        Dictionary<string, ulong> values = new(StringComparer.Ordinal);
        for (int i = 0; i < DispatchFields.Length; i++)
        {
            string prefix = DispatchFields[i] + "=";
            Need(tokens[i].StartsWith(prefix, StringComparison.Ordinal), "dispatch_field");
            string number = tokens[i][prefix.Length..];
            Need(number.Length is> 0 and <= 20 && (number.Length == 1 || number[0] != '0') && number.All(c => c is >= '0' and <= '9') && ulong.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out _), "dispatch_number");
            values.Add(DispatchFields[i], ulong.Parse(number, CultureInfo.InvariantCulture));
        }

        ulong phase = values["phaseNs"], poll = values["pollNs"], dispatch = values["dispatchNs"], nested = values["nestedPollNs"];
        Need(values["bytes"] == 65536 && phase > 0 && pollMetrics.TryGetValue("pollNs", out ulong oldPoll) && oldPoll == poll && poll <= phase && dispatch <= phase && nested <= poll && nested <= dispatch && values["dispatchNonPollNs"] == dispatch - nested && dispatch - nested <= phase - poll && values["supervisorNonPollNs"] == phase - poll - (dispatch - nested) && values["completed"] <= 10_000_000 && values["clipped"] is> 0 and <= 2 && values["physicalActive"] == 1 && values["phaseComplete"] == 1, "dispatch_partition");
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, ulong>(values);
    }

    /// <summary>Validate the generated-driver child of the already validated
    /// dispatch record. Subsets must be nonnegative and conserve the complete
    /// dispatch non-poll total. Driver time includes worker, guest, kernel,
    /// transitions, logging and measurement overhead; it is not guest-only CPU.
    /// Copied records supply no source, nesting, late-return or guest authority.</summary>

    public static IReadOnlyDictionary<string, ulong> InspectDriverMetrics(string text, IReadOnlyDictionary<string, ulong> dispatchMetrics)
    {
        Need(text.Length <= 32 * 1024 * 1024, "driver_extent");
        string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Where(line => line.StartsWith(DriverMarker, StringComparison.Ordinal)).ToArray();
        Need(lines.Length == 1 && lines[0].Length <= 1024, "driver_count");
        string[] tokens = lines[0][DriverMarker.Length..].Split(' ');
        Need(tokens.Length == DriverFields.Length, "driver_fields");
        Dictionary<string, ulong> values = new(StringComparer.Ordinal);
        for (int i = 0; i < DriverFields.Length; i++)
        {
            string prefix = DriverFields[i] + "=";
            Need(tokens[i].StartsWith(prefix, StringComparison.Ordinal), "driver_field");
            string number = tokens[i][prefix.Length..];
            Need(number.Length is> 0 and <= 20 && (number.Length == 1 || number[0] != '0') && number.All(c => c is >= '0' and <= '9') && ulong.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out _), "driver_number");
            values.Add(DriverFields[i], ulong.Parse(number, CultureInfo.InvariantCulture));
        }

        ulong phase = 0, poll = 0, dispatch = 0, parentPoll = 0, parentNonPoll = 0;
        Need(dispatchMetrics.TryGetValue("phaseNs", out phase) && dispatchMetrics.TryGetValue("pollNs", out poll) && dispatchMetrics.TryGetValue("dispatchNs", out dispatch) && dispatchMetrics.TryGetValue("nestedPollNs", out parentPoll) && dispatchMetrics.TryGetValue("dispatchNonPollNs", out parentNonPoll), "driver_parent");
        ulong driver = values["driverNs"], nested = values["nestedPollNs"];
        Need(values["bytes"] == 65536 && phase > 0 && values["phaseNs"] == phase && values["pollNs"] == poll && poll <= phase && dispatch <= phase && parentPoll <= poll && parentPoll <= dispatch && parentNonPoll == dispatch - parentPoll && parentNonPoll <= phase - poll && driver <= dispatch && nested <= driver && nested <= parentPoll && parentPoll - nested <= dispatch - driver && values["driverNonPollNs"] == driver - nested && values["driverNonPollNs"] <= parentNonPoll && values["dispatchShellNonPollNs"] == (dispatch - driver) - (parentPoll - nested) && values["dispatchShellNonPollNs"] == parentNonPoll - values["driverNonPollNs"] && values["completed"] <= 10_000_000 && values["clipped"] is> 0 and <= 2 && values["physicalActive"] == 1 && values["phaseComplete"] == 1, "driver_partition");
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, ulong>(values);
    }

    /// <summary>Leaves thirty seconds inside the admitted runner deadline for
    /// owned cancellation and joining; the diagnostic itself never exceeds480s.</summary>

    public static int PeerTimeoutSeconds(int runnerTimeoutSeconds)
    {
        Need(runnerTimeoutSeconds is >= 240 and <= 600, "runner_timeout");
        return Math.Min(480, runnerTimeoutSeconds - 30);
    }

    /// <summary>Fresh artifacts must be inside root/artifacts. Caller retains
    /// exact image/source/native authorities until return. All failures preserve
    /// copied responses and the joined runner disposition. Cancellation of the
    /// original runner is deliberate only after every diagnostic check passes.</summary>

    public static Task<Completion> RunAsync(string root, string serial, Func<int, CancellationToken, Task<ProcessResult>> runRunner, WebRootDiskInventory.Inspection inventory, int runnerTimeoutSeconds = 600, CancellationToken cancellation = default) =>
        RunAsync(root, serial, runRunner, inventory, new BenchmarkOptions(), runnerTimeoutSeconds, cancellation);

    /// <summary>
    /// Runs the same owned diagnostic with an explicit bounded request budget.
    /// Invalid controls reject before artifacts or a runner are created. Expiry
    /// during headers or body receipt preserves failed observations and joins
    /// the original runner through the existing cancellation path.
    /// </summary>
    public static async Task<Completion> RunAsync(string root, string serial, Func<int, CancellationToken, Task<ProcessResult>> runRunner,
        WebRootDiskInventory.Inspection inventory, BenchmarkOptions options, int runnerTimeoutSeconds = 600, CancellationToken cancellation = default)
    {
        if (options is null)
            throw new ToolFailure("disk_timing_request_timeout");
        options.Validate();
        int peerTimeoutSeconds = PeerTimeoutSeconds(runnerTimeoutSeconds);
        byte[]? diskImage = null;
        if (options.DiskEvidence != DiskEvidenceMode.Original)
        {
            Need(options.DiskImageBytes.Length is >= 512 and <= WebRootImageFixture.MaximumImageBytes &&
                options.DiskImageBytes.Length % 512 == 0, "evidence_image");
            diskImage = options.DiskImageBytes.ToArray();
            Need(string.Equals(Convert.ToHexString(SHA256.HashData(diskImage)), inventory.ImageSha256,
                StringComparison.OrdinalIgnoreCase), "evidence_image");
        }
        root = Path.GetFullPath(root);
        serial = Path.GetFullPath(serial, root);
        var file = inventory.Find(WebRootDiskInventory.Root + Target);
        Need(file is not null && !file.IsDirectory && file.Bytes >= 65536, "corelib_inventory");
        var asset = new DiskStaticHttpContract.Asset(file!, true);
        byte[] range = file!.CopyBytes()[..65536];
        string directory = serial + ".disk-timing";
        Need(directory.StartsWith(Path.Combine(root, "artifacts") + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) && !Path.Exists(directory), "fresh_artifacts");
        AuthorityFiles.RequirePlainPath(root, Path.GetDirectoryName(directory)!, "disk_timing_path");
        Directory.CreateDirectory(directory);
        int port;
        using (TcpListener reserve = new(IPAddress.Loopback, 0))
        {
            reserve.Start();
            port = ((IPEndPoint)reserve.LocalEndpoint).Port;
        }

        using var owned = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(owned.Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(peerTimeoutSeconds));
        Task<ProcessResult> runner = runRunner(port, owned.Token);
        Task? peer = null;
        bool complete = false;
        List<object> rows = [];
        string? requestPhase = null;
        long requestBytes = 0;
        IReadOnlyDictionary<string, ulong>? metrics = null, dispatchMetrics = null, driverMetrics = null, verificationMetrics = null;
        IReadOnlyList<SelectionObservation>? selections = null;
        NativeDiskReadObservations.DiagnosticInspection? diskEvidence = null;
        string? failure = null;
        try
        {
            peer = Observe();
            if (await Task.WhenAny(peer, runner) == runner)
                throw new ToolFailure("disk_timing_runner_exited_before_peer");
            await peer;
            cancellation.ThrowIfCancellationRequested();
            owned.Cancel();
            var joined = await runner;
            Need(joined.Cancelled && joined.TerminationConfirmed && !joined.TimedOut && !joined.OutputLimitExceeded, "runner_join");
            // A frozen phase line cannot erase a later physical-End failure.
            // Re-read only after the original runner joins; keep frozen values
            // but fail the diagnostic if later guest bookkeeping failed.
            string finalSerial = await ReadSerial(CancellationToken.None);
            InspectGuestStatus(finalSerial);
            var finalEvidence = InspectDiskEvidence(finalSerial);
            Need(finalEvidence?.Counters == diskEvidence?.Counters && finalEvidence?.Cancelled == diskEvidence?.Cancelled &&
                finalEvidence?.Rows == diskEvidence?.Rows && finalEvidence?.CopiedSectors == diskEvidence?.CopiedSectors &&
                finalEvidence?.PayloadIncluded == diskEvidence?.PayloadIncluded, "final_evidence_changed");
            Need(metrics is not null && dispatchMetrics is not null && driverMetrics is not null && verificationMetrics is not null && InspectMetrics(finalSerial).SequenceEqual(metrics) && InspectDispatchMetrics(finalSerial, metrics).SequenceEqual(dispatchMetrics) && InspectDriverMetrics(finalSerial, dispatchMetrics).SequenceEqual(driverMetrics) && InspectVerificationMetrics(finalSerial, dispatchMetrics).SequenceEqual(verificationMetrics), "final_metrics_changed");
            Need(selections is not null && InspectSelections(finalSerial).SequenceEqual(selections), "final_selections_changed");
            using Socket stopped = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            using var refusal = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            bool refused = false;
            try
            {
                await stopped.ConnectAsync(IPAddress.Loopback, port, refusal.Token);
            }
            catch (SocketException error)when (error.SocketErrorCode == SocketError.ConnectionRefused)
            {
                refused = true;
            }

            Need(refused, "endpoint_live");
            complete = true;
            return new(port, directory, joined);
        }
        catch (Exception error)
        {
            failure = error is ToolFailure tool ? tool.Reason : error.GetType().FullName;
            throw;
        }
        finally
        {
            owned.Cancel();
            if (peer is not null)
                try
                {
                    await peer;
                }
                catch
                {
                }

            ProcessResult? joined = null;
            string? joinFault = null;
            try
            {
                joined = await runner;
            }
            catch (Exception error)
            {
                joinFault = error.GetType().FullName;
            }

            // Preserve bounded original observations even on failure. Never
            // replace the original error with a secondary diagnostic I/O error.
            try
            {
                using FileStream receipt = new(Path.Combine(directory, "diagnostic.json"), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                await JsonSerializer.SerializeAsync(receipt, new { Schema = "clros-disk-read-timing-v11", Scope = "diagnostic-only", Complete = complete, RequestSequence = "json-health-head-range-health", FullAndCleanupQualified = false, Port = port, RunnerTimeoutSeconds = runnerTimeoutSeconds, PeerTimeoutSeconds = peerTimeoutSeconds, RequestTimeoutTicks = options.RequestTimeout.Ticks, RequestTimeoutScope = "headers-and-streamed-body", DiskEvidenceMode = options.DiskEvidence.ToString(), DiskEvidence = diskEvidence, ImageSha256 = inventory.ImageSha256, Asset = asset, ClockFrequency = Stopwatch.Frequency, Responses = rows, IncompleteRequest = requestPhase is null ? null : new { Phase = requestPhase, Bytes = requestBytes }, Metrics = metrics, DispatchMetrics = dispatchMetrics, DriverMetrics = driverMetrics, VerificationSchema, VerificationMetrics = verificationMetrics, SelectionSchema = "clros-thread-dispatch-selection-v1", Selections = selections, Failure = failure, RunnerJoined = true, Runner = joined, JoinFault = joinFault }, cancellationToken: CancellationToken.None);
                receipt.Flush(true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        async Task Observe()
        {
            await NativeAotSocketPeer.WaitForSerialAsync(serial, text => text.Contains("M10_018_LISTENING ", StringComparison.Ordinal), deadline.Token);
            using HttpClient client = new(new SocketsHttpHandler { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.None, UseProxy = false, MaxConnectionsPerServer = 1 })
            {
                BaseAddress = new Uri("http://127.0.0.1:" + port),
                Timeout = options.RequestTimeout,
                DefaultRequestVersion = HttpVersion.Version11,
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
            };
            await Request("GET", "/api/status", 200, null);
            await Request("GET", "/healthz", 200, "OS_MINIMAL_API_OK"u8.ToArray());
            await Request("HEAD", Target, 200, []);
            await Request("GET", Target, 206, range);
            await Request("GET", "/healthz", 200, "OS_MINIMAL_API_OK"u8.ToArray());
            while (true)
            {
                deadline.Token.ThrowIfCancellationRequested();
                string text = await ReadSerial(deadline.Token);
                InspectGuestStatus(text);
                if (text.Split('\n').Any(line => line.StartsWith(DriverMarker, StringComparison.Ordinal)) && text.LastIndexOf('\n') > text.IndexOf(DriverMarker, StringComparison.Ordinal) && text.Split('\n').Any(line => line.StartsWith(VerificationMarker, StringComparison.Ordinal)) && text.LastIndexOf('\n') > text.IndexOf(VerificationMarker, StringComparison.Ordinal) && text.Split('\n').Any(line => line.StartsWith(SelectionEndMarker, StringComparison.Ordinal)) && text.LastIndexOf('\n') > text.IndexOf(SelectionEndMarker, StringComparison.Ordinal))
                {
                    metrics = InspectMetrics(text);
                    dispatchMetrics = InspectDispatchMetrics(text, metrics);
                    driverMetrics = InspectDriverMetrics(text, dispatchMetrics);
                    verificationMetrics = InspectVerificationMetrics(text, dispatchMetrics);
                    diskEvidence = InspectDiskEvidence(text);
                    selections = InspectSelections(text);
                    Need((ulong)selections.Count >= dispatchMetrics["completed"] + dispatchMetrics["clipped"], "selection_dispatch_count");
                    break;
                }

                await Task.Delay(50, deadline.Token);
            }

            async Task Request(string method, string path, int status, byte[]? expected)
            {
                long start = Stopwatch.GetTimestamp();
                requestPhase = "headers";
                requestBytes = 0;
                using var requestDeadline = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
                requestDeadline.CancelAfter(client.Timeout);
                using var request = new HttpRequestMessage(new HttpMethod(method), path);
                bool ranged = status == 206;
                if (ranged)
                    request.Headers.Range = new(0, 65535);
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, requestDeadline.Token);
                long headersReceived = Stopwatch.GetTimestamp();
                requestPhase = "body";
                using var source = await response.Content.ReadAsStreamAsync(requestDeadline.Token);
                using MemoryStream body = new();
                byte[] buffer = new byte[8192];
                for (int count; (count = await source.ReadAsync(buffer, requestDeadline.Token)) != 0;)
                {
                    Need(body.Length + count <= 65536, "body_bound");
                    body.Write(buffer, 0, count);
                    requestBytes = body.Length;
                }

                long bodyReceived = Stopwatch.GetTimestamp();
                requestPhase = "body-file";
                byte[] actual = body.ToArray();
                int ordinal = rows.Count;
                string bodyFile = ordinal.ToString("D3", CultureInfo.InvariantCulture) + ".body";
                long bodyWritten;
                long bodyFlushed;
                using (FileStream output = new(Path.Combine(directory, bodyFile), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                {
                    await output.WriteAsync(actual, deadline.Token);
                    bodyWritten = Stopwatch.GetTimestamp();
                    output.Flush(true);
                    bodyFlushed = Stopwatch.GetTimestamp();
                }

                rows.Add(new { Ordinal = ordinal, Method = method, Path = path, Status = (int)response.StatusCode, Started = start, HeadersReceived = headersReceived, BodyReceived = bodyReceived, BodyWritten = bodyWritten, BodyFlushed = bodyFlushed, Finished = Stopwatch.GetTimestamp(), Bytes = actual.Length, Sha256 = Convert.ToHexString(SHA256.HashData(actual)), BodyFile = bodyFile, RequestHeaders = request.Headers.ToString(), ResponseHeaders = response.Headers.ToString() + response.Content.Headers.ToString() });
                requestPhase = "validation";
                Need((int)response.StatusCode == status && (expected is null || actual.AsSpan().SequenceEqual(expected)), "response");
                if (path == "/api/status")
                    InspectStatusJson(actual, response.Content.Headers.ContentType?.MediaType);
                if (path == Target)
                    Need(response.Headers.ETag?.ToString() == asset.ETag && response.Content.Headers.ContentType?.ToString() == asset.MediaType && response.Content.Headers.ContentLength == (ranged ? 65536 : asset.Bytes) && (ranged ? response.Content.Headers.ContentRange?.ToString() == "bytes 0-65535/" + asset.Bytes : response.Content.Headers.ContentRange is null), "metadata");
                requestPhase = null;
            }
        }

        async Task<string> ReadSerial(CancellationToken token)
        {
            using FileStream input = new(serial, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            Need(input.Length <= 32 * 1024 * 1024, "serial_extent");
            using StreamReader reader = new(input, Encoding.UTF8, false);
            return await reader.ReadToEndAsync(token);
        }

        void InspectGuestStatus(string text) => Need(!text.Contains("TEST_FAIL", StringComparison.Ordinal) && !text.Contains("M16_008_STOP ", StringComparison.Ordinal), "guest_failure");

        NativeDiskReadObservations.DiagnosticInspection? InspectDiskEvidence(string text)
        {
            if (options.DiskEvidence == DiskEvidenceMode.Original)
            {
                Need(!text.Contains("CLROS_DISK_IO", StringComparison.Ordinal), "evidence_unselected");
                return null;
            }
            return NativeDiskReadObservations.InspectDiagnosticTranscript(text, diskImage!,
                options.DiskEvidence == DiskEvidenceMode.Trace, file!.Inode, file!.Bytes);
        }
    }

    private static void Need(bool valid, string reason)
    {
        if (!valid)
            throw new ToolFailure("disk_timing_" + reason);
    }
}
