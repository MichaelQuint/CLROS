using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Kernel.Build;
using OS.Tools.QemuRunner;

try
{
    if (EvidenceManifestCommand.TryRun(args, out int evidenceManifestExitCode))
    {
        return evidenceManifestExitCode;
    }

    RunnerOptions options = RunnerOptions.Parse(args);
    if (options.ShowHelp)
    {
        RunnerOptions.PrintHelp();
        return 0;
    }

    Console.WriteLine("QEMU_ACCELERATOR " + options.Accelerator);
    Console.WriteLine("QEMU_MACHINE " + OS.Tooling.QemuContracts.MachineArgument(options.Accelerator));
    if (!options.DryRun && OS.Tooling.QemuContracts.AcceleratorFailure(options.Accelerator, OperatingSystem.IsWindows(), options.Accelerator == "whpx" && OS.Tooling.QemuContracts.IsWhpxAvailable()) is string acceleratorFailure)
    {
        Console.Error.WriteLine("TEST_FAIL qemu.smoke " + acceleratorFailure);
        WriteSummaryLogBestEffort(options, "fail", Array.Empty<string>(), options.RequiredMarkers, Array.Empty<string>(), acceleratorFailure);
        return 15;
    }

    if (!options.DryRun && options.VirtioBlockBootImagePath is not null && (!File.Exists(options.VirtioBlockBootImagePath) || new FileInfo(options.VirtioBlockBootImagePath).Length < 68 * 512 || new FileInfo(options.VirtioBlockBootImagePath).Length % 512 != 0))
        throw new ArgumentException("Virtio block boot image must be an existing sector-aligned raw image.");
    if (options.Preflight)
    {
        return RunPreflight(options);
    }

    if (options.DryRun)
    {
        Console.WriteLine("TEST_START qemu.runner.dryrun");
        Console.WriteLine("WORKLOAD_PROFILE " + options.WorkloadProfile);
        if (options.WritableDataImagePath is not null)
        {
            Console.WriteLine("WRITABLE_DATA_IMAGE " + options.WritableDataImagePath);
            Console.WriteLine("WRITABLE_DATA_POLICY boot=immutable-overlay data=mutable cache=writeback acceptance=pending");
        }

        if (options.FirmwareRngSource is not null)
            Console.WriteLine("FIRMWARE_RNG_SOURCE " + options.FirmwareRngSource);
        if (options.ContinuingService)
            Console.WriteLine("CONTINUING_SERVICE true");
        if (options.ServiceQuotaFailure)
            Console.WriteLine("SERVICE_QUOTA_FAILURE true");
        if (options.DiskReadTiming)
            Console.WriteLine("DISK_READ_TIMING true");
        Console.WriteLine("MARKER_PRESETS " + FormatSummaryList(options.MarkerPresets));
        Console.WriteLine("REQUIRED_MARKERS " + FormatMarkers(options.RequiredMarkers));
        Console.WriteLine("ORDERED_MARKERS " + FormatSummaryList(options.OrderedMarkers));
        Console.WriteLine("UNIQUE_MARKERS " + FormatSummaryList(options.UniqueMarkers));
        Console.WriteLine("FORBIDDEN_MARKERS " + FormatSummaryList(options.ForbiddenMarkers));
        if (options.HttpProbe is not null)
        {
            WriteHttpProbe(options.HttpProbe.Value);
        }

        Console.WriteLine(QemuCommandBuilder.BuildCommandLine(options, options.OvmfCodePath));
        Console.WriteLine("TEST_PASS qemu.runner.dryrun");
        return 0;
    }

    DiscoveryResult qemu = ResolveExecutable(options.QemuPath);
    DiscoveryResult ovmf = ResolveOvmfCode(options.OvmfCodePath, qemu.Path);
    WriteDiscovery("QEMU_DISCOVERY", qemu);
    WriteDiscovery("OVMF_DISCOVERY", ovmf);
    ValidateResolvedPathSeparation(options, qemu.Path, ovmf.Path);
    if (string.IsNullOrWhiteSpace(options.ImagePath) || !File.Exists(options.ImagePath))
    {
        Console.Error.WriteLine("TEST_FAIL qemu.smoke image_not_found");
        Console.Error.WriteLine("Image path is required and must exist. Use --image <path>.");
        WriteSummaryLogBestEffort(options, "fail", Array.Empty<string>(), options.RequiredMarkers, Array.Empty<string>(), "image_not_found");
        return 2;
    }

    if (qemu.Path is null || ovmf.Path is null)
    {
        List<string> missing = new List<string>();
        if (qemu.Path is null)
        {
            missing.Add("qemu_not_found");
        }

        if (ovmf.Path is null)
        {
            missing.Add("ovmf_not_found");
        }

        Console.Error.WriteLine("TEST_FAIL qemu.smoke " + string.Join(" ", missing));
        WriteSummaryLogBestEffort(options, "fail", Array.Empty<string>(), options.RequiredMarkers, Array.Empty<string>(), FormatSummaryList(missing));
        return qemu.Path is null ? 3 : 6;
    }

    if (options.ExpectedUnexpectedVectorPanic || options.ExpectedOwnedSubsystemPanic || options.ExpectedUnhandledException)
    {
        return RunExpectedUnexpectedVectorPanic(options, qemu.Path, ovmf.Path);
    }

    using WritableDataImageLease? writable = options.WritableDataImagePath is null ? null : WritableDataImageLease.Open(options.ImagePath, options.WritableDataImagePath);
    options.WritableDataLease = writable;
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.SerialLogPath)) ?? ".");
    if (options.EthernetCapturePath is not null)
        Directory.CreateDirectory(Path.GetDirectoryName(options.EthernetCapturePath) ?? ".");
    if (File.Exists(options.SerialLogPath))
    {
        File.Delete(options.SerialLogPath);
    }

    Console.WriteLine("TEST_START qemu.smoke");
    Console.WriteLine("IMAGE_ARTIFACT " + options.ImagePath);
    Console.WriteLine("SERIAL_LOG " + options.SerialLogPath);
    Console.WriteLine("OVMF_CODE " + ovmf.Path);
    Console.WriteLine("WORKLOAD_PROFILE " + options.WorkloadProfile);
    if (writable is not null)
        foreach (string row in writable.Summary())
            Console.WriteLine(row);
    if (options.FirmwareRngSource is not null)
        Console.WriteLine("FIRMWARE_RNG_SOURCE " + options.FirmwareRngSource);
    if (options.SocketForwardPort is int socketForward)
        Console.WriteLine("SOCKET_FORWARD_PORT " + socketForward.ToString(System.Globalization.CultureInfo.InvariantCulture));
    if (options.ContinuingService)
        Console.WriteLine("CONTINUING_SERVICE true");
    if (options.ServiceQuotaFailure)
        Console.WriteLine("SERVICE_QUOTA_FAILURE true");
    Console.WriteLine("MARKER_PRESETS " + FormatSummaryList(options.MarkerPresets));
    Console.WriteLine("REQUIRED_MARKERS " + FormatMarkers(options.RequiredMarkers));
    Console.WriteLine("ORDERED_MARKERS " + FormatSummaryList(options.OrderedMarkers));
    Console.WriteLine("UNIQUE_MARKERS " + FormatSummaryList(options.UniqueMarkers));
    Console.WriteLine("FORBIDDEN_MARKERS " + FormatSummaryList(options.ForbiddenMarkers));
    SerialMarkerStateMachine streamingMarkers = new SerialMarkerStateMachine(options.RequiredMarkers, options.ForbiddenMarkers, successTerminalMarker: options.SuccessTerminalMarker, byteCapacity: options.SerialByteCapacity, orderedMarkers: options.OrderedMarkers, uniqueMarkers: options.UniqueMarkers);
    using ContinuingNetworkPeer? networkPeer = options.ContinuingNetworkPeer ? new(TimeSpan.FromMinutes(5), runTcp: true) : null;
    using SocketContainmentPeer? containmentPeer = options.SocketContainmentPeer ? new(TimeSpan.FromSeconds(options.TimeoutSeconds)) : null;
    using CancellationTokenSource? serviceCancellation = options.ContinuingService ? new() : null;
    using WebFrameControl? webFrames = options.ContinuingWeb || options.ContinuingService ? new(TimeSpan.FromSeconds(options.TimeoutSeconds), continuingService: options.ContinuingService) : null;
    using ServiceSocketControls? serviceControls = options.ContinuingService && !options.ServiceQuotaFailure ? new() : null;
    Task<ServiceSocketControls.Report>? serviceControlTask = null;
    using Process process = new Process();
    process.StartInfo.FileName = qemu.Path;
    string? declaredCapture = Environment.GetEnvironmentVariable("CLROS_M01_CAPTURE_EXECUTABLE");
    bool captureRoute = declaredCapture is not null && string.Equals(Path.GetFullPath(qemu.Path), Path.GetFullPath(declaredCapture), StringComparison.OrdinalIgnoreCase);
    using FileStream? captureExecutable = captureRoute ? new(qemu.Path, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
    if (captureRoute && (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CLROS_M01_REAL_QEMU")) || Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(captureExecutable!)) != Environment.GetEnvironmentVariable("CLROS_M01_CAPTURE_SHA256")))
        throw new IOException("qemu_capture_route_identity");
    bool stdioStartupQmp = options.WatchStartup && !captureRoute;
    process.StartInfo.UseShellExecute = false;
    process.StartInfo.CreateNoWindow = true;
    process.StartInfo.RedirectStandardInput = stdioStartupQmp;
    process.StartInfo.RedirectStandardOutput = stdioStartupQmp;
    foreach (string argument in QemuCommandBuilder.BuildArguments(options, ovmf.Path, containmentPeer?.NetdevArgument ?? networkPeer?.NetdevArgument, webFrames?.Arguments))
    {
        process.StartInfo.ArgumentList.Add(argument);
    }

    using WhpxPrelaunch? prelaunch = WhpxPrelaunch.Create(process, options.Accelerator, options.SerialLogPath);
    Stopwatch stopwatch = Stopwatch.StartNew();
    bool processStarted = false;
    bool processTerminationRequested = false;
    bool processCleanupFailed = false;
    bool qemuExitedSpontaneously = false;
    int? qemuExitCode = null;
    if (options.ContinuingWeb && !CheckWebPortAvailable(options.SocketForwardPort!.Value))
    {
        options.WebEvidence!.Failure = "endpoint_occupied_before_launch";
        WriteSummaryLogBestEffort(options, "fail", [], options.RequiredMarkers, [], "web_endpoint_occupied_before_launch");
        Console.Error.WriteLine("TEST_FAIL qemu.smoke web_endpoint_occupied_before_launch");
        return 20;
    }

    if (options.WebEvidence is not null)
        options.WebEvidence.PreLaunchAvailable = true;
    if (options.StaticEvidence is not null)
    {
        if (!CheckWebPortAvailable(options.SocketForwardPort!.Value))
        {
            options.StaticEvidence.Failure = "endpoint_occupied_before_launch";
            WriteSummaryLogBestEffort(options, "fail", [], options.RequiredMarkers, [], "static_endpoint_occupied_before_launch");
            Console.Error.WriteLine("TEST_FAIL qemu.smoke static_endpoint_occupied_before_launch");
            return 23;
        }

        options.StaticEvidence.PreLaunchAvailable = true;
    }

    if (options.ServiceEvidence is not null)
    {
        if (!CheckWebPortAvailable(options.SocketForwardPort!.Value))
        {
            options.ServiceEvidence.Failure = "endpoint_occupied_before_launch";
            WriteSummaryLogBestEffort(options, "fail", [], options.RequiredMarkers, [], "service_endpoint_occupied_before_launch");
            Console.Error.WriteLine("TEST_FAIL qemu.smoke service_endpoint_occupied_before_launch");
            return 21;
        }

        options.ServiceEvidence.PreLaunchAvailable = true;
    }

    try
    {
        writable?.VerifyOriginalAndWorkingIdentity();
        process.Start();
        processStarted = true;
        writable?.BindStartedChild(process);
        prelaunch?.ResumeAfterStart(TimeSpan.FromSeconds(Math.Min(options.TimeoutSeconds, options.StartupTimeoutSeconds)) - stopwatch.Elapsed);
        if (options.StaticEvidence is not null)
        {
            options.StaticEvidence.ProcessId = process.Id;
            options.StaticEvidence.StartUtc = process.StartTime.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        }

        if (options.WebEvidence is not null)
        {
            options.WebEvidence.ProcessId = process.Id;
            options.WebEvidence.StartUtc = process.StartTime.ToUniversalTime().ToString("O");
        }

        if (options.ServiceEvidence is not null)
        {
            options.ServiceEvidence.ProcessId = process.Id;
            options.ServiceEvidence.StartUtc = process.StartTime.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture);
            if (!options.ServiceQuotaFailure)
                serviceControlTask = serviceControls!.RunAsync(webFrames!, options.SerialLogPath, options.EthernetCapturePath!, serviceCancellation!.Token);
        }

        long serialReadOffset = 0;
        string? serialReadFailure = null;
        bool markerSeen = false;
        // Logging markers arise only after the fixed HTTP exercise. Wait for the
        // complete startup prefix before probing, then require the full marker set.
        IReadOnlyList<string> probeMarkers = options.ContinuingWeb ? options.RequiredMarkers.TakeWhile(marker => marker != "TEST_START m10.005").ToArray() : options.RequiredMarkers;
        IReadOnlyList<string> probeOrderedMarkers = options.ContinuingWeb ? options.OrderedMarkers.TakeWhile(marker => marker != "TEST_START m10.005").ToArray() : options.OrderedMarkers;
        bool forbiddenMarkerSeen = false;
        bool timedOut = false;
        bool startupTimedOut = false;
        bool networkPeerInitiatedFailure = false;
        QemuStartupDeadline? startup = options.WatchStartup ? new(options.StartupTimeoutSeconds) : null;
        const long terminalDrainMilliseconds = 750;
        long lastSerialActivityMilliseconds = stopwatch.ElapsedMilliseconds;
        while (true)
        {
            if (prelaunch?.Failure is string startupStderrFailure)
            {
                serialReadFailure = startupStderrFailure;
                TerminateProcessAndClassify(process, ref processTerminationRequested, ref processCleanupFailed, ref qemuExitedSpontaneously, ref qemuExitCode);
                break;
            }

            long previousSerialReadOffset = serialReadOffset;
            if (!TryFeedSerialLogDelta(options.SerialLogPath, streamingMarkers, ref serialReadOffset, out SerialMarkerFeedResult feedResult, out serialReadFailure))
            {
                TerminateProcessAndClassify(process, ref processTerminationRequested, ref processCleanupFailed, ref qemuExitedSpontaneously, ref qemuExitCode);
                break;
            }

            if (!feedResult.Succeeded)
            {
                TerminateProcessAndClassify(process, ref processTerminationRequested, ref processCleanupFailed, ref qemuExitedSpontaneously, ref qemuExitCode);
                break;
            }

            if (serialReadOffset != previousSerialReadOffset)
            {
                lastSerialActivityMilliseconds = stopwatch.ElapsedMilliseconds;
            }

            if (options.ServiceQuotaFailure && streamingMarkers.SuccessTerminalCount != 0)
            {
                ServiceEvidence quota = options.ServiceEvidence!;
                if (quota.TerminalBytes is null)
                    quota.TerminalBytes = serialReadOffset;
                else if (quota.TerminalBytes != serialReadOffset)
                {
                    quota.Failure = "bytes_after_quota_terminal";
                    TerminateProcessAndClassify(process, ref processTerminationRequested, ref processCleanupFailed, ref qemuExitedSpontaneously, ref qemuExitCode);
                    break;
                }
            }

            if (networkPeer is not null && streamingMarkers.HasSeenRequiredMarker("M07_026_ROW stage=3"))
                networkPeer.AllowTcp();
            if (options.StartupTimeoutSeconds <= options.TimeoutSeconds && startup?.HasExpired(stopwatch.ElapsedMilliseconds, streamingMarkers) == true)
            {
                timedOut = startupTimedOut = true;
                if (stdioStartupQmp)
                    QemuStartupCapture.CaptureBestEffort(process, options.SerialLogPath);
                TerminateProcessAndClassify(process, ref processTerminationRequested, ref processCleanupFailed, ref qemuExitedSpontaneously, ref qemuExitCode);
                break;
            }

            if (stopwatch.ElapsedMilliseconds >= options.TimeoutSeconds * 1000L)
            {
                timedOut = true;
                TerminateProcessAndClassify(process, ref processTerminationRequested, ref processCleanupFailed, ref qemuExitedSpontaneously, ref qemuExitCode);
                break;
            }

            if (streamingMarkers.ForbiddenOccurrenceCount != 0)
            {
                forbiddenMarkerSeen = true;
                TerminateProcessAndClassify(process, ref processTerminationRequested, ref processCleanupFailed, ref qemuExitedSpontaneously, ref qemuExitCode);
                break;
            }

            if (streamingMarkers.DuplicateUniqueOccurrenceCount != 0)
            {
                TerminateProcessAndClassify(process, ref processTerminationRequested, ref processCleanupFailed, ref qemuExitedSpontaneously, ref qemuExitCode);
                break;
            }

            if (networkPeer?.Completion.IsFaulted == true || networkPeer?.Completion.IsCanceled == true || containmentPeer?.Completion.IsFaulted == true || containmentPeer?.Completion.IsCanceled == true || webFrames?.Completion.IsCompleted == true || serviceControlTask?.IsFaulted == true || serviceControlTask?.IsCanceled == true)
            {
                networkPeerInitiatedFailure = true;
                if (options.ServiceEvidence is not null)
                    options.ServiceEvidence.Failure = ServiceControlFailure(serviceControlTask) ?? "frame_owner_ended";
                TerminateProcessAndClassify(process, ref processTerminationRequested, ref processCleanupFailed, ref qemuExitedSpontaneously, ref qemuExitCode);
                break;
            }

            if (streamingMarkers.RequiredSeenCount >= probeMarkers.Count)
            {
                markerSeen = true;
                if (options.HttpProbe is not null)
                {
                    break;
                }

                if (stopwatch.ElapsedMilliseconds - lastSerialActivityMilliseconds >= terminalDrainMilliseconds)
                {
                    if (options.ServiceEvidence is not null)
                    {
                        if (!options.ServiceQuotaFailure && serviceControlTask?.IsCompletedSuccessfully != true)
                            options.ServiceEvidence.Failure = ServiceControlFailure(serviceControlTask) ?? "socket_controls_incomplete";
                        else if (streamingMarkers.SuccessTerminalCount != 1 || streamingMarkers.FailureTerminalCount != 0 || streamingMarkers.OutOfOrderOccurrenceCount != 0 || process.HasExited)
                            options.ServiceEvidence.Failure = "terminal_not_healthy";
                        else
                        {
                            if (options.ServiceQuotaFailure)
                                options.ServiceEvidence.QuietMilliseconds = stopwatch.ElapsedMilliseconds - lastSerialActivityMilliseconds;
                            webFrames!.ExpectGuestExit();
                        }
                    }

                    TerminateProcessAndClassify(process, ref processTerminationRequested, ref processCleanupFailed, ref qemuExitedSpontaneously, ref qemuExitCode);
                    break;
                }
            }

            if (process.HasExited)
            {
                break;
            }

            Thread.Sleep(100);
        }

        if (process.HasExited)
        {
            qemuExitCode = process.ExitCode;
            qemuExitedSpontaneously |= !processTerminationRequested;
        }

        HttpProbeResult? httpProbeResult = null;
        string preProbeSerial = string.Empty;
        if (serialReadFailure is null && !TryReadSerialLogBounded(options.SerialLogPath, out preProbeSerial, out serialReadFailure, options.SerialByteCapacity))
        {
            TerminateProcessAndClassify(process, ref processTerminationRequested, ref processCleanupFailed, ref qemuExitedSpontaneously, ref qemuExitCode);
        }

        SerialMarkerSummary preProbeSummary = SerialMarkerSummary.Parse(preProbeSerial, probeMarkers, options.ForbiddenMarkers, probeOrderedMarkers);
        if (options.HttpProbe is not null && !timedOut && preProbeSummary.HasAllRequiredMarkers && !preProbeSummary.HasForbiddenMarkers)
        {
            if (options.ContinuingWeb)
            {
                ContinuingWebEvidence web = options.WebEvidence!;
                HttpProbeResult Probe(HttpProbeOptions probe)
                {
                    DateTime first = DateTime.UtcNow;
                    try
                    {
                        return ExecuteContinuingWebProbe(probe, stopwatch, options.TimeoutSeconds);
                    }
                    finally
                    {
                        web.ProbeWindows.Add((first, DateTime.UtcNow));
                    }
                }

                if (process.HasExited)
                    web.Failure = "premature_exit";
                else
                {
                    web.AliveBeforeHealth = true;
                    httpProbeResult = Probe(options.HttpProbe.Value);
                    web.AliveAfterHealth = !process.HasExited;
                    if (!web.AliveAfterHealth)
                        web.Failure = "premature_exit";
                    else if (httpProbeResult.Value.Passed)
                    {
                        bool serialHealthy = TryFeedSerialLogDelta(options.SerialLogPath, streamingMarkers, ref serialReadOffset, out SerialMarkerFeedResult betweenProbes, out serialReadFailure) && betweenProbes.Succeeded && streamingMarkers.ForbiddenOccurrenceCount == 0 && streamingMarkers.OutOfOrderOccurrenceCount == 0 && streamingMarkers.DuplicateUniqueOccurrenceCount == 0;
                        if (!serialHealthy)
                            web.Failure = "serial_failure";
                        else
                        {
                            web.ConfigResult = Probe(options.WebConfigProbe!.Value);
                            web.AliveAfterConfig = !process.HasExited;
                            if (!web.AliveAfterConfig)
                                web.Failure = "premature_exit";
                            else if (web.ConfigResult.Value.Passed)
                            {
                                // All probes use the same owned process and remaining
                                // deadline. A failed step never advances to a later one.
                                foreach (int step in options.WebVectorProbe is null ? new[]
                                {
                                    0,
                                    1,
                                    3
                                }

                                : new[]
                                {
                                    0,
                                    1,
                                    2,
                                    3
                                }

                                )
                                {
                                    bool healthy = TryFeedSerialLogDelta(options.SerialLogPath, streamingMarkers, ref serialReadOffset, out SerialMarkerFeedResult delta, out serialReadFailure) && delta.Succeeded && streamingMarkers.ForbiddenOccurrenceCount == 0 && streamingMarkers.OutOfOrderOccurrenceCount == 0 && streamingMarkers.DuplicateUniqueOccurrenceCount == 0;
                                    if (!healthy)
                                    {
                                        web.Failure = "serial_failure";
                                        break;
                                    }

                                    HttpProbeOptions probe = step switch
                                    {
                                        0 => options.WebLogProbe!.Value,
                                        1 => options.WebLogRepeatProbe!.Value,
                                        2 => options.WebVectorProbe!.Value,
                                        _ => options.HttpProbe.Value
                                    };
                                    HttpProbeResult result = Probe(probe);
                                    bool alive = !process.HasExited;
                                    if (step == 0)
                                    {
                                        web.LogResult = result;
                                        web.AliveAfterLog = alive;
                                    }
                                    else if (step == 1)
                                    {
                                        web.LogRepeatResult = result;
                                        web.AliveAfterLogRepeat = alive;
                                    }
                                    else if (step == 2)
                                    {
                                        web.VectorResult = result;
                                        web.AliveAfterVectors = alive;
                                    }
                                    else
                                    {
                                        web.HealthAfterResult = result;
                                        web.AliveAfterFinalHealth = alive;
                                    }

                                    if (!alive)
                                    {
                                        web.Failure = "premature_exit";
                                        break;
                                    }

                                    if (!result.Passed)
                                    {
                                        web.Failure = (step == 2 ? "vector_http_" : "logging_http_") + result.Reason;
                                        break;
                                    }
                                }

                                if (web.Failure is null && web.HealthAfterResult?.Passed == true)
                                {
                                    try
                                    {
                                        long remaining = options.TimeoutSeconds * 1000L - stopwatch.ElapsedMilliseconds;
                                        if (remaining < 45_000)
                                            throw new TimeoutException("Web control budget.");
                                        web.Controls = WebSocketControls.RunAsync(options.SocketForwardPort!.Value, webFrames!, options.SerialLogPath, TimeSpan.FromMilliseconds(Math.Min(120_000, remaining))).GetAwaiter().GetResult();
                                        foreach (var exchange in new[]
                                        {
                                            web.Controls.Deadline,
                                            web.Controls.Before,
                                            web.Controls.After
                                        }

                                        )
                                            web.ProbeWindows.Add((exchange.StartedUtc.UtcDateTime, exchange.CompletedUtc.UtcDateTime));
                                        web.AliveAfterControls = !process.HasExited;
                                        if (!web.AliveAfterControls)
                                            web.Failure = "premature_exit";
                                    }
                                    catch (Exception failure) when (failure is IOException or InvalidDataException or OperationCanceledException or TimeoutException or SocketException or InvalidOperationException)
                                    {
                                        web.Failure = "socket_controls_" + failure.GetType().Name;
                                    }

                                    if (web.Failure is null && options.WebRacePipe is not null)
                                    {
                                        try
                                        {
                                            long remaining = options.TimeoutSeconds * 1000L - stopwatch.ElapsedMilliseconds;
                                            if (!options.WebEventOwnedSignalRace && remaining < 35_000)
                                                throw new TimeoutException("Web race handoff budget.");
                                            bool Healthy() => !process.HasExited && !webFrames!.Completion.IsCompleted && TryFeedSerialLogDelta(options.SerialLogPath, streamingMarkers, ref serialReadOffset, out SerialMarkerFeedResult delta, out serialReadFailure) && delta.Succeeded && streamingMarkers.ForbiddenOccurrenceCount == 0 && streamingMarkers.OutOfOrderOccurrenceCount == 0 && streamingMarkers.DuplicateUniqueOccurrenceCount == 0;
                                            web.RaceGate = options.WebEventOwnedSignalRace ? WebRaceRendezvous.WaitEventOwnedInRunnerAsync(options.WebRacePipe, options.SocketForwardPort!.Value, process.Id, TimeSpan.FromSeconds(165), () => TimeSpan.FromMilliseconds(options.TimeoutSeconds * 1000L - stopwatch.ElapsedMilliseconds), Healthy).GetAwaiter().GetResult() : WebRaceRendezvous.WaitInRunnerAsync(options.WebRacePipe, options.SocketForwardPort!.Value, process.Id, TimeSpan.FromMilliseconds(Math.Min(45_000, remaining)), Healthy).GetAwaiter().GetResult();
                                            web.AliveAfterRaceGate = Healthy();
                                            if (!web.AliveAfterRaceGate)
                                                web.Failure = "race_gate_guest_unhealthy";
                                        }
                                        catch (Exception failure) when (failure is IOException or InvalidDataException or OperationCanceledException or TimeoutException or SocketException or InvalidOperationException or UnauthorizedAccessException)
                                        {
                                            web.Failure = options.WebEventOwnedSignalRace && failure is InvalidDataException { Message: "web_race_insufficient_absolute_budget" } ? "race_gate_insufficient_absolute_budget" : "race_gate_" + failure.GetType().Name;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else
                httpProbeResult = ExecuteHttpProbe(options.HttpProbe.Value);
            webFrames?.ExpectGuestExit();
            TerminateProcessAndClassify(process, ref processTerminationRequested, ref processCleanupFailed, ref qemuExitedSpontaneously, ref qemuExitCode);
            if (process.HasExited)
            {
                qemuExitCode = process.ExitCode;
            }
        }

        prelaunch?.Complete();
        if (writable is not null)
        {
            if (!process.HasExited)
            {
                processCleanupFailed = true;
                AwaitWritableChildJoin(process);
            }

            try
            {
                writable.CompleteAfterChildJoin();
            }
            catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
            {
                WriteSummaryLogBestEffort(options, "fail", [], options.RequiredMarkers, [], "writable_data_identity_changed");
                Console.Error.WriteLine("TEST_FAIL qemu.smoke writable_data_identity_changed type=" + error.GetType().Name);
                return 24;
            }
        }

        if (options.StaticEvidence is not null)
        {
            var receipt = options.StaticEvidence;
            receipt.TerminationRequested = processTerminationRequested;
            receipt.DeathConfirmed = process.HasExited;
            receipt.SpontaneousExit = qemuExitedSpontaneously;
            receipt.CleanupFailed = processCleanupFailed;
            receipt.ExitCode = qemuExitCode;
            if (receipt.DeathConfirmed)
            {
                receipt.EndUtc = process.ExitTime.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture);
                receipt.LifetimeMilliseconds = (long)(process.ExitTime - process.StartTime).TotalMilliseconds;
                receipt.StoppedEndpointRefused = CheckWebEndpointRefused(options.SocketForwardPort!.Value);
            }

            if (receipt.CleanupFailed)
                receipt.Failure = "child_cleanup_failed";
            else if (receipt.SpontaneousExit)
                receipt.Failure = "premature_exit";
            else if (!receipt.DeathConfirmed || !receipt.TerminationRequested)
                receipt.Failure = "termination_unconfirmed";
            else if (receipt.StoppedEndpointRefused != true)
                receipt.Failure = "endpoint_live_after_stop";
        }

        if (options.WebEvidence is not null)
        {
            ContinuingWebEvidence web = options.WebEvidence;
            web.TerminationRequested = processTerminationRequested;
            web.DeathConfirmed = process.HasExited;
            web.SpontaneousExit = qemuExitedSpontaneously;
            web.ExitCode = qemuExitCode;
            if (web.DeathConfirmed)
            {
                web.EndUtc = process.ExitTime.ToUniversalTime().ToString("O");
                web.LifetimeMilliseconds = (long)(process.ExitTime - process.StartTime).TotalMilliseconds;
                web.StoppedEndpointRefused = CheckWebEndpointRefused(options.SocketForwardPort!.Value);
            }

            if (web.SpontaneousExit)
                web.Failure = "premature_exit";
            else if (!web.DeathConfirmed || !web.TerminationRequested)
                web.Failure = "termination_unconfirmed";
            else if (web.StoppedEndpointRefused != true)
                web.Failure = "endpoint_live_after_stop";
        }

        if (options.ServiceEvidence is not null)
        {
            ServiceEvidence service = options.ServiceEvidence;
            if (!options.ServiceQuotaFailure)
            {
                if (serviceControlTask?.IsCompletedSuccessfully != true)
                    service.Failure ??= ServiceControlFailure(serviceControlTask) ?? "socket_controls_incomplete";
                if (!JoinServiceControls(serviceControlTask, serviceCancellation!))
                    service.Failure ??= "socket_controls_join_failed";
                if (serviceControlTask?.IsCompletedSuccessfully == true)
                    service.Controls = serviceControlTask.GetAwaiter().GetResult();
            }
            else
            {
                service.TerminationRequested = processTerminationRequested;
                service.DeathConfirmed = process.HasExited;
                service.SpontaneousExit = qemuExitedSpontaneously;
                service.CleanupFailed = processCleanupFailed;
                service.ExitCode = qemuExitCode;
                if (processCleanupFailed)
                    service.Failure ??= "qemu_cleanup_failed";
            }

            if (process.HasExited)
            {
                service.EndUtc = process.ExitTime.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture);
                service.LifetimeMilliseconds = (long)(process.ExitTime - process.StartTime).TotalMilliseconds;
                service.StoppedEndpointRefused = CheckWebEndpointRefused(options.SocketForwardPort!.Value);
            }

            if (qemuExitedSpontaneously)
                service.Failure ??= "premature_exit";
            else if (!process.HasExited || !processTerminationRequested)
                service.Failure ??= "termination_unconfirmed";
            else if (service.StoppedEndpointRefused != true)
                service.Failure ??= "endpoint_live_after_stop";
        }

        if (serialReadFailure is null)
        {
            TryFeedSerialLogDelta(options.SerialLogPath, streamingMarkers, ref serialReadOffset, out _, out serialReadFailure);
        }

        markerSeen = streamingMarkers.RequiredSeenCount == streamingMarkers.RequiredMarkerCount;
        SerialMarkerCompletionResult streamingResult = streamingMarkers.Complete(timedOut);
        Console.WriteLine("SERIAL_MARKER_STREAM status=" + streamingResult.Status + " terminal=" + streamingResult.TerminalState + " bytes=" + streamingResult.TotalBytes + " lines=" + streamingResult.LinesProcessed + " invalidBytes=" + streamingResult.InvalidByteCount + " invalidLines=" + streamingResult.InvalidNoiseLineCount + " orderedSeen=" + streamingResult.OrderedSeenCount + " outOfOrder=" + streamingResult.OutOfOrderOccurrenceCount + " uniqueSeen=" + streamingResult.UniqueSeenCount + " duplicateUnique=" + streamingResult.DuplicateUniqueOccurrenceCount);
        Console.WriteLine("SERIAL_MARKER_DRAIN milliseconds=" + terminalDrainMilliseconds);
        Console.WriteLine("QEMU_PROCESS_RESULT disposition=" + (processCleanupFailed ? "cleanup-failed" : processTerminationRequested ? "runner-terminated" : qemuExitedSpontaneously ? "exited" : "running") + " exitCode=" + (qemuExitCode.HasValue ? qemuExitCode.Value.ToString() : "none"));
        string serial = string.Empty;
        if (serialReadFailure is null)
        {
            TryReadSerialLogBounded(options.SerialLogPath, out serial, out serialReadFailure, options.SerialByteCapacity);
        }

        SerialMarkerSummary summary = SerialMarkerSummary.Parse(serial, options.RequiredMarkers, options.ForbiddenMarkers, options.OrderedMarkers);
        IReadOnlyList<string> observedMarkerLines = FindObservedRunnerMarkerLines(serial, options);
        IReadOnlyList<string> forbiddenMarkers = summary.ForbiddenMarkerHits;
        IReadOnlyList<string> streamingOutOfOrderMarkers = streamingResult.OutOfOrderMarkerHits ?? Array.Empty<string>();
        IReadOnlyList<string> outOfOrderMarkers = MergeDistinctMarkers(summary.OutOfOrderMarkers, streamingOutOfOrderMarkers);
        IReadOnlyList<string> duplicateUniqueMarkers = streamingResult.DuplicateUniqueMarkerHits ?? Array.Empty<string>();
        string? networkPeerFailure = null;
        if (options.ContinuingWeb && webFrames is not null)
        {
            var capture = webFrames.Finish();
            options.WebEvidence!.Frames = capture;
            if (!capture.Stopped || !capture.Healthy)
                options.WebEvidence.Failure = "web_frame_owner_failed";
        }

        if (options.ServiceEvidence is not null)
        {
            if (options.ServiceQuotaFailure && streamingResult.SuccessTerminalCount != 0)
                options.ServiceEvidence.Failure ??= ServiceQuotaCapture.Failure(serial);
            var frames = webFrames!.FinishService();
            options.ServiceEvidence.Frames = frames;
            if (!frames.Session.Stopped || !frames.Session.Healthy || !frames.Session.GuestExitExpected)
                options.ServiceEvidence.Failure ??= "frame_owner_failed";
            if (options.ServiceQuotaFailure && (frames.Session.ForwardedFrames <= 0 || frames.ControlledPorts.Count != 0 || frames.Injections.Count != 0 || frames.Session.DroppedFrames.Count != 0 || frames.Session.VlanFrames != 0))
                options.ServiceEvidence.Failure ??= "quota_frame_owner_shape";
            StringBuilder serviceRows = new();
            AppendServiceEvidence(serviceRows, options.ServiceEvidence);
            Console.Write(serviceRows.ToString());
        }

        if (containmentPeer is not null)
        {
            bool completed = containmentPeer.Completion.IsCompletedSuccessfully;
            bool stopped = containmentPeer.Stop();
            Console.WriteLine("SOCKET_CONTAINMENT_PEER_RESULT complete=" + completed + " stopped=" + stopped + " arpReplies=" + containmentPeer.ArpReplies + " headTransmissions=" + containmentPeer.HeadTransmissions + " healthyReplies=" + containmentPeer.HealthyReplies + " vlanFrames=" + containmentPeer.VlanFrames);
            if (!completed || !stopped)
                networkPeerFailure = !stopped ? "socket_containment_peer_cleanup_failed" : containmentPeer.Completion.IsFaulted ? "socket_containment_peer_failed_" + containmentPeer.Completion.Exception!.GetBaseException().GetType().Name : "socket_containment_peer_incomplete";
        }

        if (networkPeer is not null)
        {
            bool completed = networkPeer.Completion.IsCompletedSuccessfully;
            bool stopped = networkPeer.Stop();
            Console.WriteLine("NETWORK_PEER_RESULT complete=" + completed + " stopped=" + stopped + " arpReplies=" + networkPeer.ArpReplies + " echoReplies=" + networkPeer.EchoReplies + " tcpPackets=" + networkPeer.TcpPackets);
            if (!completed || !stopped)
            {
                networkPeerFailure = !stopped ? "network_peer_cleanup_failed" : networkPeer.Completion.IsFaulted ? "network_peer_failed_" + networkPeer.Completion.Exception!.GetBaseException().GetType().Name : "network_peer_incomplete";
            }
        }

        if (serialReadFailure is not null || IsStreamingStructuralFailure(streamingResult.Status))
        {
            string failureReason = serialReadFailure ?? "serial_marker_stream_" + streamingResult.Status.ToString().ToLowerInvariant();
            WriteSummaryLogBestEffort(options, "fail", observedMarkerLines, summary.MissingMarkers, forbiddenMarkers, failureReason, httpProbeResult, summary.AppExitResults, summary.MalformedAppExitMarkers, outOfOrderMarkers, duplicateUniqueMarkers);
            Console.Error.WriteLine("TEST_FAIL qemu.smoke serial_marker_stream status=" + streamingResult.Status + " reason=" + failureReason);
            return 13;
        }

        if (forbiddenMarkerSeen || streamingResult.Status == SerialMarkerCompletionStatus.ForbiddenMarker || forbiddenMarkers.Count != 0)
        {
            WriteSummaryLogBestEffort(options, "fail", observedMarkerLines, summary.MissingMarkers, forbiddenMarkers, "forbidden_marker", httpProbeResult, summary.AppExitResults, summary.MalformedAppExitMarkers, outOfOrderMarkers, duplicateUniqueMarkers);
            string firstForbiddenMarker = forbiddenMarkers.Count == 0 ? "unknown" : forbiddenMarkers[0];
            Console.Error.WriteLine("OS_SMOKE_FAIL forbidden-marker=" + firstForbiddenMarker);
            Console.Error.WriteLine("TEST_FAIL qemu.smoke forbidden_marker marker=" + firstForbiddenMarker);
            return 8;
        }

        bool hasStreamingOrderViolation = streamingResult.Status == SerialMarkerCompletionStatus.OutOfOrderMarker || streamingOutOfOrderMarkers.Count != 0;
        if (summary.MissingMarkers.Count == 0 && (hasStreamingOrderViolation || summary.OutOfOrderMarkers.Count != 0))
        {
            WriteSummaryLogBestEffort(options, "fail", observedMarkerLines, summary.MissingMarkers, Array.Empty<string>(), "out_of_order_marker", httpProbeResult, summary.AppExitResults, summary.MalformedAppExitMarkers, outOfOrderMarkers, duplicateUniqueMarkers);
            Console.Error.WriteLine("TEST_FAIL qemu.smoke out_of_order_marker markers=" + FormatMarkers(outOfOrderMarkers));
            return 10;
        }

        if (streamingResult.Status == SerialMarkerCompletionStatus.DuplicateUniqueMarker || duplicateUniqueMarkers.Count != 0)
        {
            WriteSummaryLogBestEffort(options, "fail", observedMarkerLines, summary.MissingMarkers, Array.Empty<string>(), "duplicate_unique_marker", httpProbeResult, summary.AppExitResults, summary.MalformedAppExitMarkers, outOfOrderMarkers, duplicateUniqueMarkers);
            Console.Error.WriteLine("TEST_FAIL qemu.smoke duplicate_unique_marker markers=" + FormatMarkers(duplicateUniqueMarkers));
            return 17;
        }

        if (processCleanupFailed)
        {
            WriteSummaryLogBestEffort(options, "fail", observedMarkerLines, summary.MissingMarkers, forbiddenMarkers, "qemu_cleanup_failed", httpProbeResult, summary.AppExitResults, summary.MalformedAppExitMarkers, outOfOrderMarkers, duplicateUniqueMarkers);
            Console.Error.WriteLine("TEST_FAIL qemu.smoke qemu_cleanup_failed");
            return 15;
        }

        if (options.ContinuingWeb && options.WebEvidence!.Failure is string webFailure)
        {
            WriteSummaryLogBestEffort(options, "fail", observedMarkerLines, summary.MissingMarkers, forbiddenMarkers, "web_" + webFailure, httpProbeResult, summary.AppExitResults, summary.MalformedAppExitMarkers, outOfOrderMarkers, duplicateUniqueMarkers);
            Console.Error.WriteLine("TEST_FAIL qemu.smoke web_" + webFailure);
            return 20;
        }

        if (options.ServiceEvidence?.Failure is string serviceFailure)
        {
            WriteSummaryLogBestEffort(options, "fail", observedMarkerLines, summary.MissingMarkers, forbiddenMarkers, "service_" + serviceFailure, httpProbeResult, summary.AppExitResults, summary.MalformedAppExitMarkers, outOfOrderMarkers, duplicateUniqueMarkers);
            Console.Error.WriteLine("TEST_FAIL qemu.smoke service_" + serviceFailure);
            return 21;
        }

        if (options.StaticEvidence?.Failure is string staticFailure)
        {
            WriteSummaryLogBestEffort(options, "fail", observedMarkerLines, summary.MissingMarkers, forbiddenMarkers, "static_" + staticFailure, httpProbeResult, summary.AppExitResults, summary.MalformedAppExitMarkers, outOfOrderMarkers, duplicateUniqueMarkers);
            Console.Error.WriteLine("TEST_FAIL qemu.smoke static_" + staticFailure);
            return 23;
        }

        if (qemuExitedSpontaneously && qemuExitCode is not null and not 0)
        {
            WriteSummaryLogBestEffort(options, "fail", observedMarkerLines, summary.MissingMarkers, forbiddenMarkers, "qemu_exit_nonzero", httpProbeResult, summary.AppExitResults, summary.MalformedAppExitMarkers, outOfOrderMarkers, duplicateUniqueMarkers);
            Console.Error.WriteLine("TEST_FAIL qemu.smoke qemu_exit_nonzero code=" + qemuExitCode.Value);
            return 14;
        }

        bool uniqueEvidenceMode = options.UniqueMarkers.Count != 0;
        if (uniqueEvidenceMode && qemuExitedSpontaneously && qemuExitCode == 0 && markerSeen && streamingResult.Passed && summary.HasAllRequiredMarkers)
        {
            WriteSummaryLogBestEffort(options, "fail", observedMarkerLines, summary.MissingMarkers, Array.Empty<string>(), "qemu_reboot_or_shutdown", httpProbeResult, summary.AppExitResults, summary.MalformedAppExitMarkers, outOfOrderMarkers, duplicateUniqueMarkers);
            Console.Error.WriteLine("TEST_FAIL qemu.smoke qemu_reboot_or_shutdown exitCode=0");
            return 18;
        }

        bool serialSucceeded = markerSeen && streamingResult.Passed && streamingResult.TerminalState == SerialMarkerTerminalState.Success && streamingResult.SuccessTerminalCount == 1 && summary.HasAllRequiredMarkers;
        // Stopping QEMU for a primary failure can itself break the peer transport.
        // Reject an initiating peer failure or an otherwise successful serial run;
        // retain the original guest, process and timeout diagnosis otherwise.
        if (networkPeerFailure is not null && !timedOut && httpProbeResult?.Passed != false && ((networkPeerInitiatedFailure && !qemuExitedSpontaneously) || serialSucceeded))
        {
            WriteSummaryLogBestEffort(options, "fail", observedMarkerLines, summary.MissingMarkers, forbiddenMarkers, networkPeerFailure, httpProbeResult, summary.AppExitResults, summary.MalformedAppExitMarkers, outOfOrderMarkers, duplicateUniqueMarkers);
            Console.Error.WriteLine("TEST_FAIL qemu.smoke " + networkPeerFailure);
            return 19;
        }

        if (options.ServiceQuotaFailure && serialSucceeded)
        {
            ServiceEvidence quota = options.ServiceEvidence!;
            if (quota.QuietMilliseconds is not >= 750 || quota.TerminalBytes != streamingResult.TotalBytes || !quota.TerminationRequested || !quota.DeathConfirmed || quota.SpontaneousExit || quota.CleanupFailed || quota.ExitCode is null)
            {
                WriteSummaryLogBestEffort(options, "fail", observedMarkerLines, [], [], "quota_capture_incomplete");
                Console.Error.WriteLine("TEST_FAIL qemu.smoke quota_capture_incomplete");
                return 21;
            }

            if (!WriteSummaryLogBestEffort(options, "expected-quota", observedMarkerLines, [], [], "none", httpProbeResult, summary.AppExitResults, summary.MalformedAppExitMarkers, outOfOrderMarkers, duplicateUniqueMarkers))
            {
                Console.Error.WriteLine("TEST_FAIL qemu.smoke quota_summary_failed");
                return 21;
            }

            Console.WriteLine("QEMU_EXPECTED_SERVICE_QUOTA outcome=captured acceptance=pending");
            return 22;
        }

        if (serialSucceeded)
        {
            if (options.ContinuingWeb && httpProbeResult?.Passed == true && options.WebEvidence!.ConfigResult?.Passed != true)
            {
                string reason = options.WebEvidence.ConfigResult?.Reason ?? "not_run";
                WriteSummaryLogBestEffort(options, "fail", observedMarkerLines, [], [], "web_config_" + reason, httpProbeResult, summary.AppExitResults, summary.MalformedAppExitMarkers, outOfOrderMarkers, duplicateUniqueMarkers);
                Console.Error.WriteLine("TEST_FAIL qemu.smoke web_config_" + reason);
                return 20;
            }

            if (options.ContinuingWeb && httpProbeResult?.Passed == true && (options.WebEvidence!.LogResult?.Passed != true || options.WebEvidence.LogRepeatResult?.Passed != true || (options.WebVectorProbe is not null && options.WebEvidence.VectorResult?.Passed != true) || options.WebEvidence.HealthAfterResult?.Passed != true))
            {
                WriteSummaryLogBestEffort(options, "fail", observedMarkerLines, [], [], "web_logging_incomplete", httpProbeResult, summary.AppExitResults, summary.MalformedAppExitMarkers, outOfOrderMarkers, duplicateUniqueMarkers);
                Console.Error.WriteLine("TEST_FAIL qemu.smoke web_logging_incomplete");
                return 20;
            }

            if (options.HttpProbe is not null)
            {
                if (!options.ContinuingWeb)
                    httpProbeResult ??= ExecuteHttpProbe(options.HttpProbe.Value);
                TerminateProcessAndClassify(process, ref processTerminationRequested, ref processCleanupFailed, ref qemuExitedSpontaneously, ref qemuExitCode);
                if (httpProbeResult?.Passed != true)
                {
                    HttpProbeResult failedProbe = httpProbeResult ?? HttpProbeResult.Failed("not_run", 0, string.Empty);
                    WriteSummaryLogBestEffort(options, "fail", observedMarkerLines, Array.Empty<string>(), Array.Empty<string>(), "http_probe_" + failedProbe.Reason, httpProbeResult, summary.AppExitResults, summary.MalformedAppExitMarkers, outOfOrderMarkers, duplicateUniqueMarkers);
                    Console.Error.WriteLine("OS_SMOKE_FAIL http-probe=" + failedProbe.Reason);
                    Console.Error.WriteLine("TEST_FAIL qemu.runner.http_probe reason=" + failedProbe.Reason);
                    return 9;
                }
            }

            if (processCleanupFailed)
            {
                WriteSummaryLogBestEffort(options, "fail", observedMarkerLines, summary.MissingMarkers, forbiddenMarkers, "qemu_cleanup_failed", httpProbeResult, summary.AppExitResults, summary.MalformedAppExitMarkers, outOfOrderMarkers, duplicateUniqueMarkers);
                Console.Error.WriteLine("TEST_FAIL qemu.smoke qemu_cleanup_failed");
                return 15;
            }

            if (qemuExitedSpontaneously && qemuExitCode is not null and not 0)
            {
                WriteSummaryLogBestEffort(options, "fail", observedMarkerLines, summary.MissingMarkers, forbiddenMarkers, "qemu_exit_nonzero", httpProbeResult, summary.AppExitResults, summary.MalformedAppExitMarkers, outOfOrderMarkers, duplicateUniqueMarkers);
                Console.Error.WriteLine("TEST_FAIL qemu.smoke qemu_exit_nonzero code=" + qemuExitCode.Value);
                return 14;
            }

            WriteSummaryLogBestEffort(options, "pass", observedMarkerLines, Array.Empty<string>(), Array.Empty<string>(), "none", httpProbeResult, summary.AppExitResults, summary.MalformedAppExitMarkers, outOfOrderMarkers, duplicateUniqueMarkers);
            Console.WriteLine("TEST_PASS qemu.smoke");
            return 0;
        }

        if (streamingResult.Status == SerialMarkerCompletionStatus.MissingSuccessTerminal)
        {
            WriteSummaryLogBestEffort(options, "fail", observedMarkerLines, summary.MissingMarkers, Array.Empty<string>(), "missing_success_terminal", httpProbeResult, summary.AppExitResults, summary.MalformedAppExitMarkers, outOfOrderMarkers, duplicateUniqueMarkers);
            Console.Error.WriteLine("TEST_FAIL qemu.smoke missing_success_terminal marker=" + options.SuccessTerminalMarker);
            return 16;
        }

        IReadOnlyList<string> missingMarkers = streamingResult.Status == SerialMarkerCompletionStatus.MissingRequiredMarker && streamingResult.MissingRequiredMarkers is not null ? streamingResult.MissingRequiredMarkers : summary.MissingMarkers;
        if (timedOut)
        {
            string timeoutReason = startupTimedOut ? "startup_timeout" : "timeout";
            WriteSummaryLogBestEffort(options, "fail", observedMarkerLines, missingMarkers, Array.Empty<string>(), timeoutReason, httpProbeResult, summary.AppExitResults, summary.MalformedAppExitMarkers, outOfOrderMarkers, duplicateUniqueMarkers);
            Console.Error.WriteLine("TEST_FAIL qemu.smoke " + timeoutReason);
            return 4;
        }

        WriteSummaryLogBestEffort(options, "fail", observedMarkerLines, missingMarkers, Array.Empty<string>(), "missing_marker", httpProbeResult, summary.AppExitResults, summary.MalformedAppExitMarkers, outOfOrderMarkers, duplicateUniqueMarkers);
        Console.Error.WriteLine("TEST_FAIL qemu.smoke missing_marker required=" + FormatMarkers(options.RequiredMarkers) + " missing=" + FormatMarkers(missingMarkers));
        return 5;
    }
    finally
    {
        try
        {
            if (processStarted && !process.HasExited)
            {
                var terminationResult = TerminateProcessBounded(process);
                if (!terminationResult.Succeeded)
                {
                    Console.Error.WriteLine("QEMU_PROCESS_CLEANUP_FAILED timeout_ms=2000");
                }
            }
        }
        finally
        {
            try
            {
                if (writable is not null && processStarted)
                {
                    AwaitWritableChildJoin(process);
                    if (!writable.FinalVerificationAttempted)
                        writable.CompleteAfterChildJoin();
                }
            }
            finally
            {
                try
                {
                    prelaunch?.Complete();
                }
                finally
                {
                    if (serviceCancellation is not null && !JoinServiceControls(serviceControlTask, serviceCancellation))
                        throw new TimeoutException("Service controls join incomplete; reserved ports remain owned.");
                }
            }
        }
    }
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine("TEST_FAIL qemu.runner.invalid_args " + ex.Message);
    return 1;
}

static void AwaitWritableChildJoin(Process process)
{
    if (!process.WaitForExit(0))
    {
        Console.Error.WriteLine("TEST_FAIL qemu.smoke writable_child_join_unconfirmed");
        Console.Error.WriteLine("WRITABLE_DATA_RETAINED reason=child_unjoined handles=open runner=retained");
        // Active execution and kill attempts are bounded elsewhere. An unresolved
        // child retains this runner and its handles until actual join. The outer
        // owner may terminate the tree; handles cannot survive a killed runner.
        while (!process.WaitForExit(250))
        {
        }
    }
}

static int RunExpectedUnexpectedVectorPanic(RunnerOptions options, string qemu, string ovmf)
{
    bool owned = options.ExpectedOwnedSubsystemPanic;
    bool unhandled = options.ExpectedUnhandledException;
    string profile = unhandled ? "m04.unhandled-exception" : owned ? "m03.owned-subsystem" : "m02.unexpected-vector";
    string acceptedReason = unhandled ? "expected_unhandled_exception" : owned ? "expected_owned_subsystem_panic" : "expected_unexpected_vector_panic";
    int Evaluate(string text, out string prefix) => unhandled ? UnhandledExceptionCapture.Evaluate(text, out prefix) : owned ? OwnedSubsystemPanicCapture.Evaluate(text, out prefix) : UnexpectedVectorPanicCapture.Evaluate(text, out prefix);
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.SerialLogPath)) ?? ".");
    if (File.Exists(options.SerialLogPath))
        File.Delete(options.SerialLogPath);
    if (!string.IsNullOrWhiteSpace(options.SummaryLogPath) && File.Exists(options.SummaryLogPath))
        File.Delete(options.SummaryLogPath);
    Console.WriteLine("TEST_START qemu.smoke");
    Console.WriteLine("IMAGE_ARTIFACT " + options.ImagePath);
    Console.WriteLine("SERIAL_LOG " + options.SerialLogPath);
    Console.WriteLine("OVMF_CODE " + ovmf);
    Console.WriteLine("WORKLOAD_PROFILE " + options.WorkloadProfile);
    if (options.FirmwareRngSource is not null)
        Console.WriteLine("FIRMWARE_RNG_SOURCE " + options.FirmwareRngSource);
    Console.WriteLine("MARKER_PRESETS " + FormatSummaryList(options.MarkerPresets));
    Console.WriteLine("REQUIRED_MARKERS " + FormatMarkers(options.RequiredMarkers));
    Console.WriteLine("ORDERED_MARKERS " + FormatSummaryList(options.OrderedMarkers));
    Console.WriteLine("UNIQUE_MARKERS " + FormatSummaryList(options.UniqueMarkers));
    Console.WriteLine("FORBIDDEN_MARKERS " + FormatSummaryList(options.ForbiddenMarkers));
    Console.WriteLine("EXPECTED_PANIC_PROFILE " + profile);
    using Process process = new Process();
    process.StartInfo.FileName = qemu;
    process.StartInfo.UseShellExecute = false;
    process.StartInfo.CreateNoWindow = true;
    foreach (string argument in QemuCommandBuilder.BuildArguments(options, ovmf))
        process.StartInfo.ArgumentList.Add(argument);
    using WhpxPrelaunch? prelaunch = WhpxPrelaunch.Create(process, options.Accelerator, options.SerialLogPath);
    bool started = false, terminated = false, cleanupFailed = false, spontaneous = false;
    int? nativeExit = null;
    string serial = string.Empty, prior = string.Empty;
    string? terminal = null, failure = null;
    long terminalAt = -1, measuredQuiet = 0;
    SerialMarkerCompletionResult prefixResult = default;
    const long quietMilliseconds = 750;
    Stopwatch clock = Stopwatch.StartNew();
    try
    {
        process.Start();
        started = true;
        prelaunch?.ResumeAfterStart(TimeSpan.FromSeconds(options.TimeoutSeconds) - clock.Elapsed);
        while (failure is null)
        {
            if (prelaunch?.Failure is string startupStderrFailure)
            {
                failure = startupStderrFailure;
                break;
            }

            if (!TryReadSerialLogBounded(options.SerialLogPath, out serial, out failure))
                break;
            if (!serial.StartsWith(prior, StringComparison.Ordinal))
            {
                failure = "serial_log_reset";
                break;
            }

            if (terminal is not null && !string.Equals(serial, terminal, StringComparison.Ordinal))
            {
                failure = "bytes_after_expected_panic";
                break;
            }

            prior = serial;
            int parsed = Evaluate(serial, out string prerequisites);
            if (parsed < 0)
            {
                failure = "unexpected_vector_panic_capture_rejected";
                break;
            }

            if (parsed == 1 && terminal is null)
            {
                // Only the independently classified exact last panic record is excluded.
                // Generic required/ordered/unique/forbidden handling still checks the prefix.
                var state = new SerialMarkerStateMachine(options.RequiredMarkers, options.ForbiddenMarkers, successTerminalMarker: unhandled ? UnhandledExceptionCapture.PrerequisiteTerminal : SerialMarkerStateMachine.DefaultSuccessTerminalMarker, orderedMarkers: options.OrderedMarkers, uniqueMarkers: options.UniqueMarkers);
                var feed = state.Feed(Encoding.ASCII.GetBytes(prerequisites));
                prefixResult = state.Complete(false);
                if (!feed.Succeeded || !prefixResult.Passed)
                {
                    failure = "unexpected_vector_panic_prefix_markers";
                    break;
                }

                terminal = serial;
                terminalAt = clock.ElapsedMilliseconds;
            }

            if (clock.ElapsedMilliseconds >= options.TimeoutSeconds * 1000L)
            {
                failure = "timeout";
                break;
            }

            if (process.HasExited)
            {
                spontaneous = true;
                nativeExit = process.ExitCode;
                failure = "qemu_reboot_or_shutdown";
                break;
            }

            if (terminal is not null && clock.ElapsedMilliseconds - terminalAt >= quietMilliseconds)
            {
                measuredQuiet = clock.ElapsedMilliseconds - terminalAt;
                break;
            }

            Thread.Sleep(25);
        }

        TerminateProcessAndClassify(process, ref terminated, ref cleanupFailed, ref spontaneous, ref nativeExit);
        prelaunch?.Complete();
        if (cleanupFailed)
            failure = "qemu_cleanup_failed";
        else if (spontaneous || !terminated || nativeExit is null)
            failure = "qemu_reboot_or_shutdown";
        if (!TryReadSerialLogBounded(options.SerialLogPath, out string finalSerial, out string? readFailure))
            failure = readFailure ?? "serial_log_read_failed";
        else if (!string.Equals(finalSerial, serial, StringComparison.Ordinal))
            failure = "bytes_after_expected_panic";
        serial = finalSerial;
        if (failure is null && (terminal is null || measuredQuiet < quietMilliseconds || !string.Equals(serial, terminal, StringComparison.Ordinal) || Evaluate(serial, out _) != 1))
            failure = "unexpected_vector_panic_incomplete";
        bool accepted = failure is null;
        Console.WriteLine("SERIAL_MARKER_STREAM status=" + (accepted ? "ExpectedPanic" : "Rejected") + " terminal=" + (accepted ? "ExpectedPanic" : "Rejected") + " bytes=" + Encoding.UTF8.GetByteCount(serial) + " lines=" + (accepted ? unhandled ? UnhandledExceptionCapture.RecordCount : owned ? 79 : OS.Tooling.QemuContracts.M02UnexpectedCaptureRecordCount : 0) + " invalidBytes=" + (accepted ? owned || unhandled ? OwnedSubsystemPanicCapture.InvalidBytes(serial).ToString(System.Globalization.CultureInfo.InvariantCulture) : "0" : "unknown") + " invalidLines=" + (accepted ? unhandled ? serial.Split("\r\n", StringSplitOptions.None).Count(l => System.Text.RegularExpressions.Regex.IsMatch(l, @"[\x00-\x1f\x7f-\uffff]")).ToString(System.Globalization.CultureInfo.InvariantCulture) : owned ? "1" : "0" : "unknown") + " orderedSeen=" + prefixResult.OrderedSeenCount + " outOfOrder=" + prefixResult.OutOfOrderOccurrenceCount + " uniqueSeen=" + prefixResult.UniqueSeenCount + " duplicateUnique=" + prefixResult.DuplicateUniqueOccurrenceCount);
        Console.WriteLine("SERIAL_MARKER_DRAIN milliseconds=750 observedMilliseconds=" + measuredQuiet);
        Console.WriteLine("QEMU_PROCESS_RESULT disposition=" + (cleanupFailed ? "cleanup-failed" : terminated ? "runner-terminated" : spontaneous ? "exited" : "running") + " exitCode=" + (nativeExit.HasValue ? nativeExit.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "none"));
        string reason = accepted ? acceptedReason : failure!;
        if (!WriteExpectedPanicSummary(options, serial, accepted, reason, measuredQuiet, terminated, spontaneous, cleanupFailed, nativeExit))
        {
            Console.Error.WriteLine("TEST_FAIL qemu.smoke expected_panic_summary_failed");
            return 16;
        }

        if (!accepted)
        {
            Console.Error.WriteLine("TEST_FAIL qemu.smoke " + reason);
            return failure == "timeout" ? 4 : cleanupFailed ? 15 : spontaneous ? 18 : 8;
        }

        Console.WriteLine("QEMU_EXPECTED_PANIC outcome=expected-panic reason=" + acceptedReason);
        return 19;
    }
    finally
    {
        if (started && !process.HasExited && !TerminateProcessBounded(process).Succeeded)
            Console.Error.WriteLine("QEMU_PROCESS_CLEANUP_FAILED timeout_ms=2000");
        prelaunch?.Complete();
    }
}

static bool WriteExpectedPanicSummary(RunnerOptions options, string serial, bool accepted, string reason, long quiet, bool terminated, bool spontaneous, bool cleanupFailed, int? nativeExit)
{
    if (string.IsNullOrWhiteSpace(options.SummaryLogPath))
        return false;
    try
    {
        var b = new StringBuilder();
        b.Append("QEMU_SMOKE_STATUS ").AppendLine(accepted ? "expected-panic" : "fail");
        b.Append("IMAGE_ARTIFACT ").AppendLine(options.ImagePath);
        b.Append("SERIAL_LOG ").AppendLine(options.SerialLogPath);
        b.Append("WORKLOAD_PROFILE ").AppendLine(options.WorkloadProfile);
        if (options.FirmwareRngSource is not null)
            b.Append("FIRMWARE_RNG_SOURCE ").AppendLine(options.FirmwareRngSource);
        b.Append("MARKER_PRESETS ").AppendLine(FormatSummaryList(options.MarkerPresets));
        b.Append("REQUIRED_MARKERS ").AppendLine(FormatMarkers(options.RequiredMarkers));
        b.Append("ORDERED_MARKERS ").AppendLine(FormatSummaryList(options.OrderedMarkers));
        b.Append("UNIQUE_MARKERS ").AppendLine(FormatSummaryList(options.UniqueMarkers));
        b.Append("FORBIDDEN_MARKERS ").AppendLine(FormatSummaryList(options.ForbiddenMarkers));
        AppendHttpProbeSummary(b, options, null);
        AppendObservedMarkerLines(b, FindObservedRunnerMarkerLines(serial, options));
        AppendAppExitSummary(b, null, null);
        b.AppendLine("MISSING_MARKERS " + (accepted ? "none" : "unexpected-panic"));
        b.AppendLine("OUT_OF_ORDER_MARKERS none");
        b.AppendLine("DUPLICATE_UNIQUE_MARKERS none");
        b.AppendLine("FORBIDDEN_MARKER_HIT " + (accepted ? "none" : "capture-rejected"));
        b.Append("FAIL_REASON ").AppendLine(reason);
        b.Append("EXPECTED_PANIC_DRAIN milliseconds=750 observedMilliseconds=").AppendLine(quiet.ToString(System.Globalization.CultureInfo.InvariantCulture));
        b.Append("QEMU_PROCESS_RESULT disposition=").Append(cleanupFailed ? "cleanup-failed" : terminated ? "runner-terminated" : spontaneous ? "exited" : "running").Append(" exitCode=").AppendLine(nativeExit.HasValue ? nativeExit.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "none");
        byte[] raw = Encoding.UTF8.GetBytes(serial);
        b.Append("EXPECTED_PANIC_CAPTURE bytes=").Append(raw.Length).Append(" sha256=").AppendLine(Convert.ToHexString(SHA256.HashData(raw)));
        AppendSerialTailSummary(b, serial);
        string expected = b.ToString();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.SummaryLogPath)) ?? ".");
        File.WriteAllText(options.SummaryLogPath, expected, new UTF8Encoding(false));
        if (!string.Equals(File.ReadAllText(options.SummaryLogPath), expected, StringComparison.Ordinal))
            return false;
        Console.WriteLine("SUMMARY_LOG " + options.SummaryLogPath);
        return true;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("SUMMARY_LOG_WRITE_FAILED path=" + options.SummaryLogPath + " reason=" + ex.GetType().Name);
        return false;
    }
}

static void TerminateProcessAndClassify(Process process, ref bool runnerTerminationIssued, ref bool cleanupFailed, ref bool qemuExitedSpontaneously, ref int? qemuExitCode)
{
    bool runnerTerminationWasAlreadyIssued = runnerTerminationIssued;
    (bool succeeded, bool killIssued, bool exitedBeforeKill, int? exitCode) = TerminateProcessBounded(process);
    runnerTerminationIssued |= killIssued;
    cleanupFailed |= !succeeded;
    qemuExitedSpontaneously |= exitedBeforeKill && !runnerTerminationWasAlreadyIssued;
    if (exitCode.HasValue)
    {
        qemuExitCode = exitCode;
    }
}

static void ValidateResolvedPathSeparation(RunnerOptions options, string? qemuPath, string? ovmfPath)
{
    List<(string Name, string Path)> paths = [("image", options.ImagePath), ("serial-log", options.SerialLogPath)];
    if (options.VirtioBlockBootImagePath is not null)
        paths.Add(("virtio-block-boot-image", options.VirtioBlockBootImagePath));
    if (options.WritableDataImagePath is not null)
    {
        paths.Add(("writable-data-image", options.WritableDataImagePath));
        paths.Add(("ethernet-capture", options.EthernetCapturePath!));
    }

    if (!string.IsNullOrWhiteSpace(qemuPath))
    {
        paths.Add(("qemu", qemuPath));
    }

    if (!string.IsNullOrWhiteSpace(ovmfPath))
    {
        paths.Add(("ovmf-code", ovmfPath));
    }

    if (!string.IsNullOrWhiteSpace(options.SummaryLogPath))
    {
        paths.Add(("summary-log", options.SummaryLogPath));
    }

    StringComparer comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    Dictionary<string, string> nameByPath = new Dictionary<string, string>(comparer);
    foreach ((string name, string path) in paths)
    {
        string fullPath = Path.GetFullPath(path);
        if (options.WritableDataImagePath is not null && fullPath.IndexOfAny([',', '\r', '\n', '\0']) >= 0)
            throw new ArgumentException("Writable fixture runtime paths forbid QEMU option separators.");
        if (options.WritableDataImagePath is not null)
            WritableDataImageLease.RequirePlainRuntimePath(fullPath);
        if (nameByPath.TryGetValue(fullPath, out string? existing))
        {
            throw new ArgumentException("Resolved runtime paths must be distinct: " + existing + " and " + name + " resolve to " + fullPath + ".");
        }

        nameByPath.Add(fullPath, name);
    }
}

static (bool Succeeded, bool KillIssued, bool ExitedBeforeKill, int? ExitCode) TerminateProcessBounded(Process process)
{
    const int waitMilliseconds = 2000;
    try
    {
        if (process.HasExited)
        {
            return (true, false, true, process.ExitCode);
        }

        bool killIssued = false;
        try
        {
            process.Kill(entireProcessTree: true);
            killIssued = true;
        }
        catch (InvalidOperationException) when (process.HasExited)
        {
            return (true, false, true, process.ExitCode);
        }

        bool exited = process.WaitForExit(waitMilliseconds);
        int? exitCode = exited ? process.ExitCode : null;
        return (exited, killIssued, false, exitCode);
    }
    catch
    {
        return (false, false, false, null);
    }
}

static bool TryFeedSerialLogDelta(string path, SerialMarkerStateMachine stateMachine, ref long offset, out SerialMarkerFeedResult feedResult, out string? failureReason)
{
    feedResult = stateMachine.Feed(ReadOnlySpan<byte>.Empty);
    failureReason = null;
    if (!feedResult.Succeeded || !File.Exists(path))
    {
        return feedResult.Succeeded;
    }

    try
    {
        using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length < offset)
        {
            failureReason = "serial_log_truncated";
            return false;
        }

        stream.Position = offset;
        byte[] buffer = new byte[4096];
        while (true)
        {
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            if (bytesRead == 0)
            {
                return true;
            }

            feedResult = stateMachine.Feed(buffer.AsSpan(0, bytesRead));
            offset += feedResult.BytesConsumed;
            if (!feedResult.Succeeded)
            {
                return true;
            }
        }
    }
    catch (FileNotFoundException)
    {
        return true;
    }
    catch (DirectoryNotFoundException)
    {
        return true;
    }
    catch (UnauthorizedAccessException)
    {
        failureReason = "serial_log_unreadable";
        return false;
    }
    catch (IOException)
    {
        failureReason = "serial_log_read_failed";
        return false;
    }
}

