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

    if (!options.DryRun && options.VirtioBlockBootImagePath is not null &&
        (!File.Exists(options.VirtioBlockBootImagePath) || new FileInfo(options.VirtioBlockBootImagePath).Length < 68 * 512 ||
         new FileInfo(options.VirtioBlockBootImagePath).Length % 512 != 0))
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
        if (options.FirmwareRngSource is not null) Console.WriteLine("FIRMWARE_RNG_SOURCE " + options.FirmwareRngSource);
        if (options.ContinuingService) Console.WriteLine("CONTINUING_SERVICE true");
        if (options.ServiceQuotaFailure) Console.WriteLine("SERVICE_QUOTA_FAILURE true");
        if (options.DiskReadTiming) Console.WriteLine("DISK_READ_TIMING true");
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
        WriteSummaryLogBestEffort(
            options,
            "fail",
            Array.Empty<string>(),
            options.RequiredMarkers,
            Array.Empty<string>(),
            "image_not_found");
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
        WriteSummaryLogBestEffort(
            options,
            "fail",
            Array.Empty<string>(),
            options.RequiredMarkers,
            Array.Empty<string>(),
            FormatSummaryList(missing));
        return qemu.Path is null ? 3 : 6;
    }

    if (options.ExpectedUnexpectedVectorPanic || options.ExpectedOwnedSubsystemPanic || options.ExpectedUnhandledException)
    {
        return RunExpectedUnexpectedVectorPanic(options, qemu.Path, ovmf.Path);
    }

    using WritableDataImageLease? writable = options.WritableDataImagePath is null ? null :
        WritableDataImageLease.Open(options.ImagePath, options.WritableDataImagePath);
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
    if (writable is not null) foreach (string row in writable.Summary()) Console.WriteLine(row);
    if (options.FirmwareRngSource is not null) Console.WriteLine("FIRMWARE_RNG_SOURCE " + options.FirmwareRngSource);
    if (options.SocketForwardPort is int socketForward) Console.WriteLine("SOCKET_FORWARD_PORT " + socketForward.ToString(System.Globalization.CultureInfo.InvariantCulture));
    if (options.ContinuingService) Console.WriteLine("CONTINUING_SERVICE true");
    if (options.ServiceQuotaFailure) Console.WriteLine("SERVICE_QUOTA_FAILURE true");
    Console.WriteLine("MARKER_PRESETS " + FormatSummaryList(options.MarkerPresets));
    Console.WriteLine("REQUIRED_MARKERS " + FormatMarkers(options.RequiredMarkers));
    Console.WriteLine("ORDERED_MARKERS " + FormatSummaryList(options.OrderedMarkers));
    Console.WriteLine("UNIQUE_MARKERS " + FormatSummaryList(options.UniqueMarkers));
    Console.WriteLine("FORBIDDEN_MARKERS " + FormatSummaryList(options.ForbiddenMarkers));

    SerialMarkerStateMachine streamingMarkers = new SerialMarkerStateMachine(
        options.RequiredMarkers,
        options.ForbiddenMarkers,
        successTerminalMarker: options.SuccessTerminalMarker,
        byteCapacity: options.SerialByteCapacity,
        orderedMarkers: options.OrderedMarkers,
        uniqueMarkers: options.UniqueMarkers);

    using ContinuingNetworkPeer? networkPeer = options.ContinuingNetworkPeer
        ? new(TimeSpan.FromMinutes(5), runTcp: true) : null;
    using SocketContainmentPeer? containmentPeer = options.SocketContainmentPeer
        ? new(TimeSpan.FromSeconds(options.TimeoutSeconds)) : null;
    using CancellationTokenSource? serviceCancellation = options.ContinuingService ? new() : null;
    using WebFrameControl? webFrames = options.ContinuingWeb || options.ContinuingService
        ? new(TimeSpan.FromSeconds(options.TimeoutSeconds), continuingService: options.ContinuingService) : null;
    using ServiceSocketControls? serviceControls = options.ContinuingService && !options.ServiceQuotaFailure ? new() : null;
    Task<ServiceSocketControls.Report>? serviceControlTask = null;
    using Process process = new Process();
    process.StartInfo.FileName = qemu.Path;
    process.StartInfo.UseShellExecute = false;
    process.StartInfo.CreateNoWindow = true;
    process.StartInfo.RedirectStandardInput = options.WatchStartup;
    process.StartInfo.RedirectStandardOutput = options.WatchStartup;

    foreach (string argument in QemuCommandBuilder.BuildArguments(options, ovmf.Path, containmentPeer?.NetdevArgument ?? networkPeer?.NetdevArgument,
        webFrames?.Arguments))
    {
        process.StartInfo.ArgumentList.Add(argument);
    }

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
    if (options.WebEvidence is not null) options.WebEvidence.PreLaunchAvailable = true;
    if (options.StaticEvidence is not null)
    {
        if (!CheckWebPortAvailable(options.SocketForwardPort!.Value))
        {
            options.StaticEvidence.Failure = "endpoint_occupied_before_launch";
            WriteSummaryLogBestEffort(options, "fail", [], options.RequiredMarkers, [], "static_endpoint_occupied_before_launch");
            Console.Error.WriteLine("TEST_FAIL qemu.smoke static_endpoint_occupied_before_launch"); return 23;
        }
        options.StaticEvidence.PreLaunchAvailable = true;
    }
    if (options.ServiceEvidence is not null)
    {
        if (!CheckWebPortAvailable(options.SocketForwardPort!.Value))
        {
            options.ServiceEvidence.Failure = "endpoint_occupied_before_launch";
            WriteSummaryLogBestEffort(options, "fail", [], options.RequiredMarkers, [], "service_endpoint_occupied_before_launch");
            Console.Error.WriteLine("TEST_FAIL qemu.smoke service_endpoint_occupied_before_launch"); return 21;
        }
        options.ServiceEvidence.PreLaunchAvailable = true;
    }
    try
    {
    writable?.VerifyOriginalAndWorkingIdentity();
    process.Start();
    processStarted = true;
    writable?.BindStartedChild(process);
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
    IReadOnlyList<string> probeMarkers = options.ContinuingWeb
        ? options.RequiredMarkers.TakeWhile(marker => marker != "TEST_START m10.005").ToArray()
        : options.RequiredMarkers;
    IReadOnlyList<string> probeOrderedMarkers = options.ContinuingWeb
        ? options.OrderedMarkers.TakeWhile(marker => marker != "TEST_START m10.005").ToArray()
        : options.OrderedMarkers;
    bool forbiddenMarkerSeen = false;
    bool timedOut = false;
    bool startupTimedOut = false;
    bool networkPeerInitiatedFailure = false;
    QemuStartupDeadline? startup = options.WatchStartup ? new(options.StartupTimeoutSeconds) : null;
    const long terminalDrainMilliseconds = 750;
    Stopwatch stopwatch = Stopwatch.StartNew();
    long lastSerialActivityMilliseconds = stopwatch.ElapsedMilliseconds;
    while (true)
    {
        long previousSerialReadOffset = serialReadOffset;
        if (!TryFeedSerialLogDelta(
                options.SerialLogPath,
                streamingMarkers,
                ref serialReadOffset,
                out SerialMarkerFeedResult feedResult,
                out serialReadFailure))
        {
            TerminateProcessAndClassify(
                process,
                ref processTerminationRequested,
                ref processCleanupFailed,
                ref qemuExitedSpontaneously,
                ref qemuExitCode);
            break;
        }

        if (!feedResult.Succeeded)
        {
            TerminateProcessAndClassify(
                process,
                ref processTerminationRequested,
                ref processCleanupFailed,
                ref qemuExitedSpontaneously,
                ref qemuExitCode);
            break;
        }

        if (serialReadOffset != previousSerialReadOffset)
        {
            lastSerialActivityMilliseconds = stopwatch.ElapsedMilliseconds;
        }

        if (options.ServiceQuotaFailure && streamingMarkers.SuccessTerminalCount != 0)
        {
            ServiceEvidence quota = options.ServiceEvidence!;
            if (quota.TerminalBytes is null) quota.TerminalBytes = serialReadOffset;
            else if (quota.TerminalBytes != serialReadOffset)
            {
                quota.Failure = "bytes_after_quota_terminal";
                TerminateProcessAndClassify(process, ref processTerminationRequested,
                    ref processCleanupFailed, ref qemuExitedSpontaneously, ref qemuExitCode);
                break;
            }
        }

        if (networkPeer is not null && streamingMarkers.HasSeenRequiredMarker("M07_026_ROW stage=3")) networkPeer.AllowTcp();

        if (options.StartupTimeoutSeconds <= options.TimeoutSeconds &&
            startup?.HasExpired(stopwatch.ElapsedMilliseconds, streamingMarkers) == true)
        {
            timedOut = startupTimedOut = true;
            QemuStartupCapture.CaptureBestEffort(process, options.SerialLogPath);
            TerminateProcessAndClassify(process, ref processTerminationRequested,
                ref processCleanupFailed, ref qemuExitedSpontaneously, ref qemuExitCode);
            break;
        }

        if (stopwatch.ElapsedMilliseconds >= options.TimeoutSeconds * 1000L)
        {
            timedOut = true;
            TerminateProcessAndClassify(
                process,
                ref processTerminationRequested,
                ref processCleanupFailed,
                ref qemuExitedSpontaneously,
                ref qemuExitCode);
            break;
        }

        if (streamingMarkers.ForbiddenOccurrenceCount != 0)
        {
            forbiddenMarkerSeen = true;
            TerminateProcessAndClassify(
                process,
                ref processTerminationRequested,
                ref processCleanupFailed,
                ref qemuExitedSpontaneously,
                ref qemuExitCode);
            break;
        }

        if (streamingMarkers.DuplicateUniqueOccurrenceCount != 0)
        {
            TerminateProcessAndClassify(
                process,
                ref processTerminationRequested,
                ref processCleanupFailed,
                ref qemuExitedSpontaneously,
                ref qemuExitCode);
            break;
        }

        if (networkPeer?.Completion.IsFaulted == true || networkPeer?.Completion.IsCanceled == true ||
            containmentPeer?.Completion.IsFaulted == true || containmentPeer?.Completion.IsCanceled == true || webFrames?.Completion.IsCompleted == true ||
            serviceControlTask?.IsFaulted == true || serviceControlTask?.IsCanceled == true)
        {
            networkPeerInitiatedFailure = true;
            if (options.ServiceEvidence is not null) options.ServiceEvidence.Failure = ServiceControlFailure(serviceControlTask) ?? "frame_owner_ended";
            TerminateProcessAndClassify(process, ref processTerminationRequested,
                ref processCleanupFailed, ref qemuExitedSpontaneously, ref qemuExitCode);
            break;
        }

        if (streamingMarkers.RequiredSeenCount >= probeMarkers.Count)
        {
            markerSeen = true;
            if (options.HttpProbe is not null)
            {
                break;
            }

            if (stopwatch.ElapsedMilliseconds - lastSerialActivityMilliseconds >=
                terminalDrainMilliseconds)
            {
                if (options.ServiceEvidence is not null)
                {
                    if (!options.ServiceQuotaFailure && serviceControlTask?.IsCompletedSuccessfully != true)
                        options.ServiceEvidence.Failure = ServiceControlFailure(serviceControlTask) ?? "socket_controls_incomplete";
                    else if (streamingMarkers.SuccessTerminalCount != 1 || streamingMarkers.FailureTerminalCount != 0 ||
                        streamingMarkers.OutOfOrderOccurrenceCount != 0 || process.HasExited)
                        options.ServiceEvidence.Failure = "terminal_not_healthy";
                    else
                    {
                        if (options.ServiceQuotaFailure)
                            options.ServiceEvidence.QuietMilliseconds = stopwatch.ElapsedMilliseconds - lastSerialActivityMilliseconds;
                        webFrames!.ExpectGuestExit();
                    }
                }
                TerminateProcessAndClassify(
                    process,
                    ref processTerminationRequested,
                    ref processCleanupFailed,
                    ref qemuExitedSpontaneously,
                    ref qemuExitCode);
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
    if (serialReadFailure is null &&
        !TryReadSerialLogBounded(
            options.SerialLogPath,
            out preProbeSerial,
            out serialReadFailure, options.SerialByteCapacity))
    {
        TerminateProcessAndClassify(
            process,
            ref processTerminationRequested,
            ref processCleanupFailed,
            ref qemuExitedSpontaneously,
            ref qemuExitCode);
    }

    SerialMarkerSummary preProbeSummary = SerialMarkerSummary.Parse(
        preProbeSerial,
        probeMarkers,
        options.ForbiddenMarkers,
        probeOrderedMarkers);
    if (options.HttpProbe is not null
        && !timedOut
        && preProbeSummary.HasAllRequiredMarkers
        && !preProbeSummary.HasForbiddenMarkers)
    {
        if (options.ContinuingWeb)
        {
            ContinuingWebEvidence web = options.WebEvidence!;
            HttpProbeResult Probe(HttpProbeOptions probe)
            {
                DateTime first = DateTime.UtcNow;
                try { return ExecuteContinuingWebProbe(probe, stopwatch, options.TimeoutSeconds); }
                finally { web.ProbeWindows.Add((first, DateTime.UtcNow)); }
            }
            if (process.HasExited)
                web.Failure = "premature_exit";
            else
            {
                web.AliveBeforeHealth = true;
                httpProbeResult = Probe(options.HttpProbe.Value);
                web.AliveAfterHealth = !process.HasExited;
                if (!web.AliveAfterHealth) web.Failure = "premature_exit";
                else if (httpProbeResult.Value.Passed)
                {
                    bool serialHealthy = TryFeedSerialLogDelta(options.SerialLogPath, streamingMarkers,
                        ref serialReadOffset, out SerialMarkerFeedResult betweenProbes, out serialReadFailure)
                        && betweenProbes.Succeeded && streamingMarkers.ForbiddenOccurrenceCount == 0
                        && streamingMarkers.OutOfOrderOccurrenceCount == 0 && streamingMarkers.DuplicateUniqueOccurrenceCount == 0;
                    if (!serialHealthy) web.Failure = "serial_failure";
                    else
                    {
                        web.ConfigResult = Probe(options.WebConfigProbe!.Value);
                        web.AliveAfterConfig = !process.HasExited;
                        if (!web.AliveAfterConfig) web.Failure = "premature_exit";
                        else if (web.ConfigResult.Value.Passed)
                        {
                            // All probes use the same owned process and remaining
                            // deadline. A failed step never advances to a later one.
                            foreach (int step in options.WebVectorProbe is null ? new[] { 0, 1, 3 } : new[] { 0, 1, 2, 3 })
                            {
                                bool healthy = TryFeedSerialLogDelta(options.SerialLogPath, streamingMarkers,
                                    ref serialReadOffset, out SerialMarkerFeedResult delta, out serialReadFailure)
                                    && delta.Succeeded && streamingMarkers.ForbiddenOccurrenceCount == 0
                                    && streamingMarkers.OutOfOrderOccurrenceCount == 0 && streamingMarkers.DuplicateUniqueOccurrenceCount == 0;
                                if (!healthy) { web.Failure = "serial_failure"; break; }
                                HttpProbeOptions probe = step switch { 0 => options.WebLogProbe!.Value,
                                    1 => options.WebLogRepeatProbe!.Value, 2 => options.WebVectorProbe!.Value, _ => options.HttpProbe.Value };
                                HttpProbeResult result = Probe(probe);
                                bool alive = !process.HasExited;
                                if (step == 0) { web.LogResult = result; web.AliveAfterLog = alive; }
                                else if (step == 1) { web.LogRepeatResult = result; web.AliveAfterLogRepeat = alive; }
                                else if (step == 2) { web.VectorResult = result; web.AliveAfterVectors = alive; }
                                else { web.HealthAfterResult = result; web.AliveAfterFinalHealth = alive; }
                                if (!alive) { web.Failure = "premature_exit"; break; }
                                if (!result.Passed) { web.Failure = (step == 2 ? "vector_http_" : "logging_http_") + result.Reason; break; }
                            }
                            if(web.Failure is null && web.HealthAfterResult?.Passed==true)
                            {
                                try
                                {
                                    long remaining=options.TimeoutSeconds*1000L-stopwatch.ElapsedMilliseconds;
                                    if(remaining<45_000)throw new TimeoutException("Web control budget.");
                                    web.Controls=WebSocketControls.RunAsync(options.SocketForwardPort!.Value,webFrames!,options.SerialLogPath,
                                        TimeSpan.FromMilliseconds(Math.Min(120_000,remaining))).GetAwaiter().GetResult();
                                    foreach(var exchange in new[]{web.Controls.Deadline,web.Controls.Before,web.Controls.After})
                                        web.ProbeWindows.Add((exchange.StartedUtc.UtcDateTime,exchange.CompletedUtc.UtcDateTime));
                                    web.AliveAfterControls=!process.HasExited;
                                    if(!web.AliveAfterControls)web.Failure="premature_exit";
                                }
                                catch(Exception failure)when(failure is IOException or InvalidDataException or OperationCanceledException or TimeoutException or SocketException or InvalidOperationException)
                                {web.Failure="socket_controls_"+failure.GetType().Name;}
                                if(web.Failure is null&&options.WebRacePipe is not null)
                                {
                                    try
                                    {
                                        long remaining=options.TimeoutSeconds*1000L-stopwatch.ElapsedMilliseconds;
                                        if(!options.WebEventOwnedSignalRace&&remaining<35_000)throw new TimeoutException("Web race handoff budget.");
                                        bool Healthy()=>!process.HasExited&&!webFrames!.Completion.IsCompleted&&
                                            TryFeedSerialLogDelta(options.SerialLogPath,streamingMarkers,ref serialReadOffset,
                                                out SerialMarkerFeedResult delta,out serialReadFailure)&&delta.Succeeded&&
                                            streamingMarkers.ForbiddenOccurrenceCount==0&&streamingMarkers.OutOfOrderOccurrenceCount==0&&
                                            streamingMarkers.DuplicateUniqueOccurrenceCount==0;
                                        web.RaceGate=options.WebEventOwnedSignalRace?
                                            WebRaceRendezvous.WaitEventOwnedInRunnerAsync(options.WebRacePipe,options.SocketForwardPort!.Value,process.Id,
                                                TimeSpan.FromSeconds(165),()=>TimeSpan.FromMilliseconds(options.TimeoutSeconds*1000L-stopwatch.ElapsedMilliseconds),Healthy).GetAwaiter().GetResult():
                                            WebRaceRendezvous.WaitInRunnerAsync(options.WebRacePipe,options.SocketForwardPort!.Value,process.Id,
                                                TimeSpan.FromMilliseconds(Math.Min(45_000,remaining)),Healthy).GetAwaiter().GetResult();
                                        web.AliveAfterRaceGate=Healthy();if(!web.AliveAfterRaceGate)web.Failure="race_gate_guest_unhealthy";
                                    }
                                    catch(Exception failure)when(failure is IOException or InvalidDataException or OperationCanceledException or TimeoutException or SocketException or InvalidOperationException or UnauthorizedAccessException)
                                    {web.Failure=options.WebEventOwnedSignalRace&&failure is InvalidDataException{Message:"web_race_insufficient_absolute_budget"}?
                                        "race_gate_insufficient_absolute_budget":"race_gate_"+failure.GetType().Name;}
                                }
                            }
                        }
                    }
                }
            }
        }
        else httpProbeResult = ExecuteHttpProbe(options.HttpProbe.Value);
        webFrames?.ExpectGuestExit();
        TerminateProcessAndClassify(
            process,
            ref processTerminationRequested,
            ref processCleanupFailed,
            ref qemuExitedSpontaneously,
            ref qemuExitCode);
        if (process.HasExited)
        {
            qemuExitCode = process.ExitCode;
        }
    }

    if (writable is not null)
    {
        if (!process.HasExited)
        {
            processCleanupFailed = true;
            AwaitWritableChildJoin(process);
        }
        try { writable.CompleteAfterChildJoin(); }
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
            receipt.LifetimeMilliseconds = (long)(process.ExitTime-process.StartTime).TotalMilliseconds;
            receipt.StoppedEndpointRefused = CheckWebEndpointRefused(options.SocketForwardPort!.Value);
        }
        if (receipt.CleanupFailed) receipt.Failure = "child_cleanup_failed";
        else if (receipt.SpontaneousExit) receipt.Failure = "premature_exit";
        else if (!receipt.DeathConfirmed || !receipt.TerminationRequested) receipt.Failure = "termination_unconfirmed";
        else if (receipt.StoppedEndpointRefused != true) receipt.Failure = "endpoint_live_after_stop";
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
        if (web.SpontaneousExit) web.Failure = "premature_exit";
        else if (!web.DeathConfirmed || !web.TerminationRequested) web.Failure = "termination_unconfirmed";
        else if (web.StoppedEndpointRefused != true) web.Failure = "endpoint_live_after_stop";
    }
    if (options.ServiceEvidence is not null)
    {
        ServiceEvidence service = options.ServiceEvidence;
        if (!options.ServiceQuotaFailure)
        {
            if (serviceControlTask?.IsCompletedSuccessfully != true)
                service.Failure ??= ServiceControlFailure(serviceControlTask) ?? "socket_controls_incomplete";
            if (!JoinServiceControls(serviceControlTask, serviceCancellation!)) service.Failure ??= "socket_controls_join_failed";
            if (serviceControlTask?.IsCompletedSuccessfully == true) service.Controls = serviceControlTask.GetAwaiter().GetResult();
        }
        else
        {
            service.TerminationRequested = processTerminationRequested; service.DeathConfirmed = process.HasExited;
            service.SpontaneousExit = qemuExitedSpontaneously; service.CleanupFailed = processCleanupFailed;
            service.ExitCode = qemuExitCode;
            if (processCleanupFailed) service.Failure ??= "qemu_cleanup_failed";
        }
        if (process.HasExited)
        {
            service.EndUtc = process.ExitTime.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture);
            service.LifetimeMilliseconds = (long)(process.ExitTime - process.StartTime).TotalMilliseconds;
            service.StoppedEndpointRefused = CheckWebEndpointRefused(options.SocketForwardPort!.Value);
        }
        if (qemuExitedSpontaneously) service.Failure ??= "premature_exit";
        else if (!process.HasExited || !processTerminationRequested) service.Failure ??= "termination_unconfirmed";
        else if (service.StoppedEndpointRefused != true) service.Failure ??= "endpoint_live_after_stop";
    }

    if (serialReadFailure is null)
    {
        TryFeedSerialLogDelta(
            options.SerialLogPath,
            streamingMarkers,
            ref serialReadOffset,
            out _,
            out serialReadFailure);
    }

    markerSeen =
        streamingMarkers.RequiredSeenCount ==
        streamingMarkers.RequiredMarkerCount;

    SerialMarkerCompletionResult streamingResult =
        streamingMarkers.Complete(timedOut);
    Console.WriteLine(
        "SERIAL_MARKER_STREAM status=" + streamingResult.Status +
        " terminal=" + streamingResult.TerminalState +
        " bytes=" + streamingResult.TotalBytes +
        " lines=" + streamingResult.LinesProcessed +
        " invalidBytes=" + streamingResult.InvalidByteCount +
        " invalidLines=" + streamingResult.InvalidNoiseLineCount +
        " orderedSeen=" + streamingResult.OrderedSeenCount +
        " outOfOrder=" + streamingResult.OutOfOrderOccurrenceCount +
        " uniqueSeen=" + streamingResult.UniqueSeenCount +
        " duplicateUnique=" +
        streamingResult.DuplicateUniqueOccurrenceCount);
    Console.WriteLine(
        "SERIAL_MARKER_DRAIN milliseconds=" + terminalDrainMilliseconds);
    Console.WriteLine(
        "QEMU_PROCESS_RESULT disposition=" +
        (processCleanupFailed
            ? "cleanup-failed"
            : processTerminationRequested
                ? "runner-terminated"
                : qemuExitedSpontaneously
                    ? "exited"
                    : "running") +
        " exitCode=" +
        (qemuExitCode.HasValue ? qemuExitCode.Value.ToString() : "none"));

    string serial = string.Empty;
    if (serialReadFailure is null)
    {
        TryReadSerialLogBounded(
            options.SerialLogPath,
            out serial,
            out serialReadFailure, options.SerialByteCapacity);
    }

    SerialMarkerSummary summary = SerialMarkerSummary.Parse(
        serial,
        options.RequiredMarkers,
        options.ForbiddenMarkers,
        options.OrderedMarkers);
    IReadOnlyList<string> observedMarkerLines =
        FindObservedRunnerMarkerLines(serial, options);
    IReadOnlyList<string> forbiddenMarkers = summary.ForbiddenMarkerHits;
    IReadOnlyList<string> streamingOutOfOrderMarkers =
        streamingResult.OutOfOrderMarkerHits ?? Array.Empty<string>();
    IReadOnlyList<string> outOfOrderMarkers = MergeDistinctMarkers(
        summary.OutOfOrderMarkers,
        streamingOutOfOrderMarkers);
    IReadOnlyList<string> duplicateUniqueMarkers =
        streamingResult.DuplicateUniqueMarkerHits ?? Array.Empty<string>();
    string? networkPeerFailure = null;
    if(options.ContinuingWeb && webFrames is not null)
    {
        var capture=webFrames.Finish();options.WebEvidence!.Frames=capture;
        if(!capture.Stopped || !capture.Healthy)options.WebEvidence.Failure="web_frame_owner_failed";
    }
    if (options.ServiceEvidence is not null)
    {
        if (options.ServiceQuotaFailure && streamingResult.SuccessTerminalCount != 0)
            options.ServiceEvidence.Failure ??= ServiceQuotaCapture.Failure(serial);
        var frames = webFrames!.FinishService(); options.ServiceEvidence.Frames = frames;
        if (!frames.Session.Stopped || !frames.Session.Healthy || !frames.Session.GuestExitExpected)
            options.ServiceEvidence.Failure ??= "frame_owner_failed";
        if (options.ServiceQuotaFailure && (frames.Session.ForwardedFrames <= 0 || frames.ControlledPorts.Count != 0 ||
            frames.Injections.Count != 0 || frames.Session.DroppedFrames.Count != 0 || frames.Session.VlanFrames != 0))
            options.ServiceEvidence.Failure ??= "quota_frame_owner_shape";
        StringBuilder serviceRows = new(); AppendServiceEvidence(serviceRows, options.ServiceEvidence); Console.Write(serviceRows.ToString());
    }
    if (containmentPeer is not null)
    {
        bool completed = containmentPeer.Completion.IsCompletedSuccessfully;
        bool stopped = containmentPeer.Stop();
        Console.WriteLine("SOCKET_CONTAINMENT_PEER_RESULT complete=" + completed + " stopped=" + stopped +
            " arpReplies=" + containmentPeer.ArpReplies + " headTransmissions=" + containmentPeer.HeadTransmissions +
            " healthyReplies=" + containmentPeer.HealthyReplies + " vlanFrames=" + containmentPeer.VlanFrames);
        if (!completed || !stopped)
            networkPeerFailure = !stopped ? "socket_containment_peer_cleanup_failed" : containmentPeer.Completion.IsFaulted
                ? "socket_containment_peer_failed_" + containmentPeer.Completion.Exception!.GetBaseException().GetType().Name : "socket_containment_peer_incomplete";
    }
    if (networkPeer is not null)
    {
        bool completed = networkPeer.Completion.IsCompletedSuccessfully;
        bool stopped = networkPeer.Stop();
        Console.WriteLine("NETWORK_PEER_RESULT complete=" + completed + " stopped=" + stopped +
            " arpReplies=" + networkPeer.ArpReplies + " echoReplies=" + networkPeer.EchoReplies + " tcpPackets=" + networkPeer.TcpPackets);
        if (!completed || !stopped)
        {
            networkPeerFailure = !stopped ? "network_peer_cleanup_failed" : networkPeer.Completion.IsFaulted
                ? "network_peer_failed_" + networkPeer.Completion.Exception!.GetBaseException().GetType().Name
                : "network_peer_incomplete";
        }
    }
    if (serialReadFailure is not null ||
        IsStreamingStructuralFailure(streamingResult.Status))
    {
        string failureReason = serialReadFailure ??
            "serial_marker_stream_" +
            streamingResult.Status.ToString().ToLowerInvariant();
        WriteSummaryLogBestEffort(
            options,
            "fail",
            observedMarkerLines,
            summary.MissingMarkers,
            forbiddenMarkers,
            failureReason,
            httpProbeResult,
            summary.AppExitResults,
            summary.MalformedAppExitMarkers,
            outOfOrderMarkers,
            duplicateUniqueMarkers);
        Console.Error.WriteLine(
            "TEST_FAIL qemu.smoke serial_marker_stream status=" +
            streamingResult.Status +
            " reason=" + failureReason);
        return 13;
    }

    if (forbiddenMarkerSeen ||
        streamingResult.Status == SerialMarkerCompletionStatus.ForbiddenMarker ||
        forbiddenMarkers.Count != 0)
    {
        WriteSummaryLogBestEffort(
            options,
            "fail",
            observedMarkerLines,
            summary.MissingMarkers,
            forbiddenMarkers,
            "forbidden_marker",
            httpProbeResult,
            summary.AppExitResults,
            summary.MalformedAppExitMarkers,
            outOfOrderMarkers,
            duplicateUniqueMarkers);
        string firstForbiddenMarker = forbiddenMarkers.Count == 0 ? "unknown" : forbiddenMarkers[0];
        Console.Error.WriteLine("OS_SMOKE_FAIL forbidden-marker=" + firstForbiddenMarker);
        Console.Error.WriteLine("TEST_FAIL qemu.smoke forbidden_marker marker=" + firstForbiddenMarker);
        return 8;
    }

    bool hasStreamingOrderViolation =
        streamingResult.Status ==
            SerialMarkerCompletionStatus.OutOfOrderMarker ||
        streamingOutOfOrderMarkers.Count != 0;
    if (summary.MissingMarkers.Count == 0 &&
        (hasStreamingOrderViolation ||
            summary.OutOfOrderMarkers.Count != 0))
    {
        WriteSummaryLogBestEffort(
            options,
            "fail",
            observedMarkerLines,
            summary.MissingMarkers,
            Array.Empty<string>(),
            "out_of_order_marker",
            httpProbeResult,
            summary.AppExitResults,
            summary.MalformedAppExitMarkers,
            outOfOrderMarkers,
            duplicateUniqueMarkers);
        Console.Error.WriteLine("TEST_FAIL qemu.smoke out_of_order_marker markers=" + FormatMarkers(outOfOrderMarkers));
        return 10;
    }

    if (streamingResult.Status ==
            SerialMarkerCompletionStatus.DuplicateUniqueMarker ||
        duplicateUniqueMarkers.Count != 0)
    {
        WriteSummaryLogBestEffort(
            options,
            "fail",
            observedMarkerLines,
            summary.MissingMarkers,
            Array.Empty<string>(),
            "duplicate_unique_marker",
            httpProbeResult,
            summary.AppExitResults,
            summary.MalformedAppExitMarkers,
            outOfOrderMarkers,
            duplicateUniqueMarkers);
        Console.Error.WriteLine(
            "TEST_FAIL qemu.smoke duplicate_unique_marker markers=" +
            FormatMarkers(duplicateUniqueMarkers));
        return 17;
    }

    if (processCleanupFailed)
    {
        WriteSummaryLogBestEffort(
            options,
            "fail",
            observedMarkerLines,
            summary.MissingMarkers,
            forbiddenMarkers,
            "qemu_cleanup_failed",
            httpProbeResult,
            summary.AppExitResults,
            summary.MalformedAppExitMarkers,
            outOfOrderMarkers,
            duplicateUniqueMarkers);
        Console.Error.WriteLine("TEST_FAIL qemu.smoke qemu_cleanup_failed");
        return 15;
    }

    if (options.ContinuingWeb && options.WebEvidence!.Failure is string webFailure)
    {
        WriteSummaryLogBestEffort(options, "fail", observedMarkerLines, summary.MissingMarkers,
            forbiddenMarkers, "web_" + webFailure, httpProbeResult, summary.AppExitResults,
            summary.MalformedAppExitMarkers, outOfOrderMarkers, duplicateUniqueMarkers);
        Console.Error.WriteLine("TEST_FAIL qemu.smoke web_" + webFailure);
        return 20;
    }
    if (options.ServiceEvidence?.Failure is string serviceFailure)
    {
        WriteSummaryLogBestEffort(options, "fail", observedMarkerLines, summary.MissingMarkers, forbiddenMarkers,
            "service_" + serviceFailure, httpProbeResult, summary.AppExitResults, summary.MalformedAppExitMarkers, outOfOrderMarkers, duplicateUniqueMarkers);
        Console.Error.WriteLine("TEST_FAIL qemu.smoke service_" + serviceFailure); return 21;
    }
    if (options.StaticEvidence?.Failure is string staticFailure)
    {
        WriteSummaryLogBestEffort(options, "fail", observedMarkerLines, summary.MissingMarkers, forbiddenMarkers,
            "static_" + staticFailure, httpProbeResult, summary.AppExitResults, summary.MalformedAppExitMarkers, outOfOrderMarkers, duplicateUniqueMarkers);
        Console.Error.WriteLine("TEST_FAIL qemu.smoke static_" + staticFailure); return 23;
    }

    if (qemuExitedSpontaneously && qemuExitCode is not null and not 0)
    {
        WriteSummaryLogBestEffort(
            options,
            "fail",
            observedMarkerLines,
            summary.MissingMarkers,
            forbiddenMarkers,
            "qemu_exit_nonzero",
            httpProbeResult,
            summary.AppExitResults,
            summary.MalformedAppExitMarkers,
            outOfOrderMarkers,
            duplicateUniqueMarkers);
        Console.Error.WriteLine(
            "TEST_FAIL qemu.smoke qemu_exit_nonzero code=" +
            qemuExitCode.Value);
        return 14;
    }

    bool uniqueEvidenceMode = options.UniqueMarkers.Count != 0;
    if (uniqueEvidenceMode &&
        qemuExitedSpontaneously &&
        qemuExitCode == 0 &&
        markerSeen &&
        streamingResult.Passed &&
        summary.HasAllRequiredMarkers)
    {
        WriteSummaryLogBestEffort(
            options,
            "fail",
            observedMarkerLines,
            summary.MissingMarkers,
            Array.Empty<string>(),
            "qemu_reboot_or_shutdown",
            httpProbeResult,
            summary.AppExitResults,
            summary.MalformedAppExitMarkers,
            outOfOrderMarkers,
            duplicateUniqueMarkers);
        Console.Error.WriteLine(
            "TEST_FAIL qemu.smoke qemu_reboot_or_shutdown exitCode=0");
        return 18;
    }

    bool serialSucceeded = markerSeen &&
        streamingResult.Passed &&
        streamingResult.TerminalState == SerialMarkerTerminalState.Success &&
        streamingResult.SuccessTerminalCount == 1 &&
        summary.HasAllRequiredMarkers;
    // Stopping QEMU for a primary failure can itself break the peer transport.
    // Reject an initiating peer failure or an otherwise successful serial run;
    // retain the original guest, process and timeout diagnosis otherwise.
    if (networkPeerFailure is not null && !timedOut && httpProbeResult?.Passed != false &&
        ((networkPeerInitiatedFailure && !qemuExitedSpontaneously) || serialSucceeded))
    {
        WriteSummaryLogBestEffort(options, "fail", observedMarkerLines, summary.MissingMarkers,
            forbiddenMarkers, networkPeerFailure, httpProbeResult, summary.AppExitResults,
            summary.MalformedAppExitMarkers, outOfOrderMarkers, duplicateUniqueMarkers);
        Console.Error.WriteLine("TEST_FAIL qemu.smoke " + networkPeerFailure);
        return 19;
    }

    if (options.ServiceQuotaFailure && serialSucceeded)
    {
        ServiceEvidence quota = options.ServiceEvidence!;
        if (quota.QuietMilliseconds is not >= 750 || quota.TerminalBytes != streamingResult.TotalBytes ||
            !quota.TerminationRequested || !quota.DeathConfirmed || quota.SpontaneousExit || quota.CleanupFailed || quota.ExitCode is null)
        {
            WriteSummaryLogBestEffort(options, "fail", observedMarkerLines, [], [], "quota_capture_incomplete");
            Console.Error.WriteLine("TEST_FAIL qemu.smoke quota_capture_incomplete"); return 21;
        }
        if (!WriteSummaryLogBestEffort(options, "expected-quota", observedMarkerLines, [], [], "none",
                httpProbeResult, summary.AppExitResults, summary.MalformedAppExitMarkers, outOfOrderMarkers, duplicateUniqueMarkers))
        { Console.Error.WriteLine("TEST_FAIL qemu.smoke quota_summary_failed"); return 21; }
        Console.WriteLine("QEMU_EXPECTED_SERVICE_QUOTA outcome=captured acceptance=pending");
        return 22;
    }

    if (serialSucceeded)
    {
        if (options.ContinuingWeb && httpProbeResult?.Passed == true && options.WebEvidence!.ConfigResult?.Passed != true)
        {
            string reason = options.WebEvidence.ConfigResult?.Reason ?? "not_run";
            WriteSummaryLogBestEffort(options, "fail", observedMarkerLines, [], [], "web_config_" + reason,
                httpProbeResult, summary.AppExitResults, summary.MalformedAppExitMarkers,
                outOfOrderMarkers, duplicateUniqueMarkers);
            Console.Error.WriteLine("TEST_FAIL qemu.smoke web_config_" + reason);
            return 20;
        }
        if (options.ContinuingWeb && httpProbeResult?.Passed == true &&
            (options.WebEvidence!.LogResult?.Passed != true || options.WebEvidence.LogRepeatResult?.Passed != true ||
             (options.WebVectorProbe is not null && options.WebEvidence.VectorResult?.Passed != true) ||
             options.WebEvidence.HealthAfterResult?.Passed != true))
        {
            WriteSummaryLogBestEffort(options, "fail", observedMarkerLines, [], [], "web_logging_incomplete",
                httpProbeResult, summary.AppExitResults, summary.MalformedAppExitMarkers, outOfOrderMarkers, duplicateUniqueMarkers);
            Console.Error.WriteLine("TEST_FAIL qemu.smoke web_logging_incomplete"); return 20;
        }
        if (options.HttpProbe is not null)
        {
            if (!options.ContinuingWeb) httpProbeResult ??= ExecuteHttpProbe(options.HttpProbe.Value);
            TerminateProcessAndClassify(
                process,
                ref processTerminationRequested,
                ref processCleanupFailed,
                ref qemuExitedSpontaneously,
                ref qemuExitCode);

            if (httpProbeResult?.Passed != true)
            {
                HttpProbeResult failedProbe = httpProbeResult ?? HttpProbeResult.Failed("not_run", 0, string.Empty);
                WriteSummaryLogBestEffort(
                    options,
                    "fail",
                    observedMarkerLines,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    "http_probe_" + failedProbe.Reason,
                    httpProbeResult,
                    summary.AppExitResults,
                    summary.MalformedAppExitMarkers,
                    outOfOrderMarkers,
                    duplicateUniqueMarkers);
                Console.Error.WriteLine("OS_SMOKE_FAIL http-probe=" + failedProbe.Reason);
                Console.Error.WriteLine("TEST_FAIL qemu.runner.http_probe reason=" + failedProbe.Reason);
                return 9;
            }
        }

        if (processCleanupFailed)
        {
            WriteSummaryLogBestEffort(
                options,
                "fail",
                observedMarkerLines,
                summary.MissingMarkers,
                forbiddenMarkers,
                "qemu_cleanup_failed",
                httpProbeResult,
                summary.AppExitResults,
                summary.MalformedAppExitMarkers,
                outOfOrderMarkers,
                duplicateUniqueMarkers);
            Console.Error.WriteLine(
                "TEST_FAIL qemu.smoke qemu_cleanup_failed");
            return 15;
        }

        if (qemuExitedSpontaneously &&
            qemuExitCode is not null and not 0)
        {
            WriteSummaryLogBestEffort(
                options,
                "fail",
                observedMarkerLines,
                summary.MissingMarkers,
                forbiddenMarkers,
                "qemu_exit_nonzero",
                httpProbeResult,
                summary.AppExitResults,
                summary.MalformedAppExitMarkers,
                outOfOrderMarkers,
                duplicateUniqueMarkers);
            Console.Error.WriteLine(
                "TEST_FAIL qemu.smoke qemu_exit_nonzero code=" +
                qemuExitCode.Value);
            return 14;
        }

        WriteSummaryLogBestEffort(
            options,
            "pass",
            observedMarkerLines,
            Array.Empty<string>(),
            Array.Empty<string>(),
            "none",
            httpProbeResult,
            summary.AppExitResults,
            summary.MalformedAppExitMarkers,
            outOfOrderMarkers,
            duplicateUniqueMarkers);
        Console.WriteLine("TEST_PASS qemu.smoke");
        return 0;
    }

    if (streamingResult.Status ==
        SerialMarkerCompletionStatus.MissingSuccessTerminal)
    {
        WriteSummaryLogBestEffort(
            options,
            "fail",
            observedMarkerLines,
            summary.MissingMarkers,
            Array.Empty<string>(),
            "missing_success_terminal",
            httpProbeResult,
            summary.AppExitResults,
            summary.MalformedAppExitMarkers,
            outOfOrderMarkers,
            duplicateUniqueMarkers);
        Console.Error.WriteLine(
            "TEST_FAIL qemu.smoke missing_success_terminal marker=" +
            options.SuccessTerminalMarker);
        return 16;
    }

    IReadOnlyList<string> missingMarkers =
        streamingResult.Status ==
            SerialMarkerCompletionStatus.MissingRequiredMarker &&
        streamingResult.MissingRequiredMarkers is not null
            ? streamingResult.MissingRequiredMarkers
            : summary.MissingMarkers;
    if (timedOut)
    {
        string timeoutReason = startupTimedOut ? "startup_timeout" : "timeout";
        WriteSummaryLogBestEffort(
            options,
            "fail",
            observedMarkerLines,
            missingMarkers,
            Array.Empty<string>(),
            timeoutReason,
            httpProbeResult,
            summary.AppExitResults,
            summary.MalformedAppExitMarkers,
            outOfOrderMarkers,
            duplicateUniqueMarkers);
        Console.Error.WriteLine("TEST_FAIL qemu.smoke " + timeoutReason);
        return 4;
    }

    WriteSummaryLogBestEffort(
        options,
        "fail",
        observedMarkerLines,
        missingMarkers,
        Array.Empty<string>(),
        "missing_marker",
        httpProbeResult,
        summary.AppExitResults,
        summary.MalformedAppExitMarkers,
        outOfOrderMarkers,
        duplicateUniqueMarkers);
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
                    Console.Error.WriteLine(
                        "QEMU_PROCESS_CLEANUP_FAILED timeout_ms=2000");
                }
            }
        }
        finally
        {
            if (writable is not null && processStarted)
            {
                AwaitWritableChildJoin(process);
                if (!writable.FinalVerificationAttempted) writable.CompleteAfterChildJoin();
            }
            if (serviceCancellation is not null && !JoinServiceControls(serviceControlTask, serviceCancellation))
                throw new TimeoutException("Service controls join incomplete; reserved ports remain owned.");
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
        while (!process.WaitForExit(250)) { }
    }
}

