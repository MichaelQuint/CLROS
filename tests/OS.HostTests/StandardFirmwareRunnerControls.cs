using System.Text;
using OS.Tooling;

// Independent finite runner/summary records corresponding to a literal firmware protocol.
internal sealed class StandardFirmwareRunnerControls
{
    private readonly string root;
    private readonly QemuSmokeOptions options;
    private readonly QemuSmokePlan plan;
    private readonly string[] protocol, ordered, required, forbidden;
    private readonly bool entropy;
    private readonly string? preset;
    public string SerialPath
    {
        get;
    }
    public string SummaryPath
    {
        get;
    }
    public int Cases
    {
        get; private set;
    }
    public StandardFirmwareRunnerControls(string root, string[] protocol, string[] ordered, string[] forbidden, string flag, bool entropy = false, string? preset = null)
    {
        this.root = root;
        this.protocol = protocol;
        this.ordered = ordered;
        this.forbidden = forbidden;
        this.entropy = entropy;
        this.preset = preset;
        SerialPath = Path.Combine(root, "serial.log");
        SummaryPath = Path.Combine(root, "summary.log");
        required = new[] { "OS_BOOT_OK", "TEST_PASS boot.sequence", "OS_SMOKE_PASS", "UEFI_MEMORY_MAP_OWNED", "BOOT_HANDOFF_READY", "UEFI_MEMORY_MAP_NORMALIZED", "BOOT_MEMORY_MAP_ACCEPTED", "BOOT_MEMORY_REGIONS_READY", "BOOT_MEMORY_REGION_COUNT", "BOOT_PAGE_ALLOCATOR_READY", "BOOT_ACPI_RSDP_HANDOFF_READY", "UEFI_ACPI_RSDP_READY" }.Concat(ordered).Distinct(StringComparer.Ordinal).ToArray();
        options = QemuSmokeOptions.Parse(["--boot-policy", "Diagnostic", "--build-efi", "--image", "image.raw", "--serial-log", SerialPath, "--summary-log", SummaryPath, "-" + flag, .. entropy ? new[] { "--firmware-rng-source", "virtio" } : Array.Empty<string>(), .. preset is null ? Array.Empty<string>() : new[] { "--marker-preset", preset }]);
        plan = QemuProfiles.Create(options);
    }
    public byte[] Frame(bool shortened) => [.. FirmwareTranscriptControls.Prefix, .. FirmwareTranscriptControls.Bytes(protocol), .. shortened ? FirmwareTranscriptControls.ShortSuffix : FirmwareTranscriptControls.Suffix];
    public string[] Runner(bool shortened = false)
        => ["QEMU_ACCELERATOR " + options.Accelerator, "QEMU_MACHINE " + QemuContracts.MachineArgument(options.Accelerator), .. RunnerBody(shortened)];
    private string[] RunnerBody(bool shortened)
    {
        byte[] bytes = Frame(shortened);
        return ["QEMU_DISCOVERY result=qemu.exe checked=qemu.exe", "OVMF_DISCOVERY result=ovmf.fd checked=ovmf.fd", "TEST_START qemu.smoke", "IMAGE_ARTIFACT image.raw", "SERIAL_LOG " + SerialPath, "OVMF_CODE ovmf.fd", "WORKLOAD_PROFILE SingleMinimalApi",
            .. entropy ? new[] { "FIRMWARE_RNG_SOURCE virtio" } : Array.Empty<string>(), "MARKER_PRESETS " + (preset ?? "none"), "REQUIRED_MARKERS " + string.Join(';', required), "ORDERED_MARKERS " + string.Join(';', ordered), "UNIQUE_MARKERS " + string.Join(';', ordered), "FORBIDDEN_MARKERS " + string.Join(';', forbidden),
            $"SERIAL_MARKER_STREAM status=Passed terminal=Success bytes={bytes.Length} lines={protocol.Length + (shortened ? 4 : 5)} invalidBytes={(shortened ? 15 : 207)} invalidLines={(shortened ? 1 : 2)} orderedSeen={ordered.Length} outOfOrder=0 uniqueSeen={ordered.Length} duplicateUnique=0",
            "SERIAL_MARKER_DRAIN milliseconds=750", "QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=-1", "SUMMARY_LOG " + SummaryPath, "TEST_PASS qemu.smoke"];
    }
    public string Summary(bool shortened = false)
    {
        var runner = RunnerBody(shortened);
        List<string> summary = ["QEMU_SMOKE_STATUS pass"];
        foreach (int index in entropy ? new[] { 3, 4, 6, 7, 8, 9, 10, 11, 12 } : new[] { 3, 4, 6, 7, 8, 9, 10, 11 })
        {
            summary.Add(runner[index]);
            if (index == 6)
                summary.AddRange(["QEMU_ACCELERATOR " + options.Accelerator, "QEMU_MACHINE " + QemuContracts.MachineArgument(options.Accelerator)]);
        }
        summary.AddRange(protocol.Select(line => "OBSERVED_MARKER " + line));
        summary.AddRange(["APP_EXIT_RESULT none", "APP_EXIT_MALFORMED none", "MISSING_MARKERS none", "OUT_OF_ORDER_MARKERS none", "DUPLICATE_UNIQUE_MARKERS none", "FORBIDDEN_MARKER_HIT none", "FAIL_REASON none", "SERIAL_TAIL_LIMIT lines=8 chars=240"]);
        foreach (string line in Encoding.UTF8.GetString(Frame(shortened)).Split("\r\n", StringSplitOptions.None).Where(line => line.Trim().Length > 0).TakeLast(8))
        {
            string value = line.Trim();
            summary.Add("SERIAL_TAIL_LINE " + (value.Length > 240 ? value[..237] + "..." : value));
        }
        return string.Join("\r\n", summary) + "\r\n";
    }
    public void Save(bool shortened = false)
    {
        File.WriteAllBytes(SerialPath, Frame(shortened));
        File.WriteAllText(SummaryPath, Summary(shortened), new UTF8Encoding(true));
    }
    public void Check(string[] lines, bool expected)
    {
        bool accepted = true;
        try
        {
            QemuTranscripts.AssertStandardRunnerResult(root, options, plan, lines);
        }
        catch (ToolFailure) when (!expected) { accepted = false; }
        if (accepted != expected)
            throw new InvalidOperationException("runner case " + Cases + " expected=" + expected);
        Cases++;
    }
    public void CompleteMatrix(bool shortSuffix = true)
    {
        Save();
        string[] runner = Runner();
        Check(runner, true);
        int stream = entropy ? 15 : 14, drain = stream + 1, process = stream + 2;
        Check(runner[2..], false);
        Check([runner[1], runner[0], .. runner[2..]], false);
        Check([.. runner, runner[0]], false);
        Check([.. runner, runner[1]], false);
        foreach (string bad in new[] { "QEMU_ACCELERATOR unknown", "QEMU_ACCELERATOR " + (options.Accelerator == "whpx" ? "tcg" : "whpx") })
        {
            string[] copy = [.. runner];
            copy[0] = bad;
            Check(copy, false);
        }
        foreach (string exit in new[] { "0", "-2147483648", "2147483647" })
        {
            string[] copy = [.. runner];
            copy[process] = "QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=" + exit;
            Check(copy, true);
        }
        foreach (string exit in new[] { "none", "-0", "01", "+1", "2147483648", "-2147483649" })
        {
            string[] copy = [.. runner];
            copy[process] = "QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=" + exit;
            Check(copy, false);
        }
        for (int i = 0; i < runner.Length; i++)
        {
            string[] copy = [.. runner];
            copy[i] += "\0";
            Check(copy, false);
        }
        foreach (var pair in new[] { (stream, "invalidBytes=207", "invalidBytes=208"), (stream, "lines=" + (protocol.Length + 5), "lines=" + (protocol.Length + 4)), (stream, "orderedSeen=" + ordered.Length, "orderedSeen=" + (ordered.Length - 1)), (stream, "status=Passed", "status=Timeout"), (drain, "milliseconds=750", "milliseconds=749"), (drain, "milliseconds", "Milliseconds"), (process, "runner-terminated", "exited"), (process, "runner-terminated", "cleanup-failed") })
        {
            string[] copy = [.. runner];
            copy[pair.Item1] = copy[pair.Item1].Replace(pair.Item2, pair.Item3);
            Check(copy, false);
        }
        foreach (string extra in new[] { "TEST_FAIL x", "OS_SMOKE_FAIL", "QEMU_PROCESS_CLEANUP_FAILED", "TEST_START qemu.smoke" })
            Check([.. runner, extra], false);
        string summary = Summary();
        foreach (var pair in new[] { ("QEMU_SMOKE_STATUS pass", "QEMU_SMOKE_STATUS fail"), ("FAIL_REASON none", "FAIL_REASON timeout"), ("MISSING_MARKERS none", "MISSING_MARKERS x"), ("DUPLICATE_UNIQUE_MARKERS none", "DUPLICATE_UNIQUE_MARKERS x"), ("SERIAL_LOG " + SerialPath, "SERIAL_LOG different") })
        {
            File.WriteAllText(SummaryPath, summary.Replace(pair.Item1, pair.Item2), new UTF8Encoding(true));
            Check(runner, false);
        }
        File.WriteAllText(SummaryPath, summary + "OS_SMOKE_FAIL\r\n", new UTF8Encoding(true));
        Check(runner, false);
        File.WriteAllText(SummaryPath, summary, new UTF8Encoding(true));
        Check(runner, true);
        foreach (var pair in new[] { ("invalidLines=2", "invalidLines=1"), ("invalidBytes=207", "invalidBytes=15") })
        {
            string[] copy = [.. runner];
            copy[stream] = copy[stream].Replace(pair.Item1, pair.Item2);
            Check(copy, false);
        }
        if (shortSuffix)
        {
            string[] shortRunner = Runner(true);
            File.WriteAllBytes(SerialPath, Frame(true));
            Check(shortRunner, false);
            Save(true);
            Check(shortRunner, true);
            Check(runner, false);
            foreach (var pair in new[] { ("lines=" + (protocol.Length + 4), "lines=" + (protocol.Length + 5)), ("invalidLines=1", "invalidLines=2"), ("invalidBytes=15", "invalidBytes=207") })
            {
                string[] copy = [.. shortRunner];
                copy[stream] = copy[stream].Replace(pair.Item1, pair.Item2);
                Check(copy, false);
            }
            File.WriteAllBytes(SerialPath, Frame(false));
            Check(runner, false);
            Check(shortRunner, false);
            Save();
            Check(runner, true);
        }
        string errorPath = Path.Combine(root, "rejected-stderr.log");
        QemuTranscripts.RequireEmptyNativeError("", errorPath);
        FirmwareTranscriptControls.Reject(() => QemuTranscripts.RequireEmptyNativeError("\r\n", errorPath), "stderr whitespace");
        if (File.ReadAllText(errorPath) != "\r\n")
            throw new InvalidOperationException("stderr evidence lost");
    }
}