static bool IsStreamingStructuralFailure(SerialMarkerCompletionStatus status)
{
    return status is SerialMarkerCompletionStatus.CapacityExceeded or SerialMarkerCompletionStatus.InvalidByteSequence or SerialMarkerCompletionStatus.ConflictingTerminalMarkers or SerialMarkerCompletionStatus.DuplicateTerminalMarker or SerialMarkerCompletionStatus.FailureTerminal or SerialMarkerCompletionStatus.PartialMarkerAtEndOfStream or SerialMarkerCompletionStatus.Uninitialized;
}

static IReadOnlyList<string> MergeDistinctMarkers(IReadOnlyList<string> first, IReadOnlyList<string> second)
{
    if (first.Count == 0)
    {
        return second;
    }

    if (second.Count == 0)
    {
        return first;
    }

    List<string> merged = new List<string>(first.Count + second.Count);
    foreach (string marker in first)
    {
        if (!merged.Contains(marker, StringComparer.Ordinal))
        {
            merged.Add(marker);
        }
    }

    foreach (string marker in second)
    {
        if (!merged.Contains(marker, StringComparer.Ordinal))
        {
            merged.Add(marker);
        }
    }

    return merged;
}

static IReadOnlyList<string> FindObservedRunnerMarkerLines(string serial, RunnerOptions options)
{
    List<string> observed = new List<string>();
    using StringReader reader = new StringReader(serial);
    while (reader.ReadLine() is { } line)
    {
        string trimmed = line.Trim();
        if (trimmed.Length == 0)
        {
            continue;
        }

        if (IsStandardObservedMarkerLine(trimmed) || LineMatchesAnyMarker(trimmed, options.RequiredMarkers) || LineMatchesAnyMarker(trimmed, options.OrderedMarkers) || LineMatchesAnyMarker(trimmed, options.UniqueMarkers) || LineMatchesAnyMarker(trimmed, options.ForbiddenMarkers))
        {
            observed.Add(trimmed);
        }
    }

    return observed;
}