static int RunExpectedUnexpectedVectorPanic(RunnerOptions options, string qemu, string ovmf)
{
    bool owned = options.ExpectedOwnedSubsystemPanic;
    bool unhandled = options.ExpectedUnhandledException;
    string profile = unhandled ? "m04.unhandled-exception" : owned ? "m03.owned-subsystem" : "m02.unexpected-vector";
    string acceptedReason = unhandled ? "expected_unhandled_exception" : owned ? "expected_owned_subsystem_panic" : "expected_unexpected_vector_panic";
    int Evaluate(string text, out string prefix) => unhandled
        ? UnhandledExceptionCapture.Evaluate(text,out prefix) : owned
        ? OwnedSubsystemPanicCapture.Evaluate(text, out prefix)
        : UnexpectedVectorPanicCapture.Evaluate(text, out prefix);
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.SerialLogPath)) ?? ".");
    if (File.Exists(options.SerialLogPath)) File.Delete(options.SerialLogPath);
    if (!string.IsNullOrWhiteSpace(options.SummaryLogPath) && File.Exists(options.SummaryLogPath)) File.Delete(options.SummaryLogPath);
    Console.WriteLine("TEST_START qemu.smoke");
    Console.WriteLine("IMAGE_ARTIFACT " + options.ImagePath);
    Console.WriteLine("SERIAL_LOG " + options.SerialLogPath);
    Console.WriteLine("OVMF_CODE " + ovmf);
    Console.WriteLine("WORKLOAD_PROFILE " + options.WorkloadProfile);
    if (options.FirmwareRngSource is not null) Console.WriteLine("FIRMWARE_RNG_SOURCE " + options.FirmwareRngSource);
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
    foreach (string argument in QemuCommandBuilder.BuildArguments(options, ovmf)) process.StartInfo.ArgumentList.Add(argument);
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
        process.Start(); started = true;
        while (failure is null)
        {
            if (!TryReadSerialLogBounded(options.SerialLogPath, out serial, out failure)) break;
            if (!serial.StartsWith(prior, StringComparison.Ordinal)) { failure = "serial_log_reset"; break; }
            if (terminal is not null && !string.Equals(serial, terminal, StringComparison.Ordinal))
            { failure = "bytes_after_expected_panic"; break; }
            prior = serial;
            int parsed = Evaluate(serial, out string prerequisites);
            if (parsed < 0) { failure = "unexpected_vector_panic_capture_rejected"; break; }
            if (parsed == 1 && terminal is null)
            {
                // Only the independently classified exact last panic record is excluded.
                // Generic required/ordered/unique/forbidden handling still checks the prefix.
                var state = new SerialMarkerStateMachine(options.RequiredMarkers, options.ForbiddenMarkers,
                    successTerminalMarker: unhandled ? UnhandledExceptionCapture.PrerequisiteTerminal : SerialMarkerStateMachine.DefaultSuccessTerminalMarker,
                    orderedMarkers: options.OrderedMarkers, uniqueMarkers: options.UniqueMarkers);
                var feed = state.Feed(Encoding.ASCII.GetBytes(prerequisites));
                prefixResult = state.Complete(false);
                if (!feed.Succeeded || !prefixResult.Passed) { failure = "unexpected_vector_panic_prefix_markers"; break; }
                terminal = serial; terminalAt = clock.ElapsedMilliseconds;
            }
            if (clock.ElapsedMilliseconds >= options.TimeoutSeconds * 1000L) { failure = "timeout"; break; }
            if (process.HasExited) { spontaneous = true; nativeExit = process.ExitCode; failure = "qemu_reboot_or_shutdown"; break; }
            if (terminal is not null && clock.ElapsedMilliseconds - terminalAt >= quietMilliseconds)
            { measuredQuiet = clock.ElapsedMilliseconds - terminalAt; break; }
            Thread.Sleep(25);
        }
        TerminateProcessAndClassify(process, ref terminated, ref cleanupFailed, ref spontaneous, ref nativeExit);
        if (cleanupFailed) failure = "qemu_cleanup_failed";
        else if (spontaneous || !terminated || nativeExit is null) failure = "qemu_reboot_or_shutdown";
        if (!TryReadSerialLogBounded(options.SerialLogPath, out string finalSerial, out string? readFailure))
            failure = readFailure ?? "serial_log_read_failed";
        else if (!string.Equals(finalSerial, serial, StringComparison.Ordinal)) failure = "bytes_after_expected_panic";
        serial = finalSerial;
        if (failure is null && (terminal is null || measuredQuiet < quietMilliseconds ||
            !string.Equals(serial, terminal, StringComparison.Ordinal) || Evaluate(serial, out _) != 1))
            failure = "unexpected_vector_panic_incomplete";
        bool accepted = failure is null;
        Console.WriteLine("SERIAL_MARKER_STREAM status=" + (accepted ? "ExpectedPanic" : "Rejected") +
            " terminal=" + (accepted ? "ExpectedPanic" : "Rejected") + " bytes=" + Encoding.UTF8.GetByteCount(serial) +
            " lines=" + (accepted ? unhandled ? UnhandledExceptionCapture.RecordCount : owned ? 79 : OS.Tooling.QemuContracts.M02UnexpectedCaptureRecordCount : 0) + " invalidBytes=" + (accepted ? owned || unhandled ? OwnedSubsystemPanicCapture.InvalidBytes(serial).ToString(System.Globalization.CultureInfo.InvariantCulture) : "0" : "unknown") +
            " invalidLines=" + (accepted ? unhandled ? serial.Split("\r\n",StringSplitOptions.None).Count(l=>System.Text.RegularExpressions.Regex.IsMatch(l,@"[\x00-\x1f\x7f-\uffff]")).ToString(System.Globalization.CultureInfo.InvariantCulture) : owned ? "1" : "0" : "unknown") + " orderedSeen=" + prefixResult.OrderedSeenCount +
            " outOfOrder=" + prefixResult.OutOfOrderOccurrenceCount + " uniqueSeen=" + prefixResult.UniqueSeenCount +
            " duplicateUnique=" + prefixResult.DuplicateUniqueOccurrenceCount);
        Console.WriteLine("SERIAL_MARKER_DRAIN milliseconds=750 observedMilliseconds=" + measuredQuiet);
        Console.WriteLine("QEMU_PROCESS_RESULT disposition=" + (cleanupFailed ? "cleanup-failed" : terminated ? "runner-terminated" : spontaneous ? "exited" : "running") +
            " exitCode=" + (nativeExit.HasValue ? nativeExit.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "none"));
        string reason = accepted ? acceptedReason : failure!;
        if (!WriteExpectedPanicSummary(options, serial, accepted, reason, measuredQuiet, terminated, spontaneous, cleanupFailed, nativeExit))
        { Console.Error.WriteLine("TEST_FAIL qemu.smoke expected_panic_summary_failed"); return 16; }
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
    }
}

