using System.Text;
using OS.Tooling;

internal static class ContinuingNetworkRunnerTranscriptControls
{
    // Exercise the public complete-run validator, including its original
    // parents, packet capture, runner metadata and byte-exact summary.
    internal static void Run(string root, string serialPath, string[] protocol, string packagePath)
    {
        string summaryPath = Path.Combine(root, "summary.log");
        var options = QemuSmokeOptions.Parse("--BuildEfi", "--EnableM07026Probe", "--LinuxElfPackage", packagePath,
            "--Image", "image.raw", "--SerialLog", serialPath, "--SummaryLog", summaryPath);
        var plan = QemuProfiles.Create(options);
        byte[] raw = File.ReadAllBytes(serialPath);
        string serial = Encoding.UTF8.GetString(raw);
        int invalid = raw.Count(b => b is not (9 or 10 or 13) && (b < 32 || b > 126));
        const string peer = "NETWORK_PEER_RESULT complete=True stopped=True arpReplies=21 echoReplies=12 tcpPackets=80";
        string[] backend = ["QEMU_ACCELERATOR " + options.Accelerator, "QEMU_MACHINE " + QemuContracts.MachineArgument(options.Accelerator)];
        string[] runner = [..backend, "QEMU_DISCOVERY result=qemu.exe checked=qemu.exe", "OVMF_DISCOVERY result=ovmf.fd checked=ovmf.fd",
            "TEST_START qemu.smoke", "IMAGE_ARTIFACT image.raw", "SERIAL_LOG " + serialPath, "OVMF_CODE ovmf.fd",
            "WORKLOAD_PROFILE " + options.WorkloadProfile, "MARKER_PRESETS none", "REQUIRED_MARKERS " + string.Join(';', plan.Required),
            "ORDERED_MARKERS " + string.Join(';', plan.Ordered), "UNIQUE_MARKERS " + string.Join(';', plan.Unique),
            "FORBIDDEN_MARKERS " + string.Join(';', plan.Forbidden),
            $"SERIAL_MARKER_STREAM status=Passed terminal=Success bytes={raw.Length} lines={protocol.Length + 2} invalidBytes={invalid} invalidLines=1 orderedSeen={plan.Ordered.Count} outOfOrder=0 uniqueSeen={plan.Unique.Count} duplicateUnique=0",
            "SERIAL_MARKER_DRAIN milliseconds=750", "QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=-1", peer,
            "SUMMARY_LOG " + summaryPath, "TEST_PASS qemu.smoke"];
        List<string> summary = ["QEMU_SMOKE_STATUS pass"];
        foreach (int i in new[] { 3, 4, 6, 7, 8, 9, 10, 11 })
        {
            summary.Add(runner[i + 2]);
            if (i == 6)
                summary.AddRange(backend);
        }
        summary.AddRange(protocol.Select(line => "OBSERVED_MARKER " + line));
        summary.AddRange(["APP_EXIT_RESULT none", "APP_EXIT_MALFORMED none", "MISSING_MARKERS none", "OUT_OF_ORDER_MARKERS none",
            "DUPLICATE_UNIQUE_MARKERS none", "FORBIDDEN_MARKER_HIT none", "FAIL_REASON none", "SERIAL_TAIL_LIMIT lines=8 chars=240"]);
        foreach (string line in serial.Split("\r\n", StringSplitOptions.None).Where(line => line.Trim().Length != 0).TakeLast(8))
        {
            string value = line.Trim();
            summary.Add("SERIAL_TAIL_LINE " + (value.Length > 240 ? value[..237] + "..." : value));
        }
        File.WriteAllText(summaryPath, string.Join("\r\n", summary) + "\r\n", new UTF8Encoding(true));
        void Check(string[] records, bool accepted)
        {
            var result = new ProcessResult(0, string.Join('\n', records) + "\n", "", false, false, false, TimeSpan.Zero);
            try
            {
                HostCheck.Equal(0, QemuTranscripts.ValidateRun(root, options, plan, result, TextWriter.Null));
                HostCheck.True(accepted, "forged complete runner output accepted");
            }
            catch (ToolFailure) when (!accepted) { }
        }
        Check(runner, true);
        Check(runner[2..], false);
        Check([.. runner, backend[0]], false);
        Check(runner.Where((_, i) => i != 17).ToArray(), false);
        Check([.. runner, peer], false);
        string[] reordered = [.. runner];
        (reordered[16], reordered[17]) = (reordered[17], reordered[16]);
        Check(reordered, false);
        foreach ((string from, string to) in new[] { ("complete=True", "complete=False"), ("stopped=True", "stopped=False"),
            ("arpReplies=21", "arpReplies=20"), ("echoReplies=12", "echoReplies=11"), ("tcpPackets=80", "tcpPackets=79"),
            ("tcpPackets=80", "tcpPackets=81"), ("tcpPackets=80", "tcpPackets=080") })
        {
            string[] bad = [.. runner];
            bad[17] = peer.Replace(from, to, StringComparison.Ordinal);
            Check(bad, false);
        }
        foreach (string suffix in new[] { " extra", " ", "\0", "\u001b[0m" })
        {
            string[] bad = [.. runner];
            bad[17] += suffix;
            Check(bad, false);
        }
    }
}
