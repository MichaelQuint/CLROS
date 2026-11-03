internal static class QemuTerminalControls
{
    // Labels come from the reviewed shared inventory; the independent allocator
    // fixture values remain explicit and are mutated by the transcript controls.
    public static string Prefix()
    {
        string[] records = OS.Tooling.QemuContracts.M02TerminalPrefix.ToArray();
        records[3] += " 2";
        records[9] += " 2";
        records[10] += " status=Ready regions=2 conventionalPages=10 conventionalBytes=40960 freeNodes=2 freePages=10 freeBytes=40960 pageSize=4096 statusCode=0";
        return string.Join("\r\n", records) + "\r\n";
    }
    public static void AssertRejected(Action action, string reason)
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch (OS.Tooling.ToolFailure) { rejected = true; }
        if (!rejected)
            throw new InvalidOperationException(reason);
    }

    public static void Lifecycle(Action<IReadOnlyList<string>> validate, bool full)
    {
        const string drain = "SERIAL_MARKER_DRAIN milliseconds=750", result = "QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=";
        foreach (string code in new[] { "0", "1", "-1", "-2147483648", "2147483647" })
            validate([drain, result + code]);
        foreach (string code in new[] { "none", "-0", "01", "+1", "2147483648", "-2147483649", "99999999999999999999" })
            AssertRejected(() => validate([drain, result + code]), "invalid native exit " + code);
        foreach (string line in new[] { "QEMU_PROCESS_RESULT disposition=exited exitCode=0", "QEMU_PROCESS_RESULT disposition=cleanup-failed exitCode=0", result + "-1 " })
            AssertRejected(() => validate([drain, line]), "invalid lifecycle " + line);
        if (!full)
            return;
        string[] normal = ["TEST_START qemu.smoke", "IMAGE_ARTIFACT controlled.img", "SERIAL_LOG controlled.log", "OVMF_CODE controlled.fd", "WORKLOAD_PROFILE SingleMinimalApi", "MARKER_PRESETS none", "REQUIRED_MARKERS OS_BOOT_OK|TEST_PASS m02.015", "ORDERED_MARKERS OS_BOOT_OK|TEST_PASS m02.015", "UNIQUE_MARKERS OS_BOOT_OK|TEST_PASS m02.015", "FORBIDDEN_MARKERS OS_SMOKE_FAIL|TEST_FAIL|M02_DOUBLE_FAULT_FAILED", drain, result + "0", "TEST_PASS qemu.smoke"];
        validate(normal);
        foreach (string failure in new[] { "TEST_FAIL qemu.smoke timeout", "OS_SMOKE_FAIL late", "QEMU_PROCESS_CLEANUP_FAILED timeout_ms=2000", "TE\0ST_FAIL late" })
            AssertRejected(() => validate([.. normal, failure]), "failure record accepted");
        string[][] invalid = [[result + "0"], [drain], [drain, drain, result + "0"], ["SERIAL_MARKER_DRAIN milliseconds=749", result + "0"], [drain, result + "0", result + "0"], [drain, result + "0", "TEST_FAIL qemu.smoke timeout"]];
        foreach (string[] lines in invalid)
            AssertRejected(() => validate(lines), "invalid lifecycle");
    }
}