static bool WriteExpectedPanicSummary(RunnerOptions options, string serial, bool accepted, string reason,
    long quiet, bool terminated, bool spontaneous, bool cleanupFailed, int? nativeExit)
{
    if (string.IsNullOrWhiteSpace(options.SummaryLogPath)) return false;
    try
    {
        var b = new StringBuilder();
        b.Append("QEMU_SMOKE_STATUS ").AppendLine(accepted ? "expected-panic" : "fail");
        b.Append("IMAGE_ARTIFACT ").AppendLine(options.ImagePath);
        b.Append("SERIAL_LOG ").AppendLine(options.SerialLogPath);
        b.Append("WORKLOAD_PROFILE ").AppendLine(options.WorkloadProfile);
        if (options.FirmwareRngSource is not null) b.Append("FIRMWARE_RNG_SOURCE ").AppendLine(options.FirmwareRngSource);
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
        b.Append("QEMU_PROCESS_RESULT disposition=").Append(cleanupFailed ? "cleanup-failed" : terminated ? "runner-terminated" : spontaneous ? "exited" : "running")
            .Append(" exitCode=").AppendLine(nativeExit.HasValue ? nativeExit.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "none");
        byte[] raw = Encoding.UTF8.GetBytes(serial);
        b.Append("EXPECTED_PANIC_CAPTURE bytes=").Append(raw.Length).Append(" sha256=").AppendLine(Convert.ToHexString(SHA256.HashData(raw)));
        AppendSerialTailSummary(b, serial);
        string expected = b.ToString();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.SummaryLogPath)) ?? ".");
        File.WriteAllText(options.SummaryLogPath, expected, new UTF8Encoding(false));
        if (!string.Equals(File.ReadAllText(options.SummaryLogPath), expected, StringComparison.Ordinal)) return false;
        Console.WriteLine("SUMMARY_LOG " + options.SummaryLogPath);
        return true;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("SUMMARY_LOG_WRITE_FAILED path=" + options.SummaryLogPath + " reason=" + ex.GetType().Name);
        return false;
    }
}