static bool IsStandardObservedMarkerLine(string line) => SerialMarkerSummary.IsStandardObservedMarkerLine(line);
static bool LineMatchesAnyMarker(string line, IReadOnlyList<string> markers)
{
    for (int index = 0; index < markers.Count; index++)
    {
        string marker = markers[index];
        if (string.Equals(line, marker, StringComparison.Ordinal) || line.StartsWith(marker + " ", StringComparison.Ordinal))
        {
            return true;
        }
    }

    return false;
}

static string FormatMarkers(IReadOnlyList<string> markers)
{
    return string.Join(";", markers);
}

static string FormatSummaryList(IReadOnlyList<string> values)
{
    return values.Count == 0 ? "none" : FormatMarkers(values);
}

static int RunPreflight(RunnerOptions options)
{
    if (options.WritableDataImagePath is not null)
        ValidateResolvedPathSeparation(options, null, string.IsNullOrEmpty(options.OvmfCodePath) ? null : options.OvmfCodePath);
    Console.WriteLine("TEST_START qemu.runner.preflight");
    Console.WriteLine("WORKLOAD_PROFILE " + options.WorkloadProfile);
    if (options.FirmwareRngSource is not null)
        Console.WriteLine("FIRMWARE_RNG_SOURCE " + options.FirmwareRngSource);
    Console.WriteLine("MARKER_PRESETS " + FormatSummaryList(options.MarkerPresets));
    Console.WriteLine("REQUIRED_MARKERS " + FormatMarkers(options.RequiredMarkers));
    Console.WriteLine("ORDERED_MARKERS " + FormatSummaryList(options.OrderedMarkers));
    Console.WriteLine("UNIQUE_MARKERS " + FormatSummaryList(options.UniqueMarkers));
    Console.WriteLine("FORBIDDEN_MARKERS " + FormatSummaryList(options.ForbiddenMarkers));
    if (!File.Exists(options.ImagePath))
    {
        Console.WriteLine("QEMU_PREFLIGHT_IMAGE path=" + options.ImagePath + " exists=false bytes=0");
        return FailPreflight("image_not_found", 2);
    }

    long imageBytes;
    try
    {
        imageBytes = new FileInfo(options.ImagePath).Length;
    }
    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
    {
        Console.WriteLine("QEMU_PREFLIGHT_IMAGE path=" + options.ImagePath + " exists=true bytes=unknown");
        return FailPreflight("image_read_failed error=" + ex.GetType().Name, 11);
    }

    Console.WriteLine("QEMU_PREFLIGHT_IMAGE path=" + options.ImagePath + " exists=true bytes=" + imageBytes);
    if (imageBytes == 0)
    {
        return FailPreflight("image_empty", 11);
    }

    using WritableDataImageLease? writable = options.WritableDataImagePath is null ? null : WritableDataImageLease.Open(options.ImagePath, options.WritableDataImagePath);
    if (writable is not null)
        foreach (string row in writable.Summary())
            Console.WriteLine(row);
    string detectedDiskFormat;
    try
    {
        detectedDiskFormat = DetectImageFormat(options.ImagePath);
    }
    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
    {
        return FailPreflight("image_read_failed error=" + ex.GetType().Name, 11);
    }

    Console.WriteLine("QEMU_PREFLIGHT_DISK_FORMAT expected=" + options.DiskFormat + " detected=" + detectedDiskFormat);
    if (string.Equals(options.DiskFormat, "qcow2", StringComparison.Ordinal) && !string.Equals(detectedDiskFormat, "qcow2", StringComparison.Ordinal))
    {
        return FailPreflight("disk_format_mismatch expected=qcow2 detected=" + detectedDiskFormat, 12);
    }

    if (string.Equals(options.DiskFormat, "raw", StringComparison.Ordinal) && string.Equals(detectedDiskFormat, "qcow2", StringComparison.Ordinal))
    {
        return FailPreflight("disk_format_mismatch expected=raw detected=qcow2", 12);
    }

    string serialDirectory = GetPathDirectoryOrCurrent(options.SerialLogPath);
    Console.WriteLine("QEMU_PREFLIGHT_SERIAL_DIRECTORY path=" + serialDirectory + " exists=" + FormatBool(Directory.Exists(serialDirectory)) + " runner_creates=true");
    bool ovmfConfigured = !string.IsNullOrWhiteSpace(options.OvmfCodePath);
    bool ovmfExists = ovmfConfigured && File.Exists(options.OvmfCodePath);
    Console.WriteLine("QEMU_PREFLIGHT_OVMF configured=" + FormatBool(ovmfConfigured) + " exists=" + (ovmfConfigured ? FormatBool(ovmfExists) : "not_checked") + " path=" + (ovmfConfigured ? options.OvmfCodePath : "none"));
    if (ovmfConfigured && !ovmfExists)
    {
        return FailPreflight("ovmf_not_found", 6);
    }

    IReadOnlyList<string> arguments = QemuCommandBuilder.BuildArguments(options, options.OvmfCodePath);
    Console.WriteLine("QEMU_PREFLIGHT_COMMAND_ARGUMENTS count=" + arguments.Count);
    Console.WriteLine("QEMU_PREFLIGHT_COMMAND_LINE " + QemuCommandBuilder.BuildCommandLine(options, options.OvmfCodePath));
    if (options.HttpProbe is not null)
    {
        WriteHttpProbe(options.HttpProbe.Value);
    }

    writable?.VerifyOriginalAndWorkingIdentity();
    Console.WriteLine("TEST_PASS qemu.runner.preflight");
    return 0;
}

