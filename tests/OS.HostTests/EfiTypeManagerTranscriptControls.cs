using System.Text;
using OS.Tooling;

internal static class EfiTypeManagerTranscriptControls
{
    internal static void Run(string directory, string[] lines)
    {
        var controls = new FirmwareTranscriptControls(directory, path => QemuTranscripts.AssertM04TypeManagerTranscript(path));
        byte[] good = FirmwareTranscriptControls.Bytes(lines);
        void Check(byte[] bytes, bool expected, byte[]? prefix = null, byte[]? suffix = null)
            => controls.Check(bytes, expected, prefix, suffix ?? []);
        Check(good, true);
        for (int i = 0; i < lines.Length; i++)
        {
            foreach (string replacement in new[] { "", lines[i] + " ", " " + lines[i], lines[i] + " extra=1", lines[i] + "\0" })
            { string[] bad = [..lines]; bad[i] = replacement; Check(FirmwareTranscriptControls.Bytes(bad), false); }
            string[] lower = [..lines]; lower[i] = lower[i].ToLowerInvariant();
            if (lower[i] != lines[i]) Check(FirmwareTranscriptControls.Bytes(lower), false);
            Check(FirmwareTranscriptControls.Bytes([..lines.Take(i + 1), ..lines.Skip(i)]), false);
        }
        string goodText = Encoding.ASCII.GetString(good);
        foreach ((string from, string to) in new (string, string)[] {
            ("required=1024","required=4065"),("capacity=4096","capacity=4095"),("storage=1060864","storage=1060865"),
            ("handle=1061136","handle=1061137"),("slotBytes=16","slotBytes=8"),("index=0","index=1"),
            ("padding=0","padding=4294967296"),("abiBytes=48","abiBytes=40"),("classlib=0","classlib=1"),
            ("classlibCount=0","classlibCount=1"),("tailCanaries=true","tailCanaries=false"),("status=7","status=9"),
            ("id=213 status=1","id=213 status=11"),("id=0 status=11 start=0","id=0 status=11 start=1"),
            ("base=1048576","base=18446744073709551615"),("bytes=32768","bytes=4294967296"),
            ("slot=1056768","slot=1060864"),("tls=1057024","tls=0"),("regions=4","regions=3"),("freeBytes=81920","freeBytes=81921") })
        {
            string bad = goodText.Replace(from, to, StringComparison.Ordinal);
            HostCheck.True(bad != goodText, "mutation changes fixture " + from);
            Check(Encoding.ASCII.GetBytes(bad), false);
        }
        string[] reordered = [..lines]; (reordered[25], reordered[26]) = (reordered[26], reordered[25]);
        Check(FirmwareTranscriptControls.Bytes(reordered), false);
        Check(Encoding.ASCII.GetBytes(goodText.Replace("\r\n", "\n", StringComparison.Ordinal)), false);
        Check(good[..^1], false); Check([..good, 0], false); Check([..good, 255], false);
        Check(good, false, [255]); Check(good, false, Encoding.ASCII.GetBytes("arbitrary"));
        Check(good, false, suffix: [65]); Check(good, true);
        Runner(directory, controls.Path, lines);
        string errorPath = Path.Combine(directory, "rejected-stderr.log");
        QemuTranscripts.RequireEmptyNativeError("", errorPath);
        FirmwareTranscriptControls.Reject(() => QemuTranscripts.RequireEmptyNativeError("\r\n", errorPath), "whitespace stderr");
        HostCheck.True(File.ReadAllText(errorPath) == "\r\n", "stderr evidence retained exactly");
        Console.WriteLine("TRANSCRIPT_CONTROLS type-manager raw=" + controls.Cases + " stderrEmpty=true stderrWhitespaceRejected=true");
    }