static void TerminateProcessAndClassify(
    Process process,
    ref bool runnerTerminationIssued,
    ref bool cleanupFailed,
    ref bool qemuExitedSpontaneously,
    ref int? qemuExitCode)
{
    bool runnerTerminationWasAlreadyIssued = runnerTerminationIssued;
    (
        bool succeeded,
        bool killIssued,
        bool exitedBeforeKill,
        int? exitCode) = TerminateProcessBounded(process);

    runnerTerminationIssued |= killIssued;
    cleanupFailed |= !succeeded;
    qemuExitedSpontaneously |=
        exitedBeforeKill && !runnerTerminationWasAlreadyIssued;
    if (exitCode.HasValue)
    {
        qemuExitCode = exitCode;
    }
}

static void ValidateResolvedPathSeparation(
    RunnerOptions options,
    string? qemuPath,
    string? ovmfPath)
{
    List<(string Name, string Path)> paths =
    [
        ("image", options.ImagePath),
        ("serial-log", options.SerialLogPath)
    ];

    if (options.VirtioBlockBootImagePath is not null) paths.Add(("virtio-block-boot-image", options.VirtioBlockBootImagePath));
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

    StringComparer comparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
    Dictionary<string, string> nameByPath =
        new Dictionary<string, string>(comparer);
    foreach ((string name, string path) in paths)
    {
        string fullPath = Path.GetFullPath(path);
        if (options.WritableDataImagePath is not null && fullPath.IndexOfAny([',', '\r', '\n', '\0']) >= 0)
            throw new ArgumentException("Writable fixture runtime paths forbid QEMU option separators.");
        if (options.WritableDataImagePath is not null) WritableDataImageLease.RequirePlainRuntimePath(fullPath);
        if (nameByPath.TryGetValue(fullPath, out string? existing))
        {
            throw new ArgumentException(
                "Resolved runtime paths must be distinct: "
                + existing
                + " and "
                + name
                + " resolve to "
                + fullPath
                + ".");
        }

        nameByPath.Add(fullPath, name);
    }
}

static (
    bool Succeeded,
    bool KillIssued,
    bool ExitedBeforeKill,
    int? ExitCode) TerminateProcessBounded(Process process)
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

static bool TryFeedSerialLogDelta(
    string path,
    SerialMarkerStateMachine stateMachine,
    ref long offset,
    out SerialMarkerFeedResult feedResult,
    out string? failureReason)
{
    feedResult = stateMachine.Feed(ReadOnlySpan<byte>.Empty);
    failureReason = null;
    if (!feedResult.Succeeded || !File.Exists(path))
    {
        return feedResult.Succeeded;
    }

    try
    {
        using FileStream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
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
    return status is
        SerialMarkerCompletionStatus.CapacityExceeded or
        SerialMarkerCompletionStatus.InvalidByteSequence or
        SerialMarkerCompletionStatus.ConflictingTerminalMarkers or
        SerialMarkerCompletionStatus.DuplicateTerminalMarker or
        SerialMarkerCompletionStatus.FailureTerminal or
        SerialMarkerCompletionStatus.PartialMarkerAtEndOfStream or
        SerialMarkerCompletionStatus.Uninitialized;
}

static IReadOnlyList<string> MergeDistinctMarkers(
    IReadOnlyList<string> first,
    IReadOnlyList<string> second)
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

static IReadOnlyList<string> FindObservedRunnerMarkerLines(
    string serial,
    RunnerOptions options)
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

        if (IsStandardObservedMarkerLine(trimmed) ||
            LineMatchesAnyMarker(trimmed, options.RequiredMarkers) ||
            LineMatchesAnyMarker(trimmed, options.OrderedMarkers) ||
            LineMatchesAnyMarker(trimmed, options.UniqueMarkers) ||
            LineMatchesAnyMarker(trimmed, options.ForbiddenMarkers))
        {
            observed.Add(trimmed);
        }
    }

    return observed;
}

static bool IsStandardObservedMarkerLine(string line)
    => SerialMarkerSummary.IsStandardObservedMarkerLine(line);

