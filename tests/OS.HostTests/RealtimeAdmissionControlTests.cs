using OS.Kernel.Diagnostics;
using OS.Tooling;

internal static class RealtimeAdmissionControlTests
{
    private static string[] Tail(bool invalid = false) => [
        "TEST_START m03.007","M16_005_PREPARE_REJECTED owned-backing=released",
        "M16_004_PREPARE_REJECTED owned-backing=released","M16_002_STAGE_REJECTED executable=false backing=released",
        "M16_002_REJECTION_HANDLES closed=2","TEST_START m16.019",
        invalid?"M16_019_UTC_REJECT case=2 sourceStatus=0 month=0 available=0 firmware=1 entered=0 ready=0":
            "M16_019_UTC_REJECT case=1 sourceStatus=9223372036854775811 month=10 available=0 firmware=1 entered=0 ready=0",
        "TEST_FAIL m16.019 admission","TEST_FAIL m03.007 preparation=96/4"];
    private static void Reject(Action action, string? reason = null)
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch (ToolFailure e)
        {
            rejected = reason is null || e.Reason == reason;
        }
        HostCheck.True(rejected);
    }
    private static EfiProfile Profile(string control, Dictionary<string, bool>? extra = null, string? boot = null)
    {
        var flags = extra ?? new Dictionary<string, bool>();
        flags["EnableM16019" + control + "Probe"] = true;
        return EfiProfile.Create(boot, flags);
    }

    [HostTest("UTC rejection controls default off and select only the existing diagnostic web chain")]
    private static void Defaults()
    {
        var normal = EfiProfile.Create(null, new Dictionary<string, bool>());
        HostCheck.True(!Probes.EnableM16019UnavailableProbe && !Probes.EnableM16019InvalidProbe &&
            !normal.IsEnabled("M16019Unavailable") && !normal.IsEnabled("M16019Invalid"));
        foreach (string control in new[] { "Unavailable", "Invalid" })
        {
            var p = Profile(control);
            HostCheck.Equal("Diagnostic", p.BootPolicy);
            foreach (string parent in new[] { "M10004", "M16012", "M16011", "M16009", "M16004", "M07026", "M03FirstRealExitBootServices" })
                HostCheck.True(p.IsEnabled(parent));
            HostCheck.True(!p.IsEnabled("M16019" + (control == "Invalid" ? "Unavailable" : "Invalid")));
            HostCheck.True(EfiContracts.ProbeSymbols["EnableM16019" + control + "Probe"] == "M16_019_" + control.ToUpperInvariant() + "_PROBE");
        }
    }
    [HostTest("UTC rejection profiles reject unions false parents normal boot and ordinary smoke success paths")]
    private static void Profiles()
    {
        Reject(() => Profile("Unavailable", new() { ["EnableM16019InvalidProbe"] = true }), "realtime_rejection_controls_conflict");
        foreach (string control in new[] { "Unavailable", "Invalid" })
        {
            Reject(() => Profile(control, new() { ["EnableM10004Probe"] = false }), "realtime_rejection_requires_web_profile");
            Reject(() => Profile(control, new() { ["EnableM16012Probe"] = false }));
            Reject(() => Profile(control, boot: "VerifiedExit"));
            Reject(() => Profile(control, new() { ["EnableM16013Probe"] = true }));
            Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse("-EnableM16019" + control + "Probe")), "realtime_rejection_requires_separate_capture");
        }
    }
    [HostTest("UTC unavailable and invalid tails require actual preparation refusal without entry or readiness")]
    private static void RejectionTails()
    {
        var unavailable = QemuTranscripts.InspectRealtimeAdmissionRejectionForTests(Tail(), "Unavailable");
        var invalid = QemuTranscripts.InspectRealtimeAdmissionRejectionForTests(Tail(true), "Invalid");
        HostCheck.True(unavailable.Control == 1 && unavailable.SourceStatus == 0x8000000000000003UL && unavailable.Month == 10 &&
            invalid.Control == 2 && invalid.SourceStatus == 0 && invalid.Month == 0 && invalid.TableMemoryType == 4);
        Reject(() => QemuTranscripts.InspectRealtimeAdmissionRejectionForTests(Tail(), "Invalid"));
        Reject(() => QemuTranscripts.InspectRealtimeAdmissionRejectionForTests(Tail(true), "Unavailable"));
        Reject(() => QemuTranscripts.InspectRealtimeAdmissionRejectionForTests(Tail(), "unavailable"));
    }
    [HostTest("UTC rejection tail inventory forbids omitted duplicate cleanup failure and extra success records")]
    private static void Inventory()
    {
        var rows = Tail();
        for (int i = 0; i < rows.Length; i++)
            Reject(() => QemuTranscripts.InspectRealtimeAdmissionRejectionForTests(rows.Where((_, n) => n != i).ToArray(), "Unavailable"));
        foreach (string extra in new[] { rows[6], "TEST_PASS m16.019", "M10_004_LIVE", "CLROS_M10_004_READY" })
            Reject(() => QemuTranscripts.InspectRealtimeAdmissionRejectionForTests(rows.Append(extra).ToArray(), "Unavailable"));
        foreach (var (at, before, after) in new[]{(1,"released","reserved-release-failed"),(2,"released","reserved-release-failed"),
            (3,"executable=false","executable=true"),(4,"closed=2","closed=1"),(7,"TEST_FAIL","TEST_PASS"),(8,"96/4","95/4")})
        {
            var changed = rows.ToArray();
            changed[at] = changed[at].Replace(before, after, StringComparison.Ordinal);
            Reject(() => QemuTranscripts.InspectRealtimeAdmissionRejectionForTests(changed, "Unavailable"));
        }
    }
    [HostTest("UTC rejection observations reject wrong case status availability entry firmware and malformed numeric values")]
    private static void Observations()
    {
        foreach (var (before, after) in new[]{("case=1","case=3"),("sourceStatus=9223372036854775811","sourceStatus=0"),
            ("month=10","month=0"),("month=10","month=13"),("available=0","available=1"),("firmware=1","firmware=0"),
            ("entered=0","entered=1"),("ready=0","ready=1"),("case=1","case=01"),("month=10","month=18446744073709551616")})
        {
            var changed = Tail();
            changed[6] = changed[6].Replace(before, after, StringComparison.Ordinal);
            Reject(() => QemuTranscripts.InspectRealtimeAdmissionRejectionForTests(changed, "Unavailable"));
        }
        foreach (string memory in new[] { "00", "016", "16", "-1", "4\n", "4294967296" })
        {
            var changed = Tail();
            changed[8] = "TEST_FAIL m03.007 preparation=96/" + memory;
            Reject(() => QemuTranscripts.InspectRealtimeAdmissionRejectionForTests(changed, "Unavailable"));
        }
    }
    [HostTest("UTC full capture inspection rejects invented firmware bytes before any negative report")]
    private static void Firmware()
    {
        string path = Path.Combine(Path.GetTempPath(), "clros-utc-" + Guid.NewGuid().ToString("N") + ".log");
        try
        {
            using (var file = new FileStream(path, FileMode.CreateNew))
                file.Write(new byte[QemuTranscripts.FirmwarePrefixBytes + 128]);
            Reject(() => QemuTranscripts.AssertRealtimeAdmissionRejectionCapture(path, "Unavailable"), "realtime_rejection_firmware_prefix");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