    private static void Runner(string root, string serialPath, string[] protocol)
    {
        string summaryPath = Path.Combine(root, "summary.log");
        var options = QemuSmokeOptions.Parse(["--BootPolicy", "Diagnostic", "--BuildEfi", "--EnableM01LoadedImageStackProbe", "--EnableM04TypeManagerModuleRegistrationProbe", "--Image", "image.raw", "--SerialLog", serialPath, "--SummaryLog", summaryPath]);
        var plan = QemuProfiles.Create(options);
        byte[] raw = File.ReadAllBytes(serialPath);
        string serial = Encoding.UTF8.GetString(raw);
        int invalidBytes = raw.Count(b => b is not (9 or 10 or 13) && (b < 32 || b > 126));
        string[] runner = ["QEMU_DISCOVERY result=qemu.exe checked=qemu.exe", "OVMF_DISCOVERY result=ovmf.fd checked=ovmf.fd", "TEST_START qemu.smoke",
            "IMAGE_ARTIFACT image.raw", "SERIAL_LOG " + serialPath, "OVMF_CODE ovmf.fd", "WORKLOAD_PROFILE " + options.WorkloadProfile,
            "MARKER_PRESETS none", "REQUIRED_MARKERS " + string.Join(';', plan.Required), "ORDERED_MARKERS " + string.Join(';', plan.Ordered),
            "UNIQUE_MARKERS " + string.Join(';', plan.Unique), "FORBIDDEN_MARKERS " + string.Join(';', plan.Forbidden),
            $"SERIAL_MARKER_STREAM status=Passed terminal=Success bytes={raw.Length} lines={protocol.Length + 2} invalidBytes={invalidBytes} invalidLines=1 orderedSeen={plan.Ordered.Count} outOfOrder=0 uniqueSeen={plan.Unique.Count} duplicateUnique=0",
            "SERIAL_MARKER_DRAIN milliseconds=750", "QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=-1", "SUMMARY_LOG " + summaryPath, "TEST_PASS qemu.smoke"];
        List<string> summary = ["QEMU_SMOKE_STATUS pass"];
        foreach (int i in new[] { 3, 4, 6, 7, 8, 9, 10, 11 }) summary.Add(runner[i]);
        summary.AddRange(protocol.Select(line => "OBSERVED_MARKER " + line));
        summary.AddRange(["APP_EXIT_RESULT none", "APP_EXIT_MALFORMED none", "MISSING_MARKERS none", "OUT_OF_ORDER_MARKERS none", "DUPLICATE_UNIQUE_MARKERS none", "FORBIDDEN_MARKER_HIT none", "FAIL_REASON none", "SERIAL_TAIL_LIMIT lines=8 chars=240"]);
        foreach (string line in serial.Split("\r\n", StringSplitOptions.None).Where(line => line.Trim().Length != 0).TakeLast(8))
        { string value = line.Trim(); summary.Add("SERIAL_TAIL_LINE " + (value.Length > 240 ? value[..237] + "..." : value)); }
        string text = string.Join("\r\n", summary) + "\r\n";
        void Save(string value) => File.WriteAllText(summaryPath, value, new UTF8Encoding(true));
        int count = 0;
        void Check(string[] records, bool expected)
        {
            bool accepted = true;
            try { QemuTranscripts.AssertStandardRunnerResult(root, options, plan, records); }
            catch (ToolFailure) when (!expected) { accepted = false; }
            HostCheck.Equal(expected, accepted, "type manager runner control " + count++);
        }
        Save(text); Check(runner, true);
        foreach (string exit in new[] { "0", "-2147483648", "2147483647", "none", "-0", "01", "+1", "2147483648", "-2147483649" })
        {
            string[] bad = [..runner]; bad[14] = "QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=" + exit;
            Check(bad, exit is "0" or "-2147483648" or "2147483647");
        }
        for (int i = 0; i < runner.Length; i++) { string[] bad = [..runner]; bad[i] += "\0"; Check(bad, false); }
        foreach ((int row, string from, string to) in new (int, string, string)[] {
            (12, "invalidBytes=" + invalidBytes, "invalidBytes=" + (invalidBytes + 1)), (12, "lines=" + (protocol.Length + 2), "lines=" + (protocol.Length + 1)),
            (12, "orderedSeen=" + plan.Ordered.Count, "orderedSeen=" + (plan.Ordered.Count - 1)), (12,"status=Passed","status=Timeout"),
            (13,"milliseconds=750","milliseconds=749"),(13,"milliseconds","Milliseconds"),(14,"runner-terminated","exited"),(14,"runner-terminated","cleanup-failed") })
        { string[] bad = [..runner]; bad[row] = bad[row].Replace(from, to, StringComparison.Ordinal); Check(bad, false); }
        foreach (string extra in new[] { "TEST_FAIL x", "OS_SMOKE_FAIL", "QEMU_PROCESS_CLEANUP_FAILED", "TEST_START qemu.smoke" }) Check([..runner, extra], false);
        foreach ((string from, string to) in new (string, string)[] { ("QEMU_SMOKE_STATUS pass","QEMU_SMOKE_STATUS fail"), ("FAIL_REASON none","FAIL_REASON timeout"),
            ("MISSING_MARKERS none","MISSING_MARKERS x"),("DUPLICATE_UNIQUE_MARKERS none","DUPLICATE_UNIQUE_MARKERS x"),("SERIAL_LOG " + serialPath,"SERIAL_LOG different") })
        { string changed = text.Replace(from, to, StringComparison.Ordinal); HostCheck.True(changed != text, "summary mutation changes fixture"); Save(changed); Check(runner, false); }
        Save(text + "OS_SMOKE_FAIL\r\n"); Check(runner, false); Save(text); Check(runner, true);
        Console.WriteLine("TRANSCRIPT_CONTROLS type-manager runner=" + count);
    }
}