static bool LineMatchesAnyMarker(
    string line,
    IReadOnlyList<string> markers)
{
    for (int index = 0; index < markers.Count; index++)
    {
        string marker = markers[index];
        if (string.Equals(line, marker, StringComparison.Ordinal) ||
            line.StartsWith(marker + " ", StringComparison.Ordinal))
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
    return values.Count == 0
        ? "none"
        : FormatMarkers(values);
}

static int RunPreflight(RunnerOptions options)
{
    if (options.WritableDataImagePath is not null)
        ValidateResolvedPathSeparation(options, null, string.IsNullOrEmpty(options.OvmfCodePath) ? null : options.OvmfCodePath);
    Console.WriteLine("TEST_START qemu.runner.preflight");
    Console.WriteLine("WORKLOAD_PROFILE " + options.WorkloadProfile);
    if (options.FirmwareRngSource is not null) Console.WriteLine("FIRMWARE_RNG_SOURCE " + options.FirmwareRngSource);
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

    using WritableDataImageLease? writable = options.WritableDataImagePath is null ? null :
        WritableDataImageLease.Open(options.ImagePath, options.WritableDataImagePath);
    if (writable is not null) foreach (string row in writable.Summary()) Console.WriteLine(row);

    string detectedDiskFormat;
    try
    {
        detectedDiskFormat = DetectImageFormat(options.ImagePath);
    }
    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
    {
        return FailPreflight("image_read_failed error=" + ex.GetType().Name, 11);
    }

    Console.WriteLine(
        "QEMU_PREFLIGHT_DISK_FORMAT expected=" + options.DiskFormat
        + " detected=" + detectedDiskFormat);

    if (string.Equals(options.DiskFormat, "qcow2", StringComparison.Ordinal) &&
        !string.Equals(detectedDiskFormat, "qcow2", StringComparison.Ordinal))
    {
        return FailPreflight("disk_format_mismatch expected=qcow2 detected=" + detectedDiskFormat, 12);
    }

    if (string.Equals(options.DiskFormat, "raw", StringComparison.Ordinal) &&
        string.Equals(detectedDiskFormat, "qcow2", StringComparison.Ordinal))
    {
        return FailPreflight("disk_format_mismatch expected=raw detected=qcow2", 12);
    }

    string serialDirectory = GetPathDirectoryOrCurrent(options.SerialLogPath);
    Console.WriteLine(
        "QEMU_PREFLIGHT_SERIAL_DIRECTORY path=" + serialDirectory
        + " exists=" + FormatBool(Directory.Exists(serialDirectory))
        + " runner_creates=true");

    bool ovmfConfigured = !string.IsNullOrWhiteSpace(options.OvmfCodePath);
    bool ovmfExists = ovmfConfigured && File.Exists(options.OvmfCodePath);
    Console.WriteLine(
        "QEMU_PREFLIGHT_OVMF configured=" + FormatBool(ovmfConfigured)
        + " exists=" + (ovmfConfigured ? FormatBool(ovmfExists) : "not_checked")
        + " path=" + (ovmfConfigured ? options.OvmfCodePath : "none"));
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
    if (bytesRead == 4 &&
        header[0] == (byte)'Q' &&
        header[1] == (byte)'F' &&
        header[2] == (byte)'I' &&
        header[3] == 0xFB)
    {
        return "qcow2";
    }

    return "raw-compatible";
}

static string GetPathDirectoryOrCurrent(string path)
{
    string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
    return string.IsNullOrWhiteSpace(directory)
        ? Directory.GetCurrentDirectory()
        : directory;
}

static string FormatBool(bool value)
{
    return value ? "true" : "false";
}

static bool WriteSummaryLogBestEffort(
    RunnerOptions options,
    string status,
    IReadOnlyList<string> observedMarkers,
    IReadOnlyList<string> missingMarkers,
    IReadOnlyList<string> forbiddenMarkers,
    string failReason,
    HttpProbeResult? httpProbeResult = null,
    IReadOnlyList<AppExitMarkerResult>? appExitResults = null,
    IReadOnlyList<string>? malformedAppExitMarkers = null,
    IReadOnlyList<string>? outOfOrderMarkers = null,
    IReadOnlyList<string>? duplicateUniqueMarkers = null)
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
        if (options.WritableDataLease is not null)
            foreach (string row in options.WritableDataLease.Summary()) builder.AppendLine(row);
        if (options.FirmwareRngSource is not null) builder.Append("FIRMWARE_RNG_SOURCE ").AppendLine(options.FirmwareRngSource);
        if (options.SocketForwardPort is int socketForward) builder.Append("SOCKET_FORWARD_PORT ").AppendLine(socketForward.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (options.ContinuingService) builder.AppendLine("CONTINUING_SERVICE true");
        if (options.ServiceQuotaFailure) builder.AppendLine("SERVICE_QUOTA_FAILURE true");
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
        if (TryReadSerialLogBounded(
                options.SerialLogPath,
                out string serial,
                out string? serialTailFailure, options.SerialByteCapacity))
        {
            AppendSerialTailSummary(builder, serial);
        }
        else
        {
            if (options.ServiceQuotaFailure) return false;
            builder.Append("SERIAL_TAIL_READ_ERROR ")
                .AppendLine(serialTailFailure ?? "unknown");
            AppendSerialTailSummary(builder, string.Empty);
        }
        if (options.ServiceEvidence is not null) AppendServiceEvidence(builder, options.ServiceEvidence);
        if (options.StaticEvidence is not null)
            foreach (string row in options.StaticEvidence.Summary()) builder.AppendLine(row);

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

    builder.Append("SERIAL_TAIL_LIMIT lines=")
        .Append(maxLines.ToString())
        .Append(" chars=")
        .AppendLine(maxChars.ToString());

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

static void AppendAppExitSummary(
    StringBuilder builder,
    IReadOnlyList<AppExitMarkerResult>? appExitResults,
    IReadOnlyList<string>? malformedAppExitMarkers)
{
    if (appExitResults is null || appExitResults.Count == 0)
    {
        builder.Append("APP_EXIT_RESULT none").AppendLine();
    }
    else
    {
        foreach (AppExitMarkerResult result in appExitResults)
        {
            builder.Append("APP_EXIT_RESULT code=")
                .Append(result.ExitCode.ToString())
                .Append(" status=")
                .AppendLine(result.Status);
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
    Console.WriteLine(
        "HTTP_PROBE url=" + probe.Url
        + " expected_status=" + probe.ExpectedStatusCode
        + " expected_body_bytes=" + probe.ExpectedBodyByteLength
        + " timeout_seconds=" + probe.TimeoutSeconds);
}

static bool JoinServiceControls(Task<ServiceSocketControls.Report>? task, CancellationTokenSource cancellation)
{
    cancellation.Cancel();
    if (task is null || task.IsCompleted) return true;
    try { return task.Wait(TimeSpan.FromSeconds(5)); }
    catch (AggregateException) { return task.IsCompleted; }
}

static string? ServiceControlFailure(Task<ServiceSocketControls.Report>? task) => task?.IsFaulted == true
    ? "socket_controls_" + task.Exception!.GetBaseException().GetType().Name
    : task?.IsCanceled == true ? "socket_controls_cancelled" : null;

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
        builder.Append("SERVICE_QUOTA_CAPTURE terminal=").Append(ServiceQuotaCapture.Terminal)
            .Append(" terminalBytes=").Append(Number(service.TerminalBytes)).Append(" quietMilliseconds=").Append(Number(service.QuietMilliseconds))
            .AppendLine(" controls=disabled acceptance=pending");
        builder.Append("SERVICE_QUOTA_PROCESS terminationRequested=").Append(Bit(service.TerminationRequested))
            .Append(" deathConfirmed=").Append(Bit(service.DeathConfirmed)).Append(" spontaneous=").Append(Bit(service.SpontaneousExit))
            .Append(" cleanupFailed=").Append(Bit(service.CleanupFailed)).Append(" exitCode=").AppendLine(Number(service.ExitCode));
    }
}

static void AppendContinuingWebSummary(StringBuilder builder, RunnerOptions options)
{
    if (!options.ContinuingWeb) return;
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
    if (options.WebVectorProbe is not null) AppendWebProbe("WEB_VECTOR_PROBE", options.WebVectorProbe.Value, web.VectorResult);
    AppendWebProbe("WEB_HEALTH_AFTER_PROBE", options.HttpProbe!.Value, web.HealthAfterResult);
    builder.Append("WEB_SOCKET_CONTROLS ").AppendLine(web.Controls is null?"none":JsonSerializer.Serialize(web.Controls));
    builder.Append("WEB_FRAME_CONTROL ").AppendLine(web.Frames is null?"none":JsonSerializer.Serialize(web.Frames));
    builder.Append("WEB_PROCESS_ALIVE_AFTER_CONTROLS ").AppendLine(FormatBool(web.AliveAfterControls));
    if(options.WebRacePipe is not null)
    {
        builder.Append("WEB_RACE_GATE ").AppendLine(web.RaceGate is null?"none":JsonSerializer.Serialize(web.RaceGate));
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
    if (options.WebVectorProbe is not null) builder.Append("WEB_PROCESS_ALIVE_AFTER_VECTORS ").AppendLine(FormatBool(web.AliveAfterVectors));
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
    catch (SocketException) { return false; }
}

static bool CheckWebEndpointRefused(int port)
{
    // Windows loopback reports a refused connection after about two seconds.
    // Process-tree exit can also precede a child's socket release. Require an
    // actual refusal within one bounded window, including successful retries.
    using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(3));
    while(!cancellation.IsCancellationRequested)
    {
        using TcpClient client = new();
        try
        {
            client.ConnectAsync(System.Net.IPAddress.Loopback, port, cancellation.Token).AsTask().GetAwaiter().GetResult();
            Task.Delay(25,cancellation.Token).GetAwaiter().GetResult();
        }
        catch (SocketException ex) { return ex.SocketErrorCode == SocketError.ConnectionRefused; }
        catch (OperationCanceledException) { return false; }
    }
    return false;
}

static HttpProbeResult ExecuteContinuingWebProbe(HttpProbeOptions probe, Stopwatch lifetime, int deadlineSeconds)
{
    long remaining = deadlineSeconds * 1000L - lifetime.ElapsedMilliseconds;
    if (remaining < 1000) return HttpProbeResult.Failed("deadline", 0, string.Empty);
    HttpProbeOptions bounded = new(probe.Url, probe.ExpectedStatusCode, probe.ExpectedBody,
        probe.ExpectedBodyConfigured, Math.Min(probe.TimeoutSeconds, (int)(remaining / 1000)), probe.IsPost, probe.VectorBody);
    // HTTP/1.0 closes the connection after JSON, so the existing bounded
    // reader receives entity bytes without HTTP/1.1 chunk framing.
    try { return ExecuteHttpProbe(bounded, http10: true); }
    catch (SocketException ex) { return HttpProbeResult.Failed("request_failed", 0, ex.GetType().Name); }
}

static HttpProbeResult ExecuteHttpProbe(HttpProbeOptions probe, bool http10 = false)
{
    Console.WriteLine(
        "HTTP_PROBE_START endpoint=" + FormatProbeEndpoint(probe.Url)
        + " expected_status=" + probe.ExpectedStatusCode
        + " expected_body_bytes=" + probe.ExpectedBodyByteLength
        + " timeout_seconds=" + probe.TimeoutSeconds);

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
        else if (probe.VectorBody ? !WebVectorResponse.Matches(body) || bodySha256 != WebVectorResponse.Sha256 : probe.ExpectedBodyConfigured
            && !Encoding.UTF8.GetBytes(probe.ExpectedBody).AsSpan().SequenceEqual(body))
        {
            passed = false;
            reason = "body_mismatch";
        }

        HttpProbeResult result = new HttpProbeResult(
            passed,
            reason,
            statusCode,
            body.Length,
            bodySha256,
            stopwatch.ElapsedMilliseconds,
            string.Empty);
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
    byte[] requestBytes = Encoding.ASCII.GetBytes(
        (post ? "POST " : "GET ") + path + (http10 ? " HTTP/1.0\r\n" : " HTTP/1.1\r\n")
        + "Host: " + host + "\r\n"
        + "Connection: close\r\n"
        + "Accept: */*\r\n"
        + (post ? "Content-Length: 0\r\n" : "")
        + "\r\n");
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
            if (colon <= 0) throw new MalformedHttpResponseException("HTTP header was malformed.");
            string name = line[..colon];
            if (name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                throw new MalformedHttpResponseException("HTTP/1.0 probe requires a close-delimited or exact-length entity.");
            if (!name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)) continue;
            if (lengthSeen || !int.TryParse(line[(colon + 1)..].Trim(), System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out int length) || length != body.Length)
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
    string line = prefix
        + " endpoint=" + FormatProbeEndpoint(probe.Url)
        + " status=" + result.StatusCodeText
        + " body_bytes=" + result.BodyByteCount
        + " body_sha256=" + result.BodySha256
        + " elapsed_ms=" + result.ElapsedMilliseconds;
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
    string host = uri.Host.Contains(':', StringComparison.Ordinal)
        ? "[" + uri.Host + "]"
        : uri.Host;
    return uri.Scheme + "://" + host + ":" + uri.Port;
}

static bool TryReadSerialLogBounded(
    string path,
    out string serial,
    out string? failureReason,
    long byteCapacity = SerialMarkerStateMachine.DefaultByteCapacity)
{
    serial = string.Empty;
    failureReason = null;
    if (!File.Exists(path))
    {
        return true;
    }

    try
    {
        using FileStream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
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

    if (Path.IsPathFullyQualified(executable)
        || executable.Contains(Path.DirectorySeparatorChar)
        || executable.Contains(Path.AltDirectorySeparatorChar))
    {
        checkedPaths.Add(executable);
        return File.Exists(executable)
            ? new DiscoveryResult(executable, checkedPaths)
            : new DiscoveryResult(null, checkedPaths);
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
        return File.Exists(requestedPath)
            ? new DiscoveryResult(requestedPath, candidates)
            : new DiscoveryResult(null, candidates);
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
    string checkedPaths = result.CheckedPaths.Count == 0
        ? "none"
        : string.Join(";", result.CheckedPaths);
    Console.WriteLine(prefix + " result=" + found + " checked=" + checkedPaths);
}

readonly struct DiscoveryResult
{
    public DiscoveryResult(string? path, IReadOnlyList<string> checkedPaths)
    {
        Path = path;
        CheckedPaths = checkedPaths;
    }

    public string? Path { get; }

    public IReadOnlyList<string> CheckedPaths { get; }
}

sealed class RunnerOptions
{
    private static readonly string[] DefaultRequiredMarkers = OS.Tooling.QemuContracts.RunnerDefaultRequiredMarkers.ToArray();
    private static readonly string[] DefaultForbiddenMarkers =
        { "OS_SMOKE_FAIL", "TEST_FAIL" };
    private const string DefaultWorkloadProfile = OS.Kernel.Build.WorkloadProfile.SingleMinimalApiName;
    private readonly List<string> requiredMarkers =
        new List<string>(DefaultRequiredMarkers);
    private readonly List<string> forbiddenMarkers = new List<string>(DefaultForbiddenMarkers);
    private readonly List<string> markerPresets = new List<string>();
    private readonly List<string> orderedMarkers = new List<string>();
    private readonly List<string> uniqueMarkers = new List<string>();
    private bool markerConfigurationStarted;
    private bool defaultRequiredMarkersReplaced;

    public string QemuPath { get; private set; } = "qemu-system-x86_64";

    public string ImagePath { get; private set; } = Path.Combine("artifacts", "images", "os.raw");

    public string DiskFormat { get; private set; } = "raw";

    public string OvmfCodePath { get; private set; } = string.Empty;

    public string SerialLogPath { get; private set; } = Path.Combine("artifacts", "logs", "qemu-smoke.log");

    public string SummaryLogPath { get; private set; } = string.Empty;

    public string Memory { get; private set; } = "256M";

    public string? FirmwareRngSource { get; private set; }

    public bool PciBarTestDevice { get; private set; }
    public bool VirtioNegotiationDevice { get; private set; }
    public bool ArpReceiveFixture { get; private set; }
    public bool ContinuingNetworkPeer { get; private set; }
    public bool SocketContainmentPeer { get; private set; }
    public int? SocketForwardPort { get; private set; }
    public bool ContinuingWeb { get; private set; }
    public bool FiniteStaticFiles { get; private set; }
    public bool FullDiskStaticFiles {get; private set;}
    public bool DiskReadTiming {get; private set;}
    public bool DiskStaticFiles => FiniteStaticFiles && ReadOnlyWebRootDisk;
    public bool ReadOnlyWebRootDisk { get; private set; }
    public StaticFileProcessEvidence? StaticEvidence { get; private set; }
    public string? WebRacePipe { get; private set; }
    public bool WebEventOwnedSignalRace { get; private set; }
    public bool ContinuingService { get; private set; }
    public bool ServiceQuotaFailure { get; private set; }
    public long SerialByteCapacity => FullDiskStaticFiles ? OS.Kernel.Compatibility.LinuxX64.DiskStaticExecutionPolicy.MaximumSerialBytes :
        ContinuingService ? SerialMarkerStateMachine.ContinuingServiceByteCapacity : SerialMarkerStateMachine.DefaultByteCapacity;
    public ServiceEvidence? ServiceEvidence { get; private set; }
    public string SuccessTerminalMarker => ServiceQuotaFailure ? ServiceQuotaCapture.Terminal : FiniteStaticFiles || ReadOnlyWebRootDisk ? "TEST_PASS m16-012" : ContinuingService ? "TEST_PASS m16-012" : ContinuingWeb ? "TEST_PASS m10.004" : SerialMarkerStateMachine.DefaultSuccessTerminalMarker;
    public HttpProbeOptions? WebConfigProbe { get; private set; }
    public HttpProbeOptions? WebLogProbe { get; private set; }
    public HttpProbeOptions? WebLogRepeatProbe { get; private set; }
    public HttpProbeOptions? WebVectorProbe { get; private set; }
    public ContinuingWebEvidence? WebEvidence { get; private set; }
    public string WebLogCase { get; private set; } = "normal";
    private bool webLogCaseConfigured;
    private string? webConfigBody;
    public string? EthernetCapturePath { get; private set; }
    public bool VirtioBlockQueueDevice { get; private set; }
    public string? VirtioBlockBootImagePath { get; private set; }
    public string? WritableDataImagePath { get; private set; }
    public WritableDataImageLease? WritableDataLease { get; set; }

    public string WorkloadProfile { get; private set; } = DefaultWorkloadProfile;

    public IReadOnlyList<string> RequiredMarkers => requiredMarkers;

    public IReadOnlyList<string> ForbiddenMarkers => forbiddenMarkers;

    public IReadOnlyList<string> MarkerPresets => markerPresets;

    public IReadOnlyList<string> OrderedMarkers => orderedMarkers;

    public IReadOnlyList<string> UniqueMarkers => uniqueMarkers;

    public int TimeoutSeconds { get; private set; } = 30;

    public int StartupTimeoutSeconds { get; private set; } = 30;
    public bool WatchStartup => !ExpectedUnexpectedVectorPanic && !ExpectedOwnedSubsystemPanic && !ExpectedUnhandledException &&
        (RequiredMarkers.Contains(QemuStartupDeadline.ReadyMarker) || OrderedMarkers.Contains(QemuStartupDeadline.ReadyMarker) || UniqueMarkers.Contains(QemuStartupDeadline.ReadyMarker));

    public HttpProbeOptions? HttpProbe { get; private set; }

    public bool ExpectedUnexpectedVectorPanic { get; private set; }

    public bool ExpectedOwnedSubsystemPanic { get; private set; }
    public bool ExpectedUnhandledException { get; private set; }

    public bool DryRun { get; private set; }

    public bool Preflight { get; private set; }

    public bool ShowHelp { get; private set; }

    private bool httpProbeConfigured;

    private string? httpProbeUrl;

    private int httpProbeExpectedStatusCode = 200;

    private string httpProbeExpectedBody = string.Empty;

    private bool httpProbeExpectedBodyConfigured;

    private int httpProbeTimeoutSeconds = 10;

    public static RunnerOptions Parse(string[] args)
    {
        RunnerOptions options = new RunnerOptions();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "--help":
                case "-h":
                    options.ShowHelp = true;
                    break;
                case "--expect-unexpected-vector-panic":
                    if (options.ExpectedUnexpectedVectorPanic) throw new ArgumentException("--expect-unexpected-vector-panic may be specified only once.");
                    options.ExpectedUnexpectedVectorPanic = true;
                    break;
                case "--expect-owned-subsystem-panic":
                    if (options.ExpectedOwnedSubsystemPanic) throw new ArgumentException("--expect-owned-subsystem-panic may be specified only once.");
                    options.ExpectedOwnedSubsystemPanic = true;
                    break;
                case "--expect-unhandled-exception":
                    if (options.ExpectedUnhandledException) throw new ArgumentException("--expect-unhandled-exception may be specified only once.");
                    options.ExpectedUnhandledException = true;
                    break;
                case "--dry-run":
                    options.DryRun = true;
                    break;
                case "preflight":
                case "--preflight":
                    options.Preflight = true;
                    break;
                case "--qemu":
                    options.QemuPath = RequireNonEmpty(ReadValue(args, ref i, arg), arg);
                    break;
                case "--image":
                    options.ImagePath = RequireNonEmpty(ReadValue(args, ref i, arg), arg);
                    break;
                case "--disk-format":
                    options.SetDiskFormat(ReadValue(args, ref i, arg));
                    break;
                case "--ovmf-code":
                    options.OvmfCodePath = RequireNonEmpty(ReadValue(args, ref i, arg), arg);
                    break;
                case "--serial-log":
                    options.SerialLogPath = RequireNonEmpty(ReadValue(args, ref i, arg), arg);
                    break;
                case "--summary-log":
                    options.SummaryLogPath = RequireNonEmpty(ReadValue(args, ref i, arg), arg);
                    break;
                case "--pci-bar-test-device":
                    if (options.PciBarTestDevice) throw new ArgumentException("--pci-bar-test-device may be specified only once.");
                    options.PciBarTestDevice = true;
                    break;
                case "--virtio-negotiation-device":
                    if (options.VirtioNegotiationDevice) throw new ArgumentException("--virtio-negotiation-device may be specified only once.");
                    options.VirtioNegotiationDevice = true;
                    break;
                case "--ethernet-capture":
                    if (options.EthernetCapturePath is not null) throw new ArgumentException("--ethernet-capture may be specified only once.");
                    options.EthernetCapturePath = Path.GetFullPath(RequireNonEmpty(ReadValue(args, ref i, arg), arg));
                    if (options.EthernetCapturePath.IndexOfAny([',', '\r', '\n']) >= 0 || File.Exists(options.EthernetCapturePath))
                        throw new ArgumentException("--ethernet-capture requires a fresh file path without QEMU option separators.");
                    break;
                case "--arp-receive-fixture":
                    if (options.ArpReceiveFixture) throw new ArgumentException("--arp-receive-fixture may be specified only once.");
                    options.ArpReceiveFixture = true;
                    break;
                case "--continuing-network-peer":
                    if (options.ContinuingNetworkPeer) throw new ArgumentException("--continuing-network-peer may be specified only once.");
                    options.ContinuingNetworkPeer = true;
                    break;
                case "--socket-containment-peer":
                    if (options.SocketContainmentPeer) throw new ArgumentException("--socket-containment-peer may be specified only once.");
                    options.SocketContainmentPeer = true;
                    break;
                case "--socket-forward-port":
                    if (options.SocketForwardPort is not null) throw new ArgumentException("--socket-forward-port may be specified only once.");
                    string socketPort = ReadValue(args, ref i, arg);
                    if (!int.TryParse(socketPort, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int port) ||
                        port is < 1 or > 65535)
                        throw new ArgumentException("--socket-forward-port must be a decimal port from 1 through 65535.");
                    options.SocketForwardPort = port;
                    break;
                case "--continuing-web":
                    if (options.ContinuingWeb) throw new ArgumentException("--continuing-web may be specified only once.");
                    options.ContinuingWeb = true;
                    break;
                case "--full-disk-static-files":
                    if(options.FullDiskStaticFiles)throw new ArgumentException("duplicate full disk static selection");
                    options.FullDiskStaticFiles=true;break;
                case "--disk-read-timing":
                    if(options.DiskReadTiming)throw new ArgumentException("--disk-read-timing may be specified only once.");
                    options.DiskReadTiming=true;break;
                case "--finite-static-files":
                    if (options.FiniteStaticFiles) throw new ArgumentException("--finite-static-files may be specified only once.");
                    options.FiniteStaticFiles = true;
                    break;
                case "--readonly-webroot-disk":
                    if (options.ReadOnlyWebRootDisk) throw new ArgumentException("--readonly-webroot-disk may be specified only once.");
                    options.ReadOnlyWebRootDisk = true;
                    break;
                case "--web-race-pipe":
                    if(options.WebRacePipe is not null)throw new ArgumentException("--web-race-pipe may be specified only once.");
                    options.WebRacePipe=ReadValue(args,ref i,arg);
                    break;
                case "--web-event-owned-signal-race":
                    if(options.WebEventOwnedSignalRace)throw new ArgumentException("--web-event-owned-signal-race may be specified only once.");
                    options.WebEventOwnedSignalRace=true;
                    break;
                case "--service-quota-failure":
                    if (options.ServiceQuotaFailure) throw new ArgumentException("--service-quota-failure may be specified only once.");
                    options.ServiceQuotaFailure = true;
                    break;
                case "--continuing-service":
                    if (options.ContinuingService) throw new ArgumentException("--continuing-service may be specified only once.");
                    options.ContinuingService = true;
                    break;
                case "--web-config-body":
                    if (options.webConfigBody is not null) throw new ArgumentException("--web-config-body may be specified only once.");
                    options.webConfigBody = ReadValue(args, ref i, arg);
                    break;
                case "--web-log-case":
                    if (options.webLogCaseConfigured) throw new ArgumentException("--web-log-case may be specified only once.");
                    options.webLogCaseConfigured = true;
                    options.WebLogCase = ReadValue(args, ref i, arg);
                    if (options.WebLogCase is not ("normal" or "records64"))
                        throw new ArgumentException("--web-log-case must be normal or records64.");
                    break;
                case "--virtio-block-boot-image":
                    if (options.VirtioBlockBootImagePath is not null) throw new ArgumentException("--virtio-block-boot-image may be specified only once.");
                    options.VirtioBlockBootImagePath = RequireNonEmpty(ReadValue(args, ref i, arg), arg);
                    break;
                case "--writable-data-image":
                    if (options.WritableDataImagePath is not null) throw new ArgumentException("--writable-data-image may be specified only once.");
                    options.WritableDataImagePath = RequireNonEmpty(ReadValue(args, ref i, arg), arg);
                    if (options.WritableDataImagePath.IndexOfAny([',', '\r', '\n', '\0']) >= 0)
                        throw new ArgumentException("--writable-data-image forbids QEMU option separators.");
                    break;
                case "--virtio-block-queue-device":
                    if (options.VirtioBlockQueueDevice) throw new ArgumentException("--virtio-block-queue-device may be specified only once.");
                    options.VirtioBlockQueueDevice = true;
                    break;
                case "--firmware-rng-source":
                    if (options.FirmwareRngSource is not null)
                        throw new ArgumentException("--firmware-rng-source may be specified only once.");
                    string rngSource = ReadValue(args, ref i, arg);
                    if (!string.Equals(rngSource, "virtio", StringComparison.Ordinal) &&
                        !string.Equals(rngSource, "missing", StringComparison.Ordinal))
                        throw new ArgumentException("--firmware-rng-source must be virtio or missing.");
                    options.FirmwareRngSource = rngSource;
                    break;
                case "--memory":
                    options.Memory = RequireNonEmpty(ReadValue(args, ref i, arg), arg);
                    break;
                case "--workload-profile":
                    options.WorkloadProfile = NormalizeWorkloadProfile(RequireNonEmpty(ReadValue(args, ref i, arg), arg));
                    break;
                case "--replace-default-required-markers":
                    options.ReplaceDefaultRequiredMarkers();
                    break;
                case "--require-marker":
                    options.AddRequiredMarker(RequireNonEmpty(ReadValue(args, ref i, arg), arg));
                    break;
                case "--require-ordered-marker":
                    options.AddOrderedMarker(RequireNonEmpty(ReadValue(args, ref i, arg), arg));
                    break;
                case "--require-unique-marker":
                    options.AddUniqueMarker(RequireNonEmpty(ReadValue(args, ref i, arg), arg));
                    break;
                case "--marker-preset":
                    options.AddMarkerPreset(RequireNonEmpty(ReadValue(args, ref i, arg), arg));
                    break;
                case "--forbid-marker":
                    options.AddForbiddenMarker(RequireNonEmpty(ReadValue(args, ref i, arg), arg));
                    break;
                case "--timeout":
                    options.TimeoutSeconds = ReadPositiveInt(args, ref i, arg);
                    break;
                case "--startup-timeout":
                    options.StartupTimeoutSeconds = ReadPositiveInt(args, ref i, arg);
                    break;
                case "--http-probe-url":
                    options.SetHttpProbeUrl(ReadValue(args, ref i, arg));
                    break;
                case "--http-probe-expected-status":
                    options.SetHttpProbeExpectedStatusCode(ReadHttpStatusCode(args, ref i, arg));
                    break;
                case "--http-probe-expected-body":
                    options.SetHttpProbeExpectedBody(ReadValue(args, ref i, arg));
                    break;
                case "--http-probe-timeout":
                    options.SetHttpProbeTimeoutSeconds(ReadPositiveInt(args, ref i, arg));
                    break;
                default:
                    throw new ArgumentException("Unknown argument: " + arg);
            }
        }

        if (options.VirtioBlockBootImagePath is not null && (!options.VirtioBlockQueueDevice || options.DiskFormat != "raw"))
            throw new ArgumentException("--virtio-block-boot-image requires the dedicated raw Virtio block fixture.");
        options.FinishParsing();
        if (options.WritableDataImagePath is not null && (options.DiskFormat != "raw" || !options.VirtioNegotiationDevice ||
            !options.ArpReceiveFixture || options.EthernetCapturePath is null || string.IsNullOrWhiteSpace(options.SummaryLogPath) ||
            options.WorkloadProfile != "SingleMinimalApi" || !options.defaultRequiredMarkersReplaced ||
            options.RequiredMarkers.Count == 0 || options.MarkerPresets.Count != 0 || options.TimeoutSeconds is < 120 or > 295 ||
            options.ReadOnlyWebRootDisk || options.VirtioBlockBootImagePath is not null || options.VirtioBlockQueueDevice ||
            options.PciBarTestDevice || options.ContinuingWeb || options.ContinuingService || options.ServiceQuotaFailure ||
            options.FiniteStaticFiles || options.FullDiskStaticFiles || options.DiskReadTiming || options.ContinuingNetworkPeer ||
            options.SocketContainmentPeer || options.SocketForwardPort is not null || options.HttpProbe is not null ||
            options.ExpectedUnexpectedVectorPanic || options.ExpectedOwnedSubsystemPanic || options.ExpectedUnhandledException ||
            options.ImagePath.IndexOfAny([',', '\r', '\n', '\0']) >= 0))
            throw new ArgumentException("--writable-data-image requires the finite captured raw mechanical fixture, SingleMinimalApi, summary, replaced explicit markers and a120..295-second deadline without another fixture.");
        if(options.DiskReadTiming && (!options.FullDiskStaticFiles || !options.DiskStaticFiles || options.TimeoutSeconds is <240 or >600))
            throw new ArgumentException("--disk-read-timing requires the full finite readonly disk parent and explicit240..600-second deadline");
        if(options.FullDiskStaticFiles && (!options.DiskStaticFiles || (!options.DiskReadTiming && options.TimeoutSeconds is <2200 or >3600)))
            throw new ArgumentException("--full-disk-static-files requires the finite readonly disk parent and explicit2200..3600-second deadline");
        if (options.ReadOnlyWebRootDisk && (!options.VirtioNegotiationDevice || !options.ArpReceiveFixture || options.DiskFormat != "raw" ||
            options.VirtioBlockQueueDevice || options.VirtioBlockBootImagePath is not null || options.PciBarTestDevice ||
            options.ContinuingNetworkPeer || options.SocketContainmentPeer || options.ContinuingWeb || options.ContinuingService ||
            options.HttpProbe is not null || (options.DiskReadTiming ? options.TimeoutSeconds is <240 or >600 : options.FullDiskStaticFiles ? options.TimeoutSeconds is <2200 or >3600 : options.FiniteStaticFiles ? options.TimeoutSeconds is < 600 or > 825 : options.TimeoutSeconds is < 120 or > 295) ||
            !options.defaultRequiredMarkersReplaced || string.IsNullOrWhiteSpace(options.SummaryLogPath)))
            throw new ArgumentException("--readonly-webroot-disk requires the finite captured raw disk/network child without another fixture.");
        if ((options.PciBarTestDevice ? 1 : 0) + (options.VirtioNegotiationDevice ? 1 : 0) + (options.VirtioBlockQueueDevice ? 1 : 0) > 1)
            throw new ArgumentException("Dedicated PCI test devices cannot share slot 6.");
        if (options.EthernetCapturePath is not null && !options.VirtioNegotiationDevice)
            throw new ArgumentException("--ethernet-capture requires the dedicated modern network fixture.");
        if (options.ArpReceiveFixture && (!options.VirtioNegotiationDevice || options.EthernetCapturePath is null))
            throw new ArgumentException("--arp-receive-fixture requires the captured dedicated modern network fixture.");
        if (options.ContinuingNetworkPeer && (!options.ArpReceiveFixture || options.TimeoutSeconds > 295 ||
            options.HttpProbe is not null || options.ExpectedUnexpectedVectorPanic || options.ExpectedOwnedSubsystemPanic || options.ExpectedUnhandledException))
            throw new ArgumentException("--continuing-network-peer requires the captured ARP fixture, a timeout at most 295 seconds and the ordinary diagnostic exit path.");
        if (options.SocketContainmentPeer && (!options.ArpReceiveFixture || options.TimeoutSeconds is < 120 or > 295 ||
            options.ContinuingNetworkPeer || options.ContinuingWeb || options.ContinuingService || options.HttpProbe is not null || options.ExpectedUnexpectedVectorPanic || options.ExpectedOwnedSubsystemPanic || options.ExpectedUnhandledException))
            throw new ArgumentException("--socket-containment-peer requires the captured ARP fixture, a120..295-second timeout and ordinary diagnostic exit without another peer.");
        if (options.SocketForwardPort is not null && (!options.VirtioNegotiationDevice || !options.ArpReceiveFixture || options.EthernetCapturePath is null ||
            options.ContinuingNetworkPeer || options.SocketContainmentPeer || (options.HttpProbe is not null && !options.ContinuingWeb) || options.ExpectedUnexpectedVectorPanic || options.ExpectedOwnedSubsystemPanic || options.ExpectedUnhandledException))
            throw new ArgumentException("--socket-forward-port requires the captured dedicated modern ARP network fixture and ordinary diagnostic exit path, without --continuing-network-peer or HTTP probing.");
        options.ValidateContinuingWeb();
        options.ValidateContinuingService();
        if (options.FiniteStaticFiles && (!options.VirtioNegotiationDevice || !options.ArpReceiveFixture || options.EthernetCapturePath is null ||
            options.SocketForwardPort is null || string.IsNullOrWhiteSpace(options.SummaryLogPath) || options.WorkloadProfile != "SingleMinimalApi" ||
            (!options.FullDiskStaticFiles && options.TimeoutSeconds is <600 or >900) || !options.defaultRequiredMarkersReplaced || options.MarkerPresets.Count != 0 ||
            options.ContinuingWeb || options.ContinuingService || options.ContinuingNetworkPeer || options.SocketContainmentPeer ||
            options.HttpProbe is not null || options.ServiceQuotaFailure))
            throw new ArgumentException("--finite-static-files requires captured forwarded modern networking, SingleMinimalApi, a600..900-second deadline and explicit finite markers without another application peer.");
        return options;
    }

    public static void PrintHelp()
    {
        Console.WriteLine("--writable-data-image <raw> attaches a distinct existing mutable image at modern slot7 beside the captured slot6 NIC; --image boots through an AHCI temporary overlay. Requires raw, SingleMinimalApi, explicit replaced markers, summary and120..295seconds. Working identity/length stays held through actual child join; bytes may change. Unconfirmed cleanup retains the runner, never guest acceptance.");
        Console.WriteLine("--socket-containment-peer selects the finite three-socket timeout/VLAN raw Ethernet peer; requires captured modern ARP networking and a120..295-second timeout. Peer completion does not establish guest acceptance.");
        Console.WriteLine("--finite-static-files captures the finite static-file profile through guest shutdown, transport drain and final parent cleanup; external HTTP checks belong to the owning tooling peer.");
        Console.WriteLine("--disk-read-timing requires --full-disk-static-files, --finite-static-files, --readonly-webroot-disk and a240..600-second deadline. Its owning tooling peer joins deliberate cancellation after diagnostic HTTP checks; this does not qualify full service or cleanup.");
        Console.WriteLine("--continuing-network-peer uses a bounded loopback Ethernet peer for the captured M07-026 fixture; dry-run prints its ephemeral-port placeholder.");
        Console.WriteLine("--socket-forward-port <1-65535> forwards loopback TCP to guest port 8080 on the captured dedicated modern ARP network fixture, using QEMU usernet without the scripted Ethernet peer.");
        Console.WriteLine("--continuing-web requires that forward, exact loopback /healthz HTTP 200/body probing, --web-config-body <exact-UTF8-text>, and a summary log; it probes /config, logging POST, repeat POST 409, the normal-case binary vector response and final health 200 before terminating its QEMU and checking the stopped endpoint refuses connections.");
        Console.WriteLine("--service-quota-failure is an explicit expected capture under --continuing-service; requires replaced default markers and exact final CLROS_M16_016_QUOTA_CLEANUP, disables positive raw socket controls, retains frame/process/endpoint cleanup, and returns22 with acceptance pending. No quota or guest behavior is accepted by capture alone.");
        Console.WriteLine("--continuing-service requires the captured modern ARP fixture, guest port 8080 forward, virtio RNG, SingleMinimalApi, summary log and a 780..1080-second timeout (quota:780..900); owned raw socket controls run alongside serial capture until TEST_PASS m16-012 and confirmed process/frame cleanup. Other web, scripted peer, HTTP or panic modes are forbidden.");
        Console.WriteLine("--web-log-case normal|records64 requires --continuing-web (default normal); normal expects logging POST 200 and GET /__clros/vectors with 49152 exact binary bytes, records64 expects logging 500 with an empty body and skips vectors. Repeat POST 409 and final health 200 are required in both cases.");
        Console.WriteLine("--web-race-pipe <clros-web-race-32lowerhex> requires normal continuing-web and an owned local producer. After ordinary controls it waits for joined race-report hashes within the original deadline; dry-run/preflight opens no pipe.");
        Console.WriteLine("--startup-timeout <seconds> bounds startup to M03_EBS_RETURNED_SUCCESS when required (default 30); the overall --timeout still applies. A startup timeout captures QMP registers and up to 512 MiB RAM beside the serial log before terminating the child.");
        Console.WriteLine("OS.QemuRunner [preflight|--preflight] --image <raw|qcow2 image> [--disk-format raw|qcow2] [--workload-profile SingleMinimalApi|MultiMinimalApi] [--ovmf-code <OVMF_CODE.fd>] [--serial-log <path>] [--summary-log <path>] [--timeout <seconds>] [--replace-default-required-markers] [--require-marker <marker>]... [--require-ordered-marker <marker>]... [--require-unique-marker <marker>]... [--marker-preset " + SmokeMarkerPreset.SupportedValues + "]... [--forbid-marker <marker>]... [--http-probe-url <http-url>] [--http-probe-expected-status <100-599>] [--http-probe-expected-body <text>] [--http-probe-timeout <seconds>] [--expect-unexpected-vector-panic|--expect-owned-subsystem-panic] [--firmware-rng-source virtio|missing] [--virtio-block-queue-device [--virtio-block-boot-image <raw>]] [--dry-run]");
        Console.WriteLine("preflight validates local runner inputs and image format hints without launching QEMU or requiring libvirt.");
    }

    private void ReplaceDefaultRequiredMarkers()
    {
        if (defaultRequiredMarkersReplaced)
        {
            throw new ArgumentException(
                "--replace-default-required-markers may be specified only once.");
        }

        if (markerConfigurationStarted)
        {
            throw new ArgumentException(
                "--replace-default-required-markers must precede marker arguments.");
        }

        requiredMarkers.Clear();
        defaultRequiredMarkersReplaced = true;
    }

    private void AddRequiredMarker(string marker)
    {
        markerConfigurationStarted = true;
        AddDistinct(requiredMarkers, marker);
    }

    private void AddOrderedMarker(string marker)
    {
        AddDistinct(orderedMarkers, marker);
        AddRequiredMarker(marker);
    }

    private void AddUniqueMarker(string marker)
    {
        AddDistinct(uniqueMarkers, marker);
        AddRequiredMarker(marker);
    }

    private void AddMarkerPreset(string preset)
    {
        string normalized = SmokeMarkerPreset.Normalize(preset);
        AddDistinct(markerPresets, normalized);

        foreach (string marker in SmokeMarkerPreset.GetRequiredMarkers(normalized))
        {
            AddRequiredMarker(marker);
        }

        foreach (string marker in SmokeMarkerPreset.GetOrderedMarkers(normalized))
        {
            AddOrderedMarker(marker);
        }
    }

    private void AddForbiddenMarker(string marker)
    {
        markerConfigurationStarted = true;
        AddDistinct(forbiddenMarkers, marker);
    }

    private void SetDiskFormat(string value)
    {
        if (string.Equals(value, "raw", StringComparison.OrdinalIgnoreCase))
        {
            DiskFormat = "raw";
            return;
        }

        if (string.Equals(value, "qcow2", StringComparison.OrdinalIgnoreCase))
        {
            DiskFormat = "qcow2";
            return;
        }

        throw new ArgumentException("Value for --disk-format must be raw or qcow2.");
    }

    private void SetHttpProbeUrl(string value)
    {
        httpProbeConfigured = true;

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value for --http-probe-url cannot be empty.");
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) || string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new ArgumentException("Value for --http-probe-url must be an absolute HTTP URL with a host.");
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("HTTP probe URL must use http scheme; https/TLS is not supported.");
        }

        httpProbeUrl = value;
    }

    private void SetHttpProbeExpectedStatusCode(int statusCode)
    {
        httpProbeConfigured = true;
        httpProbeExpectedStatusCode = statusCode;
    }

    private void SetHttpProbeExpectedBody(string body)
    {
        httpProbeConfigured = true;
        httpProbeExpectedBody = body;
        httpProbeExpectedBodyConfigured = true;
    }

    private void SetHttpProbeTimeoutSeconds(int timeoutSeconds)
    {
        httpProbeConfigured = true;
        httpProbeTimeoutSeconds = timeoutSeconds;
    }

    private void FinishParsing()
    {
        if (ServiceQuotaFailure)
        {
            if (!ContinuingService || !defaultRequiredMarkersReplaced || MarkerPresets.Count != 0)
                throw new ArgumentException("--service-quota-failure requires --continuing-service and --replace-default-required-markers without marker presets.");
            foreach (string marker in ServiceQuotaCapture.ForbiddenSuccessMarkers)
            {
                if (RequiredMarkers.Concat(OrderedMarkers).Concat(UniqueMarkers).Any(required =>
                    required == marker && marker != ServiceQuotaCapture.EarlyBootSmokePass || required.StartsWith(marker + " ", StringComparison.Ordinal)))
                    throw new ArgumentException("Quota capture forbids ordinary success requirements.");
                // The inherited boot pass is phase-checked by ServiceQuotaCapture.
                if (marker != ServiceQuotaCapture.EarlyBootSmokePass) AddForbiddenMarker(marker);
            }
            AddRequiredMarker(ServiceQuotaCapture.Terminal); AddUniqueMarker(ServiceQuotaCapture.Terminal);
        }
        else if (ContinuingService) AddRequiredMarker("TEST_PASS m16-012");
        if (FiniteStaticFiles)
        {
            StaticEvidence = new();
            AddRequiredMarker(DiskStaticFiles ? "CLROS_M10_018_READY" : "CLROS_M10_017_READY"); AddUniqueMarker(DiskStaticFiles ? "CLROS_M10_018_READY" : "CLROS_M10_017_READY");
            AddRequiredMarker(DiskStaticFiles ? "TEST_PASS m10.018" : "TEST_PASS m10.017"); AddUniqueMarker(DiskStaticFiles ? "TEST_PASS m10.018" : "TEST_PASS m10.017");
            AddRequiredMarker("TEST_PASS m16-012");
        }
        if (ContinuingWeb)
        {
            AddRequiredMarker("CLROS_M10_004_READY");
            AddRequiredMarker("TEST_PASS m10.004");
        }
        if (DryRun && Preflight)
        {
            throw new ArgumentException("--dry-run and preflight are mutually exclusive.");
        }

        if (new[]{ExpectedUnexpectedVectorPanic,ExpectedOwnedSubsystemPanic,ExpectedUnhandledException}.Count(x=>x)>1)
            throw new ArgumentException("Expected-panic profiles are mutually exclusive.");
        if ((ExpectedUnexpectedVectorPanic || ExpectedOwnedSubsystemPanic || ExpectedUnhandledException) && (httpProbeConfigured || string.IsNullOrWhiteSpace(SummaryLogPath) || MarkerPresets.Count != 0))
            throw new ArgumentException("Expected unexpected-vector panic requires a summary log and forbids HTTP probing or marker presets.");

        ValidateArtifactPathSeparation();

        // Constructing the parser validates marker counts, lengths, ASCII,
        // duplicates, and required/forbidden overlap before any process can
        // be launched.
        _ = new SerialMarkerStateMachine(
            RequiredMarkers,
            ForbiddenMarkers,
            successTerminalMarker: SuccessTerminalMarker,
            byteCapacity: SerialByteCapacity,
            orderedMarkers: OrderedMarkers,
            uniqueMarkers: UniqueMarkers);

        if (!httpProbeConfigured)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(httpProbeUrl))
        {
            throw new ArgumentException("HTTP probe requires --http-probe-url.");
        }

        HttpProbe = new HttpProbeOptions(
            httpProbeUrl,
            httpProbeExpectedStatusCode,
            httpProbeExpectedBody,
            httpProbeExpectedBodyConfigured,
            httpProbeTimeoutSeconds);
    }

    private void ValidateContinuingService()
    {
        if (!ContinuingService) return;
        if (!VirtioNegotiationDevice || !ArpReceiveFixture || EthernetCapturePath is null || SocketForwardPort is null ||
            string.IsNullOrWhiteSpace(SummaryLogPath) || WorkloadProfile != OS.Kernel.Build.WorkloadProfile.SingleMinimalApiName ||
            FirmwareRngSource != "virtio" || TimeoutSeconds < 780 || TimeoutSeconds > (ServiceQuotaFailure ? 900 : 1080) || ContinuingWeb || ContinuingNetworkPeer || SocketContainmentPeer ||
            httpProbeConfigured || ExpectedUnexpectedVectorPanic || ExpectedOwnedSubsystemPanic || ExpectedUnhandledException)
            throw new ArgumentException("--continuing-service requires captured modern ARP networking, guest port 8080 forward, summary log, SingleMinimalApi, virtio RNG and a 780..1080-second timeout (quota:780..900) without web, peer, HTTP or panic modes.");
        ServiceEvidence = new() { QuotaFailure = ServiceQuotaFailure };
    }

    private void ValidateContinuingWeb()
    {
        if (!ContinuingWeb)
        {
            if(WebEventOwnedSignalRace)throw new ArgumentException("--web-event-owned-signal-race requires --continuing-web.");
            if(WebRacePipe is not null)throw new ArgumentException("--web-race-pipe requires --continuing-web.");
            if (webLogCaseConfigured) throw new ArgumentException("--web-log-case requires --continuing-web.");
            if (webConfigBody is not null) throw new ArgumentException("--web-config-body requires --continuing-web.");
            return;
        }
        if(WebEventOwnedSignalRace&&WebRacePipe is null)throw new ArgumentException("--web-event-owned-signal-race requires --web-race-pipe.");
        if(WebRacePipe is not null)
        {
            if(WebEventOwnedSignalRace)WebRaceRendezvous.ValidateEventOwnedPipeName(WebRacePipe);
            else WebRaceRendezvous.ValidatePipeName(WebRacePipe);
        }
        if(WebRacePipe is not null&&WebLogCase!="normal")throw new ArgumentException("--web-race-pipe requires normal web logging.");
        if (SocketForwardPort is null || HttpProbe is null || string.IsNullOrWhiteSpace(SummaryLogPath) ||
            WorkloadProfile != OS.Kernel.Build.WorkloadProfile.SingleMinimalApiName || TimeoutSeconds > 285 ||
            ContinuingNetworkPeer || ExpectedUnexpectedVectorPanic || ExpectedOwnedSubsystemPanic || ExpectedUnhandledException)
            throw new ArgumentException("--continuing-web requires a captured guest 8080 forward, HTTP probe, summary log, SingleMinimalApi and timeout at most 285 seconds without a scripted peer or panic profile.");
        HttpProbeOptions probe = HttpProbe.Value;
        Uri uri = new(probe.Url, UriKind.Absolute);
        if (uri.Host != "127.0.0.1" || uri.Port != SocketForwardPort || uri.PathAndQuery != "/healthz" ||
            uri.Fragment.Length != 0 || uri.UserInfo.Length != 0 || probe.ExpectedStatusCode != 200 ||
            !probe.ExpectedBodyConfigured || probe.ExpectedBodyByteLength is < 1 or > 65536 ||
            probe.TimeoutSeconds > 30 || webConfigBody is null || Encoding.UTF8.GetByteCount(webConfigBody) is < 1 or > 65536)
            throw new ArgumentException("--continuing-web requires exact http://127.0.0.1:<forward>/healthz, status 200, bounded nonempty health/config bodies and an HTTP timeout at most 30 seconds.");
        // The inherited boot prefix can require OS_SMOKE_PASS. Web startup
        // still requires both M10 markers and uses its own success terminal.
        WebConfigProbe = new HttpProbeOptions("http://127.0.0.1:" + SocketForwardPort.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/config",
            200, webConfigBody, true, probe.TimeoutSeconds);
        string loggingUrl = "http://127.0.0.1:" + SocketForwardPort.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/__clros/logexercise";
        bool quota = WebLogCase == "records64";
        WebLogProbe = new(loggingUrl, quota ? 500 : 200,
            // Eight traced writers need a larger finite request budget than health.
            // ExecuteContinuingWebProbe still clamps it to the owned guest deadline.
            quota ? "" : "CLROS_LOG_OK workers=8 records=64 writev=32 invalid_errno=9", true, 60, post: true);
        WebLogRepeatProbe = new(loggingUrl, 409, "", true, probe.TimeoutSeconds, post: true);
        if (!quota) WebVectorProbe = new("http://127.0.0.1:" + SocketForwardPort.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/__clros/vectors",
            200, "", true, probe.TimeoutSeconds, vectorBody: true);
        WebEvidence = new ContinuingWebEvidence();
    }

    private void ValidateArtifactPathSeparation()
    {
        List<(string Name, string Path)> paths =
        [
            ("image", ImagePath),
            ("serial-log", SerialLogPath),
            ("qemu-requested", QemuPath)
        ];

        if (VirtioBlockBootImagePath is not null) paths.Add(("virtio-block-boot-image", VirtioBlockBootImagePath));
        if (WritableDataImagePath is not null)
        {
            paths.Add(("writable-data-image", WritableDataImagePath));
            if (EthernetCapturePath is not null) paths.Add(("ethernet-capture", EthernetCapturePath));
        }

        if (!string.IsNullOrWhiteSpace(OvmfCodePath))
        {
            paths.Add(("ovmf-code", OvmfCodePath));
        }

        if (!string.IsNullOrWhiteSpace(SummaryLogPath))
        {
            paths.Add(("summary-log", SummaryLogPath));
        }

        StringComparer comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        Dictionary<string, string> nameByPath =
            new Dictionary<string, string>(comparer);
        foreach ((string name, string path) in paths)
        {
            string fullPath = Path.GetFullPath(path);
            if (nameByPath.TryGetValue(fullPath, out string? existing))
            {
                throw new ArgumentException(
                    "Artifact paths must be distinct: "
                    + existing
                    + " and "
                    + name
                    + " resolve to "
                    + fullPath
                    + ".");
            }

            nameByPath.Add(fullPath, name);
        }
    }

    private static string NormalizeWorkloadProfile(string value)
    {
        if (string.Equals(value, OS.Kernel.Build.WorkloadProfile.SingleMinimalApiName, StringComparison.Ordinal))
        {
            return OS.Kernel.Build.WorkloadProfile.SingleMinimalApiName;
        }

        if (string.Equals(value, OS.Kernel.Build.WorkloadProfile.MultiMinimalApiName, StringComparison.Ordinal))
        {
            return OS.Kernel.Build.WorkloadProfile.MultiMinimalApiName;
        }

        throw new ArgumentException("Invalid workload profile '" + value + "'. Valid values: SingleMinimalApi, MultiMinimalApi.");
    }

    private static string ReadValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException("Missing value for " + option);
        }

        index++;
        return args[index];
    }

    private static string RequireNonEmpty(string value, string option)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value for " + option + " cannot be empty.");
        }

        return value;
    }

    private static void AddDistinct(List<string> values, string value)
    {
        if (!values.Contains(value))
        {
            values.Add(value);
        }
    }

    private static int ReadPositiveInt(string[] args, ref int index, string option)
    {
        string value = ReadValue(args, ref index, option);
        if (!int.TryParse(value, out int parsed) || parsed <= 0)
        {
            throw new ArgumentException("Value for " + option + " must be a positive integer.");
        }

        return parsed;
    }

    private static int ReadHttpStatusCode(string[] args, ref int index, string option)
    {
        string value = ReadValue(args, ref index, option);
        if (!int.TryParse(value, out int parsed) || parsed < 100 || parsed > 599)
        {
            throw new ArgumentException("Value for " + option + " must be an HTTP status code from 100 to 599.");
        }

        return parsed;
    }
}

