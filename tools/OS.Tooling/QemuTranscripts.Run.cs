using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static void RequireCompleteProcess(ProcessResult result, string reason = "qemu_runner_incomplete") =>
        Need(result.TerminationConfirmed && result.FailureReason.Length == 0 && !result.TimedOut && !result.Cancelled && !result.OutputLimitExceeded, reason);

    public static int ValidateRun(string root, QemuSmokeOptions options, QemuSmokePlan plan, ProcessResult runner, TextWriter output,
        NativeAotHelloProgram? helloProgram = null, NativeAotThreadingProgram? threadingProgram = null, NativeAotRuntimeProgram? runtimeProgram = null,
        NativeAotSocketProgram? socketProgram=null,NativeAotSocketPeer.Completion? socketPeer=null,NativeAotSocketContainmentProgram? containmentProgram=null,
        NativeAotSocketVectorsProgram? vectorsProgram=null,NativeAotSocketVectorsPeer.Completion? vectorsPeer=null)
    {
        RequireCompleteProcess(runner);
        Need((options.LinuxHelloDisassembly.Length != 0) == (helloProgram is not null), "hello_prepared_program_required");
        if (helloProgram is not null) Need(string.Equals(Path.GetFullPath(root), helloProgram.SourceRoot,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal), "hello_prepared_source_root");
        bool signalEpoll=LinuxSignalEpollSelection.Bind(options,plan.Flags);
        Need(!signalEpoll||helloProgram is null&&threadingProgram is null&&runtimeProgram is null&&socketProgram is null&&socketPeer is null&&
            containmentProgram is null&&vectorsProgram is null&&vectorsPeer is null,"signal_epoll_prepared_program_conflict");
        string socketApp=signalEpoll?"":NativeAotSocketSelection.Application(plan.Flags,options.LinuxThreadingApplication);
        bool containment=NativeAotSocketContainmentSelection.Selected(plan.Flags);
        bool vectors=NativeAotSocketVectorsSelection.Selected(plan.Flags);
        Need(vectors==(vectorsProgram is not null) && (vectorsProgram is not null)==(vectorsPeer is not null),"socket_vectors_prepared_program_and_peer_required");
        if(vectorsProgram is not null)
        {
            var comparison=OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
            Need(helloProgram is null&&threadingProgram is null&&runtimeProgram is null&&socketProgram is null&&socketPeer is null&&containmentProgram is null&&
                vectorsPeer is not null&&ReferenceEquals(runner,vectorsPeer.Runner)&&plan.IsEnabled("EnableM16012Probe")&&plan.IsEnabled("EnableM16011Probe")&&
                options.LinuxThreadingApplication==NativeAotSocketVectorsPublisher.Application&&options.LinuxThreadingDisassembly.Length!=0&&
                string.Equals(Path.GetFullPath(root),vectorsProgram.SourceRoot,comparison)&&string.Equals(Path.GetFullPath(root),vectorsPeer.Root,comparison)&&
                string.Equals(Path.GetFullPath(options.SerialLog,root),vectorsPeer.SerialPath,comparison)&&
                string.Equals(Path.GetFullPath(options.SerialLog+".peer.log",root),vectorsPeer.Path,comparison),"socket_vectors_prepared_selection");
        }
        Need(containment==(containmentProgram is not null),"socket_containment_prepared_program_required");
        if(containmentProgram is not null)
            Need(helloProgram is null&&threadingProgram is null&&runtimeProgram is null&&socketProgram is null&&socketPeer is null&&
                options.LinuxThreadingApplication==NativeAotSocketContainmentPublisher.Application&&options.LinuxThreadingDisassembly.Length!=0&&
                string.Equals(Path.GetFullPath(root),containmentProgram.SourceRoot,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal),
                "socket_containment_prepared_selection");
        Need((socketApp.Length!=0)==(socketProgram is not null) && (socketProgram is not null)==(socketPeer is not null),"socket_prepared_program_and_peer_required");
        if(socketProgram is not null)
        {
            Need(helloProgram is null&&threadingProgram is null&&runtimeProgram is null&&socketPeer is not null&&ReferenceEquals(runner,socketPeer.Runner) &&
                socketProgram.Asynchronous==plan.IsEnabled("EnableM16012Probe") && options.LinuxThreadingDisassembly.Length!=0 &&
                string.Equals(Path.GetFullPath(root),socketProgram.SourceRoot,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal) &&
                string.Equals(Path.GetFullPath(root),socketPeer.Root,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal) &&
                (socketProgram.Asynchronous?string.Equals(Path.GetFullPath(options.SerialLog,root),socketPeer.ReceiveGateSerial,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal):socketPeer.ReceiveGateSerial.Length==0) &&
                string.Equals(Path.GetFullPath(options.SerialLog+".peer.log",root),socketPeer.Path,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal),"socket_prepared_selection");
        }
        string runtimeApp = signalEpoll||socketProgram is not null||containment||vectors?"":NativeAotRuntimeSelection.Application(plan.Flags, options.LinuxThreadingApplication);
        Need((runtimeApp.Length != 0) == (runtimeProgram is not null), "runtime_prepared_program_required");
        Need(options.HasThreadingSelection == (threadingProgram is not null || runtimeProgram is not null || socketProgram is not null || containmentProgram is not null || vectorsProgram is not null), "threading_prepared_program_required");
        Need(threadingProgram is null || runtimeProgram is null, "runtime_threading_conflict");
        if (runtimeProgram is not null)
        {
            Need(helloProgram is null && options.LinuxHelloDisassembly.Length == 0, "threading_hello_conflict");
            Need(runtimeProgram.Application == runtimeApp && options.LinuxThreadingApplication == runtimeApp &&
                options.LinuxThreadingDisassembly.Length != 0 && plan.IsEnabled("EnableM16009Probe"), "runtime_prepared_selection");
            Need(string.Equals(Path.GetFullPath(root), runtimeProgram.SourceRoot,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal), "runtime_prepared_source_root");
        }
        if (threadingProgram is not null)
        {
            Need(helloProgram is null && options.LinuxHelloDisassembly.Length == 0, "threading_hello_conflict");
            Need(options.LinuxThreadingApplication == threadingProgram.Application && options.LinuxThreadingDisassembly.Length != 0 &&
                plan.IsEnabled("EnableM16009Probe"), "threading_prepared_selection");
            Need(string.Equals(Path.GetFullPath(root), threadingProgram.SourceRoot,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal), "threading_prepared_source_root");
        }
        using AuthorityFiles? threadingEvidence = signalEpoll?HoldSignalEpollEvidence(root,options):
            vectorsPeer is not null?HoldSocketVectorsEvidence(root,options,vectorsPeer):
            containmentProgram is not null?HoldSocketContainmentEvidence(root,options):
            socketPeer is not null?HoldSocketEvidence(root,options,socketPeer):
            threadingProgram is null && runtimeProgram is null ? null : HoldThreadingEvidence(root, options);
        bool Enabled(string name) => plan.IsEnabled("Enable" + name + "Probe");
        string path = Path.GetFullPath(options.SerialLog, root);
        if (plan.IsNormalBoot || new[] { "M03TypedExitBootServicesCall", "M04TypeManagerModuleRegistration", "M03FirmwareEntropySeed", "AcpiPhysicalDiscovery", "M02UnexpectedVectorPanic", "M03MemoryMapCapture" }.Any(Enabled))
            RequireEmptyNativeError(runner.StandardError, Path.GetFullPath(options.SummaryLog + ".stderr.log", root));
        string[] lines = runner.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        string containmentPeerLine="";
        if(containment)
        {
            string[] peerLines=lines.Where(line=>line.StartsWith("SOCKET_CONTAINMENT_PEER_RESULT ",StringComparison.Ordinal)).ToArray();
            Need(peerLines.Length==1,"socket_containment_runner_peer_count");containmentPeerLine=peerLines[0];
            _=ReadSocketContainmentPeerResult(containmentPeerLine);
        }
        if (lines.Length != 0 && lines[^1] == "") lines = lines[..^1];
        if (runner.ExitCode != 0 && !Enabled("M02UnexpectedVectorPanic") && !Enabled("M03OwnedSubsystemPanic") && !Enabled("M04UnhandledException"))
        {
            if (Enabled("M03FirmwareEntropySeed") && options.FirmwareRngSource == "missing") AssertM03FirmwareEntropyTranscript(path, true);
            return runner.ExitCode;
        }
        if (plan.IsNormalBoot || Enabled("M03FirstRealExitBootServices") || Enabled("AcpiPhysicalDiscovery"))
        {
            Need(runner.StandardError.Length == 0, "qemu_runner_stderr_not_empty");
            AssertM03AcpiBootstrapTranscript(path, plan.IsNormalBoot || Enabled("M03FirstRealExitBootServices"), Enabled("M03FirmwareAdapterRevocation"), plan.IsNormalBoot || Enabled("M03OwnedSubsystemSurvival"), Enabled("M03OwnedSubsystemPanic"), plan.IsNormalBoot, Enabled("M05PhysicalMapping"), Enabled("M05OwnedAcpiSelection"), Enabled("M05MonotonicClock"), Enabled("M05PciEnumeration"), Enabled("M05BarDma"), Enabled("M05VirtioNegotiation"), Enabled("M05VirtqueuePolling"), Enabled("M05ApicSetup"), Enabled("M05VirtqueueInterrupt"), Enabled("M06VirtioBlockQueueSetup"), Enabled("M06VirtioBlockRead"), Enabled("M06LiveBlockContract"), Enabled("M06GptPartition"), options.GptImageVariant == "PrimaryHeaderCrcFault", Enabled("M06FatRead"), Enabled("M06PackageRead"), options.PackageImageVariant, Enabled("M06BlockMsix"), Enabled("M06BlockMsixPollingRecovery"), Enabled("M07VirtioNet"), Enabled("M07ArpReceive"), Enabled("M16002") ? Path.GetFullPath(options.LinuxElfPackage, root) : "", Enabled("M16003"), Enabled("M16004"), Enabled("M16005"), linuxMemory: Enabled("M16006"), linuxDescriptors: Enabled("M16007"), cooperativeSwitch: Enabled("M09004"), schedulerTimer: Enabled("M09005"), kernelThreads: Enabled("M09006"), waitAddress: Enabled("M09009"), privateTls: Enabled("M09010"), linuxThreads: Enabled("M16008"), linuxSignals: Enabled("M16009"), helloProgram: helloProgram, threadingProgram: threadingProgram, runtimeProgram: runtimeProgram, networkService: Enabled("M07026"),socketProgram:socketProgram,containmentProgram:containmentProgram,containmentPeerLine:containmentPeerLine,vectorsProgram:vectorsProgram,signalEpollRace:signalEpoll);
            if(signalEpoll||socketProgram is not null||containmentProgram is not null||vectorsProgram is not null){}
            else if (Enabled("M07026")) AssertM07026Capture(path, Path.GetFullPath(options.SerialLog + ".pcap", root));
            else if (Enabled("M07ArpReceive")) _ = AssertM07ArpReceiveCapture(path, Path.GetFullPath(options.SerialLog + ".pcap", root));
            else if (Enabled("M07VirtioNet")) _ = AssertM07VirtioNetCapture(path, Path.GetFullPath(options.SerialLog + ".pcap", root));
            if (Enabled("M06BlockMsix")) AssertM06BlockMsixImage(path, Path.GetFullPath(options.Image, root), Enabled("M06BlockMsixPollingRecovery"));
            if (Enabled("M06VirtioBlockRead")) AssertM06VirtioBlockReadImage(path, Path.GetFullPath(options.Image, root));
            if (Enabled("M06LiveBlockContract")) AssertM06LiveBlockContractImage(path, Path.GetFullPath(options.Image, root));
            if (Enabled("M06FatRead")) AssertM06FatReadImage(path, Path.GetFullPath(options.Image, root));
            if (Enabled("M06PackageRead") && !Enabled("M06BlockMsix")) AssertM06PackageReadImage(path, Path.GetFullPath(options.Image, root), options.PackageImageVariant);
            if (Enabled("M06GptPartition")) AssertM06GptPartitionImage(path, Path.GetFullPath(options.Image, root), options.GptImageVariant == "PrimaryHeaderCrcFault");
            if (Enabled("M03OwnedSubsystemPanic")) AssertExpectedPanicRunner(root, options, plan, runner, lines, ownedSubsystem: true);
            else AssertStandardRunnerResult(root, options, plan, lines, helloProgram, threadingProgram, runtimeProgram,socketProgram,socketPeer?.Port,containmentProgram,vectorsProgram,vectorsPeer?.Port);
        }
        else if (Enabled("M03TypedExitBootServicesCall"))
        { Need(runner.StandardError.Length == 0, "qemu_runner_stderr_not_empty"); AssertM03TypedExitBootServicesTranscript(path); AssertStandardRunnerResult(root, options, plan, lines); }
        else if (Enabled("M03FirmwareEntropySeed"))
        { Need(runner.StandardError.Length == 0, "qemu_runner_stderr_not_empty"); AssertM03FirmwareEntropyTranscript(path); AssertStandardRunnerResult(root, options, plan, lines); }
        else ValidateDiagnosticTranscript(root, options, plan, runner, lines);
        ValidateUnselectedRecords(File.ReadAllText(path), plan);
        threadingEvidence?.VerifyUnchanged();
        return 0;
    }

    public static void RequireEmptyNativeError(string error, string preservedPath)
    {
        if (error.Length == 0) return;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(preservedPath))!);
        File.WriteAllText(preservedPath, error, new UTF8Encoding(false));
        throw new ToolFailure("qemu_runner_stderr_not_empty", "path=" + preservedPath);
    }

    public static void AssertStandardRunnerResult(string root, QemuSmokeOptions options, QemuSmokePlan plan, IReadOnlyList<string> original,
        NativeAotHelloProgram? helloProgram = null, NativeAotThreadingProgram? threadingProgram = null, NativeAotRuntimeProgram? runtimeProgram = null,
        NativeAotSocketProgram? socketProgram=null,int? socketForwardPort=null,NativeAotSocketContainmentProgram? containmentProgram=null,
        NativeAotSocketVectorsProgram? vectorsProgram=null,int? vectorsForwardPort=null)
    {
        var lines = original.ToList();
        bool entropy = plan.IsEnabled("EnableM03FirmwareEntropySeedProbe") || plan.IsEnabled("EnableM16004Probe");
        Need(vectorsProgram is null || socketProgram is null&&containmentProgram is null&&helloProgram is null&&threadingProgram is null&&runtimeProgram is null,"socket_vectors_runner_program_conflict");
        if(socketProgram is not null||vectorsProgram is not null)
        {
            if(vectorsProgram is not null)socketForwardPort=vectorsForwardPort;
            int at=entropy?8:7;Need(socketForwardPort is >0 and <=65535 && lines.Count==(entropy?19:18) &&
                lines[at]=="SOCKET_FORWARD_PORT "+socketForwardPort.Value.ToString(CultureInfo.InvariantCulture),"socket_runner_forward_identity");lines.RemoveAt(at);
        }
        if(containmentProgram is not null)
        {
            int at=entropy?16:15;Need(lines.Count==(entropy?19:18),"socket_containment_runner_count");
            _=ReadSocketContainmentPeerResult(lines[at]);lines.RemoveAt(at);
        }
        if (plan.IsEnabled("EnableM07026Probe")&&!options.SignalEpollRace&&socketProgram is null&&containmentProgram is null&&vectorsProgram is null)
        {
            Need(!entropy && lines.Count == 18 && lines[15] ==
                "NETWORK_PEER_RESULT complete=True stopped=True arpReplies=21 echoReplies=12 tcpPackets=80", "m07_026_peer_runner_result");
            lines.RemoveAt(15);
        }
        if (entropy)
        {
            Need(lines.Count == 18 && lines[7] == "FIRMWARE_RNG_SOURCE virtio", "qemu_runner_source"); lines.RemoveAt(7);
        }
        Need(lines.Count == 17 && lines.All(l => !Controls.IsMatch(l)), "qemu_runner_count_or_control");
        Match q = Regex.Match(lines[0], @"\AQEMU_DISCOVERY result=(.+) checked=(.+)\z"), o = Regex.Match(lines[1], @"\AOVMF_DISCOVERY result=(.+) checked=(.+)\z");
        Need(q.Success && o.Success && q.Groups[1].Value != "not_found" && o.Groups[1].Value != "not_found", "qemu_runner_discovery");
        string Format(IReadOnlyList<string> values) => values.Count == 0 ? "none" : string.Join(';', values);
        string[] expected = ["TEST_START qemu.smoke", "IMAGE_ARTIFACT " + options.Image, "SERIAL_LOG " + options.SerialLog, "OVMF_CODE " + o.Groups[1].Value,
            "WORKLOAD_PROFILE " + options.WorkloadProfile, "MARKER_PRESETS " + Format(plan.Presets), "REQUIRED_MARKERS " + string.Join(';', plan.Required),
            "ORDERED_MARKERS " + Format(plan.Ordered), "UNIQUE_MARKERS " + Format(plan.Unique), "FORBIDDEN_MARKERS " + string.Join(';', plan.Forbidden),
            lines[12], "SERIAL_MARKER_DRAIN milliseconds=750", lines[14], "SUMMARY_LOG " + options.SummaryLog, "TEST_PASS qemu.smoke"];
        for (int i = 0; i < expected.Length; i++) Need(lines[i + 2] == expected[i], "qemu_runner_record_" + i);
        AssertRunnerTerminated(lines);
        string serialPath = Path.GetFullPath(options.SerialLog, root);
        byte[] raw = File.ReadAllBytes(serialPath); string serial = DecodeUtf8(raw);
        int terminal = serial.IndexOf("OS_SMOKE_PASS\r\n", StringComparison.Ordinal);
        bool noSuffix = plan.IsNormalBoot || plan.IsEnabled("EnableM03FirstRealExitBootServicesProbe") || plan.IsEnabled("EnableM04TypeManagerModuleRegistrationProbe");
        int protocolEnd = noSuffix ? serial.Length : terminal + "OS_SMOKE_PASS\r\n".Length;
        int prefixBytes = plan.IsEnabled("EnableM06VirtioBlockQueueSetupProbe") && options.GptImageVariant != "PrimaryHeaderCrcFault" ? QemuContracts.BlockFirmwarePrefixBytes : FirmwarePrefixBytes;
        Need(protocolEnd >= prefixBytes, "qemu_runner_protocol_extent");
        string protocolText = serial[prefixBytes..protocolEnd];
        if (helloProgram is not null) protocolText = NormalizeHelloWrites(protocolText);
        if (threadingProgram is not null) protocolText = NormalizeThreadingWrites(protocolText, threadingProgram.Application);
        if (runtimeProgram is not null) protocolText = NormalizeRuntimeWrites(protocolText, runtimeProgram.Application);
        if(socketProgram is not null)protocolText=NormalizeSocketWrites(protocolText,socketProgram.Asynchronous);
        if(containmentProgram is not null)protocolText=NormalizeSocketContainmentWrites(protocolText);
        if(vectorsProgram is not null)protocolText=NormalizeSocketVectorWrites(protocolText);
        string[] protocol = protocolText.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        int suffixBytes = raw.Length - protocolEnd;
        int streamLines = protocol.Length + (noSuffix ? 2 : suffixBytes == 251 ? 4 : 5), invalidLines = noSuffix || suffixBytes == 251 ? 1 : 2;
        if (helloProgram is not null || threadingProgram is not null || runtimeProgram is not null || socketProgram is not null || containmentProgram is not null || vectorsProgram is not null) streamLines = serial.Count(c => c == '\n');
        int invalid = raw.Count(b => b is not (9 or 10 or 13) && (b < 32 || b > 126));
        string stream = $"SERIAL_MARKER_STREAM status=Passed terminal=Success bytes={raw.Length} lines={streamLines} invalidBytes={invalid} invalidLines={invalidLines} orderedSeen={plan.Ordered.Count} outOfOrder=0 uniqueSeen={plan.Unique.Count} duplicateUnique=0";
        Need(lines[12] == stream, "qemu_runner_stream_accounting");
        List<string> summary = ["QEMU_SMOKE_STATUS pass"];
        foreach (int i in new[] { 1, 2, 4, 5, 6, 7, 8, 9 })
        { summary.Add(expected[i]); if (entropy && i == 4) summary.Add("FIRMWARE_RNG_SOURCE virtio");if((socketProgram is not null||vectorsProgram is not null)&&i==4)summary.Add("SOCKET_FORWARD_PORT "+socketForwardPort!.Value.ToString(CultureInfo.InvariantCulture)); }
        string[]? rawLines = threadingProgram is null && runtimeProgram is null && socketProgram is null && containmentProgram is null && vectorsProgram is null ? null : ReadThreadingSerialLines(serial);
        summary.AddRange((rawLines is null ? protocol : rawLines.Where(l => IsThreadingObservedLine(l, plan))).Select(l => "OBSERVED_MARKER " + l));
        summary.AddRange(["APP_EXIT_RESULT none", "APP_EXIT_MALFORMED none", "MISSING_MARKERS none", "OUT_OF_ORDER_MARKERS none", "DUPLICATE_UNIQUE_MARKERS none", "FORBIDDEN_MARKER_HIT none", "FAIL_REASON none", "SERIAL_TAIL_LIMIT lines=8 chars=240"]);
        foreach (string line in (rawLines ?? serial.Split("\r\n", StringSplitOptions.None).Where(l => l.Trim().Length != 0)).TakeLast(8))
        { string s = line.Trim(); summary.Add("SERIAL_TAIL_LINE " + (s.Length > 240 ? s[..237] + "..." : s)); }
        byte[] expectedBytes = [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(string.Join("\r\n", summary) + "\r\n")];
        Need(File.ReadAllBytes(Path.GetFullPath(options.SummaryLog, root)).AsSpan().SequenceEqual(expectedBytes), "qemu_runner_summary_bytes");
    }

    // The runner observes original serial lines. Payload newlines may split a
    // write envelope; normalized protocol rows cannot reproduce its summary.
    private static string[] ReadThreadingSerialLines(string serial)
    {
        List<string> lines = [];
        using StringReader reader = new(serial);
        while (reader.ReadLine() is string line)
            if (line.Trim() is { Length: > 0 } trimmed) lines.Add(trimmed);
        return lines.ToArray();
    }

    private static bool IsThreadingObservedLine(string line, QemuSmokePlan plan) =>
        OS.Tools.QemuRunner.SerialMarkerSummary.IsStandardObservedMarkerLine(line) ||
        plan.Required.Concat(plan.Ordered).Concat(plan.Unique).Concat(plan.Forbidden).Any(marker =>
            line == marker || line.StartsWith(marker + " ", StringComparison.Ordinal));

    public static void AssertM04SimpleCatchSelection(string text, bool enabled, bool finallyEnabled = false, bool rethrowEnabled = false, bool nestedEnabled = false, bool unhandledEnabled = false, bool framesEnabled = false, bool constructorFailureEnabled = false, bool filteredCatchEnabled = false, bool normalFinallyEnabled = false)
        => Need(enabled || !Regex.IsMatch(Classify(text), finallyEnabled || rethrowEnabled || nestedEnabled || unhandledEnabled || framesEnabled || constructorFailureEnabled || filteredCatchEnabled || normalFinallyEnabled ? @"(?i)M04_CATCH|CLROS_M04_SIMPLE_CATCH_READY|m04\.021\.simple-catch" : @"(?i)M04_CATCH|CLROS_M04_SIMPLE_CATCH_READY|m04\.021"), "m04_simple_catch_unselected_record");
    public static void AssertM04SingleFinallySelection(string text, bool enabled)
        => Need(enabled || !Regex.IsMatch(Classify(text), @"(?i)M04_FINALLY|CLROS_M04_SINGLE_FINALLY_READY|m04\.021\.single-finally"), "m04_single_finally_unselected_record");
    public static void AssertM04InterfaceDispatchSelection(string text, bool enabled)
        => Need(enabled || !Regex.IsMatch(Classify(text), @"(?i)M04_DISPATCH|CLROS_M04_016_READY|m04\.016"), "m04_dispatch_unselected_record");
    public static void AssertM04X64UnwindMetadataSelection(string text, bool enabled)
        => Need(enabled || !Regex.IsMatch(Classify(text), @"(?i)CLROS_M04_020_|m04\.020"), "m04_x64_unwind_unselected_record");
    public static void ValidateUnselectedRecords(string text, QemuSmokePlan plan)
    {
        string candidate = Classify(text);
        bool Enabled(string name) => plan.IsEnabled("Enable" + name + "Probe");
        AssertM04SimpleCatchSelection(text, Enabled("M04SimpleThrowCatch"), Enabled("M04SingleFinallyCatch"), Enabled("M04Rethrow"), Enabled("M04NestedFinallyCatch"), Enabled("M04UnhandledException"), Enabled("M04FrameDispatch"), Enabled("M04ConstructorFailure"), Enabled("M04FilteredCatch"), Enabled("M04NormalFinally"));
        AssertM04SingleFinallySelection(text, Enabled("M04SingleFinallyCatch"));
        AssertM04InterfaceDispatchSelection(text, Enabled("M04InterfaceDispatch"));
        AssertM04X64UnwindMetadataSelection(text, Enabled("M04X64UnwindMetadata"));
        Need(plan.IsNormalBoot || !Regex.IsMatch(candidate, @"(?i)CLROS_M03_011_READY|m03\.011"), "m03_normal_unselected_record");
        Need(plan.IsNormalBoot || Enabled("M03OwnedSubsystemSurvival") || !Regex.IsMatch(candidate, @"(?i)M03_OWNED|CLROS_M03_010_READY|m03\.010"), "m03_owned_unselected_record");
        Need(Enabled("M03OwnedSubsystemPanic") || !Regex.IsMatch(text, @"(?m)^M03_OWNED [78](?: |$)"), "m03_owned_panic_unselected_record");
        Need(Enabled("M03FirmwareAdapterRevocation") || !Regex.IsMatch(candidate, @"(?i)M03_ADAPTER_|CLROS_M03_008_READY|m03\.008"), "m03_revocation_unselected_record");
        Need(Enabled("M04ClassConstructorExecution") || !Regex.IsMatch(candidate, @"(?i)M04_CCTOR|CLROS_M04_024_READY|m04\.024"), "m04_cctor_unselected_record");
        Need(Enabled("M05VirtqueuePolling") || Enabled("M05VirtqueueInterrupt") ||
            !Regex.IsMatch(candidate, @"(?i)M05_QUEUE|CLROS_M05_01[6-9]_READY|m05\.01[6-9]"), "m05_queue_unselected_record");
        foreach (var (flag, pattern) in new (string Flag, string Pattern)[]
        {
            ("M16013", @"CLROS_M16_013_READY|TEST_(?:START|PASS)m16-013"),
            ("M04SimpleThrowCatch", "M04_CATCH|CLROS_M04_SIMPLE_CATCH|m04\\.021\\.simple-catch"),
            ("M04SingleFinallyCatch", "M04_FINALLY|CLROS_M04_SINGLE_FINALLY|m04\\.021\\.single-finally"),
            ("M04Rethrow", @"M04_RETHROW|CLROS_M04_RETHROW_READY|m04\.021\.rethrow"),
            ("M04FrameDispatch", @"M04_FRAMES|CLROS_M04_021_FRAMES_READY|m04\.021\.frames"),
            ("M04NormalFinally", @"M04_NORMAL_FINALLY|CLROS_M04_021_NORMAL_FINALLY_READY|m04\.021\.normal-finally"),
            ("M04ConstructorFailure", @"M04_CTOR_FAILURE|CLROS_M04_021_CTOR_FAILURE_READY|m04\.021\.ctor-failure"),
            ("M04FilteredCatch", @"M04_FILTERS|CLROS_M04_021_FILTERS_READY|m04\.021\.filters"),
            ("M04UnhandledException", @"M04_UNHANDLED|CLROS_M04_UNHANDLED_READY|m04\.021\.unhandled|OS_SMOKE_FAILsubsystem=runtimereason=unhandled-exception"),
            ("M04NestedFinallyCatch", @"M04_NESTED|CLROS_M04_NESTED_FINALLY_READY|m04\.021\.nested-finally"),
            ("M05PhysicalMapping", @"M05_MAP|CLROS_M05_001_READY|m05\.001"),
            ("M05OwnedAcpiSelection", @"M05_ACPI|CLROS_M05_002_READY|m05\.002"),
            ("M05MonotonicClock", @"M05_CLOCK|CLROS_M05_003_READY|m05\.003"),
            ("M05ApicSetup", @"M05_APIC|CLROS_M05_020_READY|m05\.020"),
            ("M05VirtqueueInterrupt", @"M05_IRQ|CLROS_M05_021_READY|m05\.021"),
            ("M06BlockMsix", @"M06_IRQ|CLROS_M06_019_READY|m06\.019"),
            ("M06VirtioBlockQueueSetup", @"M06_BLK|CLROS_M06_00[1-4]_READY|m06\.00[1-4]"),
            ("M06FatRead", @"M06_FAT|CLROS_M06_01[2-4]_READY|m06\.01[2-4]"),
            ("M06GptPartition", @"M06_GPT|CLROS_M06_(009|01[0-1])_READY|m06\.(009|01[0-1])"),
            ("M05PciEnumeration", @"M05_PCI|CLROS_M05_00[4-7]_READY|m05\.00[4-7]"),
            ("M04InterfaceDispatch", "M04_DISPATCH|CLROS_M04_016|m04\\.016"),
            ("M04X64UnwindMetadata", "CLROS_M04_020|m04\\.020"),
            ("M04SeededCollectorChurn", "M04_CHURN|CLROS_M04_014"),
            ("M04GcStaticRootMaterialization", "M04_GC_ROOT|M04_COLLECT|CLROS_M04_013"),
            ("M04CheckedReferenceBarrier", @"M04_REFERENCE_|CLROS_M04_010_NATIVE|m04\.010\.references"),
            ("M04StringAllocation", @"M04_STRING_|CLROS_M04_009|m04\.009"), ("M04SzArrayAllocation", @"M04_ARRAY_|CLROS_M04_008|m04\.008\.sz_array"),
            ("M02PageFaultCr2ErrorCode", @"M02_PAGE_FAULT|CLROS_M02_014|TEST_(?:START|PASS|FAIL)m02\.014"), ("M02DoubleFaultIst", @"M02_DF_|M02_DOUBLE_FAULT|CLROS_M02_015|TEST_(?:START|PASS|FAIL)m02\.015")
        }) if (!plan.IsEnabled("Enable" + flag + "Probe")) Need(!Regex.IsMatch(candidate, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "qemu_unselected_profile_record_" + flag);
        if (!plan.IsNormalBoot && !plan.IsEnabled("EnableM03FirstRealExitBootServicesProbe"))
            Need(!Regex.IsMatch(candidate, @"(?i)M03_EBS_|M03_PERMANENT_|M03_POST_EBS_|CLROS_M03_007_READY|m03\.007|UEFI_EBS_SUCCESS|CLROS_M03_009_READY|m03\.009"), "m03_first_exit_unselected_record");
    }
}