static int FailPreflight(string reason, int exitCode)
{
    Console.WriteLine("TEST_FAIL qemu.runner.preflight " + reason);
    return exitCode;
}

static string DetectImageFormat(string imagePath)
{
    Span<byte> header = stackalloc byte[4];
    using FileStream stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
    int bytesRead = stream.Read(header);
    if (bytesRead == 4 && header[0] == (byte)'Q' && header[1] == (byte)'F' && header[2] == (byte)'I' && header[3] == 0xFB)
    {
        return "qcow2";
    }

    return "raw-compatible";
}

static string GetPathDirectoryOrCurrent(string path)
{
    string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
    return string.IsNullOrWhiteSpace(directory) ? Directory.GetCurrentDirectory() : directory;
}

static string FormatBool(bool value)
{
    return value ? "true" : "false";
}

static bool WriteSummaryLogBestEffort(RunnerOptions options, string status, IReadOnlyList<string> observedMarkers, IReadOnlyList<string> missingMarkers, IReadOnlyList<string> forbiddenMarkers, string failReason, HttpProbeResult? httpProbeResult = null, IReadOnlyList<AppExitMarkerResult>? appExitResults = null, IReadOnlyList<string>? malformedAppExitMarkers = null, IReadOnlyList<string>? outOfOrderMarkers = null, IReadOnlyList<string>? duplicateUniqueMarkers = null)
{
    if (string.IsNullOrWhiteSpace(options.SummaryLogPath))
    {
        return false;
    }

    try
    {
        string fullPath = Path.GetFullPath(options.SummaryLogPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? ".");
        StringBuilder builder = new StringBuilder();
        builder.Append("QEMU_SMOKE_STATUS ").AppendLine(status);
        builder.Append("IMAGE_ARTIFACT ").AppendLine(options.ImagePath);
        builder.Append("SERIAL_LOG ").AppendLine(options.SerialLogPath);
        builder.Append("WORKLOAD_PROFILE ").AppendLine(options.WorkloadProfile);
        builder.Append("QEMU_ACCELERATOR ").AppendLine(options.Accelerator);
        builder.Append("QEMU_MACHINE ").AppendLine(OS.Tooling.QemuContracts.MachineArgument(options.Accelerator));
        if (options.WritableDataLease is not null)
            foreach (string row in options.WritableDataLease.Summary())
                builder.AppendLine(row);
        if (options.FirmwareRngSource is not null)
            builder.Append("FIRMWARE_RNG_SOURCE ").AppendLine(options.FirmwareRngSource);
        if (options.SocketForwardPort is int socketForward)
            builder.Append("SOCKET_FORWARD_PORT ").AppendLine(socketForward.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (options.ContinuingService)
            builder.AppendLine("CONTINUING_SERVICE true");
        if (options.ServiceQuotaFailure)
            builder.AppendLine("SERVICE_QUOTA_FAILURE true");
        builder.Append("MARKER_PRESETS ").AppendLine(FormatSummaryList(options.MarkerPresets));
        builder.Append("REQUIRED_MARKERS ").AppendLine(FormatMarkers(options.RequiredMarkers));
        builder.Append("ORDERED_MARKERS ").AppendLine(FormatSummaryList(options.OrderedMarkers));
        builder.Append("UNIQUE_MARKERS ").AppendLine(FormatSummaryList(options.UniqueMarkers));
        builder.Append("FORBIDDEN_MARKERS ").AppendLine(FormatSummaryList(options.ForbiddenMarkers));
        AppendHttpProbeSummary(builder, options, httpProbeResult);
        AppendContinuingWebSummary(builder, options);
        AppendObservedMarkerLines(builder, observedMarkers);
        AppendAppExitSummary(builder, appExitResults, malformedAppExitMarkers);
        builder.Append("MISSING_MARKERS ").AppendLine(FormatSummaryList(missingMarkers));
        builder.Append("OUT_OF_ORDER_MARKERS ").AppendLine(FormatSummaryList(outOfOrderMarkers ?? Array.Empty<string>()));
        builder.Append("DUPLICATE_UNIQUE_MARKERS ").AppendLine(FormatSummaryList(duplicateUniqueMarkers ?? Array.Empty<string>()));
        builder.Append("FORBIDDEN_MARKER_HIT ").AppendLine(FormatSummaryList(forbiddenMarkers));
        builder.Append("FAIL_REASON ").AppendLine(failReason);
        if (TryReadSerialLogBounded(options.SerialLogPath, out string serial, out string? serialTailFailure, options.SerialByteCapacity))
        {
            AppendSerialTailSummary(builder, serial);
        }
        else
        {
            if (options.ServiceQuotaFailure)
                return false;
            builder.Append("SERIAL_TAIL_READ_ERROR ").AppendLine(serialTailFailure ?? "unknown");
            AppendSerialTailSummary(builder, string.Empty);
        }

        if (options.ServiceEvidence is not null)
            AppendServiceEvidence(builder, options.ServiceEvidence);
        if (options.StaticEvidence is not null)
            foreach (string row in options.StaticEvidence.Summary())
                builder.AppendLine(row);
        File.WriteAllText(options.SummaryLogPath, builder.ToString(), Encoding.UTF8);
        Console.WriteLine("SUMMARY_LOG " + options.SummaryLogPath);
        return true;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("SUMMARY_LOG_WRITE_FAILED path=" + options.SummaryLogPath + " reason=" + ex.GetType().Name + ": " + ex.Message);
        return false;
    }
}

static void AppendSerialTailSummary(StringBuilder builder, string serial)
{
    const int maxLines = 8;
    const int maxChars = 240;
    builder.Append("SERIAL_TAIL_LIMIT lines=").Append(maxLines.ToString()).Append(" chars=").AppendLine(maxChars.ToString());
    string[] tailLines = GetSerialTailLines(serial, maxLines, maxChars);
    if (tailLines.Length == 0)
    {
        builder.Append("SERIAL_TAIL_LINE none").AppendLine();
        return;
    }

    foreach (string line in tailLines)
    {
        builder.Append("SERIAL_TAIL_LINE ").AppendLine(line);
    }
}

static string[] GetSerialTailLines(string serial, int maxLines, int maxChars)
{
    Queue<string> tail = new Queue<string>(maxLines);
    using StringReader reader = new StringReader(serial);
    while (reader.ReadLine() is { } line)
    {
        string normalized = NormalizeSerialTailLine(line, maxChars);
        if (normalized.Length == 0)
        {
            continue;
        }

        if (tail.Count == maxLines)
        {
            tail.Dequeue();
        }

        tail.Enqueue(normalized);
    }

    return tail.ToArray();
}

static string NormalizeSerialTailLine(string line, int maxChars)
{
    string trimmed = line.Trim();
    if (trimmed.Length <= maxChars)
    {
        return trimmed;
    }

    return trimmed[..(maxChars - 3)] + "...";
}

static void AppendObservedMarkerLines(StringBuilder builder, IReadOnlyList<string> observedMarkers)
{
    if (observedMarkers.Count == 0)
    {
        builder.Append("OBSERVED_MARKER none").AppendLine();
        return;
    }

    foreach (string marker in observedMarkers)
    {
        builder.Append("OBSERVED_MARKER ").AppendLine(marker);
    }
}

static void AppendAppExitSummary(StringBuilder builder, IReadOnlyList<AppExitMarkerResult>? appExitResults, IReadOnlyList<string>? malformedAppExitMarkers)
{
    if (appExitResults is null || appExitResults.Count == 0)
    {
        builder.Append("APP_EXIT_RESULT none").AppendLine();
    }
    else
    {
        foreach (AppExitMarkerResult result in appExitResults)
        {
            builder.Append("APP_EXIT_RESULT code=").Append(result.ExitCode.ToString()).Append(" status=").AppendLine(result.Status);
        }
    }

    if (malformedAppExitMarkers is null || malformedAppExitMarkers.Count == 0)
    {
        builder.Append("APP_EXIT_MALFORMED none").AppendLine();
        return;
    }

    foreach (string marker in malformedAppExitMarkers)
    {
        builder.Append("APP_EXIT_MALFORMED ").AppendLine(marker);
    }
}

static void AppendHttpProbeSummary(StringBuilder builder, RunnerOptions options, HttpProbeResult? result)
{
    if (options.HttpProbe is null)
    {
        return;
    }

    HttpProbeOptions probe = options.HttpProbe.Value;
    builder.Append("HTTP_PROBE_ENDPOINT ").AppendLine(FormatProbeEndpoint(probe.Url));
    builder.Append("HTTP_PROBE_EXPECTED_STATUS ").AppendLine(probe.ExpectedStatusCode.ToString());
    builder.Append("HTTP_PROBE_EXPECTED_BODY_BYTES ").AppendLine(probe.ExpectedBodyByteLength.ToString());
    builder.Append("HTTP_PROBE_TIMEOUT_SECONDS ").AppendLine(probe.TimeoutSeconds.ToString());
    if (result is null)
    {
        builder.Append("HTTP_PROBE_RESULT not_run").AppendLine();
        builder.Append("HTTP_PROBE_REASON none").AppendLine();
        builder.Append("HTTP_PROBE_STATUS none").AppendLine();
        builder.Append("HTTP_PROBE_BODY_BYTES none").AppendLine();
        builder.Append("HTTP_PROBE_BODY_SHA256 none").AppendLine();
        builder.Append("HTTP_PROBE_ELAPSED_MS none").AppendLine();
        return;
    }

    HttpProbeResult value = result.Value;
    builder.Append("HTTP_PROBE_RESULT ").AppendLine(value.Passed ? "pass" : "fail");
    builder.Append("HTTP_PROBE_REASON ").AppendLine(value.Reason);
    builder.Append("HTTP_PROBE_STATUS ").AppendLine(value.StatusCodeText);
    builder.Append("HTTP_PROBE_BODY_BYTES ").AppendLine(value.BodyByteCount.ToString());
    builder.Append("HTTP_PROBE_BODY_SHA256 ").AppendLine(value.BodySha256);
    builder.Append("HTTP_PROBE_ELAPSED_MS ").AppendLine(value.ElapsedMilliseconds.ToString());
}

static void WriteHttpProbe(HttpProbeOptions probe)
{
    Console.WriteLine("HTTP_PROBE url=" + probe.Url + " expected_status=" + probe.ExpectedStatusCode + " expected_body_bytes=" + probe.ExpectedBodyByteLength + " timeout_seconds=" + probe.TimeoutSeconds);
}

static bool JoinServiceControls(Task<ServiceSocketControls.Report>? task, CancellationTokenSource cancellation)
{
    cancellation.Cancel();
    if (task is null || task.IsCompleted)
        return true;
    try
    {
        return task.Wait(TimeSpan.FromSeconds(5));
    }
    catch (AggregateException)
    {
        return task.IsCompleted;
    }
}

static string? ServiceControlFailure(Task<ServiceSocketControls.Report>? task) => task?.IsFaulted == true ? "socket_controls_" + task.Exception!.GetBaseException().GetType().Name : task?.IsCanceled == true ? "socket_controls_cancelled" : null;
static void AppendServiceEvidence(StringBuilder builder, ServiceEvidence service)
{
    builder.Append("SERVICE_SOCKET_CONTROLS ").AppendLine(service.Controls is null ? "none" : JsonSerializer.Serialize(service.Controls));
    builder.Append("SERVICE_FRAME_CONTROL ").AppendLine(service.Frames is null ? "none" : JsonSerializer.Serialize(service.Frames));
    builder.Append("SERVICE_PROCESS_ID ").AppendLine(service.ProcessId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none");
    builder.Append("SERVICE_PROCESS_START_UTC ").AppendLine(service.StartUtc ?? "none");
    builder.Append("SERVICE_PROCESS_END_UTC ").AppendLine(service.EndUtc ?? "none");
    builder.Append("SERVICE_PROCESS_LIFETIME_MS ").AppendLine(service.LifetimeMilliseconds?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none");
    builder.Append("SERVICE_ENDPOINT_PRELAUNCH_AVAILABLE ").AppendLine(service.PreLaunchAvailable ? "true" : "false");
    builder.Append("SERVICE_ENDPOINT_STOPPED_REFUSED ").AppendLine(service.StoppedEndpointRefused?.ToString().ToLowerInvariant() ?? "not_run");
    builder.Append("SERVICE_PROCESS_FAILURE ").AppendLine(service.Failure ?? "none");
    if (service.QuotaFailure)
    {
        string Number(long? value) => value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none";
        string Bit(bool value) => value ? "true" : "false";
        builder.Append("SERVICE_QUOTA_CAPTURE terminal=").Append(ServiceQuotaCapture.Terminal).Append(" terminalBytes=").Append(Number(service.TerminalBytes)).Append(" quietMilliseconds=").Append(Number(service.QuietMilliseconds)).AppendLine(" controls=disabled acceptance=pending");
        builder.Append("SERVICE_QUOTA_PROCESS terminationRequested=").Append(Bit(service.TerminationRequested)).Append(" deathConfirmed=").Append(Bit(service.DeathConfirmed)).Append(" spontaneous=").Append(Bit(service.SpontaneousExit)).Append(" cleanupFailed=").Append(Bit(service.CleanupFailed)).Append(" exitCode=").AppendLine(Number(service.ExitCode));
    }
}

static void AppendContinuingWebSummary(StringBuilder builder, RunnerOptions options)
{
    if (!options.ContinuingWeb)
        return;
    ContinuingWebEvidence web = options.WebEvidence!;
    HttpProbeOptions config = options.WebConfigProbe!.Value;
    builder.AppendLine("CONTINUING_WEB true");
    builder.Append("WEB_LOG_CASE ").AppendLine(options.WebLogCase);
    builder.Append("WEB_CONFIG_PROBE_URL ").AppendLine(config.Url);
    builder.AppendLine("WEB_CONFIG_PROBE_EXPECTED_STATUS 200");
    builder.Append("WEB_CONFIG_PROBE_EXPECTED_BODY_BYTES ").AppendLine(config.ExpectedBodyByteLength.ToString());
    builder.Append("WEB_CONFIG_PROBE_EXPECTED_BODY_SHA256 ").AppendLine(ComputeSha256(Encoding.UTF8.GetBytes(config.ExpectedBody)));
    builder.Append("WEB_CONFIG_PROBE_RESULT ").AppendLine(web.ConfigResult is null ? "not_run" : web.ConfigResult.Value.Passed ? "pass" : "fail");
    builder.Append("WEB_CONFIG_PROBE_REASON ").AppendLine(web.ConfigResult?.Reason ?? "none");
    builder.Append("WEB_CONFIG_PROBE_STATUS ").AppendLine(web.ConfigResult?.StatusCodeText ?? "none");
    builder.Append("WEB_CONFIG_PROBE_BODY_BYTES ").AppendLine(web.ConfigResult?.BodyByteCount.ToString() ?? "none");
    builder.Append("WEB_CONFIG_PROBE_BODY_SHA256 ").AppendLine(web.ConfigResult?.BodySha256 ?? "none");
    builder.Append("WEB_CONFIG_PROBE_ELAPSED_MS ").AppendLine(web.ConfigResult?.ElapsedMilliseconds.ToString() ?? "none");
    AppendWebProbe("WEB_LOG_PROBE", options.WebLogProbe!.Value, web.LogResult);
    AppendWebProbe("WEB_LOG_REPEAT_PROBE", options.WebLogRepeatProbe!.Value, web.LogRepeatResult);
    if (options.WebVectorProbe is not null)
        AppendWebProbe("WEB_VECTOR_PROBE", options.WebVectorProbe.Value, web.VectorResult);
    AppendWebProbe("WEB_HEALTH_AFTER_PROBE", options.HttpProbe!.Value, web.HealthAfterResult);
    builder.Append("WEB_SOCKET_CONTROLS ").AppendLine(web.Controls is null ? "none" : JsonSerializer.Serialize(web.Controls));
    builder.Append("WEB_FRAME_CONTROL ").AppendLine(web.Frames is null ? "none" : JsonSerializer.Serialize(web.Frames));
    builder.Append("WEB_PROCESS_ALIVE_AFTER_CONTROLS ").AppendLine(FormatBool(web.AliveAfterControls));
    if (options.WebRacePipe is not null)
    {
        builder.Append("WEB_RACE_GATE ").AppendLine(web.RaceGate is null ? "none" : JsonSerializer.Serialize(web.RaceGate));
        builder.Append("WEB_PROCESS_ALIVE_AFTER_RACE_GATE ").AppendLine(FormatBool(web.AliveAfterRaceGate));
    }

    builder.Append("WEB_ENDPOINT_PRELAUNCH_AVAILABLE ").AppendLine(web.PreLaunchAvailable ? "true" : "false");
    builder.Append("WEB_ENDPOINT_STOPPED_REFUSED ").AppendLine(web.StoppedEndpointRefused?.ToString().ToLowerInvariant() ?? "not_run");
    builder.Append("WEB_PROCESS_ID ").AppendLine(web.ProcessId?.ToString() ?? "none");
    builder.Append("WEB_PROCESS_START_UTC ").AppendLine(web.StartUtc ?? "none");
    builder.Append("WEB_PROCESS_END_UTC ").AppendLine(web.EndUtc ?? "none");
    builder.Append("WEB_PROCESS_LIFETIME_MS ").AppendLine(web.LifetimeMilliseconds?.ToString() ?? "none");
    builder.AppendLine("WEB_HTTP_UTC_TOLERANCE_SECONDS 5");
    builder.Append("WEB_HTTP_UTC_WINDOWS ").AppendLine(web.ProbeWindows.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
    for (int i = 0; i < web.ProbeWindows.Count; i++)
    {
        string prefix = "WEB_HTTP_UTC_" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
        builder.Append(prefix).Append("_STARTED ").AppendLine(web.ProbeWindows[i].Started.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        builder.Append(prefix).Append("_COMPLETED ").AppendLine(web.ProbeWindows[i].Completed.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
    }

    builder.Append("WEB_PROCESS_ALIVE_BEFORE_HEALTH ").AppendLine(FormatBool(web.AliveBeforeHealth));
    builder.Append("WEB_PROCESS_ALIVE_AFTER_HEALTH ").AppendLine(FormatBool(web.AliveAfterHealth));
    builder.Append("WEB_PROCESS_ALIVE_AFTER_CONFIG ").AppendLine(FormatBool(web.AliveAfterConfig));
    builder.Append("WEB_PROCESS_ALIVE_AFTER_LOG ").AppendLine(FormatBool(web.AliveAfterLog));
    builder.Append("WEB_PROCESS_ALIVE_AFTER_LOG_REPEAT ").AppendLine(FormatBool(web.AliveAfterLogRepeat));
    if (options.WebVectorProbe is not null)
        builder.Append("WEB_PROCESS_ALIVE_AFTER_VECTORS ").AppendLine(FormatBool(web.AliveAfterVectors));
    builder.Append("WEB_PROCESS_ALIVE_AFTER_FINAL_HEALTH ").AppendLine(FormatBool(web.AliveAfterFinalHealth));
    builder.Append("WEB_PROCESS_TERMINATION_REQUESTED ").AppendLine(FormatBool(web.TerminationRequested));
    builder.Append("WEB_PROCESS_DEATH_CONFIRMED ").AppendLine(FormatBool(web.DeathConfirmed));
    builder.Append("WEB_PROCESS_SPONTANEOUS_EXIT ").AppendLine(FormatBool(web.SpontaneousExit));
    builder.Append("WEB_PROCESS_EXIT_CODE ").AppendLine(web.ExitCode?.ToString() ?? "none");
    builder.Append("WEB_PROCESS_FAILURE ").AppendLine(web.Failure ?? "none");
    void AppendWebProbe(string prefix, HttpProbeOptions probe, HttpProbeResult? result)
    {
        builder.Append(prefix).Append("_URL ").AppendLine(probe.Url);
        builder.Append(prefix).Append("_METHOD ").AppendLine(probe.IsPost ? "POST" : "GET");
        builder.Append(prefix).Append("_EXPECTED_STATUS ").AppendLine(probe.ExpectedStatusCode.ToString());
        builder.Append(prefix).Append("_EXPECTED_BODY_BYTES ").AppendLine(probe.ExpectedBodyByteLength.ToString());
        builder.Append(prefix).Append("_EXPECTED_BODY_SHA256 ").AppendLine(probe.ExpectedBodySha256);
        builder.Append(prefix).Append("_RESULT ").AppendLine(result is null ? "not_run" : result.Value.Passed ? "pass" : "fail");
        builder.Append(prefix).Append("_REASON ").AppendLine(result?.Reason ?? "none");
        builder.Append(prefix).Append("_STATUS ").AppendLine(result?.StatusCodeText ?? "none");
        builder.Append(prefix).Append("_BODY_BYTES ").AppendLine(result?.BodyByteCount.ToString() ?? "none");
        builder.Append(prefix).Append("_BODY_SHA256 ").AppendLine(result?.BodySha256 ?? "none");
        builder.Append(prefix).Append("_ELAPSED_MS ").AppendLine(result?.ElapsedMilliseconds.ToString() ?? "none");
    }
}

static bool CheckWebPortAvailable(int port)
{
    try
    {
        using TcpListener listener = new(System.Net.IPAddress.Loopback, port);
        listener.Server.ExclusiveAddressUse = true;
        listener.Start(1);
        return true;
    }
    catch (SocketException)
    {
        return false;
    }
}

static bool CheckWebEndpointRefused(int port)
{
    // Windows loopback reports a refused connection after about two seconds.
    // Process-tree exit can also precede a child's socket release. Require an
    // actual refusal within one bounded window, including successful retries.
    using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(3));
    while (!cancellation.IsCancellationRequested)
    {
        using TcpClient client = new();
        try
        {
            client.ConnectAsync(System.Net.IPAddress.Loopback, port, cancellation.Token).AsTask().GetAwaiter().GetResult();
            Task.Delay(25, cancellation.Token).GetAwaiter().GetResult();
        }
        catch (SocketException ex)
        {
            return ex.SocketErrorCode == SocketError.ConnectionRefused;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    return false;
}

static HttpProbeResult ExecuteContinuingWebProbe(HttpProbeOptions probe, Stopwatch lifetime, int deadlineSeconds)
{
    long remaining = deadlineSeconds * 1000L - lifetime.ElapsedMilliseconds;
    if (remaining < 1000)
        return HttpProbeResult.Failed("deadline", 0, string.Empty);
    HttpProbeOptions bounded = new(probe.Url, probe.ExpectedStatusCode, probe.ExpectedBody, probe.ExpectedBodyConfigured, Math.Min(probe.TimeoutSeconds, (int)(remaining / 1000)), probe.IsPost, probe.VectorBody);
    // HTTP/1.0 closes the connection after JSON, so the existing bounded
    // reader receives entity bytes without HTTP/1.1 chunk framing.
    try
    {
        return ExecuteHttpProbe(bounded, http10: true);
    }
    catch (SocketException ex)
    {
        return HttpProbeResult.Failed("request_failed", 0, ex.GetType().Name);
    }
}

static HttpProbeResult ExecuteHttpProbe(HttpProbeOptions probe, bool http10 = false)
{
    Console.WriteLine("HTTP_PROBE_START endpoint=" + FormatProbeEndpoint(probe.Url) + " expected_status=" + probe.ExpectedStatusCode + " expected_body_bytes=" + probe.ExpectedBodyByteLength + " timeout_seconds=" + probe.TimeoutSeconds);
    Stopwatch stopwatch = Stopwatch.StartNew();
    try
    {
        Uri uri = new Uri(probe.Url, UriKind.Absolute);
        RawHttpProbeResponse response = SendRawHttpRequest(uri, probe.TimeoutSeconds, http10, probe.IsPost);
        stopwatch.Stop();
        int statusCode = response.StatusCode;
        byte[] body = response.Body;
        string bodySha256 = ComputeSha256(body);
        string reason = "none";
        bool passed = true;
        if (statusCode != probe.ExpectedStatusCode)
        {
            passed = false;
            reason = "status_mismatch";
        }
        else if (probe.VectorBody ? !WebVectorResponse.Matches(body) || bodySha256 != WebVectorResponse.Sha256 : probe.ExpectedBodyConfigured && !Encoding.UTF8.GetBytes(probe.ExpectedBody).AsSpan().SequenceEqual(body))
        {
            passed = false;
            reason = "body_mismatch";
        }

        HttpProbeResult result = new HttpProbeResult(passed, reason, statusCode, body.Length, bodySha256, stopwatch.ElapsedMilliseconds, string.Empty);
        WriteHttpProbeResult(result, probe);
        return result;
    }
    catch (TaskCanceledException ex)
    {
        stopwatch.Stop();
        HttpProbeResult result = HttpProbeResult.Failed("timeout", stopwatch.ElapsedMilliseconds, ex.GetType().Name);
        WriteHttpProbeResult(result, probe);
        return result;
    }
    catch (TimeoutException ex)
    {
        stopwatch.Stop();
        HttpProbeResult result = HttpProbeResult.Failed("timeout", stopwatch.ElapsedMilliseconds, ex.GetType().Name);
        WriteHttpProbeResult(result, probe);
        return result;
    }
    catch (MalformedHttpResponseException ex)
    {
        stopwatch.Stop();
        HttpProbeResult result = HttpProbeResult.Failed("malformed_response", stopwatch.ElapsedMilliseconds, ex.GetType().Name);
        WriteHttpProbeResult(result, probe);
        return result;
    }
    catch (Exception ex) when (ex is IOException || ex is InvalidOperationException)
    {
        stopwatch.Stop();
        HttpProbeResult result = HttpProbeResult.Failed("request_failed", stopwatch.ElapsedMilliseconds, ex.GetType().Name);
        WriteHttpProbeResult(result, probe);
        return result;
    }
}

static RawHttpProbeResponse SendRawHttpRequest(Uri uri, int timeoutSeconds, bool http10 = false, bool post = false)
{
    using CancellationTokenSource cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
    try
    {
        return SendRawHttpRequestAsync(uri, cancellation.Token, http10, post).GetAwaiter().GetResult();
    }
    catch (OperationCanceledException ex) when (cancellation.IsCancellationRequested)
    {
        throw new TimeoutException("HTTP probe timed out.", ex);
    }
}

static async Task<RawHttpProbeResponse> SendRawHttpRequestAsync(Uri uri, CancellationToken cancellationToken, bool http10 = false, bool post = false)
{
    using TcpClient client = new TcpClient();
    await client.ConnectAsync(uri.Host, uri.Port, cancellationToken).ConfigureAwait(false);
    await using NetworkStream stream = client.GetStream();
    string path = string.IsNullOrEmpty(uri.PathAndQuery) ? "/" : uri.PathAndQuery;
    string host = uri.IsDefaultPort ? uri.Host : uri.Host + ":" + uri.Port;
    byte[] requestBytes = Encoding.ASCII.GetBytes((post ? "POST " : "GET ") + path + (http10 ? " HTTP/1.0\r\n" : " HTTP/1.1\r\n") + "Host: " + host + "\r\n" + "Connection: close\r\n" + "Accept: */*\r\n" + (post ? "Content-Length: 0\r\n" : "") + "\r\n");
    await stream.WriteAsync(requestBytes, cancellationToken).ConfigureAwait(false);
    using MemoryStream response = new MemoryStream();
    byte[] buffer = new byte[4096];
    while (true)
    {
        int read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
        if (read == 0)
        {
            break;
        }

        response.Write(buffer, 0, read);
        if (response.Length > 1024 * 1024)
        {
            throw new MalformedHttpResponseException("HTTP probe response exceeded 1 MiB.");
        }
    }

    return ParseRawHttpResponse(response.ToArray(), http10);
}

static RawHttpProbeResponse ParseRawHttpResponse(byte[] response, bool requireCloseFraming = false)
{
    int headerEnd = IndexOf(response, new byte[] { 13, 10, 13, 10 });
    if (headerEnd < 0)
    {
        throw new MalformedHttpResponseException("HTTP response header terminator was not found.");
    }

    int statusLineEnd = IndexOf(response.AsSpan(0, headerEnd), new byte[] { 13, 10 });
    if (statusLineEnd < 0)
    {
        throw new MalformedHttpResponseException("HTTP response status line was not terminated.");
    }

    string statusLine = Encoding.ASCII.GetString(response, 0, statusLineEnd);
    int firstSpace = statusLine.IndexOf(' ');
    if (firstSpace < 0 || firstSpace + 4 > statusLine.Length)
    {
        throw new MalformedHttpResponseException("HTTP response status code was missing.");
    }

    string statusCodeText = statusLine.Substring(firstSpace + 1, 3);
    if (!int.TryParse(statusCodeText, out int statusCode))
    {
        throw new MalformedHttpResponseException("HTTP response status code was invalid.");
    }

    int bodyOffset = headerEnd + 4;
    byte[] body = new byte[response.Length - bodyOffset];
    Array.Copy(response, bodyOffset, body, 0, body.Length);
    if (requireCloseFraming)
    {
        bool lengthSeen = false;
        foreach (string line in Encoding.ASCII.GetString(response, 0, headerEnd).Split("\r\n").Skip(1))
        {
            int colon = line.IndexOf(':');
            if (colon <= 0)
                throw new MalformedHttpResponseException("HTTP header was malformed.");
            string name = line[..colon];
            if (name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                throw new MalformedHttpResponseException("HTTP/1.0 probe requires a close-delimited or exact-length entity.");
            if (!name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                continue;
            if (lengthSeen || !int.TryParse(line[(colon + 1)..].Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int length) || length != body.Length)
                throw new MalformedHttpResponseException("HTTP entity length was incomplete or ambiguous.");
            lengthSeen = true;
        }
    }

    return new RawHttpProbeResponse(statusCode, body);
}

static int IndexOf(ReadOnlySpan<byte> source, ReadOnlySpan<byte> value)
{
    if (value.Length == 0 || value.Length > source.Length)
    {
        return -1;
    }

    for (int i = 0; i <= source.Length - value.Length; i++)
    {
        if (source.Slice(i, value.Length).SequenceEqual(value))
        {
            return i;
        }
    }

    return -1;
}

static void WriteHttpProbeResult(HttpProbeResult result, HttpProbeOptions probe)
{
    string prefix = result.Passed ? "HTTP_PROBE_PASS" : "HTTP_PROBE_FAIL";
    string line = prefix + " endpoint=" + FormatProbeEndpoint(probe.Url) + " status=" + result.StatusCodeText + " body_bytes=" + result.BodyByteCount + " body_sha256=" + result.BodySha256 + " elapsed_ms=" + result.ElapsedMilliseconds;
    if (!result.Passed)
    {
        line += " reason=" + result.Reason;
        if (!string.IsNullOrWhiteSpace(result.ErrorType))
        {
            line += " error_type=" + result.ErrorType;
        }
    }

    Console.WriteLine(line);
}

static string ComputeSha256(byte[] bytes)
{
    return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}

static string FormatProbeEndpoint(string url)
{
    Uri uri = new Uri(url, UriKind.Absolute);
    string host = uri.Host.Contains(':', StringComparison.Ordinal) ? "[" + uri.Host + "]" : uri.Host;
    return uri.Scheme + "://" + host + ":" + uri.Port;
}

static bool TryReadSerialLogBounded(string path, out string serial, out string? failureReason, long byteCapacity = SerialMarkerStateMachine.DefaultByteCapacity)
{
    serial = string.Empty;
    failureReason = null;
    if (!File.Exists(path))
    {
        return true;
    }

    try
    {
        using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length > byteCapacity)
        {
            failureReason = "serial_log_capacity_exceeded";
            return false;
        }

        int byteCount = checked((int)stream.Length);
        byte[] bytes = new byte[byteCount];
        int offset = 0;
        while (offset < bytes.Length)
        {
            int read = stream.Read(bytes, offset, bytes.Length - offset);
            if (read == 0)
            {
                failureReason = "serial_log_short_read";
                return false;
            }

            offset += read;
        }

        serial = Encoding.UTF8.GetString(bytes);
        return true;
    }
    catch (UnauthorizedAccessException)
    {
        failureReason = "serial_log_unreadable";
        return false;
    }
    catch (IOException)
    {
        failureReason = "serial_log_read_failed";
        return false;
    }
    catch (OverflowException)
    {
        failureReason = "serial_log_capacity_exceeded";
        return false;
    }
}

static DiscoveryResult ResolveExecutable(string executable)
{
    List<string> checkedPaths = new List<string>();
    if (Path.IsPathFullyQualified(executable) || executable.Contains(Path.DirectorySeparatorChar) || executable.Contains(Path.AltDirectorySeparatorChar))
    {
        checkedPaths.Add(executable);
        return File.Exists(executable) ? new DiscoveryResult(executable, checkedPaths) : new DiscoveryResult(null, checkedPaths);
    }

    string? path = Environment.GetEnvironmentVariable("PATH");
    if (path is null)
    {
        return new DiscoveryResult(null, checkedPaths);
    }

    foreach (string directory in path.Split(Path.PathSeparator))
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            continue;
        }

        string candidate = Path.Combine(directory, executable);
        checkedPaths.Add(candidate);
        if (File.Exists(candidate))
        {
            return new DiscoveryResult(candidate, checkedPaths);
        }

        string candidateExe = candidate + ".exe";
        checkedPaths.Add(candidateExe);
        if (File.Exists(candidateExe))
        {
            return new DiscoveryResult(candidateExe, checkedPaths);
        }
    }

    return new DiscoveryResult(null, checkedPaths);
}

static DiscoveryResult ResolveOvmfCode(string requestedPath, string? qemuPath)
{
    List<string> candidates = new List<string>();
    if (!string.IsNullOrWhiteSpace(requestedPath))
    {
        candidates.Add(requestedPath);
        return File.Exists(requestedPath) ? new DiscoveryResult(requestedPath, candidates) : new DiscoveryResult(null, candidates);
    }

    AddEnvironmentCandidate(candidates, "OS_OVMF_CODE");
    AddEnvironmentCandidate(candidates, "OVMF_CODE");
    AddEnvironmentCandidate(candidates, "QEMU_OVMF_CODE");
    if (!string.IsNullOrWhiteSpace(qemuPath))
    {
        string? qemuDirectory = Path.GetDirectoryName(Path.GetFullPath(qemuPath));
        if (!string.IsNullOrWhiteSpace(qemuDirectory))
        {
            AddOvmfCandidates(candidates, qemuDirectory);
            AddOvmfCandidates(candidates, Path.Combine(qemuDirectory, "share"));
            AddOvmfCandidates(candidates, Path.Combine(qemuDirectory, "..", "share", "qemu"));
        }
    }

    AddOvmfCandidates(candidates, @"C:\Program Files\qemu");
    AddOvmfCandidates(candidates, @"C:\Program Files\qemu\share");
    AddOvmfCandidates(candidates, @"C:\Program Files (x86)\qemu");
    AddOvmfCandidates(candidates, @"C:\ProgramData\chocolatey\lib\qemu\tools");
    AddOvmfCandidates(candidates, @"C:\msys64\mingw64\share\qemu");
    AddOvmfCandidates(candidates, @"/usr/share/OVMF");
    AddOvmfCandidates(candidates, @"/usr/share/edk2/ovmf");
    AddOvmfCandidates(candidates, @"/usr/share/qemu");
    List<string> uniqueCandidates = new List<string>();
    HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (string candidate in candidates)
    {
        string normalized = Path.GetFullPath(candidate);
        if (seen.Add(normalized))
        {
            uniqueCandidates.Add(normalized);
        }
    }

    foreach (string candidate in uniqueCandidates)
    {
        if (File.Exists(candidate))
        {
            return new DiscoveryResult(candidate, uniqueCandidates);
        }
    }

    return new DiscoveryResult(null, uniqueCandidates);
}

static void AddEnvironmentCandidate(List<string> candidates, string variableName)
{
    string? value = Environment.GetEnvironmentVariable(variableName);
    if (!string.IsNullOrWhiteSpace(value))
    {
        candidates.Add(value);
    }
}

static void AddOvmfCandidates(List<string> candidates, string directory)
{
    candidates.Add(Path.Combine(directory, "OVMF_CODE.fd"));
    candidates.Add(Path.Combine(directory, "OVMF.fd"));
    candidates.Add(Path.Combine(directory, "edk2-x86_64-code.fd"));
    candidates.Add(Path.Combine(directory, "edk2-x86_64-secure-code.fd"));
}

static void WriteDiscovery(string prefix, DiscoveryResult result)
{
    string found = result.Path ?? "not_found";
    string checkedPaths = result.CheckedPaths.Count == 0 ? "none" : string.Join(";", result.CheckedPaths);
    Console.WriteLine(prefix + " result=" + found + " checked=" + checkedPaths);
}