sealed class ServiceEvidence
{
    public bool QuotaFailure;
    public long? TerminalBytes, QuietMilliseconds;
    public bool TerminationRequested, DeathConfirmed, SpontaneousExit, CleanupFailed;
    public int? ExitCode;
    public ServiceSocketControls.Report? Controls;
    public WebFrameControl.ServiceReport? Frames;
    public bool PreLaunchAvailable;
    public bool? StoppedEndpointRefused;
    public int? ProcessId;
    public string? StartUtc, EndUtc, Failure;
    public long? LifetimeMilliseconds;
}

sealed class ContinuingWebEvidence
{
    public WebRaceRendezvous.Report? RaceGate;
    public bool AliveAfterRaceGate;
    public WebSocketControls.Report? Controls;
    public WebFrameControl.Report? Frames;
    public bool AliveAfterControls;
    public readonly List<(DateTime Started, DateTime Completed)> ProbeWindows = [];
    public HttpProbeResult? ConfigResult, LogResult, LogRepeatResult, VectorResult, HealthAfterResult;
    public bool PreLaunchAvailable, AliveBeforeHealth, AliveAfterHealth, AliveAfterConfig;
    public bool AliveAfterLog, AliveAfterLogRepeat, AliveAfterVectors, AliveAfterFinalHealth;
    public bool TerminationRequested, DeathConfirmed, SpontaneousExit;
    public bool? StoppedEndpointRefused;
    public int? ProcessId, ExitCode;
    public string? StartUtc, EndUtc, Failure;
    public long? LifetimeMilliseconds;
}

readonly struct HttpProbeOptions
{
    public HttpProbeOptions(string url, int expectedStatusCode, string expectedBody, bool expectedBodyConfigured, int timeoutSeconds, bool post = false, bool vectorBody = false)
    {
        Url = url;
        ExpectedStatusCode = expectedStatusCode;
        ExpectedBody = expectedBody;
        ExpectedBodyConfigured = expectedBodyConfigured;
        TimeoutSeconds = timeoutSeconds;
        IsPost = post;
        VectorBody = vectorBody;
    }

    public string Url { get; }
    public bool IsPost { get; }
    public bool VectorBody { get; }

    public int ExpectedStatusCode { get; }

    public string ExpectedBody { get; }

    public bool ExpectedBodyConfigured { get; }

    public int ExpectedBodyByteLength
    {
        get
        {
            return VectorBody ? WebVectorResponse.Length : Encoding.UTF8.GetByteCount(ExpectedBody);
        }
    }

    public string ExpectedBodySha256 => VectorBody ? WebVectorResponse.Sha256 :
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ExpectedBody))).ToLowerInvariant();

    public int TimeoutSeconds { get; }
}

readonly struct HttpProbeResult
{
    public HttpProbeResult(
        bool passed,
        string reason,
        int? statusCode,
        int bodyByteCount,
        string bodySha256,
        long elapsedMilliseconds,
        string errorType)
    {
        Passed = passed;
        Reason = reason;
        StatusCode = statusCode;
        BodyByteCount = bodyByteCount;
        BodySha256 = bodySha256;
        ElapsedMilliseconds = elapsedMilliseconds;
        ErrorType = errorType;
    }

    public bool Passed { get; }

    public string Reason { get; }

    public int? StatusCode { get; }

    public string StatusCodeText => StatusCode.HasValue ? StatusCode.Value.ToString() : "none";

    public int BodyByteCount { get; }

    public string BodySha256 { get; }

    public long ElapsedMilliseconds { get; }

    public string ErrorType { get; }

    public static HttpProbeResult Failed(string reason, long elapsedMilliseconds, string errorType)
    {
        return new HttpProbeResult(
            false,
            reason,
            null,
            0,
            "none",
            elapsedMilliseconds,
            errorType);
    }
}

readonly struct RawHttpProbeResponse
{
    public RawHttpProbeResponse(int statusCode, byte[] body)
    {
        StatusCode = statusCode;
        Body = body;
    }

    public int StatusCode { get; }

    public byte[] Body { get; }
}

sealed class MalformedHttpResponseException : Exception
{
    public MalformedHttpResponseException(string message)
        : base(message)
    {
    }
}


// This finite parser belongs only to the explicit expected-panic experiment.
// Generic serial marker completion/failure behavior remains unchanged.
public static class UnhandledExceptionCapture
{
    public const string Failure = "TEST_FAIL m04.021.unhandled unhandled-exception";
    public const string Terminal = "OS_SMOKE_FAIL subsystem=runtime reason=unhandled-exception";
    public const int RecordCount = 73;
    public const string PrerequisiteTerminal = "M04_UNHANDLED 7";

    // Runner framing and exact terminal; OS.Tooling independently admits every
    // prerequisite numeric association before treating exit 19 as qualification.
    public static int Evaluate(string text, out string prerequisites)
    {
        prerequisites="";
        if(text.Length>SerialMarkerStateMachine.DefaultByteCapacity) return -1;
        const string first="TEST_START efi.main";
        int begin;
        if(text.StartsWith(first,StringComparison.Ordinal)) begin=0;
        else
        {
            if(first.StartsWith(text,StringComparison.Ordinal) || text.Length<OS.Tooling.QemuContracts.FirmwarePrefixBytes) return 0;
            begin=OS.Tooling.QemuContracts.FirmwarePrefixBytes;
            byte[] prefix=Encoding.ASCII.GetBytes(text[..begin]);
            if(Convert.ToHexString(SHA256.HashData(prefix))!=OS.Tooling.QemuContracts.FirmwarePrefixSha256) return -1;
        }
        string body=text[begin..];
        for(int i=0;i<body.Length;i++)
        {
            char c=body[i];
            if(c=='\r') { if(i+1<body.Length && body[i+1]!='\n') return -1; }
            else if(c=='\n') { if(i==0 || body[i-1]!='\r') return -1; }
            else if(c<32 || c>126) return -1;
        }
        string[] lines=body.Split("\r\n",StringSplitOptions.None);
        int complete=lines.Length-1;
        if(complete>RecordCount || complete==RecordCount && lines[^1].Length!=0) return -1;
        if(complete>0 && lines[0]!=first) return -1;
        if(complete< RecordCount) return 0;
        if(lines[62]!="TEST_START m04.021.unhandled" || lines[71]!=Failure || lines[72]!=Terminal) return -1;
        for(int i=0;i<8;i++) if(!System.Text.RegularExpressions.Regex.IsMatch(lines[63+i],"\\AM04_UNHANDLED "+i+"(?: 0x[0-9A-F]{16}){4}\\z")) return -1;
        for(int i=0;i<71;i++) if(lines[i].StartsWith("TEST_FAIL",StringComparison.Ordinal) || lines[i].StartsWith("OS_SMOKE_FAIL",StringComparison.Ordinal) ||
            lines[i]=="OS_SMOKE_PASS" || lines[i]=="TEST_START boot.sequence") return -1;
        prerequisites=text[..^(Failure.Length+Terminal.Length+4)];
        return 1;
    }
}

public static class UnexpectedVectorPanicCapture
{
    private const string PreambleBase64 = "G1syShtbMDE7MDFIG1s9M2gbWzJKG1swMTswMUgbWzJKG1swMTswMUgbWz0zaBtbMkobWzAxOzAxSBtbMkobWzAxOzAxSBtbPTNoG1syShtbMDE7MDFIQmRzRHhlOiBsb2FkaW5nIEJvb3QwMDAxICJVRUZJIFFFTVUgSEFSRERJU0sgUU0wMDAxMyAiIGZyb20gUGNpUm9vdCgweDApL1BjaSgweDIsMHgwKS9TYXRhKDB4MCwweEZGRkYsMHgwKQ0KQmRzRHhlOiBzdGFydGluZyBCb290MDAwMSAiVUVGSSBRRU1VIEhBUkRESVNLIFFNMDAwMTMgIiBmcm9tIFBjaVJvb3QoMHgwKS9QY2koMHgyLDB4MCkvU2F0YSgweDAsMHhGRkZGLDB4MCkNCg==";
    private static readonly string Preamble = Encoding.ASCII.GetString(Convert.FromBase64String(PreambleBase64));
    private static readonly string[] Prefix = OS.Tooling.QemuContracts.M02TerminalPrefix.ToArray();
    private static readonly string[] MetadataNames =
    ["c","d","context","callback","captureRsp","firmwareCs","cs","ss","ds","es","tr","gdtrBase","gdtrLimit","idtrBase","idtrLimit","tssAccess","gateLow","gateHigh","normalizer0","normalizer1","trigger","cr3","entryCr3","lar"];
    private static readonly string[] FaultNames =
    ["rip","rsp","rbp","rflags","cr2","cr3","rax","rbx","rcx","rdx","rsi","rdi","r8","r9","r10","r11","r12","r13","r14","r15"];
    private const string FaultHeading = "OS_SMOKE_FAIL subsystem=idt reason=unexpected-vector vector=32 errorCodePresent=false errorCode=0x0000000000000000";

    // -1 rejected, 0 a possible unfinished capture, 1 exact complete 76-record capture.
    public static int Evaluate(string text, out string prerequisiteText)
    {
        prerequisiteText = string.Empty;
        if (text.Length > SerialMarkerStateMachine.DefaultByteCapacity) return -1;
        int begin;
        if (text.StartsWith(Prefix[0], StringComparison.Ordinal)) begin = 0;
        else if (text.StartsWith(Preamble, StringComparison.Ordinal)) begin = Preamble.Length;
        else if (Prefix[0].StartsWith(text, StringComparison.Ordinal) || Preamble.StartsWith(text, StringComparison.Ordinal)) return 0;
        else return -1;
        string body = text[begin..];
        for (int i = 0; i < body.Length; i++)
        {
            char c = body[i];
            if (c == '\r') { if (i + 1 < body.Length && body[i + 1] != '\n') return -1; }
            else if (c == '\n') { if (i == 0 || body[i - 1] != '\r') return -1; }
            else if (c < 32 || c > 126) return -1;
        }
        string[] lines = body.Split("\r\n", StringSplitOptions.None);
        int completed = lines.Length - 1;
        int expectedCount = OS.Tooling.QemuContracts.M02UnexpectedCaptureRecordCount;
        if (completed > expectedCount || (completed == expectedCount && lines[expectedCount].Length != 0)) return -1;
        for (int i = 0; i < Math.Min(completed, Prefix.Length); i++)
        {
            if (i == 3) { if (!DecimalRecord(lines[i], "UEFI_MEMORY_MAP_DESCRIPTOR_COUNT ", out _)) return -1; }
            else if (i == 9) { if (!DecimalRecord(lines[i], "BOOT_MEMORY_REGION_COUNT ", out _)) return -1; }
            else if (i != 10 && !string.Equals(lines[i], Prefix[i], StringComparison.Ordinal)) return -1;
        }
        if (completed > 10 && !Map(lines)) return -1;
        if (completed < Prefix.Length + 1) return 0;
        if (lines[Prefix.Length].Length != 648 || !HexFields(lines[Prefix.Length], "M02_UNEXPECTED_STATE", MetadataNames, out ulong[] m) || !Metadata(m)) return -1;
        if (completed < expectedCount) return 0;
        if (!HexFields(lines[Prefix.Length + 1], FaultHeading, FaultNames, out ulong[] f)) return -1;
        // RAX, RCX, RDX and R8 follow the closed entry bytes. Remaining GPRs and CR2
        // are observations, not fabricated constants or metadata authority.
        if (f[0] != m[20] + 2 || f[1] != m[1] + 28672 || (f[3] & 0x24702) != 2 || (f[3] & 0x8d5) != 0x44 ||
            f[5] != m[21] || f[6] != 24 || f[8] != m[23] || f[9] != m[2] || f[12] != m[21]) return -1;
        prerequisiteText = string.Join("\r\n", lines.AsSpan(0, Prefix.Length + 1).ToArray()) + "\r\n";
        return 1;
    }

    private static bool DecimalRecord(string text, string heading, out ulong value)
    {
        value = 0;
        if (!text.StartsWith(heading, StringComparison.Ordinal)) return false;
        ReadOnlySpan<char> number = text.AsSpan(heading.Length);
        if (number.Length == 0 || (number.Length > 1 && number[0] == '0')) return false;
        foreach (char c in number) if (c < '0' || c > '9') return false;
        return ulong.TryParse(number, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private static bool Map(string[] lines)
    {
        if (!DecimalRecord(lines[3], "UEFI_MEMORY_MAP_DESCRIPTOR_COUNT ", out ulong descriptors) ||
            !DecimalRecord(lines[9], "BOOT_MEMORY_REGION_COUNT ", out ulong regions)) return false;
        string[] keys = ["regions", "conventionalPages", "conventionalBytes", "freeNodes", "freePages", "freeBytes", "pageSize", "statusCode"];
        string[] fields = lines[10].Split(' ');
        if (fields.Length != 10 || fields[0] != "BOOT_PAGE_ALLOCATOR_READY" || fields[1] != "status=Ready") return false;
        ulong[] v = new ulong[8];
        for (int i = 0; i < 8; i++) if (!DecimalRecord(fields[i + 2], keys[i] + "=", out v[i])) return false;
        return descriptors == regions && regions >= 1 && regions <= 4096 && v[0] == regions &&
            v[3] >= 1 && v[3] <= regions && v[3] <= v[4] && v[4] <= ulong.MaxValue / 4096 &&
            v[1] == v[4] && v[2] == v[4] * 4096 && v[2] == v[5] && v[6] == 4096 && v[7] == 0;
    }

    private static bool HexFields(string text, string heading, string[] names, out ulong[] values)
    {
        values = new ulong[names.Length];
        if (!text.StartsWith(heading + " ", StringComparison.Ordinal)) return false;
        string[] fields = text[(heading.Length + 1)..].Split(' ');
        if (fields.Length != names.Length) return false;
        for (int i = 0; i < names.Length; i++)
        {
            string key = names[i] + "=0x";
            if (!fields[i].StartsWith(key, StringComparison.Ordinal) || fields[i].Length != key.Length + 16) return false;
            ReadOnlySpan<char> hex = fields[i].AsSpan(key.Length);
            foreach (char c in hex) if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F'))) return false;
            if (!ulong.TryParse(hex, System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out values[i])) return false;
        }
        return true;
    }

    private static bool Metadata(ulong[] m)
    {
        ulong c = m[0], d = m[1];
        const ulong limit = 0x0000800000000000;
        if (c == 0 || d == 0 || (c & 4095) != 0 || (d & 4095) != 0 || c >= limit || d >= limit ||
            8192 > limit - c || 28672 > limit - d || (c < d + 28672 && d < c + 8192) ||
            m[3] == 0 || m[3] >= limit || (m[3] >= c && m[3] < c + 8192) || (m[3] >= d && m[3] < d + 28672)) return false;
        ulong target = c + 512;
        ulong gateLow = (target & 65535) | (8UL << 16) | (0x8eUL << 40) | ((target & 0xffff0000) << 32);
        return m[2] == d + 4096 && m[4] == d + 28488 && m[5] != 0 && m[5] <= 65535 && (m[5] & 7) == 0 &&
            m[6] == 8 && m[7] == 16 && m[8] == 16 && m[9] == 16 && m[10] == 24 &&
            m[11] == d + 4120 && m[12] == 39 && m[13] == d && m[14] == 4095 && m[15] == 0x8b &&
            m[16] == gateLow && m[17] == target >> 32 && m[18] == 0x00002068006a006a && m[19] == 0x909000000df2e900 &&
            m[20] == c + 5056 && m[21] != 0 && (m[21] & ~0x000fffffffffffffUL) == 0 &&
            (m[21] & 0xfe7) == 0 && (m[21] & ~4095UL) != 0 && m[22] == m[21] &&
            m[23] <= uint.MaxValue && (m[23] & 0x00f0fe00) == 0x00a09a00;
    }
}

// The explicit M03 terminal control uses the common measured-quiet/cleanup path.
// The script separately validates the complete first-exit/ACPI numeric grammar.
public static class OwnedSubsystemPanicCapture
{
    private static readonly string[] Prefix = OS.Tooling.QemuContracts.M03FirstExitRequiredMarkers.ToArray();
    private static readonly string[] FaultNames =
        ["rip","rsp","rbp","rflags","cr2","cr3","rax","rbx","rcx","rdx","rsi","rdi","r8","r9","r10","r11","r12","r13","r14","r15"];
    private const string FaultHeading = "OS_SMOKE_FAIL subsystem=boot reason=exit-fault vector=14 errorCodePresent=true errorCode=0x0000000000000000";
    private const ulong LowEnd = 0x0000800000000000;

    public static int InvalidBytes(string text)
    {
        int count = 0;
        foreach (byte b in Encoding.UTF8.GetBytes(text))
            if (b != 9 && b != 10 && b != 13 && (b < 32 || b > 126)) count++;
        return count;
    }

    // -1 rejected, 0 unfinished, 1 exact terminal capture; never a normal boot pass.
    public static int Evaluate(string text, out string prerequisiteText)
    {
        prerequisiteText = string.Empty;
        if (text.Length > 32768) return -1;
        if (text.Length < 304) return 0;
        byte[] preamble = Encoding.UTF8.GetBytes(text[..304]);
        if (preamble.Length != 304 || Convert.ToHexString(SHA256.HashData(preamble)) != OS.Tooling.QemuContracts.FirmwarePrefixSha256) return -1;
        string body = text[304..];
        for (int i = 0; i < body.Length; i++)
        {
            char c = body[i];
            if (c == '\r') { if (i + 1 < body.Length && body[i + 1] != '\n') return -1; }
            else if (c == '\n') { if (i == 0 || body[i - 1] != '\r') return -1; }
            else if (c < 32 || c > 126) return -1;
        }
        string[] lines = body.Split("\r\n", StringSplitOptions.None);
        int complete = lines.Length - 1;
        if (complete > 77 || (complete == 77 && lines[77].Length != 0)) return -1;
        for (int i = 0; i < Math.Min(complete, 66); i++)
        {
            bool variable = i is 3 or 9 or 10 or >= 19 and <= 24;
            if (variable ? !lines[i].StartsWith(Prefix[i] + " ", StringComparison.Ordinal) : lines[i] != Prefix[i]) return -1;
        }
        if (complete > 66 && lines[66] != "TEST_START m03.010") return -1;
        for (int i = 67; i < Math.Min(complete, 76); i++)
            if (!Row(lines[i], i - 67, out _)) return -1;
        if (complete < 77) return 0;
        if (!ValidateRecords(lines[..77])) return -1;
        prerequisiteText = string.Join("\r\n", lines.AsSpan(0, 76).ToArray()) + "\r\n";
        return 1;
    }

    private static bool ValidateRecords(string[] lines)
    {
        var rows = new ulong[9][];
        for (int i = 0; i < 9; i++) if (!Row(lines[67 + i], i, out rows[i])) return false;
        ulong[] allocation = rows[0], descriptor = rows[6], armed = rows[7], entered = rows[8];
        if (!Extent(allocation[0], 312, 8) || !Extent(allocation[2], 8192, 4096) ||
            allocation[1] != allocation[2] || allocation[3] != 2 || allocation[4] != 2 || allocation[5] != 8192 || allocation[6] != 0 || allocation[7] != 0) return false;
        ulong workspace = rows[1][0], copied = rows[1][1], offset = 0;
        if (!Extent(workspace, 1, 8) || copied < 16 || !Extent(copied - 16, 266272, 8)) return false;
        string[] roles = ["rsdp", "root", "mcfg", "apic", "hpet"];
        string[] names = ["physical", "owned", "offset", "length", "signature", "rootIndex", "compared", "mismatches"];
        for (int i = 0; i < 5; i++)
        {
            ulong[] row = rows[i + 1];
            if (!DecimalFields(lines[19 + i], "CLROS_M03_002_TABLE role=" + roles[i], names, out ulong[] prior) ||
                row[0] != workspace || row[1] != copied || row[2] != prior[0] || copied + offset != prior[1] || row[3] != offset ||
                row[3] != prior[2] || row[4] != prior[3] || row[5] != prior[4] || row[6] != prior[5] || row[7] != row[4] ||
                row[7] != prior[6] || prior[7] != 0 || row[4] == 0 || row[4] > (i == 0 ? 4096UL : 65536UL)) return false;
            offset += row[4];
        }
        if (offset > 266240 || !Extent(descriptor[0], 105 * 4096, 4096) || !Extent(descriptor[1], 55 * 4096, 4096) ||
            descriptor[1] >= 0x100000000UL - 55 * 4096 || !Extent(descriptor[2], 3 * 4096, 4096) ||
            descriptor[3] != descriptor[0] + 4096 || descriptor[4] != descriptor[0] + 8192 || descriptor[5] != descriptor[0] + 12288 ||
            descriptor[6] != 24 || descriptor[7] <= descriptor[0] + 22 * 4096 + 8 || descriptor[7] >= descriptor[0] + 86 * 4096 - 16 ||
            (descriptor[7] & 15) != 8) return false;
        if (armed[0] != descriptor[0] + 21 * 4096 || armed[1] != descriptor[1] || armed[2] != descriptor[0] + 1024 ||
            armed[3] != descriptor[2] || armed[4] != descriptor[0] || armed[5] != descriptor[0] + 22 * 4096 + 8 ||
            armed[6] != descriptor[0] + 86 * 4096 - 16 || armed[7] != descriptor[0] + 98 * 4096 - 16 ||
            entered[0] != armed[0] || entered[1] != armed[1] || entered[2] != armed[2] ||
            entered[4] != descriptor[0] + 94 * 4096 + 8 || entered[5] != armed[7] || entered[3] < entered[4] ||
            entered[3] > entered[5] - 184 || (entered[3] & 7) != 0 || entered[6] != 14 || entered[7] != 0) return false;
        if (!HexFields(lines[76], FaultHeading, FaultNames, out ulong[] fault)) return false;
        return Extent(fault[0], 1, 1) && fault[1] >= armed[5] && fault[1] < armed[6] &&
            (fault[3] & 0x202) == 2 && fault[4] == armed[0] && fault[5] == armed[1];
    }

    private static bool Extent(ulong start, ulong bytes, ulong alignment)
        => start != 0 && start < LowEnd && (start & (alignment - 1)) == 0 && bytes < LowEnd - start;
    private static bool Row(string line, int stage, out ulong[] values)
        => HexFields(line, "M03_OWNED " + stage.ToString(System.Globalization.CultureInfo.InvariantCulture), [], out values, 8);
    private static bool HexFields(string line, string heading, string[] names, out ulong[] values, int positional = 0)
    {
        values = new ulong[positional == 0 ? names.Length : positional];
        if (!line.StartsWith(heading + " ", StringComparison.Ordinal)) return false;
        string[] fields = line[(heading.Length + 1)..].Split(' ');
        if (fields.Length != values.Length) return false;
        for (int i = 0; i < fields.Length; i++)
        {
            string key = positional == 0 ? names[i] + "=0x" : "0x";
            if (!fields[i].StartsWith(key, StringComparison.Ordinal) || fields[i].Length != key.Length + 16) return false;
            foreach (char c in fields[i].AsSpan(key.Length)) if (!(c is >= '0' and <= '9' or >= 'A' and <= 'F')) return false;
            if (!ulong.TryParse(fields[i].AsSpan(key.Length), System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out values[i])) return false;
        }
        return true;
    }
    private static bool DecimalFields(string line, string heading, string[] names, out ulong[] values)
    {
        values = new ulong[names.Length];
        if (!line.StartsWith(heading + " ", StringComparison.Ordinal)) return false;
        string[] fields = line[(heading.Length + 1)..].Split(' ');
        if (fields.Length != names.Length) return false;
        for (int i = 0; i < names.Length; i++)
        {
            string key = names[i] + "=";
            if (!fields[i].StartsWith(key, StringComparison.Ordinal)) return false;
            ReadOnlySpan<char> digits = fields[i].AsSpan(key.Length);
            if (digits.Length == 0 || (digits.Length > 1 && digits[0] == '0')) return false;
            foreach (char c in digits) if (c < '0' || c > '9') return false;
            if (!ulong.TryParse(digits, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out values[i])) return false;
        }
        return true;
    }
}
